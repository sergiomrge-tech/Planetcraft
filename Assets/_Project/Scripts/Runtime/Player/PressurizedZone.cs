using UnityEngine;

namespace Planetcraft.Player
{
    // Volume trigger dentro da base: recarrega oxigênio enquanto o jogador está dentro.
    [RequireComponent(typeof(Collider))]
    public class PressurizedZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<SurvivalStats>(out var s)) s.EnterPressurizedZone();
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<SurvivalStats>(out var s)) s.ExitPressurizedZone();
        }
    }
}
