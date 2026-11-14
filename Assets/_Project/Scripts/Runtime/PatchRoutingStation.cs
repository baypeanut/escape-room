using TMPro;
using UnityEngine;

namespace CSER
{
    public sealed class PatchRoutingStation : MonoBehaviour
    {
        public PatchModule[] modules;
        public Transform[] sockets;
        public Renderer[] terminals;
        public TMP_Text statusLabel;
        public TaskId completionTask = TaskId.SRV_RouteBackup;
        public float snapRadius = 0.12f;
        public float rotationTolerance = 28f;

        public bool Solved { get; private set; }
        public int SeatedCount
        {
            get
            {
                int count = 0;
                foreach (var module in seated) if (module) count++;
                return count;
            }
        }

        readonly PatchModule[] seated = new PatchModule[4];
        static readonly int ResolvedId = Shader.PropertyToID("_Resolved");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        GameManager manager;
        MaterialPropertyBlock properties;
        float phase;
        float resolvedBlend;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        void Start()
        {
            manager = GameManager.I;
            if (manager)
            {
                manager.TaskCompleted += OnTask;
                manager.StateChanged += OnState;
            }
            RefreshStatus();
        }

        void OnDestroy()
        {
            if (!manager) return;
            manager.TaskCompleted -= OnTask;
            manager.StateChanged -= OnState;
        }

        void OnTask(TaskId task)
        {
            if (task == TaskId.SRV_Isolate) TryComplete();
            RefreshStatus();
        }

        void OnState(GameState state)
        {
            RefreshStatus();
        }

        public void Remove(PatchModule module)
        {
            if (!module || Solved) return;
            for (int i = 0; i < seated.Length; i++)
                if (seated[i] == module) seated[i] = null;
            module.SetSeat(-1, module.QuarterTurns);
            RefreshStatus();
        }

        public bool TryPlace(PatchModule module)
        {
            var gm = manager ? manager : GameManager.I;
            if (!gm || gm.State != GameState.Playing || Solved || !module || module.station != this
                || modules == null || System.Array.IndexOf(modules, module) < 0
                || !module.item || module.item.IsHeld || !module.item.EverHeld || sockets == null || sockets.Length != 4)
                return false;

            Remove(module);
            Physics.SyncTransforms();
            module.TryGetComponent<Rigidbody>(out var body);
            Vector3 position = body ? body.position : module.transform.position;
            Quaternion rotation = body ? body.rotation : module.transform.rotation;
            int index = -1;
            float closest = snapRadius;
            for (int i = 0; i < sockets.Length; i++)
            {
                if (!sockets[i]) continue;
                float distance = Vector3.Distance(position, sockets[i].position);
                if (distance < closest) { closest = distance; index = i; }
            }
            if (index < 0) return false;
            if (seated[index])
            {
                GameUI.I?.Caption("That bay is occupied. Lift its cartridge out first.", Palette.Ink);
                return false;
            }
            Vector3 right = transform.InverseTransformDirection(rotation * Vector3.right);
            float angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            int quarter = Mathf.RoundToInt(angle / 90f);
            if (Vector3.Dot(rotation * Vector3.forward, transform.forward) < 0.8f
                || Mathf.Abs(Mathf.DeltaAngle(angle, quarter * 90)) > rotationTolerance)
            {
                GameUI.I?.Caption("Face the grooves toward you, then turn the cartridge to line up with the bay.", Palette.Ink);
                return false;
            }

            quarter = (quarter % 4 + 4) % 4;
            seated[index] = module;
            module.SetSeat(index, quarter);
            module.item.Teleport(sockets[index].position, transform.rotation * Quaternion.Euler(0, 0, quarter * 90));
            if (body) body.isKinematic = true;
            Sfx.PlayAt(Sfx.I ? Sfx.I.drop : null, sockets[index].position, 0.55f);
            Sfx.Haptic(0.22f, 0.04f);
            if (!TryComplete()) RefreshStatus();
            return true;
        }

