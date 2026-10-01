using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Records every packet a real game client logged into one Account sends, so the Harness can be checked against it byte for byte.
    /// Off unless an admin starts it with `/agent capture`, and off again after a server restart.
    /// Each capture is written to its own file under `packet-captures` in the server's root directory.
    /// </summary>
    public sealed class InboundPacketCapture : IDisposable
    {
        private static InboundPacketCapture _current;

        private readonly string _accountName;
        private readonly TextWriter _writer;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Lock _lock = new();
        private bool _disposed;

        public InboundPacketCapture(string accountName, TextWriter writer)
        {
            _accountName = accountName;
            _writer = writer;
            _writer.WriteLine($"{CapturedPacket.COMMENT_PREFIX} Inbound packets from account '{accountName}', started {DateTime.UtcNow:O}");
            _writer.WriteLine($"{CapturedPacket.COMMENT_PREFIX} elapsed_ms\ttransport\tclient_state\tcode\twire_bytes");
        }

        public static string Start(string accountName)
        {
            string directory = Path.Combine(GameServer.Instance.Configuration.RootDirectory, "packet-captures");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{accountName}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.tsv");
            Stop();
            StreamWriter writer = new(path) { AutoFlush = true };
            Interlocked.Exchange(ref _current, new(accountName, writer))?.Dispose();
            return path;
        }

        public static bool Stop()
        {
            InboundPacketCapture previous = Interlocked.Exchange(ref _current, null);
            previous?.Dispose();
            return previous != null;
        }

        // Called for every inbound packet with a valid checksum, before it's handled.
        public static void Record(GameClient client, CaptureTransport transport, byte[] buffer, int offset, int count)
        {
            Volatile.Read(ref _current)?.Add(client.Account?.Name, client.ClientState, transport, buffer.AsSpan(offset, count));
        }

        public void Add(string accountName, GameClient.eClientState clientState, CaptureTransport transport, ReadOnlySpan<byte> bytes)
        {
            if (!string.Equals(accountName, _accountName, StringComparison.OrdinalIgnoreCase))
                return;

            CapturedPacket packet = new(_stopwatch.ElapsedMilliseconds, transport, clientState, bytes.ToArray());

            // Writing synchronously on the game loop is acceptable here: capturing is a manual, one-account diagnostic.
            lock (_lock)
            {
                if (!_disposed)
                    _writer.WriteLine(packet.Format());
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _writer.Dispose();
            }
        }
    }
}
