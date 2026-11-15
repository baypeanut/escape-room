using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CSER.EditorTools
{
    [InitializeOnLoad]
    public static class MidnightValidation
    {
        const string ActiveKey = "Midnight.Validation.Active";
        const string ReportKey = "Midnight.Validation.Report";
        const string ExitKey = "Midnight.Validation.Exit";
        static readonly string[] SaveKeys = { "cser.unlocked", "cser.points", "cser.achievements", "cser.result.1", "cser.result.2", "cser.result.3", "cser.result.1.latest", "cser.result.2.latest", "cser.result.3.latest" };
        static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
        static readonly List<string> RuntimeErrors = new List<string>();
        static double deadline;

        static MidnightValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("CSER/Validate Quest Story")]
        public static void RunBatch()
        {
            try
            {
                SessionState.SetString(ReportKey, "Midnight editor validation\nUnity " + Application.unityVersion + "\nThese checks exercise game logic and scene wiring; headset interaction and performance require a Quest 3 playtest.\n");
                SessionState.SetInt(ExitKey, 1);
                ApkBuilder.ConfigureQuest();
                AuditSavedScenes();
                BackupProgress();
                StoryProgress.ResetAll();
                GameManager.Experienced = false;
                SessionState.SetBool(ActiveKey, true);
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append("FAIL setup: " + exception);
                WriteReport();
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void AuditSavedScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Check(scenes.Length == 4, "four enabled story scenes");
            foreach (var entry in scenes)
            {
                var scene = EditorSceneManager.OpenScene(entry.path);
                var roots = scene.GetRootGameObjects();
                var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                int missing = transforms.Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
                int particles = transforms.Sum(transform => transform.GetComponents<ParticleSystem>().Length);
                int audio = transforms.Sum(transform => transform.GetComponents<AudioSource>().Length);
                Check(missing == 0, scene.name + " has no missing scripts");
                Check(Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include).Length == 1, scene.name + " has one XR camera rig");
                Append("SCENE " + scene.name + " objects=" + transforms.Length + " particles=" + particles + " audioSources=" + audio);
                if (scene.name == "Ch2_FredsOffice" || scene.name == "Ch3_ServerRoom")
                {
                    Check(particles > 0, scene.name + " includes a particle system");
                    bool custom = transforms.SelectMany(transform => transform.GetComponents<Renderer>())
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Any(material => material && material.shader && material.shader.name.StartsWith("Midnight/", StringComparison.Ordinal));
                    Check(custom, scene.name + " references an authored Midnight shader");
                }
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 180;
                Steps.Clear();
                RuntimeErrors.Clear();
                Steps.Push(Suite());
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreProgress();
                SessionState.SetBool(ActiveKey, false);
                WriteReport();
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(ExitKey, 1));
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || Steps.Count == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("The validation suite exceeded 180 seconds.");
                var current = Steps.Peek();
                if (!current.MoveNext())
                {
                    if (Steps.Count > 0 && ReferenceEquals(Steps.Peek(), current)) Steps.Pop();
                }
                else if (current.Current is IEnumerator nested) Steps.Push(nested);
            }
            catch (Exception exception)
            {
                Append("FAIL runtime: " + exception);
                Finish(false);
            }
        }

        static IEnumerator Suite()
        {
            yield return Wait(1);
            Check(SceneManager.GetActiveScene().name == "MainMenu", "main menu starts");
            yield return ChapterStateCheck(1);
            yield return ChapterStateCheck(3);
            SceneManager.LoadScene("Ch2_FredsOffice");
            yield return Wait(1.5);
            var manager = GameManager.I;
            var workstation = Object.FindAnyObjectByType<Workstation>();
            var keypad = Object.FindAnyObjectByType<DoorKeypad>();
            Check(manager && workstation && keypad, "office puzzle controllers are present");
            Check(manager.State == GameState.Intro, "office waits for readiness");
            var atmosphere = Object.FindAnyObjectByType<MidnightAtmosphere>();
            Check(atmosphere && !atmosphere.Emitting && !atmosphere.Resolved, "containment display is idle before readiness");
            Check(Mathf.Approximately(manager.TimeLimit, 600), "normal office timer is ten minutes");
            Click(workstation.transform, "Terminate session & lock workstation");
            Check(!manager.IsDone(TaskId.LockWorkstation) && Read(workstation, "mode").ToString() == "Desktop", "intro UI cannot advance or consume the desktop state");
            var evidence = Object.FindObjectsByType<GrabItem>(FindObjectsInactive.Include).First(item => item.kind == ItemKind.EvidencePrintout);
            var room = Object.FindAnyObjectByType<RoomPuzzles>();
            evidence.EverHeld = true;
            Drop(room.evidenceBox, evidence);
            Check(!evidence.Consumed && !evidence.Deposited, "intro drop cannot consume evidence");
            Check(!manager.Complete(TaskId.LockWorkstation), "direct intro completion is rejected");
            manager.BeginPlaying();
            yield return Wait(0.15);
            Check(atmosphere.Emitting && !atmosphere.Resolved, "pending containment emits particles during play");
            Check(!manager.Complete(TaskId.SRV_Report), "wrong-chapter completion is rejected");
            EnterCode(keypad, string.Concat(manager.DoorCode));
            Check(!manager.IsDone(TaskId.Escape), "correct door code is refused before containment");
            yield return Wait(1);
            Click(workstation.transform, "Terminate session & lock workstation");
            Check(manager.IsDone(TaskId.LockWorkstation), "workstation locks through its UI action");
            workstation.DebugType("wrong");
            workstation.DebugSubmit();
            Check(!manager.IsDone(TaskId.FredLogin), "incorrect login is rejected");
            workstation.DebugType(workstation.fredPassword);
            workstation.DebugSubmit();
            Check(manager.IsDone(TaskId.FredLogin), "evidence-based login succeeds");
            workstation.DebugType("short");
            workstation.DebugSubmit();
            Check(!manager.IsDone(TaskId.NewPassword), "weak replacement password is rejected");
            workstation.DebugType("Blue-Tiger-Coffee-42!");
            workstation.DebugSubmit();
            Check(manager.IsDone(TaskId.NewPassword), "valid replacement password succeeds");
            workstation.DebugTab(2);
            workstation.DebugType(AuthToken.I.Current);
            workstation.DebugSubmit();
            Check(manager.IsDone(TaskId.Enable2FA), "current physical token code enables authentication");
            workstation.DebugTab(0);
            var mails = (IList)Read(workstation, "mails");
            foreach (var mail in mails) Invoke(workstation, "Classify", mail, !(bool)Read(mail, "phishing"));
            Check(!manager.IsDone(TaskId.PhishingTriage), "six incorrect email decisions do not solve the puzzle");
            Set(workstation, "openMail", 0);
            Invoke(workstation, "Rebuild");
            Check(FindButton(workstation.transform, "Report phishing") && FindButton(workstation.transform, "Mark safe"), "wrong email classifications retain retry controls");
            foreach (var mail in mails) Invoke(workstation, "Classify", mail, (bool)Read(mail, "phishing"));
            Check(manager.IsDone(TaskId.PhishingTriage), "correcting all six emails solves triage");
            workstation.DebugTab(3);
            var encryptionButton = FindButton(workstation.transform, "Encrypt & revoke access");
            Check(encryptionButton && !encryptionButton.interactable, "encryption remains locked while physical tasks are missing");
            foreach (var item in Object.FindObjectsByType<GrabItem>(FindObjectsInactive.Include))
            {
                DropZone zone = item.kind == ItemKind.EvidencePrintout || item.kind == ItemKind.EvidenceNote ? room.evidenceBox
                    : item.kind == ItemKind.Confidential ? room.shredder
                    : item.kind == ItemKind.USB ? room.quarantine : null;
                if (!zone) continue;
                item.EverHeld = true;
                Drop(zone, item);
            }
            yield return Wait(1);
            Check(atmosphere.Resolved && !atmosphere.Emitting, "USB containment resolves the display and stops emission");
            Check(manager.IsDone(TaskId.EvidencePrintout) && manager.IsDone(TaskId.EvidenceNote)
                && manager.IsDone(TaskId.ShredDocs) && manager.IsDone(TaskId.QuarantineUSB), "physical evidence and containment callbacks complete their tasks");
            yield return Until(() => (int)Read(workstation, "chatState") == 1, 10, "incoming helpdesk message");
            workstation.DebugTab(1);
            Click(workstation.transform, "Send the code from the token");
            Check(!manager.IsDone(TaskId.SocialEngineering), "unsafe helpdesk response cannot solve the puzzle");
            Click(workstation.transform, "Refuse & report to Security");
            Check(manager.IsDone(TaskId.SocialEngineering), "safe helpdesk response solves the puzzle");
            Check(manager.State == GameState.Playing && !manager.IsDone(TaskId.EncryptData), "office cannot win before final containment");
            workstation.DebugTab(3);
            yield return Wait(0.15);
            Check(manager.AllContainedExcept(TaskId.EncryptData, TaskId.Escape), "all prerequisites are complete before encryption");
            Click(workstation.transform, "Encrypt & revoke access");
            yield return Until(() => manager.IsDone(TaskId.EncryptData), 8, "encryption finishes");
            Check(manager.TimerPaused, "contained exfiltration stops the chapter deadline");
            EnterCode(keypad, "0000");
            Check(manager.State == GameState.Playing && !manager.IsDone(TaskId.Escape), "wrong final door code is rejected");
            yield return Wait(1);
            EnterCode(keypad, string.Concat(manager.DoorCode));
            Check(manager.State == GameState.Won && manager.DoneCount == manager.Chapter.tasks.Length, "all mandatory office tasks produce a win");
            Check(!manager.Complete(TaskId.Escape), "terminal state rejects duplicate completion");
            manager.Restart(true);
            yield return Until(() => GameManager.I && GameManager.I != manager, 12, "restart loads a fresh chapter");
            yield return Wait(0.5);
            manager = GameManager.I;
            Check(manager.State == GameState.Playing && manager.DoneCount == 0 && manager.Elapsed < 3, "restart resets progress and resumes play");
            workstation = Object.FindAnyObjectByType<Workstation>();
            Check(Read(workstation, "mode").ToString() == "Desktop", "restart restores the desktop puzzle");
            mails = (IList)Read(workstation, "mails");
            Check(mails.Cast<object>().All(mail => (int)Read(mail, "state") == 0), "restart clears email decisions");
            Check(Object.FindObjectsByType<GrabItem>(FindObjectsInactive.Include).Where(item => item.kind == ItemKind.EvidencePrintout || item.kind == ItemKind.USB)
                .All(item => !item.Consumed && !item.Deposited), "restart restores physical puzzle items");
            manager.DebugNearlyOutOfTime(0.02f);
            yield return Until(() => manager.State == GameState.Lost, 3, "deadline causes loss");
            yield return Wait(0.15);
            atmosphere = Object.FindAnyObjectByType<MidnightAtmosphere>();
            Check(atmosphere && !atmosphere.Emitting && !atmosphere.Resolved, "loss stops unresolved containment emission");
            Click(workstation.transform, "Terminate session & lock workstation");
            Check(manager.State == GameState.Lost && manager.DoneCount == 0, "loss blocks further puzzle progress");
            manager.Restart(false);
            yield return Until(() => GameManager.I && GameManager.I != manager, 12, "retry from loss reloads the scene");
            yield return Wait(0.5);
            Check(GameManager.I.State == GameState.Intro && GameManager.I.DoneCount == 0, "retry can return to readiness");
            Check(RuntimeErrors.Count == 0, "no game-script errors or exceptions during the suite");
            Append("PASS all checks. Physical grab tracking, controller reach, comfort, visual fidelity and device frame rate remain headset checks.");
            Finish(true);
        }

        static IEnumerator ChapterStateCheck(int chapter)
        {
            SceneManager.LoadScene(GameData.Chapters[chapter - 1].scene);
            yield return Wait(1);
            var manager = GameManager.I;
            Check(manager && manager.chapter == chapter, "chapter " + chapter + " starts with the correct controller");
            Check(manager.State == GameState.Intro && !manager.Complete(manager.Chapter.tasks[0].id), "chapter " + chapter + " rejects progress before readiness");
            var atmosphere = Object.FindAnyObjectByType<MidnightAtmosphere>();
            if (chapter == 3) Check(atmosphere && !atmosphere.Emitting && !atmosphere.Resolved, "server display waits for readiness");
            manager.BeginPlaying();
            yield return Wait(0.15);
            if (chapter == 3) Check(atmosphere.Emitting && !atmosphere.Resolved, "unresolved server isolation emits particles");
            var tasks = manager.Chapter.tasks;
            foreach (var task in tasks.Take(tasks.Length - 1)) manager.Complete(task.id);
            yield return Wait(0.15);
            if (chapter == 3) Check(atmosphere.Resolved && !atmosphere.Emitting, "server isolation resolves its shader and stops particles");
            Check(manager.State == GameState.Playing, "chapter " + chapter + " requires its final objective");
            manager.Complete(tasks[tasks.Length - 1].id);
            Check(manager.State == GameState.Won, "chapter " + chapter + " state machine completes only with all objectives");
            Append("STATE TEST chapter " + chapter + " used direct task callbacks; it is not a full physical playthrough.");
        }

        static void Drop(DropZone zone, GrabItem item)
        {
            var collider = item.GetComponentsInChildren<Collider>(true).FirstOrDefault(component => !component.isTrigger);
            Check(collider, item.name + " has a physical collider");
            Invoke(zone, "OnTriggerStay", collider);
        }

        static void EnterCode(DoorKeypad keypad, string code)
        {
            keypad.Press("C");
            foreach (char digit in code) keypad.Press(digit.ToString());
            keypad.Press("OK");
        }

        static Button FindButton(Transform root, string label) => root.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button.GetComponentInChildren<TMP_Text>() is TMP_Text text && text.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);

        static void Click(Transform root, string label)
        {
            var button = FindButton(root, label);
            Check(button && button.interactable, "UI control available: " + label);
            button.onClick.Invoke();
        }

        static object Read(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        static object Invoke(object target, string method, params object[] arguments) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, arguments);

        static IEnumerator Wait(double seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, double seconds, string description)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(description);
                yield return null;
            }
            Append("PASS " + description);
        }

        static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            Append("PASS " + description);
        }

        static void Append(string text) => SessionState.SetString(ReportKey, SessionState.GetString(ReportKey, "") + text + "\n");

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (!stack.Contains("Assets/_Project/") || message.Contains("OVRPlugin")) return;
            RuntimeErrors.Add(message);
            Append("GAME LOG ERROR " + message + "\n" + stack);
        }

        static void Finish(bool passed)
        {
            Steps.Clear();
            SessionState.SetInt(ExitKey, passed ? 0 : 1);
            WriteReport();
            EditorApplication.ExitPlaymode();
        }

        static void WriteReport()
        {
            string output = Environment.GetEnvironmentVariable("MIDNIGHT_VALIDATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("Builds/MidnightValidation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, SessionState.GetString(ReportKey, ""));
            Debug.Log("Midnight validation result: " + output + "; exit=" + SessionState.GetInt(ExitKey, 1));
        }

        static void BackupProgress()
        {
            foreach (string key in SaveKeys)
            {
                SessionState.SetBool("Midnight.Backup.Exists." + key, PlayerPrefs.HasKey(key));
                SessionState.SetString("Midnight.Backup.Value." + key, key == "cser.unlocked" ? PlayerPrefs.GetInt(key, 1).ToString() : PlayerPrefs.GetString(key, ""));
            }
        }

        static void RestoreProgress()
        {
            foreach (string key in SaveKeys)
            {
                if (!SessionState.GetBool("Midnight.Backup.Exists." + key, false)) PlayerPrefs.DeleteKey(key);
                else if (key == "cser.unlocked") PlayerPrefs.SetInt(key, int.Parse(SessionState.GetString("Midnight.Backup.Value." + key, "1")));
                else PlayerPrefs.SetString(key, SessionState.GetString("Midnight.Backup.Value." + key, ""));
            }
            PlayerPrefs.Save();
        }
    }
}
