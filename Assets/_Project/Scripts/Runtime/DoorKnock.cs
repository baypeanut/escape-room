using System.Collections;
using UnityEngine;

namespace CSER
{


    public class DoorKnock : MonoBehaviour
    {
        public enum Mode { Menu, Badge, Keypad, Exit }

        public Mode mode;
        public Hinge door;
        public BadgeReader reader;
        public DoorKeypad keypad;
        public Vector3 point;
        public float maxDistance = 3.8f;
        public float advanceDelay = 5f;

        bool busy, advancing;
        Transform head;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame) Knock();
#endif
        }

        public void Knock()
        {
            if (busy || advancing || !door || door.IsOpen) return;
            if (!head)
            {
                var cam = Camera.main;
                if (cam) head = cam.transform;
                else
                {
                    var rig = FindAnyObjectByType<OVRCameraRig>();
                    if (rig) head = rig.centerEyeAnchor;
                }
            }
            if (head)
            {
                var d = head.position - point;
                d.y = 0;
                if (d.magnitude > maxDistance)
                {
                    Say("Walk up to the door first, then point at it and pull the trigger.", Palette.Blue);
                    return;
                }
            }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            busy = true;
            var knock = Sfx.I ? Sfx.I.knock : null;
            for (int i = 0; i < 3; i++)
            {
                Sfx.PlayAt(knock, point, 1f);
                Sfx.Haptic(0.5f, 0.05f);
                StartCoroutine(Nudge());
                yield return new WaitForSeconds(i < 2 ? 0.26f : 0.55f);
            }
            Evaluate();
            busy = false;
        }

        IEnumerator Nudge()
        {
            var t = door.transform;
            var rest = t.localRotation;
            t.localRotation = rest * Quaternion.Euler(0, mode == Mode.Menu ? 1.2f : -1.2f, 0);
            yield return new WaitForSeconds(0.05f);
            if (!door.IsOpen) t.localRotation = rest;
        }

        void Evaluate()
        {
            var gm = GameManager.I;
            switch (mode)
            {
                case Mode.Menu:
                    int ch = StoryProgress.StoryComplete ? 1 : StoryProgress.UnlockedChapter;
                    door.Open();
                    Advance(() => SceneFlow.LoadChapter(ch), 2.4f);
                    break;

                case Mode.Badge:
                    if (!Ready(gm)) return;
                    if (!gm.IsDone(TaskId.SOC_Badge)) { Deny("You need your access badge first. Pick it up from your desk."); return; }
                    if (reader) reader.TryAccess();
                    break;

                case Mode.Keypad:
                    if (!Ready(gm)) return;
                    if (!gm.IsDone(TaskId.EncryptData)) { Deny("The door is locked. Contain the breach first: the override code builds up on the Containment Board."); return; }
                    Deny("Read the four digits on the Containment Board. Enter them on the keypad beside the door.");
                    break;

                case Mode.Exit:
                    if (gm.State != GameState.Won) { Deny("The door stays locked until the incident report is filed."); return; }
                    door.Open();
                    break;
            }
        }

        bool Ready(GameManager gm)
        {
            if (gm.State == GameState.Playing) return true;
            Deny(gm.State == GameState.Intro ? "Press Begin on the chapter card first." : "This chapter is over.");
            return false;
        }

        void Advance(System.Action go, float delay)
        {
            advancing = true;
            StartCoroutine(AdvanceRoutine(go, delay));
        }

        IEnumerator AdvanceRoutine(System.Action go, float delay)
        {
            yield return new WaitForSeconds(delay);
            go();
        }

        void Deny(string message)
        {
            Say(message, Palette.Red);
            Sfx.Play(Sfx.I ? Sfx.I.error : null);
        }

        static void Say(string message, Color color) => GameUI.I?.Caption(message, color, 4.5f);
    }
}
