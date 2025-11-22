using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace UnityJoycon
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(displayName = "Switch Joy-Con", stateType = typeof(SwitchJoyConHIDInputState))]
    public class SwitchJoyConHID : Gamepad, IInputStateCallbackReceiver
    {
        private const int VendorId = 0x057e;
        private const int ProductIdLeft = 0x2006;
        private const int ProductIdRight = 0x2007;
        private const double CommandIntervalSeconds = 0.1;
        private const byte StickParameterLength = 18;
        private const byte StickCalibrationLength = 9;

        private Side _side;
        private StickCalibrationState _stickCalibration;
        private double _lastCommandSentTime;

        // ReSharper disable once InconsistentNaming
        [InputControl(name = "capture", displayName = "Capture")]
        public ButtonControl captureButton { get; protected set; }

        // ReSharper disable once InconsistentNaming
        [InputControl(name = "home", displayName = "Home")]
        public ButtonControl homeButton { get; protected set; }

        private enum ReportId : byte
        {
            StandardInput = 0x30,
            SubCommandReply = 0x21
        }

        static SwitchJoyConHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdLeft));
            InputSystem.RegisterLayout<SwitchJoyConHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", VendorId).WithCapability("productId", ProductIdRight));
        }

        // ランタイムでスタティックコンストラクタを実行するためのダミーメソッド
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
        }

        void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
        {
            HandleStateEvent(eventPtr);
        }

        void IInputStateCallbackReceiver.OnNextUpdate()
        {
            if (!ShouldSendCommand()) return;

            if (TryRequestStickParameters()) return;
            if (TryRequestStickCalibration()) return;

            // TODO: IMUキャリブレーションデータの読み出し
        }

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
        {
            return false;
        }

        protected override void OnAdded()
        {
            base.OnAdded();

            var descriptor = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
            _side = descriptor.productId switch
            {
                ProductIdLeft => Side.Left,
                ProductIdRight => Side.Right,
                _ => throw new InvalidOperationException("Invalid product ID for Switch Joy-Con.")
            };

            var configureOutputModeCommand = SwitchConfigureReportModeOutput.Create(0x02, 0x30);
            ExecuteCommand(ref configureOutputModeCommand);
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();

            captureButton = GetChildControl<ButtonControl>("capture");
            homeButton = GetChildControl<ButtonControl>("home");
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
            if (!_stickCalibration.IsReady) return;

            var data = report->ToHIDInputReport(
                _side,
                _stickCalibration.DeadZone,
                _stickCalibration.CenterX,
                _stickCalibration.MinX, _stickCalibration.MaxX,
                _stickCalibration.CenterY,
                _stickCalibration.MinY, _stickCalibration.MaxY);

            InputState.Change(this, data, eventPtr: eventPtr);
        }

        private unsafe void HandleSubCommandReply(SwitchStandardInputReport* report)
        {
            if ((report->subCommandReply.ack & 0x80) == 0)
            {
                Debug.LogError($"Joy-Con sub command 0x{report->subCommandReply.subCommandId:X2} NAK received.");
                return;
            }

            if (report->subCommandReply.subCommandId != (byte)JoyConSubCommand.SpiFlashRead) return;

            var address = ReadAddress(report->subCommandReply.data);
            var length = report->subCommandReply.data[4];
            var payload = report->subCommandReply.data + 5;

            HandleSpiFlashReply(address, length, payload);
        }

        private unsafe void HandleSpiFlashReply(uint address, byte length, byte* payload)
        {
            if (address == (uint)GetStickParametersAddress() && length == StickParameterLength)
            {
                var deadZone = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
                _stickCalibration.SetParameters(deadZone);
                return;
            }

            if (address == (uint)GetStickUserCalibrationAddress() && length == StickCalibrationLength)
            {
                _stickCalibration.MarkUserCalibrationLoaded();
                if (IsAllPayloadUnset(payload, StickCalibrationLength)) return;

                var calibration = ParseStickCalibration(payload, _side);
                _stickCalibration.ApplyCalibration(calibration);
                return;
            }

            if (address == (uint)GetStickFactoryCalibrationAddress() && length == StickCalibrationLength)
            {
                var calibration = ParseStickCalibration(payload, _side);
                _stickCalibration.ApplyCalibration(calibration);
            }
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

        private static unsafe StickCalibrationValues ParseStickCalibration(byte* payload, Side side)
        {
            var rawCalData = stackalloc ushort[6];
            rawCalData[0] = (ushort)(((payload[1] << 8) & 0xf00) | payload[0]);
            rawCalData[1] = (ushort)((payload[2] << 4) | (payload[1] >> 4));
            rawCalData[2] = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
            rawCalData[3] = (ushort)((payload[5] << 4) | (payload[4] >> 4));
            rawCalData[4] = (ushort)(((payload[7] << 8) & 0xf00) | payload[6]);
            rawCalData[5] = (ushort)((payload[8] << 4) | (payload[7] >> 4));

            return side switch
            {
                Side.Left => new StickCalibrationValues(
                    rawCalData[2],
                    rawCalData[3],
                    rawCalData[4],
                    rawCalData[5],
                    rawCalData[0],
                    rawCalData[1]),
                Side.Right => new StickCalibrationValues(
                    rawCalData[0],
                    rawCalData[1],
                    rawCalData[2],
                    rawCalData[3],
                    rawCalData[4],
                    rawCalData[5]),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }

        private bool TryRequestStickParameters()
        {
            if (_stickCalibration.ParametersLoaded) return false;

            Debug.Log("Requesting stick parameters...");
            var stickParametersCommand =
                SwitchReadSPIFlashOutput.Create(0x01, GetStickParametersAddress(), StickParameterLength);
            ExecuteCommand(ref stickParametersCommand);
            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private bool TryRequestStickCalibration()
        {
            if (_stickCalibration.CalibrationLoaded) return false;

            if (!_stickCalibration.UserCalibrationLoaded)
            {
                Debug.Log("Requesting stick user calibration data...");
                var stickUserCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(0x00, GetStickUserCalibrationAddress(), StickCalibrationLength);
                ExecuteCommand(ref stickUserCalibrationCommand);
            }
            else
            {
                Debug.Log("Requesting stick factory calibration data...");
                var stickCalibrationCommand =
                    SwitchReadSPIFlashOutput.Create(0x00, GetStickFactoryCalibrationAddress(), StickCalibrationLength);
                ExecuteCommand(ref stickCalibrationCommand);
            }

            _lastCommandSentTime = lastUpdateTime;
            return true;
        }

        private SwitchReadSPIFlashOutput.Address GetStickUserCalibrationAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickUserCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickFactoryCalibrationAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickFactoryCalibration,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private SwitchReadSPIFlashOutput.Address GetStickParametersAddress()
        {
            return _side switch
            {
                Side.Left => SwitchReadSPIFlashOutput.Address.LeftStickParameters,
                Side.Right => SwitchReadSPIFlashOutput.Address.RightStickParameters,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private readonly struct StickCalibrationValues
        {
            public StickCalibrationValues(ushort centerX, ushort centerY, ushort minX, ushort minY, ushort maxX,
                ushort maxY)
            {
                CenterX = centerX;
                CenterY = centerY;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public ushort CenterX { get; }
            public ushort CenterY { get; }
            public ushort MinX { get; }
            public ushort MinY { get; }
            public ushort MaxX { get; }
            public ushort MaxY { get; }
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

            public void SetParameters(ushort deadZone)
            {
                DeadZone = deadZone;
                ParametersLoaded = true;
            }

            public void ApplyCalibration(StickCalibrationValues values)
            {
                CenterX = values.CenterX;
                CenterY = values.CenterY;
                MinX = values.MinX;
                MinY = values.MinY;
                MaxX = values.MaxX;
                MaxY = values.MaxY;
                CalibrationLoaded = true;
            }

            public void MarkUserCalibrationLoaded()
            {
                UserCalibrationLoaded = true;
            }
        }
    }
}