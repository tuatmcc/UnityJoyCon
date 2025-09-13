using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace UnityJoycon
{
    public class JoyCon : IDisposable
    {
        private const ushort LeftProductId = 0x2006;
        private const ushort RightProductId = 0x2007;

        private const int ReceiveLength = 0x31;

        private readonly byte[] _defaultRumbleData = { 0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40 };
        private readonly HidDevice _device;

        private bool _disposedValue;
        private byte _packetCounter;
        public readonly Button Button = new Button();
        public Vector2 Stick = new Vector2();

        private readonly StickCalibration _stickCalibration = new StickCalibration();

        public readonly Type Type;

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
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x01 });
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x02 });
            // SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x03 });
            // Set player lights
            SendSubCommand(SubCommandType.SetPlayerLights, new byte[] { 0b0000_0001 });
            // Enable IMU
            // SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x01 });
            // Subscribe sensor data
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x30 });
            // Enable vibration
            // SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x01 });
        }

        public void Dispose()
        {
            if (_disposedValue) return;

            // Disable vibration
            // SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x00 });
            // Unsubscribe sensor data
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f });
            // Disable IMU
            // SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x00 });
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

            var rawX = (ushort)(rawStickData[0] | ((rawStickData[1] & 0xf) << 8));
            var rawY = (ushort)(rawStickData[1] >> 4 | rawStickData[2] << 4);

            var diffX = rawX - _stickCalibration.CenterX;
            var diffY = rawY - _stickCalibration.CenterY;

            if (diffX > 0) Stick.X = (float)diffX / _stickCalibration.MaxX;
            else Stick.X = (float)diffX / _stickCalibration.MinX;

            if (diffY > 0) Stick.Y = (float)diffY / _stickCalibration.MaxY;
            else Stick.Y = (float)diffY / _stickCalibration.MinY;

            if (Type == Type.Right)
            {
                var rightData = data[3];
                Button.DpadUp = (rightData & 0x02) != 0;
                Button.DpadDown = (rightData & 0x04) != 0;
                Button.DpadLeft = (rightData & 0x01) != 0;
                Button.DpadRight = (rightData & 0x08) != 0;
                Button.SL = (rightData & 0x10) != 0;
                Button.SR = (rightData & 0x20) != 0;
                Button.R = (rightData & 0x40) != 0;
                Button.ZR = (rightData & 0x80) != 0;
            }
            else
            {
                var leftData = data[5];
                Button.DpadUp = (leftData & 0x02) != 0;
                Button.DpadDown = (leftData & 0x01) != 0;
                Button.DpadLeft = (leftData & 0x08) != 0;
                Button.DpadRight = (leftData & 0x04) != 0;
                Button.SL = (leftData & 0x10) != 0;
                Button.SR = (leftData & 0x20) != 0;
                Button.L = (leftData & 0x40) != 0;
                Button.ZL = (leftData & 0x80) != 0;
            }

            var sharedData = data[4];
            Button.Minus = (sharedData & 0x01) != 0;
            Button.Plus = (sharedData & 0x02) != 0;
            Button.StickR = (sharedData & 0x04) != 0;
            Button.StickL = (sharedData & 0x08) != 0;
            Button.Home = (sharedData & 0x10) != 0;
            Button.Capture = (sharedData & 0x20) != 0;
        }

        private void Calibration()
        {
            var stickCalibrationData = ReadSpi(Type == Type.Right ? 0x801du : 0x8012u, 9);
            var foundUserCalibration = stickCalibrationData.Any(b => b != 0xff);
            // ユーザーのキャリブレーション設定が保存されていない場合は、工場出荷時のキャリブレーションデータを使用する
            if (!foundUserCalibration)
            {
                stickCalibrationData = ReadSpi(Type == Type.Right ? 0x6046u : 0x603du, 9);
            }

            if (Type == Type.Right)
            {
                _stickCalibration.CenterX =
                    (ushort)((stickCalibrationData[1] << 8) & 0xf00 | stickCalibrationData[0]);
                _stickCalibration.CenterY = (ushort)((stickCalibrationData[2] << 4) | (stickCalibrationData[1] >> 4));
                _stickCalibration.MinX = (ushort)((stickCalibrationData[4] << 8) & 0xf00 | stickCalibrationData[3]);
                _stickCalibration.MinY = (ushort)((stickCalibrationData[5] << 4) | (stickCalibrationData[4] >> 4));
                _stickCalibration.MaxX = (ushort)((stickCalibrationData[7] << 8) & 0xf00 | stickCalibrationData[6]);
                _stickCalibration.MaxY = (ushort)((stickCalibrationData[8] << 4) | (stickCalibrationData[7] >> 4));
            }
            else
            {
                _stickCalibration.MaxX = (ushort)(((stickCalibrationData[1] << 8) & 0xf00) | stickCalibrationData[0]);
                _stickCalibration.MaxY = (ushort)((stickCalibrationData[2] << 4) | (stickCalibrationData[1] >> 4));
                _stickCalibration.CenterX =
                    (ushort)((stickCalibrationData[4] << 8) & 0xf00 | stickCalibrationData[3]);
                _stickCalibration.CenterY = (ushort)((stickCalibrationData[5] << 4) | (stickCalibrationData[4] >> 4));
                _stickCalibration.MinX = (ushort)((stickCalibrationData[7] << 8) & 0xf00 | stickCalibrationData[6]);
                _stickCalibration.MinY = (ushort)((stickCalibrationData[8] << 4) | (stickCalibrationData[7] >> 4));
            }
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
        Right,
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public record Button
    {
        public bool DpadUp { get; internal set; }
        public bool DpadDown { get; internal set; }
        public bool DpadLeft { get; internal set; }
        public bool DpadRight { get; internal set; }
        public bool Plus { get; internal set; }
        public bool Minus { get; internal set; }
        public bool Home { get; internal set; }
        public bool Capture { get; internal set; }
        public bool StickL { get; internal set; }
        public bool StickR { get; internal set; }
        public bool SL { get; internal set; }
        public bool SR { get; internal set; }
        public bool L { get; internal set; }
        public bool R { get; internal set; }
        public bool ZL { get; internal set; }
        public bool ZR { get; internal set; }
    }

    internal record StickCalibration
    {
        public ushort MinX;
        public ushort MaxX;
        public ushort CenterX;
        public ushort MinY;
        public ushort MaxY;
        public ushort CenterY;
        public ushort DeadZone;
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