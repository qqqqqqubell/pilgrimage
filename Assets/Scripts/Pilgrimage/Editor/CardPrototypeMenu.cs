using UnityEditor;
namespace Pilgrimage.Editor
{
    public static class CardPrototypeMenu
    {
        [MenuItem("GameObject/Pilgrimage/카드 프로토타입 추가", false, 10)]
        private static void CreatePrototype() => DemoSceneMenu.OpenDemo();
    }
}
