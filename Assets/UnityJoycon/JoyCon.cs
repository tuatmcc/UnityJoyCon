#nullable enable

using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace UnityJoycon
{
    public class JoyCon : IAsyncDisposable
    {
        private const ushort LeftProductId = 0x2006;
        private const ushort RightProductId = 0x2007;

        private readonly Channel<State> _stateChannel = Channel.CreateBounded<State>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public readonly Type Type;

        private bool _disposedValue;

        private InternalJoyCon? _internalJoyCon;

        private State? _lastState;

        private JoyCon(HidDevice device)
        {
            Type = device.Info.ProductId switch
            {
                LeftProductId => Type.Left,
                RightProductId => Type.Right,
                _ => throw new ArgumentException("Invalid product ID for Joy-Con", nameof(device))
            };
        }

        public State? State
        {
            get
            {
                if (_stateChannel.Reader.TryRead(out var state)) _lastState = state;

                return _lastState;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposedValue) return;

            if (_internalJoyCon != null) await _internalJoyCon.DisposeAsync();

            _disposedValue = true;
        }

        public static async ValueTask<JoyCon> Create(HidDevice device)
        {
            var joyCon = new JoyCon(device);

            joyCon._internalJoyCon = await InternalJoyCon.Create(device, joyCon.Type, joyCon._stateChannel.Writer);

            return joyCon;
        }
    }

    public enum Type
    {
        Left,
        Right
    }

    public static class SpiAddresses
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

        public static uint GetStickUserCalibrationAddress(Type type)
        {
            return type switch
            {
                Type.Left => LeftStickUserCalibration,
                Type.Right => RightStickUserCalibration,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static uint GetStickFactoryCalibrationAddress(Type type)
        {
            return type switch
            {
                Type.Left => LeftStickFactoryCalibration,
                Type.Right => RightStickFactoryCalibration,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static uint GetStickParametersAddress(Type type)
        {
            return type switch
            {
                Type.Left => LeftStickParameters,
                Type.Right => RightStickParameters,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }

    public enum SubCommandType : byte
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