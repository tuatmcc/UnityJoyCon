using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace UnityJoycon
{
    public class JoyCon : IDisposable
    {
        private const ushort LeftProductId = 0x2006;
        private const ushort RightProductId = 0x2007;

        private const int ReceiveLength = 0x31;

        private readonly byte[] _defaultRumbleData = { 0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40 };
        private readonly HidDevice _device;
        public readonly Button Button = new();
        public readonly Stick Stick = new();

        public readonly Type Type;

        private bool _disposedValue;
        private byte _packetCounter;

        public JoyCon(HidDevice device)
        {
            _device = device;
            Type = device.Info.ProductId switch
            {
                LeftProductId => Type.Left,
                RightProductId => Type.Right,
                _ => throw new ArgumentException("Invalid product ID for Joy-Con", nameof(device))
            };
            _device.SetBlockingMode(true);

            // Input report mode
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f });
            // Calibration
            Calibration();
            // Connect
            SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x01 });
            SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x02 });
            SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x03 });
            // Set player lights
            SendSubCommand(SubCommandType.SetPlayerLights, new byte[] { 0b0000_0001 });
            // Enable IMU
            SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x01 });
            // Subscribe sensor data
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x30 });
            // Enable vibration
            SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x01 });
        }

        public void Dispose()
        {
            if (_disposedValue) return;

            // Disable vibration
            SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x00 });
            // Unsubscribe sensor data
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f });
            // Disable IMU
            SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x00 });
            // Turn off player lights
            // SendSubCommand(SubCommandType.SetPlayerLights, new byte[] { 0x00 });

            _disposedValue = true;
        }

        public void Update()
        {
            var data = ReceiveRaw();
            if (data.Length == 0) return;
            if (data[0] != 0x30 && data[0] != 0x21) return; // Ignore non-standard input reports

            UpdateButtonsAndSticks(data);
        }

        private byte[] SendSubCommand(SubCommandType cmdType, Span<byte> cmdData)
        {
            // Report ID (1) + Packet Counter (1) + Rumble Data (8) + Subcommand (1) + Subcommand Data (n)
            var packetLength = 1 + 1 + _defaultRumbleData.Length + 1 + cmdData.Length;
            var packet = new byte[packetLength];
            packet[0] = 0x01; // Report ID
            packet[1] = _packetCounter;
            _packetCounter = (byte)((_packetCounter + 1) % 16);
            _defaultRumbleData.CopyTo(packet, 2);
            packet[10] = (byte)cmdType;
            cmdData.CopyTo(packet.AsSpan(11));
            _device.Write(packet);

            var res = new byte[ReceiveLength];
            var len = _device.ReadTimeout(res, TimeSpan.FromMilliseconds(50));
            return res.AsSpan(0, (int)len).ToArray();
        }

        private void UpdateButtonsAndSticks(ReadOnlySpan<byte> data)
        {
            var rawStickData = data.Slice(Type == Type.Right ? 9 : 6, 3);
            Stick.Update(rawStickData);

            var buttonData = data.Slice(3, 3);
            Button.Update(buttonData, Type);
        }

        private void Calibration()
        {
            // ユーザーのキャリブレーション設定を読み込む
            var stickCalData = ReadSpi(Type == Type.Right ? 0x801du : 0x8012u, 9);
            var foundUserStickCalData = stickCalData.Any(b => b != 0xff);
            // ユーザーのキャリブレーション設定が保存されていない場合
            if (!foundUserStickCalData)
                // 工場出荷時のキャリブレーション設定を読み込む
                stickCalData = ReadSpi(Type == Type.Right ? 0x6046u : 0x603du, 9);
            Stick.SetCalibration(stickCalData, Type);

            // スティックのデッドゾーンを読み込む
            var stickParamData = ReadSpi(Type == Type.Right ? 0x6098u : 0x6086u, 18);
            Stick.SetDeadZone(stickParamData);
        }

        private byte[] ReceiveRaw()
        {
            var res = new byte[ReceiveLength];
            var len = _device.Read(res);

            return res.AsSpan(0, (int)len).ToArray();
        }

        private byte[] ReadSpi(uint addr, byte length)
        {
            if (length is < 1 or > 0x1d) throw new ArgumentOutOfRangeException(nameof(length));
            var cmdData = new[]
            {
                (byte)(addr & 0xff), (byte)((addr >> 8) & 0xff), (byte)((addr >> 16) & 0xff),
                (byte)((addr >> 24) & 0xff), length
            };
            var res = SendSubCommand(SubCommandType.SpiFlashRead, cmdData);
            if (res[0] != 0x21 || res[14] != (byte)SubCommandType.SpiFlashRead)
                throw new Exception("Unexpected response");

            var receivedAddress = res[15] | ((uint)res[16] << 8) | ((uint)res[17] << 16) | ((uint)res[18] << 24);
            if (receivedAddress != addr) throw new Exception("Address mismatch");

            var receivedLength = res[19];
            if (receivedLength != length) throw new Exception("Length mismatch");

            return res.AsSpan(20, receivedLength).ToArray();
        }
    }

    public enum Type
    {
        Left,
        Right
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public record Button
    {
        public bool DpadUp { get; private set; }
        public bool DpadDown { get; private set; }
        public bool DpadLeft { get; private set; }
        public bool DpadRight { get; private set; }
        public bool Plus { get; private set; }
        public bool Minus { get; private set; }
        public bool Home { get; private set; }
        public bool Capture { get; private set; }
        public bool StickL { get; private set; }
        public bool StickR { get; private set; }
        public bool SL { get; private set; }
        public bool SR { get; private set; }
        public bool L { get; private set; }
        public bool R { get; private set; }
        public bool ZL { get; private set; }
        public bool ZR { get; private set; }

        internal void Update(ReadOnlySpan<byte> buttonData, Type type)
        {
            switch (type)
            {
                case Type.Right:
                    DpadUp = (buttonData[0] & 0x02) != 0;
                    DpadDown = (buttonData[0] & 0x04) != 0;
                    DpadLeft = (buttonData[0] & 0x01) != 0;
                    DpadRight = (buttonData[0] & 0x08) != 0;
                    SL = (buttonData[0] & 0x10) != 0;
                    SR = (buttonData[0] & 0x20) != 0;
                    R = (buttonData[0] & 0x40) != 0;
                    ZR = (buttonData[0] & 0x80) != 0;
                    break;
                case Type.Left:
                    DpadUp = (buttonData[2] & 0x02) != 0;
                    DpadDown = (buttonData[2] & 0x01) != 0;
                    DpadLeft = (buttonData[2] & 0x08) != 0;
                    DpadRight = (buttonData[2] & 0x04) != 0;
                    SL = (buttonData[2] & 0x10) != 0;
                    SR = (buttonData[2] & 0x20) != 0;
                    L = (buttonData[2] & 0x40) != 0;
                    ZL = (buttonData[2] & 0x80) != 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            Minus = (buttonData[1] & 0x01) != 0;
            Plus = (buttonData[1] & 0x02) != 0;
            StickR = (buttonData[1] & 0x04) != 0;
            StickL = (buttonData[1] & 0x08) != 0;
            Home = (buttonData[1] & 0x10) != 0;
            Capture = (buttonData[1] & 0x20) != 0;
        }
    }

    public record Stick
    {
        private ushort _centerX;
        private ushort _centerY;
        private ushort _deadZone;
        private ushort _maxX;
        private ushort _maxY;
        private ushort _minX;
        private ushort _minY;

        public float X { get; private set; }
        public float Y { get; private set; }

        internal void SetCalibration(ReadOnlySpan<byte> calData, Type type)
        {
            var rawData = new[]
            {
                (ushort)(((calData[1] << 8) & 0xf00) | calData[0]),
                (ushort)((calData[2] << 4) | (calData[1] >> 4)),
                (ushort)(((calData[4] << 8) & 0xf00) | calData[3]),
                (ushort)((calData[5] << 4) | (calData[4] >> 4)),
                (ushort)(((calData[7] << 8) & 0xf00) | calData[6]),
                (ushort)((calData[8] << 4) | (calData[7] >> 4))
            };

            switch (type)
            {
                case Type.Right:
                    _centerX = rawData[0];
                    _centerY = rawData[1];
                    _minX = rawData[2];
                    _minY = rawData[3];
                    _maxX = rawData[4];
                    _maxY = rawData[5];
                    break;
                case Type.Left:
                    _maxX = rawData[0];
                    _maxY = rawData[1];
                    _centerX = rawData[2];
                    _centerY = rawData[3];
                    _minX = rawData[4];
                    _minY = rawData[5];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        internal void SetDeadZone(ReadOnlySpan<byte> stickParamData)
        {
            _deadZone = (ushort)(((stickParamData[4] << 8) & 0xf00) | stickParamData[3]);
        }

        internal void Update(ReadOnlySpan<byte> stickData)
        {
            var rawX = (ushort)(stickData[0] | ((stickData[1] & 0xf) << 8));
            var rawY = (ushort)((stickData[1] >> 4) | (stickData[2] << 4));

            var diffX = rawX - _centerX;
            var diffY = rawY - _centerY;

            if (Math.Abs(diffX) < _deadZone) diffX = 0;
            if (Math.Abs(diffY) < _deadZone) diffY = 0;

            X = diffX > 0 ? (float)diffX / _maxX : (float)diffX / _minX;
            Y = diffY > 0 ? (float)diffY / _maxY : (float)diffY / _minY;
        }
    }

    internal enum SubCommandType
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