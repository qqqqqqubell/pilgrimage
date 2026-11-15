using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Pilgrimage
{
    // Pure host authority. Transport-provided connection IDs are the only identity input.
    // No client can submit an actor index, resource value, deck, random roll or game state.
    public sealed class LanRoom
    {
        public sealed class Member
        {
            public ulong Connection { get; internal set; }
            public string Name { get; internal set; }
            public FactionId Desired { get; internal set; }
            public bool Ready { get; internal set; }
            public bool Confirmed { get; internal set; }
            public int Seat { get; internal set; } = -1;
            internal long Sequence;
        }
        private readonly List<Member> members = new List<Member>();
        private readonly HashSet<int> aiSeats = new HashSet<int>();
        private readonly HashSet<ulong> acknowledged = new HashSet<ulong>();
        private readonly AiSettings aiSettings;
        private readonly DemoRuleAssumptions rules;
        private RuleBasedAi[] planners;
        private AiTurn aiTurn;
        private int aiActor = -1, aiRound = -1, revealSerial, assignmentVersion;
        private FactionId[] assigned;
        private string[] seatNames;
        private PendingPlay pending;
        private sealed class PendingPlay { internal int actor; internal Guid source; internal EffectTarget target; }
        public string Session { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; }
        public ulong HostConnection { get; }
        public LanPhase Phase { get; private set; } = LanPhase.Lobby;
        public int Version { get; private set; }
        public int AssignmentSeed { get; private set; }
        // Private host seed: publishing the faction lottery seed must not reveal deck order.
        public int GameSeed { get; }
        public GameSession Game { get; private set; }
        public bool Busy { get; private set; }
        public LanReveal Reveal { get; private set; }
        public IReadOnlyList<Member> Members => members.AsReadOnly();
        public IReadOnlyList<FactionId> Assignment => assigned ?? Array.Empty<FactionId>();
        public bool IsAi(int seat) => aiSeats.Contains(seat);

        public LanRoom(ulong host, string name, string hostName, AiSettings ai = null, DemoRuleAssumptions assumptions = null)
        {
            HostConnection = host; Name = LanProtocol.CleanName(name, 48); aiSettings = ai ?? new AiSettings();
            byte[] entropy=new byte[4]; using(var rng=RandomNumberGenerator.Create())rng.GetBytes(entropy);
            GameSeed=BitConverter.ToInt32(entropy,0)&int.MaxValue;
            var source=assumptions??new DemoRuleAssumptions();
            rules=new DemoRuleAssumptions { randomSeed=GameSeed, startingChurchProperty=source.startingChurchProperty,
                shuffleInitialDeck=source.shuffleInitialDeck, accumulateOfferingBetweenRounds=source.accumulateOfferingBetweenRounds,
                maxCardsPerActionTurn=source.maxCardsPerActionTurn, rewardDestination=source.rewardDestination,
                requireFullFollowerTargets=source.requireFullFollowerTargets, allowSelfAsOpponentTarget=source.allowSelfAsOpponentTarget,
                timedEffects=(source.timedEffects??new TimedEffectPolicy()).Copy() };
            members.Add(new Member { Connection = host, Name = LanProtocol.CleanName(hostName), Desired = FactionId.Poverty });
        }
        public bool CanJoin => Phase == LanPhase.Lobby && members.Count < 4;
        public bool Join(ulong connection, string name, out string reason)
        {
            if (!CanJoin || members.Any(m => m.Connection == connection)) { reason = "방이 가득 찼거나 이미 시작되었습니다."; return false; }
            members.Add(new Member { Connection = connection, Name = LanProtocol.CleanName(name), Desired = FactionId.Poverty });
            Version++; reason = "참가했습니다."; return true;
        }
        public void Disconnect(ulong connection)
        {
            var member = members.FirstOrDefault(m => m.Connection == connection);
            if (member == null) return;
            if (connection == HostConnection) { Phase = LanPhase.Closed; Busy = false; pending = null; Version++; return; }
            members.Remove(member); acknowledged.Remove(connection);
            if (Phase == LanPhase.Playing) {
                aiSeats.Add(member.Seat);
                if (Game.Phase == GamePhase.RewardChoice) AdvanceAcknowledgedRewards();
            } else if (Phase == LanPhase.Assignment) {
                Phase = LanPhase.Lobby; assigned = null;
                foreach (var m in members) { m.Ready = false; m.Confirmed = false; m.Seat = -1; }
            }
            Version++;
        }
        public static FactionId[] Assign(IReadOnlyList<FactionId> desired, int seed)
        {
            if (desired == null || desired.Count < 2 || desired.Count > 4 || desired.Any(f => !FactionSetup.IsPlayable(f))) throw new ArgumentException("희망 교단/인원 오류");
            var result = new FactionId[4]; var remaining = new List<FactionId> { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur };
            for (int i = 0; i < desired.Count; i++) if (desired.Count(f => f == desired[i]) == 1) { result[i] = desired[i]; remaining.Remove(desired[i]); }
            var random = new Random(seed);
            for (int i = remaining.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var f = remaining[i]; remaining[i] = remaining[j]; remaining[j] = f; }
            int next = 0; for (int i = 0; i < 4; i++) if (result[i] == FactionId.None) result[i] = remaining[next++];
            return result;
        }
        public bool Submit(ulong connection, LanCommand command, out string reason, int seed = 0)
        {
            var member = members.FirstOrDefault(m => m.Connection == connection);
            if (member == null || command == null || command.session != Session) { reason = "유효하지 않은 연결/세션입니다."; return false; }
            if (command.sequence <= member.Sequence) { reason = "이미 처리한 요청입니다."; return false; }
            member.Sequence = command.sequence; // Reject replay of both accepted and rejected requests.
            if (!Enum.IsDefined(typeof(LanCommandKind), command.kind) || command.kind == LanCommandKind.Debug) { reason = "LAN에서는 디버그 요청을 허용하지 않습니다."; return false; }
            reason = "요청을 처리했습니다.";
            if (Phase == LanPhase.Lobby) {
                if (command.kind == LanCommandKind.Faction && FactionSetup.IsPlayable(command.faction)) { member.Desired = command.faction; member.Ready = false; Version++; return true; }
                if (command.kind == LanCommandKind.Ready) { member.Ready = command.ready; Version++; return true; }
                if (command.kind == LanCommandKind.Prepare && connection == HostConnection && members.Count >= 2 && members.All(m => m.Ready)) {
                    AssignmentSeed = seed; assigned = Assign(members.Select(m => m.Desired).ToArray(), seed);
                    for (int i = 0; i < members.Count; i++) { members[i].Seat = i; members[i].Confirmed = false; }
                    Phase = LanPhase.Assignment; Version++; assignmentVersion = Version; return true;
                }
                reason = "호스트만 시작할 수 있으며 인간 2명 이상과 전원 준비가 필요합니다."; return false;
            }
            if (Phase == LanPhase.Assignment && command.kind == LanCommandKind.ConfirmAssignment) {
                if (command.version < assignmentVersion || command.version > Version) { reason = "이전 배정 확인 요청입니다."; return false; }
                member.Confirmed = true; Version++;
                if (members.All(m => m.Confirmed)) {
                    Game = new GameSession(rules, humanFaction: assigned[0], factionSeats: assigned, networkControlled: true);
                    aiSeats.Clear(); for (int i = members.Count; i < 4; i++) aiSeats.Add(i);
                    // Lobby nicknames stay attached to their seats even after a departure hands the seat to AI.
                    seatNames = Enumerable.Range(0, 4).Select(i => i < members.Count ? members[i].Name : null).ToArray();
                    planners = Enumerable.Range(0, 4).Select(i => new RuleBasedAi(aiSettings, i)).ToArray(); Phase = LanPhase.Playing;
                }
                return true;
            }
            if (Phase != LanPhase.Playing) { reason = "현재는 게임 요청을 처리할 수 없습니다."; return false; }
            if (Busy) { reason = "카드 공개/처리 중입니다."; return false; }
            if (command.kind == LanCommandKind.Acknowledge && Game.Phase == GamePhase.RewardChoice) {
                if (command.round != Game.Round) { reason = "이전 라운드의 획득 확인입니다."; return false; }
                if (acknowledged.Contains(connection)) return true;
                acknowledged.Add(connection); AdvanceAcknowledgedRewards(); Version++; return true;
            }
            if (command.version != Version || command.round != Game.Round) { reason = "상태가 갱신되었습니다. 다시 시도하세요."; return false; }
            if (member.Seat != Game.ActivePlayerIndex || IsAi(member.Seat) || Game.Phase != GamePhase.Actions) { reason = "자신의 행동 턴이 아닙니다."; return false; }
            if (command.kind == LanCommandKind.EndTurn) { Game.EndActionTurn(member.Seat); Version++; return true; }
            if (command.kind == LanCommandKind.Play) {
                if (!Guid.TryParse(command.card, out Guid source) || (command.targets?.Length ?? 0) > 2) { reason = "잘못된 카드 지정입니다."; return false; }
                var ids = new List<Guid>();
                foreach (var text in command.targets ?? Array.Empty<string>()) { if (!Guid.TryParse(text, out Guid id)) { reason = "대상 카드 ID 오류"; return false; } ids.Add(id); }
                return BeginPlay(member.Seat, source, new EffectTarget { PlayerIndex = command.target, CardInstanceIds = ids.ToArray() }, out reason);
            }
            reason = "지원하지 않는 게임 요청입니다."; return false;
        }
        // Remote humans the game is currently waiting on: the active actor, or everyone yet to acknowledge rewards.
        public IReadOnlyList<ulong> StalledRemoteHumans()
        {
            if (Phase != LanPhase.Playing || Game == null || Busy) return Array.Empty<ulong>();
            var remote = members.Where(m => m.Connection != HostConnection);
            if (Game.Phase == GamePhase.Actions) return remote.Where(m => m.Seat == Game.ActivePlayerIndex && !IsAi(m.Seat)).Select(m => m.Connection).ToArray();
            if (Game.Phase == GamePhase.RewardChoice) return remote.Where(m => !acknowledged.Contains(m.Connection)).Select(m => m.Connection).ToArray();
            return Array.Empty<ulong>();
        }
        public string SeatName(int seat) => seatNames != null && seat >= 0 && seat < seatNames.Length && seatNames[seat] != null ? seatNames[seat] + " · " + Game.Players[seat].Name : Game.Players[seat].Name;
        private void AdvanceAcknowledgedRewards()
        {
            if (members.All(m => acknowledged.Contains(m.Connection))) { Game.AcknowledgeRewards(); acknowledged.Clear(); }
        }
        private bool BeginPlay(int actor, Guid source, EffectTarget target, out string reason)
        {
            if (!Game.CanPlay(actor, source, out reason, target)) return false;
            var card = Game.FindCard(source).Card;
            pending = new PendingPlay { actor = actor, source = source, target = target }; Busy = true;
            Reveal = new LanReveal { actor = actor, id = card.Definition.Id, guid = source.ToString("D"), serial = ++revealSerial, result = "카드 공개 중" };
            Version++; return true;
        }
        public bool CompletePlay(out string message)
        {
            if (pending == null) { message = "처리할 카드가 없습니다."; return false; }
            var action = pending; pending = null;
            bool result = Game.TryPlay(action.actor, action.source, out message, action.target);
            Reveal.result = message; Version++; return result;
        }
        public void FinishReveal() { if (Busy && pending == null) { Busy = false; Version++; } }
        public bool StepAi()
        {
            if (Phase != LanPhase.Playing || Busy || Game.Phase != GamePhase.Actions || !IsAi(Game.ActivePlayerIndex)) return false;
            int actor = Game.ActivePlayerIndex;
            if (aiTurn == null || aiActor != actor || aiRound != Game.Round) { aiActor = actor; aiRound = Game.Round; aiTurn = new AiTurn(Game, actor, planners[actor], aiSettings); }
            var next = aiTurn.Next(Game);
            if (next != null && BeginPlay(actor, next.CardInstanceId, next.Target, out _)) return true;
            Game.EndActionTurn(actor); aiTurn = null; Version++; return true;
        }
        public LanLobbyView LobbyFor(ulong connection)
            => new LanLobbyView { session = Session, room = Name, localConnection = connection.ToString(), host = connection == HostConnection,
                version = Version, phase = Phase, seed = AssignmentSeed, assignment = assigned?.ToArray(),
                members = members.Select(m => new LanMemberView { connection = m.Connection.ToString(), name = m.Name, desired = m.Desired,
                    ready = m.Ready, confirmed = m.Confirmed, seat = m.Seat }).ToArray() };
        public LanGameView ViewFor(ulong connection)
        {
            var member = members.FirstOrDefault(m => m.Connection == connection);
            if (Game == null || member == null) return null;
            int owner = member.Seat; var own = Game.Players[owner];
            var choices = new List<LanSelection>();
            if (!Busy && Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex == owner)
                foreach (var source in own.Hand.Where(c => c.Definition.Target == TargetKind.Card && Game.EffectiveCost(owner, c) <= own.Offering)) {
                    var cards = Game.SelectableCards(owner, source.Definition.Id, source.InstanceId).OrderBy(c => c.InstanceId).ToArray();
                    choices.Add(new LanSelection { source = source.InstanceId.ToString("D"), cards = cards.Select(c => LanCard.Of(Game, c)).ToArray(),
                        piles = cards.Select(c => Game.FindCard(c.InstanceId).Pile.ToString()).ToArray() });
                }
            return new LanGameView {
                version = Version, localSeat = owner, round = Game.Round, phase = Game.Phase, activePlayer = Game.ActivePlayerIndex,
                atheists = Game.Atheists, population = Game.PopulationTotal, busy = Busy, acknowledged = acknowledged.Contains(connection), message = Game.LastMessage,
                players = Game.Players.Select((p, i) => new LanPlayer { seat = i, name = SeatName(i), faction = p.Faction, offering = p.Offering,
                    faith = p.Faith, doctrine = p.Doctrine, followers = p.FollowerCount, handCount = p.Hand.Count, deckCount = p.Deck.Count,
                    discardCount = p.Discard.Count, removedCount = p.Removed.Count, property = p.ChurchProperty, judgmentUses = p.JudgmentUses,
                    nextIncome = Game.PreviewRoundIncome(i), ai = IsAi(i), effects = string.Join("\n", p.TimedEffects.Select(e => CardCatalog.Get(e.Source).Name + " · " + e.RemainingRoundStarts + "회")) }).ToArray(),
                hand = own.Hand.Select(c => { var data = LanCard.Of(Game, c); data.costs = Enumerable.Range(0,4).Select(t => Game.EffectiveCost(owner,c,new EffectTarget {PlayerIndex=t})).ToArray(); return data; }).ToArray(), selections = choices.ToArray(), winners = Game.Winners.ToArray(),
                rewards = Game.RoundRewards.Select(r => new LanReward { owner = r.PlayerIndex, id = r.CardId, guid = r.InstanceId.ToString("D"), destination = r.Destination }).ToArray(),
                reveal = Reveal == null ? null : new LanReveal { actor = Reveal.actor, id = Reveal.id, guid = Reveal.guid, serial = Reveal.serial, result = Reveal.result }
            };
        }
    }
}
