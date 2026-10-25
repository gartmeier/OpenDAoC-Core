using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.AI;
using DOL.Database;
using DOL.GS.ServerRules;
using DOL.GS.Spells;
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
            typeof(Region).GetProperty(nameof(GS.Region.RegionData)).SetValue(region, new RegionData { Id = id });
            Zone zone = new(region, id, $"Zone {id}", xOffset, yOffset, width, height, id, false, 0, false, 0, 0, 0, 0, 0);
            typeof(Region).GetField("_zones", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(region, new List<Zone> { zone });
            return zone;
        }

        // A region holding the given zones, which a test lays out side by side.
        public static Region Region(ushort regionId, params (ushort Id, int XOffset, int YOffset, int Width, int Height)[] zones)
        {
            Region region = (Region) RuntimeHelpers.GetUninitializedObject(typeof(Region));
            typeof(Region).GetProperty(nameof(GS.Region.RegionData)).SetValue(region, new RegionData { Id = regionId });
            List<Zone> list = [];

            foreach ((ushort id, int xOffset, int yOffset, int width, int height) in zones)
                list.Add(new(region, id, $"Zone {id}", xOffset, yOffset, width, height, id, false, 0, false, 0, 0, 0, 0, 0));

            typeof(Region).GetField("_zones", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(region, list);
            return region;
        }

        public static GameClient Client(ushort sessionId)
        {
            GameClient client = new(null);
            SetSessionId(client, sessionId);
            return client;
        }

        // As if the server had given the client `sessionId` when it connected.
        public static void SetSessionId(GameClient client, ushort sessionId)
        {
            SessionId id = new(new SessionIdAllocator());
            typeof(SessionId).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(id, sessionId);
            typeof(BaseClient).GetProperty(nameof(BaseClient.SessionId)).SetValue(client, id);
        }

        public static TestPlayer Player(GameClient client, string name)
        {
            TestPlayer player = (TestPlayer) RuntimeHelpers.GetUninitializedObject(typeof(TestPlayer));
            player.Init(client, name);
            return player;
        }

        // Gives the player an inventory that holds what a test puts in it.
        public static TestInventory Inventory(TestPlayer player)
        {
            if (player.Inventory is TestInventory inventory)
                return inventory;

            IGameInventory proxy = DispatchProxy.Create<IGameInventory, TestInventory>();
            player.Inventory = proxy;
            return (TestInventory) proxy;
        }

        // Gives the player the cache of objects the server has sent its client, which its constructor would have made.
        public static PlayerObjectCache ObjectCache(TestPlayer player)
        {
            PlayerObjectCache cache = new();
            typeof(GamePlayer).GetField($"<{nameof(GamePlayer.PlayerObjectCache)}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, cache);
            return cache;
        }

        // Puts an Effect on the player as the server keeps it once started, with `msLeft` on its timer, or none for 0.
        public static TestEffect Effect(TestPlayer player, string name, ushort icon, bool positive, int msLeft, eEffect type = eEffect.Unknown)
        {
            TestEffect effect = new(player, name, icon, positive, msLeft, type);
            AddToEffectList(player, effect);
            return effect;
        }

        // Puts the Effect of the spell `handler` casts on the player, as the server keeps it once started, with `msLeft` on its timer.
        public static ECSGameSpellEffect SpellEffect(TestPlayer player, ISpellHandler handler, int msLeft)
        {
            ECSGameSpellEffect effect = new(new ECSGameEffectInitParams(player, msLeft, 1, handler));
            AddToEffectList(player, effect);
            return effect;
        }

        // Switches the Effect off without ending it, as the server does to a buff a better one of its kind replaced.
        public static void Disable(ECSGameEffect effect)
        {
            FieldInfo state = typeof(ECSGameEffect).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            state.SetValue(effect, Enum.Parse(state.FieldType, "Disabled"));
        }

        // Adds the Concentration buff to its caster's concentration list, wherever its Effect is, as the server does once it has started.
        public static void AddConcentrationEffect(TestPlayer caster, ECSGameSpellEffect effect)
        {
            var concentrationEffects = (List<ECSGameSpellEffect>) typeof(EffectListComponent).GetField("_concentrationEffects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(caster.effectListComponent);
            concentrationEffects.Add(effect);
        }

        private static void AddToEffectList(TestPlayer player, ECSGameEffect effect)
        {
            var effects = (Dictionary<eEffect, List<ECSGameEffect>>) typeof(EffectListComponent).GetField("_effects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player.effectListComponent);

            if (effects.TryGetValue(effect.EffectType, out List<ECSGameEffect> ofType))
                ofType.Add(effect);
            else
                effects[effect.EffectType] = [effect];
        }

        public static TestNpc Npc(string name)
        {
            TestNpc npc = (TestNpc) RuntimeHelpers.GetUninitializedObject(typeof(TestNpc));
            npc.Init(name);
            return npc;
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
        public byte TestHealthPercent { get; set; }
        public ushort TestModel { get; set; }
        public byte DisplayLevel { get; set; }
        public bool Alive { get; set; }
        public bool Underwater { get; set; }
        public bool Stealthed { get; set; }
        public bool Visible { get; set; }
        public Region Region { get; set; }
        public int ClassId { get; set; }
        public short Speed { get; set; }
        public byte TestManaPercent { get; set; }
        public byte TestEndurancePercent { get; set; }
        public bool TurningDisabled { get; set; }
        // What `GetModified` gives for each property. Every other property is 0.
        public Dictionary<eProperty, int> Modified { get; private set; }

        public override GameClient Client => TestClient;
        public override ushort Heading { get => TestHeading; set => TestHeading = value; }
        public override byte HealthPercent => TestHealthPercent;
        public override ushort Model { get => TestModel; set => TestModel = value; }
        public override bool IsAlive => Alive;
        public override bool IsUnderwater => Underwater;
        public override bool IsStealthed => Stealthed;
        public override Region CurrentRegion { get => Region; set => Region = value; }
        public override ICharacterClass CharacterClass => new TestCharacterClass(ClassId);
        public override bool IsOnHorse { get => false; set { } }
        public override short CurrentSpeed { get => Speed; set => Speed = value; }
        public override byte ManaPercent => TestManaPercent;
        public override byte EndurancePercent => TestEndurancePercent;
        public override bool IsTurningDisabled => TurningDisabled;

        // What the status, points and player packets read, all 0 unless a test sets them.
        public int TestHealth { get; set; }
        public int TestMaxHealth { get; set; }
        public int TestMana { get; set; }
        public int TestMaxMana { get; set; }
        public int TestEndurance { get; set; }
        public int TestMaxEndurance { get; set; }
        public int TestConcentration { get; set; }
        public int TestMaxConcentration { get; set; }
        public byte TestConcentrationPercent { get; set; }
        public bool Sitting { get; set; }
        public long TestRealmPoints { get; set; }
        public ushort TestLevelPermill { get; set; }
        public int TestSpecPoints { get; set; }
        public long TestBountyPoints { get; set; }
        public int TestRealmSpecPoints { get; set; }
        public ushort TestChampionLevelPermill { get; set; }
        public long TestExperience { get; set; }
        public long TestExperienceForNextLevel { get; set; }

        public override int Health { get => TestHealth; set => TestHealth = value; }
        public override int MaxHealth => TestMaxHealth;
        public override int Mana { get => TestMana; set => TestMana = value; }
        public override int MaxMana => TestMaxMana;
        public override int Endurance { get => TestEndurance; set => TestEndurance = value; }
        public override int MaxEndurance => TestMaxEndurance;
        public override int Concentration => TestConcentration;
        public override int MaxConcentration => TestMaxConcentration;
        public override byte ConcentrationPercent => TestConcentrationPercent;
        public override bool IsSitting { get => Sitting; set => Sitting = value; }
        public override long RealmPoints { get => TestRealmPoints; set => TestRealmPoints = value; }
        public override ushort LevelPermill => TestLevelPermill;
        public override int SkillSpecialtyPoints => TestSpecPoints;
        public override long BountyPoints { get => TestBountyPoints; set => TestBountyPoints = value; }
        public override int RealmSpecialtyPoints => TestRealmSpecPoints;
        public override ushort ChampionLevelPermill => TestChampionLevelPermill;
        public override long Experience { get => TestExperience; set => TestExperience = value; }
        public override long ExperienceForNextLevel => TestExperienceForNextLevel;

        // What the stats packet reads, 0 unless a test sets it.
        public int TestConstitutionLost { get; set; }

        public override int TotalConstitutionLostAtDeath { get => TestConstitutionLost; set => TestConstitutionLost = value; }

        public override int GetModified(eProperty property)
        {
            return Modified.GetValueOrDefault(property);
        }

        // No buffs or debuffs: the base is what's modified.
        public override int GetModifiedBase(eProperty property)
        {
            return Modified.GetValueOrDefault(property);
        }

        // What the money packet reads, all 0 unless a test sets them.
        public int TestCopper { get; set; }
        public int TestSilver { get; set; }
        public int TestGold { get; set; }
        public int TestMithril { get; set; }
        public int TestPlatinum { get; set; }

        public override int Copper { get => TestCopper; protected set => TestCopper = value; }
        public override int Silver { get => TestSilver; protected set => TestSilver = value; }
        public override int Gold { get => TestGold; protected set => TestGold = value; }
        public override int Mithril { get => TestMithril; protected set => TestMithril = value; }
        public override int Platinum { get => TestPlatinum; protected set => TestPlatinum = value; }

        public bool Wireframe
        {
            set => typeof(GamePlayer).GetField("m_isWireframe", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, value);
        }

        // Unset, the flag is on, as the server has it without a character record.
        public bool Roleplay
        {
            set => m_dbCharacter = new DbCoreCharacter { Name = m_name, RPFlag = value };
        }

        // The option that hides other players' Graves. A Grave asks for it, so it needs a character record.
        public bool HideGraves
        {
            set => m_dbCharacter = new DbCoreCharacter { Name = m_name, HideGraves = value };
        }

        // The spell lines a list caster can cast from now, with their spells. The server builds its list from them only when asked to, or when it has none,
        // and keeps that list to read the client's spell numbers against, as `GamePlayer.GetAllUsableListSpells` does.
        public List<(SpellLine, List<Skill>)> ListSpells { get; set; }
        private List<(SpellLine, List<Skill>)> _builtListSpells;

        public override List<(SpellLine, List<Skill>)> GetAllUsableListSpells(bool update = false)
        {
            if (update || _builtListSpells.Count == 0)
                _builtListSpells = [.. ListSpells];

            return _builtListSpells;
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
            movementComponent = new PlayerMovementComponent(this);
            effectListComponent = EffectListComponent.Create(this);
            Modified = [];
            ListSpells = [];
            _builtListSpells = [];
            // No specializations, and no skills built from them yet, with the locks the initializers would have made.
            typeof(GamePlayer).GetField(nameof(m_specialization), BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new Dictionary<string, Specialization>());
            typeof(GamePlayer).GetField(nameof(_specializationLock), BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new System.Threading.Lock());
            typeof(GamePlayer).GetField("_usableSkills", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new List<(Skill, Skill)>());
            typeof(GamePlayer).GetField("_usableSkillsLock", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new System.Threading.Lock());
            // No abilities, but the lock the constructor would have made to look them up.
            typeof(GameLiving).GetField("_abilitiesLock", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new System.Threading.Lock());
            m_abilities = [];
            // The temporary properties the initializer would have made.
            typeof(GameLiving).GetField($"<{nameof(TempProperties)}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new PropertyCollection());
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
    /// <summary>
    /// An NPC whose state a test sets directly, made without its constructor (see <see cref="WorldDoubles"/>).
    /// Its movement component is real, so what the packet library reads from it is what the server would have. Standing still unless a test calls <see cref="Move"/>.
    /// </summary>
    public class TestNpc : GameNPC
    {
        private TestNpc() : base((ABrain) null) { }

        public ushort TestHeading { get; set; }
        public ushort TestModel { get; set; }
        public byte DisplayLevel { get; set; }
        public bool Visible { get; set; }
        public bool Underwater { get; set; }
        public bool Attacking { get; set; }
        public byte Health { get; set; }
        public Region Region { get; set; }
        public eQuestIndicator QuestIndicator { get; set; }

        public override ushort Heading { get => TestHeading; set => TestHeading = value; }
        public override ushort Model { get => TestModel; set => TestModel = value; }
        public override bool IsUnderwater => Underwater;
        public override bool IsAttacking => Attacking;
        public override byte HealthPercent => Health;
        public override Region CurrentRegion { get => Region; set => Region = value; }

        public new byte Size
        {
            set => m_size = value;
        }

        public new eFlags Flags
        {
            set => m_flags = value;
        }

        public new string GuildName
        {
            set => m_guildName = value;
        }

        public bool HasInventory
        {
            set => m_inventory = value ? (IGameInventory) RuntimeHelpers.GetUninitializedObject(typeof(GameNPCInventory)) : null;
        }

        public ABrain OwnBrain
        {
            set => m_ownBrain = value;
        }

        // On its way from where it is to `destination`, as the movement component has it between two ticks.
        public void Move(short speed, Vector3 velocity, double horizontalVelocityForClient, Vector3 destination)
        {
            movementComponent.CurrentSpeed = speed;
            movementComponent.Velocity = velocity;
            movementComponent.Destination = destination;
            movementComponent.DestinationForClient = destination;
            SetComponentProperty(nameof(NpcMovementComponent.IsDestinationValid), true);
            SetComponentProperty(nameof(NpcMovementComponent.HorizontalVelocityForClient), horizontalVelocityForClient);
        }

        public override bool IsVisibleTo(GameObject checkObject)
        {
            return Visible;
        }

        public override byte GetDisplayLevel(GamePlayer player)
        {
            return DisplayLevel;
        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            return QuestIndicator;
        }

        // Where it is, for the packet library's reads of the movement component too.
        public void Place(int x, int y, int z)
        {
            (X, Y, Z) = (x, y, z);
            typeof(MovementComponent).GetField("_ownerPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(movementComponent, new Vector3(x, y, z));
            movementComponent.PositionForClient = new Vector3(x, y, z);
        }

        internal void Init(string name)
        {
            m_name = name;
            m_guildName = string.Empty;
            typeof(GameNPC).GetField("m_brains", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, new ArrayList());
            movementComponent = new NpcMovementComponent(this);
            ((GameLiving) this).movementComponent = movementComponent;
            Visible = true;
        }

        private void SetComponentProperty(string name, object value)
        {
            typeof(NpcMovementComponent).GetProperty(name).SetValue(movementComponent, value);
        }
    }

    /// <summary>
    /// A player's inventory that holds what a test puts in it (see <see cref="WorldDoubles.Inventory"/>). Every other member throws.
    /// </summary>
    public class TestInventory : DispatchProxy
    {
        public Dictionary<eInventorySlot, DbInventoryItem> Items { get; } = [];
        // In pounds, as the server works it out.
        public int Weight { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return targetMethod.Name switch
            {
                nameof(IGameInventory.GetItem) => Items.GetValueOrDefault((eInventorySlot) args[0]),
                nameof(IGameInventory.GetItemRange) => Items.Where(pair => pair.Key >= (eInventorySlot) args[0] && pair.Key <= (eInventorySlot) args[1]).Select(static pair => pair.Value).ToList(),
                "get_" + nameof(IGameInventory.EquippedItems) => Items.Where(static pair => pair.Key is >= eInventorySlot.MinEquipable and <= eInventorySlot.MaxEquipable).Select(static pair => pair.Value).ToList(),
                "get_" + nameof(IGameInventory.InventoryWeight) => Weight,
                _ => throw new NotSupportedException(targetMethod.Name)
            };
        }
    }

    /// <summary>
    /// An Effect a test puts on a living with <see cref="WorldDoubles.Effect"/>, with only what the buff bar shows of it.
    /// </summary>
    public sealed class TestEffect : ECSGameEffect
    {
        private readonly string _name;
        private readonly ushort _icon;
        private readonly bool _positive;

        public TestEffect(GameLiving owner, string name, ushort icon, bool positive, int duration, eEffect type) : base(new ECSGameEffectInitParams(owner, duration, 1))
        {
            _name = name;
            _icon = icon;
            _positive = positive;
            EffectType = type;
        }

        public override string Name => _name;
        public override ushort Icon => _icon;
        public override bool HasPositiveEffect => _positive;
    }
}
