using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DOL.GS.PacketHandler;

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

    // The Character is in the world.
    public sealed record EnteredMessage : ServerMessage;

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
    public abstract record Primitive;

    // Types `/quit`, as a Human does. The server's quit path takes it from there, quit delay included.
    public sealed record QuitPrimitive : Primitive;

    // Says `Text` to those nearby, as a Human typing a chat line does.
    public sealed record SayPrimitive(string Text) : Primitive;

    // Runs in a straight line to the point, in global coordinates, at the maximum speed the server last told the client. A new one replaces the run in progress.
    // Arriving, and stopping short, come as Percepts (ADR 0004).
    public sealed record MoveToPrimitive([property: JsonRequired] float X, [property: JsonRequired] float Y, [property: JsonRequired] float Z) : Primitive;

    // Ends the run in progress where the Character is. Without one it does nothing.
    public sealed record StopPrimitive : Primitive;

    // Turns on the spot to `Heading`, 0 to 4095, at once. Heading 0 points towards +Y and grows clockwise, as the server measures headings.
    // During a run it does nothing: the run's own heading overtakes it, and the run goes on.
    public sealed record FacePrimitive([property: JsonRequired] int Heading) : Primitive;

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
    [JsonDerivedType(typeof(ObjectUpdatePercept), "objectUpdate")]
    [JsonDerivedType(typeof(ObjectDeletePercept), "objectDelete")]
    [JsonDerivedType(typeof(ObjectRemovePercept), "objectRemove")]
    [JsonDerivedType(typeof(OwnPositionPercept), "ownPosition")]
    [JsonDerivedType(typeof(JumpPercept), "jump")]
    [JsonDerivedType(typeof(MaxSpeedPercept), "maxSpeed")]
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
        int ManaPercent,
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

    // Worked out by the Harness, as the Agent's client works it out: where the Character is while it runs to the target of a `moveTo`, sent with each position update. Positions are global.
    public sealed record MovedPercept(float X, float Y, float Z, int Heading, float Speed, float TargetX, float TargetY, float TargetZ) : Percept;

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
        UnableToMove
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
