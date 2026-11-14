using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace CSER
{


    public class Flashlight : MonoBehaviour
    {
        public Light beam;
        public Light handGlow;
        public Renderer lens;
        public Light findMeGlow;
        public Color lensOn = new Color(2f, 1.9f, 1.5f);
        public Vector3 wristOffset = new Vector3(0.045f, -0.005f, 0.02f);

        GrabItem item;
        Rigidbody rb;
        Material lensMat;
        bool on;
        Transform homeParent;
        RigidbodyInterpolation homeInterpolation;
        readonly List<GameObject> grabObjects = new List<GameObject>();
        Grabbable[] grabbables;
        Collider[] colliders;

        void Awake()
        {
            item = GetComponent<GrabItem>();
            rb = GetComponent<Rigidbody>();
            homeParent = transform.parent;
            if (lens) lensMat = lens.material;
            beam.enabled = false;
            if (handGlow) handGlow.enabled = false;
            grabbables = GetComponentsInChildren<Grabbable>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name.StartsWith("ISDK_")) grabObjects.Add(t.gameObject);
            GrabItem.AnyGrabbed += OnGrab;
            GrabItem.AnyStored += OnStored;
        }

        void OnDestroy()
        {
            GrabItem.AnyGrabbed -= OnGrab;
            GrabItem.AnyStored -= OnStored;
        }

        void OnGrab(GrabItem g)
        {
            if (g != item) return;
            if (!on) Switch(true);
            if (findMeGlow) findMeGlow.enabled = false;
            Mount();
            GameManager.I.Complete(TaskId.SRV_Flashlight);
        }

        void OnStored(GrabItem g)
        {
            if (g == item) Unmount();
        }

        public void Switch(bool value)
        {
            on = value;
            beam.enabled = on;
            if (handGlow) handGlow.enabled = on;
            if (lensMat) lensMat.SetColor("_EmissionColor", on ? lensOn : Color.black);
            Sfx.Play(Sfx.I ? Sfx.I.click : null);
        }


        void Mount()
        {
            if (item.Mounted) return;
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (!rig || !rig.rightHandAnchor || !rig.leftHandAnchor) return;
            bool left = Vector3.Distance(transform.position, rig.leftHandAnchor.position) < Vector3.Distance(transform.position, rig.rightHandAnchor.position);
            var anchor = left ? rig.leftHandAnchor : rig.rightHandAnchor;

            item.Mounted = true;
            foreach (var g in grabbables) if (g) g.enabled = false;
            foreach (var o in grabObjects) if (o) o.SetActive(false);
            foreach (var c in colliders) if (c) c.enabled = false;
            homeInterpolation = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.isKinematic = true;
            transform.SetParent(anchor, false);
            var o2 = wristOffset;
            if (left) o2.x = -o2.x;
            transform.localPosition = o2;
            transform.localRotation = Quaternion.identity;
            GameUI.I?.Caption("Flashlight on your wrist. Point your hand to aim; click the right stick to switch it. Press X for inventory.", Palette.Ink, 6f);
        }

        void Unmount()
        {
            if (!item.Mounted) return;
            item.Mounted = false;
            transform.SetParent(homeParent, true);
            foreach (var c in colliders) if (c) c.enabled = true;
            foreach (var o in grabObjects) if (o) o.SetActive(true);
            foreach (var g in grabbables) if (g) g.enabled = true;
            rb.interpolation = homeInterpolation;
            rb.isKinematic = false;
        }

        void Update()
        {
            bool toggle = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) toggle = true;
#endif
            if (item.Mounted && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch)) toggle = true;
            if (toggle && item.EverHeld) Switch(!on);
        }

        void LateUpdate()
        {

            if (item.Mounted) transform.localRotation = Quaternion.identity;

            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localPosition = new Vector3(0, 0, 0.1f);
            if (handGlow) handGlow.transform.localPosition = new Vector3(0, 0.05f, 0.18f);
        }

        public void DebugGrab() => OnGrab(item);
    }
}
