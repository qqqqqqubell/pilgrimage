using System;
using UnityEditor;
using UnityEngine;
namespace Pilgrimage.Editor
{
    public sealed class PilgrimageAudioImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            bool bgm=assetPath.StartsWith("Assets/Audio/BGM/",StringComparison.Ordinal);
            if(!bgm&&!assetPath.StartsWith("Assets/Resources/sfx/",StringComparison.Ordinal))return;
            var importer=(AudioImporter)assetImporter;var sample=importer.defaultSampleSettings;
            sample.loadType=bgm?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.8f;
            sample.preloadAudioData=!bgm;
            importer.defaultSampleSettings=sample;importer.loadInBackground=bgm;
            importer.forceToMono=false;
        }
    }
}
