using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoycon
{
    [StructLayout(LayoutKind.Sequential)]
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
        public uint buttons;

        [InputControl(name = "leftStick", layout = "Stick", format = "VEC2", displayName = "Left Stick")]
        public Vector2 leftStick;

        [InputControl(name = "rightStick", layout = "Stick", format = "VEC2", displayName = "Right Stick")]
        public Vector2 rightStick;

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
        [FieldOffset(12)] public byte vibrationReport;

        // IMU data
        [FieldOffset(13)] public IMUData imu0;
        [FieldOffset(25)] public IMUData imu1;
        [FieldOffset(37)] public IMUData imu2;

        // Sub command reply data
        [FieldOffset(13)] public SubCommandReplyData subCommandReply;

        public SwitchJoyConHIDInputState ToHIDInputReport(Side side, ushort stickDeadZone, ushort stickCenterX,
            ushort stickMinX, ushort stickMaxX, ushort stickCenterY, ushort stickMinY, ushort stickMaxY)
        {
            var rawX = side switch
            {
                Side.Left => (ushort)(left0 | ((left1 & 0x0f) << 8)),
                Side.Right => (ushort)(right0 | ((right1 & 0x0f) << 8)),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };

            var rawY = side switch
            {
                Side.Left => (ushort)(((left1 & 0xf0) >> 4) | (left2 << 4)),
                Side.Right => (ushort)(((right1 & 0xf0) >> 4) | (right2 << 4)),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };

            var diffX = rawX - stickCenterX;
            var diffY = rawY - stickCenterY;

            if (Math.Abs(diffX) < stickDeadZone) diffX = 0;
            if (Math.Abs(diffY) < stickDeadZone) diffY = 0;

            var normX = diffX > 0
                ? (float)diffX / stickMaxX
                : (float)diffX / stickMinX;
            var normY = diffY > 0
                ? (float)diffY / stickMaxY
                : (float)diffY / stickMinY;
            var stick = new Vector2(normX, normY);

            var state = new SwitchJoyConHIDInputState
            {
                buttons = ((uint)buttons2 << 16) | ((uint)buttons1 << 8) | buttons0
            };

            switch (side)
            {
                case Side.Left:
                    state.leftStick = stick;
                    break;
                case Side.Right:
                    state.rightStick = stick;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }

            return state;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct IMUData
    {
        [FieldOffset(0)] public byte accelX0;
        [FieldOffset(1)] public byte accelX1;
        [FieldOffset(2)] public byte accelY0;
        [FieldOffset(3)] public byte accelY1;
        [FieldOffset(4)] public byte accelZ0;
        [FieldOffset(5)] public byte accelZ1;
        [FieldOffset(6)] public byte gyroX0;
        [FieldOffset(7)] public byte gyroX1;
        [FieldOffset(8)] public byte gyroY0;
        [FieldOffset(9)] public byte gyroY1;
        [FieldOffset(10)] public byte gyroZ0;
        [FieldOffset(11)] public byte gyroZ1;

        public (short accelX, short accelY, short accelZ) GetAcceleration()
        {
            var accelX = (short)(accelX0 | (accelX1 << 8));
            var accelY = (short)(accelY0 | (accelY1 << 8));
            var accelZ = (short)(accelZ0 | (accelZ1 << 8));
            return (accelX, accelY, accelZ);
        }

        public (short gyroX, short gyroY, short gyroZ) GetGyroscope()
        {
            var gyroX = (short)(gyroX0 | (gyroX1 << 8));
            var gyroY = (short)(gyroY0 | (gyroY1 << 8));
            var gyroZ = (short)(gyroZ0 | (gyroZ1 << 8));
            return (gyroX, gyroY, gyroZ);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SubCommandReplyData
    {
        [FieldOffset(0)] public byte ack;
        [FieldOffset(1)] public byte subCommandId;

        // ReSharper disable once InconsistentNaming
        public unsafe byte* data
        {
            get
            {
                fixed (SubCommandReplyData* ptr = &this)
                {
                    return (byte*)ptr + 2;
                }
            }
        }
    }
}