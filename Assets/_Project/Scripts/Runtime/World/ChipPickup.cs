using Planetcraft.Core;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.World
{
    // Caixa com microchip num destroço: pegar libera um projeto e apaga o farol do local.
    public class ChipPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] string blueprintId;
        [SerializeField] string label;
        [Tooltip("Objetos escondidos depois de pegar o chip (ex.: farol).")]
        [SerializeField] GameObject[] hideWhenTaken;

        public string Prompt => $"[E] Pegar microchip ({label})";

        // Depois do SaveSystem restaurar os projetos (ordem -50).
        void Start()
        {
            if (!string.IsNullOrEmpty(blueprintId) && Blueprints.Has(blueprintId)) Hide();
        }

        public void Interact(GameObject interactor)
        {
            Blueprints.Unlock(blueprintId);
            Sfx.Collect();
            Hide();
        }

        void Hide()
        {
            if (hideWhenTaken != null)
                foreach (var g in hideWhenTaken)
                    if (g != null) g.SetActive(false);
            gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public void EditorSetup(string id, string chipLabel, GameObject[] hide)
        {
            blueprintId = id;
            label = chipLabel;
            hideWhenTaken = hide;
        }
#endif
    }
}
