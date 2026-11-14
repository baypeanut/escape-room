using System;
using System.Collections;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CSER
{

    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(100)]
    public class GrabItem : MonoBehaviour
    {
        public enum HoldPose { Free, Inspect, Read }

        public ItemKind kind;
        [Tooltip("While held: Free = as grabbed, Inspect = top face turned to the eyes, Read = document upright facing the eyes at reading distance")]
        public HoldPose holdPose = HoldPose.Free;
        public string displayName = "Item";
        [TextArea] public string caption;
        [TextArea] public string wrongZoneHint;

        public static event Action<GrabItem> AnyGrabbed;
        public static event Action<GrabItem> AnyStored;

        public bool IsHeld => !Consumed && !Stored && !Deposited && (ForceHeld || HandHeld);
        public bool Consumed { get; private set; }

        public bool EverHeld { get; set; }

        public bool Mounted { get; set; }
        public bool Stored { get; private set; }

        public bool ForceHeld { get; set; }

        public bool Deposited { get; private set; }

        public bool CanStore => kind != ItemKind.Plug && !Consumed && !Stored && !Deposited && (!Mounted || kind == ItemKind.Flashlight);
        public bool CanRecover => gameObject.activeInHierarchy && !IsHeld && !Mounted && !Stored && !Consumed && !Deposited && kind != ItemKind.Plug;
        public Vector3 HomePosition => homePos;

        Grabbable[] grabbables;
        Rigidbody rb;
        Vector3 homePos;
        Quaternion homeRot;
        float rejectCooldown, settleFor;
        bool wasHeld;

        bool HandHeld
        {
            get
            {
                if (grabbables == null) return false;
                foreach (var grabbable in grabbables)
                    if (grabbable && grabbable.isActiveAndEnabled && grabbable.SelectingPointsCount > 0) return true;
                return false;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabbables = GetComponentsInChildren<Grabbable>(true);
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        void Update()
        {
            if (HandHeld) ForceHeld = false;
            bool held = IsHeld;

            if (held != wasHeld)
            {
                wasHeld = held;
                if (held)
                {
                    EverHeld = true;
                    Sfx.Play(Sfx.I ? Sfx.I.pickup : null, 0.5f);
                    Sfx.Haptic();
                    if (!string.IsNullOrEmpty(caption)) GameUI.I?.Caption(caption, Palette.Ink);
                    AnyGrabbed?.Invoke(this);
                }
                else settleFor = 2f;
            }

            if (rejectCooldown > 0) rejectCooldown -= Time.deltaTime;

            bool free = EverHeld && !held && !Mounted && !Stored && !Consumed && !Deposited && kind != ItemKind.Plug;
            if (free && rb.isKinematic)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
            }


            if (settleFor > 0)
            {
                settleFor -= Time.deltaTime;
                if (free && !rb.isKinematic && rb.IsSleeping()) rb.WakeUp();
            }

            if (CanRecover && (transform.position.y < homePos.y - 2f || (transform.position - homePos).sqrMagnitude > 144f)) ResetHome();
        }

        static Transform eyes;
        Quaternion poseRot;
        bool posing;


        void LateUpdate()
        {
            bool active = holdPose != HoldPose.Free && IsHeld && !Consumed;
            if (!active) { posing = false; return; }
            if (!eyes) { var cam = Camera.main; if (!cam) return; eyes = cam.transform; }
            Vector3 p = transform.position;
            Vector3 toEye = eyes.position - p;
            if (holdPose == HoldPose.Read)
            {

                float dist = toEye.magnitude;
                float want = Mathf.Clamp(dist, 0.3f, 0.5f);
                if (dist > 1e-3f && !Mathf.Approximately(dist, want)) p = eyes.position - toEye / dist * want;
                toEye = eyes.position - p;
            }
            if (toEye.sqrMagnitude < 1e-6f) return;
            Vector3 up = Vector3.ProjectOnPlane(eyes.up, toEye);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, toEye);
            var target = Quaternion.LookRotation(up.normalized, toEye.normalized);
            if (!posing) poseRot = transform.rotation;
            poseRot = Quaternion.Slerp(poseRot, target, 1 - Mathf.Exp(-14f * Time.deltaTime));
            posing = true;
            transform.SetPositionAndRotation(p, poseRot);
        }


        public void Store()
        {
            if (!CanStore) return;
            AnyStored?.Invoke(this);
            ForceHeld = false;
            wasHeld = false;
            Mounted = false;
            Stored = true;
            gameObject.SetActive(false);
        }


        public void Restore(Vector3 pos, Quaternion rot)
        {
            if (Consumed || Deposited) return;
            Stored = false;
            ForceHeld = false;
            wasHeld = false;
            gameObject.SetActive(true);
            EverHeld = true;
            Teleport(pos, rot);
        }


        public void Deposit(Bounds zone)
        {
            if (Deposited || Consumed) return;
            Deposited = true;
            ForceHeld = false;
            var p = transform.position;
            var e = zone.extents * 0.6f;
            p = new Vector3(Mathf.Clamp(p.x, zone.center.x - e.x, zone.center.x + e.x), Mathf.Clamp(p.y, zone.center.y - e.y, zone.center.y + e.y), Mathf.Clamp(p.z, zone.center.z - e.z, zone.center.z + e.z));
            Teleport(p, transform.rotation);
            rb.isKinematic = true;
            foreach (var g in grabbables) if (g) g.enabled = false;
        }

        public void ResetHome()
        {
            if (!CanRecover) return;
            ForceHeld = false;
            wasHeld = false;
            rejectCooldown = 0f;
            Teleport(homePos, homeRot);
            if (EverHeld)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }
        }


        public void Teleport(Vector3 pos, Quaternion rot)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = pos;
            rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
        }

        public bool CanBeRejected => rejectCooldown <= 0;


        public void Eject(Bounds zone, Vector3 playerPos)
        {
            rejectCooldown = 1.5f;
            var dir = Vector3.ProjectOnPlane(playerPos - zone.center, Vector3.up);
            dir = dir.sqrMagnitude < 1e-4f ? Vector3.back : dir.normalized;
            float reach = new Vector2(zone.extents.x, zone.extents.z).magnitude + 0.12f;
            Teleport(zone.center + dir * reach + Vector3.up * (zone.extents.y + 0.05f), transform.rotation);
        }

        public void Consume(Transform into = null, float duration = 0.6f, float sink = 0.25f)
        {
            if (Consumed) return;
            Consumed = true;
            ForceHeld = false;
            wasHeld = false;
            foreach (var g in grabbables) if (g) g.enabled = false;
            StartCoroutine(ConsumeRoutine(into, duration, sink));
        }

        IEnumerator ConsumeRoutine(Transform into, float duration, float sink)
        {
            rb.isKinematic = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 start = transform.position;
            Vector3 end = into ? into.position : start;
            Quaternion startRot = transform.rotation;
            Quaternion endRot = into ? into.rotation : startRot;
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                float e = t * t * (3 - 2 * t);
                transform.SetPositionAndRotation(Vector3.Lerp(start, end, e), Quaternion.Slerp(startRot, endRot, e));
                yield return null;
            }
            Vector3 p = transform.position;
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                transform.position = p + Vector3.down * sink * t;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
