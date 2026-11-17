using UnityEngine;

namespace Game.Input.Routing
{
    public static class ArenaMode
    {
        public static bool UseAR
        {
            get => PlayerPrefs.GetInt("Arena.UseAR", Application.platform == RuntimePlatform.Android ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt("Arena.UseAR", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
