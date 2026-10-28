using Planetcraft.Combat;
using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.Building
{
    public enum PieceKind { None, Foundation, Floor, Wall, Pillar, Stairs, Furniture, Defense }

    // Peça do kit de base: tem vida (inimigos podem destruí-la), entra na grade para vedar salas e pode ser reparada.
    // O gerador de O2 (IsAirSource) pressuriza a sala selada em que está enquanto estiver ligado.
    [RequireComponent(typeof(PlacedObject))]
    public class BasePiece : MonoBehaviour, IInteractable
    {
        [SerializeField] PieceKind kind;
        [SerializeField] float maxHealth = 300f;
        [SerializeField, Range(0f, 0.9f)] float armor;
        [SerializeField] bool isAirSource;

        Health health;
        Machine machine;

        public PieceKind Kind => kind;
        public bool IsAirSource => isAirSource;
        public bool AirSourceRunning => isAirSource && (machine == null || machine.IsRunning);
        public Health Health => health;

        void Awake()
        {
            machine = GetComponent<Machine>();
            health = gameObject.AddComponent<Health>();
            health.Max = maxHealth;
            health.Restore();
            health.Team = Team.Base;
            health.Died += OnDied;
        }

        void OnEnable() => BaseGrid.Register(this);
        void OnDisable() => BaseGrid.Unregister(this);

        void OnDied(Health h)
        {
            Sfx.BoomAt(transform.position);
            CombatFx.Explosion(transform.position + Vector3.up * 0.8f, kind == PieceKind.Wall ? 1.2f : 0.8f);
            // Ao quebrar, o jogador perde a peça (sem reembolso); se a sala perdia ar, o alerta sai pelo BaseGrid.
            if (GameHud())
            {
                GameHudRef().ShowToast(Loc.T("Uma peça da base foi destruída!"), 3f);
            }
        }

        static bool GameHud() => Planetcraft.UI.GameHud.Instance != null;
        static Planetcraft.UI.GameHud GameHudRef() => Planetcraft.UI.GameHud.Instance;

        // Peça danificada: [E] repara gastando 1 de ferro.
        public string Prompt =>
            health != null && health.Normalized < 0.98f
                ? $"{GameInput.Hint(GameInput.Interact)} {Loc.T("Reparar")} ({Mathf.RoundToInt(health.Normalized * 100f)}%)  ·  1 {Loc.T("Ferro")}"
                : null;

        public void Interact(GameObject interactor)
        {
            if (health == null || health.Normalized >= 0.98f) return;
            var inv = interactor.GetComponentInParent<InventoryComponent>() ?? interactor.GetComponent<InventoryComponent>();
            if (inv == null) return;
            var iron = FindIron(inv);
            if (iron == null || !inv.Inventory.TryRemove(iron, 1)) return;
            health.Heal(health.Max * 0.4f);
            Sfx.Craft();
        }

        static ItemDefinition FindIron(InventoryComponent inv)
        {
            for (int i = 0; i < inv.Inventory.Capacity; i++)
                if (!inv.Inventory[i].IsEmpty && inv.Inventory[i].item.id == "iron") return inv.Inventory[i].item;
            return null;
        }
    }
}
