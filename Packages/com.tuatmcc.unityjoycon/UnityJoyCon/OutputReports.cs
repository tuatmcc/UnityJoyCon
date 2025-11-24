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

        public const int Size = InputDeviceCommand.BaseCommandSize + SubCommandBase.Size + 1;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public SubCommandBase subCommandBase;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + SubCommandBase.Size)]
        public byte data;

        public static GenericSubCommandOutputReport Create(byte packetNumber, SubCommandBase.SubCommand subCommand,
            byte data)
        {
            return new GenericSubCommandOutputReport
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                subCommandBase = SubCommandBase.Create(packetNumber, subCommand),
                data = data
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal unsafe struct ReadSPIFlashOutputReport : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + SubCommandBase.Size + 5;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public SubCommandBase subCommandBase;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + SubCommandBase.Size)]
        public fixed byte address[4];

        [FieldOffset(InputDeviceCommand.BaseCommandSize + SubCommandBase.Size + 4)]
        public byte length;

        public static ReadSPIFlashOutputReport Create(byte packetNumber, Address address, byte length)
        {
            var addr = (uint)address;

            var command = new ReadSPIFlashOutputReport
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                subCommandBase = SubCommandBase.Create(packetNumber, SubCommandBase.SubCommand.ReadSPIFlash),
                // address to be set below
                length = length
            };

            command.address[0] = (byte)(addr & 0xFF);
            command.address[1] = (byte)((addr >> 8) & 0xFF);
            command.address[2] = (byte)((addr >> 16) & 0xFF);
            command.address[3] = (byte)((addr >> 24) & 0xFF);

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
    internal unsafe struct SubCommandBase
    {
        public const int Size = 11;

        [FieldOffset(0)] public byte reportId;
        [FieldOffset(1)] public byte packetNumber;
        [FieldOffset(2)] public fixed byte rumbleData[8];
        [FieldOffset(10)] public byte subCommand;

        public static SubCommandBase Create(byte packetNumber, SubCommand subCommand)
        {
            var subCommandBase = new SubCommandBase
            {
                reportId = 0x01,
                packetNumber = packetNumber,
                // rumbleData to be set below
                subCommand = (byte)subCommand
            };

            subCommandBase.rumbleData[0] = 0x00;
            subCommandBase.rumbleData[1] = 0x01;
            subCommandBase.rumbleData[2] = 0x40;
            subCommandBase.rumbleData[3] = 0x40;
            subCommandBase.rumbleData[4] = 0x00;
            subCommandBase.rumbleData[5] = 0x01;
            subCommandBase.rumbleData[6] = 0x40;
            subCommandBase.rumbleData[7] = 0x40;

            return subCommandBase;
        }

        public enum SubCommand : byte
        {
            ConfigureReportMode = 0x03,
            ReadSPIFlash = 0x10,
            ConfigureIMU = 0x40
        }
    }
}
