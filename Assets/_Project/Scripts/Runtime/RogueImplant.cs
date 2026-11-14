using UnityEngine;

namespace CSER
{

    public class RogueImplant : MonoBehaviour
    {
        public Renderer led;
        public Material ledOn, ledOff;
        public Transform socket;
        public Transform plug;
        public Transform cableStart;
        public LineRenderer cable;
        public GameObject[] enableWhenUnplugged;
        public float unplugDistance = 0.12f;

        bool unplugged;
        Rigidbody body;

        void Start()
        {
            body = GetComponent<Rigidbody>();
            foreach (var g in enableWhenUnplugged) if (g) g.SetActive(false);
            if (body) body.isKinematic = true;
        }

        public void Inspect()
        {
            if (GameManager.I.Complete(TaskId.SRV_Find))
                GameUI.I?.Caption("Unknown device: Raspberry Pi, no asset tag, cable into switch port 14. This is the implant!", Palette.Ink, 6f);
        }

        void Update()
        {
            bool steady = AlertStateManager.I ? AlertStateManager.I.ReduceFlashing : PlayerPrefs.GetInt("cser.reduceFlash", 0) == 1;
            if (led && !unplugged) led.sharedMaterial = steady || Mathf.Repeat(Time.time, 0.6f) < 0.3f ? ledOn : ledOff;
            UpdateCable();

            if (unplugged && plug && plug.TryGetComponent<Rigidbody>(out var prb) && prb.isKinematic
                && plug.TryGetComponent<GrabItem>(out var pi) && !pi.IsHeld)
                prb.isKinematic = false;
            if (unplugged || !plug || !socket || !GameManager.I || GameManager.I.State != GameState.Playing) return;
            if (Vector3.Distance(plug.position, socket.position) > unplugDistance)
            {
                unplugged = true;
                if (!GameManager.I.IsDone(TaskId.SRV_Find)) Inspect();
                led.sharedMaterial = ledOff;
                Sfx.PlayAt(Sfx.I ? Sfx.I.drop : null, socket.position);
                foreach (var g in enableWhenUnplugged) if (g) g.SetActive(true);
                if (body) body.isKinematic = false;
                GameManager.I.Complete(TaskId.SRV_Unplug);
            }
        }


        void UpdateCable()
        {
            if (!cable || !cableStart || !plug) return;
            const int n = 16;
            cable.positionCount = n;
            var a = cableStart.position;
            var b = plug.position;
            float sag = Mathf.Clamp(Vector3.Distance(a, b) * 0.25f, 0.03f, 0.35f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                cable.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4 * t * (1 - t)));
            }
        }

        public void DebugUnplug()
        {
            if (plug) plug.position = socket.position + Vector3.forward * 0.3f;
        }
    }
}
