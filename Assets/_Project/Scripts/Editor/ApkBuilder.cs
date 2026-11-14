using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace CSER.EditorTools
{
    public static class ApkBuilder
    {
        const string PackageId = "com.baypeanut.midnightlockdown";

        public static void ConfigureQuest()
        {
            RepairSettingsReferences();
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!pipeline) throw new InvalidOperationException("The Quest render pipeline asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            PlayerSettings.productName = "Midnight Lockdown";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
        }

        static void RepairSettingsReferences()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXR Package Settings.asset"))
            {
                if (!asset) continue;
                var serialized = new SerializedObject(asset);
                var features = serialized.FindProperty("features");
                if (features == null || !features.isArray) continue;
                for (int index = features.arraySize - 1; index >= 0; index--)
                    if (!features.GetArrayElementAtIndex(index).objectReferenceValue)
                        features.DeleteArrayElementAtIndex(index);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var renderSettings = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            if (renderSettings && SerializationUtility.ClearAllManagedReferencesWithMissingTypes(renderSettings))
                EditorUtility.SetDirty(renderSettings);
        }

        [MenuItem("CSER/Build APK (Quest)")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Select Android before invoking the build, or pass -buildTarget Android to the editor.");

            ConfigureQuest();
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length != 4 || scenes.Any(scene => !File.Exists(scene)))
                throw new InvalidOperationException("All four story scenes must exist and be enabled in Build Settings.");
            string requestedOutput = Environment.GetEnvironmentVariable("NORTHWIND_APK_OUTPUT");
            string outPath = Path.GetFullPath(string.IsNullOrWhiteSpace(requestedOutput)
                ? Path.Combine("Builds", "MidnightLockdown-Quest3.apk") : requestedOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = $"result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} time={s.totalTime} path={outPath}";
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) msg += "\n" + m.content;
            Debug.Log("[ApkBuilder] " + msg);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Path.Combine("Temp", "cser_build.txt"), msg);
            File.WriteAllText(Path.ChangeExtension(outPath, ".build.txt"), msg);
            if (s.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Quest build failed: " + msg);
        }
    }
}
