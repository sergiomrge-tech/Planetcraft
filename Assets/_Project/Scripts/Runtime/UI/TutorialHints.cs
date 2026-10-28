using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.Power;
using Planetcraft.World;
using UnityEngine;

namespace Planetcraft.UI
{
    // Dicas de primeira vez, mostradas no HUD quando a situação aparece (nada de tutorial forçado): andar, coletar,
    // abrir o menu, construir, falta de oxigênio, falta de energia e as ruínas. Cada dica aparece uma vez por jogo
    // (máscara em PlayerPrefs, zerada por GameSession.NewGame) e nunca duas ao mesmo tempo.
    public class TutorialHints : MonoBehaviour
    {
        public const string PrefKey = "pc.hints";

        enum Hint { Move, Collect, Menu, Build, Oxygen, Power, Ruins }

        // O texto-fonte em português é a chave de tradução; {E}, {TAB} e {R} viram as teclas atuais do jogador.
        static string Template(Hint h) => h switch
        {
            Hint.Move => "{WASD} andar   ·   mouse olhar   ·   [SHIFT] correr   ·   [ESPAÇO] pular",
            Hint.Collect => "Aperte {E} para coletar o minério. Ferro, silício, gelo e magnésio abrem as primeiras máquinas.",
            Hint.Menu => "Aperte {TAB} para abrir o menu: fabrique itens e construa máquinas com o que você coletou.",
            Hint.Build => "{R} gira a construção, o clique esquerdo confirma e o direito cancela.",
            Hint.Oxygen => "Seu oxigênio está acabando! Entre num Módulo de Base ou use uma Cápsula de O2 pelo menu ({TAB}).",
            Hint.Power => "Faltou energia: as máquinas param. Construa um Gerador Eólico ou um Painel Solar.",
            _ => "Os pilares azuis no horizonte são ruínas do velho mundo: guardam registros, projetos novos e urânio.",
        };

        FirstPersonController player;
        InventoryComponent inventory;
        SurvivalStats survival;
        Interactor interactor;
        BuildPlacer placer;
        Vector3 start;
        float nextAllowed;
        float born;
        int seen;

        void Start()
        {
            player = FindAnyObjectByType<FirstPersonController>();
            inventory = FindAnyObjectByType<InventoryComponent>();
            survival = FindAnyObjectByType<SurvivalStats>();
            interactor = FindAnyObjectByType<Interactor>();
            placer = FindAnyObjectByType<BuildPlacer>();
            seen = PlayerPrefs.GetInt(PrefKey, 0);
            start = player != null ? player.transform.position : Vector3.zero;
            born = Time.unscaledTime;
            nextAllowed = Time.unscaledTime + 4f;
        }

        bool Seen(Hint h) => (seen & (1 << (int)h)) != 0;

        void Mark(Hint h)
        {
            seen |= 1 << (int)h;
            PlayerPrefs.SetInt(PrefKey, seen);
        }

        void Show(Hint h)
        {
            if (Seen(h) || GameHud.Instance == null || UIState.MenuOpen || Time.unscaledTime < nextAllowed) return;
            Mark(h);
            string text = Loc.T(Template(h))
                .Replace("{WASD}", "[WASD]")
                .Replace("{E}", GameInput.Hint(GameInput.Interact))
                .Replace("{TAB}", GameInput.Hint(GameInput.Menu))
                .Replace("{R}", GameInput.Hint(GameInput.Rotate));
            GameHud.Instance.ShowHint(text, 9f);
            nextAllowed = Time.unscaledTime + 14f;
        }

        void Update()
        {
            if (player == null || Time.timeScale == 0f) return;
            bool moved = (player.transform.position - start).sqrMagnitude > 9f;
            if (!Seen(Hint.Move))
            {
                if (moved) Mark(Hint.Move);
                else if (Time.unscaledTime - born > 3f) Show(Hint.Move);
            }

            if (interactor != null && interactor.Current is ResourceNode) Show(Hint.Collect);

            if (inventory != null && !Seen(Hint.Menu) && TotalItems(inventory.Inventory) >= 3) Show(Hint.Menu);
            if (placer != null && placer.Selected != null) Show(Hint.Build);
            if (survival != null && !survival.InPressurizedZone && survival.OxygenNormalized < 0.5f) Show(Hint.Oxygen);
            if (PowerGrid.Demand > 0f && !PowerGrid.IsPowered) Show(Hint.Power);
            var tm = TerraformManager.Instance;
            if (tm != null && tm.Ti >= 400_000) Show(Hint.Ruins);
        }

        static int TotalItems(Inventory inv)
        {
            int n = 0;
            for (int i = 0; i < inv.Capacity; i++)
                if (!inv[i].IsEmpty) n += inv[i].amount;
            return n;
        }
    }
}
