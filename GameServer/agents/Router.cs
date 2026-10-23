using System;
using System.Collections.Generic;
using System.Numerics;
using OpenDAoC.Pathing;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Works out the route a `moveTo` runs along, on the zones' navmeshes, as the Agent's client would if it had a map (ADR 0007).
    /// It uses only static knowledge (ADR 0006): routes go over walkable ground and through doors that open by clicking, which the navmesh marks the same open or closed, but never through blocking doors, water or jump links.
    /// The Agent walks through such a door even while it's closed: the server doesn't stop it, though a Human's client would until they clicked the door open.
    /// Where the navmesh doesn't join a door to the ground on one side of it, as at the raised floor of a hut, the route steps across the gap in a straight line, if it's no wider than a doorway's step.
    /// Navmeshes are per zone, so a target in another zone is routed to a crossing on the zone border, as the server's NPC pathing does, and on from there in the next zone.
    /// In a zone without a navmesh the route goes on in a straight line.
    /// </summary>
    public sealed class Router
    {
        // [include, exclude]. The server marks a door that opens by clicking as a door whether it's open or closed, and only a closed door that doesn't as blocking (`LocalPathfindingMgr`).
        // So routes through doors don't depend on the state of any door that opens by clicking. One that doesn't, such as a keep door, is routed through only while it's open.
        private static readonly EDtPolyFlags[] _filters = [EDtPolyFlags.Walk | EDtPolyFlags.Door, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Swim | EDtPolyFlags.Jump | EDtPolyFlags.Disabled];

        // Only the doors among them.
        private static readonly EDtPolyFlags[] _doorFilters = [EDtPolyFlags.Door, _filters[1]];

        // How far from the end of a leg, in X and Y and in Z, to look for a door to step through when the leg can't get there.
        private const float DOOR_RANGE = 512;
        private const float DOOR_HEIGHT_RANGE = 256;

        // The widest gap at a door the route steps across, in game units.
        private const float DOOR_STEP = 64;

        // How far, in each direction, the nearest walkable point to the Character or the target may be.
        private const float WALKABLE_RANGE = 1024;

        // How far the nearest walkable point may be from the target, in X and Y, for the route to reach it.
        private const float CLOSE_ENOUGH = 128;

        // How close the end of a query's route must be to where it was asked to go to count as there.
        private const float ARRIVED = 2;

        // A leg longer than this many queries' worth of corners ends as close as it got.
        private const int MAX_QUERIES_PER_LEG = 8;

        // How far above or below a position on the route its floor may be.
        private const float FLOOR_RANGE = 128;

        // How far from the border a crossing starts and ends, and how far around them to look for walkable ground, as the server's NPC pathing does.
        private const float BORDER_MARGIN = 8;
        private const float BORDER_RANGE = 128;
        private const float BORDER_HEIGHT_RANGE = 4000;

        // A route crosses at most this many zone borders.
        private const int MAX_CROSSINGS = 8;

        private readonly IPathfindingMgr _pathfinding;
        private readonly Region _region;
        private readonly WrappedPathfindingNode[] _nodes = new WrappedPathfindingNode[DetourNavMeshQuery.MAX_POLY];

        public Router(IPathfindingMgr pathfinding, Region region)
        {
            _pathfinding = pathfinding;
            _region = region;
        }

        /// <summary>
        /// The route from `start`, where the Character is, to the walkable point nearest `target`, both global, with `within` taken off its end if it reaches the target.
        /// A route that can't reach the target goes as far towards it as it can, and one that can't start has only `start`.
        /// </summary>
        public Route Find(Vector3 start, Vector3 target, float? within)
        {
            List<Vector3> corners = [start];

            if (WalkableNear(ZoneAt(target), target, WALKABLE_RANGE, WALKABLE_RANGE) is not Vector3 end)
                return new([start], false);

            // The target's Z need only be roughly right: the nearest walkable point may be well above or below it.
            bool reachesTarget = new Vector2(end.X - target.X, end.Y - target.Y).Length() <= CLOSE_ENOUGH;
            Vector3 position = start;

            for (int crossings = 0; ; crossings++)
            {
                Zone zone = ZoneAt(position);
                // Outside every zone, there's no border to cross.
                bool lastZone = zone == null || zone.IsPointInZone(end);
                Vector3 legEnd = end;
                Vector3 entry = default;

                if (!lastZone && (crossings == MAX_CROSSINGS || !TryCrossing(zone, position, end, out legEnd, out entry)))
                {
                    reachesTarget = false;
                    break;
                }

                if (!HasNavmesh(zone))
                    Add(corners, legEnd);
                else if (!TryLeg(zone, position, legEnd, corners))
                {
                    reachesTarget = false;
                    break;
                }

                if (lastZone)
                    break;

                Add(corners, entry);
                position = entry;
            }

            Vector3[] route = corners.ToArray();
            return new(reachesTarget && within > 0 ? Trim(route, within.Value) : route, reachesTarget);
        }

        /// <summary>
        /// The Z of the walkable ground under a position on a route, or null where there's no navmesh or no ground near it.
        /// </summary>
        public float? FloorAt(Vector3 position)
        {
            Zone zone = ZoneAt(position);

            if (!HasNavmesh(zone))
                return null;

            return _pathfinding.GetClosestPointInBounds(zone, position, new(-3, -3, -FLOOR_RANGE), new(3, 3, FLOOR_RANGE), _filters)?.Z;
        }

        // Adds the route through `zone` from `position` to `legEnd`, both in it, stepping through a door where the navmesh leaves a gap at it (see `TryDoorStep`).
        // Returns whether it got to `legEnd`. Where it gets no further, the route ends as close as it could.
        private bool TryLeg(Zone zone, Vector3 position, Vector3 legEnd, List<Vector3> corners)
        {
            if (WalkableNear(zone, position, WALKABLE_RANGE, WALKABLE_RANGE) is not Vector3 from)
                return false;

            Add(corners, from);
            int start = corners.Count;

            if (TryPath(zone, from, legEnd, corners))
                return true;

            List<Vector3> throughDoor = [from];

            if (!TryDoorStep(zone, from, legEnd, throughDoor))
                return false;

            corners.RemoveRange(start, corners.Count - start);

            foreach (Vector3 corner in throughDoor)
                Add(corners, corner);

            return true;
        }

        // Where the navmesh doesn't join a door to the ground on one side of it, a route can't get through. So the route goes from `from` towards the door nearest `legEnd`, and from `legEnd` back towards it.
        // Where the two meet within `DOOR_STEP` of each other and of the door, it steps across in a straight line, as a Human's client walks through the doorway. A door is never stepped through anywhere else.
        private bool TryDoorStep(Zone zone, Vector3 from, Vector3 legEnd, List<Vector3> corners)
        {
            if (_pathfinding.GetClosestPoint(zone, legEnd, DOOR_RANGE, DOOR_RANGE, DOOR_HEIGHT_RANGE, _doorFilters) is not Vector3 door)
                return false;

            List<Vector3> back = [legEnd];
            TryPath(zone, legEnd, door, back);
            Vector3 farSide = back[^1];
            TryPath(zone, from, door, corners);
            Vector3 nearSide = corners[^1];

            if (Vector3.Distance(nearSide, farSide) > DOOR_STEP || Vector3.Distance(nearSide, door) > DOOR_STEP || Vector3.Distance(farSide, door) > DOOR_STEP)
                return false;

            Add(corners, farSide);
            return TryPath(zone, farSide, legEnd, corners);
        }

        // Adds the navmesh's route from `from` to `to`, in one zone. One query gives at most `MAX_POLY` corners, so a long route is worked out on from where each query got to.
        // Returns whether it got to `to`. Where a query gets no further, the route ends as close as it could.
        private bool TryPath(Zone zone, Vector3 from, Vector3 to, List<Vector3> corners)
        {
            for (int queries = 0; queries < MAX_QUERIES_PER_LEG; queries++)
            {
                PathfindingResult result = _pathfinding.GetPathStraight(zone, from, to, _filters, _nodes);

                if (result.Status is not (PathfindingStatus.PathFound or PathfindingStatus.PartialPathFound) || result.NodeCount == 0)
                    return false;

                for (int i = 0; i < result.NodeCount; i++)
                    Add(corners, _nodes[i].Position);

                // The server's pathfinding reports a query cut short by the corner limit as found, so only where it ended says whether it got there.
                Vector3 last = _nodes[result.NodeCount - 1].Position;

                if (Vector3.Distance(last, to) <= ARRIVED)
                    return true;

                if (Vector3.Distance(last, from) <= ARRIVED)
                    return false;

                from = last;
            }

            return false;
        }

        private Zone ZoneAt(Vector3 position)
        {
            return _region.GetZone((int) position.X, (int) position.Y);
        }

        private bool HasNavmesh(Zone zone)
        {
            return zone != null && _pathfinding.HasNavmesh(zone);
        }

        // The walkable point nearest `position` in `zone`, within `range` in X and Y and `heightRange` in Z, or `position` itself in a zone without a navmesh.
        private Vector3? WalkableNear(Zone zone, Vector3 position, float range, float heightRange)
        {
            return HasNavmesh(zone) ? _pathfinding.GetClosestPoint(zone, position, range, range, heightRange, _filters) : position;
        }

        // Where the straight line from `position` to `target` leaves `zone`: the walkable points just inside it (`exit`) and just inside the next zone (`entry`).
        private bool TryCrossing(Zone zone, Vector3 position, Vector3 target, out Vector3 exit, out Vector3 entry)
        {
            Vector3 inside = position;
            Vector3 outside = target;

            while ((outside - inside).LengthSquared() > 0.25f)
            {
                Vector3 middle = (inside + outside) / 2;

                if (zone.IsPointInZone(middle))
                    inside = middle;
                else
                    outside = middle;
            }

            Vector3 direction = Vector3.Normalize(target - position);
            exit = default;
            entry = default;

            if (WalkableNear(zone, inside - direction * BORDER_MARGIN, BORDER_RANGE, BORDER_HEIGHT_RANGE) is not Vector3 walkableExit)
                return false;

            Vector3 over = outside + direction * BORDER_MARGIN;
            Zone next = ZoneAt(over);

            if (next == null || WalkableNear(next, over, BORDER_RANGE, BORDER_HEIGHT_RANGE) is not Vector3 walkableEntry)
                return false;

            (exit, entry) = (walkableExit, walkableEntry);
            return true;
        }

        // Leaves out a corner where the route already is.
        private static void Add(List<Vector3> corners, Vector3 corner)
        {
            if (corners[^1] != corner)
                corners.Add(corner);
        }

        // The route with `within` taken off its end, measured in X and Y as the run is. A route no longer than that is only its start.
        private static Vector3[] Trim(Vector3[] route, float within)
        {
            float left = within;

            for (int i = route.Length - 1; i > 0; i--)
            {
                Vector3 from = route[i - 1];
                Vector3 to = route[i];
                float leg = new Vector2(to.X - from.X, to.Y - from.Y).Length();

                if (leg > left)
                {
                    Vector3[] trimmed = route[..(i + 1)];
                    trimmed[i] = Vector3.Lerp(to, from, left / leg);
                    return trimmed;
                }

                left -= leg;
            }

            return [route[0]];
        }
    }

    /// <summary>
    /// The corners a run goes through, all global, the first where the Character is. `ReachesTarget` is false when it can't get to the target and ends as close as it could.
    /// </summary>
    public sealed record Route(Vector3[] Corners, bool ReachesTarget);
}
