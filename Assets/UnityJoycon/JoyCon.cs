#nullable enable

using System;
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

        public readonly Type Type;
        private Calibration _calibration = null!;

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
            UpdateCalibration();
            // Connect
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x01 });
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x02 });
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x03 });
            // Set player lights
            SendSubCommand(SubCommandType.SetPlayerLights, new byte[] { 0b0000_0001 });
            // Enable IMU
            SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x01 });
            // Subscribe sensor data
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x30 });
            // Enable vibration
            SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x01 });
        }

        public State? State { get; private set; }

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
            if (data[0] is not (0x30 or 0x31 or 0x32 or 0x33)) return; // IMUの含まれる標準レポート以外は無視

            var report = new StandardReport(data);
            State = new State(report, _calibration, Type);
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

        private void UpdateCalibration()
        {
            // ユーザーのキャリブレーション設定を読み込む
            var stickCalData = ReadSpi(SpiAddresses.GetStickUserCalibrationAddress(Type), 9);
            var foundUserStickCalData = stickCalData.Any(b => b != 0xff);
            // ユーザーのキャリブレーション設定が保存されていない場合
            if (!foundUserStickCalData)
                // 工場出荷時のキャリブレーション設定を読み込む
                stickCalData = ReadSpi(SpiAddresses.GetStickFactoryCalibrationAddress(Type), 9);

            // スティックのパラメータを読み込む
            var stickParamData = ReadSpi(SpiAddresses.GetStickParametersAddress(Type), 18);


            // ユーザーのIMUのキャリブレーション設定を読み込む
            // メモ: -0.24, -0.10, -0.30付近で静止
            // TODO: 工場出荷時のIMUのキャリブレーション設定を読み込む
            // メモ: 0.24, -0.73, -1.16付近で静止
            var imuCalData = ReadSpi(SpiAddresses.ImuFactoryCalibration, 24);

            // IMUのパラメータを読み込む
            var imuParamData = ReadSpi(SpiAddresses.ImuParameters, 6);

            _calibration = CalibrationParser.Parse(stickCalData, stickParamData, imuCalData, imuParamData, Type);
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