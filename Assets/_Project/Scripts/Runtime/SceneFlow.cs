using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CSER
{

    public class SceneFlow : MonoBehaviour
    {
        static SceneFlow runner;
        static bool busy;


        public static void LoadMenu()
        {
            if (busy) return;
            RunState.Reset();
            Load(GameData.MenuScene);
        }

        public static void LoadChapter(int number)
        {
            if (busy) return;
            RunState.BeginIfIdle();
            Load(GameData.Chapters[number - 1].scene);
        }

        public static void Load(string scene)
        {
            if (busy) return;
            if (!runner)
            {
                runner = new GameObject("SceneFlow").AddComponent<SceneFlow>();
                DontDestroyOnLoad(runner.gameObject);
            }
            runner.StartCoroutine(runner.Run(scene));
        }

        IEnumerator Run(string scene)
        {
            busy = true;
            if (Fader.I) yield return Fader.I.FadeTo(1f, 0.6f);
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            busy = false;
        }
    }
}
