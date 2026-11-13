using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Net
{
    public static class NetworkBuildInfo
    {
        [Serializable] public sealed class Data { public string commit; public string buildUtc; }
        public static string Commit()
        {
#if UNITY_EDITOR
            return ReadGitCommit(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
#else
            var asset = Resources.Load<TextAsset>("NetworkBuildInfo");
            return asset == null ? "unknown" : JsonUtility.FromJson<Data>(asset.text).commit;
#endif
        }
        public static string ReadGitCommit(string project)
        {
            try
            {
                string git = Path.Combine(project, ".git");
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (head.StartsWith("ref: ", StringComparison.Ordinal))
                {
                    string reference = head.Substring(5);
                    if (!reference.StartsWith("refs/", StringComparison.Ordinal) || reference.Contains("..")) return "unknown";
                    string loose = Path.Combine(git, reference);
                    if (File.Exists(loose)) head = File.ReadAllText(loose).Trim();
                    else
                    {
                        head = "unknown";
                        foreach (string line in File.ReadLines(Path.Combine(git, "packed-refs")))
                            if (line.EndsWith(" " + reference, StringComparison.Ordinal)) head = line.Split(' ')[0];
                    }
                }
                return Regex.IsMatch(head, "^[a-fA-F0-9]{40,64}$") ? head : "unknown";
            }
            catch (IOException) { return "unknown"; }
            catch (UnauthorizedAccessException) { return "unknown"; }
        }
    }
}
