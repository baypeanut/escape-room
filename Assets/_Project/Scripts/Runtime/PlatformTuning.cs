using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CSER
{


    public static class PlatformTuning
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (_, __) => Apply();
            Apply();
        }

        static void Apply()
        {
            Time.fixedDeltaTime = 1f / 72f;
            bool mobile = Application.platform == RuntimePlatform.Android;
            if (!mobile) return;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = false;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
            OVRManager.useDynamicFoveatedRendering = true;

            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            QualitySettings.vSyncCount = 0;
        }
    }
}
