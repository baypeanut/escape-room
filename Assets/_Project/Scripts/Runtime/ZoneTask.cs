using UnityEngine;

namespace CSER
{

    [RequireComponent(typeof(DropZone))]
    public class ZoneTask : MonoBehaviour
    {
        public TaskId task;

        void Start()
        {
            var zone = GetComponent<DropZone>();
            zone.Accepted += item =>
            {
                item.Deposit(zone.GetComponent<Collider>().bounds);
                Sfx.PlayAt(Sfx.I ? Sfx.I.drop : null, item.transform.position);
                GameManager.I.Complete(task);
            };
        }
    }
}
