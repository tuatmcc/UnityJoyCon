#nullable enable

using System;

namespace UnityJoycon
{
    public struct StickCalAxis
    {
        /// <summary>
        ///     中心位置の値
        /// </summary>
        public ushort Center { get; }

        /// <summary>
        ///     下限値
        /// </summary>
        public ushort Min { get; }

        /// <summary>
        ///     上限値
        /// </summary>
        public ushort Max { get; }

        public StickCalAxis(ushort center, ushort min, ushort max)
        {
            Center = center;
            Min = min;
            Max = max;
        }
    }

    public struct StickCalibration
    {
        /// <summary>
        ///     X軸のキャリブレーションデータ
        /// </summary>
        public StickCalAxis X { get; }

        /// <summary>
        ///     Y軸のキャリブレーションデータ
        /// </summary>
        public StickCalAxis Y { get; }

        /// <summary>
        ///     デッドゾーンの値
        /// </summary>
        public ushort DeadZone { get; }

        public StickCalibration(StickCalAxis x, StickCalAxis y, ushort deadZone)
        {
            X = x;
            Y = y;
            DeadZone = deadZone;
        }
    }

    public struct AccCalAxis
    {
        /// <summary>
        ///     完全水平状態のときの値
        /// </summary>
        public short Origin { get; }

        /// <summary>
        ///     完全水平状態のときに値を0にするためのオフセット値
        /// </summary>
        public short HorizontalOffset { get; }

        /// <summary>
        ///     感度調整用の係数
        /// </summary>
        public short Coeff { get; }

        public AccCalAxis(short origin, short horizontalOffset, short coeff)
        {
            Origin = origin;
            HorizontalOffset = horizontalOffset;
            Coeff = coeff;
        }
    }

    public struct GyroCalAxis
    {
        /// <summary>
        ///     完全水平状態のときの値
        /// </summary>
        public short Offset { get; }

        /// <summary>
        ///     感度調整用の係数
        /// </summary>
        public short Coeff { get; }

        public GyroCalAxis(short offset, short coeff)
        {
            Offset = offset;
            Coeff = coeff;
        }
    }

    public struct ImuCalAxis
    {
        /// <summary>
        ///     加速度センサーのキャリブレーションデータ
        /// </summary>
        public AccCalAxis Acc { get; }

        /// <summary>
        ///     ジャイロセンサーのキャリブレーションデータ
        /// </summary>
        public GyroCalAxis Gyro { get; }

        public ImuCalAxis(AccCalAxis acc, GyroCalAxis gyro)
        {
            Acc = acc;
            Gyro = gyro;
        }
    }

    public struct ImuCalibration
    {
        /// <summary>
        ///     X軸のキャリブレーションデータ
        /// </summary>
        public ImuCalAxis X { get; }

        /// <summary>
        ///     Y軸のキャリブレーションデータ
        /// </summary>
        public ImuCalAxis Y { get; }

        /// <summary>
        ///     Z軸のキャリブレーションデータ
        /// </summary>
        public ImuCalAxis Z { get; }

        public ImuCalibration(ImuCalAxis x, ImuCalAxis y, ImuCalAxis z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class Calibration
    {
        public Calibration(StickCalibration stick, ImuCalibration imu)
        {
            Stick = stick;
            Imu = imu;
        }

        /// <summary>
        ///     スティックのキャリブレーションデータ
        /// </summary>
        public StickCalibration Stick { get; }

        /// <summary>
        ///     IMUのキャリブレーションデータ
        /// </summary>
        public ImuCalibration Imu { get; }
    }

    public static class CalibrationParser
    {
        public static Calibration Parse(ReadOnlySpan<byte> stickCalData, ReadOnlySpan<byte> stickParamData,
            ReadOnlySpan<byte> imuCalData, ReadOnlySpan<byte> imuParamData, Type type)
        {
            var stickCal = ParseStickCalibration(stickCalData, stickParamData, type);
            var imuCal = ParseImuCalibration(imuCalData, imuParamData);

            return new Calibration(stickCal, imuCal);
        }

        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#analog-stick-factory-and-user-calibration
        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#stick-parameters-1--2
        private static StickCalibration ParseStickCalibration(ReadOnlySpan<byte> stickCalData,
            ReadOnlySpan<byte> stickParamData, Type type)
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

            var stickCalX = type switch
            {
                Type.Left => new StickCalAxis(rawCalData[2], rawCalData[4], rawCalData[0]),
                Type.Right => new StickCalAxis(rawCalData[0], rawCalData[2], rawCalData[4]),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            var stickCalY = type switch
            {
                Type.Left => new StickCalAxis(rawCalData[3], rawCalData[5], rawCalData[1]),
                Type.Right => new StickCalAxis(rawCalData[1], rawCalData[3], rawCalData[5]),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            return new StickCalibration(stickCalX, stickCalY, deadZone);
        }

        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#6-axis-sensor-factory-and-user-calibration
        // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/spi_flash_notes.md#6-axis-horizontal-offsets
        private static ImuCalibration ParseImuCalibration(ReadOnlySpan<byte> imuCalData,
            ReadOnlySpan<byte> imuParamData)
        {
            var axis = new ImuCalAxis[3];
            for (var i = 0; i < 3; i++)
            {
                var accOrigin = ReadInt16(imuCalData.Slice(0 + i * 2, 2));
                var accCoeff = ReadInt16(imuCalData.Slice(6 + i * 2, 2));
                var gyroOffset = ReadInt16(imuCalData.Slice(12 + i * 2, 2));
                var gyroCoeff = ReadInt16(imuCalData.Slice(18 + i * 2, 2));

                var accHorizontalOffset = ReadInt16(imuParamData.Slice(0 + i * 2, 2));

                axis[i] = new ImuCalAxis(
                    new AccCalAxis(accOrigin, accHorizontalOffset, accCoeff),
                    new GyroCalAxis(gyroOffset, gyroCoeff)
                );
            }

            return new ImuCalibration(axis[0], axis[1], axis[2]);
        }

        private static short ReadInt16(ReadOnlySpan<byte> buffer)
        {
            return (short)(buffer[0] | (buffer[1] << 8));
        }
    }
}