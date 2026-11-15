using System;
using System.Linq;
using Pilgrimage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Pilgrimage.Editor
{
    public sealed class GameplayHudImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(GameplayHudAuthoring.Root,StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.spriteImportMode=SpriteImportMode.Single;
        }
    }
    public static class GameplayHudAuthoring
    {
        public const string Root="Assets/Pilgrimage/UI/Gameplay/";
        private static readonly Color Ink=new Color(.32f,.015f,.025f);
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var found=parent.Cast<Transform>().FirstOrDefault(t=>t.name==name);
            var rect=found!=null?(RectTransform)found:(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);Place(rect,position,size);return rect;
        }
        private static void Place(RectTransform rect,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
            rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
        }
        private static Image Image(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size)
        {
            var rect=Rect(parent,name,pos,size);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        private static TMP_Text Text(Transform parent,string name,string value,Vector2 pos,Vector2 size,float fontSize)
        {
            var rect=Rect(parent,name,pos,size);var text=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=fontSize;text.color=Color.white;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.enableAutoSizing=true;text.fontSizeMax=fontSize;text.fontSizeMin=12;
            text.fontStyle=FontStyles.Bold;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        private static void Set(SerializedObject data,string key,UnityEngine.Object value)=>data.FindProperty(key).objectReferenceValue=value;
        private static void RemoveOutline(GameObject obj){foreach(var component in obj.GetComponents<Outline>())UnityEngine.Object.DestroyImmediate(component);}
        [MenuItem("Pilgrimage/UI/플레이 화면 이미지 적용")]
        public static void Apply()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            // Explicit imports also update source PNGs that were initially imported before this slicer compiled.
            foreach(string name in new[]{"Opponent.png","Local.png","Turn.png","Background.png"})AssetDatabase.ImportAsset(Root+name,ImportAssetOptions.ForceUpdate);
            try
            {
                foreach(string path in new[]{"Assets/Scenes/PilgrimageDemo.unity","Assets/Scenes/SampleScene.unity"})
                {
                    var scene=EditorSceneManager.OpenScene(path);var demo=UnityEngine.Object.FindAnyObjectByType<DemoController>();var data=new SerializedObject(demo);
                    var canvas=(RectTransform)data.FindProperty("root").objectReferenceValue;
                    var background=canvas.Find("Background").GetComponent<Image>();
                    background.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Background.png");background.color=Color.white;background.preserveAspect=true;background.raycastTarget=false;
                    var fit=background.GetComponent<AspectRatioFitter>()??background.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=16f/9;
                    var positions=new[]{new Vector2(820,-280),new Vector2(820,280),new Vector2(-820,280),new Vector2(-820,-280)};
                    data.FindProperty("opponentStats").arraySize=12;
                    for(int seat=0;seat<4;seat++)
                    {
                        var panel=(RectTransform)canvas.Find("Player "+seat+" Table");Place(panel,positions[seat],new Vector2(200,450));
                        string sheet=seat==0?"Local.png":"Opponent.png";
                        var face=panel.GetComponent<Image>();face.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+sheet);face.color=Color.white;face.preserveAspect=true;face.raycastTarget=false;RemoveOutline(panel.gameObject);
                        foreach(Transform child in panel)if(child.name.StartsWith("Stat ",StringComparison.Ordinal))child.gameObject.SetActive(false);
                        var label=(TMP_Text)data.FindProperty("playerLabels").GetArrayElementAtIndex(seat).objectReferenceValue;
                        Place(label.rectTransform,new Vector2(0,seat==0?130:170),new Vector2(160,60));label.color=Color.white;label.fontStyle=FontStyles.Bold;label.enableAutoSizing=true;label.fontSizeMax=24;label.fontSizeMin=14;label.alignment=TextAlignmentOptions.Center;
                        float[] rows={0,-43,-93,-145};
                        for(int i=0;i<4;i++)
                        {
                            var old=panel.Cast<Transform>().FirstOrDefault(t=>t.name=="HUD Stat "+i);
                            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                            var value=Text(panel,"HUD Value "+i,"0",new Vector2(35,rows[i]),new Vector2(68,34),23);
                            data.FindProperty(seat==0?"ownStats":"opponentStats").GetArrayElementAtIndex(seat==0?i:(seat-1)*4+i).objectReferenceValue=value;
                        }
                        if(seat>0)
                        {
                            var hand=(RectTransform)data.FindProperty("opponentHands").GetArrayElementAtIndex(seat-1).objectReferenceValue;
                            Place(hand,new Vector2(0,88),new Vector2(150,82));
                        }
                        var status=panel.GetComponentsInChildren<Button>(true).First(b=>b.name==(seat==0?"내 추가 상태":"상태"));
                        Place((RectTransform)status.transform,new Vector2(0,seat==0?55:33),new Vector2(90,23));
                        Style(status);
                    }
                    var table=(RectTransform)canvas.Find("Shared Table");table.GetComponent<Image>().color=Color.clear;RemoveOutline(table.gameObject);
                    foreach(var field in new[]{"activeText","messageText","logText","targetLabel"})((TMP_Text)data.FindProperty(field).objectReferenceValue).color=Color.white;
                    var ornament=Image(canvas,"Turn Ornament",AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Turn.png"),new Vector2(0,399),new Vector2(220,258));
                    var round=(TMP_Text)data.FindProperty("roundText").objectReferenceValue;round.transform.SetParent(ornament.transform,false);
                    Place(round.rectTransform,new Vector2(0,-22),new Vector2(100,78));round.text="1";round.color=Ink;round.fontStyle=FontStyles.Bold;
                    round.enableAutoSizing=true;round.fontSizeMax=54;round.fontSizeMin=26;round.alignment=TextAlignmentOptions.Center;
                    Set(data,"atheistsText",Text(ornament.transform,"Atheists","무신론자 100",new Vector2(0,-166),new Vector2(220,28),18));
                    var title=canvas.Find("Title").GetComponent<TMP_Text>();Place(title.rectTransform,new Vector2(-380,505),new Vector2(520,42));title.fontSize=22;title.color=Color.white;
                    foreach(var button in canvas.GetComponentsInChildren<Button>(true))if(button.transform.parent==canvas||button.transform.parent==table)Style(button);
                    Place((RectTransform)((Button)data.FindProperty("endButton").objectReferenceValue).transform,new Vector2(470,-595),new Vector2(230,46));
                    var target=table.GetComponentsInChildren<Button>(true).First(b=>b.name=="대상");Place((RectTransform)target.transform,new Vector2(-470,-595),new Vector2(110,42));
                    Place(((TMP_Text)data.FindProperty("targetLabel").objectReferenceValue).rectTransform,new Vector2(-270,-595),new Vector2(280,42));
                    var revealed=(RectTransform)data.FindProperty("revealedCard").objectReferenceValue;RemoveOutline(revealed.gameObject);
                    data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();Debug.Log("GAMEPLAY_HUD_APPLIED: whole images, four live stats, turn number and table background");
            }
            finally{if(!Application.isBatchMode)EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        private static void Style(Button button)
        {
            var image=button.GetComponent<Image>();if(image!=null)image.color=new Color(.24f,.015f,.025f,.94f);
            foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))text.color=Color.white;
            foreach(var outline in button.GetComponents<Outline>())outline.effectColor=new Color(.85f,.3f,.32f);
        }
    }
}
