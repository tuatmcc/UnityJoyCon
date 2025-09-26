#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityJoycon.Hidapi;

namespace UnityJoycon
{
    /// <summary>
    ///     High level Joy-Con façade that exposes the latest controller state and manages the native connection lifecycle.
    /// </summary>
    public sealed class JoyCon : IAsyncDisposable
    {
        private readonly Channel<State> _stateChannel = Channel.CreateBounded<State>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        private Connection? _connection;
        private bool _disposed;
        private State? _latestState;

        private JoyCon(Side side)
        {
            Side = side;
        }

        /// <summary>Identifies whether this Joy-Con is the left or the right controller.</summary>
        public Side Side { get; }

        /// <summary>Returns the most recent controller state, or <c>null</c> if no packets have been received yet.</summary>
        public State? State
        {
            get
            {
                if (_stateChannel.Reader.TryRead(out var state))
                {
                    Volatile.Write(ref _latestState, state);
                    return state;
                }

                return Volatile.Read(ref _latestState);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            _stateChannel.Writer.TryComplete();
            while (_stateChannel.Reader.TryRead(out _))
            {
                // Drain any remaining buffered state to release references promptly.
            }
        }

        /// <summary>Tries to obtain the latest cached controller state without allocating.</summary>
        public bool TryGetState([NotNullWhen(true)] out State? state)
        {
            state = State;
            return state is not null;
        }

        /// <summary>
        ///     Establishes a Joy-Con session over the specified HID device.
        ///     Ownership of <paramref name="device" /> is transferred to the created instance.
        /// </summary>
        public static async ValueTask<JoyCon> CreateAsync(HidDevice device,
            CancellationToken cancellationToken = default)
        {
            if (device is null) throw new ArgumentNullException(nameof(device));

            var info = device.GetInfo();
            var side = ResolveSide(info.ProductId);
            var joyCon = new JoyCon(side);

            joyCon._connection =
                await Connection.CreateAsync(device, side, joyCon._stateChannel.Writer, cancellationToken);

            return joyCon;
        }

        private static Side ResolveSide(ushort productId)
        {
            return productId switch
            {
                ProductIds.Left => Side.Left,
                ProductIds.Right => Side.Right,
                _ => throw new ArgumentException($"Invalid product ID ({productId:X4}) for Joy-Con", nameof(productId))
            };
        }

        private static class ProductIds
        {
            public const ushort Left = 0x2006;
            public const ushort Right = 0x2007;
        }
    }
}