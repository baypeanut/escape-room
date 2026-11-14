using System.Collections.Generic;
using System.Linq;
using CSER;
using UnityEditor.Events;
using UnityEngine;

namespace CSER.EditorTools
{
    public static partial class EscapeRoomBuilder
    {


        static void FredDrawers()
        {
            Transform desk = null;
            foreach (Transform t in env)
                if (t.name.StartsWith("metal_office_desk") && t.position.x > 0) desk = t;
            if (!desk) { log.Add("Fred's desk not found: no drawers"); return; }

            var drawers = new List<Transform>();
            foreach (Transform c in desk)
                if (c.name.Contains("_drawer_") && c.GetComponent<Renderer>()) drawers.Add(c);

            drawers = drawers.OrderByDescending(d => d.GetComponent<Renderer>().bounds.center.y).ThenBy(d => d.GetComponent<Renderer>().bounds.center.x).ToList();

            for (int i = 0; i < drawers.Count; i++)
            {
                var d = drawers[i];
                var b = d.GetComponent<Renderer>().bounds;


                UnityEditor.GameObjectUtility.SetStaticEditorFlags(d.gameObject, 0);
                if (d.TryGetComponent<MeshRenderer>(out var mr)) mr.receiveGI = UnityEngine.ReceiveGI.LightProbes;
                var col = d.gameObject.AddComponent<BoxCollider>();
                col.center = d.InverseTransformPoint(b.center);
                var ls = d.InverseTransformVector(b.size);
                col.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));

                var slide = d.gameObject.AddComponent<SlideDrawer>();
                slide.openOffset = d.parent.InverseTransformVector(new Vector3(0, 0, -Mathf.Min(0.28f, b.size.z * 0.7f)));
                var click = MakeClickable(d.gameObject, null);
                UnityEventTools.AddPersistentListener(click.onClick, slide.Toggle);
                FillDrawer(d, b, i);
                log.Add($"Drawer {d.name}: {b.min:F2} - {b.max:F2}");
            }
        }

        static void FillDrawer(Transform drawer, Bounds b, int index)
        {
            var contents = new GameObject("Contents").transform;
            contents.SetParent(drawer, false);

            contents.SetPositionAndRotation(new Vector3(b.center.x, b.min.y + 0.012f, b.min.z + 0.15f), Quaternion.identity);
            var s = contents.lossyScale;
            contents.localScale = new Vector3(contents.localScale.x / s.x, contents.localScale.y / s.y, contents.localScale.z / s.z);
            float w = b.size.x * 0.32f, dz = 0.08f;
            var ink = new Color(0.12f, 0.12f, 0.16f);

            GameObject Item(string name, Vector3 pos, Vector3 size, Color c, float yaw = 0)
            {
                var go = LocalBox(name, contents, pos + Vector3.up * size.y / 2, size, Mat("Drawer_" + name, c, 0.3f), false);
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                return go;
            }
            void Text(Transform on, string text, Vector2 size)
            {

                var top = on.position + Vector3.up * (on.lossyScale.y / 2 + 0.0015f);
                Label(on, text, top, on.rotation * Quaternion.Euler(90, 0, 0), size, ink, world: true);
            }

            switch (index)
            {
                case 0:
                    Text(Item("PaperStack", new Vector3(-w * 0.4f, 0, 0), new Vector3(0.15f, 0.02f, 0.2f), new Color(0.95f, 0.94f, 0.9f)).transform, "Meeting notes", new Vector2(0.12f, 0.04f));
                    Item("Pen", new Vector3(w * 0.6f, 0, -dz * 0.3f), new Vector3(0.012f, 0.012f, 0.14f), new Color(0.15f, 0.3f, 0.75f), 10);
                    Item("StickyPad", new Vector3(w * 0.6f, 0, dz * 0.5f), new Vector3(0.076f, 0.015f, 0.076f), new Color(0.98f, 0.88f, 0.3f), -8);
                    break;
                case 1:
                    var frame = Item("FamilyPhoto", new Vector3(-w * 0.3f, 0, 0), new Vector3(0.16f, 0.012f, 0.12f), new Color(0.35f, 0.22f, 0.12f), 6).transform;
                    LocalBox("Photo", frame, new Vector3(0, 0.55f, 0), new Vector3(0.85f, 0.1f, 0.8f), Mat("Drawer_PhotoPrint", new Color(0.55f, 0.72f, 0.85f), 0.6f), false);
                    Text(frame, "Lake day 2025 - the whole family", new Vector2(0.13f, 0.05f));
                    Text(Item("Chocolate", new Vector3(w * 0.65f, 0, 0), new Vector3(0.06f, 0.012f, 0.14f), new Color(0.38f, 0.2f, 0.1f), -5).transform, "CHOC", new Vector2(0.05f, 0.03f));
                    break;
                case 2:
                    Text(Item("BirthdayCard", new Vector3(-w * 0.35f, 0, 0), new Vector3(0.15f, 0.006f, 0.1f), new Color(0.95f, 0.75f, 0.8f), 4).transform, "Happy birthday Fred!", new Vector2(0.13f, 0.05f));
                    Item("GlassesCase", new Vector3(w * 0.6f, 0, 0), new Vector3(0.06f, 0.035f, 0.15f), new Color(0.15f, 0.15f, 0.18f));
                    break;
                case 3:
                    Text(Item("BusinessCards", new Vector3(0, 0, 0), new Vector3(0.09f, 0.025f, 0.055f), new Color(0.97f, 0.97f, 0.97f)).transform, "F. Jansen", new Vector2(0.07f, 0.03f));
                    Item("Mints", new Vector3(w * 0.7f, 0, dz * 0.3f), new Vector3(0.05f, 0.015f, 0.08f), new Color(0.3f, 0.7f, 0.55f), 12);
                    break;
                default:
                    Text(Item("Folder", new Vector3(0, 0, 0), new Vector3(0.22f, 0.01f, 0.28f), new Color(0.3f, 0.45f, 0.3f)).transform, index == 4 ? "Receipts 2025" : "Manuals", new Vector2(0.18f, 0.05f));
                    break;
            }
        }
    }
}
