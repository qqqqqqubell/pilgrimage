using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    // Player-facing copy is independent of source text, validation diagnostics and engine flags.
    public static class CardPresentation
    {
        private static readonly string[] DevelopmentMarkers = {
            "원문 미명시", "명세 대기", "TODO", "원문에 추가 사용 조건 없음",
            "SpecificationPending", "NotImplemented"
        };
        public static string PlayerText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "미정") return "";
            return string.Join("\n", text.Split('\n').Where(line => !DevelopmentMarkers.Any(marker => line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)))
                .Replace("교리 접합성", "교리 정합성").Replace("현금", "헌금");
        }
        public static string CostLabel(CardDefinition card)
        {
            switch (card.CostKind)
            {
                case CardCostKind.Fixed: return $"헌금 {card.Cost}";
                case CardCostKind.Variable: return "헌금 X";
                default: return "";
            }
        }
        public static string Availability(CardDefinition card)
            => card.IsPlayable && card.Implementation == EffectImplementation.Implemented ? "" : "현재 사용할 수 없습니다.";

        public static string Detail(CardDefinition card)
        {
            var parts = new List<string> { PlayerText(card.Name) };
            // Passive follower details show their inherent effect and flavor only.
            bool passiveFollower = card.CountsAsFollower && card.CostKind == CardCostKind.None;
            if (!passiveFollower && CostLabel(card) != "") parts.Add(CostLabel(card));
            parts.Add(PlayerText(card.Description));
            if (!passiveFollower)
            {
                string condition = PlayerText(card.Conditions);
                if (condition != "") parts.Add("사용 조건: " + condition);
                if (card.Target != TargetKind.Self) parts.Add("대상: " + (card.Target == TargetKind.Player ? "교단" : "카드"));
                if (Availability(card) != "") parts.Add(Availability(card));
            }
            if (PlayerText(card.FlavorText) != "") parts.Add(PlayerText(card.FlavorText));
            return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        public static IReadOnlyList<string> Diagnostics(IEnumerable<CardDefinition> cards)
        {
            var issues = new List<string>();
            foreach (var card in cards)
            {
                string prefix = $"[{card.Id}] {card.Name}: ";
                if (card.CostKind == CardCostKind.MissingSpecification) issues.Add(prefix + "코스트 명세 누락. 사용 차단.");
                if (card.Implementation != EffectImplementation.Implemented) issues.Add(prefix + "효과 미구현/상세 규칙 미확정. 사용 차단.");
                if (new[] { card.Name, card.Description, card.Conditions, card.FlavorText, card.CostText }.Any(s => PlayerText(s) != s))
                    issues.Add(prefix + "개발용 또는 기본 안내 문구를 플레이어 표시에서 제외. 원본 데이터는 유지.");
            }
            return issues.AsReadOnly();
        }
    }
}
