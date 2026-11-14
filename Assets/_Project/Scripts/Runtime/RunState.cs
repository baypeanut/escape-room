using UnityEngine;

namespace CSER
{


    public static class RunState
    {
        static double startTime = -1;

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
            startTime = -1;
            Checkpoint = 0;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnPlay() => Reset();
    }
}
