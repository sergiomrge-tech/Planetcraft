using System.Collections;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Intro
{
    // Porta deslizante de duas folhas da estação. Fica trancada até a abertura liberar (Unlock) e então abre sozinha
    // quando o jogador chega perto, com o som e a luz de estado passando de vermelho para verde.
    public class SlidingDoor : MonoBehaviour
    {
        [SerializeField] Transform left;
        [SerializeField] Transform right;
        [SerializeField] float travel = 0.9f;
        [SerializeField] float openDistance = 2.6f;
        [SerializeField] Light statusLight;

        Vector3 leftClosed, rightClosed;
        Transform player;
        bool unlocked, open;

        public bool IsOpen => open;

        void Awake()
        {
            leftClosed = left.localPosition;
            rightClosed = right.localPosition;
            SetStatus(false);
        }

        public void Unlock(Transform who)
        {
            unlocked = true;
            player = who;
            SetStatus(true);
        }

        void Update()
        {
            if (!unlocked || open || player == null) return;
            var d = player.position - transform.position;
            d.y = 0f;
            if (d.magnitude < openDistance) StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            open = true;
            Sfx.Build();
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.1f;
                float k = Mathf.SmoothStep(0f, 1f, t);
                left.localPosition = leftClosed + Vector3.left * (travel * k);
                right.localPosition = rightClosed + Vector3.right * (travel * k);
                yield return null;
            }
        }

        void SetStatus(bool free)
        {
            if (statusLight != null) statusLight.color = free ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.25f, 0.15f);
        }
    }
}
