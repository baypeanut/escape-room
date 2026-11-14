using System.Collections;
using UnityEngine;

namespace CSER
{

    public class Fader : MonoBehaviour
    {
        public static Fader I { get; private set; }
        public Material material;
        public float fadeInSeconds = 1.2f;

        Material instance;
        Renderer quad;
        float fade;
        float comfort, comfortTarget;


        void OnDestroy() { if (I == this) I = null; }

        void Awake()
        {
            I = this;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "FadeQuad";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, 0.08f);
            go.transform.localScale = new Vector3(1f, 1f, 1f);
            quad = go.GetComponent<Renderer>();
            instance = new Material(material);
            quad.sharedMaterial = instance;
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            SetAlpha(1f);
        }

        IEnumerator Start()
        {
            yield return null;
            yield return FadeTo(0f, fadeInSeconds);
        }


        public void SetComfort(float alpha) => comfortTarget = Mathf.Clamp(alpha, 0f, 0.75f);

        void Update()
        {
            if (Mathf.Approximately(comfort, comfortTarget)) return;
            comfort = Mathf.MoveTowards(comfort, comfortTarget, Time.unscaledDeltaTime / 0.2f);
            Apply();
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float start = fade;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                SetAlpha(Mathf.Lerp(start, target, t * t * (3 - 2 * t)));
                yield return null;
            }
            SetAlpha(target);
        }

        void SetAlpha(float a)
        {
            fade = a;
            Apply();
        }

        void Apply()
        {
            float a = Mathf.Max(fade, comfort);
            instance.SetColor("_BaseColor", new Color(0, 0, 0, a));
            quad.enabled = a > 0.001f;
        }
    }
}
