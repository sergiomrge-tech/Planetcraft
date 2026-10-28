using Planetcraft.Combat;
using Planetcraft.Core;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.Building
{
    // Utilidades da base que o jogador usa com [E]: dormir no beliche, curar na enfermaria, beber no purificador.
    public class BaseUtility : MonoBehaviour, IInteractable
    {
        public enum Mode { Sleep, Heal, Drink }

        [SerializeField] Mode mode;
        [SerializeField] float cooldown = 8f;

        Machine machine;
        float readyAt;

        void Awake() => machine = GetComponent<Machine>();

        bool Powered => machine == null || machine.IsRunning;

        public string Prompt
        {
            get
            {
                string verb = mode switch { Mode.Sleep => "Dormir", Mode.Heal => "Usar enfermaria", _ => "Beber água" };
                if (!Powered) return $"{Loc.T(verb)} ({Loc.T("sem energia")})";
                if (Time.time < readyAt) return $"{Loc.T(verb)} ({Mathf.CeilToInt(readyAt - Time.time)} s)";
                return $"{GameInput.Hint(GameInput.Interact)} {Loc.T(verb)}";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!Powered || Time.time < readyAt) return;
            var combat = interactor.GetComponentInParent<PlayerCombat>();
            var survival = interactor.GetComponentInParent<SurvivalStats>();
            if (combat == null || survival == null) return;
            readyAt = Time.time + cooldown;
            switch (mode)
            {
                case Mode.Sleep:
                    combat.Health.Heal(combat.Health.Max * 0.5f);
                    survival.SetValues(survival.OxygenNormalized * 100f, survival.WaterNormalized * 100f - 5f, survival.FoodNormalized * 100f - 5f);
                    Toast("Você descansou um pouco");
                    break;
                case Mode.Heal:
                    combat.Health.Heal(combat.Health.Max);
                    Toast("Ferimentos tratados");
                    break;
                default:
                    survival.SetValues(survival.OxygenNormalized * 100f, survival.WaterNormalized * 100f + 40f, survival.FoodNormalized * 100f);
                    Toast("Água purificada");
                    break;
            }
            Sfx.Craft();
        }

        static void Toast(string text)
        {
            if (UI.GameHud.Instance != null) UI.GameHud.Instance.ShowToast(Loc.T(text), 2f);
        }
    }
}
