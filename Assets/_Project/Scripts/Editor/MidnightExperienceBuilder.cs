using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Editor.QuickActions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CSER.EditorTools
{
    public static class MidnightExperienceBuilder
    {
        const string RootName = "MidnightExperience";
        const string AssetFolder = "Assets/_Project/Materials/Assignment";
        const string MeshFolder = "Assets/_Project/Models/Authored";
        static readonly string[] Scenes = { "Ch1_NightShift", "Ch2_FredsOffice", "Ch3_ServerRoom" };

        [MenuItem("CSER/Assignment/Apply Experience Improvements")]
        public static void ApplyToSavedScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity", OpenSceneMode.Single);
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

        public static void BuildForScene(Scene scene, string name)
        {
            if (Array.IndexOf(Scenes, name) < 0) return;
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == RootName) Object.DestroyImmediate(existing);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var housing = Surface("MidnightHousing", new Color(0.035f, 0.055f, 0.075f), 0.55f);
            var trim = Surface("MidnightTrim", new Color(0.42f, 0.48f, 0.54f), 0.75f);
            var dark = Surface("MidnightSocket", new Color(0.07f, 0.105f, 0.145f), 0.15f);

            if (name == Scenes[0]) ImproveSigns(scene, root.transform, housing);
            if (name == Scenes[1]) MakeNightSky();
            if (name == Scenes[2])
            {
                BuildPatch(root.transform, housing, trim, dark);
                BuildRail(root.transform, housing, trim);
                BuildRestoration(scene, root.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void BuildPatch(Transform parent, Material housing, Material trim, Material dark)
        {
            var root = Child("RecoveryPatchStation", parent);
            root.SetPositionAndRotation(new Vector3(1f, 1.3f, -2.43f), Quaternion.Euler(0, 180, 0));
            var station = root.gameObject.AddComponent<PatchRoutingStation>();
            var face = PlateMesh("RecoveryPanel", 1.32f, 1.32f, 0.07f, 0.075f);
            MeshObject("RecoveryHousing", root, Vector3.zero, face, housing, true);
            Box("HeaderBand", root, new Vector3(0, 0.59f, -0.04f), new Vector3(1.12f, 0.012f, 0.01f), trim);
            Text(root, "PatchTitle", "RECOVERY PATCH", new Vector3(0, 0.5f, -0.045f), new Vector2(1.1f, 0.12f), 0.47f);
            var clue = Text(root, "RoutingClue",
                "Join BACKUP to RECOVERY.\n\nUse all three cartridges.\n\nGrip, turn, then release into a bay.\n\nKeep the line unbroken.",
                new Vector3(-0.48f, 0.015f, -0.045f), new Vector2(0.30f, 0.78f), 0.265f);
            clue.alignment = TextAlignmentOptions.TopLeft;
            clue.fontStyle = FontStyles.Normal;

            var signal = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "/ContainmentCore.mat");
            if (!signal) throw new InvalidOperationException("Apply the containment display before the recovery patch");
            var socketMesh = PlateMesh("RecoverySocket", 0.235f, 0.235f, 0.018f, 0.02f);
            station.sockets = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 center = new Vector3(-0.02f + (i % 2) * 0.28f, 0.18f - (i / 2) * 0.28f, -0.055f);
                MeshObject("Bay" + (i + 1), root, center, socketMesh, dark);
                station.sockets[i] = Child("Socket" + i, root);
                station.sockets[i].localPosition = center + Vector3.back * 0.045f;
                Text(root, "BayNumber" + i, (i + 1).ToString(), center + new Vector3(0.088f, 0.086f, -0.014f), new Vector2(0.04f, 0.04f), 0.17f);
                for (int d = 0; d < 4; d++)
                {
                    Vector3 direction = Quaternion.Euler(0, 0, d * 90) * Vector3.right;
                    var contact = Box("Contact", root, center + direction * 0.125f + Vector3.back * 0.01f,
                        new Vector3(0.03f, 0.018f, 0.012f), trim);
                    contact.transform.localRotation = Quaternion.Euler(0, 0, d * 90);
                }
            }
            station.terminals = new[]
            {
                Box("BackupFeed", root, new Vector3(-0.22f, 0.18f, -0.07f), new Vector3(0.16f, 0.025f, 0.018f), signal).GetComponent<Renderer>(),
                Box("RecoveryFeed", root, new Vector3(0.46f, -0.1f, -0.07f), new Vector3(0.16f, 0.025f, 0.018f), signal).GetComponent<Renderer>()
            };
            Text(root, "BackupLabel", "BACKUP >", new Vector3(-0.13f, 0.35f, -0.05f), new Vector2(0.32f, 0.075f), 0.28f);
            Text(root, "RecoveryLabel", "> RECOVERY", new Vector3(0.39f, -0.27f, -0.05f), new Vector2(0.39f, 0.07f), 0.25f);
            station.statusLabel = Text(root, "PatchStatus", "Isolate the affected port before reconnecting the backup.",
                new Vector3(0, -0.60f, -0.045f), new Vector2(1.14f, 0.08f), 0.265f);
            station.modules = new PatchModule[3];
            var cartridge = Surface("MidnightCartridge", new Color(0.24f, 0.30f, 0.38f), 0.25f);
            for (int i = 0; i < station.modules.Length; i++)
                station.modules[i] = BuildModule(station, i, cartridge, trim, signal);

            var director = InScene<ChapterDirector>(parent.gameObject.scene).FirstOrDefault();
            if (director)
            {
                var steps = director.steps.Where(s => s.task != TaskId.SRV_RouteBackup).ToList();
                int index = steps.FindIndex(s => s.task == TaskId.SRV_Restore);
                if (index < 0) index = steps.Count;
                steps.Insert(index, new ChapterDirector.Step
                {
                    task = TaskId.SRV_RouteBackup,
                    targets = new[] { root },
                    label = "Recovery patch",
                    revealDelay = 12f
                });
                director.steps = steps.ToArray();
                EditorUtility.SetDirty(director);
            }
        }

        static PatchModule BuildModule(PatchRoutingStation station, int index, Material housing, Material trim, Material signal)
        {
            var tray = Child("CartridgeTray" + index, station.transform);
            tray.localPosition = new Vector3(-0.32f + index * 0.32f, -0.43f, -0.10f);
            tray.localRotation = Quaternion.Euler(0, 0, index == 2 ? 270 : 90);
            Box("TrayMagnet", station.transform, tray.localPosition + Vector3.forward * 0.04f,
                new Vector3(0.22f, 0.22f, 0.025f), trim);
            var moduleRoot = Child(index == 0 ? "StraightCartridge" : "CornerCartridge" + index, station.transform);
            moduleRoot.SetPositionAndRotation(tray.position, tray.rotation);
            var mesh = PlateMesh("RecoveryCartridge", 0.205f, 0.205f, 0.045f, 0.024f);
            MeshObject("CartridgeShell", moduleRoot, Vector3.zero, mesh, housing);
            var conductors = new List<Renderer>();
            conductors.Add(Box("EastGroove", moduleRoot, new Vector3(0.052f, 0, -0.029f), new Vector3(0.11f, 0.026f, 0.015f), signal).GetComponent<Renderer>());
            conductors.Add(Box(index == 0 ? "WestGroove" : "NorthGroove", moduleRoot,
                index == 0 ? new Vector3(-0.052f, 0, -0.029f) : new Vector3(0, 0.052f, -0.029f),
                index == 0 ? new Vector3(0.11f, 0.026f, 0.015f) : new Vector3(0.026f, 0.11f, 0.015f), signal).GetComponent<Renderer>());
            foreach (float x in new[] { -0.075f, 0.075f })
                Box("FingerRidge", moduleRoot, new Vector3(x, -0.067f, -0.035f), new Vector3(0.025f, 0.035f, 0.022f), trim);
            var collider = moduleRoot.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.21f, 0.21f, 0.075f);
            var body = moduleRoot.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = 0.12f;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            QuickActionsAPI.AddGrabInteraction(moduleRoot.gameObject);
            QuickActionsAPI.AddDistanceGrabInteraction(moduleRoot.gameObject, DistanceGrabMode.PullToHand);
            foreach (var snap in moduleRoot.GetComponentsInChildren<SnapInteractor>(true)) Object.DestroyImmediate(snap);
            var snapZone = moduleRoot.parent ? moduleRoot.parent.Find("ISDK_SnapZone (" + moduleRoot.name + ")") : null;
            if (snapZone) Object.DestroyImmediate(snapZone.gameObject);
            foreach (var reticle in moduleRoot.GetComponentsInChildren<Oculus.Interaction.DistanceReticles.ReticleDataMesh>(true)) Object.DestroyImmediate(reticle);
            var item = moduleRoot.gameObject.AddComponent<GrabItem>();
            item.kind = ItemKind.Plug;
            item.displayName = index == 0 ? "Straight patch cartridge" : "Corner patch cartridge";
            item.caption = "Turn the grooves to join the route. Release over a bay to fit the cartridge.";
            item.holdPose = GrabItem.HoldPose.Free;
            var module = moduleRoot.gameObject.AddComponent<PatchModule>();
            module.station = station;
            module.item = item;
            module.tray = tray;
            module.corner = index != 0;
            module.conductors = conductors.ToArray();
            return module;
        }

        static void BuildRail(Transform parent, Material housing, Material trim)
        {
            var root = Child("MaintenanceRail", parent);
            root.SetPositionAndRotation(new Vector3(-1.025f, 0.96f, 0.43f), Quaternion.Euler(0, 90, 0));
            var rail = root.gameObject.AddComponent<MaintenanceRail>();
            rail.railLength = 3.15f;
            rail.gripRadius = 0.18f;
            rail.maxSpeed = 0.65f;
            var bar = Shape("Handrail", root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.036f, 1.575f, 0.036f), trim);
            bar.transform.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (float z in new[] { -1.53f, 0f, 1.53f })
                Box("Mount", root, new Vector3(-0.04f, 0, z), new Vector3(0.08f, 0.04f, 0.04f), housing);
            var sign = Child("RailInstructions", parent);
            sign.SetPositionAndRotation(new Vector3(0.685f, 1.55f, 0.79f), Quaternion.Euler(0, -90, 0));
            Box("Backplate", sign, new Vector3(0, 0, 0.016f), new Vector3(0.61f, 0.4f, 0.025f), housing);
            Text(sign, "RailTitle", "MAINTENANCE RAIL", new Vector3(0, 0.14f, -0.005f), new Vector2(0.56f, 0.08f), 0.3f);
            var copy = Text(sign, "RailHowTo", "Grip the rail, then pull yourself along.\nRelease to stop.\n\nTeleport remains available.",
                new Vector3(0, -0.035f, -0.005f), new Vector2(0.54f, 0.25f), 0.255f);
            copy.fontStyle = FontStyles.Normal;
        }

        static void BuildRestoration(Scene scene, Transform root)
        {
            var response = root.gameObject.AddComponent<MidnightRoomResponse>();
            response.ceilingPanels = InScene<Renderer>(scene).Where(r => r.name == "CeilingPanel").ToArray();
            var panelMaterial = Surface("MidnightRestorationPanel", new Color(0.3f, 0.31f, 0.33f), 0.1f);
            panelMaterial.EnableKeyword("_EMISSION");
            foreach (var panel in response.ceilingPanels) panel.sharedMaterial = panelMaterial;
            response.emergencyLight = InScene<Light>(scene).FirstOrDefault(l => l.name == "EmergencyLight");
            response.serviceLights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var light = Child("RecoveredServiceLight" + i, root).gameObject.AddComponent<Light>();
                light.transform.position = new Vector3(i == 0 ? -1.4f : 2f, 2.65f, 0);
                light.type = LightType.Point;
                light.color = new Color(0.76f, 0.87f, 1f);
                light.range = 6f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                response.serviceLights[i] = light;
            }
        }

        static void MakeNightSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/NightCitySky.mat");
            if (!sky) return;
            sky.SetFloat("_Exposure", 0.22f);
            sky.SetColor("_Tint", new Color(0.24f, 0.34f, 0.52f, 1f));
            RenderSettings.skybox = sky;
            EditorUtility.SetDirty(sky);
        }

        static void ImproveSigns(Scene scene, Transform root, Material housing)
        {
            foreach (var text in InScene<TextMeshPro>(scene))
            {
                bool title = text.text == "SECURITY OPERATIONS CENTRE";
                bool exit = text.text == "FLOOR 3  >";
                bool badge = text.text.Contains("AT THE DOOR TO TAP YOUR BADGE") || text.text == "TAP YOUR BADGE ON THE READER";
                if (!title && !exit && !badge) continue;
                if (badge) text.text = "TAP YOUR BADGE ON THE READER";
                text.color = new Color(0.92f, 0.97f, 1f);
                var size = text.rectTransform.rect.size;
                var plate = Box("SignBackplate", root, Vector3.zero, new Vector3(size.x + 0.1f, size.y + 0.04f, 0.016f), housing);
                plate.transform.SetPositionAndRotation(text.transform.position + text.transform.forward * 0.015f, text.transform.rotation);
                EditorUtility.SetDirty(text);
            }
        }

        static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        }

        static Material Surface(string name, Color color, float metallic)
        {
            EnsureFolder(AssetFolder);
            string path = AssetFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", 0.3f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.16f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            return Shape(name, parent, PrimitiveType.Cube, position, size, material);
        }

        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            SetupRenderer(go.GetComponent<Renderer>(), material);
            return go;
        }

        static void SetupRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        static GameObject MeshObject(string name, Transform parent, Vector3 position, Mesh mesh, Material material, bool collider = false)
        {
            var root = Child(name, parent);
            root.localPosition = position;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            SetupRenderer(root.gameObject.AddComponent<MeshRenderer>(), material);
            if (collider) root.gameObject.AddComponent<BoxCollider>().size = mesh.bounds.size;
            return root.gameObject;
        }

        static Mesh PlateMesh(string name, float width, float height, float depth, float corner)
        {
            EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = !mesh;
            if (fresh) mesh = new Mesh { name = name };
            else mesh.Clear();
            float x = width * 0.5f, y = height * 0.5f;
            Vector2[] outline =
            {
                new Vector2(-x + corner, -y), new Vector2(x - corner, -y),
                new Vector2(x, -y + corner), new Vector2(x, y - corner),
                new Vector2(x - corner, y), new Vector2(-x + corner, y),
                new Vector2(-x, y - corner), new Vector2(-x, -y + corner)
            };
            var vertices = new Vector3[16];
            var uv = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                Vector2 point = outline[i % 8];
                vertices[i] = new Vector3(point.x, point.y, i < 8 ? -depth * 0.5f : depth * 0.5f);
                uv[i] = new Vector2(point.x / width + 0.5f, point.y / height + 0.5f);
            }
            var triangles = new List<int>();
            for (int i = 1; i < 7; i++)
            {
                triangles.AddRange(new[] { 0, i + 1, i, 8, i + 8, i + 9 });
            }
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                triangles.AddRange(new[] { i, next, next + 8, i, next + 8, i + 8 });
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static TextMeshPro Text(Transform parent, string name, string value, Vector3 position, Vector2 size, float fontSize)
        {
            var root = Child(name, parent);
            root.localPosition = position;
            var text = root.gameObject.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            text.text = value;
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * 0.9f;
            text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = new Color(0.9f, 0.95f, 1f);
            return text;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
