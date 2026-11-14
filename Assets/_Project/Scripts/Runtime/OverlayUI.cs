using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CSER
{


    public static class OverlayUI
    {
        static readonly Dictionary<int, Material> imageMats = new Dictionary<int, Material>();
        static readonly Dictionary<(Material, int), Material> textMats = new Dictionary<(Material, int), Material>();
        static bool textLookupDone;
        static Shader textShader;

        public static void Apply(Transform root, int queue = 4000)
        {
            if (!root) return;
            if (!textLookupDone)
            {
                textLookupDone = true;


                var refMat = Resources.Load<Material>("Shaders/TMP_OverlayMobile");
                if (!refMat) refMat = Resources.Load<Material>("Shaders/TMP_Overlay");
                textShader = refMat ? refMat.shader : null;
                if (!textShader) textShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
                if (!textShader) textShader = Shader.Find("TextMeshPro/Distance Field Overlay");
                if (!textShader) Debug.LogWarning("[OverlayUI] TextMeshPro overlay shader missing from the build: popups draw without overlay");
            }


            if (!textShader) return;
            var shader = Resources.Load<Shader>("Shaders/CSER_UIOverlay");
            if (shader)
            {
                if (!imageMats.TryGetValue(queue, out var im) || !im)
                    imageMats[queue] = im = new Material(shader) { name = "CSER_UIOverlay " + queue, renderQueue = queue };
                foreach (var img in root.GetComponentsInChildren<Image>(true)) img.material = im;
            }
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var src = t.fontSharedMaterial;
                if (!src || src.shader == textShader) continue;
                if (!textMats.TryGetValue((src, queue), out var tm) || !tm)
                {
                    tm = new Material(src) { name = src.name + " (overlay " + queue + ")" };
                    tm.shader = textShader;
                    tm.renderQueue = queue + 1;
                    textMats[(src, queue)] = tm;
                }
                t.fontSharedMaterial = tm;
            }
        }
    }
}
