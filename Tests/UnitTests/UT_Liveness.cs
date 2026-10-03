using System.Collections.Generic;
using System.Linq;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Harness keeps an idle Agent online by sending what a real idle 1.127 client sends, at the intervals in the captures under `Fixtures/Agents/v1127`.
    /// </summary>
    [TestFixture]
    public class UT_Liveness
    {
        private static readonly string[] Captures = ClientCaptures.Names;

        // 1 ms, and a game loop running at about 30 ticks per second.
        private static readonly int[] TickDurations = [1, 33];

        private const long SIMULATED_MILLISECONDS = 5 * 60 * 1000;

        [Test]
        public void PingInterval_ShouldMatchCapture([ValueSource(nameof(Captures))] string capture, [ValueSource(nameof(TickDurations))] int tickDuration)
        {
            List<long> pings = Simulate(tickDuration, Liveness.Due.Ping);
            Assert.That(Median(Intervals(pings)), Is.EqualTo(CapturedMedianInterval(capture, eClientPackets.PingRequest)).Within(tickDuration));
        }

        [Test]
        public void PositionUpdateInterval_ShouldMatchCapture([ValueSource(nameof(Captures))] string capture, [ValueSource(nameof(TickDurations))] int tickDuration)
        {
            List<long> positionUpdates = Simulate(tickDuration, Liveness.Due.PositionUpdate);
            Assert.That(Median(Intervals(positionUpdates)), Is.EqualTo(CapturedMedianInterval(capture, eClientPackets.PositionUpdate)).Within(tickDuration));
        }

        // A real client's own clock keeps the rate, however the server's ticks fall. Waiting a whole interval from the tick that sent would fall behind by up to a tick each time.
        [Test]
        public void Schedule_ShouldNotDrift([ValueSource(nameof(Captures))] string capture, [ValueSource(nameof(TickDurations))] int tickDuration)
        {
            const long anHour = 60 * 60 * 1000;

            Assert.That(Simulate(tickDuration, Liveness.Due.Ping, anHour).Count, Is.EqualTo(anHour / CapturedMedianInterval(capture, eClientPackets.PingRequest)).Within(1));
            Assert.That(Simulate(tickDuration, Liveness.Due.PositionUpdate, anHour).Count, Is.EqualTo(anHour / CapturedMedianInterval(capture, eClientPackets.PositionUpdate)).Within(1));
        }

        [Test]
        public void AfterAStall_ShouldSendOnceAndNotCatchUp()
        {
            Liveness liveness = new(0);

            // The game loop stalls for 20 s, then ticks again.
            Assert.That(liveness.Tick(20000), Is.EqualTo(Liveness.Due.Ping | Liveness.Due.PositionUpdate));
            Assert.That(liveness.Tick(20033), Is.EqualTo(Liveness.Due.None));
        }

        // When the packet was due on each tick of a game loop running from 0 for `duration` ms.
        private static List<long> Simulate(int tickDuration, Liveness.Due packet, long duration = SIMULATED_MILLISECONDS)
        {
            Liveness liveness = new(0);
            List<long> times = [];

            for (long now = 0; now <= duration; now += tickDuration)
            {
                if ((liveness.Tick(now) & packet) != 0)
                    times.Add(now);
            }

            return times;
        }

        // How often a real client sent this packet while Playing.
        private static long CapturedMedianInterval(string capture, eClientPackets code)
        {
            return Median(Intervals(ClientCaptures.WhilePlaying(capture, code).Select(static p => p.ElapsedMilliseconds).ToList()));
        }

        private static List<long> Intervals(List<long> times)
        {
            return times.Zip(times.Skip(1), static (previous, next) => next - previous).ToList();
        }

        private static long Median(List<long> values)
        {
            List<long> sorted = values.Order().ToList();
            return sorted[(sorted.Count - 1) / 2];
        }
    }
}
