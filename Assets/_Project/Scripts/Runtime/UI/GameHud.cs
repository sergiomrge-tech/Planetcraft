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
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // HUD do jogo em UI Toolkit, montado por código (sem UXML) para ficar perto da lógica que mostra.
    [RequireComponent(typeof(UIDocument))]
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        [SerializeField] GameDatabase database;
        [SerializeField] InventoryComponent inventory;
        [SerializeField] SurvivalStats survival;
        [SerializeField] Interactor interactor;
        [SerializeField] BuildPlacer placer;
        [SerializeField] Deconstructor deconstructor;
        [SerializeField] SaveSystem saveSystem;

        static readonly Color PanelColor = new(0.04f, 0.06f, 0.09f, 0.78f);
        static readonly Color Accent = new(0.55f, 0.85f, 1f);
        static readonly Color Warn = new(1f, 0.38f, 0.32f);
        static readonly Color Muted = new(0.62f, 0.66f, 0.7f);

        class Meter
        {
            public VisualElement fill;
            public Label value;
        }

        VisualElement menu, banner, flash, chestColumn, craftColumn, buildColumn;
        VisualElement invList, craftList, buildList, chestList;
        Label tiLabel, stageLabel, detailLabel, prompt, toast, bannerTitle, bannerSubtitle, bannerBody;
        Meter progress, oxygen, water, food;
        InventoryComponent openChest;
        bool menuDirty;
        float toastUntil;
        float lastSaveMessageTime = -1f;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (survival != null) survival.Died -= OnDied;
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged -= OnTiChanged;
        }

        void Start()
        {
            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            inventory.Inventory.Changed += MarkMenuDirty;
            if (survival != null) survival.Died += OnDied;
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged += OnTiChanged;
        }

        void OnDied() => ShowToast("Você desmaiou e acordou no ponto inicial.", 5f);
        void OnTiChanged(double _) => MarkMenuDirty();
        void MarkMenuDirty() => menuDirty = true;

        // ------------------------------------------------------------------ API pública

        public void ToggleMenu(bool open)
        {
            UIState.MenuOpen = open;
            menu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open && openChest != null)
            {
                openChest.Inventory.Changed -= MarkMenuDirty;
                openChest = null;
            }
            menuDirty = true;
        }

        public void OpenChest(InventoryComponent chest)
        {
            if (openChest != null) openChest.Inventory.Changed -= MarkMenuDirty;
            openChest = chest;
            openChest.Inventory.Changed += MarkMenuDirty;
            ToggleMenu(true);
        }

        public void ShowToast(string message, float seconds = 3f)
        {
            toast.text = message;
            toast.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + seconds;
        }

        public void ShowBanner(bool isStage, string title, string subtitle, string body, float alpha, float slide)
        {
            banner.style.display = DisplayStyle.Flex;
            banner.style.opacity = alpha;
            banner.style.translate = new Translate(new Length(0f), new Length(slide));
            bannerTitle.text = title;
            bannerSubtitle.text = subtitle;
            bannerSubtitle.style.fontSize = isStage ? 34 : 24;
            bannerBody.text = body ?? "";
            bannerBody.style.display = string.IsNullOrEmpty(body) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void HideBanner() => banner.style.display = DisplayStyle.None;

        public void SetFlash(float alpha) => flash.style.opacity = alpha;

        // ------------------------------------------------------------------ Atualização

        void Update()
        {
            if (menu == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame) ToggleMenu(!UIState.MenuOpen);
                else if (UIState.MenuOpen && kb.escapeKey.wasPressedThisFrame) ToggleMenu(false);
                if (kb.f2Key.wasPressedThisFrame && TerraformManager.Instance != null)
                {
                    var tm = TerraformManager.Instance;
                    tm.SimulationSpeed = tm.SimulationSpeed >= 100f ? 1f : tm.SimulationSpeed * 10f;
                }
            }

            UpdateStatus();
            UpdatePrompt();

            if (saveSystem != null && saveSystem.LastMessage != null && saveSystem.LastMessageTime != lastSaveMessageTime)
            {
                lastSaveMessageTime = saveSystem.LastMessageTime;
                ShowToast(saveSystem.LastMessage, 2f);
            }
            if (toast.style.display == DisplayStyle.Flex && Time.unscaledTime > toastUntil) toast.style.display = DisplayStyle.None;

            if (UIState.MenuOpen && menuDirty) RebuildMenu();
        }

        void UpdateStatus()
        {
            var tm = TerraformManager.Instance;
            if (tm != null)
            {
                tiLabel.text = TiFormat.Format(tm.Ti);
                stageLabel.text = $"Estágio: <b>{(tm.CurrentStage != null ? tm.CurrentStage.displayName : "-")}</b>";
                var next = tm.NextStage;
                var sb = new System.Text.StringBuilder();
                if (next != null)
                {
                    sb.AppendLine($"Próximo: {next.displayName} em {TiFormat.Format(next.requiredTi)}");
                    double from = tm.CurrentStage != null ? tm.CurrentStage.requiredTi : 0d;
                    SetMeter(progress, (float)((tm.Ti - from) / (next.requiredTi - from)), Accent);
                }
                else
                {
                    SetMeter(progress, 1f, Accent);
                }
                for (int i = 0; i < TerraformParameters.Count; i++)
                {
                    var p = (TerraformParameter)i;
                    string unit = TerraformParameters.Unit(p);
                    sb.AppendLine($"{TerraformParameters.DisplayName(p)}: {TiFormat.Format(tm.Get(p), unit)}  <color=#8fd18f>+{TiFormat.Format(tm.GetRate(p), unit)}/s</color>");
                }
                string power = PowerGrid.IsPowered ? "" : "  <color=#ff6a5a><b>SEM ENERGIA</b></color>";
                sb.Append($"Energia: {PowerGrid.Generation:0.#} / {PowerGrid.Demand:0.#} kW{power}");
                if (tm.SimulationSpeed != 1f) sb.Append($"\n<color=#ffd27f>Simulação x{tm.SimulationSpeed:0}</color>");
                detailLabel.text = sb.ToString();
            }

            if (survival != null)
            {
                SetMeter(oxygen, survival.OxygenNormalized, survival.InPressurizedZone ? Accent : SurvivalColor(survival.OxygenNormalized));
                SetMeter(water, survival.WaterNormalized, SurvivalColor(survival.WaterNormalized));
                SetMeter(food, survival.FoodNormalized, SurvivalColor(survival.FoodNormalized));
            }
        }

        static Color SurvivalColor(float v) => v < 0.25f ? Warn : new Color(0.85f, 0.9f, 0.95f);

        void UpdatePrompt()
        {
            string text = null;
            if (placer != null && placer.Selected != null)
            {
                text = $"{placer.Selected.displayName}: {placer.Status}";
            }
            else if (!UIState.MenuOpen)
            {
                string a = interactor != null && interactor.Current != null ? interactor.Current.Prompt : null;
                string b = deconstructor != null ? deconstructor.Prompt : null;
                text = string.Join("     ", new[] { a, b }.Where(s => !string.IsNullOrEmpty(s)));
            }
            prompt.text = text ?? "";
            prompt.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ------------------------------------------------------------------ Menu (Tab / baú)

        void RebuildMenu()
        {
            menuDirty = false;
            var inv = inventory.Inventory;
            bool chestMode = openChest != null;
            craftColumn.style.display = chestMode ? DisplayStyle.None : DisplayStyle.Flex;
            buildColumn.style.display = chestMode ? DisplayStyle.None : DisplayStyle.Flex;
            chestColumn.style.display = chestMode ? DisplayStyle.Flex : DisplayStyle.None;

            invList.Clear();
            FillInventory(invList, inv, slot =>
            {
                if (chestMode) return MakeButton("Guardar →", () => inv.TransferTo(openChest.Inventory, slot.item, slot.amount));
                if (slot.item.IsConsumable && survival != null)
                    return MakeButton("Usar", () => { if (inv.TryRemove(slot.item)) survival.Consume(slot.item); });
                return null;
            });

            if (chestMode)
            {
                chestList.Clear();
                FillInventory(chestList, openChest.Inventory, slot =>
                    MakeButton("← Pegar", () => openChest.Inventory.TransferTo(inv, slot.item, slot.amount)));
                return;
            }

            double ti = TerraformManager.Instance != null ? TerraformManager.Instance.Ti : 0;
            craftList.Clear();
            foreach (var r in database.recipes)
            {
                if (r == null || r.output == null) continue;
                if (ti < r.requiredTi)
                {
                    craftList.Add(MakeLabel($"{r.output.displayName}  (requer {TiFormat.Format(r.requiredTi)})", 13, Muted));
                    continue;
                }
                var card = Card(r.output.displayName, CostText(r.ingredients));
                var recipe = r;
                var button = MakeButton("Fabricar", () => CraftingService.TryCraft(recipe, inv));
                button.SetEnabled(inv.Has(r.ingredients));
                card.Add(button);
                craftList.Add(card);
            }

            buildList.Clear();
            foreach (var b in database.buildables)
            {
                if (b == null) continue;
                if (ti < b.requiredTi)
                {
                    buildList.Add(MakeLabel($"{b.displayName}  (requer {TiFormat.Format(b.requiredTi)})", 13, Muted));
                    continue;
                }
                var card = Card(b.displayName, $"{CostText(b.cost)}\n<color=#9aa4ad>{b.description}</color>");
                var def = b;
                var button = MakeButton("Construir", () =>
                {
                    placer.Select(def);
                    ToggleMenu(false);
                });
                button.SetEnabled(inv.Has(b.cost));
                card.Add(button);
                buildList.Add(card);
            }
        }

        void FillInventory(VisualElement list, Inventory inv, System.Func<Inventory.Slot, Button> action)
        {
            bool any = false;
            for (int i = 0; i < inv.Capacity; i++)
            {
                var slot = inv[i];
                if (slot.IsEmpty) continue;
                any = true;
                var row = Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 4;
                var name = MakeLabel($"{slot.item.displayName}  <b>x{slot.amount}</b>", 15);
                name.style.flexGrow = 1;
                row.Add(name);
                var button = action(slot);
                if (button != null) row.Add(button);
                list.Add(row);
            }
            if (!any) list.Add(MakeLabel("vazio", 13, Muted));
        }

        static string CostText(ItemAmount[] costs) =>
            costs == null || costs.Length == 0
                ? "grátis"
                : string.Join(", ", costs.Where(c => c.item != null).Select(c => $"{c.amount}x {c.item.displayName}"));

        // ------------------------------------------------------------------ Montagem da UI

        void BuildUi(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;

            flash = FullScreen();
            flash.style.backgroundColor = new Color(1f, 0.97f, 0.9f);
            flash.style.opacity = 0f;
            root.Add(flash);

            // Status (canto superior esquerdo)
            var status = Panel();
            status.style.position = Position.Absolute;
            status.style.left = 18;
            status.style.top = 18;
            status.style.width = 360;
            tiLabel = MakeLabel("0 Ti", 30, Color.white, bold: true);
            stageLabel = MakeLabel("", 16, Accent);
            status.Add(tiLabel);
            status.Add(stageLabel);
            progress = AddMeter(status, "Progresso");
            detailLabel = MakeLabel("", 13, new Color(0.86f, 0.88f, 0.9f));
            detailLabel.style.marginTop = 6;
            status.Add(detailLabel);
            root.Add(status);

            // Sobrevivência (canto inferior esquerdo)
            var vitals = Panel();
            vitals.style.position = Position.Absolute;
            vitals.style.left = 18;
            vitals.style.bottom = 44;
            vitals.style.width = 300;
            oxygen = AddMeter(vitals, "O₂");
            water = AddMeter(vitals, "Água");
            food = AddMeter(vitals, "Comida");
            root.Add(vitals);

            // Centro: mira e prompt
            var center = FullScreen();
            center.style.alignItems = Align.Center;
            center.style.justifyContent = Justify.Center;
            center.Add(MakeLabel("+", 22, new Color(1f, 1f, 1f, 0.85f)));
            prompt = MakeLabel("", 17, Color.white);
            prompt.style.marginTop = 40;
            prompt.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            Pad(prompt, 6, 12);
            Round(prompt, 4);
            prompt.style.position = Position.Absolute;
            prompt.style.top = new Length(56, LengthUnit.Percent);
            center.Add(prompt);
            root.Add(center);

            // Toast e banner (topo)
            var top = FullScreen();
            top.style.alignItems = Align.Center;
            toast = MakeLabel("", 16, Color.white);
            toast.style.marginTop = 18;
            toast.style.backgroundColor = PanelColor;
            Pad(toast, 8, 16);
            Round(toast, 6);
            toast.style.display = DisplayStyle.None;
            top.Add(toast);

            banner = Panel();
            banner.style.marginTop = 70;
            banner.style.width = 660;
            banner.style.alignItems = Align.Center;
            bannerTitle = MakeLabel("", 13, Accent, bold: true);
            bannerSubtitle = MakeLabel("", 34, Color.white, bold: true);
            bannerBody = MakeLabel("", 15, new Color(0.9f, 0.9f, 0.9f));
            bannerBody.style.whiteSpace = WhiteSpace.Normal;
            bannerBody.style.unityTextAlign = TextAnchor.UpperCenter;
            banner.Add(bannerTitle);
            banner.Add(bannerSubtitle);
            banner.Add(bannerBody);
            banner.style.display = DisplayStyle.None;
            top.Add(banner);
            root.Add(top);

            // Ajuda (rodapé)
            var help = MakeLabel("[Tab] menu   [E] interagir   [X] desmontar   [F5] salvar   [F9] carregar   [F2] acelerar simulação", 13, Muted);
            help.style.position = Position.Absolute;
            help.style.left = 18;
            help.style.bottom = 14;
            root.Add(help);

            // Menu
            menu = FullScreen();
            menu.pickingMode = PickingMode.Position;
            menu.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            menu.style.alignItems = Align.Center;
            menu.style.justifyContent = Justify.Center;
            var columns = Row();
            columns.style.alignItems = Align.FlexStart;
            menu.Add(columns);
            columns.Add(Column("Inventário", out invList, out _));
            craftColumn = Column("Fabricar", out craftList, out _);
            buildColumn = Column("Construir", out buildList, out _);
            chestColumn = Column("Baú", out chestList, out _);
            columns.Add(craftColumn);
            columns.Add(buildColumn);
            columns.Add(chestColumn);
            menu.style.display = DisplayStyle.None;
            root.Add(menu);
        }

        VisualElement Column(string title, out VisualElement list, out Label titleLabel)
        {
            var col = Panel();
            col.style.width = 330;
            col.style.height = 560;
            col.style.marginLeft = col.style.marginRight = 6;
            titleLabel = MakeLabel(title, 20, Accent, bold: true);
            titleLabel.style.marginBottom = 8;
            col.Add(titleLabel);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            col.Add(scroll);
            list = scroll.contentContainer;
            return col;
        }

        static VisualElement Card(string title, string body)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            Pad(card, 8, 10);
            Round(card, 5);
            card.style.marginBottom = 6;
            card.Add(MakeLabel(title, 16, Color.white, bold: true));
            var b = MakeLabel(body, 13, new Color(0.85f, 0.87f, 0.9f));
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.marginBottom = 4;
            card.Add(b);
            return card;
        }

        Meter AddMeter(VisualElement parent, string label)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 5;
            var name = MakeLabel(label, 14, new Color(0.86f, 0.88f, 0.9f));
            name.style.width = 82;
            row.Add(name);
            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 10;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            Round(track, 5);
            var fill = new VisualElement();
            fill.style.height = new Length(100, LengthUnit.Percent);
            Round(fill, 5);
            track.Add(fill);
            row.Add(track);
            var value = MakeLabel("", 13, Muted);
            value.style.width = 44;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(value);
            parent.Add(row);
            return new Meter { fill = fill, value = value };
        }

        static void SetMeter(Meter m, float v, Color color)
        {
            v = Mathf.Clamp01(v);
            m.fill.style.width = new Length(v * 100f, LengthUnit.Percent);
            m.fill.style.backgroundColor = color;
            m.value.text = $"{v * 100f:0}%";
        }

        static VisualElement FullScreen()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = v.style.right = v.style.top = v.style.bottom = 0;
            return v;
        }

        static VisualElement Panel()
        {
            var v = new VisualElement();
            v.style.backgroundColor = PanelColor;
            Pad(v, 12, 14);
            Round(v, 8);
            return v;
        }

        static VisualElement Row()
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Row;
            return v;
        }

        static Label MakeLabel(string text, int size, Color? color = null, bool bold = false)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            l.style.fontSize = size;
            l.style.color = color ?? Color.white;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingLeft = l.style.paddingRight = 0;
            return l;
        }

        static Button MakeButton(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = 13;
            b.style.minWidth = 86;
            return b;
        }

        static void Pad(VisualElement v, float vertical, float horizontal)
        {
            v.style.paddingTop = v.style.paddingBottom = vertical;
            v.style.paddingLeft = v.style.paddingRight = horizontal;
        }

        static void Round(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = v.style.borderTopRightRadius = r;
            v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = r;
        }
    }
}
