using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoyCon
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SwitchJoyConLeftHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'L', 'S'); // Switch Joy-Con Left State
        public FourCC format => Type;

        [InputControl(name = "dpad", layout = "Dpad", format = "BIT", bit = (int)Button.DpadDown,
            sizeInBits = 4)]
        [InputControl(name = "dpad/down", bit = (int)Button.DpadDown)]
        [InputControl(name = "dpad/up", bit = (int)Button.DpadUp)]
        [InputControl(name = "dpad/right", bit = (int)Button.DpadRight)]
        [InputControl(name = "dpad/left", bit = (int)Button.DpadLeft)]
        [InputControl(name = "smallRightShoulder", displayName = "Left SR", layout = "Button",
            bit = (int)Button.LeftSR)]
        [InputControl(name = "smallLeftShoulder", displayName = "Left SL", layout = "Button",
            bit = (int)Button.LeftSL)]
        [InputControl(name = "leftShoulder", displayName = "L", layout = "Button", bit = (int)Button.L)]
        [InputControl(name = "leftTrigger", displayName = "ZL", layout = "Button", format = "BIT",
            bit = (int)Button.ZL)]
        [InputControl(name = "select", displayName = "Minus", layout = "Button", bit = (int)Button.Minus)]
        [InputControl(name = "capture", displayName = "Capture", layout = "Button", bit = (int)Button.Capture)]
        [InputControl(name = "leftStickPress", displayName = "Left Stick", layout = "Button",
            bit = (int)Button.LeftStick)]
        public uint buttons;

        [InputControl(name = "leftStick", layout = "Stick", format = "VEC2", displayName = "Left Stick")]
        public Vector2 leftStick;

        [InputControl(name = "accelerometer", layout = "Vector3", format = "VEC3", displayName = "Accelerometer")]
        public Vector3 accelerometer;

        [InputControl(name = "gyroscope", layout = "Vector3", format = "VEC3", displayName = "Gyroscope")]
        public Vector3 gyroscope;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SwitchJoyConRightHIDInputState : IInputStateTypeInfo
    {
        public static FourCC Type => new('S', 'J', 'R', 'S'); // Switch Joy-Con Right State
        public FourCC format => Type;

        [InputControl(name = "buttonWest", displayName = "Y", layout = "Button", bit = (int)Button.Y)]
        [InputControl(name = "buttonNorth", displayName = "X", layout = "Button", bit = (int)Button.X)]
        [InputControl(name = "buttonSouth", displayName = "B", layout = "Button", bit = (int)Button.B)]
        [InputControl(name = "buttonEast", displayName = "A", layout = "Button", bit = (int)Button.A)]
        [InputControl(name = "smallRightShoulder", displayName = "Right SR", layout = "Button",
            bit = (int)Button.RightSR)]
        [InputControl(name = "smallLeftShoulder", displayName = "Right SL", layout = "Button",
            bit = (int)Button.RightSL)]
        [InputControl(name = "rightShoulder", displayName = "R", layout = "Button", bit = (int)Button.R)]
        [InputControl(name = "rightTrigger", displayName = "ZR", layout = "Button", format = "BIT",
            bit = (int)Button.ZR)]
        [InputControl(name = "start", displayName = "Plus", layout = "Button", bit = (int)Button.Plus)]
        [InputControl(name = "home", displayName = "Home", layout = "Button", bit = (int)Button.Home)]
        [InputControl(name = "rightStickPress", displayName = "Right Stick", layout = "Button",
            bit = (int)Button.RightStick)]
        public uint buttons;

        [InputControl(name = "rightStick", layout = "Stick", format = "VEC2", displayName = "Right Stick")]
        public Vector2 rightStick;

        [InputControl(name = "accelerometer", layout = "Vector3", format = "VEC3", displayName = "Accelerometer")]
        public Vector3 accelerometer;

        [InputControl(name = "gyroscope", layout = "Vector3", format = "VEC3", displayName = "Gyroscope")]
        public Vector3 gyroscope;
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

        public SwitchJoyConLeftHIDInputState ToLeftHIDInputReport(
            StickNormalizationParameters stickParams,
            IMUNormalizationParameters imuParams)
        {
            var stick = ConvertStick(ReadStick(Side.Left), stickParams);

            var state = new SwitchJoyConLeftHIDInputState
            {
                buttons = ((uint)buttons2 << 16) | ((uint)buttons1 << 8) | buttons0,
                leftStick = stick
            };

            var imu = ConvertIMUData(imu2, imuParams, Side.Left);
            state.accelerometer = imu.Acceleration;
            state.gyroscope = imu.AngularVelocity;

            return state;
        }

        public SwitchJoyConRightHIDInputState ToRightHIDInputReport(
            StickNormalizationParameters stickParams,
            IMUNormalizationParameters imuParams)
        {
            var stick = ConvertStick(ReadStick(Side.Right), stickParams);

            var state = new SwitchJoyConRightHIDInputState
            {
                buttons = ((uint)buttons2 << 16) | ((uint)buttons1 << 8) | buttons0,
                rightStick = stick
            };

            var imu = ConvertIMUData(imu2, imuParams, Side.Right);
            state.accelerometer = imu.Acceleration;
            state.gyroscope = imu.AngularVelocity;

            return state;
        }

        private (ushort rawX, ushort rawY) ReadStick(Side side)
        {
            return side switch
            {
                Side.Left => (
                    (ushort)(left0 | ((left1 & 0x0f) << 8)),
                    (ushort)(((left1 & 0xf0) >> 4) | (left2 << 4))),
                Side.Right => (
                    (ushort)(right0 | ((right1 & 0x0f) << 8)),
                    (ushort)(((right1 & 0xf0) >> 4) | (right2 << 4))),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }

        private static Vector2 ConvertStick((ushort, ushort) raw, StickNormalizationParameters parameters)
        {
            var diffX = raw.Item1 - parameters.X.Center;
            var diffY = raw.Item2 - parameters.Y.Center;

            if (Math.Abs(diffX) < parameters.DeadZone) diffX = 0;
            if (Math.Abs(diffY) < parameters.DeadZone) diffY = 0;

            var normX = diffX > 0
                ? (float)diffX / parameters.X.Max
                : (float)diffX / parameters.X.Min;
            var normY = diffY > 0
                ? (float)diffY / parameters.Y.Max
                : (float)diffY / parameters.Y.Min;

            return new Vector2(normX, normY);
        }

        private static IMUFrame ConvertIMUData(IMUData data, IMUNormalizationParameters parameters, Side side)
        {
            var (rawAccelX, rawAccelY, rawAccelZ) = data.GetAcceleration();
            var (rawGyroX, rawGyroY, rawGyroZ) = data.GetGyroscope();

            var acceleration = new Vector3(
                (side == Side.Left ? -1f : 1f) * ConvertAccel(rawAccelY, parameters.AccelY),
                (side == Side.Left ? 1f : -1f) * ConvertAccel(rawAccelZ, parameters.AccelZ),
                ConvertAccel(rawAccelX, parameters.AccelX)
            );

            var angularVelocity = new Vector3(
                (side == Side.Left ? -1f : 1f) * ConvertGyro(rawGyroY, parameters.GyroY),
                (side == Side.Left ? 1f : -1f) * ConvertGyro(rawGyroZ, parameters.GyroZ),
                ConvertGyro(rawGyroX, parameters.GyroX)
            );

            return new IMUFrame(acceleration, angularVelocity);
        }

        private static float ConvertAccel(short raw, IMUNormalizationParameters.AccelAxis axis)
        {
            var accelCoefficient = 1f / (axis.Coefficient - axis.Origin) * 4f;
            return raw * accelCoefficient;
        }

        private static float ConvertGyro(short raw, IMUNormalizationParameters.GyroAxis axis)
        {
            var gyroCoefficient = 936f / (axis.Coefficient - axis.Offset);
            return (raw - axis.Offset) * gyroCoefficient;
        }

        public bool IsEnabledIMU()
        {
            unsafe
            {
                fixed (SwitchStandardInputReport* ptr = &this)
                {
                    // IMU領域が全て0ならIMU無効とみなす
                    for (var i = 13; i < 49; i++)
                        if (((byte*)ptr)[i] != 0)
                            return true;
                }
            }

            return false;
        }

        internal readonly struct StickNormalizationParameters
        {
            public StickNormalizationParameters(
                ushort deadZone,
                ushort centerX, ushort minX, ushort maxX,
                ushort centerY, ushort minY, ushort maxY)
            {
                DeadZone = deadZone;
                X = new Axis(centerX, minX, maxX);
                Y = new Axis(centerY, minY, maxY);
            }

            public ushort DeadZone { get; }
            public Axis X { get; }
            public Axis Y { get; }

            internal readonly struct Axis
            {
                public Axis(ushort center, ushort min, ushort max)
                {
                    Center = center;
                    Min = min;
                    Max = max;
                }

                public ushort Center { get; }
                public ushort Min { get; }
                public ushort Max { get; }
            }
        }

        internal readonly struct IMUNormalizationParameters
        {
            public IMUNormalizationParameters(
                AccelAxis accelX, AccelAxis accelY, AccelAxis accelZ,
                GyroAxis gyroX, GyroAxis gyroY, GyroAxis gyroZ)
            {
                AccelX = accelX;
                AccelY = accelY;
                AccelZ = accelZ;
                GyroX = gyroX;
                GyroY = gyroY;
                GyroZ = gyroZ;
            }

            public AccelAxis AccelX { get; }
            public AccelAxis AccelY { get; }
            public AccelAxis AccelZ { get; }

            public GyroAxis GyroX { get; }
            public GyroAxis GyroY { get; }
            public GyroAxis GyroZ { get; }

            internal readonly struct AccelAxis
            {
                public AccelAxis(short origin, short horizontalOffset, short coefficient)
                {
                    Origin = origin;
                    HorizontalOffset = horizontalOffset;
                    Coefficient = coefficient;
                }

                public short Origin { get; }
                public short HorizontalOffset { get; }
                public short Coefficient { get; }
            }

            internal readonly struct GyroAxis
            {
                public GyroAxis(short offset, short coefficient)
                {
                    Offset = offset;
                    Coefficient = coefficient;
                }

                public short Offset { get; }
                public short Coefficient { get; }
            }
        }

        internal readonly struct IMUFrame
        {
            public IMUFrame(Vector3 acceleration, Vector3 angularVelocity)
            {
                Acceleration = acceleration;
                AngularVelocity = angularVelocity;
            }

            public Vector3 Acceleration { get; }
            public Vector3 AngularVelocity { get; }
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
