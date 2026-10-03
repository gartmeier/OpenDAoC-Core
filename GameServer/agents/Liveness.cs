using System;

namespace DOL.GS.Agents
{
    /// <summary>
    /// When a real idle 1.127 client tells the server it's still there, as captured in `Tests/Fixtures/Agents/v1127` (see the README there):
    /// a ping every 5000 ms, and a position update about every 2133 ms even when standing still.
    /// Without position updates the server soft link-deads a Character after 5 s, so pings alone aren't enough.
    /// The client's UDP init and ping are left out: the server falls back to TCP without them.
    /// </summary>
    public sealed class Liveness
    {
        public const int PING_INTERVAL = 5000;
        public const int POSITION_UPDATE_INTERVAL = 2133;

        private long _nextPing;
        private long _nextPositionUpdate;

        [Flags]
        public enum Due
        {
            None = 0,
            Ping = 1,
            PositionUpdate = 2
        }

        // `now` is the game loop time. The first of each is due one interval after it.
        public Liveness(long now)
        {
            _nextPing = now + PING_INTERVAL;
            _nextPositionUpdate = now + POSITION_UPDATE_INTERVAL;
        }

        // Called on every client tick. Returns what the client sends on this one.
        public Due Tick(long now)
        {
            Due due = Due.None;

            if (IsDue(ref _nextPing, PING_INTERVAL, now))
                due |= Due.Ping;

            if (IsDue(ref _nextPositionUpdate, POSITION_UPDATE_INTERVAL, now))
                due |= Due.PositionUpdate;

            return due;
        }

        // Keeps to the interval like the client's own clock, however the server's ticks fall. After a stall it sends once instead of catching up.
        private static bool IsDue(ref long next, int interval, long now)
        {
            if (next - now > 0)
                return false;

            next += interval;

            if (next - now <= 0)
                next = now + interval;

            return true;
        }
    }
}
