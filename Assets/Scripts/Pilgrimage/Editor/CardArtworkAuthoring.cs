using System;
using System.IO;
using Pilgrimage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Pilgrimage.Editor
{
    public sealed class CardArtworkImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Pilgrimage/Cards/Art/",StringComparison.Ordinal))return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Sprite;texture.spriteImportMode=SpriteImportMode.Single;
            texture.mipmapEnabled=false;texture.alphaIsTransparency=true;texture.maxTextureSize=2048;
            texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.spritePixelsPerUnit=100;
            texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
        }
    }
    public static class CardArtworkAuthoring
    {
        [Serializable] private sealed class Manifest {public Row[] cards;}
        [Serializable] private sealed class Row {public string id,front,back;public int number;}
        private const string Root="Assets/Pilgrimage/Cards/";
        private const string CardPath="Assets/Pilgrimage/UI/Card.prefab";
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        private static Image Face(Transform parent,string name,Sprite sprite)
        {
            var rect=Rect(parent,name,Vector2.zero,new Vector2(200,280));var image=rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        private static TMP_Text Text(Transform parent,string name,string placeholder,Vector2 position,Vector2 size,float max,float min)
        {
            var text=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=TMP_Settings.defaultFontAsset;text.text=placeholder;text.color=Color.black;text.alignment=TextAlignmentOptions.Center;
            text.fontSize=max;text.enableAutoSizing=true;text.fontSizeMin=min;text.fontSizeMax=max;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }
        private static void Set(SerializedObject data,string name,UnityEngine.Object value)=>data.FindProperty(name).objectReferenceValue=value;
        [MenuItem("Pilgrimage/Cards/카드 이미지 및 공통 프리팹 갱신")]
        public static void Upgrade()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Root+"Resources");
            foreach(string path in Directory.GetFiles(Root+"Art","*.png"))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer!=null&&importer.textureType!=TextureImporterType.Sprite)importer.SaveAndReimport();
            }
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"CardArtSources.json"));
            var library=AssetDatabase.LoadAssetAtPath<CardVisualCatalog>(Root+"Resources/PilgrimageCardVisuals.asset");
            if(library==null){library=ScriptableObject.CreateInstance<CardVisualCatalog>();AssetDatabase.CreateAsset(library,Root+"Resources/PilgrimageCardVisuals.asset");}
            library.cards=Array.ConvertAll(manifest.cards,row=>new CardVisualCatalog.Entry{id=(CardId)Enum.Parse(typeof(CardId),row.id),number=row.number,
                front=string.IsNullOrEmpty(row.front)?null:AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Art/"+row.front),back=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Art/"+row.back)});
            library.anonymousBack=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Art/Unknown_Back.png");
            foreach(var entry in library.cards)if(entry.back==null||(entry.front==null&&entry.id!=CardId.FurRally))throw new InvalidOperationException("Missing art: "+entry.id);
            EditorUtility.SetDirty(library);
            var prefab=PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                var visual=(RectTransform)prefab.transform.Find("Card Border");
                for(int i=visual.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);
                var backdrop=visual.GetComponent<Image>();if(backdrop!=null){backdrop.color=Color.clear;backdrop.raycastTarget=false;}
                var outline=visual.GetComponent<Outline>();if(outline!=null)UnityEngine.Object.DestroyImmediate(outline);
                var first=library.Get(CardId.Follower);
                var front=Face(visual,"Front Face",first.front);var back=Face(visual,"Back Face",first.back);
                var title=Text(back.transform,"Card Name","카드 이름",new Vector2(0,82),new Vector2(146,39),23,15);
                var cost=Text(back.transform,"Cost","헌금 0",new Vector2(0,49),new Vector2(145,21),14,11);
                var description=Text(back.transform,"Card Description","카드 설명",new Vector2(0,-6),new Vector2(147,92),17,7);
                var flavor=Text(back.transform,"Flavor Text","플레이버 텍스트",new Vector2(0,-82),new Vector2(114,48),14,7);flavor.fontStyle=FontStyles.Italic;
                var missing=Text(front.transform,"Missing Artwork","털가죽 집회",Vector2.zero,new Vector2(160,160),24,16);missing.gameObject.SetActive(false);
                var view=prefab.GetComponent<CardView>();var serialized=new SerializedObject(view);
                Set(serialized,"catalog",library);Set(serialized,"visual",visual);Set(serialized,"frontFace",front.gameObject);Set(serialized,"backFace",back.gameObject);
                Set(serialized,"frontImage",front);Set(serialized,"backImage",back);Set(serialized,"nameLabel",title);Set(serialized,"descriptionLabel",description);Set(serialized,"flavorLabel",flavor);Set(serialized,"costLabel",cost);Set(serialized,"missingArtworkLabel",missing);
                serialized.ApplyModifiedPropertiesWithoutUndo();back.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(prefab,CardPath);
            }finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var card=AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
            var hidden=PrefabUtility.LoadPrefabContents("Assets/Pilgrimage/UI/CardBack.prefab");
            try
            {
                hidden.GetComponent<Image>().sprite=library.anonymousBack;hidden.GetComponent<Image>().color=Color.white;hidden.GetComponent<Image>().preserveAspect=true;
                var symbol=hidden.transform.Find("Back Symbol");if(symbol!=null)symbol.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(hidden,"Assets/Pilgrimage/UI/CardBack.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(hidden);}
            try
            {
                foreach(string path in new[]{"Assets/Scenes/PilgrimageDemo.unity","Assets/Scenes/SampleScene.unity"})
                {
                    var scene=EditorSceneManager.OpenScene(path);var demo=UnityEngine.Object.FindAnyObjectByType<DemoController>();var data=new SerializedObject(demo);
                    var container=(RectTransform)data.FindProperty("revealedCard").objectReferenceValue;
                    var old=container.Find("Public Card Visual");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                    foreach(Transform child in container)child.gameObject.SetActive(false);
                    container.GetComponent<Image>().color=Color.clear;container.GetComponent<Image>().raycastTarget=false;
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(card,container);instance.name="Public Card Visual";
                    var rect=(RectTransform)instance.transform;rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one*(container.sizeDelta.x/200f);
                    Set(data,"submittedCardView",instance.GetComponent<CardView>());data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
            }finally{if(!Application.isBatchMode)EditorSceneManager.RestoreSceneManagerSetup(setup);}
            AssetDatabase.SaveAssets();Debug.Log("CARD_ART_UPGRADED: 45 ID mappings, shared prefab, anonymous back and scene preview");
        }
    }
}
