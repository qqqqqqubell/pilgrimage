using UnityEngine;

namespace Pilgrimage
{
    [CreateAssetMenu(fileName = "PrototypeTestSettings", menuName = "Pilgrimage/임시 테스트 설정")]
    public sealed class PrototypeTestSettings : ScriptableObject
    {
        [Tooltip("모든 값은 임시 테스트 가정입니다. 변경 후 테스트 초기화를 누르면 적용됩니다.")]
        public PrototypeTestConfig values = new PrototypeTestConfig();
    }
}
