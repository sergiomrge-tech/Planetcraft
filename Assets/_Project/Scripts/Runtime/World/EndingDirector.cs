using System.Collections;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.UI;
using UnityEngine;

namespace Planetcraft.World
{
    // Final do jogo: ao alcançar o estágio-meta, a nave-mãe desce do céu até pairar sobre o vale
    // e o HUD mostra a tela de vitória. Depois o jogador pode continuar jogando.
    public class EndingDirector : MonoBehaviour
    {
        public static bool Seen { get; set; }

        [SerializeField] TerraformStage goalStage;
        [SerializeField] Transform ship;
        [SerializeField] Vector3 hoverPosition;
        [SerializeField] float startHeight = 900f;
        [SerializeField] float descentSeconds = 28f;
        [SerializeField] AudioSource rumble;

        bool arrived;
        float elapsed;

        public float DescentSeconds => descentSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Seen = false;

        void Start()
        {
            ship.gameObject.SetActive(false);
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            tm.StageReached += OnStageReached;
            if (!tm.HasReached(goalStage)) return;
            if (Seen) PlaceHovering();
            else StartCoroutine(Descent());
        }

        void OnDestroy()
        {
            if (TerraformManager.Instance != null) TerraformManager.Instance.StageReached -= OnStageReached;
        }

        void OnStageReached(TerraformStage stage)
        {
            if (stage == goalStage && !Seen && !ship.gameObject.activeSelf) StartCoroutine(Descent());
        }

        // Dev/capturas: pula quase toda a descida.
        public void SkipAhead() => elapsed = Mathf.Max(elapsed, descentSeconds - 1f);

        IEnumerator Descent()
        {
            ship.gameObject.SetActive(true);
            var top = hoverPosition + Vector3.up * startHeight;
            if (rumble != null)
            {
                rumble.clip = ProceduralAudio.Rumble();
                rumble.loop = true;
                rumble.volume = 0f;
                rumble.Play();
            }

            elapsed = 0f;
            while (elapsed < descentSeconds)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / descentSeconds);
                float eased = 1f - Mathf.Pow(1f - k, 3f); // rápido no alto, freia ao chegar
                ship.position = Vector3.Lerp(top, hoverPosition, eased);
                ship.rotation = Quaternion.Euler(0f, Mathf.Lerp(-40f, 0f, eased), 0f);
                if (rumble != null) rumble.volume = Mathf.Sin(k * Mathf.PI) * 0.9f + 0.1f * (1f - k);
                yield return null;
            }

            if (rumble != null) rumble.Stop();
            PlaceHovering();
            Seen = true;
            if (GameHud.Instance != null)
                GameHud.Instance.ShowEnding(
                    $"Tempo de jogo: {PlayClock.Formatted}\n" +
                    $"Índice de terraformação: {TiFormat.Format(TerraformManager.Instance.Ti)}\n" +
                    $"Construções ativas: {PlacedObject.All.Count}\n" +
                    $"Registros encontrados: {System.Linq.Enumerable.Count(StoryLogs.All)} de 5");
        }

        void PlaceHovering()
        {
            ship.gameObject.SetActive(true);
            ship.SetPositionAndRotation(hoverPosition, Quaternion.identity);
            arrived = true;
        }

        void Update()
        {
            if (!arrived) return;
            ship.position = hoverPosition + Vector3.up * Mathf.Sin(Time.time * 0.4f) * 1.5f;
        }
    }
}
