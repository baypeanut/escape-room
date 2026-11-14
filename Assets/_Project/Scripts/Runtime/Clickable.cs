using System;
using System.Collections;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CSER
{

    public class Clickable : MonoBehaviour
    {
        [TextArea] public string caption;
        public UnityEvent onClick = new UnityEvent();
        InteractableUnityEventWrapper wrapper;

        void Awake()
        {
            wrapper = GetComponentInChildren<InteractableUnityEventWrapper>(true);
            if (wrapper) wrapper.WhenSelect.AddListener(Click);
        }

        public void Click()
        {
            if (GameManager.I && GameManager.I.State != GameState.Playing) return;
            Sfx.Haptic(0.3f, 0.04f);
            if (!string.IsNullOrEmpty(caption)) GameUI.I?.Caption(caption, Palette.Ink);
            onClick.Invoke();
        }
    }
}
