using UnityEditor;
using UnityEditor.SceneManagement;

namespace Pilgrimage.Editor
{
    public static class DemoSceneMenu
    {
        public const string ScenePath = "Assets/Scenes/PilgrimageDemo.unity";
        [MenuItem("Pilgrimage/v2 데모 열기")]
        public static void OpenDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
