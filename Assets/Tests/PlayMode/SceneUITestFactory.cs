using System;
using System.Collections;
using System.Linq;
using Pilgrimage.UI;
using UnityEngine.SceneManagement;
namespace Pilgrimage.Tests
{
    internal static class SceneUITestFactory
    {
        internal static void Capture(DemoController demo,string flag)
        {
            var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,flag);
            if(at<0||at+1>=args.Length||UnityEngine.SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var canvas=demo.GetComponentInChildren<UnityEngine.Canvas>();var camera=demo.GetComponentInChildren<UnityEngine.Camera>();
            var target=new UnityEngine.RenderTexture(1920,1080,24);var texture=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGB24,false);
            var previous=UnityEngine.RenderTexture.active;int mask=camera.cullingMask;
            try
            {
                camera.targetTexture=target;camera.cullingMask=-1;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                UnityEngine.Canvas.ForceUpdateCanvases();camera.Render();UnityEngine.RenderTexture.active=target;
                texture.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);texture.Apply();System.IO.File.WriteAllBytes(args[at+1],UnityEngine.ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                UnityEngine.RenderTexture.active=previous;camera.targetTexture=null;camera.cullingMask=mask;
                canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);
            }
        }
        internal static IEnumerator Load(Action<DemoController> loaded)
        {
            // Scene loading completes on the player loop; querying immediately after LoadScene is too early.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var old = SceneManager.GetSceneAt(i);
                if (old.name == "PilgrimageDemo" && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            }
            yield return SceneManager.LoadSceneAsync("PilgrimageDemo", LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByName("PilgrimageDemo");
            loaded(scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<DemoController>(true)).Single());
        }
    }
}
