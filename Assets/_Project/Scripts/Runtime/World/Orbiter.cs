using UnityEngine;

namespace Planetcraft.World
{
    // Câmera da tela de título: gira devagar ao redor de um alvo.
    public class Orbiter : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float distance = 90f;
        [SerializeField] float height = 18f;
        [SerializeField] float degreesPerSecond = 3f;
        [SerializeField] Vector3 lookOffset = new(0f, 0f, 0f);

        float angle = 200f;

        void LateUpdate()
        {
            if (target == null) return;
            angle += degreesPerSecond * Time.deltaTime;
            var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, -distance);
            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset);
        }
    }
}
