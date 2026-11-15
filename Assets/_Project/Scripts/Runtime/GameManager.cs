using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CSER
{
    public enum GameState { Intro, Playing, Won, Lost }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }


        public static bool Experienced;
        static bool skipIntroOnReload;

        [Tooltip("1-based chapter this scene plays (see GameData.Chapters)")]
        public int chapter = 2;
        public ChapterInfo Chapter => GameData.Chapters[chapter - 1];
        [Min(0f)] public float normalMinutesOverride;
        [Min(0f)] public float experiencedMinutesOverride;
        public bool HasTimer => TimeLimit > 0f;

        public GameState State { get; private set; } = GameState.Intro;
        public float Elapsed { get; private set; }
        public float TimeLimit => 60f * (Experienced
            ? (experiencedMinutesOverride > 0f ? experiencedMinutesOverride : Chapter.experiencedMinutes)
            : (normalMinutesOverride > 0f ? normalMinutesOverride : Chapter.normalMinutes));
        public float Exfil01 => HasTimer ? Mathf.Clamp01(Elapsed / TimeLimit) : 0;
        public int HintsUsed { get; private set; }
        public int Mistakes { get; private set; }
        public int[] DoorCode { get; private set; }

        public bool TimerPaused { get; set; }

        readonly HashSet<TaskId> done = new HashSet<TaskId>();
        readonly Dictionary<TaskId, int> hintTier = new Dictionary<TaskId, int>();

        public event Action<TaskId> TaskCompleted;
        public event Action<GameState> StateChanged;
        public event Action<int> AreaCompleted;

        public int DoneCount => done.Count;
        public bool IsDone(TaskId t) => done.Contains(t);


        void OnDestroy() { if (I == this) I = null; }

        void Awake()
        {
            I = this;
            var rng = new System.Random(Environment.TickCount);
            DoorCode = new[] { rng.Next(1, 10), rng.Next(0, 10), rng.Next(0, 10), rng.Next(0, 10) };
        }

        void Start()
        {
            AuditInteraction();
            RunState.SetCheckpoint(chapter);
            if (skipIntroOnReload)
            {
                skipIntroOnReload = false;
                SetState(GameState.Playing);
            }
            else SetState(GameState.Intro);
        }

        void Update()
        {
            if (State != GameState.Playing || TimerPaused) return;
            Elapsed += Time.deltaTime;
            if (HasTimer && Elapsed >= TimeLimit) Lose();
        }


        static void AuditInteraction()
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (!c.GetComponent<UnityEngine.UI.GraphicRaycaster>()) continue;
                bool ok = c.GetComponentInChildren<Oculus.Interaction.RayInteractable>(true);
                if (ok) Debug.Log($"[CSER] {c.name}: ray interaction OK");
                else Debug.LogError($"[CSER] {c.name} has no ISDK ray interaction: its buttons can't be clicked in VR");
            }
        }

        void SetState(GameState s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }

        public void BeginPlaying()
        {
            if (State != GameState.Intro) return;
            SetState(GameState.Playing);
            foreach (var item in FindObjectsByType<GrabItem>(FindObjectsInactive.Include))
            {
                if (!item.EverHeld) continue;
                if (chapter == 1 && item.kind == ItemKind.Badge) Complete(TaskId.SOC_Badge);
                if (chapter == 3 && item.kind == ItemKind.Flashlight) Complete(TaskId.SRV_Flashlight);
            }
        }

        public bool Complete(TaskId t)
        {
            if (State != GameState.Playing || !Array.Exists(Chapter.tasks, task => task.id == t) || !PrerequisitesMet(t) || !done.Add(t)) return false;
            Sfx.Play(Sfx.I ? Sfx.I.success : null);
            Sfx.Haptic(0.5f, 0.1f);
            if (GameData.HasPoint(t))
            {
                StoryProgress.AddPoint(t);
                var lp = GameData.Point(t);
                GameUI.I?.Toast("Learning point unlocked", $"<b>{lp.title}</b>\n{lp.text}", Palette.Mint, 9f);
            }
            Achievements.EvaluateTask(t);
            TaskCompleted?.Invoke(t);
            for (int i = 0; i < GameData.Areas.Length; i++)
            {
                var area = GameData.Areas[i];
                if (Array.IndexOf(area.tasks, t) < 0) continue;
                if (Array.TrueForAll(area.tasks, done.Contains))
                {
                    Sfx.Play(Sfx.I ? Sfx.I.bong : null);
                    AreaCompleted?.Invoke(i);
                }
            }
            if (Array.TrueForAll(Chapter.tasks, x => done.Contains(x.id))) Win();
            return true;
        }

        public bool AreaDone(int i) => Array.TrueForAll(GameData.Areas[i].tasks, done.Contains);

        public bool AllContainedExcept(params TaskId[] except)
        {
            foreach (var info in Chapter.tasks)
                if (Array.IndexOf(except, info.id) < 0 && !done.Contains(info.id)) return false;
            return true;
        }

        public bool PrerequisitesMet(TaskId t)
        {
            foreach (var required in RequiredBefore(t))
                if (!done.Contains(required)) return false;
            return true;
        }

        static TaskId[] RequiredBefore(TaskId t)
        {
            switch (t)
            {
                case TaskId.SOC_FindHost: return new[] { TaskId.SOC_OpenAlert };
                case TaskId.SOC_Classify: return new[] { TaskId.SOC_FindHost };
                case TaskId.SOC_Escalate: return new[] { TaskId.SOC_Classify };
                case TaskId.SOC_Exit: return new[] {
                    TaskId.SOC_Badge, TaskId.SOC_OpenAlert, TaskId.SOC_FindHost,
                    TaskId.SOC_Classify, TaskId.SOC_Escalate, TaskId.SOC_Malware };
                case TaskId.NewPassword: return new[] { TaskId.FredLogin };
                case TaskId.Enable2FA: return new[] { TaskId.NewPassword };
                case TaskId.EncryptData: return new[] {
                    TaskId.LockWorkstation, TaskId.EvidencePrintout, TaskId.EvidenceNote, TaskId.ShredDocs,
                    TaskId.QuarantineUSB, TaskId.FredLogin, TaskId.NewPassword, TaskId.Enable2FA,
                    TaskId.PhishingTriage, TaskId.SocialEngineering };
                case TaskId.Escape: return new[] { TaskId.EncryptData };
                case TaskId.SRV_Unplug: return new[] { TaskId.SRV_Find };
                case TaskId.SRV_Bag: return new[] { TaskId.SRV_Unplug };
                case TaskId.SRV_Isolate: return new[] { TaskId.SRV_Find };
                case TaskId.SRV_RouteBackup: return new[] { TaskId.SRV_Isolate };
                case TaskId.SRV_Restore: return new[] { TaskId.SRV_Find, TaskId.SRV_Unplug, TaskId.SRV_Bag, TaskId.SRV_Isolate, TaskId.SRV_RouteBackup };
                case TaskId.SRV_Report: return new[] { TaskId.SRV_Restore };
                default: return Array.Empty<TaskId>();
            }
        }

        public void Mistake(string message)
        {
            if (State == GameState.Playing) Mistakes++;
            Sfx.Play(Sfx.I ? Sfx.I.error : null);
            if (!string.IsNullOrEmpty(message)) GameUI.I?.Caption(message, Palette.Red);
        }


        public TaskId CurrentObjective()
        {
            foreach (var info in Chapter.tasks)
                if (!done.Contains(info.id)) return info.id;
            return Chapter.tasks[Chapter.tasks.Length - 1].id;
        }

        public string NextHint(out TaskId task, out int tier)
        {
            task = CurrentObjective();
            hintTier.TryGetValue(task, out tier);
            var hints = GameData.Info(task).hints;
            var text = hints[Mathf.Min(tier, hints.Length - 1)];
            if (tier < hints.Length) HintsUsed++;
            hintTier[task] = tier + 1;
            return text;
        }

        public int HintTierFor(TaskId t) => hintTier.TryGetValue(t, out var v) ? v : 0;

        void Win()
        {
            RunState.Record(chapter, Elapsed, HintsUsed, Mistakes);
            StoryProgress.Complete(chapter, Elapsed, HintsUsed, Mistakes);
            Achievements.EvaluateChapter(chapter, Elapsed, HintsUsed, Mistakes);
            SetState(GameState.Won);
            Sfx.Play(Sfx.I ? Sfx.I.win : null);
        }

        void Lose()
        {
            Elapsed = TimeLimit;
            Narrator.I?.Stop();
            SetState(GameState.Lost);
            Sfx.Play(Sfx.I ? Sfx.I.glitch : null);
        }

        public void Restart(bool skipIntro)
        {
            skipIntroOnReload = skipIntro;
            SceneFlow.Load(SceneManager.GetActiveScene().name);
        }

        public void SetDifficulty(bool experienced)
        {
            if (experienced == Experienced) return;
            Experienced = experienced;
            if (State != GameState.Intro) Restart(true);
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{(int)(seconds / 60):00}:{(int)(seconds % 60):00}";
        }

