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
    public abstract record Primitive;

    // Types `/quit`, as a Human does. The server's quit path takes it from there, quit delay included.
    public sealed record QuitPrimitive : Primitive;

    // Says `Text` to those nearby, as a Human typing a chat line does.
    public sealed record SayPrimitive(string Text) : Primitive;

    /// <summary>
    /// One thing the Harness tells the Mind: only what the 1.127 packet would tell a real client (ADR 0001).
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(BarePercept), "bare")]
    [JsonDerivedType(typeof(MessagePercept), "message")]
    public abstract record Percept;

    // A packet-library call without a projection yet. Only its name.
    public sealed record BarePercept(string Call) : Percept;

    // A line for the chat window, or a popup: chat, system messages, combat text. The text is whole, with any speaker in it (`Vue says, "hello"`).
    // The 1.127 packet only marks popups, so `ChatLocation` is `CL_PopupWindow` or `CL_ChatWindow`. The client picks the window for the rest by `ChatType`.
    public sealed record MessagePercept(string Text, eChatType ChatType, eChatLoc ChatLocation) : Percept;
}
