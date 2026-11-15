using UnityEngine;
using UnityEngine.UI;
namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        [SerializeField] private Sprite[] factionPortraitSprites=new Sprite[5];
        [SerializeField] private Image[] playerPortraitImages=new Image[4];
        private void UpdatePlayerPortrait(int relative,FactionId faction)
        {
            playerLabels[relative].gameObject.SetActive(false);
            int index=(int)faction;
            if(relative<playerPortraitImages.Length&&playerPortraitImages[relative]!=null)
                playerPortraitImages[relative].sprite=index>=0&&index<factionPortraitSprites.Length?factionPortraitSprites[index]:null;
            if(relative==0)return;
            opponentHands[relative-1].gameObject.SetActive(false);
            for(int i=0;i<4;i++)opponentStats[(relative-1)*4+i].gameObject.SetActive(true);
        }
    }
}
