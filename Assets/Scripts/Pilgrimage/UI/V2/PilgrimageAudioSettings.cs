using UnityEngine;
namespace Pilgrimage.UI
{
    [CreateAssetMenu(menuName="Pilgrimage/Audio settings",fileName="PilgrimageAudioSettings")]
    public sealed class PilgrimageAudioSettings : ScriptableObject
    {
        public AudioClip[] playlist;
        [Range(0,1)] public float bgmVolume=.45f;
        [Range(0,1)] public float sfxVolume=.8f;
        public float crossfadeSeconds=1.5f;
        [Range(0,1)] public float cutInDuck=.35f;
        [Range(1,32)] public int voices=12;
    }
}
