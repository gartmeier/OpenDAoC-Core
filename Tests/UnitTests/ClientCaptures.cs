using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The real 1.127 client captures under `Fixtures/Agents/v1127`. See the README next to them for what they contain.
    /// </summary>
    public static class ClientCaptures
    {
        // Login, idle and quit sessions of the same Character.
        public static readonly string[] Names = ["login-idle-quit-1", "login-idle-quit-2"];

        // A Character already in the world saying one line.
        public const string SAY = "say-hello";

        // The same Character running, strafing, turning on the spot and stopping.
        public const string MOVEMENT = "run-turn-stop";

        // A Character logging in next to an Agent already in the world, and asking for its create.
        public const string LOGIN_NEXT_TO_AGENT = "login-next-to-agent";

        // A Character targeting a Mob, attacking it until it dies, sitting and standing.
        public const string FIGHT = "target-attack-sit";

        // A Character sitting and standing with the sit key.
        public const string SIT_KEY = "sit-key";

        // A Character logging in at Connla to trade with merchants, equip, examine, drop and pick up items.
        public const string ITEMS = "items-merchants";

        // A Blademaster fighting Mobs with styles from the quickbar.
        public const string STYLES = "styles";

        // A Character swimming across a river, swimming and turning about, and stopping, sitting and standing in it.
        public const string SWIM_STRAIGHT = "swim-straight";
        public const string SWIM_TURN = "swim-turn";
        public const string SWIM_STOP_SIT_STAND = "swim-stop-sit-stand";

        // A Character that lost Constitution declining, then accepting, a Healer's Dialog.
        public const string HEALER_DIALOG = "healer-dialog";

        // A Character using its Recall Stone from the backpack and the quickbar, standing, moving and on the reuse timer.
        public const string USE_ITEM = "use-item";

        // A Naturalist casting a heal on itself, then a heal and a buff on another and on itself, and cancelling buffs from the concentration window.
        public const string CAST_HEAL = "cast-heal";
        public const string CAST_BUFFS = "cast-buffs";

        // A Magician casting a buff on itself, then killing a Mob with a damage spell.
        public const string CAST_DAMAGE = "cast-damage";

        // A Mentalist clicking Quick Cast, mostly before a spell.
        public const string QUICK_CAST = "quick-cast";

        // A Druid summoning its pet and clicking each button in the pet window once.
        public const string PET_WINDOW = "pet-window";

        // An Eldritch inviting a Druid into a Group and using `/assist`, clicking on the Druid or a Mob between.
        public const string ASSIST = "assist";

        public static readonly string[] All = [.. Names, SAY, MOVEMENT, LOGIN_NEXT_TO_AGENT, FIGHT, SIT_KEY, ITEMS, STYLES, SWIM_STRAIGHT, SWIM_TURN, SWIM_STOP_SIT_STAND, HEALER_DIALOG, USE_ITEM, CAST_HEAL, CAST_BUFFS, CAST_DAMAGE, QUICK_CAST, PET_WINDOW, ASSIST];

        public static CapturedPacket[] Load(string capture)
        {
            return CapturedPacket.ParseLines(File.ReadLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Agents", "v1127", $"{capture}.tsv")));
        }

        // Every TCP packet with this code that arrived while the client was Playing.
        public static CapturedPacket[] WhilePlaying(string capture, eClientPackets code)
        {
            return Load(capture).Where(p => p.Transport is CaptureTransport.Tcp && p.ClientState is GameClient.eClientState.Playing && p.Code == (byte) code).ToArray();
        }

        // Data offsets 12-15 of a position update, little-endian. 0 while standing or strafing.
        public static float Speed(CapturedPacket positionUpdate)
        {
            return BinaryPrimitives.ReadSingleLittleEndian(positionUpdate.Data.AsSpan(12));
        }
    }
}
