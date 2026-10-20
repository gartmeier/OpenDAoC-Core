using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.Database;
using DOL.GS.ServerRules;
using DOL.Network;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Stand-ins for the parts of a running server that a projection reads, for tests that can't boot a world.
    /// Game objects are made without running their constructors, which need a database and a world. Only what a test sets is real.
    /// </summary>
    public static class WorldDoubles
    {
        // Installs a server with an empty database and the given rules, and returns what to call to put the previous server back.
        // Game objects can't be made without it: their type initializer reads the database.
        public static Action InstallServer(IServerRules rules)
        {
            GameServer previous = GameServer.Instance;
            TestServer server = (TestServer) RuntimeHelpers.GetUninitializedObject(typeof(TestServer));
            server.Database = DispatchProxy.Create<IObjectDatabase, EmptyDatabase>();
            server.Rules = rules;
            GameServer.LoadTestDouble(server);
            return () => GameServer.LoadTestDouble(previous);
        }

        // A region holding one zone.
        public static Zone Zone(ushort id, int xOffset, int yOffset, int width, int height)
        {
            Region region = (Region) RuntimeHelpers.GetUninitializedObject(typeof(Region));
            Zone zone = new(region, id, $"Zone {id}", xOffset, yOffset, width, height, id, false, 0, false, 0, 0, 0, 0, 0);
            typeof(Region).GetField("_zones", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(region, new List<Zone> { zone });
            return zone;
        }

        // A region holding the given zones, which a test lays out side by side.
        public static Region Region(params (ushort Id, int XOffset, int YOffset, int Width, int Height)[] zones)
        {
            Region region = (Region) RuntimeHelpers.GetUninitializedObject(typeof(Region));
            List<Zone> list = [];

            foreach ((ushort id, int xOffset, int yOffset, int width, int height) in zones)
                list.Add(new(region, id, $"Zone {id}", xOffset, yOffset, width, height, id, false, 0, false, 0, 0, 0, 0, 0));

            typeof(Region).GetField("_zones", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(region, list);
            return region;
        }

        public static GameClient Client(ushort sessionId)
        {
            GameClient client = new(null);
            SessionId id = new(new SessionIdAllocator());
            typeof(SessionId).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(id, sessionId);
            typeof(BaseClient).GetProperty(nameof(BaseClient.SessionId)).SetValue(client, id);
            return client;
        }

        public static TestPlayer Player(GameClient client, string name)
        {
            TestPlayer player = (TestPlayer) RuntimeHelpers.GetUninitializedObject(typeof(TestPlayer));
            player.Init(client, name);
            return player;
        }

        private sealed class TestServer : GameServer
        {
            public IObjectDatabase Database;
            public IServerRules Rules;

            protected override IObjectDatabase DataBaseImpl => Database;
            protected override IServerRules ServerRulesImpl => Rules;
        }

        // Every query finds nothing.
        public class EmptyDatabase : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                Type type = targetMethod.ReturnType;

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    return Activator.CreateInstance(type);

                return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
            }
        }
    }

    /// <summary>
    /// A player whose state a test sets directly, made without its constructor (see <see cref="WorldDoubles"/>).
    /// Everything a test leaves alone is the type's default, and anything not overridden here that needs the constructor's work throws.
    /// </summary>
    public class TestPlayer : GamePlayer
    {
        private TestPlayer() : base(null, null) { }

        public GameClient TestClient { get; private set; }
        public ushort TestHeading { get; set; }
        public ushort TestModel { get; set; }
        public byte DisplayLevel { get; set; }
        public bool Alive { get; set; }
        public bool Underwater { get; set; }
        public bool Stealthed { get; set; }
        public bool Visible { get; set; }
        public Region Region { get; set; }
        public int ClassId { get; set; }
        public short Speed { get; set; }
        public byte Mana { get; set; }
        public byte Endurance { get; set; }

        public override GameClient Client => TestClient;
        public override ushort Heading { get => TestHeading; set => TestHeading = value; }
        public override ushort Model { get => TestModel; set => TestModel = value; }
        public override bool IsAlive => Alive;
        public override bool IsUnderwater => Underwater;
        public override bool IsStealthed => Stealthed;
        public override Region CurrentRegion { get => Region; set => Region = value; }
        public override ICharacterClass CharacterClass => new TestCharacterClass(ClassId);
        public override bool IsOnHorse { get => false; set { } }
        public override short CurrentSpeed { get => Speed; set => Speed = value; }
        public override byte ManaPercent => Mana;
        public override byte EndurancePercent => Endurance;

        public bool Wireframe
        {
            set => typeof(GamePlayer).GetField("m_isWireframe", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, value);
        }

        // Unset, the flag is on, as the server has it without a character record.
        public bool Roleplay
        {
            set => m_dbCharacter = new DbCoreCharacter { Name = m_name, RPFlag = value };
        }

        public override byte GetDisplayLevel(GamePlayer player)
        {
            return DisplayLevel;
        }

        public override bool IsVisibleTo(GameObject checkObject)
        {
            return Visible;
        }

        public void SetFace(eCharFacePart part, byte value)
        {
            m_customFaceAttributes[(int) part] = value;
        }

        internal void Init(GameClient client, string name)
        {
            TestClient = client;
            client.Player = this;
            m_name = name;
            m_customFaceAttributes = new byte[(int) eCharFacePart._Last + 1];
            Alive = true;
            Visible = true;
        }

        private sealed class TestCharacterClass : DefaultCharacterClass
        {
            public TestCharacterClass(int id)
            {
                m_id = id;
            }
        }
    }
}
