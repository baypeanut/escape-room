using TMPro;
using UnityEngine;

namespace CSER
{
    public sealed class NorthwindAtmosphere : MonoBehaviour
    {
        public TaskId recoveryTask;
        public Renderer signalCore;
        public ParticleSystem signalMotes;
        public TMP_Text statusLabel;
        public string pendingText = "! CONTAINMENT PENDING";
        public string resolvedText = "OK - CONTAINMENT VERIFIED";

        public bool Resolved { get; private set; }
        public bool Emitting => signalMotes && signalMotes.isEmitting;

        static readonly int ResolvedId = Shader.PropertyToID("_Resolved");
        static readonly int UrgencyId = Shader.PropertyToID("_Urgency");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");

        GameManager manager;
        MaterialPropertyBlock properties;
        float resolvedBlend;
        float urgencyBlend;
        float phase;
        bool emitting;
        bool subscribed;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        void Start()
        {
            Bind();
        }

        void OnEnable()
        {
            if (properties != null) Bind();
        }

        void Bind()
        {
            if (subscribed) return;
            manager = GameManager.I ? GameManager.I : FindAnyObjectByType<GameManager>();
            if (!manager) return;
            manager.TaskCompleted += OnTask;
            manager.StateChanged += OnState;
            subscribed = true;
            RefreshStatus();
        }

        void OnDisable()
        {
            if (manager && subscribed)
            {
                manager.TaskCompleted -= OnTask;
                manager.StateChanged -= OnState;
            }
            subscribed = false;
            emitting = false;
            if (signalMotes)
                signalMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnTask(TaskId task)
        {
            if (task == recoveryTask) RefreshStatus();
        }

        void OnState(GameState state)
        {
            RefreshStatus();
        }

        void RefreshStatus()
        {
            Resolved = manager && manager.IsDone(recoveryTask);
            if (!statusLabel) return;
            statusLabel.text = Resolved ? resolvedText : manager && manager.State == GameState.Lost
                ? "! SESSION ENDED - UNRESOLVED" : pendingText;
            statusLabel.color = Resolved ? new Color(0.45f, 1f, 0.8f)
                : new Color(1f, 0.78f, 0.38f);
        }

        void Update()
        {
            if (!manager) return;
            bool playing = manager.State == GameState.Playing;
            float urgency = !Resolved && manager.State == GameState.Lost ? 1f
                : !Resolved && manager.HasTimer ? Mathf.InverseLerp(0.6f, 0.95f, manager.Exfil01) : 0f;
            float blend = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
            resolvedBlend = Mathf.Lerp(resolvedBlend, Resolved ? 1f : 0f, blend);
            urgencyBlend = Mathf.Lerp(urgencyBlend, urgency, blend);
            bool reduceMotion = AlertStateManager.I && AlertStateManager.I.ReduceFlashing;
            if (!reduceMotion && playing)
                phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * (Resolved ? 0.08f : 0.22f), 256f);

            if (signalCore)
            {
                signalCore.GetPropertyBlock(properties);
                properties.SetFloat(ResolvedId, resolvedBlend);
                properties.SetFloat(UrgencyId, urgencyBlend);
                properties.SetFloat(PhaseId, phase);
                signalCore.SetPropertyBlock(properties);
            }

            if (!signalMotes) return;
            bool shouldEmit = playing && !Resolved;
            if (shouldEmit != emitting)
            {
                emitting = shouldEmit;
                if (emitting) signalMotes.Play(true);
                else signalMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (!emitting) return;
            var emission = signalMotes.emission;
            emission.rateOverTime = Mathf.Lerp(8f, 14f, urgencyBlend);
            var main = signalMotes.main;
            main.startColor = Color.Lerp(new Color(1f, 0.64f, 0.2f, 0.85f),
                new Color(1f, 0.28f, 0.16f, 0.85f), urgencyBlend);
        }
    }
}
