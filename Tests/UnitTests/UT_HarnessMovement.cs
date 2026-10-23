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

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Agent's client runs its Character to a target and ends the run when the server moves the Character or lets it move no more.
    /// </summary>
    [TestFixture]
    public class UT_HarnessMovement
    {
        private static readonly Vector3 Start = new(30000, 30000, 2000);
        private static readonly Vector3 Target = new(33000, 30000, 2000);
        private static readonly MaxSpeedPercept FullSpeed = new(100, false, 0);
        private static readonly MaxSpeedPercept Rooted = new(0, false, 0);

        private Action _restoreServer;
        private Region _region;
        private FakeClient _client;
        private HarnessMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _restoreServer = WorldDoubles.InstallServer(DispatchProxy.Create<IServerRules, UT_PerceptProjection.TestRules>());
            _region = WorldDoubles.Region(100, (101, 0, 0, 65536, 65536));
            TestPlayer greeen = WorldDoubles.Player(WorldDoubles.Client(0x0101), "Greeen");
            greeen.Region = _region;
            (greeen.X, greeen.Y, greeen.Z) = ((int) Start.X, (int) Start.Y, (int) Start.Z);
            _client = new(greeen);
            _movement = new(_client, PathfindingProvider.NullPathfindingMgr);
        }

        [TearDown]
        public void TearDown()
        {
            _restoreServer();
        }

        [Test]
        public void Stop_ShouldEndTheRunWhereTheCharacterIs()
        {
            Running();

            _movement.Stop(500);

            Vector3 there = HarnessMovement.WholeUnits(RunOnItsOwn().At(500).Position);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(there.X, there.Y, there.Z, StopReason.Stop)));
            Assert.That(_client.Queued.Select(Read).Last(), Is.EqualTo((there, 0f)));
            Assert.That(_movement.IsRunning, Is.False);
        }

        // As the captured client's do once it has targeted something.
        [Test]
        public void Tick_ShouldSendPositionUpdatesWithTheClientsTargetAndActionFlags()
        {
            _client.TargetId = 0x1F0B;
            _client.ActionFlags = PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;
            _movement.OnMaxSpeed(FullSpeed, 0);
            _movement.MoveTo(Target, null, 0);

            _movement.Tick(0);

            byte[] data = _client.Queued.Single().Data;
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(22)), Is.EqualTo(0x1F0B));
            Assert.That((PlayerPositionUpdateHandler.ActionFlags) data[32], Is.EqualTo(_client.ActionFlags));
        }

        // As the server sets it for a GM's `/jump` or a pull back by the speed check.
        [Test]
        public void OnJump_ShouldEndTheRunAtTheServersPosition()
        {
            Running();

            ServerJumps(new(20000, 25000, 1500), 1024);

            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(20000, 25000, 1500, StopReason.MovedByServer)));
            Assert.That(_movement.IsRunning, Is.False);
        }

        [Test]
        public void OnJump_ShouldEndTheRunForGood()
        {
            Running();
            ServerJumps(new(20000, 25000, 1500), 1024);
            int perceived = _client.Perceived.Count;

            for (long now = 600; now <= 20000; now += 33)
                Assert.That(_movement.Tick(now), Is.False);

            Assert.That(_client.Queued, Is.Empty);
            Assert.That(_client.Perceived, Has.Count.EqualTo(perceived));
        }

        // The server's speed check never sees the jump as movement: nothing queued before it carries the Character back.
        [Test]
        public void OnJump_ShouldDropThePositionUpdatesQueuedBeforeIt()
        {
            Running();
            _movement.Stop(500);
            ClientPacket ping = ClientPackets1127.Ping(1234);
            _client.Enqueue(ping);

            ServerJumps(new(20000, 25000, 1500), 1024);

            Assert.That(_client.Queued, Is.EqualTo(new[] { ping }));
        }

        [Test]
        public void MoveTo_ShouldRunFromTheServersPosition_AfterAJump()
        {
            Running();
            ServerJumps(new(20000, 25000, 1500), 1024);

            _movement.MoveTo(new(21000, 25000, 1500), null, 1000);
            _movement.Tick(1000);

            Assert.That(Read(_client.Queued.Single()).Position, Is.EqualTo(new Vector3(20000, 25000, 1500)));
        }

        [Test]
        public void OnJump_ShouldDoNothing_WhileTheCharacterStands()
        {
            _movement.OnMaxSpeed(FullSpeed, 0);

            ServerJumps(new(20000, 25000, 1500), 1024);

            Assert.That(_client.Perceived, Is.Empty);
            Assert.That(_client.Queued, Is.Empty);
        }

        // As `/faceloc` sends it. The run's own heading overtakes it at the next position update.
        [Test]
        public void OnJump_ShouldLeaveTheRunAsItWas_WhenTheServerSetOnlyTheHeading()
        {
            Running();

            _movement.OnJump(new JumpPercept(0, 0, 0, 0x0123, 1024, 0));

            Assert.That(_movement.IsRunning, Is.True);
            Assert.That(_movement.Tick(600), Is.True);
            Assert.That(_client.Perceived.Last(), Is.TypeOf<MovedPercept>());
        }

        // For example while the Character is rooted.
        [Test]
        public void OnMaxSpeed_ShouldEndTheRunWhereTheCharacterIs_ForAMaximumSpeedOf0()
        {
            Running();

            bool sent = _movement.OnMaxSpeed(Rooted, 500);

            Vector3 there = HarnessMovement.WholeUnits(RunOnItsOwn().At(500).Position);
            Assert.That(sent, Is.True);
            Assert.That(_client.Perceived.Last(), Is.EqualTo(new StoppedPercept(there.X, there.Y, there.Z, StopReason.UnableToMove)));
            Assert.That(_client.Queued.Select(Read).Last(), Is.EqualTo((there, 0f)));
            Assert.That(_movement.IsRunning, Is.False);
        }

        [Test]
        public void OnMaxSpeed_ShouldKeepTheRunGoing_ForALowerMaximumSpeed()
        {
            Running();

            bool sent = _movement.OnMaxSpeed(new(50, false, 0), 500);

            Assert.That(sent, Is.False);
            Assert.That(_movement.IsRunning, Is.True);
            Assert.That(_client.Perceived.OfType<StoppedPercept>(), Is.Empty);
        }

        // Before the server has said, the client knows of no speed it may run at.
        [TestCase(true)]
        [TestCase(false)]
        public void MoveTo_ShouldNotStartARun_WithAMaximumSpeedOf0(bool rooted)
        {
            if (rooted)
                _movement.OnMaxSpeed(Rooted, 0);

            _movement.MoveTo(Target, null, 100);

            Assert.That(_client.Perceived, Is.EqualTo(new[] { new StoppedPercept(Start.X, Start.Y, Start.Z, StopReason.UnableToMove) }));
            Assert.That(_movement.IsRunning, Is.False);
            Assert.That(_movement.Tick(100), Is.False);
            Assert.That(_client.Queued, Is.Empty);
        }

        // `PlayerMovementComponent.UpdatePosition` keeps whole units of each coordinate, and the idle position update sends those back.
        // Where a run ends must survive that unchanged, or the idle update is a move that stands the Character up and cancels a quit.
        [TestCase(298900.4f, 642000.9f, 4848.5f)]
        [TestCase(31234.5f, 29123.25f, 2100.75f)]
        [TestCase(296000f, 642000f, 4848f)]
        public void WholeUnits_ShouldBeWhatTheServerKeeps(float x, float y, float z)
        {
            Vector3 sent = HarnessMovement.WholeUnits(new(x, y, z));
            Vector3 repeated = new((int) sent.X, (int) sent.Y, (int) sent.Z);

            Assert.That(repeated, Is.EqualTo(sent));
            Assert.That(Vector3.Distance(sent, new(x, y, z)), Is.LessThan(2));
        }

        // At full speed from `Start` to `Target`, started at 0 with position updates at 0, 200 and 400, which the client tick has sent off.
        private void Running()
        {
            _movement.OnMaxSpeed(FullSpeed, 0);
            _movement.MoveTo(Target, null, 0);

            for (long now = 0; now <= 400; now += 200)
                Assert.That(_movement.Tick(now), Is.True);

            _client.Queued.Clear();
        }

        // The same run, stepped on its own.
        private RouteRun RunOnItsOwn()
        {
            return new(Start, Target, 0, 0, GamePlayer.PLAYER_BASE_SPEED, _region, 0);
        }

        // What `GamePlayer.MoveTo` does within a region: it sets the Character's position and heading, then calls `SendPlayerJump`.
        private void ServerJumps(Vector3 position, ushort heading)
        {
            TestPlayer greeen = (TestPlayer) _client.Player;
            (greeen.X, greeen.Y, greeen.Z) = ((int) position.X, (int) position.Y, (int) position.Z);
            greeen.TestHeading = heading;
            _movement.OnJump(new JumpPercept(position.X, position.Y, position.Z, 0x0123, heading, 0));
        }

        // The position and speed a position update carries.
        private static (Vector3 Position, float Speed) Read(ClientPacket packet)
        {
            Assert.That(packet.Code, Is.EqualTo((byte) eClientPackets.PositionUpdate));
            ReadOnlySpan<byte> data = packet.Data;
            Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data), BinaryPrimitives.ReadSingleLittleEndian(data[4..]), BinaryPrimitives.ReadSingleLittleEndian(data[8..]));
            return (position, BinaryPrimitives.ReadSingleLittleEndian(data[12..]));
        }

        // Keeps what the client would send and tell the Mind. A test clears `Queued` where the client tick would have sent it off.
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
