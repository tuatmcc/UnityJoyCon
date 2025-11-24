using System;

namespace UnityJoyCon
{
    internal struct StickCalibrationState
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

        public StandardInputReport.StickNormalizationParameters ToNormalizationParameters()
        {
            if (!IsReady)
                throw new InvalidOperationException("Stick calibration is not ready.");

            return new StandardInputReport.StickNormalizationParameters(
                DeadZone,
                CenterX, MinX, MaxX,
                CenterY, MinY, MaxY);
        }

        public unsafe void ApplyParameters(byte* payload)
        {
            var deadZone = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);

            DeadZone = deadZone;
            ParametersLoaded = true;
        }

        public unsafe void ApplyCalibration(byte* payload, Side side)
        {
            var rawCalData = stackalloc ushort[6];
            rawCalData[0] = (ushort)(((payload[1] << 8) & 0xf00) | payload[0]);
            rawCalData[1] = (ushort)((payload[2] << 4) | (payload[1] >> 4));
            rawCalData[2] = (ushort)(((payload[4] << 8) & 0xf00) | payload[3]);
            rawCalData[3] = (ushort)((payload[5] << 4) | (payload[4] >> 4));
            rawCalData[4] = (ushort)(((payload[7] << 8) & 0xf00) | payload[6]);
            rawCalData[5] = (ushort)((payload[8] << 4) | (payload[7] >> 4));

            switch (side)
            {
                case Side.Left:
                    CenterX = rawCalData[2];
                    CenterY = rawCalData[3];
                    MinX = rawCalData[4];
                    MinY = rawCalData[5];
                    MaxX = rawCalData[0];
                    MaxY = rawCalData[1];
                    break;
                case Side.Right:
                    CenterX = rawCalData[0];
                    CenterY = rawCalData[1];
                    MinX = rawCalData[2];
                    MinY = rawCalData[3];
                    MaxX = rawCalData[4];
                    MaxY = rawCalData[5];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }

            CalibrationLoaded = true;
        }

        public void MarkUserCalibrationLoaded()
        {
            UserCalibrationLoaded = true;
        }
    }

    internal struct IMUCalibrationState
    {
        public bool ParametersLoaded { get; private set; }
        public bool CalibrationLoaded { get; private set; }
        public bool UserCalibrationLoaded { get; private set; }

        public short AccelOffsetX { get; private set; }
        public short AccelOffsetY { get; private set; }
        public short AccelOffsetZ { get; private set; }

        public short AccelOriginX { get; private set; }
        public short AccelOriginY { get; private set; }
        public short AccelOriginZ { get; private set; }

        public short AccelCoefficientX { get; private set; }
        public short AccelCoefficientY { get; private set; }
        public short AccelCoefficientZ { get; private set; }

        public short GyroOffsetX { get; private set; }
        public short GyroOffsetY { get; private set; }
        public short GyroOffsetZ { get; private set; }

        public short GyroCoefficientX { get; private set; }
        public short GyroCoefficientY { get; private set; }
        public short GyroCoefficientZ { get; private set; }

        public bool IsReady => ParametersLoaded && CalibrationLoaded;

        public StandardInputReport.IMUNormalizationParameters ToNormalizationParameters()
        {
            if (!IsReady)
                throw new InvalidOperationException("IMU calibration is not ready.");

            return new StandardInputReport.IMUNormalizationParameters(
                new StandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginX, AccelOffsetX,
                    AccelCoefficientX),
                new StandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginY, AccelOffsetY,
                    AccelCoefficientY),
                new StandardInputReport.IMUNormalizationParameters.AccelAxis(AccelOriginZ, AccelOffsetZ,
                    AccelCoefficientZ),
                new StandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetX, GyroCoefficientX),
                new StandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetY, GyroCoefficientY),
                new StandardInputReport.IMUNormalizationParameters.GyroAxis(GyroOffsetZ, GyroCoefficientZ));
        }

        public unsafe void ApplyParameters(byte* payload)
        {
            AccelOffsetX = ReadInt16(payload, 0);
            AccelOffsetY = ReadInt16(payload, 2);
            AccelOffsetZ = ReadInt16(payload, 4);

            ParametersLoaded = true;
        }

        public unsafe void ApplyCalibration(byte* payload)
        {
            AccelOriginX = ReadInt16(payload, 0);
            AccelOriginY = ReadInt16(payload, 2);
            AccelOriginZ = ReadInt16(payload, 4);

            AccelCoefficientX = ReadInt16(payload, 6);
            AccelCoefficientY = ReadInt16(payload, 8);
            AccelCoefficientZ = ReadInt16(payload, 10);

            GyroOffsetX = ReadInt16(payload, 12);
            GyroOffsetY = ReadInt16(payload, 14);
            GyroOffsetZ = ReadInt16(payload, 16);

            GyroCoefficientX = ReadInt16(payload, 18);
            GyroCoefficientY = ReadInt16(payload, 20);
            GyroCoefficientZ = ReadInt16(payload, 22);

            CalibrationLoaded = true;
        }

        public void MarkUserCalibrationLoaded()
        {
            UserCalibrationLoaded = true;
        }

        private static unsafe short ReadInt16(byte* data, int index)
        {
            return (short)(data[index] | (data[index + 1] << 8));
        }
    }
}
