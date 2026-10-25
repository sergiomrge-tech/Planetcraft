using UnityEngine;

namespace Planetcraft.World
{
    public class Spinner : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float degreesPerSecond = 90f;

        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
