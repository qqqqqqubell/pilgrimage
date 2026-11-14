using UnityEngine;
using TMPro;

namespace Pilgrimage.UI
{
    [CreateAssetMenu(fileName = "PilgrimageDemoSettings", menuName = "Pilgrimage/v2 데모 설정")]
    public sealed class DemoSettings : ScriptableObject
    {
        [Header("미확정 규칙 — 확정 카드 데이터와 분리")]
        public DemoRuleAssumptions assumptions = new DemoRuleAssumptions();
        [Header("AI — 교단별 가중치·안전 상한·공개 간격")]
        public AiSettings ai = new AiSettings();
        [Header("선택 사항 — 비어 있으면 임시 도형과 OS 한글 폰트 사용")]
        public TMP_FontAsset koreanFont;
        public Sprite cardBack;
        public Sprite cardIllustration;
        public Sprite generalCardFrame;
        public Sprite followerCardFrame;
        public Sprite hereticCardFrame;
        public Sprite factionCardFrame;
        [Range(0.1f, 0.8f)] public float releaseDuration = 0.25f;
    }
}
