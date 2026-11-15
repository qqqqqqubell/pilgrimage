using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Pilgrimage.Editor
{
    public static class LanBuildMenu
    {
        // Batch automation uses a temporary output path; never touches scenes/build settings.
        public static void BuildSmokePlayer()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-pilgrimageBuildPath");
            if(i<0||i+1>=args.Length)throw new ArgumentException("-pilgrimageBuildPath required");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/PilgrimageDemo.unity"},locationPathName=Path.GetFullPath(args[i+1]),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("LAN smoke player build failed: "+report.summary.result);
        }
    }
}
