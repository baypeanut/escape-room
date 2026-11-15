using System;
using System.Collections.Generic;
using UnityEngine;

namespace CSER
{
    public static class Achievements
    {
        const string Key = "cser.achievements";

        public struct Entry
        {
            public string id;
            public string title;
            public string detail;
        }

        public static readonly Entry[] Catalog =
        {
            new Entry { id = "first_alert", title = "First Alert", detail = "Open the critical SIEM alert on night shift." },
            new Entry { id = "night_shift", title = "Night Shift Clear", detail = "Contain the SOC incident and leave for Floor 3." },
            new Entry { id = "office_lockdown", title = "Office Lockdown", detail = "Stop the AURORA upload and escape Fred's office." },
            new Entry { id = "server_clean", title = "Server Room Clean", detail = "Remove the implant and close the incident." },
            new Entry { id = "full_story", title = "Midnight Contained", detail = "Finish the full three-chapter story." },
            new Entry { id = "clean_hands", title = "Clean Hands", detail = "Finish any chapter with zero mistakes." },
            new Entry { id = "quiet_hints", title = "Quiet Shift", detail = "Finish any chapter without using hints." },
            new Entry { id = "circuit_route", title = "Recovery Route", detail = "Verify the physical backup circuit." },
            new Entry { id = "evidence_keeper", title = "Evidence Keeper", detail = "Recover a misplaced item with BAG." },
            new Entry { id = "speed_office", title = "Under the Wire", detail = "Escape the office with at least two minutes left." },
        };

        static HashSet<string> Unlocked =>
            new HashSet<string>(PlayerPrefs.GetString(Key, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        public static bool Has(string id) => Unlocked.Contains(id);

        public static int Count
        {
            get
            {
                int n = 0;
                var set = Unlocked;
                foreach (var entry in Catalog) if (set.Contains(entry.id)) n++;
                return n;
            }
        }

        public static void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var set = Unlocked;
            if (!set.Add(id)) return;
            PlayerPrefs.SetString(Key, string.Join(",", set));
            PlayerPrefs.Save();
            Entry found = default;
            foreach (var entry in Catalog) if (entry.id == id) { found = entry; break; }
            if (!string.IsNullOrEmpty(found.title))
                GameUI.I?.Toast("Achievement unlocked", "<b>" + found.title + "</b>\n" + found.detail, Palette.Mint, 7f);
        }

        public static void EvaluateChapter(int chapter, float seconds, int hints, int mistakes)
        {
            float timeLimit = 0f;
            if (chapter >= 1 && chapter <= GameData.Chapters.Length)
            {
                var info = GameData.Chapters[chapter - 1];
                timeLimit = 60f * (GameManager.Experienced ? info.experiencedMinutes : info.normalMinutes);
            }
            if (GameManager.I && GameManager.I.chapter == chapter) timeLimit = GameManager.I.TimeLimit;
            if (chapter == 1) Unlock("night_shift");
            if (chapter == 2) Unlock("office_lockdown");
            if (chapter == 3) Unlock("server_clean");
            if (chapter == GameData.Chapters.Length && StoryProgress.Get(1).done && StoryProgress.Get(2).done)
                Unlock("full_story");
            if (mistakes == 0) Unlock("clean_hands");
            if (hints == 0) Unlock("quiet_hints");
            if (chapter == 2 && timeLimit > 0f && seconds <= timeLimit - 120f) Unlock("speed_office");
        }

        public static void EvaluateTask(TaskId task)
        {
            if (task == TaskId.SOC_OpenAlert) Unlock("first_alert");
            if (task == TaskId.SRV_RouteBackup) Unlock("circuit_route");
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
