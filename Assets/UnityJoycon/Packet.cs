#nullable enable

using System;
using System.Buffers.Binary;

namespace UnityJoycon
{
    public enum BatteryLevel : byte
    {
        Empty = 0x00,
        Critical = 0x02,
        Low = 0x04,
        Medium = 0x06,
        Full = 0x08,
        Unknown = 0xff
    }

    public enum ControllerType : byte
    {
        ProOrGrip = 0x00,
        JoyCon = 0x03,
        Unknown = 0xff
    }

    public struct ConnectionInfo
    {
        public ControllerType ControllerType { get; }
        public bool IsSwitchOrUsbPowered { get; }

        public ConnectionInfo(ControllerType controllerType, bool isSwitchOrUsbPowered)
        {
            ControllerType = controllerType;
            IsSwitchOrUsbPowered = isSwitchOrUsbPowered;
        }
    }

    public struct StickRaw
    {
        public ushort X { get; }
        public ushort Y { get; }

        public StickRaw(ushort x, ushort y)
        {
            X = x;
            Y = y;
        }
    }

    public struct ImuRaw
    {
        public short AccX { get; }
        public short AccY { get; }
        public short AccZ { get; }
        public short GyroX { get; }
        public short GyroY { get; }
        public short GyroZ { get; }

        public ImuRaw(short accX, short accY, short accZ, short gyroX, short gyroY, short gyroZ)
        {
            AccX = accX;
            AccY = accY;
            AccZ = accZ;
            GyroX = gyroX;
            GyroY = gyroY;
            GyroZ = gyroZ;
        }
    }

    // 参照: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/bluetooth_hid_notes.md#standard-input-report-format
    public readonly struct StandardReport
    {
        private readonly ReadOnlyMemory<byte> _data;

        public StandardReport(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        private ReadOnlySpan<byte> S => _data.Span;

        public byte ReportId => S[0];
        public byte Timer => S[1];

        public BatteryLevel BatteryLevel => DecodeBatteryLevel((byte)(S[2] >> 4));

        public ConnectionInfo ConnectionInfo => DecodeConnectionInfo((byte)(S[2] >> 4));

        public uint Buttons => (uint)(S[3] | (S[4] << 8) | (S[5] << 16));

        public StickRaw StickL => DecodeStick(S.Slice(6, 3));
        public StickRaw StickR => DecodeStick(S.Slice(9, 3));

        public byte Vibration => S[12];

        public SubCommandReply SubCommandReply
        {
            get
            {
                if (ReportId != 0x21) throw new InvalidOperationException("Not a subcommand response packet.");
                if (S.Length < 15) throw new InvalidOperationException("Subcommand response packet is too short.");

                var maxDataLength = Math.Min(35, S.Length - 15);
                return new SubCommandReply(S.Slice(13, 2 + maxDataLength).ToArray());
            }
        }

        public ImuRaw[] ImuFrames
        {
            get
            {
                if (ReportId is not (0x30 or 0x31 or 0x32 or 0x33))
                    throw new InvalidOperationException("Not an IMU packet.");
                if (S.Length < 49) throw new InvalidOperationException("IMU packet is too short.");

                return DecodeImu(S.Slice(13, 36));
            }
        }

        private static BatteryLevel DecodeBatteryLevel(byte highNibble)
        {
            return highNibble switch
            {
                0x0 => BatteryLevel.Empty,
                0x2 => BatteryLevel.Critical,
                0x4 => BatteryLevel.Low,
                0x6 => BatteryLevel.Medium,
                0x8 => BatteryLevel.Full,
                _ => BatteryLevel.Unknown
            };
        }

        private static ConnectionInfo DecodeConnectionInfo(byte lowNibble)
        {
            var typeBits = (byte)((lowNibble >> 1) & 0x03);
            var type = typeBits switch
            {
                0x0 => ControllerType.ProOrGrip,
                0x3 => ControllerType.JoyCon,
                _ => ControllerType.Unknown
            };
            var powered = (lowNibble & 0x01) != 0;
            return new ConnectionInfo(type, powered);
        }

        private static StickRaw DecodeStick(ReadOnlySpan<byte> s3)
        {
            var x = (ushort)(s3[0] | ((s3[1] & 0x0F) << 8));
            var y = (ushort)(((s3[1] & 0xF0) >> 4) | (s3[2] << 4));
            return new StickRaw(x, y);
        }

        private static ImuRaw[] DecodeImu(ReadOnlySpan<byte> imu36)
        {
            var frames = new ImuRaw[3];
            for (var i = 0; i < 3; i++)
            {
                var span = imu36.Slice(i * 12, 12);
                var ax = BinaryPrimitives.ReadInt16LittleEndian(span[..2]);
                var ay = BinaryPrimitives.ReadInt16LittleEndian(span[2..4]);
                var az = BinaryPrimitives.ReadInt16LittleEndian(span[4..6]);
                var gx = BinaryPrimitives.ReadInt16LittleEndian(span[6..8]);
                var gy = BinaryPrimitives.ReadInt16LittleEndian(span[8..10]);
                var gz = BinaryPrimitives.ReadInt16LittleEndian(span[10..12]);
                frames[i] = new ImuRaw(ax, ay, az, gx, gy, gz);
            }

            return frames;
        }
    }

    public readonly struct SubCommandReply
    {
        private readonly ReadOnlyMemory<byte> _data;
        private ReadOnlySpan<byte> S => _data.Span;

        public SubCommandReply(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public byte Ack => S[0];
        public bool IsPositive => (Ack & 0x80) != 0;
        public SubCommandType SubCommandType => (SubCommandType)S[1];
        public ReadOnlySpan<byte> Data => S[2..];
    }
}