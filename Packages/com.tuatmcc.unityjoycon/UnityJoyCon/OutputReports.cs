using System;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoyCon
{
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct GenericSubCommandOutputReport : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + OutputReportBase.Size + 2;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public OutputReportBase outputReportBase;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + OutputReportBase.Size)]
        public byte subCommandId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + OutputReportBase.Size + 1)]
        public byte data;

        public static GenericSubCommandOutputReport Create(byte packetNumber, ReadOnlySpan<byte> rumbleData,
            SubCommand subCommand,
            byte data)
        {
            return new GenericSubCommandOutputReport
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                outputReportBase = OutputReportBase.Create(OutputReportBase.ReportType.SubCommand, packetNumber,
                    rumbleData),
                data = data
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal unsafe struct ReadSPIFlashOutputReport : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + OutputReportBase.Size + 6;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public OutputReportBase OutputReportBase;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + OutputReportBase.Size)]
        public byte subCommandId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + OutputReportBase.Size + 1)]
        public fixed byte AddressData[4];

        [FieldOffset(InputDeviceCommand.BaseCommandSize + OutputReportBase.Size + 5)]
        public byte Length;

        public static ReadSPIFlashOutputReport Create(byte packetNumber, ReadOnlySpan<byte> rumbleData, Address address,
            byte length)
        {
            var command = new ReadSPIFlashOutputReport
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                OutputReportBase = OutputReportBase.Create(OutputReportBase.ReportType.SubCommand, packetNumber,
                    rumbleData),
                subCommandId = (byte)SubCommand.ReadSPIFlash,
                // address to be set below
                Length = length
            };

            var addr = (uint)address;
            command.AddressData[0] = (byte)(addr & 0xFF);
            command.AddressData[1] = (byte)((addr >> 8) & 0xFF);
            command.AddressData[2] = (byte)((addr >> 16) & 0xFF);
            command.AddressData[3] = (byte)((addr >> 24) & 0xFF);

            return command;
        }

        public static Address GetStickUserCalibrationAddress(Side side)
        {
            return side switch
            {
                Side.Left => Address.LeftStickUserCalibration,
                Side.Right => Address.RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static Address GetStickFactoryCalibrationAddress(Side side)
        {
            return side switch
            {
                Side.Left => Address.LeftStickFactoryCalibration,
                Side.Right => Address.RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static Address GetStickParametersAddress(Side side)
        {
            return side switch
            {
                Side.Left => Address.LeftStickParameters,
                Side.Right => Address.RightStickParameters,
                _ => throw new ArgumentOutOfRangeException()
            };
        }


        public enum Address : uint
        {
            LeftStickUserCalibration = 0x8012,
            RightStickUserCalibration = 0x801d,
            LeftStickFactoryCalibration = 0x603d,
            RightStickFactoryCalibration = 0x6046,
            LeftStickParameters = 0x6086,
            RightStickParameters = 0x6098,
            IMUFactoryCalibration = 0x6020,
            IMUUserCalibration = 0x8028,
            IMUParameters = 0x6080
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SetRumbleOutputReport : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + OutputReportBase.Size;

        [FieldOffset(0)] public InputDeviceCommand BaseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public OutputReportBase OutputReportBase;

        public static SetRumbleOutputReport Create(byte packetNumber, ReadOnlySpan<byte> rumbleData)
        {
            return new SetRumbleOutputReport
            {
                BaseCommand = new InputDeviceCommand(Type, Size),
                OutputReportBase = OutputReportBase.Create(OutputReportBase.ReportType.Rumble, packetNumber,
                    rumbleData)
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal unsafe struct OutputReportBase
    {
        public const int Size = 10;

        [FieldOffset(0)] public byte ReportId;
        [FieldOffset(1)] public byte PacketNumber;
        [FieldOffset(2)] public fixed byte RumbleData[8];

        public static OutputReportBase Create(ReportType reportType, byte packetNumber, ReadOnlySpan<byte> rumbleData)
        {
            if (rumbleData.Length != 8)
                throw new ArgumentException("Rumble data must be 8 bytes long.", nameof(rumbleData));

            var reportBase = new OutputReportBase
            {
                ReportId = (byte)reportType,
                PacketNumber = packetNumber
                // rumbleData to be set below
            };

            for (var i = 0; i < 8; i++) reportBase.RumbleData[i] = rumbleData[i];

            return reportBase;
        }

        public enum ReportType : byte
        {
            SubCommand = 0x01,
            Rumble = 0x10
        }
    }

    public enum SubCommand : byte
    {
        ConfigureReportMode = 0x03,
        ReadSPIFlash = 0x10,
        ConfigureIMU = 0x40
    }
}
