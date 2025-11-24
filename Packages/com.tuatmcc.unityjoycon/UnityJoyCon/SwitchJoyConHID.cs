using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.HID;
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
        private byte _commandPacketNumber;

        private bool _haveParsedHIDDescriptor;
        private HID.HIDDeviceDescriptor _hidDeviceDescriptor;
        private IMUCalibrationState _imuCalibration;
        private bool _imuEnabled;

        private JoyConInitializationPipeline _initializer;
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
            _initializer?.Tick(lastUpdateTime);
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

            _initializer = new JoyConInitializationPipeline(this);

            accelerometer = GetChildControl<Vector3Control>("accelerometer");
            gyroscope = GetChildControl<Vector3Control>("gyroscope");
        }

        private unsafe void HandleStateEvent(InputEventPtr eventPtr)
        {
            if (eventPtr.type != StateEvent.Type) return;

            var stateEvent = StateEvent.From(eventPtr);
            if (stateEvent->stateFormat != GenericInputReport.Format) return;

            var genericReport = (GenericInputReport*)stateEvent->state;
            var reportId = (GenericInputReport.ReportId)genericReport->reportId;

            switch (reportId)
            {
                case GenericInputReport.ReportId.StandardInput:
                    HandleStandardInput((StandardInputReport*)stateEvent->state, eventPtr);
                    break;
                case GenericInputReport.ReportId.SubCommandReply:
                    HandleSubCommandReply((StandardInputReport*)stateEvent->state);
                    break;
            }
        }

        private unsafe void HandleStandardInput(StandardInputReport* report, InputEventPtr eventPtr)
        {
            _initializer?.MarkStandardInputReceived(lastUpdateTime);
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

        private unsafe void HandleSubCommandReply(StandardInputReport* report)
        {
            if ((report->subCommandReply.ack & 0x80) == 0)
            {
                Debug.LogError($"Joy-Con sub command 0x{report->subCommandReply.subCommandId:X2} NAK received.");
                return;
            }

            if (report->subCommandReply.subCommandId != (byte)SubCommandBase.SubCommand.ReadSPIFlash) return;

            var address = report->subCommandReply.data[0] |
                          ((uint)report->subCommandReply.data[1] << 8) |
                          ((uint)report->subCommandReply.data[2] << 16) |
                          ((uint)report->subCommandReply.data[3] << 24);
            var length = report->subCommandReply.data[4];
            var payload = report->subCommandReply.data + 5;

            HandleSpiFlashReply(address, length, payload);
        }

        private unsafe void HandleSpiFlashReply(uint address, byte length, byte* payload)
        {
            if (address == (uint)ReadSPIFlashOutputReport.GetStickParametersAddress(Side) &&
                length == StickParameterLength)
            {
                _stickCalibration.ApplyParameters(payload);
                return;
            }

            if (address == (uint)ReadSPIFlashOutputReport.GetStickUserCalibrationAddress(Side) &&
                length == StickCalibrationLength)
            {
                _stickCalibration.MarkUserCalibrationLoaded();
                if (IsAllPayloadUnset(payload, StickCalibrationLength)) return;

                _stickCalibration.ApplyCalibration(payload, Side);
                return;
            }

            if (address == (uint)ReadSPIFlashOutputReport.GetStickFactoryCalibrationAddress(Side) &&
                length == StickCalibrationLength)
                _stickCalibration.ApplyCalibration(payload, Side);

            if (address == (uint)ReadSPIFlashOutputReport.Address.IMUParameters && length == IMUParameterLength)
                _imuCalibration.ApplyParameters(payload);

            if (address == (uint)ReadSPIFlashOutputReport.Address.IMUUserCalibration && length == IMUCalibrationLength)
            {
                _imuCalibration.MarkUserCalibrationLoaded();
                if (IsAllPayloadUnset(payload, IMUCalibrationLength)) return;

                _imuCalibration.ApplyCalibration(payload);
                return;
            }

            if (address == (uint)ReadSPIFlashOutputReport.Address.IMUFactoryCalibration &&
                length == StickParameterLength)
                _imuCalibration.ApplyCalibration(payload);
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

        private sealed class JoyConInitializationPipeline
        {
            private const double StandardReportTimeoutSeconds = 2.0;

            private readonly SwitchJoyConHID _owner;
            private double _lastCommandSentTime;
            private double _lastStandardInputReceivedTime;

            public JoyConInitializationPipeline(SwitchJoyConHID owner)
            {
                _owner = owner;
            }

            public void Tick(double now)
            {
                if (!ShouldSendCommand(now)) return;

                if (TryChangeReportMode(now)) return;

                if (TryRequestStickParameters(now)) return;
                if (TryRequestStickCalibration(now)) return;

                if (TryRequestIMUParameters(now)) return;
                if (TryRequestIMUCalibration(now)) return;

                if (TryEnableIMU(now)) return;
            }

            public void MarkStandardInputReceived(double time)
            {
                _lastStandardInputReceivedTime = time;
            }

            private bool ShouldSendCommand(double now)
            {
                return !(now < _lastCommandSentTime + CommandIntervalSeconds);
            }

            private bool TryChangeReportMode(double now)
            {
                // 一定時間標準入力レポートが受信されていない場合、レポートモードを再設定する
                if (!(now > _lastStandardInputReceivedTime + StandardReportTimeoutSeconds)) return false;

                var configureOutputModeCommand =
                    GenericSubCommandOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                        SubCommandBase.SubCommand.ConfigureReportMode, 0x30);
                _owner.ExecuteCommand(ref configureOutputModeCommand);
                _lastCommandSentTime = now;
                return true;
            }

            private bool TryRequestStickParameters(double now)
            {
                if (_owner._stickCalibration.ParametersLoaded) return false;

                var stickParametersCommand =
                    ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                        ReadSPIFlashOutputReport.GetStickParametersAddress(_owner.Side),
                        StickParameterLength);
                _owner.ExecuteCommand(ref stickParametersCommand);
                _lastCommandSentTime = now;
                return true;
            }

            private bool TryRequestStickCalibration(double now)
            {
                if (_owner._stickCalibration.CalibrationLoaded) return false;

                if (!_owner._stickCalibration.UserCalibrationLoaded)
                {
                    var stickUserCalibrationCommand =
                        ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                            ReadSPIFlashOutputReport.GetStickUserCalibrationAddress(_owner.Side),
                            StickCalibrationLength);
                    _owner.ExecuteCommand(ref stickUserCalibrationCommand);
                }
                else
                {
                    var stickCalibrationCommand =
                        ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                            ReadSPIFlashOutputReport.GetStickFactoryCalibrationAddress(_owner.Side),
                            StickCalibrationLength);
                    _owner.ExecuteCommand(ref stickCalibrationCommand);
                }

                _lastCommandSentTime = now;
                return true;
            }

            private bool TryRequestIMUParameters(double now)
            {
                if (_owner._imuCalibration.ParametersLoaded) return false;

                var imuParametersCommand =
                    ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                        ReadSPIFlashOutputReport.Address.IMUParameters,
                        IMUParameterLength);
                _owner.ExecuteCommand(ref imuParametersCommand);
                _lastCommandSentTime = now;
                return true;
            }

            private bool TryRequestIMUCalibration(double now)
            {
                if (_owner._imuCalibration.CalibrationLoaded) return false;

                if (!_owner._imuCalibration.UserCalibrationLoaded)
                {
                    var imuUserCalibrationCommand =
                        ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                            ReadSPIFlashOutputReport.Address.IMUUserCalibration,
                            IMUCalibrationLength);
                    _owner.ExecuteCommand(ref imuUserCalibrationCommand);
                }
                else
                {
                    var imuCalibrationCommand =
                        ReadSPIFlashOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                            ReadSPIFlashOutputReport.Address.IMUFactoryCalibration, 24);
                    _owner.ExecuteCommand(ref imuCalibrationCommand);
                }

                _lastCommandSentTime = now;
                return true;
            }

            private bool TryEnableIMU(double now)
            {
                if (_owner._imuEnabled) return false;

                var enableImuCommand = GenericSubCommandOutputReport.Create(_owner.GetNextCommandPacketNumber(),
                    SubCommandBase.SubCommand.ConfigureIMU, 0x01);
                _owner.ExecuteCommand(ref enableImuCommand);
                _lastCommandSentTime = now;
                return true;
            }
        }
    }
}
