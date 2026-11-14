using System.Collections;
using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace CSER
{


    public class StartPose : MonoBehaviour
    {
        public Vector3 position;
        public float yaw;

        IEnumerator Start()
        {
            yield return null;
            yield return new WaitForSeconds(0.2f);
            var rot = Quaternion.Euler(0, yaw, 0);
            var locomotor = FindAnyObjectByType<FirstPersonLocomotor>();
            if (locomotor)
                locomotor.HandleLocomotionEvent(new LocomotionEvent(0, new Pose(position, rot),
                    LocomotionEvent.TranslationType.Absolute, LocomotionEvent.RotationType.Absolute));
            else
                transform.SetPositionAndRotation(position, rot);
        }
    }
}
