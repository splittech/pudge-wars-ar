using System;
using System.IO;
using Game.Net;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Net.Editor
{
    public sealed class NetworkBuildInfoProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string AssetPath = "Assets/Game/Resources/NetworkBuildInfo.json";
        private static string _previous;
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            _previous = File.Exists(AssetPath) ? File.ReadAllText(AssetPath) : null;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllText(AssetPath, JsonUtility.ToJson(new NetworkBuildInfo.Data
            {
                commit = NetworkBuildInfo.ReadGitCommit(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))), buildUtc = DateTime.UtcNow.ToString("O")
            }));
            AssetDatabase.ImportAsset(AssetPath);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (_previous == null) AssetDatabase.DeleteAsset(AssetPath);
            else { File.WriteAllText(AssetPath, _previous); AssetDatabase.ImportAsset(AssetPath); }
        }
    }
}
