using UnityEngine;

namespace Game.Services.RaycastService
{
    public interface IRaycastService
    {
        bool RaycastOnComponent<T>(out T component, LayerMask layerMask) where T : Component;

        /// <param name="camera">Камера, из которой пускается луч</param>
        /// <param name="view">UI-элемент, на котором показывается RenderTexture камеры.
        /// Null — камера рисует прямо на экран</param>
        void SwitchCamera(Camera camera, RectTransform view = null);

        // Возвращает камеру по умолчанию (из ProjectScope)
        void ResetCamera();
    }
}
