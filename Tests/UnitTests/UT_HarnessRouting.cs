using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.GS.ServerRules;
using NUnit.Framework;
using OpenDAoC.Pathing;

namespace DOL.GS.Tests
{
    /// <summary>
    /// On a zone's navmesh the Agent's client runs its Character along a route, on the ground, around what's in the way (ADR 0007).
    /// The navmesh is a fake of the server's pathfinding interface, giving set routes, nearest walkable points and floors.
    /// </summary>
    [TestFixture]
    public class UT_HarnessRouting
    {
        private static readonly Vector3 Start = new(30000, 30000, 2000);
        private static readonly Vector3 Corner = new(31000, 31000, 2100);
        private static readonly Vector3 Target = new(32000, 30000, 2200);
        private static readonly MaxSpeedPercept FullSpeed = new(100, false, 0);

        private Action _restoreServer;
        private FakeClient _client;
        private FakeNavmesh _navmesh;
        private HarnessMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _restoreServer = WorldDoubles.InstallServer(DispatchProxy.Create<IServerRules, UT_PerceptProjection.TestRules>());
            // Two zones side by side along X.
            Region region = WorldDoubles.Region(100, (101, 0, 0, 65536, 65536), (102, 65536, 0, 65536, 65536));
            TestPlayer greeen = WorldDoubles.Player(WorldDoubles.Client(0x0101), "Greeen");
            greeen.Region = region;
            (greeen.X, greeen.Y, greeen.Z) = ((int) Start.X, (int) Start.Y, (int) Start.Z);
            _client = new(greeen);
            _navmesh = new();
            _movement = new(_client, _navmesh);
            _movement.OnMaxSpeed(FullSpeed, 0);
        }

        [TearDown]
        public void TearDown()
        {
            _restoreServer();
        }

