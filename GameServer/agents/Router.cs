using System;
using System.Collections.Generic;
using System.Numerics;
using OpenDAoC.Pathing;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Works out the route a `moveTo` runs along, on the zones' navmeshes, as the Agent's client would if it had a map (ADR 0007).
    /// It uses only static knowledge (ADR 0006): routes go over walkable ground, across water on the surface, and through doors that open by clicking, which the navmesh marks the same open or closed, but never through blocking doors or jump links.
    /// The Agent walks through such a door even while it's closed: the server doesn't stop it, though a Human's client would until they clicked the door open.
    /// It works out a route over land only and one that may swim, and gives the faster: swimming is slower than running (`RouteRun.SWIM_SPEED`).
    /// Where the navmesh doesn't join a door to the ground on one side of it, as at the raised floor of a hut, the route steps across the gap in a straight line, if it's no wider than a doorway's step.
    /// Navmeshes are per zone, so a target in another zone is routed to a crossing on the zone border, as the server's NPC pathing does, and on from there in the next zone.
    /// In a zone without a navmesh the route goes on in a straight line.
    /// </summary>
    public sealed class Router
    {
        // [include, exclude]. The server marks a door that opens by clicking as a door whether it's open or closed, and only a closed door that doesn't as blocking (`LocalPathfindingMgr`).
        // So routes through doors don't depend on the state of any door that opens by clicking. One that doesn't, such as a keep door, is routed through only while it's open.
        private static readonly EDtPolyFlags[] _landFilters = [EDtPolyFlags.Walk | EDtPolyFlags.Door, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Swim | EDtPolyFlags.Jump | EDtPolyFlags.Disabled];

        // The same, across water too.
        private static readonly EDtPolyFlags[] _swimFilters = [EDtPolyFlags.Walk | EDtPolyFlags.Door | EDtPolyFlags.Swim, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Jump | EDtPolyFlags.Disabled];

        // Only the water, under a Swim leg.
        private static readonly EDtPolyFlags[] _waterFilters = [EDtPolyFlags.Swim, _swimFilters[1]];

        // Only the doors.
        private static readonly EDtPolyFlags[] _doorFilters = [EDtPolyFlags.Door, _landFilters[1]];

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
        // Those of the route being worked out: over land only, or swimming too.
        private EDtPolyFlags[] _filters;

        public Router(IPathfindingMgr pathfinding, Region region)
        {
            _pathfinding = pathfinding;
            _region = region;
        }

        /// <summary>
        /// The faster route from `start`, where the Character is, to the walkable point nearest `target`, both global, with `within` taken off its end if it reaches the target.
        /// A route that reaches the target beats one that doesn't. Of two that don't, the one that ends nearer the target wins. A tie goes to the one over land.
        /// A route that can't reach the target goes as far towards it as it can, and one that can't start has only `start`.
        /// </summary>
        public Route Find(Vector3 start, Vector3 target, float? within)
        {
            Route land = Find(start, target, _landFilters);
            Route swim = Find(start, target, _swimFilters);
            Route route = IsBetter(swim, land, target) ? swim : land;
            return route.ReachesTarget && within > 0 ? Trim(route, within.Value) : route;
        }

        /// <summary>
        /// The Z of the ground under a position on a route, or of the navmesh's water on a Swim leg (`swim`), or null where there's no navmesh or none near it.
        /// </summary>
        public float? FloorAt(Vector3 position, bool swim)
        {
            Zone zone = ZoneAt(position);

            if (!HasNavmesh(zone))
                return null;

            return _pathfinding.GetClosestPointInBounds(zone, position, new(-3, -3, -FLOOR_RANGE), new(3, 3, FLOOR_RANGE), swim ? _waterFilters : _landFilters)?.Z;
        }

        private Route Find(Vector3 start, Vector3 target, EDtPolyFlags[] filters)
        {
            _filters = filters;
            RouteBuilder corners = new(start);

            if (WalkableNear(ZoneAt(target), target, WALKABLE_RANGE, WALKABLE_RANGE) is not Vector3 end)
                return corners.ToRoute(false);

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
                    corners.Continue(legEnd);
                else if (!TryLeg(zone, position, legEnd, corners))
                {
                    reachesTarget = false;
                    break;
                }

                if (lastZone)
                    break;

                corners.Continue(entry);
                position = entry;
            }

            return corners.ToRoute(reachesTarget);
        }

        // Adds the route through `zone` from `position` to `legEnd`, both in it, stepping through a door where the navmesh leaves a gap at it (see `TryDoorStep`).
        // Returns whether it got to `legEnd`. Where it gets no further, the route ends as close as it could.
        private bool TryLeg(Zone zone, Vector3 position, Vector3 legEnd, RouteBuilder corners)
        {
            if (WalkableNear(zone, position, WALKABLE_RANGE, WALKABLE_RANGE) is not Vector3 from)
                return false;

            corners.Continue(from);
            int start = corners.Count;

            if (TryPath(zone, from, legEnd, corners))
                return true;

            RouteBuilder throughDoor = new(from);

            if (!TryDoorStep(zone, from, legEnd, throughDoor))
                return false;

            corners.RemoveFrom(start);
            corners.Append(throughDoor);
            return true;
        }

        // Where the navmesh doesn't join a door to the ground on one side of it, a route can't get through: into such ground at the door nearest `legEnd`, or out of it at the door nearest `from`.
        // So the route goes from `from` towards that door, and from `legEnd` back towards it. Where the two meet within `DOOR_STEP` of each other and of the door, it steps across in a straight line, as a Human's client walks through the doorway.
        // A door is never stepped through anywhere else.
        private bool TryDoorStep(Zone zone, Vector3 from, Vector3 legEnd, RouteBuilder corners)
        {
            int start = corners.Count;

            foreach (Vector3 near in new[] { legEnd, from })
            {
                if (_pathfinding.GetClosestPoint(zone, near, DOOR_RANGE, DOOR_RANGE, DOOR_HEIGHT_RANGE, _doorFilters) is Vector3 door && TryDoorStep(zone, from, legEnd, door, corners))
                    return true;

                corners.RemoveFrom(start);
            }

            return false;
        }

        private bool TryDoorStep(Zone zone, Vector3 from, Vector3 legEnd, Vector3 door, RouteBuilder corners)
        {
            RouteBuilder back = new(legEnd);
            TryPath(zone, legEnd, door, back);
            Vector3 farSide = back.Last;
            TryPath(zone, from, door, corners);
            Vector3 nearSide = corners.Last;

            if (Vector3.Distance(nearSide, farSide) > DOOR_STEP || Vector3.Distance(nearSide, door) > DOOR_STEP || Vector3.Distance(farSide, door) > DOOR_STEP)
                return false;

            corners.Continue(farSide);
            return TryPath(zone, farSide, legEnd, corners);
        }

        // Adds the navmesh's route from `from` to `to`, in one zone. One query gives at most `MAX_POLY` corners, so a long route is worked out on from where each query got to.
        // Returns whether it got to `to`. Where a query gets no further, the route ends as close as it could.
        private bool TryPath(Zone zone, Vector3 from, Vector3 to, RouteBuilder corners)
        {
            for (int queries = 0; queries < MAX_QUERIES_PER_LEG; queries++)
            {
                PathfindingResult result = _pathfinding.GetPathStraight(zone, from, to, _filters, _nodes);

                if (result.Status is not (PathfindingStatus.PathFound or PathfindingStatus.PartialPathFound) || result.NodeCount == 0)
                    return false;

                for (int i = 0; i < result.NodeCount; i++)
                    corners.Follow(_nodes[i]);

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

        // Whether `route` beats `other` to `target`.
        private static bool IsBetter(Route route, Route other, Vector3 target)
        {
            if (route.ReachesTarget != other.ReachesTarget)
                return route.ReachesTarget;

            if (route.ReachesTarget)
                return route.Effort < other.Effort;

            return Route.DistanceInXY(route.Corners[^1].Position, target) < Route.DistanceInXY(other.Corners[^1].Position, target);
        }

        // The route with `within` taken off its end, measured in X and Y as the run is. A route no longer than that is only its start.
        private static Route Trim(Route route, float within)
        {
            float left = within;

            for (int i = route.Corners.Length - 1; i > 0; i--)
            {
                Route.Corner from = route.Corners[i - 1];
                Route.Corner to = route.Corners[i];
                float leg = Route.DistanceInXY(from.Position, to.Position);

                if (leg > left)
                {
                    Route.Corner[] trimmed = route.Corners[..(i + 1)];
                    // The new end is on the leg into it.
                    trimmed[i] = new(Vector3.Lerp(to.Position, from.Position, left / leg), from.Swim);
                    return new(trimmed, true);
                }

                left -= leg;
            }

            return new([route.Corners[0]], true);
        }

        /// <summary>
        /// A route as it's worked out, corner by corner, each knowing whether the route swims on from it.
        /// A corner where the route already is isn't added again, but what it says about the route on from there counts.
        /// </summary>
        private sealed class RouteBuilder
        {
            private readonly List<Route.Corner> _corners;

            public RouteBuilder(Vector3 first)
            {
                _corners = [new(first, false)];
            }

            public int Count => _corners.Count;

            public Vector3 Last => _corners[^1].Position;

            // A corner the route got to some other way than along the navmesh, such as the nearest walkable point, a zone border or a door step: it goes on as it came in.
            public void Continue(Vector3 position)
            {
                Add(new(position, _corners[^1].Swim));
            }

            // A corner of the navmesh's route. Its flags are the polygon's the route goes on into, and the end of a query's route goes into none, so it goes on as it came in.
            public void Follow(WrappedPathfindingNode node)
            {
                Add(new(node.Position, node.Flags == 0 ? _corners[^1].Swim : node.Flags.HasFlag(EDtPolyFlags.Swim)));
            }

            // All of `other`'s corners after its first.
            public void Append(RouteBuilder other)
            {
                for (int i = 1; i < other.Count; i++)
                    Add(other._corners[i]);
            }

            public void RemoveFrom(int index)
            {
                _corners.RemoveRange(index, _corners.Count - index);
            }

            public Route ToRoute(bool reachesTarget)
            {
                return new(_corners.ToArray(), reachesTarget);
            }

            private void Add(Route.Corner corner)
            {
                if (Last != corner.Position)
                    _corners.Add(corner);
                else
                    _corners[^1] = corner;
            }
        }
    }

    /// <summary>
    /// The corners a run goes through, the first where the Character is. `ReachesTarget` is false when it can't get to the target and ends as close as it could.
    /// </summary>
    public sealed record Route(Route.Corner[] Corners, bool ReachesTarget)
    {
        // How long the route takes, as far as running would take as long (`RouteRun.Effort`).
        public float Effort
        {
            get
            {
                float effort = 0;

                for (int i = 1; i < Corners.Length; i++)
                    effort += RouteRun.Effort(DistanceInXY(Corners[i - 1].Position, Corners[i].Position), Corners[i - 1].Swim);

                return effort;
            }
        }

        // Only X and Y count, as in the server's speed check.
        public static float DistanceInXY(Vector3 from, Vector3 to)
        {
            return new Vector2(to.X - from.X, to.Y - from.Y).Length();
        }

        /// <summary>
        /// A corner of a route, global. `Swim` when the route swims from it on, across water on the surface; on the last corner, when the route ends in the water.
        /// </summary>
        public readonly record struct Corner(Vector3 Position, bool Swim);
    }
}
