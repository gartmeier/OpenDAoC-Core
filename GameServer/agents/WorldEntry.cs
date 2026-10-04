using System;
using System.Threading.Tasks;
using DOL.Database;
using static DOL.GS.GameClient;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Replays what a real 1.127 client sends from character select until it's in the world, as captured in `Tests/Fixtures/Agents/v1127` (see the README there).
    /// The client sends in bursts, each in reaction to what the server told it. The Harness instead holds each burst back until the client state shows the server is ready for it.
    /// Left out: packets without a handler (0x1D, 0x42, 0x43) and the create object request, which reacts to a specific object the server sent.
    /// World entry fails if it doesn't complete within <see cref="TIME_LIMIT"/>, so that a stuck login doesn't look like one still in progress.
    /// The client then gives up its connection, but not while the server is still loading its Character: the load carries on regardless and would add the Character to the world of a disconnected client.
    /// </summary>
    internal sealed class WorldEntry
    {
        // In milliseconds. A real world entry takes about a second, most of it the asynchronous character load.
        public const int TIME_LIMIT = 30000;

        private readonly string _characterName;
        private readonly Step[] _steps;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly long _deadline;
        private int _nextStep;

        public enum Progress
        {
            Entering,
            Entered,
            // The client should close its connection.
            GaveUp
        }

        // Throws a `WorldEntryFailedException` if the Character doesn't reach the world.
        public Task Completion => _completion.Task;

        public WorldEntry(DbCoreCharacter character, long now)
        {
            _characterName = character.Name;
            _deadline = now + TIME_LIMIT;
            byte characterIndex = ClientPackets1127.CharacterIndex(character.AccountSlot);
            eRealm realm = (eRealm) character.Realm;

            _steps =
            [
                // At the character screen: choose the Character and enter the world. The server loads the Character asynchronously.
                new(static _ => true, _ =>
                [
                    ClientPackets1127.CharacterSelect(0x00),
                    ClientPackets1127.CharacterOverview(realm),
                    ClientPackets1127.RegionList(characterIndex),
                    HarnessClient.Ping(),
                    ClientPackets1127.CharacterSelect(characterIndex),
                    ClientPackets1127.WorldInit(characterIndex)
                ]),
                // The Character was loaded and added to the world. The position update arrives too early to be used, as a real client's does.
                new(static client => client.ClientState is eClientState.WorldEnter && client.Player?.ObjectState is GameObject.eObjectState.Active, static client =>
                [
                    ClientPackets1127.PlayerInit(),
                    HarnessClient.Ping(),
                    client.PositionUpdate(),
                    ClientPackets1127.GameOpen()
                ]),
                // Game open made the client Playing. The client asks for guild info on its own.
                new(static client => client.ClientState is eClientState.Playing, static client =>
                [
                    ClientPackets1127.ClientFilterSettings(initial: true),
                    ClientPackets1127.Command("&gc info 1"),
                    ClientPackets1127.ClientFilterSettings(initial: false),
                    ClientPackets1127.ClientFilterSettings(initial: false),
                    ClientPackets1127.ShowWarmapRequest(),
                    client.PositionUpdate()
                ])
            ];
        }

        // Called on every client tick before the queued packets are processed.
        public Progress Tick(HarnessClient client, long now)
        {
            if (_completion.Task.IsFaulted)
                return IsLoadingCharacter(client) ? Progress.Entering : Progress.GaveUp;

            if (_nextStep == _steps.Length && client.IsInWorld)
            {
                _completion.TrySetResult();
                return Progress.Entered;
            }

            // A Character already in the world only waits for the last burst.
            if (now >= _deadline && !client.IsInWorld)
            {
                Fail($"within {TIME_LIMIT / 1000} s (client {client.ClientState}, Character {client.Player?.ObjectState.ToString() ?? "not loaded"})");
                return Tick(client, now);
            }

            if (_nextStep < _steps.Length)
            {
                Step step = _steps[_nextStep];

                if (step.IsReady(client))
                {
                    foreach (ClientPacket packet in step.Packets(client))
                        client.Enqueue(packet);

                    _nextStep++;
                }
            }

            return Progress.Entering;
        }

        // `reason` completes "<Character> didn't reach the world ...".
        public void Fail(string reason)
        {
            _completion.TrySetException(new WorldEntryFailedException($"{_characterName} didn't reach the world {reason}."));
        }

        // World init started loading the Character (`GameClient.LoadPlayer` sets the index first), and hasn't yet moved the client on to WorldEnter.
        // A load that never ends leaves the client at the character screen until the server's ping timeout disconnects it.
        private static bool IsLoadingCharacter(HarnessClient client)
        {
            return client.ClientState is eClientState.CharScreen && client.ActiveCharIndex >= 0;
        }

        private sealed record Step(Func<HarnessClient, bool> IsReady, Func<HarnessClient, ClientPacket[]> Packets);
    }
}
