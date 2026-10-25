using System.Linq;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Crafting;
using Planetcraft.Data;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.Power;
using Planetcraft.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Planetcraft.UI
{
    // HUD de protótipo em IMGUI: rápido de iterar. Trocar por UI Toolkit na vertical slice.
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] InventoryComponent inventory;
        [SerializeField] SurvivalStats survival;
        [SerializeField] Interactor interactor;
        [SerializeField] BuildPlacer placer;
        [SerializeField] SaveSystem saveSystem;

        string toast;
        float toastUntil;
        Vector2 scroll;
        GUIStyle rich;

        void Start()
        {
            if (survival != null) survival.Died += OnDied;
        }

        void OnDestroy()
        {
            if (survival != null) survival.Died -= OnDied;
        }

        void OnDied() => ShowToast("Você desmaiou e acordou no ponto inicial.");

        void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.time + 6f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) UIState.MenuOpen = !UIState.MenuOpen;
            if (kb.f2Key.wasPressedThisFrame && TerraformManager.Instance != null)
            {
                var tm = TerraformManager.Instance;
                tm.SimulationSpeed = tm.SimulationSpeed >= 100f ? 1f : tm.SimulationSpeed * 10f;
            }
        }

        void OnGUI()
        {
            rich ??= new GUIStyle(GUI.skin.label) { richText = true };

            DrawStatus();
            DrawCenter();
            if (UIState.MenuOpen) DrawMenu();

            GUI.Label(new Rect(10, Screen.height - 24, 900, 22),
                "[Tab] menu   [E] coletar   [F5] salvar   [F9] carregar   [F2] acelerar simulação (x1/x10/x100)");
        }

        void DrawStatus()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 330), GUI.skin.box);
            var tm = TerraformManager.Instance;
            if (tm != null)
            {
                GUILayout.Label($"<size=18><b>{TiFormat.Format(tm.Ti)}</b></size>", rich);
                GUILayout.Label($"Estágio: <b>{(tm.CurrentStage != null ? tm.CurrentStage.displayName : "-")}</b>", rich);
                var next = tm.NextStage;
                if (next != null)
                {
                    GUILayout.Label($"Próximo: {next.displayName} em {TiFormat.Format(next.requiredTi)}");
                    double from = tm.CurrentStage != null ? tm.CurrentStage.requiredTi : 0d;
                    Bar("Progresso", Mathf.Clamp01((float)((tm.Ti - from) / (next.requiredTi - from))), warnWhenLow: false);
                }
                for (int i = 0; i < TerraformParameters.Count; i++)
                {
                    var p = (TerraformParameter)i;
                    string unit = TerraformParameters.Unit(p);
                    GUILayout.Label($"{TerraformParameters.DisplayName(p)}: {TiFormat.Format(tm.Get(p), unit)}  (+{TiFormat.Format(tm.GetRate(p), unit)}/s)");
                }
                string power = PowerGrid.IsPowered ? "" : "  <color=#ff5555><b>SEM ENERGIA</b></color>";
                GUILayout.Label($"Energia: {PowerGrid.Generation:0.#} / {PowerGrid.Demand:0.#} kW{power}", rich);
                if (tm.SimulationSpeed != 1f) GUILayout.Label($"Simulação x{tm.SimulationSpeed:0}");
            }
            if (survival != null)
            {
                GUILayout.Space(6);
                Bar("O2", survival.OxygenNormalized);
                Bar("Água", survival.WaterNormalized);
                Bar("Comida", survival.FoodNormalized);
                if (survival.InPressurizedZone) GUILayout.Label("<color=#88ddff>Dentro da base</color>", rich);
            }
            GUILayout.EndArea();
        }

        void Bar(string label, float value, bool warnWhenLow = true)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value * 20f), 0, 20);
            string color = warnWhenLow && value < 0.25f ? "#ff5555" : "#ffffff";
            GUILayout.Label($"<color={color}>{label,-7} {new string('|', filled)}{new string('.', 20 - filled)} {value * 100f:0}%</color>", rich);
        }

        void DrawCenter()
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            if (!UIState.MenuOpen) GUI.Label(new Rect(cx - 5, cy - 11, 20, 22), "+");

            string prompt = interactor != null && interactor.Current != null ? interactor.Current.Prompt : null;
            if (placer != null && placer.Selected != null) prompt = $"{placer.Selected.displayName}: {placer.Status}";
            if (!string.IsNullOrEmpty(prompt)) GUI.Label(new Rect(cx - 250, cy + 30, 500, 24), prompt, Centered());

            if (toast != null && Time.time < toastUntil)
                GUI.Box(new Rect(cx - 220, 20, 440, 60), toast);

            if (saveSystem != null && saveSystem.LastMessage != null && Time.time - saveSystem.LastMessageTime < 2f)
                GUI.Label(new Rect(cx - 150, cy + 60, 300, 24), saveSystem.LastMessage, Centered());
        }

        GUIStyle Centered() => new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

        void DrawMenu()
        {
            var rect = new Rect(Screen.width / 2f - 450, Screen.height / 2f - 260, 900, 520);
            GUILayout.BeginArea(rect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            DrawInventory();
            DrawRecipes();
            DrawBuildables();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawInventory()
        {
            GUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("<b>Inventário</b>", rich);
            var inv = inventory.Inventory;
            for (int i = 0; i < inv.Capacity; i++)
            {
                var s = inv[i];
                if (s.IsEmpty) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{s.item.displayName} x{s.amount}");
                if (s.item.IsConsumable && survival != null && GUILayout.Button("Usar", GUILayout.Width(60)))
                {
                    if (inv.TryRemove(s.item)) survival.Consume(s.item);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        void DrawRecipes()
        {
            GUILayout.BeginVertical(GUILayout.Width(290));
            GUILayout.Label("<b>Fabricar</b>", rich);
            var inv = inventory.Inventory;
            double ti = TerraformManager.Instance != null ? TerraformManager.Instance.Ti : 0;
            foreach (var r in database.recipes)
            {
                if (r == null || r.output == null) continue;
                bool unlocked = ti >= r.requiredTi;
                GUILayout.Label(unlocked ? $"{r.output.displayName}: {CostText(r.ingredients)}" : $"<color=#888888>{r.output.displayName} (requer {TiFormat.Format(r.requiredTi)})</color>", rich);
                GUI.enabled = unlocked && inv.Has(r.ingredients);
                if (GUILayout.Button("Fabricar")) CraftingService.TryCraft(r, inv);
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
        }

        void DrawBuildables()
        {
            GUILayout.BeginVertical(GUILayout.Width(290));
            GUILayout.Label("<b>Construir</b>", rich);
            var inv = inventory.Inventory;
            double ti = TerraformManager.Instance != null ? TerraformManager.Instance.Ti : 0;
            foreach (var b in database.buildables)
            {
                if (b == null) continue;
                bool unlocked = ti >= b.requiredTi;
                if (!unlocked)
                {
                    GUILayout.Label($"<color=#888888>{b.displayName} (requer {TiFormat.Format(b.requiredTi)})</color>", rich);
                    continue;
                }
                GUILayout.Label($"<b>{b.displayName}</b>: {CostText(b.cost)}\n<size=11>{b.description}</size>", rich);
                GUI.enabled = inv.Has(b.cost);
                if (GUILayout.Button("Construir"))
                {
                    placer.Select(b);
                    UIState.MenuOpen = false;
                }
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
        }

        static string CostText(ItemAmount[] costs) =>
            costs == null || costs.Length == 0
                ? "grátis"
                : string.Join(", ", costs.Where(c => c.item != null).Select(c => $"{c.amount}x {c.item.displayName}"));
    }
}
