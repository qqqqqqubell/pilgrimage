using System;
using System.IO;
using System.Linq;
using Pilgrimage.UI;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Pilgrimage.Editor
{
    // Repeatable import setup. Source PNG bytes are preserved.
    public sealed class VisualAssetsImporter : AssetPostprocessor
    {
        public const string Root="Assets/Pilgrimage/Visuals/";
        public const string LibraryPath=Root+"Resources/PilgrimageVisualAssets.asset";
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
            Configure((TextureImporter)assetImporter,assetPath);
        }
        private static void Configure(TextureImporter importer,string path)
        {
            importer.textureType=TextureImporterType.Sprite;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.alphaIsTransparency=true;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.spritePixelsPerUnit=100;importer.npotScale=TextureImporterNPOTScale.None;
            int cols=0,rows=0,w=0,h=0;
            if(path.Contains("cutin_")&&path.Contains("sheet")){cols=4;rows=6;w=960;h=540;}
            else if(path.Contains("Sinestesia/Hit/")){cols=rows=4;w=h=1024;}
            else if(path.Contains("Sinestesia/Gold/")){cols=rows=8;w=h=512;}
            else if(path.Contains("Sinestesia/Explosions/")){cols=rows=8;w=h=256;}
            importer.spriteImportMode=cols==0?SpriteImportMode.Single:SpriteImportMode.Multiple;
            if(cols==0)return;
            var factory=new SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var previous=provider.GetSpriteRects().ToDictionary(r=>r.name,r=>r.spriteID);
            var sprites=new SpriteRect[cols*rows];
            for(int i=0;i<sprites.Length;i++){
                string name=Path.GetFileNameWithoutExtension(path)+"_"+i.ToString("00");
                sprites[i]=new SpriteRect{name=name,rect=new Rect(i%cols*w,(rows-1-i/cols)*h,w,h),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=previous.TryGetValue(name,out var id)?id:GUID.Generate()};
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();
        }
        [InitializeOnLoadMethod] private static void Schedule()=>EditorApplication.delayCall+=EnsureLibrary;
        [MenuItem("Pilgrimage/Visuals/에셋 설정 및 라이브러리 갱신")]
        public static void EnsureLibrary()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=EnsureLibrary;return;}
            if(!Directory.Exists(Root))return;
            Directory.CreateDirectory(Root+"Resources");
            var data=AssetDatabase.LoadAssetAtPath<VisualAssetLibrary>(LibraryPath);
            bool create=data==null;if(create)data=ScriptableObject.CreateInstance<VisualAssetLibrary>();
            string previous=EditorJsonUtility.ToJson(data);
            data.background=Load("Background/red_table_background.png");
            data.cutInBackground=Frames("CutIn/cutin_bg_sheet.png");data.cutInMask=Frames("CutIn/cutin_mask_sheet.png");data.cutInForeground=Frames("CutIn/cutin_fg_sheet.png");
            data.poverty=Load("CutIn/portrait_poor.png");data.gold=Load("CutIn/portrait_gold.png");data.discipline=Load("CutIn/portrait_law.png");data.fur=Load("CutIn/portrait_forest.png");
            data.hit1=Frames("Sinestesia/Hit/hit_yellow_sheet.png");data.hit2=Frames("Sinestesia/Hit/hit2_yellow_sheet.png");
            data.explosion1=Frames("Sinestesia/Explosions/explosion_1_half.png");data.explosion2=Frames("Sinestesia/Explosions/explosion_2_half.png");data.coins=Frames("Sinestesia/Gold/gold_coins_sheet.png");
            data.cutInMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"CutIn/cutin_meta.json");
            if(data.cutInBackground.Length!=24||data.cutInMask.Length!=24||data.cutInForeground.Length!=24||data.hit1.Length!=16||data.hit2.Length!=16||data.explosion1.Length!=64||data.explosion2.Length!=64||data.coins.Length!=64)
            {
                EditorJsonUtility.FromJsonOverwrite(previous,data);
                if(create)UnityEngine.Object.DestroyImmediate(data);
                EditorApplication.delayCall+=EnsureLibrary;return;
            }
            if(create)AssetDatabase.CreateAsset(data,LibraryPath);
            if(create||previous!=EditorJsonUtility.ToJson(data)){EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();}
            string prefabPath=Root+"Resources/PilgrimageGameplayVisuals.prefab";
            if(!File.Exists(prefabPath)){
                var go=new GameObject("Pilgrimage Gameplay Visuals",typeof(RectTransform),typeof(GameplayVisualCoordinator),typeof(CutInPresenter),typeof(ImpactVfxPresenter),typeof(StatusPopupPresenter),typeof(AudioSource));
                go.GetComponent<AudioSource>().playOnAwake=false;
                PrefabUtility.SaveAsPrefabAsset(go,prefabPath);UnityEngine.Object.DestroyImmediate(go);
            }
        }
        private static void EnsureImport(string path,bool multiple)
        {
            var importer=AssetImporter.GetAtPath(Root+path) as TextureImporter;
            if(importer==null)return;
            if(importer.textureType!=TextureImporterType.Sprite||importer.maxTextureSize!=4096||importer.spriteImportMode!=(multiple?SpriteImportMode.Multiple:SpriteImportMode.Single)||!AssetDatabase.LoadAllAssetsAtPath(Root+path).OfType<Sprite>().Any()){
                Configure(importer,Root+path);importer.SaveAndReimport();
            }
        }
        private static Sprite Load(string path){EnsureImport(path,false);return AssetDatabase.LoadAssetAtPath<Sprite>(Root+path);}
        private static Sprite[] Frames(string path){EnsureImport(path,true);return AssetDatabase.LoadAllAssetsAtPath(Root+path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();}
        [MenuItem("Pilgrimage/Visuals/스프라이트 재임포트")]
        public static void ReimportAll()
        {
            foreach(string path in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))AssetDatabase.ImportAsset(path.Replace('\\','/'),ImportAssetOptions.ForceUpdate);
            EnsureLibrary();
        }
    }
}
