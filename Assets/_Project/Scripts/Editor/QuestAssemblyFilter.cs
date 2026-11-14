using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CSER.EditorTools
{
    public sealed class QuestAssemblyFilter : IFilterBuildAssemblies
    {
        public int callbackOrder => 2000;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return assemblies;
            var excluded = new HashSet<string>(assemblies.Select(Path.GetFileNameWithoutExtension)
                .Where(IsOptionalHelper), StringComparer.Ordinal);
            if (excluded.Count == 0) return assemblies;
            var retained = assemblies.Where(path => !excluded.Contains(Path.GetFileNameWithoutExtension(path))).ToArray();
            foreach (string path in retained)
            {
                var metadata = System.Reflection.Assembly.ReflectionOnlyLoadFrom(Path.GetFullPath(path));
                foreach (var dependency in metadata.GetReferencedAssemblies())
                    if (excluded.Contains(dependency.Name))
                        throw new BuildFailedException(Path.GetFileName(path) + " requires an excluded SDK helper: " + dependency.Name);
            }

            var roots = AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string dependency in AssetDatabase.GetDependencies(roots, true))
            {
                if (!dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(dependency);
                var type = script ? script.GetClass() : null;
                if (type != null && excluded.Contains(type.Assembly.GetName().Name))
                    throw new BuildFailedException("A project asset uses an excluded SDK helper: " + dependency);
            }

            Directory.CreateDirectory("Library/ReleaseVerification");
            File.WriteAllLines("Library/ReleaseVerification/ExcludedAssemblies.txt", excluded.OrderBy(name => name));
            Debug.Log("Release assembly filter: skipped " + excluded.Count + " unused SDK helper assemblies.");
            return retained;
        }

        static bool IsOptionalHelper(string name) =>
            name == "Meta.XR.BuildingBlocks" || name.StartsWith("Meta.XR.BuildingBlocks.", StringComparison.Ordinal) ||
            name == "Meta.XR.MultiplayerBlocks" || name.StartsWith("Meta.XR.MultiplayerBlocks.", StringComparison.Ordinal);
    }
}
