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
    /// </summary>
    internal sealed class WorldEntry
    {
        private readonly Step[] _steps;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _nextStep;

        public Task Completion => _completion.Task;

        public WorldEntry(DbCoreCharacter character)
        {
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

        // Called on every client tick before the queued packets are processed. Returns true once the Character is in the world.
        public bool Tick(HarnessClient client)
        {
            if (_nextStep < _steps.Length)
            {
                Step step = _steps[_nextStep];

                if (step.IsReady(client))
                {
                    foreach (ClientPacket packet in step.Packets(client))
                        client.Enqueue(packet);

                    _nextStep++;
                }

                return false;
            }

            if (!client.IsInWorld)
                return false;

            _completion.TrySetResult();
            return true;
        }

        private sealed record Step(Func<HarnessClient, bool> IsReady, Func<HarnessClient, ClientPacket[]> Packets);
    }
}
