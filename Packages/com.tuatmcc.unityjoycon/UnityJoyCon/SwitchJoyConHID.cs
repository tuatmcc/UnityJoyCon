using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Haptics;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;

namespace UnityJoyCon
{
    public abstract class SwitchJoyConHID : InputDevice, IDualMotorRumble, IInputStateCallbackReceiver
    {
        protected const int VendorId = 0x057e;
        protected const int ProductIdLeft = 0x2006;
        protected const int ProductIdRight = 0x2007;

        private const double CommandIntervalSeconds = 0.1;
        private const double StandardReportTimeoutSeconds = 2.0;

        private const float DefaultRumbleLowFrequencyHz = 160.0f;
        private const float DefaultRumbleHighFrequencyHz = 320.0f;

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

        private bool _haveParsedHIDDescriptor;
        private HID.HIDDeviceDescriptor _hidDeviceDescriptor;

        private float _rumbleLowFrequencyHz = DefaultRumbleLowFrequencyHz;
        private float _rumbleHighFrequencyHz = DefaultRumbleHighFrequencyHz;
        private float _rumbleLowFrequencyAmplitude;
        private float _rumbleHighFrequencyAmplitude;
        private bool _rumblePaused;

        private bool _imuEnabled;
        private double _lastCommandSentTime;
        private double _lastStandardInputReceivedTime;
        private byte _commandPacketNumber;

        private StickCalibrationState _stickCalibration;
        private IMUCalibrationState _imuCalibration;

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

        public void PauseHaptics()
        {
            _rumblePaused = true;
            SendRumble();
        }

        public void ResumeHaptics()
        {
            _rumblePaused = false;
            SendRumble();
        }

        public void ResetHaptics()
        {
            _rumbleLowFrequencyHz = DefaultRumbleLowFrequencyHz;
            _rumbleHighFrequencyHz = DefaultRumbleHighFrequencyHz;
            _rumbleLowFrequencyAmplitude = 0.0f;
            _rumbleHighFrequencyAmplitude = 0.0f;
            _rumblePaused = false;
            SendRumble();
        }

        public void SetMotorSpeeds(float lowFrequency, float highFrequency)
        {
            _rumbleLowFrequencyAmplitude = lowFrequency;
            _rumbleHighFrequencyAmplitude = highFrequency;
            _rumblePaused = false;
            SendRumble();
        }

        public void SetRumble(float? lowFrequencyHz, float? highFrequencyHz, float? lowFrequencyAmplitude,
            float? highFrequencyAmplitude)
        {
            if (lowFrequencyHz.HasValue)
                _rumbleLowFrequencyHz = lowFrequencyHz.Value;
            if (highFrequencyHz.HasValue)
                _rumbleHighFrequencyHz = highFrequencyHz.Value;
            if (lowFrequencyAmplitude.HasValue)
                _rumbleLowFrequencyAmplitude = lowFrequencyAmplitude.Value;
            if (highFrequencyAmplitude.HasValue)
                _rumbleHighFrequencyAmplitude = highFrequencyAmplitude.Value;
            _rumblePaused = false;
            SendRumble();
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

        private unsafe void HandleSubCommandReply(StandardInputReport* report)
        {
            if ((report->subCommandReply.ack & 0x80) == 0)
            {
                Debug.LogError($"Joy-Con sub command 0x{report->subCommandReply.subCommandId:X2} NAK received.");
                return;
            }

            if (report->subCommandReply.subCommandId != (byte)SubCommand.ReadSPIFlash) return;

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

        private byte[] GetRumbleData()
        {
            if (_rumblePaused)
                // ニュートラル状態のバイブレーションデータを返す
                return RumbleEncoder.Encode(DefaultRumbleLowFrequencyHz, DefaultRumbleHighFrequencyHz, 0.0f, 0.0f);

            return RumbleEncoder.Encode(_rumbleLowFrequencyHz, _rumbleHighFrequencyHz,
                _rumbleLowFrequencyAmplitude, _rumbleHighFrequencyAmplitude);
        }

        private void SendRumble()
        {
            var setRumbleCommand = SetRumbleOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData());
            ExecuteCommand(ref setRumbleCommand);
        }

        private bool ShouldSendCommand()
        {
            return !(lastUpdateTime < _lastCommandSentTime + CommandIntervalSeconds);
        }

        private bool TryChangeReportMode()
        {
            // 一定時間標準入力レポートが受信されていない場合、レポートモードを再設定する
            if (!(lastUpdateTime > _lastStandardInputReceivedTime + StandardReportTimeoutSeconds)) return false;

            var configureOutputModeCommand =
                GenericSubCommandOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                    SubCommand.ConfigureReportMode, 0x30);
            ExecuteCommand(ref configureOutputModeCommand);

            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestStickParameters()
        {
            if (_stickCalibration.ParametersLoaded) return false;

            var stickParametersCommand =
                ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                    ReadSPIFlashOutputReport.GetStickParametersAddress(Side),
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
                    ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                        ReadSPIFlashOutputReport.GetStickUserCalibrationAddress(Side),
                        StickCalibrationLength);
                ExecuteCommand(ref stickUserCalibrationCommand);
            }
            else
            {
                var stickCalibrationCommand =
                    ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                        ReadSPIFlashOutputReport.GetStickFactoryCalibrationAddress(Side),
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
                ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                    ReadSPIFlashOutputReport.Address.IMUParameters,
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
                    ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                        ReadSPIFlashOutputReport.Address.IMUUserCalibration,
                        IMUCalibrationLength);
                ExecuteCommand(ref imuUserCalibrationCommand);
            }
            else
            {
                var imuCalibrationCommand =
                    ReadSPIFlashOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                        ReadSPIFlashOutputReport.Address.IMUFactoryCalibration, 24);
                ExecuteCommand(ref imuCalibrationCommand);
            }

            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryEnableIMU()
        {
            if (_imuEnabled) return false;

            var enableImuCommand = GenericSubCommandOutputReport.Create(GetNextCommandPacketNumber(), GetRumbleData(),
                SubCommand.ConfigureIMU, 0x01);
            ExecuteCommand(ref enableImuCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }
    }
}
