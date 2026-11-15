using System;
using UnityEngine;
namespace Pilgrimage.UI
{
    [CreateAssetMenu(menuName="Pilgrimage/Card visual catalog",fileName="PilgrimageCardVisuals")]
    public sealed class CardVisualCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public CardId id;
            [Tooltip("Printed ordinary-card art number, independent of CardId. Faction cards use -1.")]
            public int number = -1;
            public Sprite front;
            public Sprite back;
        }
        public Entry[] cards = Array.Empty<Entry>();
        public Sprite anonymousBack;
        public Entry Get(CardId id)
        {
            foreach(var entry in cards) if(entry.id==id) return entry;
            return null;
        }
    }
}
