using System;

namespace UnityJoycon
{
    public enum Side
    {
        Left,
        Right
    }

    internal static class SpiFlashAddresses
    {
        private const uint LeftStickUserCalibration = 0x8012U;
        private const uint RightStickUserCalibration = 0x801dU;

        private const uint LeftStickFactoryCalibration = 0x603dU;
        private const uint RightStickFactoryCalibration = 0x6046U;

        private const uint LeftStickParameters = 0x6086U;
        private const uint RightStickParameters = 0x6098U;

        public const uint ImuFactoryCalibration = 0x6020U;
        public const uint ImuUserCalibration = 0x8028U;
        public const uint ImuParameters = 0x6080U;

        public static uint GetStickUserCalibrationAddress(Side side)
        {
            return side switch
            {
                Side.Left => LeftStickUserCalibration,
                Side.Right => RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }

        public static uint GetStickFactoryCalibrationAddress(Side side)
        {
            return side switch
            {
                Side.Left => LeftStickFactoryCalibration,
                Side.Right => RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }

        public static uint GetStickParametersAddress(Side side)
        {
            return side switch
            {
                Side.Left => LeftStickParameters,
                Side.Right => RightStickParameters,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }
    }

    internal enum JoyConSubCommand : byte
    {
        GetOnlyControllerState = 0x00,
        BluetoothManualPairing = 0x01,
        RequestDeviceInfo = 0x02,
        SetInputReportMode = 0x03,
        TriggerButtonsElapsedTime = 0x04,
        GetPageListState = 0x05,
        SetHciState = 0x06,
        ResetPairingInfo = 0x07,
        SetShipmentLowPowerState = 0x08,
        SpiFlashRead = 0x10,
        SpiFlashWrite = 0x11,
        SpiSectorErase = 0x12,
        ResetNfcIrMcu = 0x20,
        SetNfcIrConfiguration = 0x21,
        SetNfcIrState = 0x22,
        SetUnknownData = 0x24,
        ResetUnknownData = 0x25,
        SetUnknownNfcIrMcuData = 0x28,
        GetX28NfcIrMcuData = 0x29,
        SetGpioPinOutputValue2 = 0x2a,
        GetX29NfcIrMcuData = 0x2b,
        SetPlayerLights = 0x30,
        GetPlayerLights = 0x31,
        SetHomeLight = 0x38,
        EnableImu = 0x40,
        SetImuSensitivity = 0x41,
        WriteToImuRegisters = 0x42,
        ReadImuRegisters = 0x43,
        EnableVibration = 0x48,
        GetRegulatedVoltage = 0x50,
        SetGpioPinOutputValue1 = 0x51,
        GetGpioPinInputOutputValue = 0x52
    }
}