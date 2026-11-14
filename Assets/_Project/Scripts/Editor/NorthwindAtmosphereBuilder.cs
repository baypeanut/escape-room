using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CSER.EditorTools
{
    public static class NorthwindAtmosphereBuilder
    {
        const string RootName = "NorthwindContainmentDisplay";
        const string MaterialFolder = "Assets/_Project/Materials/Assignment";
        const string SceneFolder = "Assets/_Project/Scenes/";
        static readonly string[] Scenes = { "Ch2_FredsOffice", "Ch3_ServerRoom" };

        [MenuItem("CSER/Assignment/Add Containment Displays")]
        public static void ApplyToSavedScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    string path = SceneFolder + name + ".unity";
                    if (!File.Exists(path)) throw new FileNotFoundException("Chapter scene is missing", path);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    BuildForScene(scene, name);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void BuildForScene(Scene scene, string sceneName)
        {
            bool office = sceneName == Scenes[0];
            if (!office && sceneName != Scenes[1]) return;
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("A loaded chapter scene is required");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName) Object.DestroyImmediate(root);

            var coreMaterial = Material("ContainmentCore", "Northwind/Containment Signal");
            var moteMaterial = Material("ContainmentMotes", "Northwind/Signal Mote");
            var housingMaterial = Material("ContainmentHousing", "Universal Render Pipeline/Lit");
            housingMaterial.SetColor("_BaseColor", new Color(0.065f, 0.095f, 0.12f));
            housingMaterial.SetFloat("_Metallic", 0.5f);
            housingMaterial.SetFloat("_Smoothness", 0.32f);
            housingMaterial.EnableKeyword("_EMISSION");
            housingMaterial.SetColor("_EmissionColor", new Color(0.018f, 0.025f, 0.035f));
            EditorUtility.SetDirty(housingMaterial);

            var rootObject = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            var rootTransform = rootObject.transform;
            rootTransform.SetPositionAndRotation(office ? new Vector3(2.43f, 1.65f, 1.15f)
                : new Vector3(1.1f, 1.65f, 2.43f), Quaternion.Euler(0, office ? 90 : 0, 0));
            var atmosphere = rootObject.AddComponent<NorthwindAtmosphere>();
            atmosphere.recoveryTask = office ? TaskId.QuarantineUSB : TaskId.SRV_Isolate;
            atmosphere.pendingText = office ? "! QUARANTINE PENDING" : "! ISOLATION PENDING";
            atmosphere.resolvedText = office ? "OK - USB SECURED" : "OK - NETWORK ISOLATED";

            Shape("Housing", rootTransform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.58f, 0.64f, 0.07f), housingMaterial);
            Shape("SignalCore", rootTransform, PrimitiveType.Cylinder, new Vector3(0, 0.01f, -0.115f), new Vector3(0.15f, 0.12f, 0.15f), coreMaterial, false);
            atmosphere.signalCore = rootTransform.Find("SignalCore").GetComponent<Renderer>();
            foreach (float y in new[] { -0.12f, 0.14f })
                Shape("CoreClamp", rootTransform, PrimitiveType.Cylinder, new Vector3(0, y, -0.115f), new Vector3(0.175f, 0.014f, 0.175f), housingMaterial);
            foreach (float x in new[] { -0.23f, 0.23f })
                foreach (float y in new[] { -0.26f, 0.26f })
                    Shape("Fastener", rootTransform, PrimitiveType.Sphere, new Vector3(x, y, -0.038f), Vector3.one * 0.018f, housingMaterial);
            foreach (float y in new[] { -0.07f, 0f, 0.07f })
                Shape("VentSlat", rootTransform, PrimitiveType.Cube, new Vector3(-0.185f, y, -0.045f), new Vector3(0.075f, 0.012f, 0.018f), housingMaterial);
            Text(rootTransform, "Heading", office ? "USB CONTAINMENT" : "NETWORK ISOLATION",
                new Vector3(0, 0.235f, -0.045f), new Vector2(0.48f, 0.075f), 0.3f);
            atmosphere.statusLabel = Text(rootTransform, "ContainmentStatus", atmosphere.pendingText,
                new Vector3(0, -0.225f, -0.045f), new Vector2(0.5f, 0.105f), 0.24f);
            atmosphere.statusLabel.color = new Color(1f, 0.78f, 0.38f);
            atmosphere.signalMotes = Particles(rootTransform, moteMaterial);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException("Required shader was not imported: " + shaderName);
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, bool stationary = true)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            SceneManager.MoveGameObjectToScene(shape, parent.gameObject.scene);
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stationary) GameObjectUtility.SetStaticEditorFlags(shape, StaticEditorFlags.BatchingStatic);
            return shape;
        }

        static TextMeshPro Text(Transform parent, string name, string value, Vector3 position, Vector2 size, float maxSize)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!text.font) throw new InvalidOperationException("The project font must be imported before adding containment displays");
            text.text = value;
            text.rectTransform.sizeDelta = size;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = maxSize * 0.85f;
            text.fontSizeMax = maxSize;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = new Color(0.88f, 0.94f, 1f);
            text.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return text;
        }

        static ParticleSystem Particles(Transform parent, Material material)
        {
            var go = new GameObject("ContainmentSignalMotes");
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.17f, -0.1f, -0.12f);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.13f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.03f);
            main.startColor = new Color(1f, 0.64f, 0.2f, 0.85f);
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var emission = system.emission;
            emission.rateOverTime = 8f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.02f;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0, 1) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 0.025f;
            return system;
        }
    }
}
