using UnityEngine;

namespace Planetcraft.World
{
    // Andar ocioso simples: escolhe um ponto perto de casa (fora d'água), caminha até lá,
    // para um pouco e repete. Segue o relevo do terreno; o corpo balança ou pula enquanto anda.
    public class AnimalWander : MonoBehaviour
    {
        [SerializeField] float wanderRadius = 25f;
        [SerializeField] float speed = 2.5f;
        [SerializeField] float turnSpeed = 180f;
        [SerializeField] Vector2 idleTime = new(1.5f, 5f);
        [SerializeField] Transform body;
        [Tooltip("Saltador: pula em arcos em vez de balançar.")]
        [SerializeField] bool hops;
        [SerializeField] float bobHeight = 0.08f;
        [SerializeField] float stepFrequency = 4f;
        [Tooltip("Modelos animados (Sketchfab): o parâmetro bool \"Moving\" troca entre parado e andando; sem balanço procedural.")]
        [SerializeField] Animator animator;

        static readonly int MovingId = Animator.StringToHash("Moving");
        static WaterReactor water;

        Terrain terrain;
        Vector3 home;
        Vector3 target;
        float idleUntil;
        float stepPhase;
        Vector3 bodyRest;

        void Start()
        {
            terrain = Terrain.activeTerrain;
            if (water == null) water = FindAnyObjectByType<WaterReactor>();
            home = transform.position;
            if (body != null) bodyRest = body.localPosition;
            PickTarget();
        }

        void Update()
        {
            if (terrain == null) return;

            if (Time.time < idleUntil)
            {
                Animate(false);
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            if (to.magnitude < 0.5f)
            {
                idleUntil = Time.time + Random.Range(idleTime.x, idleTime.y);
                PickTarget();
                return;
            }

            var look = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            pos += transform.forward * (speed * Time.deltaTime);
            pos.y = GroundHeight(pos);

            // Não entra na água: se o próximo passo afunda, desiste desse destino.
            if (IsUnderwater(pos))
            {
                PickTarget();
                return;
            }
            transform.position = pos;
            Animate(true);
        }

        void Animate(bool moving)
        {
            if (animator != null)
            {
                animator.SetBool(MovingId, moving);
                return;
            }
            if (body == null) return;
            if (!moving)
            {
                body.localPosition = Vector3.Lerp(body.localPosition, bodyRest, 10f * Time.deltaTime);
                return;
            }
            stepPhase += stepFrequency * Time.deltaTime * Mathf.PI * 2f;
            float s = hops ? Mathf.Abs(Mathf.Sin(stepPhase * 0.5f)) * bobHeight * 4f : Mathf.Sin(stepPhase) * bobHeight;
            body.localPosition = bodyRest + Vector3.up * s;
        }

        void PickTarget()
        {
            for (int i = 0; i < 8; i++)
            {
                var c = Random.insideUnitCircle * wanderRadius;
                var p = home + new Vector3(c.x, 0f, c.y);
                p.y = GroundHeight(p);
                if (!IsUnderwater(p))
                {
                    target = p;
                    return;
                }
            }
            target = home;
        }

        float GroundHeight(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static bool IsUnderwater(Vector3 p) => water != null && p.y < water.transform.position.y + 0.2f;
    }
}
