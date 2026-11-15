using TMPro;
using UnityEngine;

namespace CSER
{

    public class DoorKeypad : MonoBehaviour
    {
        public RectTransform canvas;
        public Hinge door;
        TMP_Text display;
        string entry = "";
        bool open;

        void Start()
        {
            var bg = UIKit.Panel(UIKit.ContentRoot(canvas), "Bg", Palette.Hex("2A2F38"));
            bg.rectTransform.Fill();

            var screen = UIKit.Panel(bg.transform, "Screen", Palette.Dark);
            screen.rectTransform.Top(80, 14, 20, 110);
            display = UIKit.Text(screen.transform, "", 64, Palette.Terminal, TextAlignmentOptions.Center, FontStyles.Bold);
            display.rectTransform.Fill();
            var back = UIKit.Button(bg.transform, "<", Palette.Hex("6A7385"), Color.white, () => Press("<"), 48, "KeyBack");
            back.GetComponent<RectTransform>().At(new Vector2(1, 1), new Vector2(-20, -12), new Vector2(88, 88), new Vector2(1, 1));
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                Color c = k == "OK" ? Palette.Green : k == "C" ? Palette.Red : Palette.Hex("4A5160");
                var b = UIKit.Button(bg.transform, k, c, Color.white, () => Press(k), 44, "Key" + k);
                b.GetComponent<RectTransform>().At(new Vector2(0, 1), new Vector2(20 + (i % 3) * 110, -104 - (i / 3) * 103), new Vector2(100, 100), new Vector2(0, 1));
            }
            Show();
        }

        public void Press(string k)
        {
            if (open || flashing || !GameManager.I || GameManager.I.State != GameState.Playing) return;
            Sfx.Haptic(0.25f, 0.03f);
            Sfx.Play(Sfx.I ? Sfx.I.key : null, 0.5f);
            if (k == "C") entry = "";
            else if (k == "<") { if (entry.Length > 0) entry = entry.Substring(0, entry.Length - 1); }
            else if (k == "OK") Submit();
            else if (entry.Length < 4) entry += k;
            Show();
        }

        void Submit()
        {
            var gm = GameManager.I;
            if (gm.State != GameState.Playing) return;
            string code = string.Concat(gm.DoorCode);
            if (entry == code && gm.AllContainedExcept(TaskId.Escape))
            {
                open = true;
                display.text = "OPEN";
                display.color = Palette.Green;
                door.Open();
                gm.Complete(TaskId.Escape);
            }
            else
            {
                gm.Mistake(entry == code ? "Override refused: finish every containment task before the door will open." : "Wrong passkey. Check the Data Section popup or the Containment Board.");
                entry = "";
                StartCoroutine(Flash("DENIED"));
            }
        }

        bool flashing;


        System.Collections.IEnumerator Flash(string text)
        {
            flashing = true;
            display.text = text;
            display.color = Palette.Red;
            yield return new WaitForSeconds(0.9f);
            display.color = Palette.Terminal;
            flashing = false;
            Show();
        }

        void Show()
        {
            if (!open && !flashing && display) display.text = string.Join(" ", entry.PadRight(4, '_').ToCharArray());
        }

#if UNITY_EDITOR
        public void DebugEnterCode() { entry = string.Concat(GameManager.I.DoorCode); Submit(); }
#endif
    }
}
