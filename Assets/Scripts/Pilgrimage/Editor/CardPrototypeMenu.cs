using UnityEditor;
using UnityEngine;

namespace Pilgrimage.Editor
{
    public static class CardPrototypeMenu
    {
        [MenuItem("GameObject/Pilgrimage/카드 프로토타입 추가", false, 10)]
        private static void CreatePrototype()
        {
            CardPrototypeController existing = Object.FindAnyObjectByType<CardPrototypeController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            var root = new GameObject("Pilgrimage Card Prototype");
            Undo.RegisterCreatedObjectUndo(root, "Create Pilgrimage prototype");
            Undo.AddComponent<CardPrototypeController>(root);
            Selection.activeGameObject = root;
        }
    }
}
