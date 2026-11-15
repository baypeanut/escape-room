using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace CSER
{
    public class MaintenanceRail : MonoBehaviour
    {
        [Min(0.3f)] public float railLength = 3.15f;
        [Range(0.08f, 0.3f)] public float gripRadius = 0.18f;
        [Range(0.1f, 1.5f)] public float maxSpeed = 1.05f;
        [Range(0.5f, 1.5f)] public float pullScale = 1f;
        public bool allowPull = true;

        public readonly struct HandSample
        {
            public readonly bool tracked, grip;
            public readonly Vector3 worldPosition, trackingPosition;

            public HandSample(bool tracked, bool grip, Vector3 worldPosition, Vector3 trackingPosition)
            {
                this.tracked = tracked;
                this.grip = grip;
                this.worldPosition = worldPosition;
                this.trackingPosition = trackingPosition;
            }
        }

        OVRCameraRig rig;
        Transform trackingOrigin, head;
        ILocomotionEventHandler locomotor;
        PlayerBlocker blocker;
        GrabItem[] items;
        Vector3 previousHand;
        bool leftWasGripped, rightWasGripped;
        OVRInput.Controller hand = OVRInput.Controller.None;
        Quaternion previousTrackingRotation;
        bool initialized;
        UniqueIdentifier identifier;
        float nextRailCue;

        public bool IsPulling => hand != OVRInput.Controller.None;
        public Vector3 LastRequestedMovement { get; private set; }
        public Vector3 Axis => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 StartPoint => transform.position - Axis * railLength * 0.5f;
        public Vector3 EndPoint => transform.position + Axis * railLength * 0.5f;

        void Awake() => identifier = UniqueIdentifier.Generate(Context.Global.GetInstance(), this);

        void Start()
        {
            if (initialized) return;
            rig = FindAnyObjectByType<OVRCameraRig>();
            var mover = FindAnyObjectByType<FirstPersonLocomotor>();
            if (!rig || !mover) return;
            blocker = rig.GetComponent<PlayerBlocker>();
            Initialize(rig.trackingSpace, rig.centerEyeAnchor, mover);
        }

        public void Initialize(Transform origin, Transform playerHead, ILocomotionEventHandler handler)
        {
            StopPull();
            if (locomotor != null) locomotor.WhenLocomotionEventHandled -= OnLocomotion;
            trackingOrigin = origin;
            head = playerHead;
            locomotor = handler;
            if (locomotor != null) locomotor.WhenLocomotionEventHandled += OnLocomotion;
            items = FindObjectsByType<GrabItem>(FindObjectsInactive.Include);
            initialized = origin && playerHead && handler != null;
        }

        void Update()
        {
            if (!rig || !initialized) return;
            ProcessInput(ReadHand(rig.leftHandAnchor, OVRInput.Controller.LTouch),
                ReadHand(rig.rightHandAnchor, OVRInput.Controller.RTouch), Time.unscaledDeltaTime);
        }

        HandSample ReadHand(Transform anchor, OVRInput.Controller controller)
        {
            bool tracked = anchor && OVRInput.GetControllerPositionTracked(controller) && OVRInput.GetControllerOrientationTracked(controller);
            bool grip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controller);
            Vector3 world = anchor ? anchor.position : Vector3.zero;
            Vector3 local = anchor ? trackingOrigin.InverseTransformPoint(world) : Vector3.zero;
            return new HandSample(tracked, grip, world, local);
        }

        public void ProcessInput(HandSample left, HandSample right, float deltaTime)
        {
            LastRequestedMovement = Vector3.zero;
            bool leftPressed = left.grip && !leftWasGripped;
            bool rightPressed = right.grip && !rightWasGripped;
            leftWasGripped = left.grip;
            rightWasGripped = right.grip;
            if (!initialized || !isActiveAndEnabled || !allowPull || !GameManager.I || GameManager.I.State != GameState.Playing ||
                deltaTime <= 0f || deltaTime > 0.1f || (Hub.I && Hub.I.IsOpen) || AnyItemHeld())
            {
                StopPull();
                return;
            }

            if (!IsPulling)
            {
                if (leftPressed && left.tracked && IsNearRail(left.worldPosition, gripRadius)) BeginPull(left, OVRInput.Controller.LTouch);
                else if (rightPressed && right.tracked && IsNearRail(right.worldPosition, gripRadius)) BeginPull(right, OVRInput.Controller.RTouch);
                return;
            }

            var sample = hand == OVRInput.Controller.LTouch ? left : right;
            if (!sample.tracked || !sample.grip || !IsNearRail(sample.worldPosition, gripRadius * 2f) ||
                Quaternion.Angle(previousTrackingRotation, trackingOrigin.rotation) > 1f)
            {
                StopPull();
                return;
            }

            Vector3 physicalDelta = previousHand - sample.trackingPosition;
            previousHand = sample.trackingPosition;
            previousTrackingRotation = trackingOrigin.rotation;
            if (physicalDelta.sqrMagnitude > 0.04f)
            {
                StopPull();
                return;
            }

            Vector3 axis = Axis;
            float requested = Vector3.Dot(trackingOrigin.TransformVector(physicalDelta), axis) * pullScale;
            float maxStep = Mathf.Max(0.1f, maxSpeed) * Mathf.Min(deltaTime, 0.05f);
            requested = Mathf.Clamp(requested, -maxStep, maxStep);
            float current = Vector3.Dot(head.position - StartPoint, axis);
            float next = Mathf.Clamp(current + requested, Mathf.Min(0f, current), Mathf.Max(railLength, current));
            Vector3 movement = axis * (next - current);
            if (movement.sqrMagnitude < 0.00000001f) return;
            if (blocker)
            {
                Vector3 feet = head.position + movement;
                feet.y = rig.transform.position.y;
                if (!blocker.IsDestinationClear(feet)) return;
            }
            LastRequestedMovement = movement;
            locomotor.HandleLocomotionEvent(new LocomotionEvent(identifier.ID, movement, LocomotionEvent.TranslationType.Relative));
            float strength = Mathf.Clamp(0.12f + movement.magnitude * 8f, 0.12f, 0.35f);
            Sfx.Haptic(strength, 0.03f, hand);
            if (Sfx.I && Sfx.I.drop && movement.sqrMagnitude > 0.00005f && Time.unscaledTime >= nextRailCue)
            {
                nextRailCue = Time.unscaledTime + 0.18f;
                Sfx.PlayAt(Sfx.I.drop, sample.worldPosition, 0.12f);
            }
        }

        bool AnyItemHeld()
        {
            if (items == null) return false;
            foreach (var item in items)
                if (item && item.IsHeld && !item.Mounted) return true;
            return false;
        }

        void BeginPull(HandSample sample, OVRInput.Controller controller)
        {
            hand = controller;
            previousHand = sample.trackingPosition;
            previousTrackingRotation = trackingOrigin.rotation;
            Sfx.Haptic(0.25f, 0.04f, controller);
        }

        public bool IsNearRail(Vector3 worldPosition, float tolerance)
        {
            Vector3 axis = Axis;
            if (axis.sqrMagnitude < 0.9f) return false;
            float distance = Mathf.Clamp(Vector3.Dot(worldPosition - StartPoint, axis), 0f, railLength);
            Vector3 closest = StartPoint + axis * distance;
            return (worldPosition - closest).sqrMagnitude <= tolerance * tolerance;
        }

        public void StopPull()
        {
            hand = OVRInput.Controller.None;
            LastRequestedMovement = Vector3.zero;
        }

        void OnLocomotion(LocomotionEvent motion, Pose delta)
        {
            if (motion.Translation == LocomotionEvent.TranslationType.Absolute ||
                motion.Translation == LocomotionEvent.TranslationType.AbsoluteEyeLevel ||
                motion.Rotation != LocomotionEvent.RotationType.None) StopPull();
        }

        void OnDisable() => StopPull();
        void OnDestroy()
        {
            StopPull();
            if (locomotor != null) locomotor.WhenLocomotionEventHandled -= OnLocomotion;
            if (identifier != null) UniqueIdentifier.Release(identifier);
        }
        void OnApplicationPause(bool paused) { if (paused) StopPull(); }
        void OnApplicationFocus(bool focused) { if (!focused) StopPull(); }
    }
}
