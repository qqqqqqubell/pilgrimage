using System;
using UnityEngine;

namespace Pilgrimage.UI
{
    [CreateAssetMenu(menuName="Pilgrimage/Visual Asset Library")]
    public sealed class VisualAssetLibrary : ScriptableObject
    {
        public Sprite background;
        public Sprite[] cutInBackground, cutInMask, cutInForeground;
        public Sprite poverty, gold, discipline, fur;
        public Sprite[] hit1, hit2, explosion1, explosion2, coins;
        public TextAsset cutInMetadata;
        public Sprite Portrait(FactionId faction)
        { switch(faction){case FactionId.Poverty:return poverty;case FactionId.Gold:return gold;case FactionId.Discipline:return discipline;case FactionId.Fur:return fur;default:return null;} }
        public static bool HasCutIn(CardDefinition def)=>def!=null&&def.Faction!=FactionId.None&&def.IsPlayable&&def.Implementation==EffectImplementation.Implemented;
        [Serializable] public sealed class PortraitFrame { public float x,y,scale;public bool visible; }
        [Serializable] public sealed class Timeline { public float fps;public int frame_count;public float[] dim_alpha;public PortraitFrame[] portrait; }
    }
}
