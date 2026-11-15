using UnityEngine;
namespace Pilgrimage
{
    /// <summary>Legacy scene migration marker. Both shipped scenes now use authored Canvas UI.</summary>
    [DisallowMultipleComponent]
    public sealed class CardPrototypeController : MonoBehaviour
    {
        private void Start()
        {
            Debug.LogWarning("Pilgrimage: legacy IMGUI was retired. Open Assets/Scenes/SampleScene.unity or PilgrimageDemo.unity for the editable Canvas UI.", this);
            enabled = false;
        }
    }
}
