using System;
using System.Diagnostics.CodeAnalysis;

namespace UnityJoycon
{
    public class JoyCon : IDisposable
    {
        private const int ReceiveLength = 0x31;

        private readonly byte[] _defaultRumbleData = { 0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40 };
        private readonly HidDevice _device;

        private readonly bool[] _buttonStates = new bool[16];
        private bool _disposedValue;
        private byte _packetCounter;

        public JoyCon(HidDevice device)
        {
            _device = device;
            _device.SetBlockingMode(true);

            // Input report mode
            SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f });
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

        public JoyConType Type { get; } = JoyConType.Right;

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

        public bool GetButton(JoyConButton button)
        {
            return _buttonStates[(int)button];
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
            if (Type == JoyConType.Right)
            {
                var rightData = data[3];
                _buttonStates[(int)JoyConButton.DpadUp] = (rightData & 0x02) != 0;
                _buttonStates[(int)JoyConButton.DpadDown] = (rightData & 0x04) != 0;
                _buttonStates[(int)JoyConButton.DpadLeft] = (rightData & 0x01) != 0;
                _buttonStates[(int)JoyConButton.DpadRight] = (rightData & 0x08) != 0;
                _buttonStates[(int)JoyConButton.SL] = (rightData & 0x10) != 0;
                _buttonStates[(int)JoyConButton.SR] = (rightData & 0x20) != 0;
                _buttonStates[(int)JoyConButton.R] = (rightData & 0x40) != 0;
                _buttonStates[(int)JoyConButton.ZR] = (rightData & 0x80) != 0;
            }
            else
            {
                var leftData = data[5];
                _buttonStates[(int)JoyConButton.DpadUp] = (leftData & 0x02) != 0;
                _buttonStates[(int)JoyConButton.DpadDown] = (leftData & 0x01) != 0;
                _buttonStates[(int)JoyConButton.DpadLeft] = (leftData & 0x08) != 0;
                _buttonStates[(int)JoyConButton.DpadRight] = (leftData & 0x04) != 0;
                _buttonStates[(int)JoyConButton.SL] = (leftData & 0x10) != 0;
                _buttonStates[(int)JoyConButton.SR] = (leftData & 0x20) != 0;
                _buttonStates[(int)JoyConButton.L] = (leftData & 0x40) != 0;
                _buttonStates[(int)JoyConButton.ZL] = (leftData & 0x80) != 0;
            }

            var sharedData = data[4];
            _buttonStates[(int)JoyConButton.Minus] = (sharedData & 0x01) != 0;
            _buttonStates[(int)JoyConButton.Plus] = (sharedData & 0x02) != 0;
            _buttonStates[(int)JoyConButton.StickRight] = (sharedData & 0x04) != 0;
            _buttonStates[(int)JoyConButton.StickLeft] = (sharedData & 0x08) != 0;
            _buttonStates[(int)JoyConButton.Home] = (sharedData & 0x10) != 0;
            _buttonStates[(int)JoyConButton.Capture] = (sharedData & 0x20) != 0;
        }

        private byte[] ReceiveRaw()
        {
            var res = new byte[ReceiveLength];
            var len = _device.Read(res);

            return res.AsSpan(0, (int)len).ToArray();
        }
    }

    public enum JoyConType
    {
        Left,
        Right
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum JoyConButton
    {
        DpadUp = 0,
        DpadDown = 1,
        DpadLeft = 2,
        DpadRight = 3,
        Plus = 4,
        Minus = 5,
        Home = 6,
        Capture = 7,
        StickLeft = 8,
        StickRight = 9,
        SL = 10,
        SR = 11,
        L = 12,
        R = 13,
        ZL = 14,
        ZR = 15
    }

    public enum SubCommandType
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