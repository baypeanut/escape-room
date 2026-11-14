using UnityEngine;

namespace CSER
{


    public class KeyboardGrab : MonoBehaviour
    {
        public float reach = 3f;

        public static KeyboardGrab I { get; private set; }
        public bool HasItem => held != null;

        void Awake() => I = this;
        void OnDisable() => Release();
        void OnDestroy() { Release(); if (I == this) I = null; }
        void OnApplicationPause(bool paused) { if (paused) Release(); }
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }

        OVRCameraRig rig;
        GrabItem held;

        void Update()
        {
            if (!rig) rig = FindAnyObjectByType<OVRCameraRig>();
            if (held && (!held.ForceHeld || held.Mounted || held.Stored || held.Consumed || held.Deposited || !held.gameObject.activeInHierarchy)) Forget();
            if (!rig || GameManager.I == null || GameManager.I.State != GameState.Playing) { Release(); return; }
            if (held && OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) Release();

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Grab();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Release();
#endif
        }

        void LateUpdate()
        {
            if (!held || !held.ForceHeld || held.Mounted || !rig || !rig.rightHandAnchor) return;

            var t = rig.rightHandAnchor;
            held.transform.SetPositionAndRotation(t.position + t.forward * 0.18f, t.rotation);
        }

        GrabItem Find(Transform from)
        {
            if (!from) return null;
            if (Physics.SphereCast(from.position, 0.1f, from.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var g = hit.collider.GetComponentInParent<GrabItem>();
                if (g && !g.Stored && !g.Consumed && !g.Deposited && g.gameObject.activeInHierarchy && g.kind != ItemKind.Plug) return g;
            }

            GrabItem best = null; float bd = 0.8f;
            foreach (var g in FindObjectsByType<GrabItem>(FindObjectsInactive.Exclude))
            {
                if (g.Stored || g.Consumed || g.Deposited || g.Mounted || g.kind == ItemKind.Plug) continue;
                float d = Vector3.Distance(g.transform.position, from.position);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        void Grab()
        {
            if (held) return;
            var g = Find(rig.rightHandAnchor) ?? Find(rig.centerEyeAnchor);
            if (!g) { GameUI.I?.Caption("Nothing to grab. Point at an item and press 4.", Palette.Ink, 2.5f); return; }
            Hold(g);
        }


        public bool Hold(GrabItem g)
        {
            if (held || !g || !GameManager.I || GameManager.I.State != GameState.Playing || g.IsHeld || g.Stored || g.Mounted || g.Consumed || g.Deposited || !g.gameObject.activeInHierarchy) return false;
            held = g;
            var rb = g.GetComponent<Rigidbody>();
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            g.ForceHeld = true;
            g.EverHeld = true;
            return true;
        }

        void Release()
        {
            if (!held) return;
            var g = held;
            Forget();
            if (g.Mounted || g.Stored || g.Consumed || g.Deposited || !g.gameObject.activeInHierarchy) return;
            var rb = g.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
            Sfx.Play(Sfx.I ? Sfx.I.drop : null, 0.6f);
        }

        void Forget()
        {
            if (held) held.ForceHeld = false;
            held = null;
        }
    }
}
