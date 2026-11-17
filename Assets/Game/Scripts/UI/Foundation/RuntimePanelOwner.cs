using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Foundation
{
    public sealed class RuntimePanelOwner : MonoBehaviour
    {
        public PanelSettings Panel
        {
            get; set;
        }
        private void OnDestroy()
        {
            if (Panel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(Panel);
                }
                else
                {
                    DestroyImmediate(Panel);
                }
            }
        }
    }
}