        [Test]
        public void MoveTo_ShouldRunAlongTheRoute_CornerToCorner()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, null, 0);
            List<Vector3> positions = RunToTheEnd();

            Assert.That(positions, Has.All.Matches<Vector3>(static position => DistanceToRoute(position, [Start, Corner, Target]) < 0.01f));
            Assert.That(positions.Any(static position => position.X < Corner.X - 100), Is.True);
            Assert.That(positions.Any(static position => position.X > Corner.X + 100), Is.True);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        [Test]
        public void MoveTo_ShouldHeadAlongEachLegInTurn()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            // Heading 0 points towards +Y and grows clockwise: (+1, +1) is 3584, and (+1, −1) is 2560.
            int[] headings = _client.Perceived.OfType<MovedPercept>().Select(static moved => moved.Heading).Distinct().ToArray();
            Assert.That(headings, Is.EqualTo(new[] { 3584, 2560 }).Within(1));
        }

        [Test]
        public void MoveTo_ShouldTellTheMindTheRoute_BeforeItsFirstMove()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, null, 0);
            _movement.Tick(0);

            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, Corner, Target }));
            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.True);
            Assert.That(_client.Perceived[1], Is.TypeOf<MovedPercept>());
        }

        [Test]
        public void MoveTo_ShouldKeepTheCharacterOnTheFloor()
        {
            _navmesh.Route = [Start, Corner, Target];
            _navmesh.Floor = static position => position.Z + 37;

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            foreach (MovedPercept moved in _client.Perceived.OfType<MovedPercept>())
                Assert.That(moved.Z, Is.EqualTo(InterpolatedZ(new(moved.X, moved.Y, 0), [Start, Corner, Target]) + 37).Within(0.01));
        }

        [Test]
        public void MoveTo_ShouldInterpolateZBetweenCorners_WhereThereIsNoFloor()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            foreach (MovedPercept moved in _client.Perceived.OfType<MovedPercept>())
                Assert.That(moved.Z, Is.EqualTo(InterpolatedZ(new(moved.X, moved.Y, 0), [Start, Corner, Target])).Within(0.01));
        }

        // A target's Z need only be roughly right: the route goes to the nearest walkable point, however far above or below it.
        [Test]
        public void MoveTo_ShouldRunToTheNearestWalkablePoint()
        {
            Vector3 ground = Target + new Vector3(10, 0, -300);
            _navmesh.Walkable = position => position == Target ? ground : position;
            _navmesh.Route = [Start, ground];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(_navmesh.OverLand.Single().End, Is.EqualTo(ground));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(ground.X, ground.Y, ground.Z)));
        }

        [Test]
        public void MoveTo_ShouldEndWithinTheDistance_AlongTheRoute()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, 100, 0);
            RunToTheEnd();

            // 100 units back along the last leg, in X and Y, as the speed check measures.
            Vector2 shortOfTarget = new Vector2(Target.X, Target.Y) + Vector2.Normalize(new Vector2(Corner.X - Target.X, Corner.Y - Target.Y)) * 100;
            Vector3 end = Corners(_client.Perceived[0]).Last();
            Assert.That(Vector2.Distance(new(end.X, end.Y), shortOfTarget), Is.LessThan(0.01));
            ArrivedPercept arrived = (ArrivedPercept) _client.Perceived.Last();
            Assert.That(Vector2.Distance(new(arrived.X, arrived.Y), shortOfTarget), Is.LessThan(2));
        }

        [Test]
        public void MoveTo_ShouldArriveAtOnce_WithinTheDistanceAlongTheRoute()
        {
            Vector3 near = Start + new Vector3(60, 0, 0);
            _navmesh.Route = [Start, near];

            _movement.MoveTo(near, 100, 0);

            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Start.X, Start.Y, Start.Z)));
            Assert.That(_movement.IsRunning, Is.False);
            Assert.That(_movement.Tick(0), Is.False);
            Assert.That(_client.Queued, Is.Empty);
        }

        [Test]
        public void MoveTo_ShouldEndUnreachable_AtTheEndOfARouteThatStopsShort()
        {
            // Asked again from where it stopped, it gets no further.
            _navmesh.Routes = static asked => [asked.Start, Corner];
            _navmesh.Status = PathfindingStatus.PartialPathFound;

            _movement.MoveTo(Target, 100, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.False);
            Assert.That(Corners(_client.Perceived[0]).Last(), Is.EqualTo(Corner));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(Corner.X, Corner.Y, Corner.Z, StopReason.Unreachable)));
        }

        // A hut whose raised floor the navmesh doesn't join to its door: the floor is an island, a short step from the door's polygon.
        [Test]
        public void MoveTo_ShouldStepAcrossAShortGapAtADoor_IntoGroundTheRouteCantReach()
        {
            Vector3 door = new(31000, 30000, 2100);
            Vector3 inside = new(31040, 30000, 2116);
            Vector3 behind = new(31500, 29500, 2000);
            _navmesh.Door = position => position == Target ? door : null;
            _navmesh.Routes = asked => (asked.Start, asked.End) switch
            {
                (var from, var to) when from == Start && to == Target => [Start, behind],
                (var from, var to) when from == Start && to == door => [Start, door],
                (var from, var to) when from == Target && to == door => [Target, inside],
                (var from, var to) when from == inside && to == Target => [inside, Target],
                _ => [asked.Start]
            };

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.True);
            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, door, inside, Target }));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
            Assert.That(_navmesh.Filters, Has.Some.EqualTo((EDtPolyFlags.Door, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Swim | EDtPolyFlags.Jump | EDtPolyFlags.Disabled)));
        }

        // The other way round: the door belongs to the island, and the ground outside stops short of it.
        [Test]
        public void MoveTo_ShouldStepAcrossAShortGapBeforeADoor()
        {
            Vector3 door = new(31000, 30000, 2100);
            Vector3 outside = new(30950, 30000, 2100);
            _navmesh.Door = position => position == Target ? door : null;
            _navmesh.Routes = asked => (asked.Start, asked.End) switch
            {
                (var from, var to) when from == Start && to == Target => [Start, outside],
                (var from, var to) when from == Start && to == door => [Start, outside],
                (var from, var to) when from == Target && to == door => [Target, door],
                (var from, var to) when from == door && to == Target => [door, Target],
                _ => [asked.Start]
            };

            _movement.MoveTo(Target, null, 0);

            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, outside, door, Target }));
        }

        // Leaving the hut: the gap is at the door nearest where the route starts, not where it goes.
        [Test]
        public void MoveTo_ShouldStepAcrossAShortGapAtADoor_OutOfGroundTheRouteCantLeave()
        {
            Vector3 door = new(30300, 30000, 2100);
            Vector3 inside = new(30260, 30000, 2116);
            Vector3 edge = new(30100, 29900, 2116);
            _navmesh.Door = position => position == Start ? door : null;
            _navmesh.Routes = asked => (asked.Start, asked.End) switch
            {
                (var from, var to) when from == Start && to == Target => [Start, edge],
                (var from, var to) when from == Start && to == door => [Start, inside],
                (var from, var to) when from == Target && to == door => [Target, door],
                (var from, var to) when from == door && to == Target => [door, Target],
                _ => [asked.Start]
            };

            _movement.MoveTo(Target, null, 0);

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.True);
            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, inside, door, Target }));
        }

        // Further apart, the two sides aren't a doorway's step: the route ends as close as it could.
        [Test]
        public void MoveTo_ShouldNotStepAcrossAGapWiderThanADoorway()
        {
            Vector3 door = new(31000, 30000, 2100);
            Vector3 inside = new(31070, 30000, 2116);
            Vector3 behind = new(31500, 29500, 2000);
            _navmesh.Door = position => position == Target ? door : null;
            _navmesh.Routes = asked => (asked.Start, asked.End) switch
            {
                (var from, var to) when from == Start && to == Target => [Start, behind],
                (var from, var to) when from == Start && to == door => [Start, door],
                (var from, var to) when from == Target && to == door => [Target, inside],
                _ => [asked.Start]
            };

            _movement.MoveTo(Target, null, 0);

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.False);
            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, behind }));
        }

        // Standing in deep water, say: the nearest walkable point is the shore.
        [Test]
        public void MoveTo_ShouldEndUnreachable_WhenTheNearestWalkablePointIsFarFromTheTarget()
        {
            Vector3 shore = Target + new Vector3(0, -500, 0);
            _navmesh.Walkable = position => position == Target ? shore : position;
            _navmesh.Route = [Start, shore];

            _movement.MoveTo(Target, 100, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.False);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(shore.X, shore.Y, shore.Z, StopReason.Unreachable)));
        }

        [TestCase(PathfindingStatus.NoPathFound)]
        [TestCase(PathfindingStatus.BufferTooSmall)]
        public void MoveTo_ShouldEndUnreachableAtOnce_WithoutARoute(PathfindingStatus status)
        {
            _navmesh.Route = [];
            _navmesh.Status = status;

            _movement.MoveTo(Target, null, 0);

            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start }));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(Start.X, Start.Y, Start.Z, StopReason.Unreachable)));
            Assert.That(_movement.IsRunning, Is.False);
            Assert.That(_client.Queued, Is.Empty);
        }

        // One query gives at most 256 corners, and the server's pathfinding reports a route cut short by that as found, or as partial when its search ran out first.
        [TestCase(PathfindingStatus.PathFound)]
        [TestCase(PathfindingStatus.PartialPathFound)]
        public void MoveTo_ShouldWorkTheRouteOutOnFromWhereAQueryGotTo(PathfindingStatus status)
        {
            _navmesh.Routes = static asked => asked.Start == Start ? [Start, Corner] : [asked.Start, Target];
            _navmesh.Status = status;

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(_navmesh.OverLand.Select(static asked => asked.Start), Is.EqualTo(new[] { Start, Corner }));
            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, Corner, Target }));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        [Test]
        public void MoveTo_ShouldEndUnreachableAtOnce_WithNoWalkablePointNearTheCharacter()
        {
            _navmesh.Walkable = static position => position == Start ? null : position;

            _movement.MoveTo(Target, null, 0);

            Assert.That(_navmesh.Asked, Is.Empty);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(Start.X, Start.Y, Start.Z, StopReason.Unreachable)));
        }

        [Test]
        public void MoveTo_ShouldNotOvershootTheEndOfTheRoute_AfterAStall()
        {
            _navmesh.Route = [Start, Corner, Target];
            _movement.MoveTo(Target, null, 0);
            _movement.Tick(0);

            // The game loop stalls for an hour.
            _movement.Tick(60 * 60 * 1000);

            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        // The Character's zone has no navmesh but the target's has: straight to the border, then on along a route.
        [Test]
        public void MoveTo_ShouldRunStraightToTheBorder_FromAZoneWithoutANavmesh()
        {
            Vector3 over = new(70000, 30000, 2200);
            _navmesh.Zones = [102];

            _movement.MoveTo(over, null, 0);
            RunToTheEnd();

            Assert.That(_navmesh.OverLand.Single().Zone, Is.EqualTo(102));
            Assert.That(_navmesh.OverLand.Single().Start.X, Is.GreaterThanOrEqualTo(65536).And.LessThan(65600));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(over.X, over.Y, over.Z)));
        }

        // The server must hear the Character stand, or it goes on thinking it runs.
        [Test]
        public void MoveTo_ShouldStopTheCharacterWithAPositionUpdate_WhenItReplacesARunWithoutARoute()
        {
            _navmesh.Route = [Start, Corner, Target];
            _movement.MoveTo(Target, null, 0);

            for (long now = 0; now <= 2000; now += 200)
                _movement.Tick(now);

            _client.Queued.Clear();
            _navmesh.Status = PathfindingStatus.NoPathFound;
            _movement.MoveTo(Start, null, 2000);

            Assert.That(_client.Perceived.TakeLast(3).Select(static percept => percept.GetType()), Is.EqualTo(new[] { typeof(StoppedPercept), typeof(RoutedPercept), typeof(StoppedPercept) }));
            Assert.That(((StoppedPercept) _client.Perceived.Last()).Reason, Is.EqualTo(StopReason.Unreachable));
            Assert.That(BinaryPrimitives.ReadSingleLittleEndian(_client.Queued.Single().Data[12..]), Is.Zero);
            Assert.That(_movement.IsRunning, Is.False);
        }

        [Test]
        public void MoveTo_ShouldEndUnreachableAtOnce_WithNoWalkablePointNearTheTarget()
        {
            _navmesh.Walkable = position => position == Target ? null : position;

            _movement.MoveTo(Target, null, 0);

            Assert.That(_navmesh.Asked, Is.Empty);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(Start.X, Start.Y, Start.Z, StopReason.Unreachable)));
        }

        // A door that opens by clicking is marked a door whether it's open or closed, so a route through it depends on no door's state (ADR 0006).
        // The server marks a closed door that doesn't open by clicking as blocking, and the route avoids it, open or not, as far as the flags allow.
        // One route goes over land only, and one may swim too.
        [Test]
        public void MoveTo_ShouldRouteOverWalkableGroundDoorsAndWater_NeverThroughBlockingDoorsOrJumps()
        {
            _navmesh.Route = [Start, Target];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            // Every query: the routes, the nearest walkable points and the floors.
            Assert.That(_navmesh.Filters, Has.Count.GreaterThan(6));
            Assert.That(_navmesh.Filters, Has.All.EqualTo((EDtPolyFlags.Walk | EDtPolyFlags.Door, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Swim | EDtPolyFlags.Jump | EDtPolyFlags.Disabled))
                .Or.EqualTo((EDtPolyFlags.Walk | EDtPolyFlags.Door | EDtPolyFlags.Swim, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Jump | EDtPolyFlags.Disabled)));
            Assert.That(_navmesh.OverLand.Count(), Is.EqualTo(1));
            Assert.That(_navmesh.Asked.Count(static asked => asked.Swim), Is.EqualTo(1));
        }

        // Navmeshes are per zone: the route goes to the border, then on in the next zone, as the server's NPC pathing does.
        [Test]
        public void MoveTo_ShouldRouteAcrossAZoneBorder()
        {
            Vector3 over = new(70000, 30000, 2200);
            _navmesh.Routes = asked => [asked.Start, asked.End];

            _movement.MoveTo(over, null, 0);
            RunToTheEnd();

            Asked[] overLand = _navmesh.OverLand.ToArray();
            Assert.That(overLand.Select(static asked => asked.Zone), Is.EqualTo(new ushort[] { 101, 102 }));
            Assert.That(overLand[0].End.X, Is.LessThan(65536).And.GreaterThan(65500));
            Assert.That(overLand[1].Start.X, Is.GreaterThanOrEqualTo(65536).And.LessThan(65600));
            Assert.That(overLand[1].End, Is.EqualTo(over));
            Assert.That(_client.Perceived.OfType<RoutedPercept>().Count(), Is.EqualTo(1));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(over.X, over.Y, over.Z)));
        }

        // As in milestone 3: a straight line with Z interpolated between start and target.
        [Test]
        public void MoveTo_ShouldRunInAStraightLine_InAZoneWithoutANavmesh()
        {
            _navmesh.Zones = [];

            _movement.MoveTo(Target, null, 0);
            List<Vector3> positions = RunToTheEnd();

            Assert.That(_navmesh.Asked, Is.Empty);
            Assert.That(Corners(_client.Perceived[0]), Is.EqualTo(new[] { Start, Target }));
            Assert.That(positions, Has.All.Matches<Vector3>(static position => DistanceToRoute(position, [Start, Target]) < 0.01f));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        [Test]
        public void MoveTo_ShouldEndWithinTheDistance_InAZoneWithoutANavmesh()
        {
            _navmesh.Zones = [];

            _movement.MoveTo(Target, 100, 0);
            RunToTheEnd();

            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X - 100, Target.Y, Target.Z - 10)));
        }

        // The run steps from the time since it started: a corner never makes it cover more than the maximum speed allows.
        [Test]
        public void MoveTo_ShouldNeverRunFasterThanTheMaximumSpeed_RoundACorner()
        {
            _navmesh.Route = [Start, Corner, Target];

            _movement.MoveTo(Target, null, 0);
            List<Vector3> positions = RunToTheEnd();

            // Position updates go out on the first 33 ms tick after each 200 ms, so at most 233 ms apart.
            for (int i = 1; i < positions.Count; i++)
                Assert.That(Vector2.Distance(new(positions[i].X, positions[i].Y), new(positions[i - 1].X, positions[i - 1].Y)), Is.LessThanOrEqualTo(GamePlayer.PLAYER_BASE_SPEED * 0.233f + 0.01f));
        }

        [Test]
        public void Stop_ShouldEndARouteWhereTheCharacterIs()
        {
            _navmesh.Route = [Start, Corner, Target];
            _movement.MoveTo(Target, null, 0);

            for (long now = 0; now <= 8000; now += 200)
                _movement.Tick(now);

            _movement.Stop(8000);

            StoppedPercept stopped = (StoppedPercept) _client.Perceived.Last();
            Assert.That(stopped.Reason, Is.EqualTo(StopReason.Stop));
            Assert.That(DistanceToRoute(new(stopped.X, stopped.Y, stopped.Z), [Start, Corner, Target]), Is.LessThan(2));
            Assert.That(stopped.X, Is.GreaterThan(Corner.X));
        }

        // Across a lake: in at `Shore`, out at `FarShore`, 1000 units of water between 500 units of ground each side.
        private static readonly Vector3 Shore = new(30500, 30000, 2050);
        private static readonly Vector3 FarShore = new(31500, 30000, 2150);
        private static readonly (Vector3, EDtPolyFlags)[] AcrossTheLake = [(Start, EDtPolyFlags.Walk), (Shore, EDtPolyFlags.Swim), (FarShore, EDtPolyFlags.Walk), (Target, 0)];

        // Swimming the lake takes 1000 + 1000 × 191/85 ≈ 3247 units' worth of running. Round it via `FarRound` is about 4472, via `Corner` about 2828.
        private static readonly Vector3 FarRound = new(31000, 32000, 2100);

        // A Mob standing in the lake. Over land the route stops at the shore.
        private static readonly Vector3 InTheLake = new(31000, 30000, 2000);

        private void IntoTheLake()
        {
            _navmesh.Routes = static asked => [asked.Start, Shore];
            _navmesh.Status = PathfindingStatus.PartialPathFound;
            _navmesh.SwimRoutes = static _ => [(Start, EDtPolyFlags.Walk), (Shore, EDtPolyFlags.Swim), (InTheLake, 0)];
        }

        private void AcrossOrFarRound()
        {
            _navmesh.Route = [Start, FarRound, Target];
            _navmesh.SwimRoutes = static _ => AcrossTheLake;
        }

        [Test]
        public void MoveTo_ShouldSwimAcross_WhereThatsFasterThanGoingRound()
        {
            AcrossOrFarRound();

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            RoutedPercept routed = (RoutedPercept) _client.Perceived[0];
            Assert.That(Corners(routed), Is.EqualTo(new[] { Start, Shore, FarShore, Target }));
            Assert.That(routed.Corners.Select(static corner => corner.Swim), Is.EqualTo(new[] { false, true, false, false }));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        [Test]
        public void MoveTo_ShouldGoRound_WhereThatsFasterThanSwimming()
        {
            _navmesh.Route = [Start, Corner, Target];
            _navmesh.SwimRoutes = static _ => AcrossTheLake;

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            RoutedPercept routed = (RoutedPercept) _client.Perceived[0];
            Assert.That(Corners(routed), Is.EqualTo(new[] { Start, Corner, Target }));
            Assert.That(routed.Corners.Select(static corner => corner.Swim), Has.All.False);
            Assert.That(_client.Queued.Select(StateFlags), Has.All.EqualTo((PlayerPositionUpdateHandler.StateFlags) 0));
        }

        // The same route both ways, as where there's no water.
        [Test]
        public void MoveTo_ShouldGoOverLand_WhenSwimmingIsNoFaster()
        {
            _navmesh.Route = [Start, Corner, Target];
            _navmesh.SwimRoutes = static _ => [(Start, EDtPolyFlags.Walk), (Corner, EDtPolyFlags.Walk), (Target, 0)];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(_client.Queued.Select(StateFlags), Has.All.EqualTo((PlayerPositionUpdateHandler.StateFlags) 0));
        }

        [Test]
        public void MoveTo_ShouldSwim_WhereOnlySwimmingReachesTheTarget()
        {
            _navmesh.Routes = static asked => [asked.Start, Shore];
            _navmesh.Status = PathfindingStatus.PartialPathFound;
            _navmesh.SwimRoutes = static _ => AcrossTheLake;

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).ReachesTarget, Is.True);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(Target.X, Target.Y, Target.Z)));
        }

        // The captured client swims at 85 where it runs at 191, 40 below the water's surface, and says so in its state flags, never diving (`Tests/Fixtures/Agents/v1127/README.md`, Swimming).
        // The navmesh's water is 16 below the surface. Going in, the speed drops to the swim speed at once.
        [Test]
        public void MoveTo_ShouldSwimAcrossAtSwimSpeed_BelowTheNavmeshsWater_SayingItSwims()
        {
            AcrossOrFarRound();
            _navmesh.Floor = static _ => 2000;
            _client.ActionFlags = PlayerPositionUpdateHandler.ActionFlags.TORCH;

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            ClientPacket[] swimming = _client.Queued.Where(static packet => StateFlags(packet) is PlayerPositionUpdateHandler.StateFlags.SWIMMING).ToArray();
            Assert.That(swimming.Select(static packet => Position(packet).X), Has.All.InRange(Shore.X, FarShore.X));
            Assert.That(swimming.Select(static packet => Position(packet).X).Min(), Is.LessThan(Shore.X + 0.233f * 85));
            Assert.That(swimming.Select(Speed), Has.All.EqualTo(85).Within(0.01));
            Assert.That(swimming.Select(static packet => Position(packet).Z), Has.All.EqualTo(1976));
            Assert.That(_client.Queued.Select(static packet => (PlayerPositionUpdateHandler.ActionFlags) packet.Data[32]), Has.All.EqualTo(PlayerPositionUpdateHandler.ActionFlags.TORCH));

            ClientPacket[] running = _client.Queued.Where(static packet => Position(packet).X is > 30100 and < 30400 or > 31600 and < 31900).ToArray();
            Assert.That(running.Select(StateFlags), Has.All.EqualTo((PlayerPositionUpdateHandler.StateFlags) 0));
            Assert.That(running.Select(static packet => Position(packet).Z), Has.All.EqualTo(2000));
            Assert.That(running.Select(Speed), Has.Some.EqualTo(GamePlayer.PLAYER_BASE_SPEED).Within(0.01));
        }

        // A Swim leg's floor is the navmesh's water, never the ground under it.
        [Test]
        public void MoveTo_ShouldTakeTheFloorOfASwimLeg_FromTheWater()
        {
            AcrossOrFarRound();

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(_navmesh.Filters, Has.Some.EqualTo((EDtPolyFlags.Swim, EDtPolyFlags.BlockingDoor | EDtPolyFlags.Jump | EDtPolyFlags.Disabled)));
        }

        // Where there's no floor, the water lies flat to the far shore, however high the bank there.
        [Test]
        public void MoveTo_ShouldKeepToTheWater_WhereThereIsNoFloor()
        {
            AcrossOrFarRound();

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            ClientPacket[] swimming = _client.Queued.Where(static packet => StateFlags(packet) is PlayerPositionUpdateHandler.StateFlags.SWIMMING).ToArray();
            Assert.That(swimming.Select(static packet => Position(packet).Z), Has.All.EqualTo(Shore.Z - RouteRun.SWIM_DEPTH));
        }

        // Faster or slower, swimming keeps to the same fraction of the running speed. The captured client swam at 124 with a speed buff running at 280.
        [Test]
        public void MoveTo_ShouldSwimFaster_WithASpeedBuff()
        {
            AcrossOrFarRound();
            // 146% of 191 is 278.86, and swimming 124.1.
            _movement.OnMaxSpeed(new(146, false, 0), 0);

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(_client.Queued.Where(static packet => StateFlags(packet) is PlayerPositionUpdateHandler.StateFlags.SWIMMING).Select(Speed), Has.Some.EqualTo(124.1f).Within(0.1));
        }

        [Test]
        public void OnMaxSpeed_ShouldChangeTheSwimSpeed_MidLeg()
        {
            AcrossOrFarRound();
            _movement.MoveTo(Target, null, 0);
            long now = TickUntilSwimming();

            _movement.OnMaxSpeed(new(146, false, 0), now);
            now += 2000;
            _movement.Tick(now);

            Assert.That(StateFlags(_client.Queued.Last()), Is.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
            Assert.That(Speed(_client.Queued.Last()), Is.EqualTo(124.1f).Within(0.1));
        }

        // A run stopped in the water leaves the Character swimming there, as the captured client stands in the water.
        [Test]
        public void Stop_ShouldLeaveTheCharacterSwimming_InTheWater()
        {
            AcrossOrFarRound();
            _movement.MoveTo(Target, null, 0);
            long now = TickUntilSwimming();

            _movement.Stop(now + 1000);

            ClientPacket stop = _client.Queued.Last();
            Assert.That(Speed(stop), Is.Zero);
            Assert.That(StateFlags(stop), Is.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
            Assert.That(_movement.StateFlags, Is.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
        }

        [Test]
        public void MoveTo_ShouldNotOvershootTheEndOfASwimRoute_AfterAStall()
        {
            IntoTheLake();
            _movement.MoveTo(InTheLake, null, 0);
            _movement.Tick(0);

            // The game loop stalls for an hour.
            _movement.Tick(60 * 60 * 1000);

            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(InTheLake.X, InTheLake.Y, InTheLake.Z - RouteRun.SWIM_DEPTH)));
        }

        // A Mob standing in a lake: the run ends in the water, and the Character stands there swimming, which the client's idle position updates say too.
        [Test]
        public void MoveTo_ShouldEndInTheWater_SwimmingThere()
        {
            IntoTheLake();

            _movement.MoveTo(InTheLake, null, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).Corners.Select(static corner => corner.Swim), Is.EqualTo(new[] { false, true, true }));
            Assert.That(StateFlags(_client.Queued.Last()), Is.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(InTheLake.X, InTheLake.Y, InTheLake.Z - RouteRun.SWIM_DEPTH)));
            Assert.That(_movement.StateFlags, Is.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
        }

        [Test]
        public void MoveTo_ShouldEndWithinTheDistance_InTheWater()
        {
            IntoTheLake();

            _movement.MoveTo(InTheLake, 100, 0);
            RunToTheEnd();

            RoutedPercept routed = (RoutedPercept) _client.Perceived[0];
            Assert.That(routed.Corners.Last().X, Is.EqualTo(InTheLake.X - 100).Within(0.01));
            Assert.That(routed.Corners.Last().Swim, Is.True);
            Assert.That(_movement.IsSwimming, Is.True);
        }

        // Starting in the water, the step to the nearest point of it swims too.
        [Test]
        public void MoveTo_ShouldSwimFromTheStart_InTheWater()
        {
            Vector3 water = Start + new Vector3(0, 0, RouteRun.SWIM_DEPTH);
            _navmesh.Walkable = position => position == Start ? water : position;
            _navmesh.Routes = static asked => [asked.Start];
            _navmesh.Status = PathfindingStatus.PartialPathFound;
            _navmesh.SwimRoutes = _ => [(water, EDtPolyFlags.Swim), (Target, 0)];

            _movement.MoveTo(Target, null, 0);
            RunToTheEnd();

            Assert.That(((RoutedPercept) _client.Perceived[0]).Corners.Select(static corner => corner.Swim), Is.EqualTo(new[] { true, true, true }));
            Assert.That(_client.Queued.Select(StateFlags), Has.All.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
        }

        // Lough Derg's water goes on across the border into Silvermine Mts.
        [Test]
        public void MoveTo_ShouldSwimAcrossAZoneBorder()
        {
            Vector3 over = new(70000, 30000, 2200);
            _navmesh.Routes = static asked => [asked.Start];
            _navmesh.Status = PathfindingStatus.PartialPathFound;
            _navmesh.SwimRoutes = static asked => [(asked.Start, EDtPolyFlags.Swim), (asked.End, 0)];

            _movement.MoveTo(over, null, 0);
            RunToTheEnd();

            RoutedPercept routed = (RoutedPercept) _client.Perceived[0];
            Assert.That(routed.ReachesTarget, Is.True);
            Assert.That(routed.Corners.Select(static corner => corner.Swim), Has.All.True);
            Assert.That(_client.Queued.Select(StateFlags), Has.All.EqualTo(PlayerPositionUpdateHandler.StateFlags.SWIMMING));
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new ArrivedPercept(over.X, over.Y, over.Z - RouteRun.SWIM_DEPTH)));
        }

        // The server set the Character's position: out of the water, as far as the client knows.
        [Test]
        public void OnJump_ShouldEndSwimming()
        {
            IntoTheLake();
            _movement.MoveTo(InTheLake, null, 0);
            RunToTheEnd();

            _movement.OnJump(new(40000, 40000, 3000, 0, 0, 0));

            Assert.That(_movement.StateFlags, Is.EqualTo((PlayerPositionUpdateHandler.StateFlags) 0));
        }

        // Ticks the client every 33 ms until its last position update says it swims, and gives the time then.
        private long TickUntilSwimming()
        {
            long now = 0;

            for (; now < 600000 && (_client.Queued.Count == 0 || StateFlags(_client.Queued.Last()) is not PlayerPositionUpdateHandler.StateFlags.SWIMMING); now += 33)
                _movement.Tick(now);

            Assert.That(_movement.IsRunning, Is.True);
            return now;
        }
        // Ticks the client every 33 ms until the run ends, and gives the position of each position update it sent.
        private List<Vector3> RunToTheEnd()
        {
            for (long now = 0; now < 600000 && (_movement.IsRunning || now == 0); now += 33)
                _movement.Tick(now);

            Assert.That(_movement.IsRunning, Is.False);
            return _client.Queued.Select(static packet => Position(packet)).ToList();
        }

        private static Vector3[] Corners(Percept percept)
        {
            return ((RoutedPercept) percept).Corners.Select(static corner => new Vector3(corner.X, corner.Y, corner.Z)).ToArray();
        }

        private static Vector3 Position(ClientPacket packet)
        {
            Assert.That(packet.Code, Is.EqualTo((byte) eClientPackets.PositionUpdate));
            ReadOnlySpan<byte> data = packet.Data;
            return new(BinaryPrimitives.ReadSingleLittleEndian(data), BinaryPrimitives.ReadSingleLittleEndian(data[4..]), BinaryPrimitives.ReadSingleLittleEndian(data[8..]));
        }

        private static float Speed(ClientPacket packet)
        {
            return BinaryPrimitives.ReadSingleLittleEndian(packet.Data.AsSpan(12));
        }

        private static PlayerPositionUpdateHandler.StateFlags StateFlags(ClientPacket packet)
        {
            return (PlayerPositionUpdateHandler.StateFlags) packet.Data[26];
        }

        // In X and Y, from the nearest leg of the route.
        private static float DistanceToRoute(Vector3 position, Vector3[] route)
        {
            Vector2 point = new(position.X, position.Y);
            float nearest = float.MaxValue;

            for (int i = 1; i < route.Length; i++)
            {
                Vector2 from = new(route[i - 1].X, route[i - 1].Y);
                Vector2 leg = new Vector2(route[i].X, route[i].Y) - from;
                float along = Math.Clamp(Vector2.Dot(point - from, leg) / leg.LengthSquared(), 0, 1);
                nearest = Math.Min(nearest, Vector2.Distance(point, from + leg * along));
            }

            return nearest;
        }

        // Z on the nearest leg of the route, between its two corners.
        private static float InterpolatedZ(Vector3 position, Vector3[] route)
        {
            Vector2 point = new(position.X, position.Y);
            (float distance, float z) nearest = (float.MaxValue, 0);

            for (int i = 1; i < route.Length; i++)
            {
                Vector2 from = new(route[i - 1].X, route[i - 1].Y);
                Vector2 leg = new Vector2(route[i].X, route[i].Y) - from;
                float along = Math.Clamp(Vector2.Dot(point - from, leg) / leg.LengthSquared(), 0, 1);
                float distance = Vector2.Distance(point, from + leg * along);

                if (distance < nearest.distance)
                    nearest = (distance, route[i - 1].Z + (route[i].Z - route[i - 1].Z) * along);
            }

            return nearest.z;
        }

        /// <summary>
        /// The server's pathfinding with set answers. Every zone in `Zones` has a navmesh, every point is walkable where it is, and there's no floor, unless a test says otherwise.
        /// It keeps every route asked for.
        /// </summary>
        private sealed class FakeNavmesh : PathfindingMgrBase
        {
            public HashSet<ushort> Zones { get; set; } = [101, 102];

            // The route's nodes, start and end included, whatever's asked. `Routes` instead works them out from what's asked.
            public Vector3[] Route { set => Routes = _ => value; }
            public Func<Asked, Vector3[]> Routes { get; set; } = static asked => [asked.Start, asked.End];
            // The route's nodes with the flags of the polygon each enters, asked with water allowed. `Routes`, all walkable, unless a test says otherwise.
            public Func<Asked, (Vector3 Position, EDtPolyFlags Flags)[]> SwimRoutes { get; set; }
            public PathfindingStatus Status { get; set; } = PathfindingStatus.PathFound;
            public Func<Vector3, Vector3?> Walkable { get; set; } = static position => position;
            // The door polygon nearest a position. None unless a test says so.
            public Func<Vector3, Vector3?> Door { get; set; } = static _ => null;
            public Func<Vector3, float?> Floor { get; set; } = static _ => null;
            public List<Asked> Asked { get; } = [];
            // Those asked with water not allowed.
            public IEnumerable<Asked> OverLand => Asked.Where(static asked => !asked.Swim);
            // [include, exclude] of every query.
            public List<(EDtPolyFlags, EDtPolyFlags)> Filters { get; } = [];

            public override bool HasNavmesh(Zone zone)
            {
                return zone != null && Zones.Contains(zone.ID);
            }

            public override PathfindingResult GetPathStraight(Zone zone, Vector3 start, Vector3 end, EDtPolyFlags[] filters, Span<WrappedPathfindingNode> nodes)
            {
                Asked asked = new(zone.ID, start, end, filters[0].HasFlag(EDtPolyFlags.Swim));
                Asked.Add(asked);
                Filters.Add((filters[0], filters[1]));

                if (Status is PathfindingStatus.NoPathFound or PathfindingStatus.BufferTooSmall)
                    return new(Status, Status is PathfindingStatus.BufferTooSmall ? nodes.Length + 1 : 0);

                (Vector3 Position, EDtPolyFlags Flags)[] route = asked.Swim && SwimRoutes != null ? SwimRoutes(asked) : OverLandAll(Routes(asked));

                for (int i = 0; i < route.Length; i++)
                    nodes[i] = new(route[i].Position, route[i].Flags);

                return new(Status, route.Length);
            }

            // Every node on walkable ground, but the end, which goes into no polygon, as the server's pathfinding gives it.
            private static (Vector3, EDtPolyFlags)[] OverLandAll(Vector3[] route)
            {
                return route.Select((node, i) => (node, i < route.Length - 1 ? EDtPolyFlags.Walk : 0)).ToArray();
            }

            public override Vector3? GetClosestPoint(Zone zone, Vector3 position, float xRange, float yRange, float zRange, EDtPolyFlags[] filters)
            {
                Filters.Add((filters[0], filters[1]));
                return filters[0] is EDtPolyFlags.Door ? Door(position) : Walkable(position);
            }

            public override Vector3? GetClosestPointInBounds(Zone zone, Vector3 origin, Vector3 minOffset, Vector3 maxOffset, EDtPolyFlags[] filters)
            {
                Filters.Add((filters[0], filters[1]));
                return Floor(origin) is float z ? new(origin.X, origin.Y, z) : null;
            }
        }

        // `Swim` when water was allowed.
        private readonly record struct Asked(ushort Zone, Vector3 Start, Vector3 End, bool Swim);

        private sealed class FakeClient : IMovingClient
        {
            public FakeClient(GamePlayer player)
            {
                Player = player;
            }

            public GamePlayer Player { get; }
            public ushort SessionID => 0x0101;
            public ushort TargetId { get; set; }
            public PlayerPositionUpdateHandler.ActionFlags ActionFlags { get; set; }
            public byte HealthByte => Player.HealthPercent;
            public List<ClientPacket> Queued { get; } = [];
            public List<Percept> Perceived { get; } = [];

            public void Enqueue(ClientPacket packet)
            {
                Queued.Add(packet);
            }

            public void Unqueue(byte code)
            {
                Queued.RemoveAll(packet => packet.Code == code);
            }

            public void Perceive(Percept percept)
            {
                Perceived.Add(percept);
            }
        }
    }
}
