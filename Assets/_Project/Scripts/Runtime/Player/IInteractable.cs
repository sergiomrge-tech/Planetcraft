using UnityEngine;

namespace Planetcraft.Player
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(GameObject interactor);
    }
}