#if UNITY_EDITOR
        public void DebugComplete(string task)
        {
            if (Enum.TryParse(task, out TaskId t)) Complete(t);
        }


        public string DebugClick(string label)
        {
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude))
            {
                var t = b.GetComponentInChildren<TMPro.TMP_Text>();
                if (t && t.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0 && b.interactable)
                {
                    b.onClick.Invoke();
                    return "clicked " + t.text;
                }
            }
            return "no button " + label;
        }


        public void DebugDrop(string item, string zone)
        {
            var i = GameObject.Find(item);
            var z = GameObject.Find(zone);
            if (i && z)
            {
                if (i.TryGetComponent<GrabItem>(out var g))
                {
                    g.EverHeld = true;
                    g.Teleport(z.transform.position, i.transform.rotation);
                }
                else i.transform.position = z.transform.position;
            }
        }


        public void DebugNearlyOutOfTime(float secondsLeft) { if (HasTimer) Elapsed = Mathf.Max(Elapsed, TimeLimit - secondsLeft); }

        public static string StatusLine() => I ? I.DebugStatus() : "no GameManager";

        public string DebugStatus()
        {
            var sb = new System.Text.StringBuilder($"{State} t={Elapsed:0} hints={HintsUsed} mistakes={Mistakes} code={string.Concat(DoorCode)} done=");
            foreach (var t in done) sb.Append(t).Append(',');
            return sb.ToString();
        }

        public void DebugCompleteAllButEscape()
        {
            var last = Chapter.tasks[Chapter.tasks.Length - 1].id;
            foreach (var info in Chapter.tasks)
                if (info.id != last) Complete(info.id);
        }
#endif
    }
}
