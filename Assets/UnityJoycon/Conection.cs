#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityJoycon.Hidapi;

namespace UnityJoycon
{
    internal sealed class Connection : IAsyncDisposable
    {
        private const int ReceiveLength = 0x31;
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(8);
        private static readonly byte[] DefaultRumbleData = { 0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40 };

        private readonly CancellationTokenSource _cts = new();
        private readonly HidDevice _device;
        private readonly Thread _readThread;
        private readonly Side _side;

        private readonly ChannelWriter<State> _stateWriter;

        private readonly Channel<SubCommandReply> _subCommandReplies =
            Channel.CreateBounded<SubCommandReply>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private Calibration? _calibration;
        private bool _disposed;
        private byte _packetCounter;

        private Connection(HidDevice device, Side side, ChannelWriter<State> stateWriter)
        {
            _device = device;
            _side = side;
            _stateWriter = stateWriter;

            _device.SetBlockingMode(false);
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await SendShutdownCommandsAsync();

            _cts.Cancel();

            try
            {
                if (!_readThread.Join(TimeSpan.FromSeconds(1))) _readThread.Join();
            }
            catch (ThreadStateException)
            {
                // Thread already terminated.
            }

            _device.Dispose();
            _cts.Dispose();
            _writeSemaphore.Dispose();

            _subCommandReplies.Writer.TryComplete();
            while (_subCommandReplies.Reader.TryRead(out _))
            {
                // Drain pending replies.
            }

            _stateWriter.TryComplete();
        }

        public static async ValueTask<Connection> CreateAsync(
            HidDevice device,
            Side side,
            ChannelWriter<State> stateWriter,
            CancellationToken cancellationToken)
        {
            var connection = new Connection(device, side, stateWriter);

            try
            {
                await connection.InitializeAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        private async ValueTask InitializeAsync(CancellationToken ct)
        {
            // Input report mode
            await SendSubCommandAsync(JoyConSubCommand.SetInputReportMode, new byte[] { 0x3f }, ct);
            // Calibration
            _calibration = await ReadCalibrationAsync(ct);
            // Connect
            await SendSubCommandAsync(JoyConSubCommand.BluetoothManualPairing, new byte[] { 0x01 }, ct);
            await SendSubCommandAsync(JoyConSubCommand.BluetoothManualPairing, new byte[] { 0x02 }, ct);
            await SendSubCommandAsync(JoyConSubCommand.BluetoothManualPairing, new byte[] { 0x03 }, ct);
            // Set player lights
            await SendSubCommandAsync(JoyConSubCommand.SetPlayerLights, new byte[] { 0b0000_0001 }, ct);
            // Enable IMU
            await SendSubCommandAsync(JoyConSubCommand.EnableImu, new byte[] { 0x01 }, ct);
            // Subscribe sensor data
            await SendSubCommandAsync(JoyConSubCommand.SetInputReportMode, new byte[] { 0x30 }, ct);
            // Enable vibration
            await SendSubCommandAsync(JoyConSubCommand.EnableVibration, new byte[] { 0x01 }, ct);
        }

        private async ValueTask SendShutdownCommandsAsync()
        {
            // Disable vibration
            await SendSubCommandSafelyAsync(JoyConSubCommand.EnableVibration, 0x00);
            // Unsubscribe sensor data
            await SendSubCommandSafelyAsync(JoyConSubCommand.SetInputReportMode, 0x3f);
            // Disable IMU
            await SendSubCommandSafelyAsync(JoyConSubCommand.EnableImu, 0x00);
        }

        private async ValueTask SendSubCommandSafelyAsync(JoyConSubCommand command, byte value)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                await SendSubCommandAsync(command, new[] { value }, linkedCts.Token);
            }
            catch
            {
                // Swallow exceptions so shutdown can't deadlock.
            }
        }

