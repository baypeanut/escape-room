using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CSER.EditorTools
{
    public sealed class QuestReleaseSettings : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string SettingsPath = "Assets/Resources/DevAgentSettings.asset";
        public int callbackOrder => 1000;

        [Serializable]
        public sealed class Fingerprint
        {
            public string field;
            public int length;
            public string digest;
        }

        [Serializable]
        public sealed class FingerprintList
        {
            public List<Fingerprint> values = new List<Fingerprint>();
        }

        public void OnPreprocessBuild(BuildReport report) => ClearConnectionSettings(true);
        public void OnPostprocessBuild(BuildReport report) => ClearConnectionSettings(false);

        static void ClearConnectionSettings(bool recordFingerprints)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(SettingsPath);
            if (!asset) return;
            var serialized = new SerializedObject(asset);
            var enabled = serialized.FindProperty("enabled");
            if (enabled == null) throw new BuildFailedException("Development connection settings changed format.");
            enabled.boolValue = false;
            var fingerprints = new FingerprintList();
            foreach (string field in new[] { "serverAddress", "accessToken", "witClientAccessToken" })
            {
                var property = serialized.FindProperty(field);
                if (property == null) throw new BuildFailedException("Development connection settings changed format: " + field);
                string value = property.stringValue;
                if (recordFingerprints && !string.IsNullOrEmpty(value))
                {
                    using (var hash = SHA256.Create())
                    {
                        fingerprints.values.Add(new Fingerprint
                        {
                            field = field,
                            length = Encoding.UTF8.GetByteCount(value),
                            digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant()
                        });
                    }
                }
                property.stringValue = "";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            if (recordFingerprints)
            {
                Directory.CreateDirectory("Library/ReleaseVerification");
                File.WriteAllText("Library/ReleaseVerification/ConnectionFingerprints.json", JsonUtility.ToJson(fingerprints));
            }
            Debug.Log("Release settings: development connections disabled and private connection fields cleared.");
        }
    }
}
