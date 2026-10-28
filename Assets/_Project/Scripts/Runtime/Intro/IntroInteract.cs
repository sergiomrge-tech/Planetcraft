using Planetcraft.Core;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.Intro
{
    // Objeto da abertura que o jogador usa com [E] (armário do traje, escotilha da nave).
    public class IntroInteract : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Usar";

        public bool Used { get; private set; }
        public string Prompt => Used ? null : $"{GameInput.Hint(GameInput.Interact)} {Loc.T(prompt)}";

        public void Interact(GameObject interactor) => Used = true;
    }
}
