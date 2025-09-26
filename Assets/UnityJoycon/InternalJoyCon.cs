#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace UnityJoycon
{
    internal class InternalJoyCon : IAsyncDisposable
    {
        private const int ReceiveLength = 0x31;

        private readonly CancellationTokenSource _cts = new();

        private readonly byte[] _defaultRumbleData = { 0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40 };
        private readonly HidDevice _device;
        private readonly Thread _readThread;
        private readonly ChannelWriter<State> _stateWriter;

        private readonly Channel<SubCommandReply> _subCommandChannel =
            Channel.CreateBounded<SubCommandReply>(new BoundedChannelOptions(1)
                { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });

        private readonly Type _type;
        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
        private Calibration? _calibration;

        private bool _disposedValue;
        private byte _packetCounter;

        private InternalJoyCon(HidDevice device, Type type,
            ChannelWriter<State> stateWriter)
        {
            _device = device;
            _type = type;
            _stateWriter = stateWriter;

            _device.SetBlockingMode(false);
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposedValue) return;

            // Disable vibration
            await SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x00 });
            // Unsubscribe sensor data
            await SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f });
            // Disable IMU
            await SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x00 });

            _cts.Cancel();
            _readThread.Join();

            _device.Dispose();

            _disposedValue = true;
        }

        internal static async ValueTask<InternalJoyCon> Create(HidDevice device, Type type,
            ChannelWriter<State> stateWriter, CancellationToken ct = default)
        {
            var joycon = new InternalJoyCon(device, type, stateWriter);

            // Input report mode
            await joycon.SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x3f }, ct);
            // Calibration
            joycon._calibration = await joycon.ReadCalibration(ct);
            // Connect
            await joycon.SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x01 }, ct);
            await joycon.SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x02 }, ct);
            await joycon.SendSubCommand(SubCommandType.BluetoothManualPairing, new byte[] { 0x03 }, ct);
            // Set player lights
            await joycon.SendSubCommand(SubCommandType.SetPlayerLights, new byte[] { 0b0000_0001 }, ct);
            // Enable IMU
            await joycon.SendSubCommand(SubCommandType.EnableImu, new byte[] { 0x01 }, ct);
            // Subscribe sensor data
            await joycon.SendSubCommand(SubCommandType.SetInputReportMode, new byte[] { 0x30 }, ct);
            // Enable vibration
            await joycon.SendSubCommand(SubCommandType.EnableVibration, new byte[] { 0x01 }, ct);

            return joycon;
        }

        private void ReadLoop()
        {
            var buf = new byte[ReceiveLength];

            while (!_cts.IsCancellationRequested)
                try
                {
                    var len = _device.ReadTimeout(buf, TimeSpan.FromMilliseconds(8));
                    if (len <= 0) continue;

                    var reportId = buf[0];
                    switch (reportId)
                    {
                        // Sub command reply
                        case 0x21:
                        {
                            var data = buf[..(int)len];
                            var report = new StandardReport(data);
                            var reply = report.SubCommandReply;

                            _subCommandChannel.Writer.TryWrite(reply);
                            break;
                        }
                        // Standard input report (IMU)
                        case 0x30:
                        case 0x31:
                        case 0x32:
                        case 0x33:
                        {
                            if (_calibration is null) break;
                            var data = buf[..(int)len];
                            var report = new StandardReport(data);
                            var state = new State(report, _calibration, _type);
                            _stateWriter.TryWrite(state);

                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
        }

        private async ValueTask<Calibration> ReadCalibration(CancellationToken ct = default)
        {
            // ユーザーのキャリブレーション設定を読み込む
            var stickCalData = await ReadSpi(SpiAddresses.GetStickUserCalibrationAddress(_type), 9, ct);
            var foundUserStickCalData = stickCalData.Any(b => b != 0xff);
            // ユーザーのキャリブレーション設定が保存されていない場合
            if (!foundUserStickCalData)
                // 工場出荷時のキャリブレーション設定を読み込む
                stickCalData = await ReadSpi(SpiAddresses.GetStickFactoryCalibrationAddress(_type), 9, ct);

            // スティックのパラメータを読み込む
            var stickParamData = await ReadSpi(SpiAddresses.GetStickParametersAddress(_type), 18, ct);


            // ユーザーのIMUのキャリブレーション設定を読み込む
            // メモ: -0.24, -0.10, -0.30付近で静止
            // TODO: 工場出荷時のIMUのキャリブレーション設定を読み込む
            // メモ: 0.24, -0.73, -1.16付近で静止
            var imuCalData = await ReadSpi(SpiAddresses.ImuUserCalibration, 24, ct);

            // IMUのパラメータを読み込む
            var imuParamData = await ReadSpi(SpiAddresses.ImuParameters, 6, ct);

            return CalibrationParser.Parse(stickCalData, stickParamData, imuCalData, imuParamData, _type);
        }

        private async ValueTask<SubCommandReply> SendSubCommand(SubCommandType cmdType, byte[] cmdData,
            CancellationToken ct = default)
        {
            await _writeSemaphore.WaitAsync(ct);
            try
            {
                // Report ID (1) + Packet Counter (1) + Rumble Data (8) + Sub Command Type (1) + Command Data (n)
                var len = 1 + 1 + _defaultRumbleData.Length + 1 + cmdData.Length;
                var buf = new byte[len];

                buf[0] = 0x01; // Report ID
                buf[1] = _packetCounter;
                _packetCounter = (byte)((_packetCounter + 1) % 16);
                _defaultRumbleData.CopyTo(buf, 2);
                buf[10] = (byte)cmdType;
                cmdData.CopyTo(buf, 11);
                _device.Write(buf);

                var reply = await _subCommandChannel.Reader.ReadAsync(ct);
                return reply;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async ValueTask<byte[]> ReadSpi(uint addr, byte length, CancellationToken ct = default)
        {
            if (length is < 1 or > 0x20) throw new ArgumentOutOfRangeException(nameof(length));
            var cmdData = new[]
            {
                (byte)(addr & 0xff), (byte)((addr >> 8) & 0xff), (byte)((addr >> 16) & 0xff),
                (byte)((addr >> 24) & 0xff), length
            };
            var res = await SendSubCommand(SubCommandType.SpiFlashRead, cmdData, ct);
            if (!res.IsPositive || res.SubCommandType != SubCommandType.SpiFlashRead)
                throw new InvalidOperationException("Failed to read SPI flash.");

            var receivedAddress = res.Data[0] | (res.Data[1] << 8) | (res.Data[2] << 16) | (res.Data[3] << 24);
            if (receivedAddress != addr)
                throw new InvalidOperationException("SPI flash read address mismatch.");

            var receivedLength = res.Data[4];
            if (receivedLength != length)
                throw new InvalidOperationException("SPI flash read length mismatch.");

            return res.Data.Slice(5, receivedLength).ToArray();
        }
    }
}