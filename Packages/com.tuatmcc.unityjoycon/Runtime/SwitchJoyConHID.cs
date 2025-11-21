using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoycon
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct SwitchJoyConHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'V', 'S'); // Switch Joy-Con Virtual State
        public FourCC format => Type;

        [InputControl(name = "buttonWest", displayName = "Y", layout = "Button", bit = (int)Button.Y)]
        [InputControl(name = "buttonNorth", displayName = "X", layout = "Button", bit = (int)Button.X)]
        [InputControl(name = "buttonSouth", displayName = "B", layout = "Button", bit = (int)Button.B)]
        [InputControl(name = "buttonEast", displayName = "A", layout = "Button", bit = (int)Button.A)]
        [InputControl(name = "rightSmallRightShoulder", displayName = "Right SR", layout = "Button",
            bit = (int)Button.RightSR)]
        [InputControl(name = "rightSmallLeftShoulder", displayName = "Right SL", layout = "Button",
            bit = (int)Button.RightSL)]
        [InputControl(name = "rightShoulder", displayName = "R", layout = "Button", bit = (int)Button.R)]
        [InputControl(name = "rightTrigger", displayName = "ZR", layout = "Button", format = "BIT",
            bit = (int)Button.ZR)]
        [InputControl(name = "start", displayName = "Plus", layout = "Button", bit = (int)Button.Plus)]
        [InputControl(name = "select", displayName = "Minus", layout = "Button", bit = (int)Button.Minus)]
        [InputControl(name = "rightStickPress", displayName = "Right Stick", layout = "Button",
            bit = (int)Button.RightStick)]
        [InputControl(name = "leftStickPress", displayName = "Left Stick", layout = "Button",
            bit = (int)Button.LeftStick)]
        [InputControl(name = "home", displayName = "Home", layout = "Button", bit = (int)Button.Home)]
        [InputControl(name = "capture", displayName = "Capture", layout = "Button", bit = (int)Button.Capture)]
        [InputControl(name = "dpad", format = "BIT", bit = (int)Button.DpadDown, sizeInBits = 4)]
        [InputControl(name = "dpad/down", bit = (int)Button.DpadDown)]
        [InputControl(name = "dpad/up", bit = (int)Button.DpadUp)]
        [InputControl(name = "dpad/right", bit = (int)Button.DpadRight)]
        [InputControl(name = "dpad/left", bit = (int)Button.DpadLeft)]
        [InputControl(name = "leftSmallLeftShoulder", displayName = "Left SL", layout = "Button",
            bit = (int)Button.LeftSL)]
        [InputControl(name = "leftSmallRightShoulder", displayName = "Left SR", layout = "Button",
            bit = (int)Button.LeftSR)]
        [InputControl(name = "leftShoulder", displayName = "L", layout = "Button", bit = (int)Button.L)]
        [InputControl(name = "leftTrigger", displayName = "ZL", layout = "Button", format = "BIT",
            bit = (int)Button.ZL)]
        [FieldOffset(0)]
        public uint buttons;

        public enum Button
        {
            Y = 0,
            X = 1,
            B = 2,
            A = 3,
            RightSR = 4,
            RightSL = 5,
            R = 6,
            ZR = 7,

            Minus = 8,
            Plus = 9,
            RightStick = 10,
            LeftStick = 11,
            Home = 12,
            Capture = 13,
            ChargeGrip = 15,

            DpadDown = 16,
            DpadUp = 17,
            DpadRight = 18,
            DpadLeft = 19,
            LeftSL = 20,
            LeftSR = 21,
            L = 22,
            ZL = 23
        }

        public void Set(Button button, bool state)
        {
            if (state)
                buttons |= (uint)(1 << (int)button);
            else
                buttons &= ~(uint)(1 << (int)button);
        }
    }

    [InitializeOnLoad]
    [InputControlLayout(displayName = "Switch Joy-Con", stateType = typeof(SwitchJoyConHIDInputState))]
    public class SwitchJoyConHID : Gamepad, IInputStateCallbackReceiver
    {
        static SwitchJoyConHID()
        {
            InputSystem.RegisterLayout<SwitchJoyConHID>(matches: new InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", 0x057e).WithCapability("productId", 0x2007));
        }

        [InputControl(name = "capture", displayName = "Capture")]
        public ButtonControl captureButton { get; protected set; }

        [InputControl(name = "home", displayName = "Home")]
        public ButtonControl homeButton { get; protected set; }

        unsafe void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
        {
            // Delta eventは無視する
            if (eventPtr.type != StateEvent.Type) return;

            var stateEvent = StateEvent.From(eventPtr);
            // HID以外のイベントは無視する
            if (stateEvent->stateFormat != SwitchHIDGenericInputReport.Format) return;

            // 汎用レポートに変換
            var genericReport = (SwitchHIDGenericInputReport*)stateEvent->state;

            // 標準レポートの場合
            if (genericReport->reportId == SwitchStandardInputReport.ExpectedReportId)
            {
                var data = ((SwitchStandardInputReport*)stateEvent->state)->ToHIDInputReport();
                InputState.Change(this, data, eventPtr: eventPtr);
            }
        }

        void IInputStateCallbackReceiver.OnNextUpdate()
        {
            // const double handshakeRestartTimeout = 2.0;
            //
            // var currentTime = Time.realtimeSinceStartupAsDouble;
            //
            // if (currentTime >= lastUpdateTime + handshakeRestartTimeout &&
            //     currentTime >= _handshakeTimer + handshakeRestartTimeout)
            // {
            //     _handshakeTimer = currentTime;
            //     var command = SwitchConfigureReportModeOutput.Create(0x01, 0x30);
            //     ExecuteCommand(ref command);
            // }
        }

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr,
            ref uint offset)
        {
            return false;
        }

        protected override void OnAdded()
        {
            base.OnAdded();

            var command = SwitchConfigureReportModeOutput.Create(0x00, 0x30);
            ExecuteCommand(ref command);
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();

            captureButton = GetChildControl<ButtonControl>("capture");
            homeButton = GetChildControl<ButtonControl>("home");
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SwitchHIDGenericInputReport
    {
        public static FourCC Format => new('H', 'I', 'D');

        [FieldOffset(0)] public byte reportId;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SwitchStandardInputReport
    {
        public const int Size = 0x49;
        public const byte ExpectedReportId = 0x30;

        [FieldOffset(0)] public byte reportId;
        [FieldOffset(1)] public byte timer;
        [FieldOffset(2)] public byte batteryAndConnectionInfo;
        [FieldOffset(3)] public byte buttons0;
        [FieldOffset(4)] public byte buttons1;
        [FieldOffset(5)] public byte buttons2;
        [FieldOffset(6)] public byte left0;
        [FieldOffset(7)] public byte left1;
        [FieldOffset(8)] public byte left2;
        [FieldOffset(9)] public byte right0;
        [FieldOffset(10)] public byte right1;
        [FieldOffset(11)] public byte right2;

        // TODO: IMU or sub command reply

        public SwitchJoyConHIDInputState ToHIDInputReport()
        {
            var state = new SwitchJoyConHIDInputState
            {
                buttons = ((uint)buttons2 << 16) | ((uint)buttons1 << 8) | buttons0
            };

            return state;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SwitchConfigureReportModeOutput : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + 3;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 0)]
        public byte reportId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)]
        public byte packetNumber;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)]
        public ulong rumbleData;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 10)]
        public byte subCommandConfigureReportMode;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)]
        public byte mode;

        public static SwitchConfigureReportModeOutput Create(byte packetNumber, byte mode)
        {
            return new SwitchConfigureReportModeOutput
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                reportId = 0x01,
                packetNumber = packetNumber,
                rumbleData = 0x00_01_40_40_00_01_40_40,
                subCommandConfigureReportMode = 0x03,
                mode = mode
            };
        }
    }
}