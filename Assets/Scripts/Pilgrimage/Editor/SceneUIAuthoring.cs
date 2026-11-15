using System.IO;
using Pilgrimage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.Editor
{
    public static class SceneUIAuthoring
    {
        private const string Folder = "Assets/Pilgrimage/UI";
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = Rect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = Color.white; image.raycastTarget = false;
            return rect;
        }
        private static void Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
        {
            var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset; text.text = value; text.fontSize = fontSize;
            text.color = new Color(.08f,.08f,.08f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
        // Destructive regeneration is an explicit command, never an import hook.
        [MenuItem("Pilgrimage/UI/기본 씬 UI 다시 생성")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("씬 UI 다시 생성", "기본 두 씬과 카드 프리팹의 UI 편집 내용을 초기화합니다. 계속할까요?", "다시 생성", "취소")) return;
            Directory.CreateDirectory(Folder);
            var front = Panel(null, "Card", Vector2.zero, new Vector2(200,280));
            front.GetComponent<Image>().color = Color.clear; front.GetComponent<Image>().raycastTarget = true;
            front.gameObject.AddComponent<CardView>();
            var border = Panel(front,"Card Border",Vector2.zero,new Vector2(200,280));
            var outline=border.gameObject.AddComponent<Outline>();outline.effectColor=Color.gray;outline.effectDistance=new Vector2(1,-1);
            var frame = Panel(border,"Card Frame",Vector2.zero,new Vector2(188,268));
            Label(frame,"Card Number","#01",new Vector2(0,111),new Vector2(163,25),18);
            Label(frame,"Card Name","카드 이름",new Vector2(0,76),new Vector2(172,55),25);
            var art = Panel(frame,"Illustration Slot",new Vector2(0,4),new Vector2(155,85));
            Label(art,"Placeholder Icon","이미지",Vector2.zero,new Vector2(140,75),24);
            Label(frame,"Category","종류",new Vector2(0,-57),new Vector2(175,28),17);
            Label(frame,"Cost","비용",new Vector2(0,-88),new Vector2(177,30),20);
            Label(frame,"Play State","",new Vector2(0,-116),new Vector2(180,24),13);
            var frontPrefab = PrefabUtility.SaveAsPrefabAsset(front.gameObject,Folder+"/Card.prefab").GetComponent<CardView>();
            Object.DestroyImmediate(front.gameObject);
            var back = Panel(null,"Private Card Back",Vector2.zero,new Vector2(62,82));
            Label(back,"Back Symbol","?",Vector2.zero,new Vector2(54,70),30);
            var backPrefab=PrefabUtility.SaveAsPrefabAsset(back.gameObject,Folder+"/CardBack.prefab").GetComponent<RectTransform>();
            Object.DestroyImmediate(back.gameObject);
            var choice=Panel(null,"Card Choice",Vector2.zero,new Vector2(1300,60));
            choice.GetComponent<Image>().raycastTarget=true;
            var button=choice.gameObject.AddComponent<Button>();button.targetGraphic=choice.GetComponent<Image>();
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            choice.gameObject.AddComponent<SceneCardChoice>();
            Label(choice,"Label","카드 선택",Vector2.zero,new Vector2(1280,54),20);
            var label=(RectTransform)choice.Find("Label");label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;label.sizeDelta=new Vector2(-20,-6);
            var choicePrefab=PrefabUtility.SaveAsPrefabAsset(choice.gameObject,Folder+"/CardChoice.prefab").GetComponent<Button>();
            Object.DestroyImmediate(choice.gameObject);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var demo=new GameObject("Pilgrimage Demo").AddComponent<DemoController>();
            demo.AuthorSceneUI(frontPrefab,backPrefab,choicePrefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/PilgrimageDemo.unity");
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/SampleScene.unity",true);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/PilgrimageDemo.unity",true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",true)};
            AssetDatabase.SaveAssets();
            if (File.Exists("Assets/Pilgrimage/Cards/CardArtSources.json")) CardArtworkAuthoring.Upgrade();
            Debug.Log("SCENE_UI_AUTHORED: scenes and three card prefabs saved");
        }
    }
}
