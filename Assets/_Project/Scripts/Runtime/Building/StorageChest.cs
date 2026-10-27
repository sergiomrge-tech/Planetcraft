using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.UI;
using UnityEngine;

namespace Planetcraft.Building
{
    // Baú construível: E abre a transferência entre o inventário do jogador e o do baú.
    [RequireComponent(typeof(InventoryComponent))]
    public class StorageChest : MonoBehaviour, IInteractable
    {
        public string Prompt => $"[E] {Loc.T("Abrir baú")}";

        public void Interact(GameObject interactor)
        {
            if (GameHud.Instance != null) GameHud.Instance.OpenChest(GetComponent<InventoryComponent>());
        }
    }
}
