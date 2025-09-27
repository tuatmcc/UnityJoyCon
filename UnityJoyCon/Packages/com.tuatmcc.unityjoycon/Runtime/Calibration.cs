#nullable enable

using System;
using System.Buffers.Binary;

namespace UnityJoycon
{
    internal readonly struct StickAxisCalibration
    {
        /// <summary>中心位置の値</summary>
        public StickAxisCalibration(ushort center, ushort min, ushort max)
        {
            Center = center;
            Min = min;
            Max = max;
        }

        public ushort Center { get; }
        public ushort Min { get; }
        public ushort Max { get; }
    }

    internal readonly struct StickCalibration
    {
        /// <summary>デッドゾーンの値</summary>
        public StickCalibration(StickAxisCalibration x, StickAxisCalibration y, ushort deadZone)
        {
            X = x;
            Y = y;
            DeadZone = deadZone;
        }

        public StickAxisCalibration X { get; }
        public StickAxisCalibration Y { get; }
        public ushort DeadZone { get; }
    }

    internal readonly struct AccelerometerAxisCalibration
    {
        /// <summary>完全水平状態のときの値</summary>
        public AccelerometerAxisCalibration(short origin, short horizontalOffset, short coefficient)
        {
            Origin = origin;
            HorizontalOffset = horizontalOffset;
            Coefficient = coefficient;
        }

        public short Origin { get; }
        public short HorizontalOffset { get; }
        public short Coefficient { get; }
    }

    internal readonly struct GyroscopeAxisCalibration
    {
        /// <summary>完全水平状態のときの値</summary>
        public GyroscopeAxisCalibration(short offset, short coefficient)
        {
            Offset = offset;
            Coefficient = coefficient;
        }

        public short Offset { get; }
        public short Coefficient { get; }
    }

    internal readonly struct ImuAxisCalibration
    {
        public ImuAxisCalibration(AccelerometerAxisCalibration accelerometer,
            GyroscopeAxisCalibration gyroscope)
        {
            Accelerometer = accelerometer;
            Gyroscope = gyroscope;
        }

        public AccelerometerAxisCalibration Accelerometer { get; }
        public GyroscopeAxisCalibration Gyroscope { get; }
    }

    internal sealed class ImuCalibration
    {
        public ImuCalibration(ImuAxisCalibration x, ImuAxisCalibration y, ImuAxisCalibration z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public ImuAxisCalibration X { get; }
        public ImuAxisCalibration Y { get; }
        public ImuAxisCalibration Z { get; }
    }

    internal sealed class Calibration
    {
        public Calibration(StickCalibration stick, ImuCalibration imu)
        {
            Stick = stick;
            Imu = imu;
        }

        /// <summary>スティックのキャリブレーションデータ</summary>
        public StickCalibration Stick { get; }

        /// <summary>IMUのキャリブレーションデータ</summary>
        public ImuCalibration Imu { get; }
    }

    internal static class CalibrationParser
    {
        public static Calibration Parse(
            ReadOnlySpan<byte> stickCalibrationData,
            ReadOnlySpan<byte> stickParameterData,
            ReadOnlySpan<byte> imuCalibrationData,
            ReadOnlySpan<byte> imuParameterData,
            Side side)
        {
            var stick = ParseStickCalibration(stickCalibrationData, stickParameterData, side);
            var imu = ParseImuCalibration(imuCalibrationData, imuParameterData);

            return new Calibration(stick, imu);
        }

        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#analog-stick-factory-and-user-calibration
        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#stick-parameters-1--2
        private static StickCalibration ParseStickCalibration(ReadOnlySpan<byte> stickCalData,
            ReadOnlySpan<byte> stickParamData, Side side)
        {
            var rawCalData = new[]
            {
                (ushort)(((stickCalData[1] << 8) & 0xf00) | stickCalData[0]),
                (ushort)((stickCalData[2] << 4) | (stickCalData[1] >> 4)),
                (ushort)(((stickCalData[4] << 8) & 0xf00) | stickCalData[3]),
                (ushort)((stickCalData[5] << 4) | (stickCalData[4] >> 4)),
                (ushort)(((stickCalData[7] << 8) & 0xf00) | stickCalData[6]),
                (ushort)((stickCalData[8] << 4) | (stickCalData[7] >> 4))
            };

            var deadZone = (ushort)(((stickParamData[4] << 8) & 0xf00) | stickParamData[3]);

            var xAxis = side switch
            {
                Side.Left => new StickAxisCalibration(rawCalData[2], rawCalData[4], rawCalData[0]),
                Side.Right => new StickAxisCalibration(rawCalData[0], rawCalData[2], rawCalData[4]),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };

            var yAxis = side switch
            {
                Side.Left => new StickAxisCalibration(rawCalData[3], rawCalData[5], rawCalData[1]),
                Side.Right => new StickAxisCalibration(rawCalData[1], rawCalData[3], rawCalData[5]),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };

            return new StickCalibration(xAxis, yAxis, deadZone);
        }

        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#6-axis-sensor-factory-and-user-calibration
        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#6-axis-horizontal-offsets
        private static ImuCalibration ParseImuCalibration(ReadOnlySpan<byte> imuCalData,
            ReadOnlySpan<byte> imuParamData)
        {
            var axes = new ImuAxisCalibration[3];
            for (var i = 0; i < 3; i++)
            {
                var accOrigin = ReadInt16(imuCalData.Slice(0 + i * 2, 2));
                var accCoeff = ReadInt16(imuCalData.Slice(6 + i * 2, 2));
                var gyroOffset = ReadInt16(imuCalData.Slice(12 + i * 2, 2));
                var gyroCoeff = ReadInt16(imuCalData.Slice(18 + i * 2, 2));

                var accHorizontalOffset = ReadInt16(imuParamData.Slice(0 + i * 2, 2));

                var accelerometer = new AccelerometerAxisCalibration(accOrigin, accHorizontalOffset, accCoeff);
                var gyroscope = new GyroscopeAxisCalibration(gyroOffset, gyroCoeff);

                axes[i] = new ImuAxisCalibration(accelerometer, gyroscope);
            }

            return new ImuCalibration(axes[0], axes[1], axes[2]);
        }

        private static short ReadInt16(ReadOnlySpan<byte> buffer)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }
    }
}