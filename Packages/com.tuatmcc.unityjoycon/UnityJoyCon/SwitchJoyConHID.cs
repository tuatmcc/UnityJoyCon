using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace UnityJoyCon
{
    public abstract class SwitchJoyConHID : InputDevice, IInputStateCallbackReceiver
    {
        protected const int VendorId = 0x057e;
        protected const int ProductIdLeft = 0x2006;
        protected const int ProductIdRight = 0x2007;
        private const double CommandIntervalSeconds = 0.1;
        private const byte StickParameterLength = 18;
        private const byte StickCalibrationLength = 9;
        private const byte IMUParameterLength = 6;
        private const byte IMUCalibrationLength = 24;

        private static readonly List<SwitchJoyConHID> AllDevices = new();
        private byte _commandPacketNumber;

        private bool _haveParsedHIDDescriptor;
        private HID.HIDDeviceDescriptor _hidDeviceDescriptor;
        private IMUCalibrationState _imuCalibration;
        private bool _imuEnabled;

        private double _lastCommandSentTime;
        private double _lastStandardInputReceivedTime;

        private StickCalibrationState _stickCalibration;

        public virtual Side Side => HIDDeviceDescriptor.productId switch
        {
            ProductIdLeft => Side.Left,
            ProductIdRight => Side.Right,
            _ => throw new InvalidOperationException("Invalid product ID for Switch Joy-Con.")
        };

        public HID.HIDDeviceDescriptor HIDDeviceDescriptor
        {
            get
            {
                if (_haveParsedHIDDescriptor) return _hidDeviceDescriptor;

                _hidDeviceDescriptor = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
                _haveParsedHIDDescriptor = true;
                return _hidDeviceDescriptor;
            }
        }

        void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
        {
            HandleStateEvent(eventPtr);
        }

        void IInputStateCallbackReceiver.OnNextUpdate()
        {
            if (!ShouldSendCommand()) return;

            if (TryChangeReportMode()) return;

            if (TryRequestStickParameters()) return;
            if (TryRequestStickCalibration()) return;

            if (TryRequestIMUParameters()) return;
            if (TryRequestIMUCalibration()) return;

            if (TryEnableIMU()) return;
        }

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
        {
            return false;
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            AllDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            AllDevices.Remove(this);
            if (current == this)
                current = null;
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            accelerometer = GetChildControl<Vector3Control>("accelerometer");
            gyroscope = GetChildControl<Vector3Control>("gyroscope");
        }

        private bool ShouldSendCommand()
        {
            return !(lastUpdateTime < _lastCommandSentTime + CommandIntervalSeconds);
        }

        private unsafe void HandleStateEvent(InputEventPtr eventPtr)
        {
            if (eventPtr.type != StateEvent.Type) return;

            var stateEvent = StateEvent.From(eventPtr);
            if (stateEvent->stateFormat != SwitchHIDGenericInputReport.Format) return;

            var genericReport = (SwitchHIDGenericInputReport*)stateEvent->state;
            var reportId = (ReportId)genericReport->reportId;

            switch (reportId)
            {
                case ReportId.StandardInput:
                    HandleStandardInput((SwitchStandardInputReport*)stateEvent->state, eventPtr);
                    break;
                case ReportId.SubCommandReply:
                    HandleSubCommandReply((SwitchStandardInputReport*)stateEvent->state);
                    break;
            }
        }

        private unsafe void HandleStandardInput(SwitchStandardInputReport* report, InputEventPtr eventPtr)
        {
            _lastStandardInputReceivedTime = lastUpdateTime;
            _imuEnabled = report->IsEnabledIMU();
            if (!_imuEnabled) return;
            if (!_stickCalibration.IsReady) return;
            if (!_imuCalibration.IsReady) return;

            switch (Side)
            {
                case Side.Left:
                    var leftState = report->ToLeftHIDInputReport(
                        _stickCalibration.ToNormalizationParameters(),
                        _imuCalibration.ToNormalizationParameters());
                    InputState.Change(this, leftState, eventPtr: eventPtr);
                    break;
                case Side.Right:
                    var rightState = report->ToRightHIDInputReport(
                        _stickCalibration.ToNormalizationParameters(),
                        _imuCalibration.ToNormalizationParameters());
                    InputState.Change(this, rightState, eventPtr: eventPtr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private unsafe void HandleSubCommandReply(SwitchStandardInputReport* report)
        {
            if ((report->subCommandReply.ack & 0x80) == 0)
            {
                Debug.LogError($"Joy-Con sub command 0x{report->subCommandReply.subCommandId:X2} NAK received.");
                return;
            }

            if (report->subCommandReply.subCommandId != (byte)SubCommandBase.SubCommand.ReadSPIFlash) return;

            var address = ReadAddress(report->subCommandReply.data);
            var length = report->subCommandReply.data[4];
            var payload = report->subCommandReply.data + 5;

            HandleSpiFlashReply(address, length, payload);
        }

        private unsafe void HandleSpiFlashReply(uint address, byte length, byte* payload)
        {
            if (address == (uint)GetStickParametersAddress() && length == StickParameterLength)
            {
                _stickCalibration.ApplyParameters(payload);
                return;
            }

            if (address == (uint)GetStickUserCalibrationAddress() && length == StickCalibrationLength)
            {
                _stickCalibration.MarkUserCalibrationLoaded();
                if (IsAllPayloadUnset(payload, StickCalibrationLength)) return;

                _stickCalibration.ApplyCalibration(payload, Side);
                return;
            }

            if (address == (uint)GetStickFactoryCalibrationAddress() && length == StickCalibrationLength)
                _stickCalibration.ApplyCalibration(payload, Side);

            if (address == (uint)SwitchReadSPIFlashOutput.Address.IMUParameters && length == IMUParameterLength)
                _imuCalibration.ApplyParameters(payload);

            if (address == (uint)SwitchReadSPIFlashOutput.Address.IMUUserCalibration && length == IMUCalibrationLength)
            {
                _imuCalibration.MarkUserCalibrationLoaded();
                if (IsAllPayloadUnset(payload, IMUCalibrationLength)) return;

                _imuCalibration.ApplyCalibration(payload);
                return;
            }

            if (address == (uint)SwitchReadSPIFlashOutput.Address.IMUFactoryCalibration &&
                length == StickParameterLength)
                _imuCalibration.ApplyCalibration(payload);
        }

        private static unsafe uint ReadAddress(byte* data)
        {
            return data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16) | ((uint)data[3] << 24);
        }

        private static unsafe bool IsAllPayloadUnset(byte* payload, int length)
        {
            for (var i = 0; i < length; i++)
                if (payload[i] != 0xFF)
                    return false;

            return true;
        }

        private byte GetNextCommandPacketNumber()
        {
            var nextPacketNumber = _commandPacketNumber;
            _commandPacketNumber = (byte)((_commandPacketNumber + 1) % 0x10);
            return nextPacketNumber;
        }

        private bool TryChangeReportMode()
        {
            const double standardReportTimeoutSeconds = 2.0;
            // 一定時間標準入力レポートが受信されていない場合、レポートモードを再設定する
            if (!(lastUpdateTime > _lastStandardInputReceivedTime + standardReportTimeoutSeconds)) return false;

            var configureOutputModeCommand =
                SwitchGenericSubCommandOutput.Create(GetNextCommandPacketNumber(),
                    SubCommandBase.SubCommand.ConfigureReportMode, 0x30);
            ExecuteCommand(ref configureOutputModeCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestStickParameters()
        {
            if (_stickCalibration.ParametersLoaded) return false;

            var stickParametersCommand =
                SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(), GetStickParametersAddress(),
                    StickParameterLength);
            ExecuteCommand(ref stickParametersCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestStickCalibration()
        {
            if (_stickCalibration.CalibrationLoaded) return false;

            if (!_stickCalibration.UserCalibrationLoaded)
            {
                var stickUserCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(), GetStickUserCalibrationAddress(),
                        StickCalibrationLength);
                ExecuteCommand(ref stickUserCalibrationCommand);
            }
            else
            {
                var stickCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(), GetStickFactoryCalibrationAddress(),
                        StickCalibrationLength);
                ExecuteCommand(ref stickCalibrationCommand);
            }

            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestIMUParameters()
        {
            if (_imuCalibration.ParametersLoaded) return false;

            var imuParametersCommand =
                SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(),
                    SwitchReadSPIFlashOutput.Address.IMUParameters,
                    IMUParameterLength);
            ExecuteCommand(ref imuParametersCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestIMUCalibration()
        {
            if (_imuCalibration.CalibrationLoaded) return false;

            if (!_imuCalibration.UserCalibrationLoaded)
            {
                var imuUserCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(),
                        SwitchReadSPIFlashOutput.Address.IMUUserCalibration,
                        IMUCalibrationLength);
                ExecuteCommand(ref imuUserCalibrationCommand);
            }
            else
            {
                var imuCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(GetNextCommandPacketNumber(),
                        SwitchReadSPIFlashOutput.Address.IMUFactoryCalibration, 24);
                ExecuteCommand(ref imuCalibrationCommand);
            }

            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryEnableIMU()
        {
            if (_imuEnabled) return false;

            var enableImuCommand = SwitchGenericSubCommandOutput.Create(GetNextCommandPacketNumber(),
                SubCommandBase.SubCommand.ConfigureIMU, 0x01);
            ExecuteCommand(ref enableImuCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private SwitchReadSPIFlashOutput.Address GetStickUserCalibrationAddress()
        {
            return Side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickUserCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickFactoryCalibrationAddress()
        {
            return Side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickFactoryCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickParametersAddress()
        {
            return Side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickParameters,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickParameters,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private enum ReportId : byte
        {
            StandardInput = 0x30,
            SubCommandReply = 0x21
        }

        // ReSharper disable InconsistentNaming
        public Vector3Control accelerometer { get; private set; }
        public Vector3Control gyroscope { get; private set; }

        public static SwitchJoyConHID current { get; private set; }

        public new static IReadOnlyList<SwitchJoyConHID> all => AllDevices;
        // ReSharper restore InconsistentNaming
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (L)", stateType = typeof(SwitchJoyConLeftHIDInputState))]
    public class SwitchJoyConLeftHID : SwitchJoyConHID
    {
        private static readonly List<SwitchJoyConLeftHID> AllDevices = new();

        static SwitchJoyConLeftHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConLeftHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdLeft));
        }

        public override Side Side => Side.Left;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            AllDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            AllDevices.Remove(this);
            if (current == this)
                current = null;
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            dpad = GetChildControl<DpadControl>("dpad");
            smallLeftShoulder = GetChildControl<ButtonControl>("smallLeftShoulder");
            smallRightShoulder = GetChildControl<ButtonControl>("smallRightShoulder");
            leftShoulder = GetChildControl<ButtonControl>("leftShoulder");
            leftTrigger = GetChildControl<ButtonControl>("leftTrigger");
            selectButton = GetChildControl<ButtonControl>("select");
            captureButton = GetChildControl<ButtonControl>("capture");
            leftStickButton = GetChildControl<ButtonControl>("leftStickPress");
            leftStick = GetChildControl<StickControl>("leftStick");
        }

        // ReSharper disable InconsistentNaming
        public DpadControl dpad { get; private set; }
        public ButtonControl smallLeftShoulder { get; private set; }
        public ButtonControl smallRightShoulder { get; private set; }
        public ButtonControl leftShoulder { get; private set; }
        public ButtonControl leftTrigger { get; private set; }
        public ButtonControl selectButton { get; private set; }
        public ButtonControl captureButton { get; private set; }
        public ButtonControl leftStickButton { get; private set; }
        public StickControl leftStick { get; private set; }

        public new static SwitchJoyConLeftHID current { get; private set; }

        public new static IReadOnlyList<SwitchJoyConLeftHID> all => AllDevices;
        // ReSharper restore InconsistentNaming
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (R)", stateType = typeof(SwitchJoyConRightHIDInputState))]
    public class SwitchJoyConRightHID : SwitchJoyConHID
    {
        private static readonly List<SwitchJoyConRightHID> AllDevices = new();


        static SwitchJoyConRightHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConRightHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdRight));
        }

        public override Side Side => Side.Right;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            buttonWest = GetChildControl<ButtonControl>("buttonWest");
            buttonNorth = GetChildControl<ButtonControl>("buttonNorth");
            buttonSouth = GetChildControl<ButtonControl>("buttonSouth");
            buttonEast = GetChildControl<ButtonControl>("buttonEast");
            smallLeftShoulder = GetChildControl<ButtonControl>("smallLeftShoulder");
            smallRightShoulder = GetChildControl<ButtonControl>("smallRightShoulder");
            rightShoulder = GetChildControl<ButtonControl>("rightShoulder");
            rightTrigger = GetChildControl<ButtonControl>("rightTrigger");
            startButton = GetChildControl<ButtonControl>("start");
            homeButton = GetChildControl<ButtonControl>("home");
            rightStickButton = GetChildControl<ButtonControl>("rightStickPress");
            rightStick = GetChildControl<StickControl>("rightStick");
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            AllDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            AllDevices.Remove(this);
            if (current == this)
                current = null;
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        // ReSharper disable InconsistentNaming
        public ButtonControl buttonWest { get; private set; }
        public ButtonControl buttonNorth { get; private set; }
        public ButtonControl buttonSouth { get; private set; }
        public ButtonControl buttonEast { get; private set; }
        public ButtonControl smallLeftShoulder { get; private set; }
        public ButtonControl smallRightShoulder { get; private set; }
        public ButtonControl rightShoulder { get; private set; }
        public ButtonControl rightTrigger { get; private set; }
        public ButtonControl startButton { get; private set; }
        public ButtonControl homeButton { get; private set; }
        public ButtonControl rightStickButton { get; private set; }
        public StickControl rightStick { get; private set; }

        public new static SwitchJoyConRightHID current { get; private set; }

        public new static IReadOnlyList<SwitchJoyConRightHID> all => AllDevices;
        // ReSharper restore InconsistentNaming
    }
}
