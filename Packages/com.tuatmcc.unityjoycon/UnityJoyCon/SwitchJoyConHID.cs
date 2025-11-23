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

        // ReSharper disable InconsistentNaming
        public Vector3Control accelerometer { get; private set; }
        public Vector3Control gyroscope { get; private set; }

        public static SwitchJoyConHID current { get; private set; }
        public new static IReadOnlyList<SwitchJoyConHID> all => AllDevices;
        // ReSharper restore InconsistentNaming

        private static readonly List<SwitchJoyConHID> AllDevices = new();

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

        private bool _haveParsedHIDDescriptor;
        private HID.HIDDeviceDescriptor _hidDeviceDescriptor;

        private StickCalibrationState _stickCalibration;
        private IMUCalibrationState _imuCalibration;
        private bool _imuEnabled;

        private double _lastCommandSentTime;
        private double _lastStandardInputReceivedTime;
        private byte _commandPacketNumber;

        private enum ReportId : byte
        {
            StandardInput = 0x30,
            SubCommandReply = 0x21
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

        private struct StickCalibrationState
        {
            public bool ParametersLoaded { get; private set; }
            public bool CalibrationLoaded { get; private set; }
            public bool UserCalibrationLoaded { get; private set; }

            public ushort DeadZone { get; private set; }
            public ushort CenterX { get; private set; }
            public ushort CenterY { get; private set; }
            public ushort MinX { get; private set; }
            public ushort MinY { get; private set; }
            public ushort MaxX { get; private set; }
            public ushort MaxY { get; private set; }

            public bool IsReady => ParametersLoaded && CalibrationLoaded;

            public SwitchStandardInputReport.StickNormalizationParameters ToNormalizationParameters()
            {
                if (!IsReady)
                    throw new InvalidOperationException("Stick calibration is not ready.");

                return new SwitchStandardInputReport.StickNormalizationParameters(
                    DeadZone,
                    CenterX, MinX, MaxX,
                    CenterY, MinY, MaxY);
            }

            public unsafe void ApplyParameters(byte* payload)
            {
                var deadZone = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);

                DeadZone = deadZone;
                ParametersLoaded = true;
            }

            public unsafe void ApplyCalibration(byte* payload, Side side)
            {
                var rawCalData = stackalloc ushort[6];
                rawCalData[0] = (ushort)(((payload[1] << 8) & 0xf00) | payload[0]);
                rawCalData[1] = (ushort)((payload[2] << 4) | (payload[1] >> 4));
                rawCalData[2] = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
                rawCalData[3] = (ushort)((payload[5] << 4) | (payload[4] >> 4));
                rawCalData[4] = (ushort)(((payload[7] << 8) & 0xf00) | payload[6]);
                rawCalData[5] = (ushort)((payload[8] << 4) | (payload[7] >> 4));

                switch (side)
                {
                    case Side.Left:
                        CenterX = rawCalData[2];
                        CenterY = rawCalData[3];
                        MinX = rawCalData[4];
                        MinY = rawCalData[5];
                        MaxX = rawCalData[0];
                        MaxY = rawCalData[1];
                        break;
                    case Side.Right:
                        CenterX = rawCalData[0];
                        CenterY = rawCalData[1];
                        MinX = rawCalData[2];
                        MinY = rawCalData[3];
                        MaxX = rawCalData[4];
                        MaxY = rawCalData[5];
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(side), side, null);
                }

                CalibrationLoaded = true;
            }

            public void MarkUserCalibrationLoaded()
            {
                UserCalibrationLoaded = true;
            }
        }

        private struct IMUCalibrationState
        {
            public bool ParametersLoaded { get; private set; }
            public bool CalibrationLoaded { get; private set; }
            public bool UserCalibrationLoaded { get; private set; }

            public short AccelOffsetX { get; private set; }
            public short AccelOffsetY { get; private set; }
            public short AccelOffsetZ { get; private set; }

            public short AccelOriginX { get; private set; }
            public short AccelOriginY { get; private set; }
            public short AccelOriginZ { get; private set; }

            public short AccelCoefficientX { get; private set; }
            public short AccelCoefficientY { get; private set; }
            public short AccelCoefficientZ { get; private set; }

            public short GyroOffsetX { get; private set; }
            public short GyroOffsetY { get; private set; }
            public short GyroOffsetZ { get; private set; }

            public short GyroCoefficientX { get; private set; }
            public short GyroCoefficientY { get; private set; }
            public short GyroCoefficientZ { get; private set; }

            public bool IsReady => ParametersLoaded && CalibrationLoaded;

            public SwitchStandardInputReport.IMUNormalizationParameters ToNormalizationParameters()
            {
                if (!IsReady)
                    throw new InvalidOperationException("IMU calibration is not ready.");

                return new SwitchStandardInputReport.IMUNormalizationParameters(
                    new SwitchStandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginX, AccelOffsetX,
                        AccelCoefficientX),
                    new SwitchStandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginY, AccelOffsetY,
                        AccelCoefficientY),
                    new SwitchStandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginZ, AccelOffsetZ,
                        AccelCoefficientZ),
                    new SwitchStandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetX, GyroCoefficientX),
                    new SwitchStandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetY, GyroCoefficientY),
                    new SwitchStandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetZ, GyroCoefficientZ));
            }

            public unsafe void ApplyParameters(byte* payload)
            {
                AccelOffsetX = ReadInt16(payload, 0);
                AccelOffsetY = ReadInt16(payload, 2);
                AccelOffsetZ = ReadInt16(payload, 4);

                ParametersLoaded = true;
            }

            public unsafe void ApplyCalibration(byte* payload)
            {
                AccelOriginX = ReadInt16(payload, 0);
                AccelOriginY = ReadInt16(payload, 2);
                AccelOriginZ = ReadInt16(payload, 4);

                AccelCoefficientX = ReadInt16(payload, 6);
                AccelCoefficientY = ReadInt16(payload, 8);
                AccelCoefficientZ = ReadInt16(payload, 10);

                GyroOffsetX = ReadInt16(payload, 12);
                GyroOffsetY = ReadInt16(payload, 14);
                GyroOffsetZ = ReadInt16(payload, 16);

                GyroCoefficientX = ReadInt16(payload, 18);
                GyroCoefficientY = ReadInt16(payload, 20);
                GyroCoefficientZ = ReadInt16(payload, 22);

                CalibrationLoaded = true;
            }

            public void MarkUserCalibrationLoaded()
            {
                UserCalibrationLoaded = true;
            }

            private static unsafe short ReadInt16(byte* data, int index)
            {
                return (short)(data[index] | (data[index + 1] << 8));
            }
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (L)", stateType = typeof(SwitchJoyConLeftHIDInputState))]
    public class SwitchJoyConLeftHID : SwitchJoyConHID
    {
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

        private static readonly List<SwitchJoyConLeftHID> AllDevices = new();

        public override Side Side => Side.Left;

        static SwitchJoyConLeftHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConLeftHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdLeft));
        }

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
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con (R)", stateType = typeof(SwitchJoyConRightHIDInputState))]
    public class SwitchJoyConRightHID : SwitchJoyConHID
    {
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

        private static readonly List<SwitchJoyConRightHID> AllDevices = new();

        public override Side Side => Side.Right;


        static SwitchJoyConRightHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConRightHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdRight));
        }

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
    }
}