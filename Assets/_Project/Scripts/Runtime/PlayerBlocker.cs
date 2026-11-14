using System.Collections.Generic;
using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace CSER
{
    public class PlayerBlocker : MonoBehaviour
    {
        [Min(0.1f)] public float radius = 0.22f;
        public float kneeHeight = 0.3f;
        public bool comfortFade = true;
        [Min(0.02f)] public float eyeRadius = 0.06f;
        public bool protectTeleport = true;

        OVRCameraRig rig;
        Transform env, props;
        readonly Collider[] overlaps = new Collider[96];
        readonly RaycastHit[] rayHits = new RaycastHit[96];
        readonly List<DestinationFilter> filters = new List<DestinationFilter>();
        float insideFor, hintCooldown;
        bool started;

        sealed class DestinationFilter
        {
            public TeleportInteractor interactor;
            public TeleportInteractor.AcceptDestinationComputer previous;
            public TeleportInteractor.AcceptDestinationComputer installed;
        }

        public bool Dimmed => comfortFade && insideFor > 0.1f;
        public int ProtectedTeleportInteractors => filters.Count;

        void Start()
        {
            rig = GetComponent<OVRCameraRig>();
            var environment = GameObject.Find("Environment");
            var furniture = GameObject.Find("Props");
            env = environment ? environment.transform : null;
            props = furniture ? furniture.transform : null;
            started = true;
            InstallFilters();
        }

        void OnEnable()
        {
            if (started) InstallFilters();
        }

        void OnDisable()
        {
            foreach (var filter in filters)
                if (filter.interactor && filter.interactor.AcceptDestination == filter.installed)
                    filter.interactor.AcceptDestination = filter.previous;
            filters.Clear();
            insideFor = 0f;
            if (Fader.I) Fader.I.SetComfort(0f);
        }

        void InstallFilters()
        {
            if (!rig || filters.Count > 0) return;
            foreach (var interactor in FindObjectsByType<TeleportInteractor>(FindObjectsInactive.Include))
            {
                var filter = new DestinationFilter { interactor = interactor, previous = interactor.AcceptDestination };
                filter.installed = (target, pose) =>
                    (filter.previous == null || filter.previous(target, pose)) &&
                    (!protectTeleport || AcceptDestination(filter.interactor, target, pose));
                interactor.AcceptDestination = filter.installed;
                filters.Add(filter);
            }
        }

        bool Blocks(Collider collider)
        {
            if (!collider || !collider.enabled || collider.isTrigger) return false;
            if (rig && collider.transform.IsChildOf(rig.transform)) return false;
            if (!(env && collider.transform.IsChildOf(env)) && !(props && collider.transform.IsChildOf(props))) return false;
            if (collider.GetComponentInParent<GrabItem>()) return false;
            return !collider.attachedRigidbody || collider.attachedRigidbody.isKinematic;
        }

        float StandingHeight => rig && rig.centerEyeAnchor
            ? Mathf.Clamp(rig.centerEyeAnchor.position.y - rig.transform.position.y + 0.1f, 0.65f, 2.6f)
            : 1.75f;

        bool AcceptDestination(TeleportInteractor interactor, TeleportInteractable target, Pose pose)
        {
            Vector3 feet = pose.position;
            if (target.EyeLevel) feet.y -= StandingHeight - 0.1f;
            if (!IsDestinationClear(feet)) return false;
            var arc = interactor.TeleportArc;
            if (arc == null) return false;
            for (int i = 1; i < arc.PointsCount; i++)
            {
                Vector3 from = arc.PointAtIndex(i - 1);
                Vector3 to = arc.PointAtIndex(i);
                Vector3 segment = to - from;
                float along = segment.sqrMagnitude > 0.000001f
                    ? Vector3.Dot(interactor.ArcEnd.Point - from, segment) / segment.sqrMagnitude : -1f;
                bool reached = along >= 0f && along <= 1.01f &&
                    (from + segment * along - interactor.ArcEnd.Point).sqrMagnitude < 0.0025f;
                if (reached) to = interactor.ArcEnd.Point;
                if (!IsPathClear(from, to)) return false;
                if (reached) break;
            }
            return true;
        }

        public bool IsDestinationClear(Vector3 feet)
        {
            if (!IsFinite(feet) || (!env && !props)) return false;
            float capsuleRadius = Mathf.Max(0.1f, radius);
            Vector3 bottom = feet + Vector3.up * (capsuleRadius + 0.025f);
            Vector3 top = feet + Vector3.up * Mathf.Max(capsuleRadius + 0.025f, StandingHeight - capsuleRadius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, capsuleRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(overlaps[i])) return false;

            count = Physics.RaycastNonAlloc(feet + Vector3.up * 0.15f, Vector3.down, rayHits, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Blocks(rayHits[i].collider) && rayHits[i].normal.y >= 0.7f) return true;
            return false;
        }

        public bool IsPathClear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f) return true;
            int count = Physics.RaycastNonAlloc(from, delta / distance, rayHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == rayHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = rayHits[i].collider;
                if (Blocks(collider) && collider.bounds.size.y > 0.25f) return false;
            }
            return true;
        }

        static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        void LateUpdate()
        {
            if (!rig || !rig.centerEyeAnchor) return;
            bool inside = false;
            if (comfortFade)
            {
                int count = Physics.OverlapSphereNonAlloc(rig.centerEyeAnchor.position, eyeRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (Blocks(overlaps[i])) { inside = true; break; }
            }
            insideFor = inside ? insideFor + Time.unscaledDeltaTime : 0f;
            if (Fader.I) Fader.I.SetComfort(Dimmed ? 0.7f : 0f);
            hintCooldown = Mathf.Max(0f, hintCooldown - Time.unscaledDeltaTime);
            if (Dimmed && hintCooldown <= 0f)
            {
                hintCooldown = 6f;
                GameUI.I?.Caption("Move your head back into the clear space", Palette.Ink);
            }
        }
    }
}
