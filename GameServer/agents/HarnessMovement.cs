using System.Numerics;
using DOL.GS.PacketHandler.Client.v168;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What the Agent's client does to move its Character, since a real client owns its own position (ADR 0002): it runs the Character to a target and tells the server where it is on the way.
    /// While running it sends a position update about every 200 ms, as the captured client does, and tells the Mind what the client works out itself: `moved`, `arrived` and `stopped`.
    /// The position updates go into the Harness client's inbound queue, so the server's handlers and its speed check see them as a Human's.
    /// It moves only at the maximum speed the server last told the client. Called on the game loop, only while the Character is in the world and a Mind is connected (ADR 0005).
    /// </summary>
    public sealed class HarnessMovement
    {
        // In milliseconds. The captured client sends one every 200 or 233 ms while running, and the server broadcasts at most one every 200 ms.
        public const int RUNNING_POSITION_UPDATE_INTERVAL = 200;

        // The flag the captured client sets in the heading of nearly every position update.
        private const ushort HEADING_FLAG = 0x1000;

        private readonly HarnessClient _client;
        private StraightRun _run;
        private long _nextPositionUpdate;

        public HarnessMovement(HarnessClient client)
        {
            _client = client;
        }

        // In game units per second, from the last maximum speed update. 0 until the server has sent one.
        public float MaxSpeed { get; private set; }

        public bool IsRunning => _run != null;

        // The server told the client how fast its Character may run. A run in progress keeps to it from now on.
        public void OnMaxSpeed(MaxSpeedPercept percept, long now)
        {
            MaxSpeed = percept.MaxSpeedPercent * GamePlayer.PLAYER_BASE_SPEED / 100f;
            _run?.ChangeMaxSpeed(MaxSpeed, now);
        }

        // Starts running to `target`, from where the Character is. A run in progress ends with `stopped` first, and the new one keeps its speed.
        public void MoveTo(Vector3 target, long now)
        {
            GamePlayer player = _client.Player;
            Vector3 start = new(player.X, player.Y, player.Z);
            float speed = 0;

            if (_run != null)
            {
                StraightRun.Step step = _run.At(now);
                start = step.Position;

                // It got there before its next position update.
                if (step.Ended)
                    Send(step, now);
                else
                {
                    speed = step.Speed;
                    _run = null;
                    Perceive(new StoppedPercept(start.X, start.Y, start.Z, StopReason.Replaced));
                }
            }

            // The captured client sends its first position update as soon as the Character moves.
            if (speed == 0)
                _nextPositionUpdate = now;

            _run = new(start, target, player.Heading, speed, MaxSpeed, player.CurrentRegion, now);
        }

        // Ends the run in progress where the Character is. Returns whether there was one.
        // The position update with speed 0 goes out at once, so that the server knows the Character stands before anything the Mind sends next, such as a `/quit`.
        public bool Stop(long now)
        {
            if (_run == null)
                return false;

            _run.Stop(now);
            Send(_run.At(now), now);
            return true;
        }

        // Called on every client tick. Returns whether it sent a position update on this one.
        public bool Tick(long now)
        {
            if (_run == null || _nextPositionUpdate - now > 0)
                return false;

            Send(_run.At(now), now);
            return true;
        }

        // A position update for where the run has got to, and what the client works out from it.
        private void Send(StraightRun.Step step, long now)
        {
            GamePlayer player = _client.Player;
            Vector3 position = step.Position;
            _client.Enqueue(ClientPackets1127.PositionUpdate(position, step.ZoneId, (ushort) (step.Heading | HEADING_FLAG), step.Speed, player.HealthPercent, _client.SessionID, 0, player.ActionFlags));
            NextPositionUpdate(now);

            if (!step.Ended)
            {
                Vector3 target = _run.Target;
                Perceive(new MovedPercept(position.X, position.Y, position.Z, step.Heading, step.Speed, target.X, target.Y, target.Z));
                return;
            }

            _run = null;
            Perceive(step.Arrived ? new ArrivedPercept(position.X, position.Y, position.Z) : new StoppedPercept(position.X, position.Y, position.Z, StopReason.Stop));
        }

        // Keeps to the interval like the client's own clock, however the server's ticks fall. After a stall it sends once instead of catching up, as liveness does.
        private void NextPositionUpdate(long now)
        {
            _nextPositionUpdate += RUNNING_POSITION_UPDATE_INTERVAL;

            if (_nextPositionUpdate - now <= 0)
                _nextPositionUpdate = now + RUNNING_POSITION_UPDATE_INTERVAL;
        }

        private void Perceive(Percept percept)
        {
            _client.Mind.Perceive(percept);
        }
    }
}
