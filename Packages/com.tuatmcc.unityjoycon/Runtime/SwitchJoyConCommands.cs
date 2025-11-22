using System.Runtime.InteropServices;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityJoycon
{
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SwitchConfigureReportModeOutput : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + 12;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 0)]
        public byte reportId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)]
        public byte packetNumber;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)]
        public byte rumbleData0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)]
        public byte rumbleData1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)]
        public byte rumbleData2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 5)]
        public byte rumbleData3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 6)]
        public byte rumbleData4;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 7)]
        public byte rumbleData5;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 8)]
        public byte rumbleData6;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 9)]
        public byte rumbleData7;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 10)]
        public byte subCommandConfigureReportMode;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)]
        public byte mode;

        public static SwitchConfigureReportModeOutput Create(byte packetNumber, byte mode)
        {
            return new SwitchConfigureReportModeOutput
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                reportId = 0x01,
                packetNumber = packetNumber,
                rumbleData0 = 0x00,
                rumbleData1 = 0x01,
                rumbleData2 = 0x40,
                rumbleData3 = 0x40,
                rumbleData4 = 0x00,
                rumbleData5 = 0x01,
                rumbleData6 = 0x40,
                rumbleData7 = 0x40,
                subCommandConfigureReportMode = 0x03,
                mode = mode
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct SwitchReadSPIFlashOutput : IInputDeviceCommandInfo
    {
        public static FourCC Type => new('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        public const int Size = InputDeviceCommand.BaseCommandSize + 0x16;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 0)]
        public byte reportId;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)]
        public byte packetNumber;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)]
        public byte rumbleData0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)]
        public byte rumbleData1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)]
        public byte rumbleData2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 5)]
        public byte rumbleData3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 6)]
        public byte rumbleData4;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 7)]
        public byte rumbleData5;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 8)]
        public byte rumbleData6;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 9)]
        public byte rumbleData7;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 10)]
        public byte subCommandReadSPIFlash;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)]
        public byte address0;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 12)]
        public byte address1;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 13)]
        public byte address2;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 14)]
        public byte address3;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 15)]
        public byte length;

        public static SwitchReadSPIFlashOutput Create(byte packetNumber, Address address, byte length)
        {
            var addr = (uint)address;

            return new SwitchReadSPIFlashOutput
            {
                baseCommand = new InputDeviceCommand(Type, Size),
                reportId = 0x01,
                packetNumber = packetNumber,
                rumbleData0 = 0x00,
                rumbleData1 = 0x01,
                rumbleData2 = 0x40,
                rumbleData3 = 0x40,
                rumbleData4 = 0x00,
                rumbleData5 = 0x01,
                rumbleData6 = 0x40,
                rumbleData7 = 0x40,
                subCommandReadSPIFlash = 0x10,
                address0 = (byte)(addr & 0xFF),
                address1 = (byte)((addr >> 8) & 0xFF),
                address2 = (byte)((addr >> 16) & 0xFF),
                address3 = (byte)((addr >> 24) & 0xFF),
                length = length
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
            ImuFactoryCalibration = 0x6020,
            ImuUserCalibration = 0x8028,
            ImuParameters = 0x6080
        }
    }
}