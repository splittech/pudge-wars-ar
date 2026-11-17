using UnityEngine;

namespace Game.App
{
    public static class SceneUiNavigation
    {
        public static void Navigate(Component owner)
        {
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
            {
                var navigation = root.GetComponentInChildren<SwitchSceneButton>(true);
                if (navigation == null)
                {
                    continue;
                }

                navigation.Navigate();
                return;
            }
            Debug.LogError("Scene has no navigation target.", owner);
        }
    }
}
