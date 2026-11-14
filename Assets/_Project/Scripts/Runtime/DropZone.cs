using System;
using System.Collections;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CSER
{

    public class DropZone : MonoBehaviour
    {
        public string zoneName = "Zone";
        public ItemKind[] accepts;
        public Transform intake;
        public event Action<GrabItem> Accepted;

        void OnTriggerStay(Collider other)
        {
            TryAccept(other.GetComponentInParent<GrabItem>());
        }

        public bool TryAccept(GrabItem item)
        {
            if (!GameManager.I || GameManager.I.State != GameState.Playing) return false;
            if (!item || !item.gameObject.activeInHierarchy || item.Consumed || item.Deposited || item.Stored || item.Mounted || item.IsHeld || !item.EverHeld) return false;
            if (accepts != null && Array.IndexOf(accepts, item.kind) >= 0)
            {
                if (Accepted == null) return false;
                Accepted?.Invoke(item);
                return true;
            }
            else if (item.CanBeRejected)
            {
                var head = GameUI.I && GameUI.I.head ? GameUI.I.head.position : transform.position + Vector3.back;
                item.Eject(GetComponent<Collider>().bounds, head);
                if (item.kind != ItemKind.Prop)
                    GameManager.I?.Mistake(string.IsNullOrEmpty(item.wrongZoneHint) ? $"That doesn't belong in the {zoneName}." : item.wrongZoneHint);
            }
            return false;
        }
    }
}
