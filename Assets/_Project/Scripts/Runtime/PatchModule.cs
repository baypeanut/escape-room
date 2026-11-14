using System.Collections;
using UnityEngine;

namespace CSER
{
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(GrabItem))]
    public sealed class PatchModule : MonoBehaviour
    {
        public PatchRoutingStation station;
        public GrabItem item;
        public Transform tray;
        public bool corner;
        public Renderer[] conductors;

        public int SocketIndex { get; private set; } = -1;
        public int QuarterTurns { get; private set; }
        public bool Latched { get; private set; }
        public int ConnectionMask
        {
            get
            {
                int mask = corner ? 3 : 5;
                int turn = QuarterTurns & 3;
                return ((mask << turn) | (mask >> (4 - turn))) & 15;
            }
        }

        bool wasHeld;
        Coroutine returning;
        Rigidbody body;

        void Awake()
        {
            if (!item) item = GetComponent<GrabItem>();
            body = GetComponent<Rigidbody>();
            if (body) body.interpolation = RigidbodyInterpolation.None;
        }

        void Update()
        {
            if (!item || Latched || !station) return;
            bool held = item.IsHeld;
            if (held && !wasHeld)
            {
                if (returning != null) StopCoroutine(returning);
                returning = null;
                station.Remove(this);
            }
            if (!held && wasHeld && !station.TryPlace(this)) ReturnToTray();
            wasHeld = held;
        }

        public void SetSeat(int index, int quarterTurns)
        {
            SocketIndex = index;
            QuarterTurns = quarterTurns & 3;
            if (item) item.Mounted = index >= 0;
        }

        public void Latch()
        {
            if (SocketIndex < 0 || !station) return;
            Latched = true;
            Transform socket = station.sockets[SocketIndex];
            item.Deposit(new Bounds(socket.position, Vector3.one * 0.01f));
            item.Teleport(socket.position, station.transform.rotation * Quaternion.Euler(0, 0, QuarterTurns * 90));
        }

        public void ReturnToTray()
        {
            if (Latched || !tray || !item) return;
            station.Remove(this);
            if (returning != null) StopCoroutine(returning);
            returning = StartCoroutine(Return());
        }

        IEnumerator Return()
        {
            if (body) body.isKinematic = true;
            Vector3 start = transform.position;
            Quaternion rotation = transform.rotation;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.3f)
            {
                if (item.IsHeld) yield break;
                float eased = t * t * (3 - 2 * t);
                item.Teleport(Vector3.Lerp(start, tray.position, eased), Quaternion.Slerp(rotation, tray.rotation, eased));
                yield return null;
            }
            item.Teleport(tray.position, tray.rotation);
            returning = null;
        }
    }
}
