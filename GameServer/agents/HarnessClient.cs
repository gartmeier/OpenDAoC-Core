using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Stands in for the game client of an Agent's Character: a client without a socket.
    /// Inbound packets come from a queue instead of the network and go through the normal packet processor, at the same point in the client tick as a Human's.
    /// Outbound packets go to <see cref="HarnessPacketLib"/>, which records them in the Agent's trace and tells the Agent's Mind.
    /// </summary>
    public sealed class HarnessClient : GameClient, IMovingClient
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // In milliseconds. The longest quit delay the server applies (`GamePlayer.QuitTimer`), with a margin. Combat during the delay starts it over, so a quit can still take longer.
        public const int QUIT_TIME_LIMIT = 90000;

        // A real client's version check and login request are packets 1 and 2. The Harness skips both.
        private const ushort FIRST_SEQUENCE = 3;

        // Headings are the low 12 bits of a heading update, 4096 to a full turn.
        private const int MAX_HEADING = 4095;

        // Object IDs are 16 bits in every packet that carries one.
        private const int MAX_OBJECT_ID = ushort.MaxValue;

        // The Character's own slots, which a move goes between: the ground, its equipment, its backpack and its horse's bags (`eInventorySlot`).
        private const int MIN_OWN_SLOT = (int) eInventorySlot.Ground;
        private const int MAX_OWN_SLOT = (int) eInventorySlot.RightRearSaddleBag;

        private readonly ConcurrentQueue<ClientPacket> _inbound = new();
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ushort _sequence = FIRST_SEQUENCE;
        private WorldEntry _worldEntry;
        private Liveness _liveness;
        private WorldExit _worldExit;
        private readonly HarnessMovement _movement;
        private PlayerCreateRequests _playerCreateRequests = new();
        // The heading of a heading update still in the inbound queue, which the server hasn't set on the Character yet.
        private ushort? _queuedHeading;
        // What the client has selected, 0 for nothing, and whether it's in attack mode.
        private ushort _targetId;
        private bool _attacking;
        // The skill list the server last sent, which the client's icons stand for and the server reads their indexes against.
        private SkillEntry[] _skills = [];
        // The Dialog the client shows, until it answers it. The server keeps no copy of its text to send a new Mind.
        private DialogPercept _openDialog;
        private AgentTrace _trace;
        private bool _mindLost;

        public HarnessClient(MindLink mind) : base(null)
        {
            Mind = mind;
            _movement = new(this, PathfindingProvider.Instance);
        }

        // The Agent's Mind connection, which is the client's network connection (ADR 0005). Told every call the server makes on the packet library.
        // Changes only on the game loop, when another Mind takes over.
        public MindLink Mind { get; private set; }

        // Playing, with the Character added to the world.
        public bool IsInWorld => ClientState is eClientState.Playing && Player?.ObjectState is GameObject.eObjectState.Active;

        // Completes once the server has disconnected the client, its Character saved and removed and its session released.
        public Task Disconnected => _disconnected.Task;

        // Where this login's trace is written, once the client has started entering the world.
        public string TracePath => _trace?.FilePath;

        // The Character it's entering the world as, known before the server has loaded it.
        public string CharacterName { get; private set; }

        /// <summary>
        /// Connects as the Account's client, already past the network handshake, version check and password check, and enters the world as the Character.
        /// Completes once the Character is in the world. Throws a <see cref="WorldEntryFailedException"/> if it doesn't get there within <see cref="WorldEntry.TIME_LIMIT"/>, or the server disconnects the client first.
        /// After a time limit failure the client disconnects itself once the server isn't loading its Character any more, so <see cref="Disconnected"/> completes either way.
        /// </summary>
        public Task EnterWorld(DbAccount account, DbCoreCharacter character)
        {
            Account = account;
            CharacterName = character.Name;
            Version = eClientVersion.Version1127;
            // What the real 1.127 client reports in its version and crypt-key request, which the Harness skips.
            ClientType = eClientType.LabyrinthOfTheMinotaur;
            ClientAddons = eClientAddons.bit4 | eClientAddons.NewNewFrontiers;
            _trace = AgentTrace.Start(character.Name);
            Out = HarnessPacketLib.Create(this, _trace);
            PacketProcessor = new(this);
            ClientState = eClientState.Connecting;
            _worldEntry = new(character, GameLoop.GameLoopTime);
            GameEventMgr.AddHandler(this, GameClientEvent.Disconnected, OnDisconnected);
            GameServer.Instance.Connect(this);
            return _worldEntry.Completion;
        }

        /// <summary>
        /// Types `/quit`. The server's quit path takes it from there, quit delay included, and ends with `OnPlayerQuit`.
        /// Completes once the server has disconnected the client. Throws a <see cref="TimeoutException"/> if it hasn't within <see cref="QUIT_TIME_LIMIT"/>, for example because the server refused the quit.
        /// Nothing is undone then: a refused quit leaves the Character in the world, and a quit delay still running ends in a quit. The trace shows which.
        /// </summary>
        public Task Quit()
        {
            Enqueue(ClientPackets1127.Quit());
            return Disconnected.WaitAsync(TimeSpan.FromMilliseconds(QUIT_TIME_LIMIT));
        }

        /// <summary>
        /// Connects the client to a new Mind in place of its live one, without a logout: the Character stays where it is, and liveness carries on through the new connection.
        /// The old Mind is told it was replaced, and its connection closes. The new one hears `entered` straight away if the Character is in the world, or once world entry completes.
        /// Returns why the new Mind can't take over, or null once it has. A client whose Mind is lost, which goes or has gone linkdead, or which is leaving the world, can't be taken over: it's gone soon, and the Character can log in again then.
        /// Called on the game loop.
        /// </summary>
        public string TakeOver(MindLink mind)
        {
            string name = Player?.Name ?? CharacterName;

            if (!Mind.IsConnected || _mindLost || ClientState is eClientState.Linkdead || Player?.IsLinkDeathTimerRunning is true)
                return $"{name} is linkdead: its last Mind's connection was lost. The server removes the Character once its link-death window has run out, and a new Mind can log in after that.";

            if (_worldExit != null || ClientState is eClientState.Disconnected)
                return $"{name} is leaving the world. A new Mind can log in once the server has disconnected it.";

            MindLink old = Mind;
            Mind = mind;
            old.Replace();

            // Before any call the server makes from here on, so the new Mind hears it in order.
            if (_worldEntry == null && IsInWorld)
            {
                mind.Send(new EnteredMessage(TookOver: true));
                TellAnew();
            }

            return null;
        }

        // The new Mind knows nothing of what the old one was told, though a real client never forgets. So the server tells the client again what it tells one entering the world (`WorldInitRequestHandler`), with the status and encumbrance it sends just after.
        // The client also forgets which NPCs, items and doors the server sent it, so that the server creates them anew, and which players it knows, so that it asks for them again.
        // A Dialog it hasn't answered is told again last.
        private void TellAnew()
        {
            GamePlayer player = Player;
            IPacketLib packetLib = player.Out;
            packetLib.SendPlayerPositionAndObjectID();
            packetLib.SendUpdateMaxSpeed();
            packetLib.SendInventoryItemsUpdate(eInventoryWindowType.Equipment, player.Inventory.EquippedItems);
            packetLib.SendInventoryItemsUpdate(eInventoryWindowType.Inventory, player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBagHorse));
            packetLib.SendUpdatePlayerSkills(false);
            packetLib.SendUpdatePlayer();
            packetLib.SendUpdateMoney();
            packetLib.SendCharStatsUpdate();
            // Every Effect, as for a client that has shown none.
            int effectsCount = 0;
            packetLib.SendUpdateIcons(ref effectsCount, true);
            packetLib.SendUpdatePoints();
            packetLib.SendStatusUpdate();
            packetLib.SendEncumbrance();

            PlayerObjectCache cache = player.PlayerObjectCache;

            // In the order `ClientService.UpdateWorld` takes them, one at a time.
            lock (cache.NpcUpdateCacheLock)
                cache.NpcUpdateCache.Clear();

            lock (cache.ItemUpdateCacheLock)
                cache.ItemUpdateCache.Clear();

            lock (cache.DoorUpdateCacheLock)
                cache.DoorUpdateCache.Clear();

            lock (cache.HouseUpdateCacheLock)
                cache.HouseUpdateCache.Clear();

            _playerCreateRequests = new();

            // The window the client still shows, which the server can't send again.
            if (_openDialog != null)
                Mind.Perceive(_openDialog);
        }

        /// <summary>
        /// Queues the packet a real client sends for the Primitive, for the next client tick (ADR 0002). `from` is the Mind that sent it. Called on the game loop.
        /// Returns why it wasn't queued, or null if it was. Whether the server then accepts it isn't known here (ADR 0004).
        /// </summary>
        public string Dispatch(Primitive primitive, MindLink from)
        {
            // A Mind can send Primitives before it hears that it was replaced.
            if (from != Mind)
                return $"Another Mind has taken over {CharacterName}.";

            if (!IsInWorld)
                return $"{CharacterName} isn't in the world.";

            // Movement is the client's own: it sends the position updates while it runs.
            switch (primitive)
            {
                case MoveToPrimitive moveTo:
                {
                    _movement.MoveTo(new(moveTo.X, moveTo.Y, moveTo.Z), moveTo.Within, GameLoop.GameLoopTime);
                    return null;
                }
                case StopPrimitive:
                {
                    if (_movement.Stop(GameLoop.GameLoopTime))
                        _liveness?.OnPositionUpdate(GameLoop.GameLoopTime);

                    return null;
                }
                case FacePrimitive face:
                {
                    if (face.Heading is < 0 or > MAX_HEADING)
                        return $"A heading is 0 to {MAX_HEADING}, not {face.Heading}.";

                    // A running client sends no heading update, as captured: the run's own heading, in its next position update, overtakes the face at once.
                    if (_movement.IsRunning)
                        return null;

                    // One heading update with the final heading, as a client turned by the mouse can send: the server and other clients see only the heading each one carries.
                    // Position updates keep their idle cadence, as while the captured client turns.
                    _queuedHeading = (ushort) face.Heading;
                    Enqueue(HeadingUpdate(_queuedHeading.Value));
                    return null;
                }
            }

            int styleIndex = 0;

            switch (primitive)
            {
                case UseStylePrimitive useStyle:
                {
                    if (StyleIndex(useStyle.Style, useStyle.Line, out styleIndex) is string reason)
                        return reason;

                    break;
                }
                case TargetPrimitive target:
                {
                    if (target.ObjectId is < 0 or > MAX_OBJECT_ID)
                        return $"An object ID is 0 to {MAX_OBJECT_ID}, not {target.ObjectId}.";

                    _targetId = (ushort) target.ObjectId;
                    break;
                }
                case AttackPrimitive attack:
                {
                    _attacking = attack.On;
                    break;
                }
                case CommandPrimitive command:
                {
                    if (command.Text is not ['/', ..])
                        return $"A command starts with /, as a Human types it, not \"{command.Text}\".";

                    break;
                }
                case MoveItemPrimitive moveItem:
                {
                    // Not the paper doll (100), where the server would choose the slot from the item, and not above 1000, which gives the item to another object.
                    if (moveItem.From is < MIN_OWN_SLOT or > MAX_OWN_SLOT || moveItem.To is < MIN_OWN_SLOT or > MAX_OWN_SLOT)
                        return $"A move is between the Character's own slots, {MIN_OWN_SLOT} to {MAX_OWN_SLOT}, not {moveItem.From} to {moveItem.To}.";

                    break;
                }
                case ExaminePrimitive examine:
                {
                    if (BadSlot(examine.Slot) is string reason)
                        return reason;

                    break;
                }
                case SellPrimitive sell:
                {
                    if (BadSlot(sell.Slot) is string reason)
                        return reason;

                    break;
                }
                case UseItemPrimitive useItem:
                {
                    // The use slot packet's slot is a single byte. Whether there's anything to use in it, the server says.
                    if (useItem.Slot is < 0 or > byte.MaxValue)
                        return $"A slot to use is 0 to {byte.MaxValue}, not {useItem.Slot}.";

                    break;
                }
                case ExamineMerchantItemPrimitive examine:
                {
                    if (BadIndex(examine.Index) is string reason)
                        return reason;

                    break;
                }
                case BuyPrimitive buy:
                {
                    if (BadIndex(buy.Index) is string reason)
                        return reason;

                    if (buy.Count is < 1 or > byte.MaxValue)
                        return $"A count is 1 to {byte.MaxValue}, not {buy.Count}.";

                    break;
                }
                case PickUpPrimitive pickUp:
                {
                    if (TargetFirst(pickUp.ObjectId) is string reason)
                        return reason;

                    break;
                }
                case TalkToPrimitive talkTo:
                {
                    if (TargetFirst(talkTo.ObjectId) is string reason)
                        return reason;

                    break;
                }
                case AnswerDialogPrimitive answer:
                {
                    if ((BadDialogData(answer.Data1) ?? BadDialogData(answer.Data2) ?? BadDialogData(answer.Data3)) is string reason)
                        return reason;

                    // The client closes the window on the click.
                    _openDialog = null;
                    break;
                }
            }

            GamePlayer player = Player;
            ClientPacket packet = primitive switch
            {
                QuitPrimitive => ClientPackets1127.Quit(),
                SayPrimitive say => ClientPackets1127.Say(say.Text),
                TargetPrimitive { ObjectId: 0 } => ClientPackets1127.ClearTarget(_attacking),
                TargetPrimitive target => ClientPackets1127.Target((ushort) target.ObjectId),
                AttackPrimitive attack => ClientPackets1127.AttackRequest(attack.On),
                SitPrimitive => ClientPackets1127.Sit(),
                StandPrimitive => ClientPackets1127.Stand(),
                // The client sends `&` in place of the `/` the Human typed.
                CommandPrimitive command => ClientPackets1127.Command($"&{command.Text[1..]}"),
                MoveItemPrimitive moveItem => ClientPackets1127.MoveItem(SessionID, (ushort) moveItem.From, (ushort) moveItem.To, 0),
                ExaminePrimitive examine => ClientPackets1127.Examine((ushort) examine.Slot),
                ExamineMerchantItemPrimitive examine => ClientPackets1127.ExamineMerchantItem((ushort) examine.Index),
                PickUpPrimitive => ClientPackets1127.PickUpRequest(),
                TalkToPrimitive talkTo => ClientPackets1127.InteractRequest((uint) player.X, (uint) player.Y, SessionID, (ushort) talkTo.ObjectId),
                BuyPrimitive buy => ClientPackets1127.BuyRequest((uint) player.X, (uint) player.Y, SessionID, (ushort) buy.Index, (byte) buy.Count),
                SellPrimitive sell => ClientPackets1127.SellRequest((uint) player.X, (uint) player.Y, SessionID, (ushort) sell.Slot),
                AnswerDialogPrimitive answer => ClientPackets1127.DialogResponse((byte) answer.Code, (ushort) answer.Data1, (ushort) answer.Data2, (ushort) answer.Data3, answer.Accept),
                // Where the server has the Character, so the position it takes from the packet moves it nowhere.
                UseStylePrimitive => ClientPackets1127.UseSkill(new Vector3(player.X, player.Y, player.Z), player.CurrentSpeed, _queuedHeading ?? player.Heading, SkillFlags, (byte) styleIndex),
                // As a right-click in the backpack, never the quickbar's click, which on a weapon or horse slot means something else.
                UseItemPrimitive useItem => ClientPackets1127.UseSlot(new Vector3(player.X, player.Y, player.Z), player.CurrentSpeed, _queuedHeading ?? player.Heading, SkillFlags, (byte) useItem.Slot,
                    useItem.Second is true ? eUseType.Use2 : eUseType.Use1),
                _ => null
            };

            if (packet == null)
                return $"The Harness has no Primitive {primitive?.GetType().Name ?? "null"}.";

            Enqueue(packet);
            return null;
        }

        // Slots are 16 bits in every packet that carries one.
        private static string BadSlot(int slot)
        {
            return slot is < 0 or > ushort.MaxValue ? $"A slot is 0 to {ushort.MaxValue}, not {slot}." : null;
        }

        // A merchant window's index is 16 bits in the detail and buy requests.
        private static string BadIndex(int index)
        {
            return index is < 0 or > ushort.MaxValue ? $"An index is 0 to {ushort.MaxValue}, not {index}." : null;
        }

        // A Dialog's data are 16 bits in the dialog response.
        private static string BadDialogData(int data)
        {
            return data is < 0 or > ushort.MaxValue ? $"A Dialog's data is 0 to {ushort.MaxValue}, not {data}." : null;
        }

        // The style's index as the skill handler reads it: its place in the skill list the server last sent, counting from the first entry after the specializations.
        // Only styles match. Without `line`, the first of that name does, as a Human clicks the first icon they see.
        private string StyleIndex(string name, string line, out int index)
        {
            int found = Array.FindIndex(_skills, skill => skill.Page is SkillPage.Styles && skill.Name == name && (line == null || skill.Line == line));
            index = 0;

            if (found < 0)
                return line == null ? $"{Player.Name} has no Style \"{name}\"." : $"{Player.Name} has no Style \"{name}\" in {line}.";

            // The server's skills packet stops at the 255th entry, so the client never got an icon for one past it.
            if (found >= byte.MaxValue)
                return $"\"{name}\" is past the first {byte.MaxValue} skills the server sends a client.";

            index = found - Math.Max(0, Array.FindIndex(_skills, static skill => skill.Page is not SkillPage.Specialization));
            return null;
        }

        // The skill packet's target in view bits (see `UseSkillHandler`).
        private const ushort SKILL_TARGET_IN_VIEW = 0xA000;

        // The skill and use slot packets' flags: the target in view exactly while something is selected, as in the position updates (see `ActionFlags`), and never strafing.
        private ushort SkillFlags => _targetId == 0 ? (ushort) 0 : SKILL_TARGET_IN_VIEW;

        // The server picks up and talks to what the client targets, so a click on the object selects it first, unless it's selected already.
        private string TargetFirst(int objectId)
        {
            if (objectId is < 1 or > MAX_OBJECT_ID)
                return $"An object ID is 1 to {MAX_OBJECT_ID}, not {objectId}.";

            if (_targetId != objectId)
            {
                _targetId = (ushort) objectId;
                Enqueue(ClientPackets1127.Target(_targetId));
            }

            return null;
        }

        // The server told the client its Character has quit. Called on the game loop, before the server has finished removing and saving the Character.
        public void OnPlayerQuit(bool totalOut)
        {
            _worldExit ??= new(totalOut, Player?.Realm ?? eRealm.None);
        }

        // What the client does with what the server told it. Called on the game loop, after the Mind was told.
        public void OnPerceived(Percept percept)
        {
            if (_playerCreateRequests.OnPerceived(percept, GameLoop.GameLoopTime) is ushort sessionId)
                Enqueue(ClientPackets1127.CreatePlayerRequest(sessionId));

            switch (percept)
            {
                case SkillsPercept skills:
                {
                    _skills = skills.Skills;
                    break;
                }
                // The client shows the window until it's clicked, and a newer one in its place.
                case DialogPercept dialog:
                {
                    _openDialog = dialog;
                    break;
                }
                case MaxSpeedPercept maxSpeed:
                {
                    if (_movement.OnMaxSpeed(maxSpeed, GameLoop.GameLoopTime))
                        _liveness?.OnPositionUpdate(GameLoop.GameLoopTime);

                    break;
                }
                case JumpPercept jump:
                {
                    // The server set the Character's heading too, and the client takes it: a `face` still queued would turn the Character back.
                    Unqueue((byte) eClientPackets.PlayerHeadingUpdate);
                    _queuedHeading = null;
                    _movement.OnJump(jump);
                    break;
                }
                // The client forgets what it can't see any more, and its selection with it. When it was attacking it, as when it dies, the captured client also stops attacking and clears its target itself.
                case ObjectDeletePercept delete when delete.ObjectId == _targetId:
                case ObjectRemovePercept remove when remove.ObjectId == _targetId:
                {
                    _targetId = 0;

                    if (_attacking)
                    {
                        Enqueue(ClientPackets1127.AttackRequest(false, byClient: true));
                        Enqueue(ClientPackets1127.ClearTarget(attacking: true));
                        _attacking = false;
                    }

                    break;
                }
            }
        }

        // What the client has selected, 0 for nothing. Its position and heading updates carry it.
        public ushort TargetId => _targetId;

        // The health percentage the client's position and heading updates carry, with the top bit set while it's in attack mode, as captured.
        public byte HealthByte => (byte) (Player.HealthPercent | (_attacking ? 0x80 : 0));

        // The flags the client's position and heading updates carry: as the server last read them, but with the target in view exactly while something is selected.
        // The server reads whether the target is in view from every update, and the Harness claims it always is (see `ClientPackets1127.Target`).
        public PlayerPositionUpdateHandler.ActionFlags ActionFlags
        {
            get
            {
                PlayerPositionUpdateHandler.ActionFlags flags = Player.ActionFlags & ~PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;
                return _targetId == 0 ? flags : flags | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;
            }
        }

        public void Enqueue(ClientPacket packet)
        {
            _inbound.Enqueue(packet);
        }

        // Called on the game loop, like everything that queues a packet, so nothing is queued while it goes through the queue.
        public void Unqueue(byte code)
        {
            for (int count = _inbound.Count; count > 0 && _inbound.TryDequeue(out ClientPacket packet); count--)
            {
                if (packet.Code != code)
                    _inbound.Enqueue(packet);
            }
        }

        void IMovingClient.Perceive(Percept percept)
        {
            Mind.Perceive(percept);
        }

        // Stamped with a millisecond tick counter, like a real client's.
        public static ClientPacket Ping()
        {
            return ClientPackets1127.Ping((uint) Environment.TickCount);
        }

        // What a client standing still tells the server about its Character. It faces the way it last turned to, even if the server hasn't read that heading update yet.
        // Standing in the water it swims, and sitting there it only sits, as the captured client does.
        public ClientPacket PositionUpdate()
        {
            GamePlayer player = Player;
            PlayerPositionUpdateHandler.StateFlags stateFlags = player.IsSitting ? PlayerPositionUpdateHandler.StateFlags.SITTING : _movement.StateFlags;
            return ClientPackets1127.PositionUpdate(new Vector3(player.X, player.Y, player.Z), player.CurrentZone.ID, _queuedHeading ?? player.Heading, 0, HealthByte, SessionID, stateFlags, ActionFlags, _targetId);
        }

        // What a client turning on the spot tells the server, once it faces `heading` (0 to 4095).
        public ClientPacket HeadingUpdate(ushort heading)
        {
            return ClientPackets1127.HeadingUpdate(heading, HealthByte, SessionID, ActionFlags, _targetId);
        }

        // Called by the client service on the game loop, where a real client's received bytes are processed.
        public override void Receive()
        {
            // Where a real client's closed connection is noticed.
            if (_worldExit?.Tick(this) is true)
            {
                _worldExit = null;
                Disconnect();
                return;
            }

            // A lost Mind is a closed connection (ADR 0005). While Playing, the server takes that as a link death.
            if (!Mind.IsConnected && !_mindLost)
            {
                _mindLost = true;

                if (_worldEntry == null)
                {
                    Disconnect();
                    return;
                }

                // World entry gives up the connection itself, once the server isn't loading the Character any more.
                _worldEntry.Fail("before its Mind disconnected");
            }

            switch (_worldEntry?.Tick(this, GameLoop.GameLoopTime))
            {
                case WorldEntry.Progress.Entered:
                {
                    _worldEntry = null;
                    _liveness = new(GameLoop.GameLoopTime);
                    // Before any call the server makes from here on, so the Mind hears it in order.
                    Mind.Send(new EnteredMessage(TookOver: false));
                    break;
                }
                case WorldEntry.Progress.GaveUp:
                {
                    // A real client giving up closes its connection. Short of Playing, the server takes that as a disconnect. World entry only gives up while Playing if the Character isn't in the world, and then a link death comes first.
                    Mind.Refuse(WorldEntryFailure());
                    _worldEntry = null;
                    Disconnect();
                    return;
                }
            }

            // Only while a Mind is connected, as only a running client keeps its connection alive and moves its Character (ADR 0005).
            // A running Character's position updates stand in for the idle ones.
            if (_liveness != null && !_mindLost)
            {
                if (IsInWorld && _movement.Tick(GameLoop.GameLoopTime))
                    _liveness.OnPositionUpdate(GameLoop.GameLoopTime);

                StayAlive(_liveness.Tick(GameLoop.GameLoopTime));
            }

            while (_inbound.TryDequeue(out ClientPacket clientPacket))
            {
                byte[] bytes = clientPacket.ToWire(_sequence++, SessionID);
                GSPacketIn packet = PooledObjectFactory.GetForTick<GSPacketIn>().Init();
                packet.Load(bytes, 0, bytes.Length);

                try
                {
                    PacketProcessor.ProcessInboundPacket(packet);
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error(e);
                }
                finally
                {
                    packet.ReleasePooledObject();
                }
            }

            // The server has read every heading update, so the Character's heading is the client's again.
            _queuedHeading = null;
        }

        // What an idle client sends so the server doesn't time it out or link-dead it. A position update repeats where the Character is, so it needs the Character in the world.
        private void StayAlive(Liveness.Due due)
        {
            if ((due & Liveness.Due.Ping) != 0)
                Enqueue(Ping());

            if ((due & Liveness.Due.PositionUpdate) != 0 && IsInWorld)
                Enqueue(PositionUpdate());
        }

        // Everything the server sends went to `Out` instead of being queued for a socket.
        public override void SendPendingPackets() { }

        protected override void CloseSocket() { }

        private void OnDisconnected(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveAllHandlersForObject(this);
            // For example when the Character couldn't be added to the world.
            if (_worldEntry != null)
            {
                _worldEntry.Fail("before the server disconnected its Harness client");
                Mind.Refuse(WorldEntryFailure());
            }

            // A normal close: the Agent has left the world. However the client disconnected, `/agent logout` included, the Mind's connection ends here.
            Mind.Close();
            _trace.Dispose();
            _disconnected.TrySetResult();
        }

        // Why world entry failed, once it has.
        private string WorldEntryFailure()
        {
            return _worldEntry.Completion.Exception?.InnerException?.Message;
        }
    }
}