        private async ValueTask<Calibration> ReadCalibrationAsync(CancellationToken ct)
        {
            var stickCalibration = await ReadStickCalibrationAsync(ct);
            var stickParameters = await ReadSpiAsync(SpiFlashAddresses.GetStickParametersAddress(_side), 18, ct);

            // こちらもユーザーと工場出荷時の両方が存在するが、とりあえずユーザーデータのみを読むようにする。
            var imuCalibration = await ReadSpiAsync(SpiFlashAddresses.ImuUserCalibration, 24, ct);
            var imuParameters = await ReadSpiAsync(SpiFlashAddresses.ImuParameters, 6, ct);

            return CalibrationParser.Parse(stickCalibration, stickParameters, imuCalibration, imuParameters,
                _side);
        }

        private async ValueTask<byte[]> ReadStickCalibrationAsync(CancellationToken ct)
        {
            var userData = await ReadSpiAsync(SpiFlashAddresses.GetStickUserCalibrationAddress(_side), 9, ct);
            var hasUserData = userData.Any(b => b != 0xff);
            if (hasUserData) return userData;

            return await ReadSpiAsync(SpiFlashAddresses.GetStickFactoryCalibrationAddress(_side), 9, ct);
        }

        private async ValueTask<byte[]> ReadSpiAsync(uint address, byte length, CancellationToken ct)
        {
            if (length is < 1 or > 0x20) throw new ArgumentOutOfRangeException(nameof(length));

            var payload = new[]
            {
                (byte)(address & 0xff),
                (byte)((address >> 8) & 0xff),
                (byte)((address >> 16) & 0xff),
                (byte)((address >> 24) & 0xff),
                length
            };

            var reply = await SendSubCommandAsync(JoyConSubCommand.SpiFlashRead, payload, ct);

            var receivedAddress = reply.Data[0] | (reply.Data[1] << 8) | (reply.Data[2] << 16) | (reply.Data[3] << 24);
            if (receivedAddress != address)
                throw new InvalidOperationException("SPI flash read address mismatch.");

            var receivedLength = reply.Data[4];
            if (receivedLength != length)
                throw new InvalidOperationException("SPI flash read length mismatch.");

            return reply.Data.Slice(5, receivedLength).ToArray();
        }

        private async ValueTask<SubCommandReply> SendSubCommandAsync(
            JoyConSubCommand command,
            ReadOnlyMemory<byte> data,
            CancellationToken ct)
        {
            await _writeSemaphore.WaitAsync(ct);
            try
            {
                var buffer = BuildCommandBuffer(command, data.Span);
                _device.Write(buffer);

                var reply = await _subCommandReplies.Reader.ReadAsync(ct);
                if (!reply.IsPositive || reply.SubCommand != command)
                    throw new InvalidOperationException(
                        $"Unexpected reply for {command} (ack=0x{reply.Ack:X2}, response={reply.SubCommand}).");

                return reply;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private byte[] BuildCommandBuffer(JoyConSubCommand command, ReadOnlySpan<byte> data)
        {
            var length = 1 + 1 + DefaultRumbleData.Length + 1 + data.Length;
            var buffer = new byte[length];

            buffer[0] = 0x01; // Report ID.
            buffer[1] = _packetCounter;
            _packetCounter = (byte)((_packetCounter + 1) % 16);

            DefaultRumbleData.CopyTo(buffer, 2);
            buffer[10] = (byte)command;
            data.CopyTo(buffer.AsSpan(11));

            return buffer;
        }

        private void ReadLoop()
        {
            var buffer = new byte[ReceiveLength];

            while (!_cts.IsCancellationRequested)
                try
                {
                    var length = _device.ReadTimeout(buffer, ReadTimeout);
                    if (length <= 0) continue;

                    ProcessIncomingReport(buffer, (int)length);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore transient read errors; the channel will surface issues via lack of updates.
                }
        }

        private void ProcessIncomingReport(byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, data, length);

            switch (data[0])
            {
                case 0x21:
                {
                    var report = new StandardReport(data);
                    _subCommandReplies.Writer.TryWrite(report.SubCommandReply);
                    break;
                }
                case 0x30:
                case 0x31:
                case 0x32:
                case 0x33:
                {
                    if (_calibration is null) return;
                    var report = new StandardReport(data);
                    var state = new State(report, _calibration, _side);
                    _stateWriter.TryWrite(state);
                    break;
                }
            }
        }
    }
}