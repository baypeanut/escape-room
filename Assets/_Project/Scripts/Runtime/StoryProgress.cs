using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CSER
{

    public static class StoryProgress
    {
        const string KeyUnlocked = "cser.unlocked";
        const string KeyPoints = "cser.points";
        const string KeyResult = "cser.result.";

        public static int UnlockedChapter => Mathf.Clamp(PlayerPrefs.GetInt(KeyUnlocked, 1), 1, GameData.Chapters.Length);

        public static bool IsUnlocked(int chapter) => chapter <= UnlockedChapter;

        public static bool HasPoint(TaskId t) => Points.Contains(t.ToString());

        public static int PointCount => Points.Count;

        static HashSet<string> Points => new HashSet<string>(PlayerPrefs.GetString(KeyPoints, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        public static void AddPoint(TaskId t)
        {
            var set = Points;
            if (set.Add(t.ToString()))
            {
                PlayerPrefs.SetString(KeyPoints, string.Join(",", set));
                PlayerPrefs.Save();
            }
        }

        public struct Result
        {
            public bool done;
            public float seconds;
            public int hints, mistakes;
        }

        public static Result Get(int chapter) => Parse(PlayerPrefs.GetString(KeyResult + chapter, ""));

        public static Result GetLatest(int chapter) => Parse(PlayerPrefs.GetString(KeyResult + chapter + ".latest", ""));

        static Result Parse(string value)
        {
            var parts = value.Split(';');
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0
                || !int.TryParse(parts[1], out int hints) || hints < 0
                || !int.TryParse(parts[2], out int mistakes) || mistakes < 0) return default;
            return new Result { done = true, seconds = seconds, hints = hints, mistakes = mistakes };
        }

        public static void Complete(int chapter, float seconds, int hints, int mistakes)
        {
            if (chapter < 1 || chapter > GameData.Chapters.Length || float.IsNaN(seconds)
                || float.IsInfinity(seconds) || seconds < 0 || hints < 0 || mistakes < 0) return;
            var prev = Get(chapter);
            string record = string.Format(CultureInfo.InvariantCulture, "{0};{1};{2}", seconds, hints, mistakes);
            PlayerPrefs.SetString(KeyResult + chapter + ".latest", record);
            if (!prev.done || seconds < prev.seconds
                || (Mathf.Approximately(seconds, prev.seconds) && hints + mistakes < prev.hints + prev.mistakes))
                PlayerPrefs.SetString(KeyResult + chapter, record);
            if (chapter + 1 > UnlockedChapter) PlayerPrefs.SetInt(KeyUnlocked, Mathf.Min(chapter + 1, GameData.Chapters.Length));
            PlayerPrefs.Save();
        }

        public static bool StoryComplete => Get(GameData.Chapters.Length).done;

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyUnlocked);
            PlayerPrefs.DeleteKey(KeyPoints);
            for (int i = 1; i <= GameData.Chapters.Length; i++)
            {
                PlayerPrefs.DeleteKey(KeyResult + i);
                PlayerPrefs.DeleteKey(KeyResult + i + ".latest");
            }
            Achievements.ResetAll();
            PlayerPrefs.Save();
        }
    }
}
