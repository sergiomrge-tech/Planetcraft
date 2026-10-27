using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Player
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float range = 4f;

        public IInteractable Current { get; private set; }

        void Update()
        {
            Current = null;
            if (UIState.MenuOpen) return;

            var t = cam.transform;
            if (Physics.Raycast(t.position, t.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Current = hit.collider.GetComponentInParent<IInteractable>();

            if (Current != null && GameInput.Interact.WasPressedThisFrame()) Current.Interact(gameObject);
        }
    }
}
