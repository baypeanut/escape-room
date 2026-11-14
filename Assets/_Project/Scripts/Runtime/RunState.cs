using UnityEngine;
using System.Collections.Generic;

namespace CSER
{


    public static class RunState
    {
        static double startTime = -1;
        static readonly Dictionary<int, StoryProgress.Result> results = new Dictionary<int, StoryProgress.Result>();
        public static int CompletedChapters => results.Count;
        public static StoryProgress.Result Get(int chapter) => results.TryGetValue(chapter, out var result) ? result : default;

        public static void Record(int chapter, float seconds, int hints, int mistakes)
        {
            results[chapter] = new StoryProgress.Result { done = true, seconds = seconds, hints = hints, mistakes = mistakes };
        }

        public static bool Running => startTime >= 0;


        public static int Checkpoint { get; private set; }


        public static float Seconds => Running ? (float)(Time.realtimeSinceStartupAsDouble - startTime) : 0f;


        public static void BeginIfIdle()
        {
            if (!Running) startTime = Time.realtimeSinceStartupAsDouble;
        }

        public static void SetCheckpoint(int chapter) => Checkpoint = chapter;

        public static void Reset()
        {
            results.Clear();
            startTime = -1;
            Checkpoint = 0;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnPlay() => Reset();
    }
}
