using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Pilgrimage
{
    public enum LanPhase { Lobby, Assignment, Playing, Closed }
    public enum LanCommandKind { Faction, Ready, Prepare, ConfirmAssignment, Play, EndTurn, Acknowledge, Debug, Rematch }

    [Serializable] public sealed class LanCommand
    {
        public string session;
        public long sequence;
        public int version, round;
        public LanCommandKind kind;
        public FactionId faction;
        public bool ready;
        public string card;
        public int target;
        public string[] targets;
    }
    [Serializable] public sealed class LanCard
    {
        public string guid;
        public CardId id;
        public int uses, refusingTurns;
        public int[] costs;
        public CardInstance DisplayCard() => new CardInstance(Guid.Parse(guid), id);
        internal static LanCard Of(GameSession game, CardInstance card) => new LanCard {
            guid = card.InstanceId.ToString("D"), id = card.Definition.Id, uses = card.SuccessfulUses, refusingTurns = game.RefusingTurns(card.InstanceId)
        };
    }
    [Serializable] public sealed class LanSelection
    {
        public string source;
        public LanCard[] cards;
        public string[] piles;
    }
    [Serializable] public sealed class LanPlayer
    {
        public int seat;
        public string name;
        public FactionId faction;
        public int offering, faith, doctrine, followers, handCount, deckCount, discardCount, removedCount, property, judgmentUses, nextIncome;
        public bool ai;
        public string effects;
    }
    [Serializable] public sealed class LanReward
    {
        public int owner;
        public CardId id;
        public string guid;
        public CardPile destination;
    }
    [Serializable] public sealed class LanReveal
    {
        public int serial, actor;
        public CardId id;
        public string guid, result;
        public bool completed, succeeded;
        public float travelSeconds, cutInSeconds, resultSeconds;
        public GameVisualEvent outcome;
    }
    [Serializable] public sealed class LanGameView
    {
        public int version, localSeat, round, activePlayer, atheists, population;
        public GamePhase phase;
        public bool busy, acknowledged;
        public string message;
        public LanPlayer[] players;
        public LanCard[] hand;
        public LanSelection[] selections;
        public LanReward[] rewards;
        public int[] winners;
        public LanReveal reveal;
        public GameVisualEvent[] events;
        public int EffectiveCost(LanCard card, int target)
        {
            // The host computes every cost with the real rules; clients never re-derive them.
            return card != null && card.costs != null && card.costs.Length == 4 && target >= 0 && target < 4 ? card.costs[target] : -1;
        }
    }
    [Serializable] public sealed class LanMemberView
    {
        public string connection, name;
        public FactionId desired;
        public bool ready, confirmed;
        public int seat;
    }
    [Serializable] public sealed class LanLobbyView
    {
        public string session, room, localConnection;
        public int version, seed;
        public LanPhase phase;
        public bool host;
        public LanMemberView[] members;
        public FactionId[] assignment;
    }
    [Serializable] public sealed class LanEnvelope
    {
        public string error;
        public bool closed;
        public long reply;
        public LanLobbyView lobby;
        public LanGameView game;
    }
    public static class LanProtocol
    {
        public const ushort DefaultPort = 7777;
        public const int MaximumJsonCharacters = 120000;
        public static string Fingerprint
        {
            get {
                var rules = "pilgrimage-lan-v2-visual-events|2026-11-15-rematch|40,4,5,3,4,2,f3|" + string.Join("|", CardCatalog.All.OrderBy(c => c.Id)
                    .Select(c => (int)c.Id + ":" + c.Name + ":" + c.CostKind + ":" + c.Cost + ":" + c.Faction + ":" + c.Target + ":" + c.Implementation + ":" + c.Description + ":" + c.EffectKey));
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(rules))).Replace("-", "");
            }
        }
        public static string CleanName(string name, int maximum = 32)
        {
            string clean = new string((name ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(maximum).ToArray()).Trim();
            return clean.Length == 0 ? "플레이어" : clean;
        }
    }
}
