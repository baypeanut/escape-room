using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
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
    public static class MidnightAcceptance
    {
        const string Prefix = "Midnight.Acceptance.";
        static readonly string[] StringKeys =
        {
            "cser.points", "cser.achievements", "cser.result.1", "cser.result.2", "cser.result.3",
            "cser.result.1.latest", "cser.result.2.latest", "cser.result.3.latest"
        };
        static readonly string[] IntKeys = { "cser.unlocked", "cser.reduceFlash" };
        static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
        static readonly List<string> GameErrors = new List<string>();
        static double deadline;

        [Serializable]
        sealed class SavedScene
        {
            public string path;
            public bool loaded;
            public bool active;
        }

        [Serializable]
        sealed class SavedScenes
        {
            public SavedScene[] scenes;
        }

        static MidnightAcceptance()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += WatchRequest;
        }

        static void WatchRequest()
        {
            const string path = "Temp/cser_request_acceptance";
            if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(path);
            RunBatch();
        }

        [MenuItem("CSER/Validate Midnight Acceptance")]
        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start acceptance from edit mode, outside an active play session.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Acceptance does not discard unsaved scenes.");
            SessionState.SetString(Prefix + "Report", "Midnight Lockdown independent acceptance\nUnity " + Application.unityVersion
                + "\nUI callbacks and simulated object poses exercise player-facing actors. They do not establish controller tracking, headset comfort, stereo appearance or device performance.\n");
            SessionState.SetInt(Prefix + "Exit", 1);
            SessionState.SetInt(Prefix + "Count", 0);
            try
            {
                var setup = EditorSceneManager.GetSceneManagerSetup();
                SessionState.SetString(Prefix + "Scenes", JsonUtility.ToJson(new SavedScenes
                {
                    scenes = setup.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray()
                }));
                BackupProgress();
                SessionState.SetBool(Prefix + "Experienced", GameManager.Experienced);
                StoryProgress.ResetAll();
                GameManager.Experienced = false;
                SessionState.SetBool(Prefix + "Active", true);
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append("FAIL setup: " + exception);
                RestoreProgress();
                SessionState.SetBool(Prefix + "Active", false);
                WriteReport();
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 360;
                Steps.Clear();
                GameErrors.Clear();
                Steps.Push(Suite());
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            RestoreProgress();
            GameManager.Experienced = SessionState.GetBool(Prefix + "Experienced", false);
            RunState.Reset();
            SessionState.SetBool(Prefix + "Active", false);
            WriteReport();
            var previous = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Prefix + "Scenes", "{}"));
            if (previous?.scenes != null && previous.scenes.Length > 0 && previous.scenes.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous.scenes.Select(s => new SceneSetup
                {
                    path = s.path, isLoaded = s.loaded, isActive = s.active
                }).ToArray());
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Prefix + "Exit", 1));
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || Steps.Count == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Acceptance exceeded six minutes.");
                var current = Steps.Peek();
                if (!current.MoveNext())
                {
                    if (Steps.Count > 0 && ReferenceEquals(Steps.Peek(), current)) Steps.Pop();
                }
                else if (current.Current is IEnumerator nested) Steps.Push(nested);
            }
            catch (Exception exception)
            {
                Append("FAIL " + exception);
                Finish(false);
            }
        }

        static IEnumerator Suite()
        {
            yield return Wait(0.5f);
            Check(SceneManager.GetActiveScene().name == "MainMenu", "saved main menu opens");
            Check(GameData.Chapters.SelectMany(ch => ch.tasks).GroupBy(t => t.id).All(group => group.Count() == 1)
                && Enum.GetValues(typeof(TaskId)).Cast<TaskId>().All(id => GameData.Chapters.Any(ch => ch.tasks.Any(t => t.id == id))),
                "every task enum belongs to exactly one chapter");
            PersistenceChecks();
            UiLifetimeChecks();
            MainMenuChecks();
            yield return ChapterOne();
            yield return OfficeFailures();
            yield return ServerRoom();
            yield return AlternativeRecoveryRoute();
            Check(GameErrors.Count == 0, "no game-script errors or exceptions during acceptance");
            Append("Completed " + SessionState.GetInt(Prefix + "Count", 0) + " assertions. Chapter one and three wins used actor UI, grab-state, placement and trigger paths; no task-completion shortcut supplied either win.");
            Append("Human headset play, both-eye rendering, controller reach, haptic feel, frame time, video and peer assessment remain outside this automated evidence.");
            Finish(true);
        }

        static void PersistenceChecks()
        {
            string[] corrupt = { "", "x", "1;2", "1;2;3;4", "NaN;0;0", "Infinity;0;0", "-1;0;0", "1;-1;0", "1;0;-1", "1;999999999999;0", "1;bad;0" };
            foreach (string value in corrupt)
            {
                PlayerPrefs.SetString("cser.result.1", value);
                PlayerPrefs.SetString("cser.result.1.latest", value);
                Check(!StoryProgress.Get(1).done && !StoryProgress.GetLatest(1).done, "invalid persisted result rejected: " + (value.Length == 0 ? "empty" : value));
            }
            StoryProgress.ResetAll();
            StoryProgress.Complete(1, 20f, 0, 1);
            StoryProgress.Complete(1, 40f, 5, 6);
            var best = StoryProgress.Get(1);
            var latest = StoryProgress.GetLatest(1);
            Check(best.done && best.seconds == 20 && best.hints == 0 && best.mistakes == 1, "historical best retains the actual faster attempt");
            Check(latest.done && latest.seconds == 40 && latest.hints == 5 && latest.mistakes == 6, "latest result retains all fields of the slower attempt");
            StoryProgress.Complete(1, float.NaN, 0, 0);
            StoryProgress.Complete(1, float.PositiveInfinity, 0, 0);
            StoryProgress.Complete(1, -1, 0, 0);
            StoryProgress.Complete(1, 1, -1, 0);
            StoryProgress.Complete(0, 1, 0, 0);
            Check(StoryProgress.GetLatest(1).seconds == 40, "invalid new records cannot replace valid persisted results");
            RunState.Reset();
            RunState.Record(0, 1, 0, 0);
            RunState.Record(4, 1, 0, 0);
            RunState.Record(1, float.NaN, 0, 0);
            RunState.Record(1, float.PositiveInfinity, 0, 0);
            RunState.Record(1, -1, 0, 0);
            RunState.Record(1, 1, -1, 0);
            RunState.Record(1, 1, 0, -1);
            Check(RunState.CompletedChapters == 0, "invalid runtime records cannot count as completed chapters");
            RunState.Record(3, 90, 4, 7);
            Check(RunState.CompletedChapters == 1 && !RunState.Get(1).done && RunState.Get(3).mistakes == 7,
                "partial session records remain separate from historical chapter results");
            RunState.Reset();
            StoryProgress.ResetAll();
            Achievements.Unlock("first_alert");
            Achievements.Unlock("first_alert");
            Check(Achievements.Has("first_alert") && Achievements.Count == 1, "achievement unlocks once and stays recorded");
            Achievements.EvaluateTask(TaskId.SRV_RouteBackup);
            Check(Achievements.Has("circuit_route"), "task evaluation unlocks the recovery-route achievement");
            Achievements.EvaluateChapter(1, 30f, 0, 0);
            Check(Achievements.Has("night_shift") && Achievements.Has("clean_hands") && Achievements.Has("quiet_hints"),
                "chapter evaluation unlocks clear-run achievements");
            StoryProgress.ResetAll();
            Check(!Achievements.Has("first_alert") && Achievements.Count == 0, "story reset clears achievement progress");
        }

        static void UiLifetimeChecks()
        {
            var parent = new GameObject("AcceptanceUi", typeof(RectTransform));
            int calls = 0;
            var stale = UIKit.Button(parent.transform, "Old", Color.black, Color.white, () => calls++);
            UIKit.Clear(parent.transform);
            Check(!stale.gameObject.activeInHierarchy, "clearing UI deactivates old children synchronously");
            stale.onClick.Invoke();
            stale.onClick.Invoke();
            Check(calls == 0, "stale cleared UI callback cannot perform an action");
            var current = UIKit.Button(parent.transform, "Current", Color.black, Color.white, () => calls++);
            current.interactable = false;
            current.onClick.Invoke();
            Check(calls == 0, "disabled UI callback cannot perform an action");
            current.interactable = true;
            current.onClick.Invoke();
            Check(calls == 1, "replacement active UI control remains usable");
            Object.Destroy(parent);
        }

        static void MainMenuChecks()
        {
            var menu = Object.FindAnyObjectByType<MainMenu>();
            Check(menu, "main menu actor is present");
            ClickNamed(menu.transform, "Menu_Achievements");
            string page = TextOf(menu.transform);
            Check(page.Contains("Achievements") && page.Contains("Locked") && page.Contains("0 / " + Achievements.Catalog.Length),
                "achievements page lists locked entries before any unlocks");
            ClickNamed(menu.transform, "Menu_How to play");
            Check(TextOf(menu.transform).Contains("Maintenance rail") && TextOf(menu.transform).Contains("BAG"),
                "how-to page documents rail movement and BAG recovery");
            ClickNamed(menu.transform, "Menu_Settings");
            Check(TextOf(menu.transform).Contains("Reduce flashing") && FindButton(menu.transform, "On") && FindButton(menu.transform, "Off"),
                "main menu settings expose reduced flashing");
        }

        static void RequiredTaskChecks(GameManager manager)
        {
            var done = (HashSet<TaskId>)Read(manager, "done");
            var previous = done.ToArray();
            try
            {
                done.Clear();
                foreach (var task in manager.Chapter.tasks) done.Add(task.id);
                Check(manager.AllContainedExcept(), "chapter " + manager.chapter + " predicate accepts its complete required set");
                foreach (var task in manager.Chapter.tasks)
                {
                    done.Remove(task.id);
                    Check(!manager.AllContainedExcept() && manager.AllContainedExcept(task.id),
                        "required predicate blocks missing " + task.id + " and permits only its explicit exclusion");
                    done.Add(task.id);
                }
                done.Clear();
                foreach (var task in manager.Chapter.tasks)
                {
                    if (manager.PrerequisitesMet(task.id)) continue;
                    Check(!manager.Complete(task.id) && !manager.IsDone(task.id),
                        "core completion rejects " + task.id + " before its prerequisites");
                }
            }
            finally
            {
                done.Clear();
                foreach (var task in previous) done.Add(task);
            }
            Append("Predicate coverage above temporarily modified only an internal set. It did not emit completion events or represent a player solution.");
        }

        static IEnumerator ChapterOne()
        {
            SceneManager.LoadScene("Ch1_NightShift");
            yield return Wait(1f);
            var manager = GameManager.I;
            var siem = Object.FindAnyObjectByType<SiemConsole>();
            var laptop = Object.FindAnyObjectByType<MalwareLaptop>();
            var reader = Object.FindAnyObjectByType<BadgeReader>();
            var badge = Items().First(item => item.kind == ItemKind.Badge);
            Check(manager && siem && laptop && reader, "chapter one saved actor references exist");
            RequiredTaskChecks(manager);
            Check(manager.State == GameState.Intro && !reader.TryAccess(), "chapter one waits for readiness and denies introductory exit");
            badge.ForceHeld = true;
            yield return Wait(0.15f);
            Check(badge.EverHeld && !manager.IsDone(TaskId.SOC_Badge), "intro pickup is remembered without completing the badge objective");
            manager.BeginPlaying();
            badge.ForceHeld = false;
            Check(manager.IsDone(TaskId.SOC_Badge), "begin recognizes a badge picked up during readiness");
            Check(!reader.TryAccess() && !manager.IsDone(TaskId.SOC_Exit), "reader refuses exit before the investigation");
            Check(!manager.Complete(TaskId.SRV_Report), "foreign chapter task cannot be added during play");
            yield return Until(() => NamedButton(siem.transform, "Alert_Critical"), 10, "critical alert becomes selectable");
            ClickNamed(siem.transform, "Alert_Critical");
            Click(siem.transform, "ANNA-PC-03");
            Check(!manager.IsDone(TaskId.SOC_FindHost), "wrong host does not solve log interpretation");
            Click(siem.transform, "FRED-PC-07");
            Click(siem.transform, "Ransomware");
            Check(!manager.IsDone(TaskId.SOC_Classify), "wrong attack classification does not solve the alert");
            Click(siem.transform, "Data exfiltration");
            Click(siem.transform, "Declare CRITICAL incident");
            Check(manager.IsDone(TaskId.SOC_Escalate), "SIEM actor declares the correctly investigated incident");
            ClickNamed(laptop.transform, "Proc0");
            Click(laptop.transform, "Terminate selected process");
            Check((int)Read(laptop, "stage") == 1 && !manager.IsDone(TaskId.SOC_Malware), "normal process selection is rejected");
            ClickNamed(laptop.transform, "Proc2");
            Click(laptop.transform, "Terminate selected process");
            ClickNamed(laptop.transform, "Opt1");
            Check((int)Read(laptop, "stage") == 2, "unsafe network response does not advance malware containment");
            ClickNamed(laptop.transform, "Opt0");
            ClickNamed(laptop.transform, "DeleteAll");
            Check((int)Read(laptop, "stage") == 3, "destructive file choice does not substitute for quarantine");
            var quarantine = NamedButton(laptop.transform, "Quarantine");
            Check(quarantine && quarantine.interactable, "quarantine actor control is available");
            quarantine.onClick.Invoke();
            quarantine.onClick.Invoke();
            yield return Wait(1.7f);
            Check(!manager.IsDone(TaskId.SOC_Malware), "repeated quarantine callback cannot accelerate the scan");
            yield return Until(() => manager.IsDone(TaskId.SOC_Malware), 12, "actor scan finishes malware containment");
            Check(manager.State == GameState.Playing && manager.AllContainedExcept(TaskId.SOC_Exit), "chapter one still requires the physical reader action");
            badge.Teleport(reader.transform.position, badge.transform.rotation);
            Physics.SyncTransforms();
            Invoke(reader, "OnTriggerEnter", ItemCollider(badge));
            Check(manager.State == GameState.Won && manager.DoneCount == manager.Chapter.tasks.Length,
                "chapter one wins through investigation, malware UI and badge-reader actors");
            Capture("acceptance_soc_complete", new Vector3(-3.6f, 2.7f, -2.7f), new Vector3(26, 48, 0));
            yield return Until(() => SceneManager.GetActiveScene().name == "Ch2_FredsOffice", 20, "badge reader automatically advances to the office");
            yield return Wait(1f);
        }

        static IEnumerator OfficeFailures()
        {
            var manager = GameManager.I;
            var workstation = Object.FindAnyObjectByType<Workstation>();
            var keypad = Object.FindAnyObjectByType<DoorKeypad>();
            Check(manager && manager.chapter == 2 && workstation && keypad, "office transition provides its saved gameplay actors");
            RequiredTaskChecks(manager);
            Check(manager.State == GameState.Intro && Mathf.Approximately(manager.TimeLimit, 600), "normal office starts with readiness and a ten-minute deadline");
            Click(workstation.transform, "Terminate session & lock workstation");
            Check(!manager.IsDone(TaskId.LockWorkstation), "introductory workstation click cannot advance gameplay");
            manager.BeginPlaying();
            yield return InteractionRegression.Run(Check);
            yield return HubChecks();
            EnterCode(keypad, string.Concat(manager.DoorCode));
            Check(!manager.IsDone(TaskId.Escape), "even the correct door code is refused before containment");
            var display = (TMP_Text)Read(keypad, "display");
            Check(display.text == "DENIED", "keypad keeps its denial message after submission");
            keypad.Press("1");
            Check(display.text == "DENIED", "input during denial does not erase the feedback");
            yield return Wait(1f);
            Check(display.text.Contains("_") && !display.text.Contains("DENIED"), "keypad returns to empty entry after denial");
            int mistakes = manager.Mistakes;
            EnterCode(keypad, "0000");
            Check(manager.Mistakes == mistakes + 1 && !manager.IsDone(TaskId.Escape), "a wrong door attempt counts once and cannot escape");
            manager.DebugNearlyOutOfTime(0.05f);
            yield return Until(() => manager.State == GameState.Lost, 5, "the office deadline produces a loss");
            int completed = manager.DoneCount;
            Click(workstation.transform, "Terminate session & lock workstation");
            EnterCode(keypad, string.Concat(manager.DoorCode));
            Check(manager.State == GameState.Lost && manager.DoneCount == completed, "loss rejects later workstation and keypad actions");
            Capture("acceptance_office_loss", new Vector3(0.72f, 1.45f, 0.05f), new Vector3(28, 0, 0));
            manager.Restart(false);
            yield return Until(() => GameManager.I && GameManager.I != manager, 20, "retry replaces the lost chapter instance");
            yield return Wait(0.5f);
            Check(GameManager.I.State == GameState.Intro && GameManager.I.DoneCount == 0 && GameManager.I.Elapsed == 0,
                "retry restores readiness, tasks and timer");
        }

        static IEnumerator HubChecks()
        {
            var hub = Hub.I;
            Check(hub && GameUI.I, "live in-game menu is present");
            GameUI.I.OpenMenu();
            Check(hub.IsOpen && GameUI.I.MenuOpen && hub.Tab == 0, "game menu opens the live hub");
            foreach (string tab in new[] { "Menu", "Inventory", "Notes", "Settings" })
                Check(ExactButton(hub.canvas, tab), "live menu exposes the " + tab + " tab");
            ExactButton(hub.canvas, "Inventory").onClick.Invoke();
            Check(hub.Tab == 1 && FindButton(hub.canvas, "Recover misplaced items"), "inventory tab exposes required-item recovery");
            var loose = Items().FirstOrDefault(item => item.CanRecover
                && (item.kind == ItemKind.EvidencePrintout || item.kind == ItemKind.EvidenceNote || item.kind == ItemKind.USB));
            Check(loose && Inventory.I, "office provides a recoverable evidence item for inventory recovery");
            Vector3 home = loose.HomePosition;
            loose.Teleport(home + new Vector3(2.4f, 0.15f, 0f), loose.transform.rotation);
            Physics.SyncTransforms();
            int recovered = Inventory.I.RecoverLooseItems();
            Check(recovered >= 1 && Achievements.Has("evidence_keeper"),
                "recover misplaced items returns loose evidence and unlocks Evidence Keeper");
            Check((loose.transform.position - home).sqrMagnitude < 0.01f, "recovered evidence returns to its original location");
            ExactButton(hub.canvas, "Notes").onClick.Invoke();
            Check(hub.Tab == 2 && TextOf(hub.canvas).Contains("Notes collected during this chapter"),
                "notes are reachable through the live menu");
            yield return Wait(0.1f);
            CapturePanel("acceptance_notes", hub);
            ExactButton(hub.canvas, "Settings").onClick.Invoke();
            Check(hub.Tab == 3 && SettingButton(hub, "Radio voice", true) && SettingButton(hub, "Room ambience", true)
                && SettingButton(hub, "Guide marker", true) && SettingButton(hub, "Experienced mode", true),
                "live settings expose sound, guidance and timer difficulty");
            bool reduced = AlertStateManager.I.ReduceFlashing;
            var toggle = SettingButton(hub, "Reduce flashing", !reduced);
            Check(toggle && toggle.interactable, "reduced-flashing control is reachable");
            toggle.onClick.Invoke();
            Check(AlertStateManager.I.ReduceFlashing != reduced, "reduced-flashing control changes the production preference");
            SettingButton(hub, "Reduce flashing", reduced).onClick.Invoke();
            Check(AlertStateManager.I.ReduceFlashing == reduced, "reduced-flashing preference can be restored through the UI");
            yield return Wait(0.1f);
            CapturePanel("acceptance_settings", hub);
            ExactButton(hub.canvas, "Close").onClick.Invoke();
            Check(!hub.IsOpen && !GameUI.I.MenuOpen, "live menu close returns to gameplay");
        }

        static IEnumerator ServerRoom()
        {
            RunState.Reset();
            StoryProgress.Complete(1, 1, 0, 0);
            StoryProgress.Complete(2, 1, 0, 0);
            SceneManager.LoadScene("Ch3_ServerRoom");
            yield return Wait(1f);
            var manager = GameManager.I;
            var consoles = Object.FindObjectsByType<OpsConsole>(FindObjectsInactive.Exclude);
            var network = consoles.Single(console => console.mode == OpsConsole.Mode.Switch);
            var backup = consoles.Single(console => console.mode == OpsConsole.Mode.Backup);
            var report = consoles.Single(console => console.mode == OpsConsole.Mode.Report);
            var implant = Object.FindAnyObjectByType<RogueImplant>();
            var station = Object.FindAnyObjectByType<PatchRoutingStation>();
            var response = Object.FindAnyObjectByType<MidnightRoomResponse>();
            Check(manager && implant && station && station.modules.Length == 3 && station.sockets.Length == 4,
                "server saved scene includes implant and three-module recovery puzzle");
            Check(response && response.ceilingPanels.Length > 0 && response.serviceLights.Length > 0,
                "server saved scene includes the restoration light and panel references");
            Check(response.ceilingPanels.All(panel => panel && panel.sharedMaterial && panel.sharedMaterial.IsKeywordEnabled("_EMISSION")),
                "restoration panel materials retain their emission shader keyword after import");
            Check(response.serviceLights.All(light => light && light.intensity < 0.05f),
                "restoration service lights begin switched off");
            RequiredTaskChecks(manager);
            Check(manager.State == GameState.Intro && !NamedButton(backup.transform, "Backup0") && !NamedButton(report.transform, "Report0"),
                "server operations wait for readiness");
            var torch = Items().First(item => item.kind == ItemKind.Flashlight);
            torch.ForceHeld = true;
            yield return Wait(0.15f);
            manager.BeginPlaying();
            torch.ForceHeld = false;
            Check(manager.IsDone(TaskId.SRV_Flashlight), "readiness pickup becomes the flashlight objective on begin");
            FlashlightPoseChecks(torch);
            Check(!NamedButton(backup.transform, "Backup0") && !NamedButton(report.transform, "Report0"),
                "backup and report remain unavailable before containment");
            BackupPrerequisiteChecks(manager, backup, report);
            ClickNamed(network.transform, "Port14");
            Click(network.transform, "Move to QUARANTINE VLAN 999");
            Check(!manager.IsDone(TaskId.SRV_Isolate), "port quarantine is refused before identifying the device");
            implant.Inspect();
            Check(manager.IsDone(TaskId.SRV_Find), "inspecting the implant completes discovery");
            ClickNamed(network.transform, "Port1");
            Click(network.transform, "Move to QUARANTINE VLAN 999");
            Check(!manager.IsDone(TaskId.SRV_Isolate), "a legitimate port cannot satisfy containment");
            var plug = implant.plug.GetComponent<GrabItem>();
            Check(plug, "network plug has a grab actor");
            plug.ForceHeld = true;
            yield return Wait(0.1f);
            plug.Teleport(implant.socket.position + Vector3.up * 0.3f, plug.transform.rotation);
            yield return Until(() => manager.IsDone(TaskId.SRV_Unplug), 5, "moving the held plug away disconnects the implant");
            plug.ForceHeld = false;
            var device = implant.GetComponent<GrabItem>();
            var zoneTask = Object.FindObjectsByType<ZoneTask>(FindObjectsInactive.Exclude).Single(zone => zone.task == TaskId.SRV_Bag);
            device.ForceHeld = true;
            yield return Wait(0.1f);
            device.Teleport(zoneTask.GetComponent<Collider>().bounds.center, device.transform.rotation);
            device.ForceHeld = false;
            yield return Wait(0.1f);
            Physics.SyncTransforms();
            Invoke(zoneTask.GetComponent<DropZone>(), "OnTriggerStay", ItemCollider(device));
            Check(manager.IsDone(TaskId.SRV_Bag) && device.Deposited, "released implant is secured through its evidence-zone actor");
            SecuredTargetChecks(device);
            yield return RecoveryRoute(station, manager);
            Check(station.HasValidRoute() && !station.Solved && !manager.IsDone(TaskId.SRV_RouteBackup),
                "a connected route cannot complete while the network port is still active");
            ClickNamed(network.transform, "Port14");
            Click(network.transform, "Move to QUARANTINE VLAN 999");
            Check(manager.IsDone(TaskId.SRV_Isolate) && manager.IsDone(TaskId.SRV_RouteBackup) && station.Solved,
                "port isolation permits the valid physical recovery route");
            Check(station.modules.All(module => module.Latched && module.item.Deposited && !module.item.CanStore),
                "solved cartridges remain latched and cannot be stored or removed");
            foreach (var module in station.modules) SecuredTargetChecks(module.item);
            yield return Wait(1f);
            Capture("acceptance_recovery_route", station.transform.position - station.transform.forward * 1.2f,
                station.transform.rotation.eulerAngles);
            Check(!NamedButton(report.transform, "Report0"), "report remains unavailable before restoration");
            ClickNamed(backup.transform, "Backup1");
            Click(backup.transform, "Restore selected snapshot");
            Check(!manager.IsDone(TaskId.SRV_Restore) && (float)Read(backup, "restore") < 0, "post-breach backup is rejected");
            ClickNamed(backup.transform, "Backup2");
            Click(backup.transform, "Restore selected snapshot");
            Check(!manager.IsDone(TaskId.SRV_Restore), "failed-integrity backup is rejected");
            ClickNamed(backup.transform, "Backup0");
            var restore = FindButton(backup.transform, "Restore selected snapshot");
            Check(restore && restore.interactable, "clean recovery operation is available");
            restore.onClick.Invoke();
            restore.onClick.Invoke();
            yield return Wait(2f);
            Check(!manager.IsDone(TaskId.SRV_Restore), "duplicate restore callback cannot accelerate the recovery");
            yield return Until(() => manager.IsDone(TaskId.SRV_Restore), 12, "clean backup restore completes through its actor");
            Check(manager.State == GameState.Playing && manager.AllContainedExcept(TaskId.SRV_Report),
                "all physical and recovery objectives precede the incident report");
            ClickNamed(report.transform, "Report1");
            Check((int)Read(report, "question") == 0, "wrong report answer does not advance the question");
            var firstAnswer = NamedButton(report.transform, "Report0");
            Check(firstAnswer && firstAnswer.interactable, "correct first report answer is available");
            firstAnswer.onClick.Invoke();
            firstAnswer.onClick.Invoke();
            firstAnswer.onClick.Invoke();
            Check((int)Read(report, "question") == 1 && !manager.IsDone(TaskId.SRV_Report),
                "repeating a stale answer cannot skip the later report questions");
            ClickNamed(report.transform, "Report1");
            Check((int)Read(report, "question") == 2 && manager.State == GameState.Playing,
                "the second distinct report question must be answered");
            ClickNamed(report.transform, "Report2");
            Check(manager.State == GameState.Won && manager.DoneCount == manager.Chapter.tasks.Length,
                "server actor path wins only after every mandatory physical and report objective");
            Check(RunState.CompletedChapters == 1 && RunState.Get(3).mistakes == manager.Mistakes,
                "partial replay stores the actual server attempt without older chapter records");
            string review = string.Join("\n", GameUI.I.cardCanvas.GetComponentsInChildren<TMP_Text>(false).Select(text => text.text));
            Check(review.Contains("Server room cleared") && review.Contains("Chapter 1: not completed in this session")
                && review.Contains("Chapter 2: not completed in this session") && review.Contains("Session rank"),
                "partial replay review labels its actual session scope");
            yield return Wait(3f);
            Check(response.serviceLights.All(light => light && light.intensity > 2f),
                "successful restoration raises the service lights after the transition");
            bool panelsEmitting = true;
            var properties = new MaterialPropertyBlock();
            foreach (var panel in response.ceilingPanels)
            {
                panel.GetPropertyBlock(properties);
                panelsEmitting &= panel.sharedMaterial.IsKeywordEnabled("_EMISSION")
                    && properties.GetColor(Shader.PropertyToID("_EmissionColor")).maxColorComponent > 0.4f;
            }
            Check(panelsEmitting, "restored ceiling panels have active emission with a nonzero material property value");
            if (Environment.GetEnvironmentVariable("MIDNIGHT_ACCEPTANCE_CAPTURE") == "1")
            {
                torch.GetComponent<Flashlight>().Switch(false);
                Append("EDITOR CAPTURE SETUP: mounted torch switched off for the environmental recovery preview after the restoration lights settled. This is a staged editor view.");
            }
            Capture("acceptance_server_complete", new Vector3(3.3f, 2.7f, -2.3f), new Vector3(28, -60, 0));
        }

        static void SecuredTargetChecks(GrabItem item)
        {
            var targets = item.GetComponentsInChildren<MonoBehaviour>(true).Where(component => component is GrabInteractable
                || component is DistanceGrabInteractable || component is HandGrabInteractable || component is DistanceHandGrabInteractable).ToArray();
            Check(targets.Length > 0 && targets.All(target => !target.isActiveAndEnabled
                && ((IInteractableView)target).State == InteractableState.Disabled),
                item.name + " secured native grab targets are disabled in the SDK state machine");
            var grabbables = item.GetComponentsInChildren<Grabbable>(true);
            Check(grabbables.Length > 0 && grabbables.All(grabbable => !grabbable.isActiveAndEnabled),
                item.name + " secured pointable grab actors are disabled");
            var colliders = item.GetComponentsInChildren<Collider>(true);
            Check(colliders.Length > 0 && colliders.All(collider => !collider.enabled),
                item.name + " secured colliders cannot remain native hover or distance-grab targets");
        }

        static void FlashlightPoseChecks(GrabItem torch)
        {
            var flashlight = torch.GetComponent<Flashlight>();
            Check(torch.Mounted && flashlight && flashlight.beam && torch.transform.parent,
                "flashlight mounts on a tracked hand with its beam present");
            var hand = torch.transform.parent;
            Quaternion original = hand.rotation;
            Vector3 flat = Vector3.ProjectOnPlane(hand.forward, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.5f) flat = Vector3.forward;
            try
            {
                hand.rotation = Quaternion.LookRotation((flat + Vector3.up * 0.65f).normalized, Vector3.up);
                Invoke(flashlight, "LateUpdate");
                Check(flashlight.beam.transform.forward.y > 0.4f
                    && Vector3.Dot(flashlight.beam.transform.forward, hand.forward) > 0.999f,
                    "mounted beam follows an upward wrist pose instead of staying level");
                hand.rotation = Quaternion.LookRotation((flat - Vector3.up * 0.65f).normalized, Vector3.up);
                Invoke(flashlight, "LateUpdate");
                Check(flashlight.beam.transform.forward.y < -0.4f
                    && Vector3.Dot(flashlight.beam.transform.forward, hand.forward) > 0.999f,
                    "mounted beam follows a downward wrist pose");
            }
            finally
            {
                hand.rotation = original;
                Invoke(flashlight, "LateUpdate");
            }
            Check(Quaternion.Angle(hand.rotation, original) < 0.001f,
                "wrist-pose fixture restores the original tracked-hand transform synchronously");
        }

        static IEnumerator AlternativeRecoveryRoute()
        {
            SceneManager.LoadScene("Ch3_ServerRoom");
            yield return Wait(1f);
            var manager = GameManager.I;
            var station = Object.FindAnyObjectByType<PatchRoutingStation>();
            Check(station && !station.Solved && station.SeatedCount == 0 && station.modules.All(module => !module.Latched && !module.item.Deposited),
                "reloading the server resets routing, latches and physical cartridges");
            manager.BeginPlaying();
            Object.FindAnyObjectByType<RogueImplant>().Inspect();
            var network = Object.FindObjectsByType<OpsConsole>(FindObjectsInactive.Exclude).Single(console => console.mode == OpsConsole.Mode.Switch);
            ClickNamed(network.transform, "Port14");
            Click(network.transform, "Move to QUARANTINE VLAN 999");
            var straight = station.modules.Single(module => !module.corner);
            var elbows = station.modules.Where(module => module.corner).ToArray();
            yield return NativeSeat(station, elbows[0], 0, 2);
            yield return NativeSeat(station, elbows[1], 2, 0);
            yield return NativeSeat(station, straight, 3, 2);
            Check(station.HasValidRoute() && station.Solved && manager.IsDone(TaskId.SRV_RouteBackup),
                "native SDK releases solve the alternate bottom route with a half-turned straight cartridge");
            var backup = Object.FindObjectsByType<OpsConsole>(FindObjectsInactive.Exclude).Single(console => console.mode == OpsConsole.Mode.Backup);
            Check(manager.State == GameState.Playing && !NamedButton(backup.transform, "Backup0"),
                "an alternate route still cannot bypass disconnection and evidence containment");
            yield return Wait(1f);
            Capture("acceptance_recovery_alternate", station.transform.position - station.transform.forward * 1.2f,
                station.transform.rotation.eulerAngles);
        }

        static void BackupPrerequisiteChecks(GameManager manager, OpsConsole backup, OpsConsole report)
        {
            var done = (HashSet<TaskId>)Read(manager, "done");
            var previous = done.ToArray();
            var prerequisites = new[] { TaskId.SRV_Find, TaskId.SRV_Unplug, TaskId.SRV_Bag, TaskId.SRV_Isolate, TaskId.SRV_RouteBackup };
            try
            {
                done.Clear();
                foreach (var task in prerequisites) done.Add(task);
                foreach (var missing in prerequisites)
                {
                    done.Remove(missing);
                    Invoke(backup, "Rebuild");
                    Check(!NamedButton(backup.transform, "Backup0"), "backup UI rejects missing prerequisite " + missing);
                    done.Add(missing);
                }
                foreach (var task in manager.Chapter.tasks) done.Add(task.id);
                done.Remove(TaskId.SRV_Report);
                foreach (var missing in manager.Chapter.tasks.Where(task => task.id != TaskId.SRV_Report).Select(task => task.id))
                {
                    done.Remove(missing);
                    Invoke(report, "Rebuild");
                    Check(!NamedButton(report.transform, "Report0"), "report UI rejects missing prerequisite " + missing);
                    done.Add(missing);
                }
            }
            finally
            {
                done.Clear();
                foreach (var task in previous) done.Add(task);
                Invoke(backup, "Rebuild");
                Invoke(report, "Rebuild");
            }
            Append("Missing-prerequisite UI checks seeded task sets without callbacks. The following actor path starts from the restored actual progress.");
        }

        static IEnumerator RecoveryRoute(PatchRoutingStation station, GameManager manager)
        {
            var straight = station.modules.Single(module => !module.corner);
            var elbows = station.modules.Where(module => module.corner).ToArray();
            straight.item.Teleport(station.sockets[0].position, station.transform.rotation);
            straight.item.EverHeld = false;
            Check(!station.TryPlace(straight) && station.SeatedCount == 0, "untouched cartridge cannot be registered by proximity");
            straight.item.EverHeld = true;
            straight.item.Teleport(station.sockets[0].position + station.transform.forward * 0.3f, station.transform.rotation);
            Check(!station.TryPlace(straight), "cartridge outside the snap radius is rejected");
            straight.item.Teleport(station.sockets[0].position, station.transform.rotation * Quaternion.Euler(0, 180, 0));
            Check(!station.TryPlace(straight), "backward-facing cartridge is rejected");
            straight.item.Teleport(station.sockets[0].position, station.transform.rotation * Quaternion.Euler(0, 0, 45));
            Check(!station.TryPlace(straight), "diagonal cartridge does not silently rotate into a valid pose");
            yield return Seat(station, straight, 0, 0);
            elbows[0].item.EverHeld = true;
            elbows[0].item.Teleport(station.sockets[0].position, station.transform.rotation);
            Check(!station.TryPlace(elbows[0]) && station.SeatedCount == 1, "occupied bay rejects a second cartridge");
            yield return Seat(station, elbows[0], 1, 0);
            Check(!station.HasValidRoute() && !manager.IsDone(TaskId.SRV_RouteBackup), "two cartridges cannot satisfy the recovery route");
            yield return Seat(station, elbows[1], 3, 0);
            Check(!station.HasValidRoute() && !manager.IsDone(TaskId.SRV_RouteBackup), "three cartridges with a broken or dangling route do not solve recovery");
            yield return Seat(station, elbows[0], 1, 2);
            Check(station.HasValidRoute(), "correct physical cartridge rotations form a connected route");
        }

        static IEnumerator Seat(PatchRoutingStation station, PatchModule module, int socket, int quarterTurns)
        {
            module.item.ForceHeld = true;
            yield return Until(() => module.item.IsHeld && (bool)Read(module, "wasHeld"), 5,
                module.name + " observes the simulated grip before release");
            module.item.Teleport(station.sockets[socket].position,
                station.transform.rotation * Quaternion.Euler(0, 0, quarterTurns * 90));
            Physics.SyncTransforms();
            module.item.ForceHeld = false;
            yield return Until(() => !(bool)Read(module, "wasHeld"), 5,
                module.name + " observes the simulated release");
            Check(module.SocketIndex == socket && module.QuarterTurns == quarterTurns,
                "release seats " + module.name + " in bay " + socket + " at quarter turn " + quarterTurns
                + (module.SocketIndex == socket && module.QuarterTurns == quarterTurns ? "" :
                    "; actual bay=" + module.SocketIndex + " quarter=" + module.QuarterTurns
                    + " held=" + module.item.IsHeld + " forced=" + module.item.ForceHeld + " everHeld=" + module.item.EverHeld
                    + " releaseLatch=" + Read(module, "wasHeld") + " returning=" + (Read(module, "returning") != null)
                    + " distance=" + Vector3.Distance(module.transform.position, station.sockets[socket].position)
                    + " position=" + module.transform.position + " bodyPosition=" + module.GetComponent<Rigidbody>().position
                    + " localRotation=" + (Quaternion.Inverse(station.transform.rotation) * module.transform.rotation).eulerAngles
                    + " bodyRotation=" + (Quaternion.Inverse(station.transform.rotation) * module.GetComponent<Rigidbody>().rotation).eulerAngles));
        }

        static IEnumerator NativeSeat(PatchRoutingStation station, PatchModule module, int socket, int quarterTurns)
        {
            var grabbable = module.GetComponentsInChildren<Grabbable>(true).FirstOrDefault(value => value.isActiveAndEnabled);
            Check(grabbable && grabbable.Transform, module.name + " has an enabled native grab actor and target");
            Check(module.GetComponent<Rigidbody>().interpolation == RigidbodyInterpolation.None,
                module.name + " avoids physics interpolation competing with native hand poses");
            int pointerId = 1180 + socket;
            var initial = new Pose(grabbable.Transform.position, grabbable.Transform.rotation);
            grabbable.ProcessPointerEvent(new PointerEvent(pointerId, PointerEventType.Hover, initial));
            grabbable.ProcessPointerEvent(new PointerEvent(pointerId, PointerEventType.Select, initial));
            Check(!module.item.ForceHeld && module.item.IsHeld && grabbable.SelectingPointsCount > 0,
                module.name + " is held through native selection without the forced-grip adapter");
            yield return Until(() => (bool)Read(module, "wasHeld"), 5, module.name + " observes native selection");
            Check(!station.TryPlace(module), "native held cartridge cannot be seated before release");
            var release = new Pose(station.sockets[socket].position,
                station.transform.rotation * Quaternion.Euler(0, 0, quarterTurns * 90));
            grabbable.ProcessPointerEvent(new PointerEvent(pointerId, PointerEventType.Move, release));
            Check(Vector3.Distance(module.transform.position, release.position) < 0.002f
                && Quaternion.Angle(module.transform.rotation, release.rotation) < 0.5f,
                module.name + " native move updates its final world pose");
            grabbable.ProcessPointerEvent(new PointerEvent(pointerId, PointerEventType.Unselect, release));
            grabbable.ProcessPointerEvent(new PointerEvent(pointerId, PointerEventType.Unhover, release));
            Check(!module.item.IsHeld, module.name + " native unselect clears the held state immediately");
            yield return Until(() => !(bool)Read(module, "wasHeld"), 5, module.name + " observes native release");
            Check(module.SocketIndex == socket && module.QuarterTurns == quarterTurns,
                "native release seats " + module.name + " in bay " + socket + " at quarter turn " + quarterTurns
                + "; actual bay=" + module.SocketIndex + " quarter=" + module.QuarterTurns);
        }

        static GrabItem[] Items() => Object.FindObjectsByType<GrabItem>(FindObjectsInactive.Include);

        static Collider ItemCollider(GrabItem item)
        {
            var collider = item.GetComponentsInChildren<Collider>(true).FirstOrDefault(value => !value.isTrigger);
            if (!collider) throw new InvalidOperationException(item.name + " has no physical collider.");
            return collider;
        }

        static Button NamedButton(Transform root, string name) => root.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button.name == name && button.isActiveAndEnabled);

        static Button FindButton(Transform root, string label) => root.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button.isActiveAndEnabled && button.GetComponentInChildren<TMP_Text>() is TMP_Text text
                && text.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);

        static Button ExactButton(Transform root, string label) => root.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button.isActiveAndEnabled && button.GetComponentInChildren<TMP_Text>() is TMP_Text text && text.text == label);

        static string TextOf(Transform root) => string.Join("\n", root.GetComponentsInChildren<TMP_Text>(false).Select(text => text.text));

        static Button SettingButton(Hub hub, string label, bool value)
        {
            var heading = hub.canvas.GetComponentsInChildren<TMP_Text>(false)
                .FirstOrDefault(text => text.text.StartsWith(label, StringComparison.Ordinal));
            if (!heading) return null;
            float y = heading.rectTransform.anchoredPosition.y;
            return heading.transform.parent.GetComponentsInChildren<Button>(false).FirstOrDefault(button =>
                button.GetComponentInChildren<TMP_Text>() is TMP_Text text && text.text == (value ? "On" : "Off")
                && Mathf.Abs(button.GetComponent<RectTransform>().anchoredPosition.y - y) < 1f);
        }

        static void ClickNamed(Transform root, string name)
        {
            var button = NamedButton(root, name);
            Check(button && button.interactable, "named UI control available: " + name);
            button.onClick.Invoke();
        }

        static void Click(Transform root, string label)
        {
            var button = FindButton(root, label);
            Check(button && button.interactable, "UI control available: " + label);
            button.onClick.Invoke();
        }

        static void EnterCode(DoorKeypad keypad, string code)
        {
            keypad.Press("C");
            foreach (char digit in code) keypad.Press(digit.ToString());
            keypad.Press("OK");
        }

        static object Read(object target, string field) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        static object Invoke(object target, string method, params object[] arguments) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, arguments);

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, double seconds, string description)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(description);
                yield return null;
            }
            Check(true, description);
        }

        static void Capture(string name, Vector3 position, Vector3 angles)
        {
            if (Environment.GetEnvironmentVariable("MIDNIGHT_ACCEPTANCE_CAPTURE") != "1") return;
            ViewCapture.Capture(name, position, Quaternion.Euler(angles));
            Append("EDITOR RENDER Screenshots/" + name + ".png; this is a separate capture camera, not a headset eye.");
        }

        static void CapturePanel(string name, Hub hub)
        {
            if (Environment.GetEnvironmentVariable("MIDNIGHT_ACCEPTANCE_CAPTURE") != "1") return;
            Vector3 original = hub.root.position;
            try
            {
                hub.root.position += Vector3.up * (1.52f - original.y);
                ViewCapture.Capture(name, hub.root.position - hub.root.forward * 0.85f + Vector3.up * 0.08f, hub.root.rotation);
                Append("EDITOR RENDER Screenshots/" + name + ".png; panel and separate capture camera temporarily raised together to a nominal 1.6m view. Tracked rig unchanged; this is layout evidence, not a headset eye.");
            }
            finally
            {
                hub.root.position = original;
            }
        }

        static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            SessionState.SetInt(Prefix + "Count", SessionState.GetInt(Prefix + "Count", 0) + 1);
            Append("PASS " + description);
        }

        static void Append(string text) => SessionState.SetString(Prefix + "Report", SessionState.GetString(Prefix + "Report", "") + text + "\n");

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if ((!stack.Contains("Assets/_Project/") && !message.StartsWith("[CSER]", StringComparison.Ordinal)) || message.Contains("OVRPlugin")) return;
            GameErrors.Add(message);
            Append("GAME ERROR " + message + "\n" + stack);
        }

        static void Finish(bool passed)
        {
            Steps.Clear();
            SessionState.SetInt(Prefix + "Exit", passed ? 0 : 1);
            WriteReport();
            EditorApplication.ExitPlaymode();
        }

        static void WriteReport()
        {
            string path = Environment.GetEnvironmentVariable("MIDNIGHT_ACCEPTANCE_OUTPUT");
            if (string.IsNullOrWhiteSpace(path)) path = "Builds/MidnightAcceptance.txt";
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, SessionState.GetString(Prefix + "Report", ""));
            Debug.Log("Midnight acceptance report: " + path + "; exit=" + SessionState.GetInt(Prefix + "Exit", 1));
        }

        static void BackupProgress()
        {
            foreach (string key in StringKeys.Concat(IntKeys))
            {
                SessionState.SetBool(Prefix + "Exists." + key, PlayerPrefs.HasKey(key));
                SessionState.SetString(Prefix + "Value." + key, IntKeys.Contains(key)
                    ? PlayerPrefs.GetInt(key).ToString() : PlayerPrefs.GetString(key));
            }
            SessionState.SetBool(Prefix + "BackupReady", true);
        }

        static void RestoreProgress()
        {
            if (!SessionState.GetBool(Prefix + "BackupReady", false)) return;
            foreach (string key in StringKeys.Concat(IntKeys))
            {
                if (!SessionState.GetBool(Prefix + "Exists." + key, false)) PlayerPrefs.DeleteKey(key);
                else if (IntKeys.Contains(key)) PlayerPrefs.SetInt(key, int.Parse(SessionState.GetString(Prefix + "Value." + key, "0")));
                else PlayerPrefs.SetString(key, SessionState.GetString(Prefix + "Value." + key, ""));
            }
            PlayerPrefs.Save();
            SessionState.SetBool(Prefix + "BackupReady", false);
        }
    }
}
