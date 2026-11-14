using System;
using System.Linq;

namespace Pilgrimage
{
    public static class FactionSetup
    {
        public static bool IsPlayable(FactionId faction) => faction >= FactionId.Poverty && faction <= FactionId.Fur;
        public static FactionId[] Seats(FactionId human)
        {
            if (!IsPlayable(human)) throw new ArgumentOutOfRangeException(nameof(human));
            return new[] { human }.Concat(new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur }
                .Where(f => f != human)).ToArray();
        }
        public static string Name(FactionId faction)
        {
            if (!IsPlayable(faction)) throw new ArgumentOutOfRangeException(nameof(faction));
            return GameSession.FactionNames[(int)faction - 1];
        }
        public static string Description(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Poverty: return "구휼과 모금으로 신도를 모읍니다.\n소박한 성전에서 믿음을 키웁니다.";
                case FactionId.Gold: return "교회 재산과 이자를 활용합니다.\n풍요와 믿음 사이의 균형을 살피세요.";
                case FactionId.Discipline: return "교리를 지키고 이단을 교화합니다.\n대주교가 믿음의 흔들림을 막습니다.";
                case FactionId.Fur: return "신도를 신실한 신도로 변화시킵니다.\n높은 신앙으로 숲의 힘을 펼치세요.";
                default: return "";
            }
        }
    }
}
