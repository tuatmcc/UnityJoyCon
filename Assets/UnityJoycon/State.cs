using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace UnityJoycon
{
    // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/bluetooth_hid_notes.md#standard-input-report---buttons
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum Button : uint
    {
        Y = 1 << 0,
        X = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
        RSR = 1 << 4,
        RSL = 1 << 5,
        R = 1 << 6,
        ZR = 1 << 7,

        Minus = 1 << 8,
        Plus = 1 << 9,
        RStick = 1 << 10,
        LStick = 1 << 11,
        Home = 1 << 12,
        Capture = 1 << 13,
        ChargeGrip = 1 << 15,

        Down = 1 << 16,
        Up = 1 << 17,
        Right = 1 << 18,
        Left = 1 << 19,
        LSL = 1 << 20,
        LSR = 1 << 21,
        L = 1 << 22,
        ZL = 1 << 23
    }

    public readonly struct ImuSample
    {
        public ImuSample(Vector3 acceleration, Vector3 gyroscope)
        {
            Acceleration = acceleration;
            Gyroscope = gyroscope;
        }

        public Vector3 Acceleration { get; }
        public Vector3 Gyroscope { get; }
    }

    public sealed class State
    {
        private readonly uint _buttons;

        internal State(StandardReport report, Calibration calibration, Side side)
        {
            _buttons = report.Buttons;
            Stick = ConvertStick(report, calibration, side);
            ImuSamples = ConvertImuSamples(report, calibration);
        }

        public ImuSample[] ImuSamples { get; }
        public Vector2 Stick { get; }

        public bool IsButtonPressed(Button button)
        {
            return (_buttons & (uint)button) != 0;
        }

        private static Vector2 ConvertStick(StandardReport report, Calibration calibration, Side side)
        {
            var rawStick = side switch
            {
                Side.Left => report.StickL,
                Side.Right => report.StickR,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };

            var diffX = rawStick.X - calibration.Stick.X.Center;
            var diffY = rawStick.Y - calibration.Stick.Y.Center;

            if (Math.Abs(diffX) < calibration.Stick.DeadZone) diffX = 0;
            if (Math.Abs(diffY) < calibration.Stick.DeadZone) diffY = 0;

            var normX = diffX > 0
                ? (float)diffX / calibration.Stick.X.Max
                : (float)diffX / calibration.Stick.X.Min;
            var normY = diffY > 0
                ? (float)diffY / calibration.Stick.Y.Max
                : (float)diffY / calibration.Stick.Y.Min;

            return new Vector2(normX, normY);
        }

        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/imu_sensor_notes.md#convert-to-basic-useful-data-using-spi-calibration
        private static ImuSample[] ConvertImuSamples(StandardReport report, Calibration calibration)
        {
            var accCoeffX = 1f /
                (calibration.Imu.X.Accelerometer.Coefficient - calibration.Imu.X.Accelerometer.Origin) * 4f;
            var accCoeffY = 1f /
                (calibration.Imu.Y.Accelerometer.Coefficient - calibration.Imu.Y.Accelerometer.Origin) * 4f;
            var accCoeffZ = 1f /
                (calibration.Imu.Z.Accelerometer.Coefficient - calibration.Imu.Z.Accelerometer.Origin) * 4f;

            var gyroCoeffX = 936f / (calibration.Imu.X.Gyroscope.Coefficient - calibration.Imu.X.Gyroscope.Offset);
            var gyroCoeffY = 936f / (calibration.Imu.Y.Gyroscope.Coefficient - calibration.Imu.Y.Gyroscope.Offset);
            var gyroCoeffZ = 936f / (calibration.Imu.Z.Gyroscope.Coefficient - calibration.Imu.Z.Gyroscope.Offset);

            var samples = new ImuSample[3];
            for (var i = 0; i < 3; i++)
            {
                var raw = report.ImuFrames[i];

                var acceleration = new Vector3(
                    raw.AccX * accCoeffX,
                    raw.AccY * accCoeffY,
                    raw.AccZ * accCoeffZ
                );

                var gyroscope = new Vector3(
                    (raw.GyroX - calibration.Imu.X.Gyroscope.Offset) * gyroCoeffX,
                    (raw.GyroY - calibration.Imu.Y.Gyroscope.Offset) * gyroCoeffY,
                    (raw.GyroZ - calibration.Imu.Z.Gyroscope.Offset) * gyroCoeffZ
                );

                samples[i] = new ImuSample(acceleration, gyroscope);
            }

            return samples;
        }
    }
}