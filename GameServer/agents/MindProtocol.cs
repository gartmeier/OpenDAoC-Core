using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.GS.Styles;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What a Mind and the server say to each other over the Mind's WebSocket: JSON, one message per frame, each with a discriminator naming its kind.
    /// These types are the protocol's only definition. <see cref="MindProtocolTypeScript"/> generates the Mind's TypeScript from them, so a change here needs a regenerated `mind/src/protocol.ts`.
    /// </summary>
    public static class MindProtocol
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // A Mind may write the discriminator anywhere in its object.
            AllowOutOfOrderMetadataProperties = true,
            // Both ends are on this machine, and apostrophes in reasons stay readable.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Enums go over the wire as their names, which the generated TypeScript lists.
            Converters = { new JsonStringEnumConverter() }
        };

        // Returns null for anything that isn't a message a Mind can send.
        public static MindMessage Read(ReadOnlySpan<byte> json)
        {
            return Read(json, out _);
        }

        // Returns null for anything that isn't a message a Mind can send, with `error` saying what's wrong, for the Mind's developer.
        public static MindMessage Read(ReadOnlySpan<byte> json, out string error)
        {
            error = null;

            try
            {
                MindMessage message = JsonSerializer.Deserialize<MindMessage>(json, _options);

                if (message == null)
                    error = "It's null.";

                return message;
            }
            catch (JsonException e)
            {
                // The reader's own exception is inside when the text isn't JSON at all. Otherwise the message names an unknown discriminator, or the path of a value of the wrong kind.
                error = e.InnerException is JsonException ? $"It isn't JSON: {e.Message}" : e.Message;
                return null;
            }
            catch (NotSupportedException e)
            {
                // A message, or a Primitive, without its discriminator.
                error = $"Its \"type\", or its Primitive's \"name\", is missing ({e.Message}).";
                return null;
            }
        }

        public static byte[] Write(ServerMessage message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, _options);
        }
    }

    /// <summary>
    /// A message from a Mind to the server.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(HelloMessage), "hello")]
    [JsonDerivedType(typeof(PrimitiveMessage), "primitive")]
    public abstract record MindMessage;

    // The first message: the shared token from the server's configuration, and the Agent's Character to log in.
    public sealed record HelloMessage(string Token, string Character) : MindMessage;

    // A Primitive for the Agent to do. `Id` is the Mind's own, and comes back in the Primitive's result.
    public sealed record PrimitiveMessage(int Id, Primitive Primitive) : MindMessage;

    /// <summary>
    /// A message from the server to a Mind.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(RefusedMessage), "refused")]
    [JsonDerivedType(typeof(EnteredMessage), "entered")]
    [JsonDerivedType(typeof(PerceptMessage), "percept")]
    [JsonDerivedType(typeof(PrimitiveResultMessage), "primitiveResult")]
    [JsonDerivedType(typeof(ReplacedMessage), "replaced")]
    public abstract record ServerMessage;

    // The login didn't happen, or the Mind sent something the protocol doesn't allow, for a reason that's a whole sentence for the Human. The server closes the connection after it.
    // `Retry` says whether the same hello can succeed later, for example once the server has removed a linkdead Character.
    public sealed record RefusedMessage(string Reason, bool Retry) : ServerMessage;

    // The Character is in the world. `TookOver` says whether this Mind took over the Agent's connection from another while the Character stayed in the world, rather than logging it in.
    public sealed record EnteredMessage(bool TookOver) : ServerMessage;

    // One call the server made on the Agent's packet library. `Sequence` counts from 1 in call order, and `ElapsedMs` is the time since the login.
    public sealed record PerceptMessage(long Sequence, long ElapsedMs, Percept Percept) : ServerMessage;

    // Whether the Primitive's packet was queued for the Harness client's next tick, and why not if it wasn't. Never whether it worked (ADR 0004).
    public sealed record PrimitiveResultMessage(int Id, bool Queued, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Reason) : ServerMessage;

    // Another Mind has taken over the Agent's connection, and the Character stays in the world. The server closes the connection after it.
    public sealed record ReplacedMessage : ServerMessage;

    /// <summary>
    /// One entry in the Harness's vocabulary of things an Agent decides to do.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "name")]
    [JsonDerivedType(typeof(QuitPrimitive), "quit")]
    [JsonDerivedType(typeof(SayPrimitive), "say")]
    [JsonDerivedType(typeof(MoveToPrimitive), "moveTo")]
    [JsonDerivedType(typeof(StopPrimitive), "stop")]
    [JsonDerivedType(typeof(FacePrimitive), "face")]
    [JsonDerivedType(typeof(TargetPrimitive), "target")]
    [JsonDerivedType(typeof(AttackPrimitive), "attack")]
    [JsonDerivedType(typeof(SitPrimitive), "sit")]
    [JsonDerivedType(typeof(StandPrimitive), "stand")]
    [JsonDerivedType(typeof(CommandPrimitive), "command")]
    [JsonDerivedType(typeof(MoveItemPrimitive), "moveItem")]
    [JsonDerivedType(typeof(ExaminePrimitive), "examine")]
    [JsonDerivedType(typeof(ExamineMerchantItemPrimitive), "examineMerchantItem")]
    [JsonDerivedType(typeof(PickUpPrimitive), "pickUp")]
    [JsonDerivedType(typeof(TalkToPrimitive), "talkTo")]
    [JsonDerivedType(typeof(BuyPrimitive), "buy")]
    [JsonDerivedType(typeof(SellPrimitive), "sell")]
    [JsonDerivedType(typeof(UseStylePrimitive), "useStyle")]
    [JsonDerivedType(typeof(AnswerDialogPrimitive), "answerDialog")]
    [JsonDerivedType(typeof(UseItemPrimitive), "useItem")]
    [JsonDerivedType(typeof(CastSpellPrimitive), "castSpell")]
    [JsonDerivedType(typeof(CancelConcentrationPrimitive), "cancelConcentration")]
    [JsonDerivedType(typeof(UseAbilityPrimitive), "useAbility")]
    [JsonDerivedType(typeof(CommandPetPrimitive), "commandPet")]
    public abstract record Primitive;

    // Types `/quit`, as a Human does. The server's quit path takes it from there, quit delay included.
    public sealed record QuitPrimitive : Primitive;

    // Says `Text` to those nearby, as a Human typing a chat line does.
    public sealed record SayPrimitive(string Text) : Primitive;

    // Runs to the point, in global coordinates, at the maximum speed the server last told the client. A new one replaces the run in progress.
    // On a zone's navmesh the run follows a route around what's in the way, on the ground, to the nearest walkable point; elsewhere it goes in a straight line (ADR 0007).
    // `Within` ends the run that many units short of the end of the route. The route, arriving, and stopping short come as Percepts (ADR 0004).
    public sealed record MoveToPrimitive(
        [property: JsonRequired] float X,
        [property: JsonRequired] float Y,
        [property: JsonRequired] float Z,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] float? Within = null) : Primitive;

    // Ends the run in progress where the Character is. Without one it does nothing.
    public sealed record StopPrimitive : Primitive;

    // Turns on the spot to `Heading`, 0 to 4095, at once. Heading 0 points towards +Y and grows clockwise, as the server measures headings.
    // During a run it does nothing: the run's own heading overtakes it, and the run goes on.
    public sealed record FacePrimitive([property: JsonRequired] int Heading) : Primitive;

    // Selects the object, as a click on it does, and the server describes it in `message` Percepts. 0 selects nothing.
    // From then on the client's position updates carry it, claiming it's in view: the server's own checks are the only line-of-sight checks.
    public sealed record TargetPrimitive([property: JsonRequired] int ObjectId) : Primitive;

    // Turns auto-attack on or off, as the attack key does. Attacking needs a target, a melee weapon and the server's own checks.
    public sealed record AttackPrimitive([property: JsonRequired] bool On) : Primitive;

    // Sits down, as the sit key does.
    public sealed record SitPrimitive : Primitive;

    // Stands up, as the sit key does while sitting.
    public sealed record StandPrimitive : Primitive;

    // Types a slash command, as a Human types it, such as `/train Blades 3` or `/release`. The server handles it as a Human's.
    public sealed record CommandPrimitive([property: JsonRequired] string Text) : Primitive;

    // Moves the item in slot `From` to slot `To`, as dragging it does, which equips and unequips too. Slots are the client's: equipment 10-37 (right hand 10, left hand 11, two-handed 12, ranged 13, head 21, hands 22, feet 23, torso 25, cloak 26, legs 27, arms 28), backpack 40-79.
    // A whole stack moves. Onto a taken slot, the two items swap. Slot 1 is the ground, and the horse's bags are 80-99. The Harness refuses anything else.
    // Moving an item to the ground drops it there, the only way a 1.127 client gets rid of one: it has no way to destroy an item.
    public sealed record MoveItemPrimitive([property: JsonRequired] int From, [property: JsonRequired] int To) : Primitive;

    // Asks for the details of the item in slot `Slot`, as a shift-click does. They come as an `examined` Percept.
    public sealed record ExaminePrimitive([property: JsonRequired] int Slot) : Primitive;

    // Asks for the details of the item at `Index` in the merchant window the client has open, the page times 30 plus the place on the page. They come as an `examined` Percept. Needs the merchant targeted.
    public sealed record ExamineMerchantItemPrimitive([property: JsonRequired] int Index) : Primitive;

    // Picks up the object from the ground, targeting it first as a click does. The server checks the Character may take it and stands within 256 units.
    public sealed record PickUpPrimitive([property: JsonRequired] int ObjectId) : Primitive;

    // Talks to the NPC, as a right-click does, targeting it first. The server needs it within 192 units. A merchant answers with its window, as a `merchantWindow` Percept.
    public sealed record TalkToPrimitive([property: JsonRequired] int ObjectId) : Primitive;

    // Buys `Count` (1 to 255) of the item at `Index` in the window of the merchant the client targets, numbered as for `examineMerchantItem`. The server needs the merchant within 256 units.
    public sealed record BuyPrimitive([property: JsonRequired] int Index, [property: JsonRequired] int Count) : Primitive;

    // Sells the item in slot `Slot` to the merchant the client targets. The server needs the merchant within 256 units.
    public sealed record SellPrimitive([property: JsonRequired] int Slot) : Primitive;

    // Uses the style named `Style` on the next swing, as a click on its icon does. `Line` is its specialization, as in `skills`: without it, the first style of that name in the skill list.
    // The Harness refuses a style the server's last `skills` didn't list. Whether it's performed, and why not, the server says in `message` Percepts.
    // What a style needs and does is in its `skills` entry: a client never asks the server, it has the tooltip the server sent with the skill list.
    public sealed record UseStylePrimitive(
        [property: JsonRequired] string Style,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Line = null) : Primitive;

    // Accepts the open Dialog, or declines it (`Accept` false), as a click on its button does. `Code`, `Data1`, `Data2` and `Data3` are the `dialog` Percept's, echoed back as the client does.
    // An OK-only Dialog is answered the same way. Whether the server still waits on that Dialog isn't known here: the chat that follows says what happened.
    public sealed record AnswerDialogPrimitive(
        [property: JsonRequired] eDialogCode Code,
        [property: JsonRequired] int Data1,
        [property: JsonRequired] int Data2,
        [property: JsonRequired] int Data3,
        [property: JsonRequired] bool Accept) : Primitive;

    // Uses the item in slot `Slot`, backpack or equipment as for `moveItem`, as a right-click on it in the backpack does. `Second` uses its second spell, which some charged gear has.
    // The Harness refuses only a slot past 255. Whether the use worked, and why not, the server says in `message` Percepts: an empty slot, moving, combat, the reuse timer. A teleport comes as a `jump` Percept.
    public sealed record UseItemPrimitive(
        [property: JsonRequired] int Slot,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Second = null) : Primitive;

    // Casts the spell named `Spell` at what the client targets, as a click on its icon does. A heal or a buff needs a friendly target, the Character itself included: `target` it first.
    // The Harness refuses a spell the server's last `skills` didn't list. Whether the cast starts, lands or is interrupted the server says in `castStarted`, `spellLanded` and `interrupted` Percepts, and why it refuses one only in `message` Percepts.
    // What a spell does and costs is in its `skills` entry, from the tooltip the server sent with the skill list.
    public sealed record CastSpellPrimitive([property: JsonRequired] string Spell) : Primitive;

    // Uses the ability named `Ability`, such as Sprint, Quickcast or a realm ability, as a click on its icon does.
    // The Harness refuses an ability or realm ability the server's last `skills` didn't list. Whether it worked, and why not, the server says in `message` Percepts, and an ability with an Effect shows in `effects`.
    public sealed record UseAbilityPrimitive([property: JsonRequired] string Ability) : Primitive;

    // Clicks one button in the pet window, as a Human does: `Order` changes one row of the window, its aggression, its walk or a command, and leaves the other rows as they were.
    // `Attack` and `GoTarget` act on what the client targets: `target` it first. The Harness doesn't check that there's a Pet. What the Pet does, and why not, the server says in `message` Percepts, and its settings in `petWindow`.
    public sealed record CommandPetPrimitive([property: JsonRequired] PetOrder Order) : Primitive;

    // The pet window's buttons, row by row: aggression, walk, then the commands.
    public enum PetOrder
    {
        Aggressive,
        Defensive,
        Passive,
        Follow,
        Stay,
        GoTarget,
        ComeHere,
        Attack,
        Release
    }

    // Cancels the Concentration buff `Spell` that the Character keeps up, as a click on it in the concentration window does. `Target` names who wears it: without it, the first of that spell in the window.
    // Names are matched as the `concentration` Percept cuts them, so a whole name and a cut one both do. The Harness refuses a buff the server's last `concentration` didn't list.
    public sealed record CancelConcentrationPrimitive(
        [property: JsonRequired] string Spell,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Target = null) : Primitive;

    /// <summary>
    /// One thing the Harness tells the Mind: only what the 1.127 packet would tell a real client (ADR 0001).
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(BarePercept), "bare")]
    [JsonDerivedType(typeof(MessagePercept), "message")]
    [JsonDerivedType(typeof(BarePacketPercept), "barePacket")]
    [JsonDerivedType(typeof(PlayerCreatePercept), "playerCreate")]
    [JsonDerivedType(typeof(PlayerPositionPercept), "playerPosition")]
    [JsonDerivedType(typeof(NpcCreatePercept), "npcCreate")]
    [JsonDerivedType(typeof(ObjectCreatePercept), "objectCreate")]
    [JsonDerivedType(typeof(ObjectUpdatePercept), "objectUpdate")]
    [JsonDerivedType(typeof(ObjectDeletePercept), "objectDelete")]
    [JsonDerivedType(typeof(ObjectRemovePercept), "objectRemove")]
    [JsonDerivedType(typeof(OwnPositionPercept), "ownPosition")]
    [JsonDerivedType(typeof(JumpPercept), "jump")]
    [JsonDerivedType(typeof(MaxSpeedPercept), "maxSpeed")]
    [JsonDerivedType(typeof(StatusPercept), "status")]
    [JsonDerivedType(typeof(PointsPercept), "points")]
    [JsonDerivedType(typeof(PlayerPercept), "player")]
    [JsonDerivedType(typeof(SkillsPercept), "skills")]
    [JsonDerivedType(typeof(EffectsPercept), "effects")]
    [JsonDerivedType(typeof(ConcentrationPercept), "concentration")]
    [JsonDerivedType(typeof(PetWindowPercept), "petWindow")]
    [JsonDerivedType(typeof(TargetChangedPercept), "targetChanged")]
    [JsonDerivedType(typeof(GroupWindowPercept), "groupWindow")]
    [JsonDerivedType(typeof(GroupStatusPercept), "groupStatus")]
    [JsonDerivedType(typeof(GroupPositionsPercept), "groupPositions")]
    [JsonDerivedType(typeof(CastStartedPercept), "castStarted")]
    [JsonDerivedType(typeof(SpellLandedPercept), "spellLanded")]
    [JsonDerivedType(typeof(InterruptedPercept), "interrupted")]
    [JsonDerivedType(typeof(CooldownsPercept), "cooldowns")]
    [JsonDerivedType(typeof(DiedPercept), "died")]
    [JsonDerivedType(typeof(RevivedPercept), "revived")]
    [JsonDerivedType(typeof(InventoryPercept), "inventory")]
    [JsonDerivedType(typeof(EncumbrancePercept), "encumbrance")]
    [JsonDerivedType(typeof(MoneyPercept), "money")]
    [JsonDerivedType(typeof(ExaminedPercept), "examined")]
    [JsonDerivedType(typeof(MerchantWindowPercept), "merchantWindow")]
    [JsonDerivedType(typeof(DialogPercept), "dialog")]
    [JsonDerivedType(typeof(StatsPercept), "stats")]
    [JsonDerivedType(typeof(RoutedPercept), "routed")]
    [JsonDerivedType(typeof(MovedPercept), "moved")]
    [JsonDerivedType(typeof(ArrivedPercept), "arrived")]
    [JsonDerivedType(typeof(StoppedPercept), "stopped")]
    public abstract record Percept;

    // A packet-library call without a projection yet. Only its name.
    public sealed record BarePercept(string Call) : Percept;

    // A packet the server built itself and handed whole to `SendUDP` or `SendTCP` (`Call`), in a layout that isn't decoded. Only its packet code.
    public sealed record BarePacketPercept(string Call, int Code) : Percept;

    // Another player came into view. Positions are global.
    // `Realm` is the player's realm as the Agent's Character sees it. The face fields are the bytes the client draws the face from.
    // `Horse` is there only while the player rides a horse.
    public sealed record PlayerCreatePercept(
        float X,
        float Y,
        float Z,
        int SessionId,
        int ObjectId,
        int Heading,
        int Model,
        int Level,
        PacketRealm Realm,
        bool Dead,
        bool Swimming,
        bool Stealthed,
        bool Wireframe,
        bool Vampiir,
        int EyeSize,
        int LipSize,
        int MoodType,
        int EyeColor,
        int HairColor,
        int FaceType,
        int HairStyle,
        string Name,
        string GuildName,
        string LastName,
        string PrefixName,
        string Title,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlayerCreateHorse Horse) : Percept;

    // The horse a player rides when it comes into view. `BardingColor` carries the rider's guild emblem instead when the barding has no colour and the rider has a guild.
    public sealed record PlayerCreateHorse(int Id, int Barding, int BardingColor, int Saddle, int SaddleColor);

    // Another player moved, turned or changed state, decoded from the ready-built 1.127 position packet (ADR 0001). Positions are global.
    // `Heading` is 0 to 4095, and `HeadingFlags` the bits above it that the player's client sent. While the player rides, the server puts its steed's object ID in their place.
    // `StateFlags` and `ActionFlags` are the server's `PlayerPositionUpdateHandler.StateFlags` and `ActionFlags`. `Attacking` is whether the player is in attack mode.
    public sealed record PlayerPositionPercept(
        float X,
        float Y,
        float Z,
        float Speed,
        float FallSpeed,
        int SessionId,
        int ObjectId,
        int ZoneId,
        int StateFlags,
        int SteedSeat,
        int Heading,
        int HeadingFlags,
        int ActionFlags,
        bool Roleplay,
        int HealthPercent,
        bool Attacking,
        int PowerPercent,
        int EndurancePercent) : Percept;

    // An NPC came into view. Positions are global. The server sends it again whenever the NPC's look changes.
    // `Speed` and `ZSpeed` are 0 unless the NPC is on its way somewhere. `Level` is the level the Agent's Character sees, and `Realm` the realm it sees.
    // `Equipped` means the client should wait for the NPC's equipment before drawing it. `Pet` means someone controls it.
    // `Untargetable` and `NameHidden` are never set for a GM, whose `Name` instead ends in `-DOR` and `-NON`, after the name in brackets.
    public sealed record NpcCreatePercept(
        int X,
        int Y,
        int Z,
        int ObjectId,
        int Speed,
        int ZSpeed,
        int Heading,
        int Model,
        int Size,
        int Level,
        bool Statue,
        PacketRealm Realm,
        bool Ghost,
        bool Equipped,
        bool Torch,
        bool Peaceful,
        bool Flying,
        bool Pet,
        bool Untargetable,
        bool NameHidden,
        bool Stealthed,
        eQuestIndicator QuestIndicator,
        string Name,
        string GuildName) : Percept;

    // A World object came into view: anything that's neither a Character nor an NPC, such as a Grave, a door or loot on the ground. Positions are global. The server sends it again whenever its name, model, heading or emblem changes.
    // `Heading` is 0 to 4095. `Emblem` is a guild emblem, 0 for none. `KeepBanner` marks a keep's banner, and `Yours` loot the Agent's Character may pick up.
    // `DoorId` is there only for a door.
    public sealed record ObjectCreatePercept(
        int X,
        int Y,
        int Z,
        int ObjectId,
        int Heading,
        int Model,
        PacketRealm Realm,
        int Emblem,
        bool Underwater,
        bool KeepBanner,
        bool Yours,
        string Name,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DoorId = null) : Percept;

    // Where an object is now, usually an NPC, sent about every 5 s while it's in view and whenever it starts or stops moving. Positions are global, turned back from the zone-relative ones the packet carries.
    // `Speed` is the horizontal speed and `ZSpeed` a quarter of the vertical one, signed. `Heading` is 0 to 4095. `ZoneId` is the zone's skin ID.
    // `TargetId` is 0, or while the NPC attacks, the object it attacks, or 65535 when it isn't standing within reach of it.
    // `Untargetable` is also set for a statue. `Destination` is there only while the NPC is on its way to a known point.
    public sealed record ObjectUpdatePercept(
        int X,
        int Y,
        int Z,
        int ObjectId,
        int Speed,
        int ZSpeed,
        int Heading,
        int TargetId,
        int HealthPercent,
        PacketRealm Realm,
        bool Untargetable,
        bool NameHidden,
        bool Underwater,
        bool Flying,
        int ZoneId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ObjectUpdateDestination Destination) : Percept;

    // Where an NPC is going, in global coordinates, and the zone's skin ID there.
    public sealed record ObjectUpdateDestination(int X, int Y, int Z, int ZoneId);

    // An object went out of view, for example a player who stealthed.
    public sealed record ObjectDeletePercept(int ObjectId) : Percept;

    // An object left the world, for example a player who quit. `ObjectType` is 2 for a player, 1 for a living NPC and 0 for anything else, a dead NPC included.
    public sealed record ObjectRemovePercept(int ObjectId, int ObjectType) : Percept;

    // The server set where the Agent's Character is, at world entry. Positions are global.
    // `ZoneXOffset` and `ZoneYOffset` are the zone's offsets in units of 8192, sent only in a dungeon and 0 elsewhere. `Region` is the region the client loads, the region's skin.
    // `ServerName` is there only in a housing region.
    public sealed record OwnPositionPercept(
        float X,
        float Y,
        float Z,
        int ObjectId,
        int Heading,
        int ZoneXOffset,
        int ZoneYOffset,
        int Region,
        bool DivingEnabled,
        bool Underwater,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string ServerName) : Percept;

    // The server set where the Agent's Character is and which way it faces, for example by a GM's `/jump` or by pulling it back after its speed check. Positions are global.
    // `X`, `Y` and `Z` are 0 when the server set only the heading, as for `/faceloc`, and the client then keeps its position. `Z` is the packet's 16 bits. `HouseNumber` is the house the Character is in, or 0.
    public sealed record JumpPercept(float X, float Y, float Z, int ObjectId, int Heading, int HouseNumber) : Percept;

    // The server set how fast the Agent's Character may run, as a percentage of the base speed of 191 units per second. `WaterSpeed` is the same for swimming.
    public sealed record MaxSpeedPercept(int MaxSpeedPercent, bool TurningDisabled, int WaterSpeed) : Percept;

    // The Agent's Character's health, power, endurance and concentration, as percentages and as values, and whether it sits.
    // The 1.127 packet carries no dead flag: dying shows as health 0 and a `died` Percept.
    public sealed record StatusPercept(
        int HealthPercent,
        int PowerPercent,
        bool Sitting,
        int EndurancePercent,
        int ConcentrationPercent,
        int MaxPower,
        int MaxEndurance,
        int MaxConcentration,
        int MaxHealth,
        int Health,
        int Endurance,
        int Power,
        int Concentration) : Percept;

    // The Agent's Character's progress. `LevelPermill` is the experience towards the next level in thousandths, and `Experience` and `ExperienceForNextLevel` the totals it's worked out from.
    // `SpecPoints` are the specialization points it has to train with, and `RealmSpecPoints` the realm ability points.
    public sealed record PointsPercept(
        long RealmPoints,
        int LevelPermill,
        int SpecPoints,
        long BountyPoints,
        int RealmSpecPoints,
        int ChampionLevelPermill,
        long Experience,
        long ExperienceForNextLevel) : Percept;

    // Who the Agent's Character is: its level, name, class and base class (the same until it chooses a class at 5), and its maximum health.
    // The packet also carries titles, guild, house and crafting, which have no fields yet.
    public sealed record PlayerPercept(int Level, string Name, string ClassName, string BaseClassName, int MaxHealth) : Percept;

    // Everything on the Agent's Character's skill pages, in the order the client lists them: specializations with their trained levels, then abilities, styles and spells.
    public sealed record SkillsPercept(SkillEntry[] Skills) : Percept;

    // One skill, by the page of the client's skill window it's on, as the packet numbers them. `Level` is a specialization's trained level, an ability's or a spell's level, or the specialization level a style needs.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "page")]
    [JsonDerivedType(typeof(SpecializationEntry), "Specialization")]
    [JsonDerivedType(typeof(AbilityEntry), "Abilities")]
    [JsonDerivedType(typeof(StyleEntry), "Styles")]
    [JsonDerivedType(typeof(SpellEntry), "Spells")]
    [JsonDerivedType(typeof(SongEntry), "Songs")]
    [JsonDerivedType(typeof(AbilitySpellEntry), "AbilitiesSpell")]
    [JsonDerivedType(typeof(RealmAbilityEntry), "RealmAbilities")]
    public abstract record SkillEntry(string Name, int Level);

    // `Name` is the specialization's database name, as `/train` takes it, such as "Light" for Light Magic.
    public sealed record SpecializationEntry(string Name, int Level) : SkillEntry(Name, Level);

    public sealed record AbilityEntry(string Name, int Level) : SkillEntry(Name, Level);

    // `Line` is its specialization, which the packet names by an index the client knows, and none for 0. `Details` are what its tooltip shows.
    public sealed record StyleEntry(
        string Name,
        int Level,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Line,
        StyleDetails Details) : SkillEntry(Name, Level);

    // `Details` are what its tooltip shows, as for a spell's Effect, or none when the server has no handler for the spell.
    public sealed record SpellEntry(string Name, int Level, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpellDetails Details) : SkillEntry(Name, Level);

    // A song, a spell played on an instrument. `Details` as for a spell.
    public sealed record SongEntry(string Name, int Level, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpellDetails Details) : SkillEntry(Name, Level);

    public sealed record AbilitySpellEntry(string Name, int Level) : SkillEntry(Name, Level);

    public sealed record RealmAbilityEntry(string Name, int Level) : SkillEntry(Name, Level);

    // What a style needs and does, as the server's forced tooltip update tells a 1.110+ client alongside the skill list, and the client shows when its icon is hovered.
    // `Opening` is what opens it: `Offensive` (your last attack's `OpeningResult` on the target, such as `Parry` when it parried, or `Style` after `OpeningStyle`),
    // `Defensive` (the target's last attack on you, such as `Parry` when you parried it) or `Positional` (you at the target's `OpeningPosition`, facing it). `Any` needs nothing.
    // `FollowupStyles` are the styles it opens. `Fatigue` is its endurance cost, `AttackMod` and `DefensiveMod` its bonuses to hit and to defence, and `OpeningDamage` its damage growth,
    // as the tooltip numbers them. `Hidden` says it needs stealth, and `Weapon` names the weapon it needs, if any.
    public sealed record StyleDetails(
        Style.eOpening Opening,
        Style.eAttackResultRequirement OpeningResult,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Style.eOpeningPosition? OpeningPosition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string OpeningStyle,
        string[] FollowupStyles,
        int Fatigue,
        int AttackMod,
        int DefensiveMod,
        float OpeningDamage,
        bool Hidden,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string Weapon);

    // The Agent's Character's buff bar: every Effect on it, in the order the client shows them.
    public sealed record EffectsPercept(EffectEntry[] Effects) : Percept;

    // One Effect. `Positive` says it's a buff rather than a debuff. `SecondsLeft` is its time left in whole seconds when the server sent the bar, or none for an Effect without a timer.
    // `Details` are what a spell's Effect shows in its tooltip. An ability's Effect has none.
    // `Disabled` is the packet's mark for an Effect that does nothing while it's shown: one the server switched off, such as a weaker buff a better one of its kind replaced, or an immunity.
    public sealed record EffectEntry(
        string Name,
        bool Positive,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SecondsLeft = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpellDetails Details = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Disabled = null);

    // What a spell is, as the server's forced tooltip update tells a 1.110+ client, and the client's tooltip window shows: each field under its label there, and only when it shows it.
    // `Target` is in the client's words where seen ("Realm", "Realm area effect") and the server's name otherwise. `MagicType` is the server's name, which is the client's word for those seen ("Energy").
    // A negative `PowerCost` is a percentage of maximum power. `CastingSeconds` is 0 for an instant spell. `Description` is the server's sentence for what it does.
    // `ConcentrationCost` is the concentration a Concentration buff uses while it's up. `RecastSeconds` is how long after casting it the spell can't be cast again, for a spell that has a wait.
    public sealed record SpellDetails(
        int Level,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] float? DurationSeconds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PowerCost,
        string Target,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Range,
        float CastingSeconds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string MagicType,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Radius,
        string Description,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ConcentrationCost = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] float? RecastSeconds = null);

    // The Agent's Character's concentration window: every Concentration buff it keeps up, in the order it cast them, which `cancelConcentration` numbers them by.
    public sealed record ConcentrationPercept(ConcentrationBuff[] Buffs) : Percept;

    // One Concentration buff: its spell, who wears it, the Agent's Character included, and the concentration it uses. The packet cuts both names longer than 14 characters to their first 12 and "..".
    public sealed record ConcentrationBuff(string Spell, string Target, int Cost);

    // The Agent's Character's pet window: `Open` when it gets a pet, `Update` when the Pet's aggression or walk changes, `Close` when it loses the Pet, which then has `ObjectId` 0.
    // `ObjectId` is the Pet's, as its `npcCreate` has it. The packet's effect icons aren't carried.
    public sealed record PetWindowPercept(int ObjectId, ePetWindowAction Action, eAggressionState Aggression, eWalkState Walk) : Percept;

    // The server has the client select `ObjectId`, 0 for nothing, as `/assist` does with what the assisted Character has targeted. The client then selects it, as a click would.
    public sealed record TargetChangedPercept(int ObjectId) : Percept;

    // The Agent's Character's group window: every Group member in the window's order, its own Character too, or no one when it isn't in a Group.
    // Sent when someone joins or leaves, the leader changes, members are reordered or a name changes.
    public sealed record GroupWindowPercept(GroupMember[] Members) : Percept;

    // One Group member, by the object ID its `playerCreate` has.
    public sealed record GroupMember(string Name, string ClassName, int ObjectId, int Level);

    // How the Group members the server names are doing, as the group window shows it, sent when they join and whenever their percents or some of their Effects change.
    public sealed record GroupStatusPercept(GroupMemberStatus[] Members) : Percept;

    // One Group member's health, Power and endurance percent and status. A member in another region has only `InAnotherRegion`, and 0 for the rest.
    // `Nearsighted` is a spell or archery range debuff, which the server doesn't send an update for on its own.
    public sealed record GroupMemberStatus(
        int ObjectId,
        int HealthPercent,
        int PowerPercent,
        int EndurancePercent,
        bool Dead,
        bool Mezzed,
        bool Diseased,
        bool Poisoned,
        bool Linkdead,
        bool Nearsighted,
        bool InAnotherRegion);

    // Where the Agent's Group members are, as the client's map shows them, sent every 2.5 s for every member but the Agent's own Character.
    public sealed record GroupPositionsPercept(GroupMemberPosition[] Members) : Percept;

    // One Group member's zone and position in global coordinates; the packet gives no height. All three are null for a member in another region, whom the packet gives no place.
    public sealed record GroupMemberPosition(
        int ObjectId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Zone,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? X,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Y);

    // A caster in view started a cast, the Agent's Character and Mobs included: its object ID and how long the cast takes, 0 for an instant spell.
    // The server sends it once every check before the cast has passed, and not at all for an instant spell that isn't harmful. Which spell isn't carried: a client shows only its animation.
    public sealed record CastStartedPercept(int CasterId, int CastingMs) : Percept;

    // A spell landed on `TargetId` (0 for none), cast by `CasterId`. `Success` is false when it didn't take: resisted, a heal with nothing to heal, or a buff a better one kept off.
    // A bolt lands at launch, with `Success` true whether it then hits or misses. A pulsing spell lands on every pulse, and an area spell once per target.
    public sealed record SpellLandedPercept(int CasterId, int TargetId, bool Success) : Percept;

    // A caster in view had its cast interrupted, by an attack or by moving. A cast that fails its checks as it ends isn't interrupted this way: the caster gets only a message.
    public sealed record InterruptedPercept(int ObjectId) : Percept;

    // Spells and Abilities of the Agent's Character that went on Cooldown, each for whole seconds from now. 0 is ready, which the server sends only for skills it lists at world entry; it says nothing when a Cooldown ends.
    public sealed record CooldownsPercept(Cooldown[] Skills) : Percept;

    // A Spell or Ability, named as the skills Percept names it, and its Cooldown in whole seconds.
    public sealed record Cooldown(string Name, int Seconds);

    // A player died, the Agent's Character included: its object ID, and the object ID of what killed it, or 0.
    public sealed record DiedPercept(int ObjectId, int KillerId) : Percept;

    // A player came back to life, the Agent's Character included, for example after releasing.
    public sealed record RevivedPercept(int ObjectId) : Percept;

    // Slots of the Agent's Character's inventory changed: each one the server updated, with the item now in it, or without one when it's empty.
    // Slots are the client's (see `MoveItemPrimitive`), as the packet carries them, in a byte. `Window` is the inventory window the client opens with it, `Update` for none.
    // A vault's or a consignment merchant's window numbers its own slots instead. A stat bonus isn't carried: only an examine shows it.
    public sealed record InventoryPercept(eInventoryWindowType Window, InventorySlot[] Slots) : Percept;

    public sealed record InventorySlot(int Slot, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InventoryItem Item);

    // An item as the 1.127 inventory packet writes it. The packet's two value fields mean different things for different `ObjectType`s, the server's `eObjectType`:
    // `DpsAf` is a weapon's DPS times 10 or an armour piece's armour factor, and `SpeedAbs` a weapon's speed in tenths of a second or an armour piece's absorption.
    // `Count` is how many there are, carried for generic items, arrows, bolts, poisons and thrown weapons, and 1 for anything else. Any stack of more than one also has its count in front of its `Name`.
    // `Hand` is 0 for the right hand, 1 for two-handed and 2 for either hand. `Weight` is in tenths of a pound, for the whole stack. `Condition`, `Durability` and `Quality` are percentages.
    // `SpellNames` are the names of the spells it casts or procs.
    // `ItemType` is the server's equipment slot the item is made for (see `MoveItemPrimitive`). The packet doesn't carry it, but the client works it out from its own data, as a Human sees it from the item's icon (ADR 0006).
    // An item goes only in its own slot, except that a wrist item (33 or 34) goes on either wrist, a ring (35 or 36) on either finger, a right-hand weapon (10) in the right hand or both,
    // and a left-hand weapon (11) in either hand or both. A shield or an instrument goes by its `ObjectType` too, and arrows and bolts go in the quivers (14 to 17) by theirs alone.
    // Anything else, such as 0 or 40, goes in no equipment slot.
    public sealed record InventoryItem(
        string Name,
        int Level,
        int DpsAf,
        int SpeedAbs,
        int Count,
        int Hand,
        int DamageType,
        int ObjectType,
        int ItemType,
        int Weight,
        int Condition,
        int Durability,
        int Quality,
        int Bonus,
        int BonusLevel,
        int Model,
        int Color,
        int Effect,
        string[] SpellNames);

    // How much the Agent's Character carries and how much it can, in pounds. The server sends it whenever either changes. Above `Capacity` the Character slows down.
    public sealed record EncumbrancePercept(int Capacity, int Weight) : Percept;

    // How much money the Agent's Character has: 100 copper make a silver, 100 silver a gold, 1000 gold a mithril and 1000 mithril a platinum.
    public sealed record MoneyPercept(int Copper, int Silver, int Gold, int Mithril, int Platinum) : Percept;

    // A detail window, such as what examining an item shows: its caption and its text, line by line, as the Human reads them. Lines longer than 255 characters come split.
    public sealed record ExaminedPercept(string Caption, string[] Lines) : Percept;

    // The window of a merchant the Agent's Character talked to, with every item it sells, page by page. It doesn't say which merchant it is.
    public sealed record MerchantWindowPercept(eMerchantWindowType Window, MerchantItem[] Items) : Percept;

    // One item in a merchant's window. `Page` and `Place` are where it is: `buy` and `examineMerchantItem` take `Page * 30 + Place`.
    // `DpsAf`, `SpeedAbs`, `Hand`, `DamageType`, `ObjectType` and `ItemType` are as in an `InventoryItem`. `Count` is how many come in a pack, 1 unless the packet carries it.
    // `Weight` is in tenths of a pound, for the whole pack, and 0 for a thrown weapon. `Price` is in copper. `Usable` says whether the Character's class can use it.
    public sealed record MerchantItem(
        int Page,
        int Place,
        int Level,
        int DpsAf,
        int SpeedAbs,
        int Count,
        int Hand,
        int DamageType,
        int ObjectType,
        int ItemType,
        bool Usable,
        int Weight,
        long Price,
        int Model,
        string Name);

    // A Dialog the server opened, a question in a window: its text, and whether it asks yes or no (`YesNo`) or only says OK.
    // `Code`, `Data1`, `Data2` and `Data3` are what an `answerDialog` echoes back, as the client does. A newer Dialog takes its place.
    public sealed record DialogPercept(eDialogCode Code, int Data1, int Data2, int Data3, bool YesNo, string Text) : Percept;

    // The Agent's Character's stats window: the Constitution it has lost by dying, which a Healer restores, and the client shows in red, and its maximum health. The packet's other stats have no fields yet.
    public sealed record StatsPercept(int ConstitutionLost, int MaxHealth) : Percept;

    // Worked out by the Harness, as the Agent's client works it out: where the Character is while it runs to the target of a `moveTo`, sent with each position update. Positions are global.
    public sealed record MovedPercept(float X, float Y, float Z, int Heading, float Speed, float TargetX, float TargetY, float TargetZ) : Percept;

    // Worked out by the Harness: the route a `moveTo` runs along, from where the Character is to where it will stop, `within` already taken off. Positions are global.
    // `ReachesTarget` is false when the route can't get to the target, and the run then ends `stopped` with `Unreachable` at the last corner.
    public sealed record RoutedPercept(RouteCorner[] Corners, bool ReachesTarget) : Percept;

    // One corner of a route, global. `Swim` when the route swims from it on, across water on the surface; on the last corner, when the route ends in the water.
    public sealed record RouteCorner(float X, float Y, float Z, bool Swim);

    // Worked out by the Harness: a `moveTo` reached its target, and the Character stands there.
    public sealed record ArrivedPercept(float X, float Y, float Z) : Percept;

    // Worked out by the Harness: a run ended short of its target, where the Character stands now, and why.
    public sealed record StoppedPercept(float X, float Y, float Z, StopReason Reason) : Percept;

    // Why a run ended short of its target.
    public enum StopReason
    {
        // A `stop` Primitive.
        Stop,
        // A new `moveTo`.
        Replaced,
        // The server set the Character's position itself, with a jump.
        MovedByServer,
        // The server set the maximum speed to 0, for example while the Character is rooted.
        UnableToMove,
        // The route can't get to the target, and the Character got as close as it could.
        Unreachable
    }

    // A realm as the packets carry it, in two bits.
    public enum PacketRealm
    {
        None,
        Albion,
        Midgard,
        Hibernia
    }

    // A line for the chat window, or a popup: chat, system messages, combat text. The text is whole, with any speaker in it (`Vue says, "hello"`).
    // The 1.127 packet only marks popups, so `ChatLocation` is `CL_PopupWindow` or `CL_ChatWindow`. The client picks the window for the rest by `ChatType`.
    public sealed record MessagePercept(string Text, eChatType ChatType, eChatLoc ChatLocation) : Percept;
}
