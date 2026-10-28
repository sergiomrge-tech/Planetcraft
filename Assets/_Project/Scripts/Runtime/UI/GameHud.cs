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
using static Planetcraft.UI.HudStyle;

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

        class Meter
        {
            public Segments bar;
            public Label value;
        }

        VisualElement menu, banner, flash, chestColumn, craftColumn, buildColumn, logsColumn, logPanel;
        VisualElement pausePanel, pauseButtons, pauseOptions, shipColumn, shipList;
        Shipyard openShipyard;
        VisualElement invList, craftList, buildList, chestList, logsList;
        Label logTitle, logBody;
        int logOpenedFrame = -1;
        Label tiLabel, stageLabel, nextLabel, powerLabel, prompt, toast, bannerTitle, bannerSubtitle, bannerBody;
        Label[] paramValues, paramRates;
        Label help;
        bool helpGamepad;
        bool helpDirty = true;
        VisualElement statusPanel, vitalsPanel, objectivePanel, hintPanel;
        Label hintLabel;
        float hintUntil;
        Label objectiveLabel;
        float nextObjectiveRefresh;
        Meter progress, oxygen, water, food;
        InventoryComponent openChest;
        bool menuDirty;
        float toastUntil;
        float lastSaveMessageTime = -1f;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Blueprints.Unlocked -= OnBlueprintUnlocked;
            GameInput.BindingsChanged -= MarkHelpDirty;
            if (survival != null) survival.Died -= OnDied;
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged -= OnTiChanged;
        }

        void Start()
        {
            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            if (database != null) StoryLogs.Total = database.logs.Count;
            inventory.Inventory.Changed += MarkMenuDirty;
            if (survival != null) survival.Died += OnDied;
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged += OnTiChanged;
            Blueprints.Unlocked += OnBlueprintUnlocked;
            GameInput.BindingsChanged += MarkHelpDirty;
        }

        void MarkHelpDirty() => helpDirty = true;

        // Rodapé com as teclas atuais; troca para os botões do controle quando ele é o último dispositivo usado.
        void UpdateHelp()
        {
            if (!helpDirty && helpGamepad == GameInput.UsingGamepad) return;
            helpDirty = false;
            helpGamepad = GameInput.UsingGamepad;
            string Item(InputAction a, string verb) => $"{GameInput.Hint(a)} {Loc.T(verb)}";
            help.text = KeyCaps(string.Join("   ·   ", Item(GameInput.Menu, "menu"), Item(GameInput.Interact, "interagir"),
                Item(GameInput.Deconstruct, "desmontar"), Item(GameInput.QuickSave, "salvar"),
                Item(GameInput.QuickLoad, "carregar"), Item(GameInput.SimulationSpeed, "acelerar")));
        }

        // Os prompts dos objetos são escritos com as teclas padrão ("[E] ..."); troca pelas teclas atuais.
        static string WithHints(string text) =>
            text.Replace("[E]", GameInput.Hint(GameInput.Interact)).Replace("[X]", GameInput.Hint(GameInput.Deconstruct));

        void OnBlueprintUnlocked(string _) => MarkMenuDirty();

        void OnDied() => ShowToast(Loc.T("Você desmaiou e acordou no ponto inicial."), 5f);
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
            if (!open && openShipyard != null)
            {
                ShipProgress.Changed -= MarkMenuDirty;
                openShipyard = null;
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

        public void OpenShipyard(Shipyard shipyard)
        {
            openShipyard = shipyard;
            ShipProgress.Changed += MarkMenuDirty;
            ToggleMenu(true);
        }

        public void ShowLog(StoryLog log)
        {
            if (log == null) return;
            if (UIState.MenuOpen) ToggleMenu(false);
            logTitle.text = Loc.T(log.title);
            logBody.text = Loc.T(log.body);
            logPanel.style.display = DisplayStyle.Flex;
            UIState.MenuOpen = true;
            logOpenedFrame = Time.frameCount;
        }

        bool LogOpen => logPanel != null && logPanel.style.display == DisplayStyle.Flex;

        void CloseLog()
        {
            logPanel.style.display = DisplayStyle.None;
            UIState.MenuOpen = false;
        }

        bool PauseOpen => pausePanel != null && pausePanel.style.display == DisplayStyle.Flex;

        public void SetPaused(bool paused)
        {
            pausePanel.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            ShowPauseOptions(false);
            Time.timeScale = paused ? 0f : 1f;
            if (paused) pauseButtons.Q<Button>()?.Focus();
            UIState.MenuOpen = paused;
        }

        void ShowPauseOptions(bool show)
        {
            pauseButtons.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            pauseOptions.Clear();
            if (show) pauseOptions.Add(OptionsUi.Build(() => ShowPauseOptions(false)));
            pauseOptions.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Dica de primeira vez (TutorialHints): painel no rodapé, acima da ajuda de teclas.
        public void ShowHint(string text, float seconds)
        {
            hintLabel.text = KeyCaps(WithHints(text));
            hintPanel.style.display = DisplayStyle.Flex;
            hintUntil = Time.unscaledTime + seconds;
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
            bool pause = GameInput.Pause.WasPressedThisFrame();
            if (PauseOpen)
            {
                if ((pause || GameInput.Cancel.WasPressedThisFrame()) && pauseButtons.style.display == DisplayStyle.Flex)
                    SetPaused(false);
            }
            else if (LogOpen)
            {
                // O mesmo botão que abriu o registro não pode fechá-lo no mesmo frame.
                if (Time.frameCount > logOpenedFrame && (GameInput.Interact.WasPressedThisFrame() || pause ||
                                                         GameInput.Menu.WasPressedThisFrame() || GameInput.Cancel.WasPressedThisFrame()))
                    CloseLog();
            }
            else
            {
                if (GameInput.Menu.WasPressedThisFrame()) ToggleMenu(!UIState.MenuOpen);
                else if (UIState.MenuOpen && (pause || GameInput.Cancel.WasPressedThisFrame())) ToggleMenu(false);
                else if (!UIState.MenuOpen && pause &&
                         (placer == null || (placer.Selected == null && placer.LastCancelFrame != Time.frameCount)))
                    SetPaused(true);
                if (GameInput.SimulationSpeed.WasPressedThisFrame() && TerraformManager.Instance != null)
                {
                    var tm = TerraformManager.Instance;
                    tm.SimulationSpeed = tm.SimulationSpeed >= 100f ? 1f : tm.SimulationSpeed * 10f;
                }
            }

            UpdateStatus();
            UpdatePrompt();
            UpdateHelp();

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
            // Com menu, pausa ou registro abertos, o visor recolhe os mostradores para não sobrepor os painéis.
            var hudDisplay = UIState.MenuOpen ? DisplayStyle.None : DisplayStyle.Flex;
            statusPanel.style.display = vitalsPanel.style.display = objectivePanel.style.display = hudDisplay;
            if (hintPanel.style.display == DisplayStyle.Flex && (UIState.MenuOpen || Time.unscaledTime > hintUntil))
                hintPanel.style.display = DisplayStyle.None;
            if (Time.unscaledTime >= nextObjectiveRefresh)
            {
                nextObjectiveRefresh = Time.unscaledTime + 0.5f;
                objectiveLabel.text = KeyCaps(WithHints(Objectives.Current(inventory != null ? inventory.Inventory : null)));
            }
            var tm = TerraformManager.Instance;
            if (tm != null)
            {
                tiLabel.text = TiFormat.Format(tm.Ti);
                stageLabel.text = Caps(tm.CurrentStage != null ? Loc.T(tm.CurrentStage.displayName) : "-");
                var next = tm.NextStage;
                if (next != null)
                {
                    nextLabel.text = Caps($"{Loc.T("Próximo")}  ·  {Loc.T(next.displayName)}  ·  {TiFormat.Format(next.requiredTi)}");
                    double from = tm.CurrentStage != null ? tm.CurrentStage.requiredTi : 0d;
                    SetMeter(progress, (float)((tm.Ti - from) / (next.requiredTi - from)), Accent);
                }
                else
                {
                    nextLabel.text = Caps(Loc.T("Todos os estágios alcançados"));
                    SetMeter(progress, 1f, Accent);
                }
                for (int i = 0; i < TerraformParameters.Count; i++)
                {
                    var p = (TerraformParameter)i;
                    string unit = TerraformParameters.Unit(p);
                    paramValues[i].text = TiFormat.Format(tm.Get(p), unit);
                    paramRates[i].text = $"+{TiFormat.Format(tm.GetRate(p), unit)}/s";
                }
                string power = $"{PowerGrid.Generation:0.#} / {PowerGrid.Demand:0.#} kW";
                if (!PowerGrid.IsPowered) power = $"<color=#FF5C3D>{Loc.T("SEM ENERGIA")}</color>  " + power;
                if (tm.SimulationSpeed != 1f) power = $"<color=#FFB848>SIM x{tm.SimulationSpeed:0}</color>  " + power;
                powerLabel.text = power;
            }

            if (survival != null)
            {
                SetMeter(oxygen, survival.OxygenNormalized, survival.InPressurizedZone ? Signal : SurvivalColor(survival.OxygenNormalized));
                SetMeter(water, survival.WaterNormalized, SurvivalColor(survival.WaterNormalized));
                SetMeter(food, survival.FoodNormalized, SurvivalColor(survival.FoodNormalized));
            }
        }

        static Color SurvivalColor(float v) => v < 0.25f ? Warn : Ink;

        void UpdatePrompt()
        {
            string text = null;
            if (placer != null && placer.Selected != null)
            {
                text = $"{Loc.T(placer.Selected.displayName)}: {placer.Status}";
            }
            else if (!UIState.MenuOpen)
            {
                string a = interactor != null && interactor.Current != null ? interactor.Current.Prompt : null;
                string b = deconstructor != null ? deconstructor.Prompt : null;
                text = string.Join("     ", new[] { a, b }.Where(s => !string.IsNullOrEmpty(s)));
            }
            prompt.text = KeyCaps(WithHints(text ?? ""));
            prompt.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ------------------------------------------------------------------ Menu (Tab / baú)

        void RebuildMenu()
        {
            menuDirty = false;
            var inv = inventory.Inventory;
            bool chestMode = openChest != null;
            bool shipMode = openShipyard != null;
            bool normal = !chestMode && !shipMode;
            craftColumn.style.display = normal ? DisplayStyle.Flex : DisplayStyle.None;
            buildColumn.style.display = normal ? DisplayStyle.Flex : DisplayStyle.None;
            logsColumn.style.display = normal ? DisplayStyle.Flex : DisplayStyle.None;
            chestColumn.style.display = chestMode ? DisplayStyle.Flex : DisplayStyle.None;
            shipColumn.style.display = shipMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (shipMode) RebuildShipyard(inv);

            invList.Clear();
            FillInventory(invList, inv, slot =>
            {
                if (chestMode) return MakeButton("Guardar →", () => inv.TransferTo(openChest.Inventory, slot.item, slot.amount));
                if (slot.item.IsConsumable && survival != null)
                    return MakeButton("Usar", () => { if (inv.TryRemove(slot.item)) survival.Consume(slot.item); });
                return null;
            });

            if (shipMode) return;
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
                if (!r.IsUnlocked(ti))
                {
                    craftList.Add(MakeLabel($"{Loc.T(r.output.displayName)}  ({LockReason(r.requiredTi, r.blueprintId, ti)})", 13, Muted));
                    continue;
                }
                var card = Card(Loc.T(r.output.displayName), CostText(r.ingredients));
                var recipe = r;
                var button = MakeButton("Fabricar", () => { if (CraftingService.TryCraft(recipe, inv)) Sfx.Craft(); });
                button.SetEnabled(inv.Has(r.ingredients));
                card.Add(button);
                craftList.Add(card);
            }

            buildList.Clear();
            foreach (var b in database.buildables)
            {
                if (b == null) continue;
                if (!b.IsUnlocked(ti))
                {
                    buildList.Add(MakeLabel($"{Loc.T(b.displayName)}  ({LockReason(b.requiredTi, b.blueprintId, ti)})", 13, Muted));
                    continue;
                }
                var card = Card(Loc.T(b.displayName), $"{CostText(b.cost)}\n<color=#9aa4ad>{WithHints(Loc.T(b.description))}</color>");
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

            logsList.Clear();
            bool anyLog = false;
            foreach (var log in database.logs)
            {
                if (log == null || !StoryLogs.IsRead(log.id)) continue;
                anyLog = true;
                var row = Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 4;
                var name = MakeLabel(log.title, 14);
                name.style.flexGrow = 1;
                name.style.whiteSpace = WhiteSpace.Normal;
                row.Add(name);
                var entry = log;
                row.Add(MakeButton("Ler", () => ShowLog(entry)));
                logsList.Add(row);
            }
            if (!anyLog)
            {
                var none = MakeLabel("Nenhum registro encontrado.\nProcure ruínas: os faróis marcam onde estão.", 13, Muted);
                none.style.whiteSpace = WhiteSpace.Normal;
                logsList.Add(none);
            }
        }

        void RebuildShipyard(Inventory inv)
        {
            shipList.Clear();
            double ti = TerraformManager.Instance != null ? TerraformManager.Instance.Ti : 0;
            int total = openShipyard.Modules.Length;
            var progressLabel = MakeLabel($"{Loc.T("Módulos instalados")}: <b>{ShipProgress.Count} {Loc.T("de")} {total}</b>", 15, Accent);
            progressLabel.style.marginBottom = 8;
            shipList.Add(progressLabel);

            foreach (var m in openShipyard.Modules)
            {
                if (m == null) continue;
                bool done = ShipProgress.IsInstalled(m.id);
                var card = Card(done ? $"{Loc.T(m.displayName)}  <color=#8fd18f>({Loc.T("instalado")})</color>" : Loc.T(m.displayName),
                    $"{Loc.T(m.description)}\n<color=#c9d1d9>{CostText(m.cost)}</color>");
                string reason = openShipyard.BlockReason(m, inv, ti);
                if (!done)
                {
                    var module = m;
                    var button = MakeButton("Instalar", () => openShipyard.TryInstall(module, inv, ti));
                    button.SetEnabled(reason == null);
                    card.Add(button);
                    if (reason != null) card.Add(MakeLabel(reason, 12, Warn));
                }
                shipList.Add(card);
            }

            string launchBlock = openShipyard.LaunchBlockReason(ti);
            var launch = BigButton("DECOLAR", () =>
            {
                var yard = openShipyard;
                ToggleMenu(false);
                yard.Launch();
            });
            launch.style.marginTop = 10;
            launch.style.backgroundColor = launchBlock == null ? new Color(1f, 0.72f, 0.28f, 0.35f) : new Color(1f, 1f, 1f, 0.04f);
            launch.SetEnabled(launchBlock == null);
            shipList.Add(launch);
            if (launchBlock != null) shipList.Add(MakeLabel(launchBlock, 13, Muted));
        }

        static string LockReason(double requiredTi, string blueprintId, double ti) =>
            !Blueprints.Has(blueprintId) ? Loc.T("requer microchip das ruínas") : $"{Loc.T("requer")} {TiFormat.Format(requiredTi)}";

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
                var name = MakeLabel($"{Loc.T(slot.item.displayName)}  <b>x{slot.amount}</b>", 15);
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
                ? Loc.T("grátis")
                : string.Join(", ", costs.Where(c => c.item != null).Select(c => $"{c.amount}x {Loc.T(c.item.displayName)}"));

        // ------------------------------------------------------------------ Montagem da UI

        void BuildUi(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.AddToClassList(RootClass);

            flash = FullScreen();
            flash.style.backgroundColor = new Color(1f, 0.97f, 0.9f);
            flash.style.opacity = 0f;
            root.Add(flash);

            // Índice de terraformação (canto superior esquerdo): número grande, estágio e tabela de parâmetros.
            var status = statusPanel = Panel(true, -1.2f);
            status.style.position = Position.Absolute;
            status.style.left = 22;
            status.style.top = 22;
            status.style.width = 392;
            status.Add(Tag("Índice de terraformação"));
            tiLabel = MakeLabel("0 Ti", 40, Accent, mono: true);
            status.Add(tiLabel);
            var stageRow = Row();
            stageRow.style.alignItems = Align.Center;
            stageRow.Add(Tag("Estágio"));
            stageLabel = MakeLabel("", 19, Ink, bold: true);
            stageLabel.style.marginLeft = 8;
            stageRow.Add(stageLabel);
            status.Add(stageRow);
            progress = AddMeter(status, null, 30, 5);
            nextLabel = Tag("");
            nextLabel.style.marginTop = 3;
            status.Add(nextLabel);

            var table = new VisualElement();
            table.style.marginTop = 10;
            table.style.paddingTop = 6;
            table.style.borderTopWidth = 1;
            table.style.borderTopColor = Hairline;
            paramValues = new Label[TerraformParameters.Count];
            paramRates = new Label[TerraformParameters.Count];
            for (int i = 0; i < TerraformParameters.Count; i++)
            {
                var row = TableRow(table, TerraformParameters.DisplayName((TerraformParameter)i));
                paramValues[i] = Cell(row, 112, 15, Ink);
                paramRates[i] = Cell(row, 96, 12, Signal);
            }
            powerLabel = Cell(TableRow(table, "Energia"), 208, 14, Ink);
            status.Add(table);
            root.Add(status);

            // Dica contextual (rodapé central).
            var hintHost = FullScreen();
            hintHost.style.alignItems = Align.Center;
            hintHost.style.justifyContent = Justify.FlexEnd;
            hintHost.style.paddingBottom = 70;
            hintPanel = Panel(true, 0.6f);
            hintPanel.style.maxWidth = 640;
            hintPanel.Add(Tag("Dica", Signal));
            hintLabel = MakeLabel("", 17, Ink);
            hintLabel.style.whiteSpace = WhiteSpace.Normal;
            hintLabel.style.marginTop = 2;
            hintPanel.Add(hintLabel);
            hintPanel.style.display = DisplayStyle.None;
            hintHost.Add(hintPanel);
            root.Add(hintHost);

            // Objetivo atual (canto superior direito).
            objectivePanel = Panel(true, 1.1f);
            objectivePanel.style.position = Position.Absolute;
            objectivePanel.style.right = 22;
            objectivePanel.style.top = 22;
            objectivePanel.style.width = 380;
            objectivePanel.Add(Tag("Objetivo", Signal));
            objectiveLabel = MakeLabel("", 16, Ink);
            objectiveLabel.style.whiteSpace = WhiteSpace.Normal;
            objectiveLabel.style.marginTop = 3;
            objectivePanel.Add(objectiveLabel);
            root.Add(objectivePanel);

            // Suporte de vida (canto inferior esquerdo).
            var vitals = vitalsPanel = Panel(true, -0.8f);
            vitals.style.position = Position.Absolute;
            vitals.style.left = 22;
            vitals.style.bottom = 50;
            vitals.style.width = 310;
            vitals.Add(Tag("Suporte de vida"));
            oxygen = AddMeter(vitals, "O2", 14, 9);
            water = AddMeter(vitals, "H2O", 14, 9);
            food = AddMeter(vitals, "NUTR", 14, 9);
            root.Add(vitals);

            // Centro: retícula do visor e prompt de interação.
            var center = FullScreen();
            center.style.alignItems = Align.Center;
            center.style.justifyContent = Justify.Center;
            center.Add(Reticle());
            prompt = MakeLabel("", 17, Ink);
            prompt.style.backgroundColor = PanelColor;
            Border(prompt, 1f, Hairline);
            prompt.style.borderLeftWidth = 2;
            prompt.style.borderLeftColor = Accent;
            Pad(prompt, 5, 12);
            prompt.style.position = Position.Absolute;
            prompt.style.top = new Length(57, LengthUnit.Percent);
            center.Add(prompt);
            root.Add(center);

            // Toast e banner (topo).
            var top = FullScreen();
            top.style.alignItems = Align.Center;
            toast = MakeLabel("", 16, Ink);
            toast.style.marginTop = 20;
            toast.style.backgroundColor = PanelColor;
            toast.style.borderLeftWidth = 2;
            toast.style.borderLeftColor = Signal;
            Pad(toast, 7, 16);
            toast.style.display = DisplayStyle.None;
            top.Add(toast);

            banner = Panel();
            banner.style.marginTop = 72;
            banner.style.width = 660;
            banner.style.alignItems = Align.Center;
            banner.style.paddingTop = banner.style.paddingBottom = 16;
            bannerTitle = Tag("", Accent);
            bannerSubtitle = MakeLabel("", 38, Ink, bold: true);
            bannerBody = MakeLabel("", 16, Body);
            bannerBody.style.whiteSpace = WhiteSpace.Normal;
            bannerBody.style.unityTextAlign = TextAnchor.UpperCenter;
            banner.Add(bannerTitle);
            banner.Add(bannerSubtitle);
            banner.Add(bannerBody);
            banner.style.display = DisplayStyle.None;
            top.Add(banner);
            root.Add(top);

            // Ajuda (rodapé), com as teclas desenhadas como teclas.
            help = MakeLabel("", 14, Muted);
            help.style.position = Position.Absolute;
            help.style.left = 22;
            help.style.bottom = 16;
            if (Paper)
            {
                help.style.backgroundColor = PanelColor;
                help.style.color = Muted;
                Pad(help, 3, 10);
                help.style.bottom = 12;
                help.style.rotate = new Rotate(new Angle(-0.6f, AngleUnit.Degree));
            }
            root.Add(help);

            // Menu
            menu = FullScreen();
            menu.pickingMode = PickingMode.Position;
            menu.style.backgroundColor = new Color(0.01f, 0.015f, 0.02f, 0.62f);
            menu.style.alignItems = Align.Center;
            menu.style.justifyContent = Justify.Center;
            var columns = Row();
            columns.style.alignItems = Align.FlexStart;
            menu.Add(columns);
            columns.Add(Column("Inventário", out invList, out _));
            craftColumn = Column("Fabricar", out craftList, out _);
            buildColumn = Column("Construir", out buildList, out _);
            chestColumn = Column("Baú", out chestList, out _);
            logsColumn = Column("Registros", out logsList, out _);
            logsColumn.style.width = 290;
            shipColumn = Column("Nave-mãe · Estaleiro", out shipList, out _);
            shipColumn.style.width = 470;
            columns.Add(shipColumn);
            columns.Add(craftColumn);
            columns.Add(buildColumn);
            columns.Add(chestColumn);
            columns.Add(logsColumn);
            menu.style.display = DisplayStyle.None;
            root.Add(menu);

            // Leitor de registros
            logPanel = FullScreen();
            logPanel.pickingMode = PickingMode.Position;
            logPanel.style.backgroundColor = new Color(0.01f, 0.015f, 0.02f, 0.7f);
            logPanel.style.alignItems = Align.Center;
            logPanel.style.justifyContent = Justify.Center;
            var sheet = Panel();
            sheet.style.width = 780;
            sheet.style.maxHeight = new Length(80, LengthUnit.Percent);
            Pad(sheet, 20, 26);
            sheet.Add(Tag("Registro recuperado", Signal));
            logTitle = MakeLabel("", 28, Ink, bold: true);
            logTitle.style.marginBottom = 12;
            sheet.Add(logTitle);
            var bodyScroll = new ScrollView(ScrollViewMode.Vertical);
            bodyScroll.style.flexShrink = 1;
            logBody = MakeLabel("", 18, Body);
            logBody.style.whiteSpace = WhiteSpace.Normal;
            bodyScroll.Add(logBody);
            sheet.Add(bodyScroll);
            var close = MakeButton("Fechar  [E / Esc]", CloseLog);
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 14;
            sheet.Add(close);
            logPanel.Add(sheet);
            logPanel.style.display = DisplayStyle.None;
            root.Add(logPanel);

            // Pausa (Esc)
            pausePanel = FullScreen();
            pausePanel.pickingMode = PickingMode.Position;
            pausePanel.style.backgroundColor = new Color(0.01f, 0.015f, 0.02f, 0.72f);
            pausePanel.style.alignItems = Align.Center;
            pausePanel.style.justifyContent = Justify.Center;
            pauseButtons = Panel();
            pauseButtons.style.width = 360;
            pauseButtons.style.alignItems = Align.Stretch;
            Pad(pauseButtons, 18, 20);
            pauseButtons.Add(Tag("Sistema"));
            var pauseTitle = MakeLabel(Paper ? "Pausado" : "PAUSADO", 34, Ink, bold: true);
            pauseTitle.style.marginBottom = 12;
            pauseButtons.Add(pauseTitle);
            pauseButtons.Add(BigButton("Continuar", () => SetPaused(false)));
            pauseButtons.Add(BigButton("Salvar jogo", () =>
            {
                if (saveSystem != null) saveSystem.Save();
                SetPaused(false);
            }));
            pauseButtons.Add(BigButton("Opções", () => ShowPauseOptions(true)));
            pauseButtons.Add(BigButton("Menu principal", () => Planetcraft.Save.GameSession.ToTitle()));
            pausePanel.Add(pauseButtons);
            pauseOptions = new VisualElement();
            pauseOptions.style.display = DisplayStyle.None;
            pausePanel.Add(pauseOptions);
            pausePanel.style.display = DisplayStyle.None;
            root.Add(pausePanel);
        }

        static VisualElement TableRow(VisualElement table, string name)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            row.style.height = 21;
            var label = MakeLabel(name, 15, Body);
            label.style.flexGrow = 1;
            row.Add(label);
            table.Add(row);
            return row;
        }

        static Label Cell(VisualElement row, float width, int size, Color color)
        {
            var l = MakeLabel("", size, color, mono: true);
            l.style.width = width;
            l.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(l);
            return l;
        }

        // Retícula: ponto central e quatro traços, como a marcação de um visor.
        static VisualElement Reticle()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.width = r.style.height = 24;
            void Mark(float x, float y, float w, float h, float alpha)
            {
                var m = new VisualElement { pickingMode = PickingMode.Ignore };
                m.style.position = Position.Absolute;
                m.style.left = x;
                m.style.top = y;
                m.style.width = w;
                m.style.height = h;
                m.style.backgroundColor = new Color(1f, 1f, 1f, alpha);
                r.Add(m);
            }
            Mark(11, 11, 2, 2, 0.95f);
            Mark(11, 0, 2, 6, 0.6f);
            Mark(11, 18, 2, 6, 0.6f);
            Mark(0, 11, 6, 2, 0.6f);
            Mark(18, 11, 6, 2, 0.6f);
            return r;
        }

        VisualElement Column(string title, out VisualElement list, out Label titleLabel)
        {
            var col = Panel();
            col.style.width = 340;
            col.style.height = 580;
            col.style.marginLeft = col.style.marginRight = 7;
            titleLabel = MakeLabel(Caps(Loc.T(title)), Paper ? 24 : 20, Accent, bold: true);
            titleLabel.style.letterSpacing = Paper ? 0 : 3;
            titleLabel.style.paddingBottom = 6;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.borderBottomWidth = 1;
            titleLabel.style.borderBottomColor = Hairline;
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
            card.style.backgroundColor = CardColor;
            card.style.borderLeftWidth = 2;
            card.style.borderLeftColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
            Pad(card, 7, 10);
            card.style.marginBottom = 6;
            card.Add(MakeLabel(title, 17, Ink, bold: true));
            var b = MakeLabel(body, 14, Body);
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.marginBottom = 4;
            card.Add(b);
            return card;
        }

        Meter AddMeter(VisualElement parent, string label, int segments, float height)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6;
            if (label != null)
            {
                var name = MakeLabel(label, 13, Muted, mono: true);
                name.style.width = 46;
                row.Add(name);
            }
            var bar = new Segments(segments, height);
            row.Add(bar.Root);
            var value = MakeLabel("", 13, Ink, mono: true);
            value.style.width = 38;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(value);
            parent.Add(row);
            return new Meter { bar = bar, value = value };
        }

        static void SetMeter(Meter m, float v, Color color)
        {
            v = Mathf.Clamp01(v);
            m.bar.Set(v, color);
            m.value.text = $"{v * 100f:0}";
        }
    }
}
