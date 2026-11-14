using UnityEngine;

namespace CSER
{


    public class FlickerLight : MonoBehaviour
    {
        public Light[] lights;
        public Renderer glow;
        public Color glowColor = new Color(1.5f, 1.6f, 1.8f);
        [Range(0.05f, 1f)] public float minLevel = 0.3f;

        float[] baseIntensity;
        MaterialPropertyBlock glowProperties;
        float next, level = 1f;
        int burstLeft;

        void Awake()
        {
            if (lights == null) lights = new Light[0];
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i]) baseIntensity[i] = lights[i].intensity;
            glowProperties = new MaterialPropertyBlock();
        }

        void Update()
        {
            bool steady = AlertStateManager.I ? AlertStateManager.I.ReduceFlashing : PlayerPrefs.GetInt("cser.reduceFlash", 0) == 1;
            if (steady)
            {
                burstLeft = 0;
                level = 1f;
                next = Time.time + 1.2f;
                Apply(level);
                return;
            }
            if (Time.time >= next)
            {
                if (burstLeft > 0)
                {
                    burstLeft--;
                    level = Random.value < 0.35f ? 1f : Random.Range(minLevel, 0.9f);
                    next = Time.time + Random.Range(0.22f, 0.38f);
                }
                else
                {
                    level = 1f;
                    next = Time.time + Random.Range(1.2f, 4.5f);
                    burstLeft = Random.Range(3, 6);
                }
            }
            Apply(level);
        }

        void Apply(float l)
        {
            for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].intensity = baseIntensity[i] * l;
            if (glow)
            {
                glow.GetPropertyBlock(glowProperties);
                glowProperties.SetColor("_EmissionColor", glowColor * l);
                glow.SetPropertyBlock(glowProperties);
            }
        }
    }
}
