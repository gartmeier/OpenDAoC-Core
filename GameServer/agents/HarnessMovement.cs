using System.Linq;
using System.Numerics;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What the Agent's client does to move its Character, since a real client owns its own position (ADR 0002): it runs the Character to a target, along a route on the navmesh where there is one (ADR 0007), and tells the server where it is on the way.
    /// While running it sends a position update about every 200 ms, as the captured client does, and tells the Mind what the client works out itself: `routed`, `moved`, `arrived` and `stopped`.
    /// The position updates go into the Harness client's inbound queue, so the server's handlers and its speed check see them as a Human's.
    /// It moves only at the maximum speed the server last told the client, and a maximum speed of 0 ends the run. A jump ends it too: the server has set the Character's position, and the client takes it.
    /// Called on the game loop, only while the Character is in the world and a Mind is connected (ADR 0005).
    /// </summary>
    public sealed class HarnessMovement
    {
        // In milliseconds. The captured client sends one every 200 or 233 ms while running, and the server broadcasts at most one every 200 ms.
        public const int RUNNING_POSITION_UPDATE_INTERVAL = 200;

        // The flag the captured client sets in the heading of nearly every position update.
        private const ushort HEADING_FLAG = 0x1000;

        private readonly IMovingClient _client;
        private readonly IPathfindingMgr _pathfinding;
        private RouteRun _run;
        // The `moveTo`'s target, and whether the run's route gets to it.
        private Vector3 _target;
        private bool _reachesTarget;
        private long _nextPositionUpdate;

        // `pathfinding` is the server's, with the zones' navmeshes (ADR 0007).
        public HarnessMovement(IMovingClient client, IPathfindingMgr pathfinding)
        {
            _client = client;
            _pathfinding = pathfinding;
        }

        // In game units per second, from the last maximum speed update. 0 until the server has sent one.
        public float MaxSpeed { get; private set; }

        public bool IsRunning => _run != null;

        // The server told the client how fast its Character may run. A run in progress keeps to it from now on, or ends where it is if it's 0, for example while the Character is rooted.
        // Returns whether it sent a position update.
        public bool OnMaxSpeed(MaxSpeedPercept percept, long now)
        {
            MaxSpeed = percept.MaxSpeedPercent * GamePlayer.PLAYER_BASE_SPEED / 100f;

            if (_run == null)
                return false;

            if (MaxSpeed <= 0)
            {
                End(StopReason.UnableToMove, now);
                return true;
            }

            _run.ChangeMaxSpeed(MaxSpeed, now);
            return false;
        }

        // The server set the Character's position itself, and the client takes it as it is: a run in progress ends there, without a position update of its own.
        // Position updates still queued would carry the Character back to where it was, so they're dropped, and the next one carries the server's position. The server's speed check, paused by the jump, then never sees it as movement.
        // A jump that sets only the heading changes nothing here: the Character stands facing that way, or the run's own heading overtakes it at the next position update.
        public void OnJump(JumpPercept jump)
        {
            if (jump.X == 0 && jump.Y == 0)
                return;

            _client.Unqueue((byte) eClientPackets.PositionUpdate);

            if (_run == null)
                return;

            _run = null;
            Perceive(new StoppedPercept(jump.X, jump.Y, jump.Z, StopReason.MovedByServer));
        }

        // Starts running to `target`, from where the Character is, along the route the navmesh gives, `within` short of its end (ADR 0007). The Mind hears the route first.
        // A run in progress ends with `stopped` first, and the new one keeps its speed.
        // With a maximum speed of 0 it doesn't start, and the Mind hears `stopped` where the Character stands.
        // A route with nowhere to go ends at once where the Character stands: `arrived` if it's already there, `stopped` with `Unreachable` if it can't get anywhere.
        public void MoveTo(Vector3 target, float? within, long now)
        {
            GamePlayer player = _client.Player;
            Vector3 start = new(player.X, player.Y, player.Z);
            float speed = 0;

            // No run can be in progress: a maximum speed of 0 ended it.
            if (MaxSpeed <= 0)
            {
                Perceive(new StoppedPercept(start.X, start.Y, start.Z, StopReason.UnableToMove));
                return;
            }

            if (_run != null)
            {
                RouteRun.Step step = _run.At(now);
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

            Router router = new(_pathfinding, player.CurrentRegion);
            Route route = router.Find(start, target, within);
            Perceive(new RoutedPercept(route.Corners.Select(static corner => new RouteCorner(corner.X, corner.Y, corner.Z)).ToArray(), route.ReachesTarget));
            _run = new(route.Corners, player.Heading, speed, MaxSpeed, player.CurrentRegion, router.FloorAt, now);
            _target = target;
            _reachesTarget = route.ReachesTarget;

            if (route.Corners.Length > 1)
            {
                // The captured client sends its first position update as soon as the Character moves.
                if (speed == 0)
                    _nextPositionUpdate = now;

                return;
            }

            // Nowhere to go. A Character still going from the run it replaced stops, with a position update, and one standing stays as it is.
            if (speed > 0)
                Send(_run.At(now), now);
            else
                Ended(WholeUnits(start), true, StopReason.Stop);
        }

        // Ends the run in progress where the Character is. Returns whether there was one.
        // The position update with speed 0 goes out at once, so that the server knows the Character stands before anything the Mind sends next, such as a `/quit`.
        public bool Stop(long now)
        {
            if (_run == null)
                return false;

            End(StopReason.Stop, now);
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

        // Ends the run in progress where the Character is at `now`, at once, with a position update with speed 0. If it got to its target first, it has arrived instead.
        private void End(StopReason reason, long now)
        {
            _run.Stop(now);
            Send(_run.At(now), now, reason);
        }

        // A position update for where the run has got to, and what the client works out from it. `reason` is why the run ended, if a stop ended it.
        // A run that got to the end of a route that can't reach the target ends `Unreachable`.
        private void Send(RouteRun.Step step, long now, StopReason reason = StopReason.Stop)
        {
            GamePlayer player = _client.Player;
            Vector3 position = step.Ended ? WholeUnits(step.Position) : step.Position;
            _client.Enqueue(ClientPackets1127.PositionUpdate(position, step.ZoneId, (ushort) (step.Heading | HEADING_FLAG), step.Speed, player.HealthPercent, _client.SessionID, 0, _client.ActionFlags, _client.TargetId));
            NextPositionUpdate(now);

            if (!step.Ended)
            {
                Perceive(new MovedPercept(position.X, position.Y, position.Z, step.Heading, step.Speed, _target.X, _target.Y, _target.Z));
                return;
            }

            Ended(position, step.Arrived, reason);
        }

        // The run ended at `position`, at the end of its route or, if not, for `reason`. The end of a route that can't reach the target is `Unreachable`.
        private void Ended(Vector3 position, bool atTheEnd, StopReason reason)
        {
            _run = null;

            if (atTheEnd && _reachesTarget)
                Perceive(new ArrivedPercept(position.X, position.Y, position.Z));
            else
                Perceive(new StoppedPercept(position.X, position.Y, position.Z, atTheEnd ? StopReason.Unreachable : reason));
        }

        // Where a run ends. The server keeps only whole units of the position a client sends, and the idle position update repeats what the server kept.
        // A fraction of a unit between the two is a move to the server, which stands a sitting Character up and so cancels a quit.
        public static Vector3 WholeUnits(Vector3 position)
        {
            return new((int) position.X, (int) position.Y, (int) position.Z);
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
            _client.Perceive(percept);
        }
    }

    /// <summary>
    /// What <see cref="HarnessMovement"/> needs from the Agent's client: its Character, the packets it sends and what it tells the Mind. Called on the game loop.
    /// </summary>
    public interface IMovingClient
    {
        GamePlayer Player { get; }

        ushort SessionID { get; }

        // What the client has selected, 0 for nothing, and the action flags its position updates carry.
        ushort TargetId { get; }

        PlayerPositionUpdateHandler.ActionFlags ActionFlags { get; }

        // Queues a packet for the next client tick, after those already queued.
        void Enqueue(ClientPacket packet);

        // Drops every queued packet with `code`, leaving the others in order.
        void Unqueue(byte code);

        // Tells the Mind what the client worked out itself, in order with what the server told it.
        void Perceive(Percept percept);
    }
}
