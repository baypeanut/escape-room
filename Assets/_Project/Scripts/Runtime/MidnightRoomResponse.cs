using UnityEngine;

namespace CSER
{
    public sealed class MidnightRoomResponse : MonoBehaviour
    {
        public Renderer[] ceilingPanels;
        public Light emergencyLight;
        public Light[] serviceLights;

        MaterialPropertyBlock properties;
        float initialEmergency;
        float blend;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            initialEmergency = emergencyLight ? emergencyLight.intensity : 0f;
        }

        void Update()
        {
            var manager = GameManager.I;
            bool restored = manager && manager.IsDone(TaskId.SRV_Restore);
            blend = Mathf.MoveTowards(blend, restored ? 1f : 0f, Time.deltaTime * 0.4f);
            if (emergencyLight) emergencyLight.intensity = Mathf.Lerp(initialEmergency, 0.15f, blend);
            if (serviceLights != null)
                foreach (var light in serviceLights) if (light) light.intensity = 2.4f * blend;
            if (ceilingPanels == null) return;
            foreach (var panel in ceilingPanels)
            {
                if (!panel) continue;
                panel.GetPropertyBlock(properties);
                properties.SetColor(BaseId, Color.Lerp(new Color(0.3f, 0.31f, 0.33f), new Color(0.8f, 0.87f, 1f), blend));
                properties.SetColor(EmissionId, new Color(0.45f, 0.62f, 0.82f) * blend);
                panel.SetPropertyBlock(properties);
            }
        }
    }
}