        public bool HasValidRoute()
        {
            if (SeatedCount != 3 || !seated[0] || !seated[3]) return false;
            if ((seated[0].ConnectionMask & 4) == 0 || (seated[3].ConnectionMask & 1) == 0) return false;
            for (int i = 0; i < seated.Length; i++)
            {
                if (!seated[i]) continue;
                int mask = seated[i].ConnectionMask;
                for (int direction = 0; direction < 4; direction++)
                {
                    if ((mask & (1 << direction)) == 0) continue;
                    if ((i == 0 && direction == 2) || (i == 3 && direction == 0)) continue;
                    int neighbor = Neighbor(i, direction);
                    if (neighbor < 0 || !seated[neighbor]
                        || (seated[neighbor].ConnectionMask & (1 << ((direction + 2) & 3))) == 0)
                        return false;
                }
            }
            int visited = 1;
            for (int pass = 0; pass < 4; pass++)
                for (int i = 0; i < 4; i++)
                    if ((visited & (1 << i)) != 0)
                        for (int direction = 0; direction < 4; direction++)
                        {
                            if ((seated[i].ConnectionMask & (1 << direction)) == 0) continue;
                            int neighbor = Neighbor(i, direction);
                            if (neighbor >= 0 && seated[neighbor]) visited |= 1 << neighbor;
                        }
            int occupied = 0;
            for (int i = 0; i < 4; i++) if (seated[i]) occupied |= 1 << i;
            return visited == occupied;
        }

        static int Neighbor(int index, int direction)
        {
            switch (direction)
            {
                case 0: return index % 2 == 0 ? index + 1 : -1;
                case 1: return index >= 2 ? index - 2 : -1;
                case 2: return index % 2 == 1 ? index - 1 : -1;
                default: return index < 2 ? index + 2 : -1;
            }
        }

        bool TryComplete()
        {
            var gm = manager ? manager : GameManager.I;
            if (Solved || !gm || gm.State != GameState.Playing || !gm.IsDone(TaskId.SRV_Isolate) || !HasValidRoute()) return false;
            if (!gm.Complete(completionTask) && !gm.IsDone(completionTask)) return false;
            Solved = true;
            foreach (var module in modules) if (module) module.Latch();
            Sfx.PlayAt(Sfx.I ? Sfx.I.confirm : null, transform.position, 0.65f);
            RefreshStatus();
            return true;
        }

        void RefreshStatus()
        {
            if (!statusLabel) return;
            var gm = manager ? manager : GameManager.I;
            statusLabel.color = Solved ? new Color(0.45f, 1f, 0.8f) : new Color(0.9f, 0.94f, 1f);
            if (Solved) statusLabel.text = "Route verified. The backup console is ready.";
            else if (gm && gm.State == GameState.Lost) statusLabel.text = "Session ended. Retry to restore the connection.";
            else if (!gm || !gm.IsDone(TaskId.SRV_Isolate)) statusLabel.text = "Isolate the affected port before reconnecting the backup.";
            else if (SeatedCount == 3) statusLabel.text = "The line is open. Turn or move a cartridge.";
            else statusLabel.text = "Join BACKUP to RECOVERY using all three cartridges.";
        }

        void Update()
        {
            bool reduceMotion = AlertStateManager.I && AlertStateManager.I.ReduceFlashing;
            if (!reduceMotion) phase = Mathf.Repeat(phase + Time.deltaTime * 0.1f, 256f);
            resolvedBlend = Mathf.MoveTowards(resolvedBlend, Solved ? 1f : 0f, Time.deltaTime * 1.8f);
            if (terminals != null)
                foreach (var terminal in terminals) UpdateConductor(terminal);
            if (modules == null) return;
            foreach (var module in modules)
            {
                if (!module || module.conductors == null) continue;
                foreach (var conductor in module.conductors)
                {
                    UpdateConductor(conductor);
                }
            }
        }

        void UpdateConductor(Renderer conductor)
        {
            if (!conductor) return;
            conductor.GetPropertyBlock(properties);
            properties.SetFloat(ResolvedId, resolvedBlend);
            properties.SetFloat(PhaseId, phase);
            conductor.SetPropertyBlock(properties);
        }
    }
}
