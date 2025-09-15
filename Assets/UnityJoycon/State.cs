using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace UnityJoycon
{
    // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/bluetooth_hid_notes.md#standard-input-report---buttons
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum ButtonRaw : uint
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

    public record ImuSample
    {
        public Vector3 Acc;
        public Vector3 Gyro;
    }

    public record State
    {
        private readonly uint _buttons;
        public readonly ImuSample[] ImuSamples;
        public readonly Vector2 Stick;

        public State(StandardReport report, Calibration calibration, Type type)
        {
            _buttons = report.Buttons;
            Stick = GetStick(report, calibration, type);
            ImuSamples = GetImuSamples(report, calibration);
        }

        public bool GetButtonRaw(ButtonRaw button)
        {
            return (_buttons & (uint)button) != 0;
        }

        private static Vector2 GetStick(StandardReport report, Calibration calibration, Type type)
        {
            var stickRaw = type switch
            {
                Type.Left => report.StickL,
                Type.Right => report.StickR,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            var diffX = stickRaw.X - calibration.Stick.X.Center;
            var diffY = stickRaw.Y - calibration.Stick.Y.Center;

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
        private static ImuSample[] GetImuSamples(StandardReport report, Calibration calibration)
        {
            // 加速度係数: 1.0 / (coeff - origin) * 4.0
            var accCoeffX = 1f / (calibration.Imu.X.Acc.Coeff - calibration.Imu.X.Acc.Origin) * 4f;
            var accCoeffY = 1f / (calibration.Imu.Y.Acc.Coeff - calibration.Imu.Y.Acc.Origin) * 4f;
            var accCoeffZ = 1f / (calibration.Imu.Z.Acc.Coeff - calibration.Imu.Z.Acc.Origin) * 4f;

            // ジャイロ係数: 936.0 / (coeff - offset)
            var gyroCoeffX = 936f / (calibration.Imu.X.Gyro.Coeff - calibration.Imu.X.Gyro.Offset);
            var gyroCoeffY = 936f / (calibration.Imu.Y.Gyro.Coeff - calibration.Imu.Y.Gyro.Offset);
            var gyroCoeffZ = 936f / (calibration.Imu.Z.Gyro.Coeff - calibration.Imu.Z.Gyro.Offset);

            var samples = new ImuSample[3];
            for (var i = 0; i < 3; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                var raw = report.ImuFrames[i];

                var acc = new Vector3(
                    raw.AccX * accCoeffX,
                    raw.AccY * accCoeffY,
                    raw.AccZ * accCoeffZ
                );

                var gyro = new Vector3(
                    (raw.GyroX - calibration.Imu.X.Gyro.Offset) * gyroCoeffX,
                    (raw.GyroY - calibration.Imu.Y.Gyro.Offset) * gyroCoeffY,
                    (raw.GyroZ - calibration.Imu.Z.Gyro.Offset) * gyroCoeffZ
                );

                samples[i] = new ImuSample
                {
                    Acc = acc,
                    Gyro = gyro
                };
            }

            return samples;
        }
    }
}