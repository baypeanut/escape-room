using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CSER.EditorTools
{
    public static class InteractionRegression
    {
        sealed class RecordedLocomotion : ILocomotionEventHandler
        {
            public readonly List<LocomotionEvent> Events = new List<LocomotionEvent>();
            public event Action<LocomotionEvent, Pose> WhenLocomotionEventHandled = delegate { };

            public void HandleLocomotionEvent(LocomotionEvent motion)
            {
                Events.Add(motion);
                WhenLocomotionEventHandled.Invoke(motion, motion.Pose);
            }
        }

        public static IEnumerator Run(Action<bool, string> check)
        {
            check(Application.isPlaying && GameManager.I && GameManager.I.State == GameState.Playing,
                "interaction regression starts in a live playing chapter");
            if (!Application.isPlaying || !GameManager.I || GameManager.I.State != GameState.Playing) yield break;
            if (Hub.I && Hub.I.IsOpen) Hub.I.Close();
            var fixture = new GameObject("InteractionRegressionFixtures");
            var environment = GameObject.Find("Environment");
            if (environment) fixture.transform.SetParent(environment.transform);
            float timeScale = Time.timeScale;
            try
            {
                yield return ItemChecks(fixture.transform, check);
                yield return MotionChecks(fixture.transform, check);
                yield return RailChecks(fixture.transform, check);
                if (Sfx.I)
                {
                    Sfx.Haptic(0.2f, 0.05f);
                    check(Sfx.I.HapticsActive, "haptic pulse starts through the production feedback API");
                    Time.timeScale = 0f;
                    yield return Wait(0.12f);
                    check(!Sfx.I.HapticsActive, "haptic pulse ends while game time is paused");
                    Time.timeScale = timeScale;
                    Sfx.Haptic(0.2f, 0.5f);
                    Sfx.I.enabled = false;
                    check(!Sfx.I.HapticsActive, "disabling feedback stops an active haptic pulse");
                    Sfx.I.enabled = true;
                    Sfx.Haptic(0.2f, 0.5f);
                    Sfx.I.SendMessage("OnApplicationFocus", false);
                    check(!Sfx.I.HapticsActive, "focus loss stops controller vibration");
                }
            }
            finally
            {
                Time.timeScale = timeScale;
                if (Sfx.I) { Sfx.I.StopHaptics(); Sfx.I.enabled = true; }
                if (KeyboardGrab.I) { KeyboardGrab.I.enabled = false; KeyboardGrab.I.enabled = true; }
                fixture.SetActive(false);
                Object.Destroy(fixture);
            }
            yield return null;
        }

        static IEnumerator ItemChecks(Transform parent, Action<bool, string> check)
        {
            var zoneObject = new GameObject("RegressionDropZone");
            zoneObject.transform.SetParent(parent);
            zoneObject.transform.position = new Vector3(20f, 1f, 0f);
            var zoneCollider = zoneObject.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            var zone = zoneObject.AddComponent<DropZone>();
            zone.accepts = new[] { ItemKind.Prop };
            int accepted = 0;
            zone.Accepted += item => { accepted++; item.Deposit(zoneCollider.bounds); };
            var item = MakeItem(parent, "Loose test item", new Vector3(20f, 1f, 1f), false);
            var native = MakeItem(parent, "Native selected item", new Vector3(20f, 1f, 2f), true);
            yield return null;

            check(!zone.TryAccept(item) && accepted == 0, "drop callback rejects an item never picked up");
            var keyboard = KeyboardGrab.I;
            check(keyboard && keyboard.Hold(item), "inventory hand adapter can acquire an available item");
            check(item.IsHeld && !zone.TryAccept(item), "drop callback sees forced grip immediately before Update");
            keyboard.enabled = false;
            check(!item.IsHeld && !keyboard.HasItem && !item.GetComponent<Rigidbody>().isKinematic,
                "disabling the hand adapter releases the item with physics restored");
            keyboard.enabled = true;

            native.EverHeld = true;
            var grabbable = native.GetComponent<Grabbable>();
            Pose pointer = new Pose(native.transform.position, native.transform.rotation);
            grabbable.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Hover, pointer));
            grabbable.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Select, pointer));
            check(grabbable.SelectingPointsCount > 0 && native.IsHeld, "native SDK selection is visible without waiting for GrabItem.Update");
            check(!zone.TryAccept(native), "a newly selected native item cannot be consumed by a physics callback");
            grabbable.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Unselect, pointer));
            check(!native.IsHeld && zone.TryAccept(native) && native.Deposited && accepted == 1,
                "released native item is accepted once through the real drop callback");
            check(!zone.TryAccept(native) && accepted == 1, "secured item cannot increment its drop task twice");
            check(!native.CanStore && !native.CanRecover, "secured item cannot return to inventory or recovery");

            var inventory = Inventory.I;
            check(inventory != null && inventory.Count < Inventory.Capacity, "fresh chapter has inventory space for recovery regression");
            int slot = inventory.Count;
            item.ForceHeld = true;
            check(inventory.Store(item) && item.Stored && !item.ForceHeld && !item.gameObject.activeSelf,
                "storing clears synthetic grip and disables the stored item");
            check(inventory.TakeToHand(slot) && !item.Stored && item.IsHeld && inventory.Count == slot,
                "retrieval restores exactly one item to the controller hand adapter");
            keyboard.enabled = false;
            keyboard.enabled = true;
            Vector3 home = item.HomePosition;
            item.Teleport(home + Vector3.right * 0.7f, Quaternion.identity);
            inventory.RecoverLooseItems();
            check(Vector3.Distance(item.transform.position, home) < 0.001f,
                "reachable recovery returns an item misplaced within the old ten-metre boundary");

            item.ForceHeld = true;
            item.Teleport(home + Vector3.right * 0.8f, Quaternion.identity);
            inventory.RecoverLooseItems();
            check(Vector3.Distance(item.transform.position, home) > 0.5f,
                "recovery does not move an item in the player's hand");
            item.ForceHeld = false;
            item.Mounted = true;
            check(!item.CanStore && !item.CanRecover && !inventory.Store(item),
                "locked non-flashlight modules cannot be stored or recovered");
            item.Mounted = false;
            item.Consume();
            Vector3 consumedPosition = item.transform.position;
            inventory.RecoverLooseItems();
            check(item.Consumed && Vector3.Distance(item.transform.position, consumedPosition) < 0.001f,
                "recovery cannot duplicate or reset a consumed item");
            item.gameObject.SetActive(false);
            native.gameObject.SetActive(false);
            zoneObject.SetActive(false);
        }

        static IEnumerator MotionChecks(Transform parent, Action<bool, string> check)
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            var blocker = rig ? rig.GetComponent<PlayerBlocker>() : null;
            check(blocker != null, "active XR rig has destination and head-clearance protection");
            if (!blocker) yield break;
            int interactors = Object.FindObjectsByType<TeleportInteractor>(FindObjectsInactive.Include).Length;
            check(interactors > 0 && blocker.ProtectedTeleportInteractors == interactors,
                "destination protection is wired to every scene teleport interactor");
            var floor = Box(parent, "RegressionFloor", new Vector3(30f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
            var desk = Box(parent, "RegressionDesk", new Vector3(31f, 0.6f, 0f), new Vector3(1f, 1.2f, 1f));
            var wall = Box(parent, "RegressionWall", new Vector3(30f, 1.3f, 2f), new Vector3(0.2f, 2.6f, 3f));
            Physics.SyncTransforms();
            check(blocker.IsDestinationClear(new Vector3(28f, 0f, 0f)), "teleport accepts a supported destination with body clearance");
            check(!blocker.IsDestinationClear(new Vector3(31f, 0f, 0f)), "teleport rejects placing the body inside a desk");
            check(!blocker.IsDestinationClear(new Vector3(40f, 0f, 0f)), "teleport rejects a destination without ground");
            check(!blocker.IsDestinationClear(new Vector3(float.NaN, 0f, 0f)), "teleport rejects an invalid position");
            check(!blocker.IsPathClear(new Vector3(28f, 1f, 2f), new Vector3(32f, 1f, 2f)),
                "teleport path cannot pass through a solid wall");
            check(blocker.IsPathClear(new Vector3(28f, 1f, 0f), new Vector3(29f, 1f, 0f)),
                "teleport path accepts an unobstructed segment");
            var eyeBlock = Box(parent, "RegressionEyeObstruction", rig.centerEyeAnchor.position, Vector3.one * 0.25f);
            Physics.SyncTransforms();
            Vector3 origin = rig.transform.position;
            Quaternion rotation = rig.transform.rotation;
            blocker.SendMessage("LateUpdate");
            check(Vector3.Distance(rig.transform.position, origin) < 0.00001f && Quaternion.Angle(rig.transform.rotation, rotation) < 0.001f,
                "physical head penetration does not translate or rotate the tracked rig");
            eyeBlock.SetActive(false);
            floor.SetActive(false);
            desk.SetActive(false);
            wall.SetActive(false);
            Physics.SyncTransforms();
            blocker.SendMessage("LateUpdate");
            yield return null;
        }

        static IEnumerator RailChecks(Transform parent, Action<bool, string> check)
        {
            var railObject = new GameObject("RegressionMaintenanceRail");
            railObject.transform.SetParent(parent);
            railObject.transform.position = new Vector3(50f, 1f, 0f);
            var rail = railObject.AddComponent<MaintenanceRail>();
            rail.railLength = 2f;
            var origin = new GameObject("RegressionTrackingOrigin").transform;
            origin.SetParent(parent);
            origin.position = new Vector3(50f, 0f, 0f);
            var head = new GameObject("RegressionHead").transform;
            head.SetParent(origin, false);
            head.localPosition = Vector3.up * 1.6f;
            var recorder = new RecordedLocomotion();
            rail.Initialize(origin, head, recorder);
            var idle = new MaintenanceRail.HandSample(true, false, rail.transform.position, Vector3.up);
            var grip = new MaintenanceRail.HandSample(true, true, rail.transform.position, Vector3.up);
            rail.ProcessInput(grip, idle, 0.02f);
            check(rail.IsPulling && recorder.Events.Count == 0, "rail grip starts only a tether, without moving the player");
            var pull = new MaintenanceRail.HandSample(true, true, rail.transform.position + Vector3.back * 0.05f, Vector3.up + Vector3.back * 0.05f);
            rail.ProcessInput(pull, idle, 0.02f);
            check(recorder.Events.Count == 1 && recorder.Events[0].Translation == LocomotionEvent.TranslationType.Relative &&
                rail.LastRequestedMovement.z > 0f && rail.LastRequestedMovement.magnitude <= rail.maxSpeed * 0.02f + 0.0001f &&
                Mathf.Abs(rail.LastRequestedMovement.x) < 0.0001f && Mathf.Abs(rail.LastRequestedMovement.y) < 0.0001f,
                "hand pull emits speed-limited movement only along the rail through the locomotion API");
            int requests = recorder.Events.Count;
            rail.ProcessInput(pull, idle, 0.02f);
            check(recorder.Events.Count == requests && rail.LastRequestedMovement == Vector3.zero,
                "stationary hand produces no passive drift");
            rail.ProcessInput(idle, idle, 0.02f);
            check(!rail.IsPulling && rail.LastRequestedMovement == Vector3.zero, "releasing grip stops rail movement immediately");
            var far = new MaintenanceRail.HandSample(true, true, rail.transform.position + Vector3.right, Vector3.right + Vector3.up);
            rail.ProcessInput(far, idle, 0.02f);
            check(!rail.IsPulling, "pressing grip away from a rail cannot initiate movement");
            rail.ProcessInput(grip, idle, 0.02f);
            check(!rail.IsPulling, "holding an air grip while entering the rail does not unexpectedly latch");
            rail.ProcessInput(idle, idle, 0.02f);
            rail.ProcessInput(grip, idle, 0.02f);
            rail.ProcessInput(new MaintenanceRail.HandSample(false, true, grip.worldPosition, grip.trackingPosition), idle, 0.02f);
            check(!rail.IsPulling, "loss of controller tracking cancels a rail pull");
            rail.ProcessInput(idle, idle, 0.02f);
            rail.ProcessInput(grip, idle, 0.02f);
            rail.ProcessInput(pull, idle, 0.2f);
            check(!rail.IsPulling && rail.LastRequestedMovement == Vector3.zero, "a frame hitch cannot cause a large rail jump");
            rail.ProcessInput(idle, idle, 0.02f);
            rail.ProcessInput(grip, idle, 0.02f);
            recorder.HandleLocomotionEvent(new LocomotionEvent(0, Vector3.zero, LocomotionEvent.TranslationType.Absolute));
            check(!rail.IsPulling, "teleport releases the rail tether");
            rail.ProcessInput(idle, idle, 0.02f);
            head.position = rail.EndPoint + Vector3.up * 0.6f - Vector3.forward * 0.002f;
            rail.ProcessInput(grip, idle, 0.02f);
            rail.ProcessInput(pull, idle, 0.02f);
            check(rail.LastRequestedMovement.z >= 0f && rail.LastRequestedMovement.z <= 0.0021f,
                "rail motion is clamped to the authored end point");
            rail.enabled = false;
            check(!rail.IsPulling, "disabling the rail cancels its active tether");
            railObject.SetActive(false);
            origin.gameObject.SetActive(false);
            yield return null;
        }

        static GrabItem MakeItem(Transform parent, string name, Vector3 position, bool native)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.08f;
            if (native)
            {
                var grabbable = go.AddComponent<Grabbable>();
                grabbable.InjectOptionalTargetTransform(go.transform);
                grabbable.InjectOptionalRigidbody(rb);
                grabbable.InjectOptionalThrowWhenUnselected(false);
            }
            var item = go.AddComponent<GrabItem>();
            item.kind = ItemKind.Prop;
            item.displayName = name;
            go.SetActive(true);
            return item;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
