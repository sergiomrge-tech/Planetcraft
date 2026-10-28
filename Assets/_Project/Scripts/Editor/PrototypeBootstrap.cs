using System;
using System.Collections.Generic;
using System.IO;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Crafting;
using Planetcraft.Data;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.Save;
using Planetcraft.UI;
using Planetcraft.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Planetcraft.EditorTools
{
    // Gera dados de exemplo, prefabs e a cena de protótipo.
    // Pode rodar de novo: assets já existentes são mantidos (preserva ajustes de balanceamento);
    // só a cena é recriada.
    public static class PrototypeBootstrap
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";
        const string MatDir = Root + "/Materials";
        const string TerrainDir = Root + "/Terrain";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Prototype.unity";
        const string TitleScenePath = SceneDir + "/Title.unity";
        const string EpilogueScenePath = SceneDir + "/Epilogue.unity";
        const string StationScenePath = SceneDir + "/Station.unity";
        const string NasaDir = "Assets/ThirdParty/NASA";

        const float TerrainSize = 500f;
        const float TerrainHeight = 60f;
        const int HeightmapRes = 513;
        const string PolyHavenDir = "Assets/ThirdParty/PolyHaven";

        [MenuItem("Planetcraft/Gerar Protótipo")]
        public static void Build()
        {
            HdrpSetup.Configure();
            PolyHavenMaterials.Setup();
            EnsureFxMaterial();
            foreach (var dir in new[] { DataDir + "/Stages", DataDir + "/Items", DataDir + "/Recipes", DataDir + "/Buildables", DataDir + "/Logs", PrefabDir, MatDir, TerrainDir, SceneDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var mats = new MaterialLibrary();
            var stages = CreateStages(out var stageDb);
            var items = CreateItems();
            var recipes = CreateRecipes(items);
            var buildables = CreateBuildables(items, mats);
            EnsureSentinelPrefab();
            ConfigureShipyardPrefab(buildables.Find(b => b.id == "shipyard"), CreateShipModules(items), stages["genesis"]);

            var db = Asset<GameDatabase>(DataDir + "/GameDatabase.asset", d => { });
            db.items = new List<ItemDefinition>(items.Values);
            db.recipes = recipes;
            db.buildables = buildables;
            db.logs = CreateLogs();
            EditorUtility.SetDirty(db);

            BuildScene(stageDb, stages, db, items, mats);
            CopyCredits();
            // Ícone do executável (provisório: quadro do epílogo, tools/ -> Assets/_Project/UI/icon.png).
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/UI/icon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            BuildTitleScene();
            BuildEpilogueScene();
            BuildStationScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(StationScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(EpilogueScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Planetcraft] Protótipo gerado: " + ScenePath);
        }

        // ---------- Dados ----------

        static Dictionary<string, TerraformStage> CreateStages(out StageDatabase db)
        {
            var data = new (string id, string name, double ti, string desc)[]
            {
                ("barren", "Terra Morta", 0, "A Última Guerra queimou o ar e contaminou o solo."),
                ("haze", "Névoa", 100, "Os primeiros gases engrossam o ar."),
                ("bluesky", "Céu Azul", 1_000, "A luz começa a se espalhar no céu."),
                ("clouds", "Nuvens", 5_000, "Vapor condensa em nuvens."),
                ("rain", "Chuva", 20_000, "A primeira chuva cai."),
                ("water", "Água Líquida", 60_000, "A água fica líquida na superfície."),
                ("lakes", "Lagos", 150_000, "A bacia começa a encher."),
                ("moss", "Musgo", 400_000, "Musgo cobre as rochas úmidas."),
                ("flora", "Flora", 1_000_000, "Plantas se espalham pelo vale."),
                ("forest", "Florestas", 2_500_000, "Árvores crescem sozinhas."),
                ("fauna", "Fauna", 6_000_000, "Os primeiros animais caminham pelo vale."),
                ("genesis", "Renascimento", 15_000_000, "A Terra respira sozinha. A nave-mãe pode voltar à Estação Horizonte."),
            };

            // A tabela acima é a fonte da verdade dos estágios: sincroniza sempre.
            var result = new Dictionary<string, TerraformStage>();
            foreach (var s in data)
            {
                var stage = Asset<TerraformStage>($"{DataDir}/Stages/{s.id}.asset", a => { });
                stage.displayName = s.name;
                stage.requiredTi = s.ti;
                stage.description = s.desc;
                EditorUtility.SetDirty(stage);
                result[s.id] = stage;
            }

            db = Asset<StageDatabase>(DataDir + "/StageDatabase.asset", d => { });
            db.SetStages(result.Values);
            EditorUtility.SetDirty(db);
            return result;
        }

        static Dictionary<string, ItemDefinition> CreateItems()
        {
            var data = new (string id, string name, int stack, float o2, float water, float food)[]
            {
                ("iron", "Ferro", 10, 0, 0, 0),
                ("silicon", "Silício", 10, 0, 0, 0),
                ("titanium", "Titânio", 10, 0, 0, 0),
                ("ice", "Gelo", 10, 0, 0, 0),
                ("magnesium", "Magnésio", 10, 0, 0, 0),
                ("alloy", "Liga Metálica", 10, 0, 0, 0),
                ("o2_capsule", "Cápsula de O2", 5, 40, 0, 0),
                ("water_bottle", "Garrafa d'Água", 5, 0, 35, 0),
                ("nutrient_bar", "Barra Nutritiva", 5, 0, 0, 40),
                ("uranium", "Urânio", 10, 0, 0, 0),
                ("fuel_rod", "Barra de Combustível", 5, 0, 0, 0),
            };

            var result = new Dictionary<string, ItemDefinition>();
            foreach (var i in data)
            {
                result[i.id] = Asset<ItemDefinition>($"{DataDir}/Items/{i.id}.asset", a =>
                {
                    a.id = i.id;
                    a.displayName = i.name;
                    a.maxStack = i.stack;
                    a.restoreOxygen = i.o2;
                    a.restoreWater = i.water;
                    a.restoreFood = i.food;
                });
            }
            return result;
        }

        static List<RecipeDefinition> CreateRecipes(Dictionary<string, ItemDefinition> it)
        {
            RecipeDefinition R(string id, string output, double ti, string blueprint, params ItemAmount[] ingredients) =>
                Asset<RecipeDefinition>($"{DataDir}/Recipes/{id}.asset", a =>
                {
                    a.id = id;
                    a.output = it[output];
                    a.outputAmount = 1;
                    a.requiredTi = ti;
                    a.blueprintId = blueprint;
                    a.ingredients = ingredients;
                });

            return new List<RecipeDefinition>
            {
                R("o2_capsule", "o2_capsule", 0, null, C(it["ice"], 1), C(it["silicon"], 1)),
                R("water_bottle", "water_bottle", 0, null, C(it["ice"], 1)),
                R("nutrient_bar", "nutrient_bar", 0, null, C(it["ice"], 1), C(it["magnesium"], 1)),
                R("alloy", "alloy", 300, null, C(it["iron"], 1), C(it["titanium"], 1), C(it["magnesium"], 1)),
                R("fuel_rod", "fuel_rod", 50_000, "bp_nuclear", C(it["uranium"], 2), C(it["alloy"], 1)),
            };
        }

        static List<BuildableDefinition> CreateBuildables(Dictionary<string, ItemDefinition> it, MaterialLibrary m)
        {
            var list = new List<BuildableDefinition>();

            void Add(string id, string name, string desc, double ti, float gen, float demand, ItemAmount[] cost,
                TerraformRate[] rates, bool isMachine, string model, float scale = 1f, bool pressurized = false, bool storage = false,
                string blueprint = null, bool unique = false, PieceKind snap = PieceKind.None, System.Action<GameObject> setup = null)
            {
                var def = Asset<BuildableDefinition>($"{DataDir}/Buildables/{id}.asset", a =>
                {
                    a.id = id;
                    a.displayName = name;
                    a.description = desc;
                    a.requiredTi = ti;
                    a.powerGeneration = gen;
                    a.powerDemand = demand;
                    a.cost = cost;
                    a.terraformRates = rates;
                });
                // Textos e projeto exigido fazem parte do design (e da tradução em LocData): sempre sincronizam.
                def.displayName = name;
                def.description = desc;
                def.blueprintId = blueprint;
                def.unique = unique;
                // Prefab é derivado do modelo: sempre regenera para pegar mudanças do gen_models.py.
                def.snap = snap;
                def.prefab = SavePrefab(def, isMachine, pressurized, model, scale, storage, setup);
                EditorUtility.SetDirty(def);
                list.Add(def);
            }

            var none = Array.Empty<TerraformRate>();
            Add("base_module", "Módulo de Base", "Abrigo pressurizado: recarrega oxigênio.", 0, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Machines/base_module", pressurized: true);
            Add("storage_chest", "Baú", "Guarda até 30 pilhas de itens. [E] para abrir.", 0, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "ph:metal_tool_chest", storage: true);
            Add("wind_turbine", "Gerador Eólico", "+1.2 kW", 0, 1.2f, 0,
                new[] { C(it["iron"], 1), C(it["silicon"], 1) }, none, true, "Machines/wind_turbine");
            Add("heater_t1", "Aquecedor", "Calor +1.2/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 1), C(it["titanium"], 1) }, Rates(TerraformParameter.Heat, 1.2), true, "Machines/heater_1");
            Add("drill_t1", "Perfuratriz", "Pressão +1/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 2) }, Rates(TerraformParameter.Pressure, 1), true, "Machines/drill_1");
            Add("decon_t1", "Filtro de Radiação", "Descontaminação +1/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 2), C(it["silicon"], 1) }, Rates(TerraformParameter.Decontamination, 1), true,
                "Machines/decon_1");
            Add("vegetube", "Vegetubo", "Oxigênio +0.8/s. Consome 0.5 kW.", 50, 0, 0.5f,
                new[] { C(it["ice"], 1), C(it["silicon"], 1) }, Rates(TerraformParameter.Oxygen, 0.8), true, "Machines/vegetube");
            Add("solar_panel", "Painel Solar", "+6.5 kW", 300, 6.5f, 0,
                new[] { C(it["silicon"], 2), C(it["iron"], 1) }, none, true, "Machines/solar_panel");
            Add("heater_t2", "Aquecedor II", "Calor +12/s. Consome 4 kW.", 1_000, 0, 4f,
                new[] { C(it["titanium"], 2), C(it["alloy"], 1) }, Rates(TerraformParameter.Heat, 12), true, "Machines/heater_2");
            Add("drill_t2", "Perfuratriz II", "Pressão +10/s. Consome 4 kW.", 2_500, 0, 4f,
                new[] { C(it["iron"], 3), C(it["alloy"], 1) }, Rates(TerraformParameter.Pressure, 10), true, "Machines/drill_2");
            Add("algae_farm", "Fazenda de Algas", "Biomassa +25/s, Oxigênio +5/s. Consome 2 kW.", 20_000, 0, 2f,
                new[] { C(it["ice"], 2), C(it["magnesium"], 1) },
                new[] { new TerraformRate(TerraformParameter.Biomass, 25), new TerraformRate(TerraformParameter.Oxygen, 5) }, true, "Machines/algae_farm");
            // Máquinas escaneadas (Poly Haven): condensador (unidade externa de ar-condicionado) e gerador a combustível.
            Add("condenser", "Condensador Atmosférico", "Pressão +8/s, Biomassa +4/s. Consome 2.5 kW.", 8_000, 0, 2.5f,
                new[] { C(it["titanium"], 2), C(it["silicon"], 2), C(it["magnesium"], 1) },
                new[] { new TerraformRate(TerraformParameter.Pressure, 8), new TerraformRate(TerraformParameter.Biomass, 4) },
                true, "ph:exterior_aircon_unit");
            Add("fuel_generator", "Gerador a Combustível", "+8 kW. Barulhento, mas compacto.", 20_000, 8f, 0,
                new[] { C(it["iron"], 4), C(it["alloy"], 1), C(it["titanium"], 1) }, none, true, "ph:portable_generator");
            Add("shipyard", "Estaleiro", "Plataforma para montar a nave-mãe que leva a notícia de volta à Estação Horizonte. Só um por mundo.", 2_500_000, 0, 0,
                new[] { C(it["alloy"], 6), C(it["titanium"], 4), C(it["iron"], 8) }, none, false, "Machines/shipyard", unique: true);
            Add("decon_t2", "Filtro de Radiação II", "Descontaminação +15/s. Consome 4 kW.", 5_000, 0, 4f,
                new[] { C(it["titanium"], 2), C(it["alloy"], 1), C(it["magnesium"], 1) },
                Rates(TerraformParameter.Decontamination, 15), true, "Machines/decon_2");
            Add("heater_t3", "Aquecedor III", "Calor +400/s. Consome 15 kW.", 60_000, 0, 15f,
                new[] { C(it["titanium"], 4), C(it["alloy"], 2) }, Rates(TerraformParameter.Heat, 400), true, "Machines/heater_3");

            // Tier 4: exigem microchip de destroço (blueprint) além do Ti.
            Add("vegetube_t2", "Vegetubo II", "Oxigênio +60/s. Consome 6 kW.", 60_000, 0, 6f,
                new[] { C(it["silicon"], 3), C(it["alloy"], 1), C(it["ice"], 2) }, Rates(TerraformParameter.Oxygen, 60), true,
                "Machines/vegetube", 1.6f, blueprint: "bp_vegetube2");
            Add("nuclear_reactor", "Reator Nuclear", "+80 kW. Precisa de barras de combustível para construir.", 50_000, 80f, 0,
                new[] { C(it["alloy"], 3), C(it["titanium"], 2), C(it["fuel_rod"], 1) }, none, true,
                "Machines/nuclear_reactor", blueprint: "bp_nuclear");
            Add("heater_t4", "Aquecedor IV", "Calor +6000/s. Consome 40 kW.", 300_000, 0, 40f,
                new[] { C(it["titanium"], 5), C(it["alloy"], 3), C(it["fuel_rod"], 1) }, Rates(TerraformParameter.Heat, 6000), true,
                "Machines/heater_4", blueprint: "bp_heater4");
            Add("drill_t3", "Perfuratriz III", "Pressão +3000/s. Consome 35 kW.", 300_000, 0, 35f,
                new[] { C(it["iron"], 6), C(it["alloy"], 3), C(it["fuel_rod"], 1) }, Rates(TerraformParameter.Pressure, 3000), true,
                "Machines/drill_3", blueprint: "bp_drill3");
            Add("decon_t3", "Filtro de Radiação III", "Descontaminação +3500/s. Consome 30 kW.", 300_000, 0, 30f,
                new[] { C(it["titanium"], 4), C(it["alloy"], 3), C(it["fuel_rod"], 1) },
                Rates(TerraformParameter.Decontamination, 3500), true, "Machines/decon_3", blueprint: "bp_decon3");

            // ---- Kit de base: peças em grade de 4 m (BaseGrid), utilidades, mobília e defesas ----
            Add("base_foundation", "Fundação", "Laje de 4 x 4 m sobre estacas: define o nível do piso da base.", 100, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Base/base_foundation", snap: PieceKind.Foundation, setup: Piece(PieceKind.Foundation, 500f));
            Add("base_floor", "Piso e Teto", "Piso de 4 x 4 m. Empilhe para fazer andares e tetos.", 100, 0, 0,
                new[] { C(it["iron"], 1) }, none, false, "Base/base_floor", snap: PieceKind.Floor, setup: Piece(PieceKind.Floor, 300f));
            Add("base_wall", "Parede", "Parede de painéis de 4 m. Vede as quatro arestas e o teto de uma sala.", 100, 0, 0,
                new[] { C(it["iron"], 1) }, none, false, "Base/base_wall", snap: PieceKind.Wall, setup: Piece(PieceKind.Wall, 350f, 0.1f));
            Add("base_wall_door", "Parede com Porta", "Parede com passagem. Mantém a sala selada.", 250, 0, 0,
                new[] { C(it["iron"], 1), C(it["silicon"], 1) }, none, false, "Base/base_wall_door", snap: PieceKind.Wall, setup: Piece(PieceKind.Wall, 300f, 0.1f));
            Add("base_wall_window", "Parede com Janela", "Parede com janela panorâmica.", 500, 0, 0,
                new[] { C(it["iron"], 1), C(it["silicon"], 2) }, none, false, "Base/base_wall_window", snap: PieceKind.Wall, setup: Piece(PieceKind.Wall, 200f));
            Add("base_airlock", "Câmara de Ar", "Escotilha blindada para entrar e sair da base.", 1_200, 0, 0,
                new[] { C(it["iron"], 2), C(it["titanium"], 1) }, none, false, "Base/base_airlock", snap: PieceKind.Wall, setup: Piece(PieceKind.Wall, 450f, 0.2f));
            Add("base_wall_heavy", "Parede Reforçada", "Liga metálica: aguenta muito mais dano.", 30_000, 0, 0,
                new[] { C(it["titanium"], 1), C(it["alloy"], 1) }, none, false, "Base/base_wall_heavy", snap: PieceKind.Wall, setup: Piece(PieceKind.Wall, 900f, 0.35f));
            Add("base_pillar", "Pilar", "Coluna de apoio para os cantos.", 150, 0, 0,
                new[] { C(it["iron"], 1) }, none, false, "Base/base_pillar", snap: PieceKind.Pillar, setup: Piece(PieceKind.Pillar, 250f));
            Add("base_stairs", "Escada", "Sobe um andar (3 m). [R] gira.", 200, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Base/base_stairs", snap: PieceKind.Stairs, setup: Piece(PieceKind.Stairs, 250f));
            Add("base_o2gen", "Gerador de O2", "Pressuriza a sala selada em que está. Consome 0.8 kW.", 300, 0, 0.8f,
                new[] { C(it["iron"], 2), C(it["silicon"], 1), C(it["ice"], 1) }, none, true, "Base/base_o2gen", snap: PieceKind.Furniture,
                setup: Piece(PieceKind.Furniture, 200f, 0f, true));
            Add("base_battery", "Célula de Energia", "+3 kW.", 1_500, 3f, 0,
                new[] { C(it["silicon"], 2), C(it["iron"], 1), C(it["titanium"], 1) }, none, true, "Base/base_battery", snap: PieceKind.Furniture,
                setup: Piece(PieceKind.Furniture, 160f));
            Add("base_purifier", "Purificador de Água", "[E] para beber. Consome 0.4 kW.", 800, 0, 0.4f,
                new[] { C(it["iron"], 2), C(it["ice"], 1), C(it["silicon"], 1) }, none, true, "Base/base_purifier", snap: PieceKind.Furniture,
                setup: root => { Piece(PieceKind.Furniture, 160f)(root); Utility(root, 2); });
            Add("base_bunk", "Beliche", "[E] para descansar e recuperar vida.", 500, 0, 0,
                new[] { C(it["iron"], 2), C(it["silicon"], 1) }, none, false, "Base/base_bunk", snap: PieceKind.Furniture,
                setup: root => { Piece(PieceKind.Furniture, 150f)(root); Utility(root, 0); });
            Add("base_medbay", "Enfermaria", "[E] cura todos os ferimentos. Consome 0.6 kW.", 3_000, 0, 0.6f,
                new[] { C(it["silicon"], 2), C(it["titanium"], 1), C(it["magnesium"], 1) }, none, true, "Base/base_medbay", snap: PieceKind.Furniture,
                setup: root => { Piece(PieceKind.Furniture, 160f)(root); Utility(root, 1); });
            Add("base_locker", "Armário", "Guarda até 30 pilhas de itens. [E] para abrir.", 400, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Base/base_locker", snap: PieceKind.Furniture, storage: true, setup: Piece(PieceKind.Furniture, 150f));
            Add("base_workbench", "Bancada", "Bancada de trabalho: dá vida à base.", 800, 0, 0,
                new[] { C(it["iron"], 3), C(it["silicon"], 1) }, none, false, "Base/base_workbench", snap: PieceKind.Furniture, setup: Piece(PieceKind.Furniture, 150f));
            Add("base_kitchen", "Cozinha", "Cozinha compacta: a base fica mais confortável.", 1_500, 0, 0,
                new[] { C(it["iron"], 2), C(it["silicon"], 1), C(it["ice"], 1) }, none, false, "Base/base_kitchen", snap: PieceKind.Furniture,
                setup: Piece(PieceKind.Furniture, 150f));
            Add("base_turret_1", "Torre de Pulso", "Atira nos inimigos próximos. Consome 0.8 kW.", 3_000, 0, 0.8f,
                new[] { C(it["iron"], 3), C(it["silicon"], 2), C(it["titanium"], 1) }, none, true, "Base/base_turret_1",
                setup: root => { Piece(PieceKind.Defense, 220f, 0.1f)(root); Turret(root, 26f, 14f, 0.55f, false); });
            Add("base_turret_2", "Torre de Plasma", "Dano pesado e longo alcance. Consome 3 kW.", 40_000, 0, 3f,
                new[] { C(it["titanium"], 3), C(it["alloy"], 2), C(it["silicon"], 3) }, none, true, "Base/base_turret_2",
                setup: root => { Piece(PieceKind.Defense, 520f, 0.25f)(root); Turret(root, 36f, 38f, 0.8f, true); });
            Add("base_floodlight", "Refletor", "Ilumina a área e afasta os lobos. Consome 0.3 kW.", 1_200, 0, 0.3f,
                new[] { C(it["iron"], 2), C(it["silicon"], 1) }, none, true, "Base/base_floodlight",
                setup: root => { Piece(PieceKind.Defense, 120f)(root); Floodlight(root); });
            Add("base_shock_fence", "Cerca Eletrificada", "Dá choque em quem atravessa. Consome 0.6 kW.", 4_000, 0, 0.6f,
                new[] { C(it["iron"], 2), C(it["titanium"], 1) }, none, true, "Base/base_shock_fence",
                setup: root => { Piece(PieceKind.Defense, 160f)(root); Fence(root); });
            return list;
        }


        // ---------- Componentes do kit de base ----------

        static System.Action<GameObject> Piece(PieceKind kind, float hp, float armor = 0f, bool air = false) => root =>
        {
            var bp = root.AddComponent<BasePiece>();
            var so = new SerializedObject(bp);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("maxHealth").floatValue = hp;
            so.FindProperty("armor").floatValue = armor;
            so.FindProperty("isAirSource").boolValue = air;
            so.ApplyModifiedPropertiesWithoutUndo();
        };

        static void Utility(GameObject root, int mode)
        {
            var u = root.AddComponent<BaseUtility>();
            var so = new SerializedObject(u);
            so.FindProperty("mode").enumValueIndex = mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Turret(GameObject root, float range, float damage, float interval, bool heavy)
        {
            var t = root.AddComponent<Planetcraft.Defense.Turret>();
            var so = new SerializedObject(t);
            so.FindProperty("range").floatValue = range;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("interval").floatValue = interval;
            so.FindProperty("heavy").boolValue = heavy;
            so.FindProperty("tracer").colorValue = heavy ? new Color(1f, 0.62f, 0.2f, 0.95f) : new Color(0.5f, 0.95f, 1f, 0.9f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Floodlight(GameObject root)
        {
            var lamp = new GameObject("Luz");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 3.5f, 0.35f);
            lamp.transform.localRotation = Quaternion.Euler(35f, 180f, 0f);
            var l = lamp.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 95f;
            ConfigureHdLight(l, 60000f, LightUnit.Lumen, 38f, new Color(1f, 0.96f, 0.85f));
            var f = root.AddComponent<Planetcraft.Defense.Floodlight>();
            var so = new SerializedObject(f);
            var arr = so.FindProperty("lights");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = l;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Fence(GameObject root)
        {
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.9f, 0f);
            box.size = new Vector3(4f, 1.8f, 1.4f);
            root.AddComponent<Planetcraft.Defense.ShockFence>();
        }

        // Sentinela: drone hostil. Fica em Resources para a invasão (ThreatDirector) instanciar em jogo.
        static GameObject EnsureSentinelPrefab()
        {
            const string path = "Assets/_Project/Resources/Enemies/sentinel.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var root = new GameObject("Sentinela");
            var visual = Model("Fauna/sentinel", root.transform);
            visual.transform.localScale = Vector3.one * 1.1f;
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.7f;
            var health = root.AddComponent<Planetcraft.Combat.Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("max").floatValue = 60f;
            hso.FindProperty("team").enumValueIndex = (int)Planetcraft.Combat.Team.Hostile;
            hso.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<Planetcraft.Enemies.Sentinel>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Material das partículas das máquinas (fumaça, poeira, névoa, faíscas): fica em Resources para o jogo carregar.
        static void EnsureFxMaterial()
        {
            const string path = "Assets/_Project/Resources/Fx/FxSoft.mat";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var shader = Shader.Find("Planetcraft/FxSoft");
            if (shader == null) return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) AssetDatabase.CreateAsset(new Material(shader), path);
            else if (mat.shader != shader) mat.shader = shader;
            AssetDatabase.SaveAssets();
        }

        // Etapas da nave-mãe, em ordem de montagem (a tabela é a fonte da verdade).
        static ShipModule[] CreateShipModules(Dictionary<string, ItemDefinition> it)
        {
            Directory.CreateDirectory(DataDir + "/ShipModules");
            var data = new (string id, string name, string desc, double ti, string obj, ItemAmount[] cost)[]
            {
                ("ship_hull", "Casco", "Casco facetado em flecha e blindagem.", 2_500_000, "Hull",
                    new[] { C(it["alloy"], 8), C(it["iron"], 10), C(it["titanium"], 6) }),
                ("ship_engines", "Motores", "Propulsores da popa e naceles sob as asas.", 4_000_000, "Engines",
                    new[] { C(it["alloy"], 6), C(it["titanium"], 4), C(it["fuel_rod"], 3) }),
                ("ship_superstructure", "Superestrutura", "Espinha dorsal com conveses, hangares e alojamentos.", 6_000_000, "Superstructure",
                    new[] { C(it["iron"], 10), C(it["silicon"], 8), C(it["alloy"], 4) }),
                ("ship_bridge", "Ponte de comando", "Cabine de comando e mastro de sensores.", 8_000_000, "Bridge",
                    new[] { C(it["silicon"], 8), C(it["magnesium"], 6), C(it["alloy"], 4) }),
                ("ship_hyperdrive", "Reator de salto", "Anel ventral de salto para alcançar a órbita da estação.", 12_000_000, "Hyperdrive",
                    new[] { C(it["fuel_rod"], 4), C(it["uranium"], 6), C(it["alloy"], 6) }),
            };
            var list = new List<ShipModule>();
            foreach (var d in data)
            {
                var module = Asset<ShipModule>($"{DataDir}/ShipModules/{d.id}.asset", a => { });
                module.id = d.id;
                module.displayName = d.name;
                module.description = d.desc;
                module.requiredTi = d.ti;
                module.objectName = d.obj;
                module.cost = d.cost;
                EditorUtility.SetDirty(module);
                list.Add(module);
            }
            return list.ToArray();
        }

        // O prefab do estaleiro ganha a nave-mãe (partes escondidas até instalar) e o componente Shipyard.
        static void ConfigureShipyardPrefab(BuildableDefinition def, ShipModule[] modules, TerraformStage launchStage)
        {
            string path = AssetDatabase.GetAssetPath(def.prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            var ship = Model("Sky/mothership", root.transform);
            ship.name = "NaveMae";
            ship.transform.localPosition = new Vector3(0f, 7.2f, 0f);
            FaceForward(ship, longerSideIsFront: false); // motores (lado mais comprido) para trás
            var rumble = root.AddComponent<AudioSource>();
            rumble.playOnAwake = false;
            rumble.spatialBlend = 0f;
            root.AddComponent<Shipyard>().EditorSetup(ship.transform, modules, launchStage, rumble);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Registros de história encontrados nos destroços (a tabela é a fonte da verdade).
        static List<StoryLog> CreateLogs()
        {
            var data = new (string id, string title, string body)[]
            {
                ("log_01", "Bunker Lisboa-7 — Diário de Ilsa Moreau, agrônoma",
                 "Ano 2189. As sirenes pararam ontem. Lá fora o céu está cor de ferrugem e nada que eu planto passa da " +
                 "primeira folha.\n\nGravei neste núcleo de dados o projeto dos vegetubos de alta eficiência. Se um dia " +
                 "alguém voltar a pisar aqui fora, plante. Plante muito."),
                ("log_02", "Relatório técnico — Reator KR-4",
                 "O reator compacto KR-4 roda com barras de urânio refinado misturado com liga metálica.\n\nOs veios " +
                 "verdes que brilham perto das crateras são urânio exposto pelas ogivas da Última Guerra. Use traje ao " +
                 "coletar. O projeto completo do reator está no núcleo deste abrigo."),
                ("log_03", "Última ordem — Conselho da Estação Horizonte",
                 "Ano zero. Partimos hoje com os últimos quarenta mil. Os esterilizadores atmosféricos que lançamos uns " +
                 "contra os outros não podem mais ser desligados: vão queimar o ar e o solo por eras.\n\nA quem um dia " +
                 "voltar: quando a Terra respirar sozinha, monte uma nave-mãe no estaleiro e traga a notícia de volta " +
                 "para a estação. Vamos esperar o tempo que for preciso."),
                ("log_04", "IA de defesa — ATLAS",
                 "Registro automático. Esterilizadores atmosféricos: sem energia há 999.412 anos.\n\nDetecto " +
                 "atividade de terraformação na superfície, de origem humana. Atualizando projeções: com perfuratrizes " +
                 "de alta pressão o índice cresce em ordens de grandeza. Projeto anexado. Esperança: recalculando... " +
                 "valor positivo."),
                ("log_05", "Cofre genético — Dra. Nadia Okoro",
                 "Selei o cofre genético antes do último apagão. Sementes, insetos, animais: tudo em estase no fundo " +
                 "do vale, fundo o bastante para atravessar a radiação.\n\nQuando a Terra respirar sozinha, eles " +
                 "acordam. Você vai saber que deu certo quando ouvir algo correndo entre as árvores."),
                ("log_06", "Diário da Estação Horizonte — Ano 14",
                 "Ano 14 da estação. Hoje as crianças perguntaram por que o céu do vídeo é azul se a janela mostra marrom.\n\n" +
                 "Explicamos que a Terra é um lugar que está dormindo. A mais nova respondeu que então era preciso acordá-la " +
                 "com cuidado. Anotei isso no quadro da escola: a primeira lição da nossa geração é paciência."),
                ("log_07", "Carta de Tomás Rocha, engenheiro de reatores",
                 "A quem encontrar este abrigo: o KR-4 não perdoa pressa. Antes de ligar o núcleo, tenha energia estável e " +
                 "filtros de radiação rodando — o ar daqui ainda cobra juros.\n\nDeixei o projeto do filtro de três estágios " +
                 "na caixa preta da porta. Se a luz do painel estiver verde, o ar já está limpando. Boa sorte, e desculpe " +
                 "pelo estado do mundo."),
                ("log_08", "Arquivo ATLAS — Cronologia da Última Guerra",
                 "Dia 1: colapso das redes de alerta. Dia 2: ativação dos esterilizadores atmosféricos em oito frentes. " +
                 "Dia 3: evacuação da Terra encerrada, quarenta mil a bordo.\n\nNão houve vencedor. Os esterilizadores foram " +
                 "feitos para tornar o território inimigo inabitável; ninguém previu que o inimigo respirava o mesmo ar. " +
                 "Registro encerrado por ordem do Conselho."),
                ("log_09", "Canção de ninar (transcrição danificada)",
                 "...durma, Terra, durma, a chuva vai voltar. Teu azul está guardado onde a gente vai buscar. Quando o vento " +
                 "for brisa e o mar tiver cheiro de sal, a gente desce de volta pra cuidar do teu quintal...\n\nA última " +
                 "estrofe foi cantada por uma criança. A estação a tocou todas as noites durante um milhão de anos."),
                ("log_10", "Mensagem final — Estação Horizonte",
                 "Aqui é a Estação Horizonte. Recebemos o sinal de terraformação há onze anos e não ousamos acreditar. Hoje os " +
                 "sensores confirmaram: há oxigênio, há água, há vida.\n\nSe alguém aí embaixo ouve isto: terminem o que " +
                 "começaram e voltem para casa. A porta de acoplagem está aberta. Esperamos um milhão de anos; podemos " +
                 "esperar mais um dia."),
            };
            var list = new List<StoryLog>();
            foreach (var d in data)
            {
                var log = Asset<StoryLog>($"{DataDir}/Logs/{d.id}.asset", a => { });
                log.id = d.id;
                log.title = d.title;
                log.body = d.body;
                EditorUtility.SetDirty(log);
                list.Add(log);
            }
            return list;
        }

        static TerraformRate[] Rates(TerraformParameter p, double v) => new[] { new TerraformRate(p, v) };
        static ItemAmount C(ItemDefinition item, int amount) => new ItemAmount(item, amount);

        // ---------- Prefabs a partir dos modelos de tools/gen_models.py ----------

        const string ModelDir = Root + "/Models";

        // Instancia um FBX gerado (ex.: "Nature/tree_broad_0") como prefab aninhado.
        static GameObject Model(string name, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (asset == null) throw new Exception($"Modelo não encontrado: {ModelDir}/{name}.fbx (rode tools/gen_models.py)");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        // Peças escaneadas (tamanho real) montadas nas construções modeladas por nós: dão escala e acabamento de foto.
        // Coordenadas locais do Unity: +Z é a frente (porta) dos modelos do gen_models.py, -X o lado dos tanques de O2.
        static readonly Dictionary<string, (string id, Vector3 pos, float yaw)[]> Decor = new()
        {
            ["base_module"] = new[]
            {
                ("exterior_aircon_unit", new Vector3(2.55f, 0.6f, -1.0f), 90f),
                ("power_box_01", new Vector3(1.0f, 1.0f, -2.12f), 180f),
                ("security_light", new Vector3(0.9f, 2.9f, 2.1f), 0f),
                ("small_lpg_tank", new Vector3(-2.6f, 0f, 0.9f), 30f),
                ("portable_generator", new Vector3(2.9f, 0f, 1.8f), -70f),
                ("industrial_pastic_container", new Vector3(-1.4f, 0f, 2.9f), 10f),
            },
            ["nuclear_reactor"] = new[]
            {
                ("utility_box_02", new Vector3(1.7f, 0.4f, 1.55f), 180f),
                ("power_box_01", new Vector3(-1.6f, 0.4f, -1.9f), 0f),
                ("security_light", new Vector3(-1.6f, 1.5f, 1.15f), 0f),
            },
            // Plantas escaneadas dentro do tubo de vidro (raio interno ~0,5 m): três andares de samambaia e um arbusto.
            ["vegetube"] = new[]
            {
                ("fern_02_b", new Vector3(0f, 0.4f, 0f), 20f),
                ("fern_02_c", new Vector3(0.05f, 0.95f, -0.03f), 110f),
                ("fern_02_a", new Vector3(-0.04f, 1.5f, 0.04f), 200f),
                ("shrub_02_c", new Vector3(0f, 0.4f, 0.12f), 70f),
            },
            ["wind_turbine"] = new[] { ("utility_box_01", new Vector3(0.95f, 0.3f, 0.2f), 90f) },
            ["solar_panel"] = new[] { ("utility_box_01", new Vector3(0.75f, 0.2f, 0.1f), 90f) },
            ["algae_farm"] = new[]
            {
                ("industrial_pastic_container", new Vector3(2.15f, 0f, -0.6f), 80f),
                ("industrial_pastic_container", new Vector3(2.2f, 0f, 0.35f), 95f),
                ("small_lpg_tank", new Vector3(-2.0f, 0f, 0.9f), 0f),
            },
            ["shipyard"] = new[]
            {
                ("portable_welding_cart", new Vector3(-14.6f, 0.25f, 19.5f), 160f),
                ("tool_cart", new Vector3(-13.8f, 0.25f, 16.5f), 200f),
                ("metal_tool_chest", new Vector3(-15.6f, 0.25f, 17.6f), 90f),
                ("Barrel_01", new Vector3(14.6f, 0.25f, -21.5f), 0f),
                ("Barrel_01", new Vector3(15.3f, 0.25f, -20.8f), 40f),
                ("Barrel_01", new Vector3(14.5f, 0.25f, -20.4f), 75f),
                ("small_lpg_tank", new Vector3(13.6f, 0.25f, -22.2f), 0f),
                ("portable_generator", new Vector3(-12.5f, 0.25f, 21.8f), 100f),
                ("industrial_pastic_container", new Vector3(12.8f, 0.25f, 22.4f), 15f),
                ("industrial_pastic_container", new Vector3(13.5f, 0.25f, 21.6f), -20f),
            },
        };

        static void Dress(GameObject root, string id)
        {
            if (!Decor.TryGetValue(id, out var parts)) return;
            foreach (var (part, pos, yaw) in parts)
            {
                bool plant = part.StartsWith("fern") || part.StartsWith("shrub");
                // Plantas: sem colisor e em escala de vaso; as demais peças têm colisor (o jogador não as atravessa).
                var go = plant ? PolyHaven(part, root.transform) : PhProp(part, root.transform, 1f);
                if (plant) go.transform.localScale = Vector3.one * (part.StartsWith("fern") ? 1.5f : 0.55f);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        // Tamanho natural dos scans é pequeno (um fogão de 0,86 m): fator para o tamanho de jogo, antes do `scale` do tier.
        static float PhBaseScale(string id) => id switch
        {
            "barrel_stove" => 1.9f,
            "drill_press_01" => 2.6f,
            "metal_tool_chest" => 2.0f,
            "old_military_compressor" => 1.5f,
            "exterior_aircon_unit" => 2.2f,
            "portable_generator" => 3.0f,
            _ => 1f,
        };

        // Encaixa `visual` na base (y=0, centrado em x/z) dentro de `root` e põe um BoxCollider no root.
        // `root` precisa estar na origem com escala 1 (como no SavePrefab). Devolve os limites locais.
        static Bounds FitBox(GameObject visual, GameObject root, bool recenter)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (recenter)
            {
                var shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                visual.transform.position += shift;
                b.center += shift;
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center);
            box.size = b.size;
            return new Bounds(box.center, box.size);
        }

        static Material IndicatorMaterial()
        {
            string path = MatDir + "/Indicator.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("HDRP/Lit"));
            ModelPostprocessor.ConfigureLit(mat, new Color(0.3f, 1f, 0.4f), 0.4f);
            ModelPostprocessor.MakeEmissive(mat, new Color(0.3f, 1f, 0.4f), ModelPostprocessor.GlowNits);
            HDMaterial.ValidateMaterial(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Prop fotoescaneado com colisor, para a interação (terminal, caixa de microchip).
        static GameObject PhProp(string id, Transform parent, float scale)
        {
            var go = PolyHaven(id, parent);
            go.transform.localScale = Vector3.one * scale;
            // Colisor no próprio objeto: calcula os limites no espaço dele.
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var inv = new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f / go.transform.lossyScale.z);
            box.size = Vector3.Scale(b.size, inv);
            return go;
        }

        // Rocha-base de cada minério (scan do Poly Haven, escala de jogo e tinta) e o mineral cravado nela.
        static readonly Dictionary<string, (string rock, float scale, Color tint, string mineral)> OreRocks = new()
        {
            ["iron"] = ("moon_rock_01", 6.0f, new Color(0.72f, 0.5f, 0.42f), "nuggets_iron"),
            ["silicon"] = ("rock_07", 4.6f, new Color(0.78f, 0.82f, 0.9f), "druse_silicon"),
            ["ice"] = ("stone_01", 8.4f, new Color(0.88f, 0.96f, 1.08f), "druse_ice"),
            ["magnesium"] = ("moon_rock_03", 7.6f, new Color(1.2f, 1.18f, 1.1f), "druse_magnesium"),
            ["titanium"] = ("moon_rock_05", 8.6f, new Color(0.62f, 0.66f, 0.75f), "nuggets_titanium"),
            ["uranium"] = ("moon_rock_01", 5.4f, new Color(0.5f, 0.6f, 0.52f), "druse_uranium"),
        };

        // Minério = rocha escaneada + 3 a 6 aglomerados do mineral cravados na superfície dela: raio de cima para baixo
        // contra um colisor temporário da rocha, aglomerado alinhado à normal e meio afundado.
        static void Ore(string kind, GameObject node, float scale, System.Random rng)
        {
            var (rockId, rockScale, tint, mineral) = OreRocks[kind];
            var rock = PolyHaven(rockId, node.transform);
            rock.transform.localScale = Vector3.one * (rockScale * scale);
            rock.transform.localRotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            var temp = new List<Collider>();
            foreach (var r in rock.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = TintedRockMaterial(mats[i], kind, tint);
                r.sharedMaterials = mats;
                if (r.name.EndsWith("_LOD0") && r.TryGetComponent<MeshFilter>(out var mf))
                {
                    var mc = r.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    temp.Add(mc);
                }
            }
            Physics.SyncTransforms();

            var bounds = rock.GetComponentInChildren<Renderer>().bounds;
            int clusters = 3 + rng.Next(4);
            for (int c = 0, tries = 0; c < clusters && tries < 30; tries++)
            {
                var from = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, 0.2f + 0.6f * (float)rng.NextDouble()),
                    bounds.max.y + 1f, Mathf.Lerp(bounds.min.z, bounds.max.z, 0.2f + 0.6f * (float)rng.NextDouble()));
                if (!Physics.Raycast(from, Vector3.down, out var hit, bounds.size.y + 2f) || !temp.Contains(hit.collider)) continue;
                var cluster = Model("Ore/" + mineral, node.transform);
                cluster.transform.position = hit.point - hit.normal * 0.03f;
                cluster.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                                             Quaternion.Euler(0f, rng.Next(0, 360), 0f);
                cluster.transform.localScale = Vector3.one * (scale * (0.7f + 0.7f * (float)rng.NextDouble()));
                c++;
            }
            foreach (var col in temp) Object.DestroyImmediate(col);

            var sphere = node.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0.35f * scale, 0f);
            sphere.radius = 0.7f * scale;
        }

        static Material TintedRockMaterial(Material source, string kind, Color tint)
        {
            string path = $"{MatDir}/Ore_{kind}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(source) { name = $"Ore_{kind}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.GetTexture("_BaseColorMap") != source.GetTexture("_BaseColorMap") || kind == "iron")
            {
                EditorUtility.CopySerialized(source, mat); // rocha trocou: recopia do material-fonte
                mat.name = $"Ore_{kind}";
            }
            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Instancia um modelo do Poly Haven otimizado (tools/optimize_polyhaven.py).
        static GameObject PolyHaven(string id, Transform parent)
        {
            string path = $"{PolyHavenDir}/Models/{id}/{id}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception($"Modelo não encontrado: {path} (rode tools/fetch_polyhaven.py e optimize_polyhaven.py)");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        static GameObject SavePrefab(BuildableDefinition def, bool isMachine, bool pressurized, string model, float scale, bool storage,
            System.Action<GameObject> setup = null)
        {
            string path = $"{PrefabDir}/{def.id}.prefab";
            var root = new GameObject(def.displayName);
            bool scanned = model.StartsWith("ph:");
            var visual = scanned ? PolyHaven(model.Substring(3), root.transform) : Model(model, root.transform);
            visual.transform.localScale = Vector3.one * (scale * (scanned ? PhBaseScale(model.Substring(3)) : 1f));
            var placed = root.AddComponent<PlacedObject>();
            placed.EditorSetDefinition(def);
            GameObject scannedIndicator = null;
            if (scanned)
            {
                // Pivô na base, centrado; colisor em caixa e luz de funcionamento no topo (os scans não têm).
                var b = FitBox(visual, root, true);
                if (isMachine)
                {
                    scannedIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    scannedIndicator.name = "Indicator";
                    Object.DestroyImmediate(scannedIndicator.GetComponent<Collider>());
                    scannedIndicator.transform.SetParent(root.transform, false);
                    scannedIndicator.transform.localPosition = new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
                    scannedIndicator.transform.localScale = Vector3.one * 0.14f;
                    scannedIndicator.GetComponent<Renderer>().sharedMaterial = IndicatorMaterial();
                }
            }

            var rotor = visual.transform.Find("Rotor");
            if (rotor != null) rotor.gameObject.AddComponent<Spinner>();

            Dress(root, def.id);

            if (isMachine)
            {
                var machine = root.AddComponent<Machine>();
                root.AddComponent<MachineFx>();
                var indicator = scannedIndicator != null ? scannedIndicator.transform : visual.transform.Find("Indicator");
                if (indicator != null) SetRef(machine, "runningIndicator", indicator.gameObject);
            }

            if (storage)
            {
                var chestInventory = root.AddComponent<InventoryComponent>();
                var so = new SerializedObject(chestInventory);
                so.FindProperty("capacity").intValue = 30;
                so.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<StorageChest>();
            }

            if (pressurized)
            {
                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 1.5f, 0f);
                box.size = new Vector3(3.6f, 2.6f, 3.6f);
                root.AddComponent<PressurizedZone>();
            }

            setup?.Invoke(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tela de título: a Terra queimada vista da órbita (Blue Marble tingida de ferrugem, tempestades de poeira),
        // a nave-mãe em órbita, estrelas e o menu em UI Toolkit.
        static void BuildTitleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceLighting(Quaternion.Euler(20f, 120f, 0f));
            Earth(new Vector3(0f, -130f, 0f), 120f, scorched: true);

            var shipRoot = new GameObject("Nave");
            shipRoot.transform.position = new Vector3(0f, 12f, 0f);
            Model("Sky/mothership", shipRoot.transform).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            Stars(600f, 0.6f, 1.8f);
            var cam = SpaceCamera(2000f);
            var orbit = cam.gameObject.AddComponent<Orbiter>();
            SetRef(orbit, "target", shipRoot.transform);
            var orbitSo = new SerializedObject(orbit);
            orbitSo.FindProperty("lookOffset").vector3Value = new Vector3(0f, -6f, 0f);
            orbitSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = new GameObject("Menu");
            ui.AddComponent<UIDocument>().panelSettings = HudPanelSettings();
            ui.AddComponent<TitleMenu>();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }


        // Luz do HDRP com intensidade na unidade certa: o HDAdditionalLightData reescreve Light.intensity com o padrão dele,
        // então o valor precisa ir por aqui (lúmens para pontos e refletores, lux para o sol).
        static void ConfigureHdLight(Light l, float intensity, LightUnit unit, float range = 0f, Color? color = null, bool shadows = false)
        {
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = l.gameObject.AddComponent<HDAdditionalLightData>();
            hd.SetIntensity(intensity, unit);
            if (range > 0f) hd.range = range;
            if (color.HasValue) hd.color = color.Value;
            hd.EnableShadows(shadows);
        }

        // Abertura: a Estação Horizonte por dentro (cabine, corredor, hangar com a nave de descida) e o comunicador de pulso.
        // Coordenadas do Unity: o jogador anda de +Z (cabine em z 0..7, corredor 7..27, hangar 27..51).
        static void BuildStationScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceLighting(Quaternion.LookRotation(new Vector3(-0.55f, -0.45f, -0.7f)));
            var stationSun = GameObject.Find("Sol").GetComponent<Light>();
            // Sem sombras o sol atravessaria as paredes e acenderia tudo por dentro.
            ConfigureHdLight(stationSun, 6000f, LightUnit.Lux, shadows: true);
            GameObject.Find("Espaço").GetComponent<Volume>().sharedProfile = HdrpSetup.StationProfile();
            Earth(new Vector3(430f, -380f, 430f), 360f, scorched: true);
            Stars(900f, 0.8f, 2.4f);
            var mats = new MaterialLibrary();

            var station = new GameObject("Estação");
            Model("Station/station_interior", station.transform);

            // Luzes: cabine morna, corredor e hangar frios (as unidades do HDRP pedem valores altos com o brilho de saída).
            void Lamp(string name, Vector3 pos, float lumen, float range, Color color)
            {
                var go = new GameObject(name);
                go.transform.SetParent(station.transform, false);
                go.transform.position = pos;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                ConfigureHdLight(l, lumen, LightUnit.Lumen, range, color);
            }
            var cold = new Color(0.82f, 0.92f, 1f);
            var cabinLight = new Color(1f, 0.97f, 0.92f);
            Lamp("Luz cabine 1", new Vector3(0f, 2.9f, 1.8f), 90000f, 10f, cabinLight);
            Lamp("Luz cabine 2", new Vector3(0f, 2.9f, 5.2f), 90000f, 10f, cabinLight);
            for (int i = 0; i < 5; i++) Lamp($"Luz corredor {i}", new Vector3(0f, 2.8f, 9f + i * 4.2f), 36000f, 8f, cold);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    Lamp($"Luz hangar {i}{j}", new Vector3(-5.5f + j * 11f, 9.4f, 31f + i * 8.5f), 160000f, 22f, cold);
            // Sondas de reflexo em tempo real (atualizadas ao carregar): sem elas o alumínio polido fica preto.
            void Probe(string name, Vector3 center, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(station.transform, false);
                go.transform.position = center;
                var rp = go.AddComponent<ReflectionProbe>();
                rp.size = size;
                var hd = go.GetComponent<HDAdditionalReflectionData>() ?? go.AddComponent<HDAdditionalReflectionData>();
                hd.mode = ProbeSettings.Mode.Realtime;
                hd.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
                hd.influenceVolume.shape = InfluenceShape.Box;
                hd.influenceVolume.boxSize = size;
                hd.influenceVolume.boxBlendDistancePositive = Vector3.one * 0.3f;
                hd.influenceVolume.boxBlendDistanceNegative = Vector3.one * 0.3f;
            }
            Probe("Reflexo cabine", new Vector3(0f, 1.6f, 3.5f), new Vector3(6.4f, 3.6f, 7.4f));
            Probe("Reflexo corredor", new Vector3(0f, 1.5f, 17f), new Vector3(4f, 3.4f, 20.4f));
            Probe("Reflexo hangar", new Vector3(0f, 5f, 39f), new Vector3(20.6f, 10.4f, 24.6f));

            // Armário do traje (corredor, parede -X, de frente para o corredor) e nave de descida no pad do hangar.
            var locker = Model("Station/suit_locker", station.transform);
            locker.name = "ArmárioTraje";
            locker.transform.position = new Vector3(-1.42f, 0f, 15.5f);
            locker.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            var lockerUse = locker.AddComponent<Planetcraft.Intro.IntroInteract>();
            new SerializedObject(lockerUse).FindProperty("prompt").stringValue = "Pegar o traje";
            var lso = new SerializedObject(lockerUse);
            lso.FindProperty("prompt").stringValue = "Pegar o traje";
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Nave de descida: o módulo lunar Apollo da NASA (domínio público), 9,6 m, com escotilha e escada de frente para o +Z.
            var lander = new GameObject("NaveDescida");
            lander.transform.position = new Vector3(0f, 0f, 39f);
            var landerModel = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/NASA/lunar_module/lunar_module.fbx"), lander.transform);
            landerModel.transform.localPosition = Vector3.zero;
            var landerBody = lander.AddComponent<BoxCollider>();
            landerBody.center = new Vector3(0f, 3.6f, 0f);
            landerBody.size = new Vector3(5.2f, 6f, 5.2f);
            var hatch = new GameObject("Escotilha");
            hatch.transform.SetParent(lander.transform, false);
            hatch.transform.localPosition = new Vector3(0f, 1.6f, 3.9f);
            hatch.AddComponent<BoxCollider>().size = new Vector3(2.4f, 3.2f, 2.2f);
            var hatchUse = hatch.AddComponent<Planetcraft.Intro.IntroInteract>();
            var hso = new SerializedObject(hatchUse);
            hso.FindProperty("prompt").stringValue = "Embarcar";
            hso.ApplyModifiedPropertiesWithoutUndo();
            var cockpit = new GameObject("Cabine");
            cockpit.transform.SetParent(lander.transform, false);
            cockpit.transform.localPosition = new Vector3(0f, 6.5f, 1.6f);
            var engineLights = new List<GameObject>();
            foreach (var side in new[] { -1f, 1f })
            {
                var glow = new GameObject("Propulsor");
                glow.transform.SetParent(lander.transform, false);
                glow.transform.localPosition = new Vector3(side * 1.2f, 0.9f, 0f);
                var l = glow.AddComponent<Light>();
                l.type = LightType.Point;
                ConfigureHdLight(l, 90000f, LightUnit.Lumen, 18f, new Color(1f, 0.55f, 0.2f));
                engineLights.Add(glow);
            }
            // Luz de apoio de baixo para a cabine da nave: o hangar é iluminado por cima e a parte de baixo ficava preta.
            foreach (var side in new[] { -1f, 1f })
            {
                var fill = new GameObject("Apoio nave");
                fill.transform.SetParent(lander.transform, false);
                fill.transform.localPosition = new Vector3(side * 4.2f, 2.2f, -3.2f);
                var fl = fill.AddComponent<Light>();
                fl.type = LightType.Point;
                ConfigureHdLight(fl, 70000f, LightUnit.Lumen, 14f, new Color(0.85f, 0.92f, 1f));
            }

            // Marcadores do caminho (feixe azul com losango): cabine, armário, hangar e escotilha.
            var beaconMat = mats.Emissive("Beacon", new Color(0.4f, 0.9f, 1f));
            var beacons = new List<GameObject>();
            foreach (var pos in new[] { new Vector3(0f, 0f, 6.2f), new Vector3(-0.6f, 0f, 15.5f), new Vector3(0f, 0f, 28.2f), new Vector3(0f, 0f, 43.4f) })
            {
                var b = new GameObject("Marcador");
                b.transform.SetParent(station.transform, false);
                b.transform.position = pos;
                // Marcador discreto: anel luminoso no chão (sem feixe vertical no meio da tela) e uma seta que gira de leve.
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(b.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                ring.transform.localScale = new Vector3(1.3f, 0.01f, 1.3f);
                ring.GetComponent<Renderer>().sharedMaterial = beaconMat;
                ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(inner.GetComponent<Collider>());
                inner.transform.SetParent(b.transform, false);
                inner.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                inner.transform.localScale = new Vector3(1.05f, 0.01f, 1.05f);
                inner.GetComponent<Renderer>().sharedMaterial = mats.Lit("BeaconInner", new Color(0.03f, 0.06f, 0.08f), 0.2f);
                inner.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                beacons.Add(b);
            }

            // Jogador (começa deitado na cápsula: o IntroDirector posiciona a câmera) com o comunicador no braço.
            var playerGo = new GameObject("Player") { tag = "Player" };
            playerGo.transform.position = new Vector3(-0.9f, 0f, 3.2f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            var cam = SpaceCamera(3000f);
            cam.nearClipPlane = 0.05f;
            cam.transform.SetParent(playerGo.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var fpc = playerGo.AddComponent<FirstPersonController>();
            SetRef(fpc, "cameraPivot", cam.transform);
            var interactor = playerGo.AddComponent<Interactor>();
            SetRef(interactor, "cam", cam);
            playerGo.AddComponent<FootstepAudio>();

            var wrist = new GameObject("Pulso");
            wrist.transform.SetParent(cam.transform, false);
            wrist.transform.localPosition = new Vector3(-0.14f, -0.72f, 0.3f);
            wrist.transform.localRotation = Quaternion.Euler(-12f, 25f, 28f);
            wrist.transform.localScale = Vector3.one * 0.72f;
            Model("Station/wrist_arm", wrist.transform);
            // Luva de astronauta da NASA (domínio público) na ponta da manga.
            var glove = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/NASA/glove/glove.fbx"), wrist.transform);
            glove.transform.localPosition = new Vector3(0f, -0.09f, 0.03f);
            glove.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // Holograma: a UI do comunicador é desenhada numa RenderTexture e mostrada num quadro sobre o relógio.
            const string rtPath = Root + "/UI/HoloRT.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(768, 576, 0, RenderTextureFormat.ARGB32) { name = "HoloRT" };
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            var holoSettings = Asset<PanelSettings>(Root + "/UI/HoloPanelSettings.asset", ps => { });
            holoSettings.themeStyleSheet = HudPanelSettings().themeStyleSheet;
            holoSettings.targetTexture = rt;
            holoSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            holoSettings.clearColor = true;
            holoSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(holoSettings);

            var holoUi = new GameObject("HoloUI");
            holoUi.transform.SetParent(station.transform, false);
            holoUi.AddComponent<UIDocument>().panelSettings = holoSettings;
            var comm = holoUi.AddComponent<Planetcraft.Intro.HoloComm>();

            var holo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            holo.name = "Holograma";
            Object.DestroyImmediate(holo.GetComponent<Collider>());
            holo.transform.SetParent(wrist.transform, false);
            holo.transform.localPosition = new Vector3(0f, 0.27f, 0.12f);
            holo.transform.localRotation = Quaternion.Euler(-62f, 180f, 0f);
            holo.transform.localScale = new Vector3(0.5f, 0.375f, 1f);
            var holoMat = new Material(Shader.Find("Planetcraft/FxSoft")) { name = "Holograma" };
            holoMat.SetTexture("_MainTex", rt);
            holoMat.SetFloat("_Intensity", 700f);
            const string holoMatPath = Root + "/Materials/Holograma.mat";
            AssetDatabase.DeleteAsset(holoMatPath);
            AssetDatabase.CreateAsset(holoMat, holoMatPath);
            var holoRenderer = holo.GetComponent<Renderer>();
            holoRenderer.sharedMaterial = holoMat;
            holoRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Diretor da abertura e overlay (fade, dicas de interação).
            var director = new GameObject("Abertura");
            director.AddComponent<UIDocument>().panelSettings = HudPanelSettings();
            var intro = director.AddComponent<Planetcraft.Intro.IntroDirector>();
            var iso = new SerializedObject(intro);
            iso.FindProperty("player").objectReferenceValue = fpc;
            iso.FindProperty("interactor").objectReferenceValue = interactor;
            iso.FindProperty("cameraPivot").objectReferenceValue = cam.transform;
            iso.FindProperty("comm").objectReferenceValue = comm;
            iso.FindProperty("wrist").objectReferenceValue = wrist.transform;
            iso.FindProperty("holo").objectReferenceValue = holo.transform;
            var bed = new GameObject("PontoCama");
            bed.transform.SetParent(station.transform, false);
            bed.transform.SetPositionAndRotation(new Vector3(-1.55f, 1.25f, 1.8f), Quaternion.Euler(-78f, 6f, 0f));
            var stand = new GameObject("PontoEmPé");
            stand.transform.SetParent(station.transform, false);
            stand.transform.SetPositionAndRotation(new Vector3(-0.9f, 1.6f, 3.2f), Quaternion.Euler(0f, 0f, 0f));
            iso.FindProperty("bedPoint").objectReferenceValue = bed.transform;
            iso.FindProperty("standPoint").objectReferenceValue = stand.transform;
            iso.FindProperty("locker").objectReferenceValue = lockerUse;
            // Portas deslizantes: cabine -> corredor e corredor -> hangar (as molduras estão no station_interior).
            Planetcraft.Intro.SlidingDoor Door(string name, float z, float width, float height)
            {
                var root = new GameObject(name);
                root.transform.SetParent(station.transform, false);
                root.transform.localPosition = new Vector3(0f, 0f, z);
                var leaves = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    var leaf = Model("Station/station_door", root.transform);
                    leaf.name = i == 0 ? "FolhaEsq" : "FolhaDir";
                    float side = i == 0 ? -1f : 1f;
                    leaf.transform.localPosition = new Vector3(side * width / 4f, 0f, 0f);
                    leaf.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 0f : 180f, 0f);
                    leaf.transform.localScale = new Vector3((width / 2f + 0.03f) / 1f, height / 2.6f, 1f);
                    leaves[i] = leaf.transform;
                }
                var status = new GameObject("LuzEstado");
                status.transform.SetParent(root.transform, false);
                status.transform.localPosition = new Vector3(0f, height + 0.05f, -0.45f);
                var sl = status.AddComponent<Light>();
                sl.type = LightType.Point;
                ConfigureHdLight(sl, 1800f, LightUnit.Lumen, 3f, new Color(1f, 0.25f, 0.15f));
                var door = root.AddComponent<Planetcraft.Intro.SlidingDoor>();
                var dso = new SerializedObject(door);
                dso.FindProperty("left").objectReferenceValue = leaves[0];
                dso.FindProperty("right").objectReferenceValue = leaves[1];
                dso.FindProperty("travel").floatValue = width / 2f + 0.05f;
                dso.FindProperty("statusLight").objectReferenceValue = sl;
                dso.ApplyModifiedPropertiesWithoutUndo();
                return door;
            }
            iso.FindProperty("cabinDoor").objectReferenceValue = Door("PortaCabine", 7f, 1.8f, 2.5f);
            iso.FindProperty("hangarDoor").objectReferenceValue = Door("PortaHangar", 27f, 2.0f, 2.6f);
            iso.FindProperty("hatch").objectReferenceValue = hatchUse;
            var beaconArr = iso.FindProperty("beacons");
            beaconArr.arraySize = beacons.Count;
            for (int i = 0; i < beacons.Count; i++) beaconArr.GetArrayElementAtIndex(i).objectReferenceValue = beacons[i];
            iso.FindProperty("lander").objectReferenceValue = lander.transform;
            iso.FindProperty("cockpitPoint").objectReferenceValue = cockpit.transform;
            var earthPoint = new GameObject("AlvoTerra");
            earthPoint.transform.position = new Vector3(430f, -380f, 430f);
            iso.FindProperty("earthTarget").objectReferenceValue = earthPoint.transform;
            var fxArr = iso.FindProperty("engineFx");
            fxArr.arraySize = engineLights.Count;
            for (int i = 0; i < engineLights.Count; i++) fxArr.GetArrayElementAtIndex(i).objectReferenceValue = engineLights[i];
            iso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, StationScenePath);
        }

        // Epílogo: a Terra viva lá embaixo, a Estação Horizonte em órbita e a nave-mãe chegando para acoplar.
        static void BuildEpilogueScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpaceLighting(Quaternion.Euler(18f, -35f, 0f));
            Earth(new Vector3(-2500f, -9800f, -4000f), 8000f, scorched: false);

            var station = new GameObject("EstaçãoHorizonte");
            station.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
            // O porto do modelo fica no -Z local; meia-volta para o porto apontar para o +Z da raiz.
            Model("Sky/station", station.transform).transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var ship = new GameObject("NaveMae");
            Model("Sky/mothership", ship.transform);

            Stars(30000f, 40f, 110f);
            var cam = SpaceCamera(60000f);
            cam.transform.position = new Vector3(560f, 130f, 330f);
            cam.transform.LookAt(new Vector3(0f, 0f, 200f));

            var ui = new GameObject("Epílogo");
            ui.AddComponent<UIDocument>().panelSettings = HudPanelSettings();
            ui.AddComponent<EpilogueDirector>().EditorSetup(station.transform, ship.transform, cam.transform);

            EditorSceneManager.SaveScene(scene, EpilogueScenePath);
        }

        static void SpaceLighting(Quaternion sunRotation)
        {
            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 100000f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sunGo.transform.rotation = sunRotation;

            var space = new GameObject("Espaço").AddComponent<Volume>();
            space.isGlobal = true;
            space.sharedProfile = HdrpSetup.TitleProfile();
        }

        // Terra com a imagem Blue Marble (NASA). Queimada: superfície tingida de ferrugem e nuvens de poeira.
        static void Earth(Vector3 center, float radius, bool scorched)
        {
            var surface = Sphere(scorched ? "TerraQueimada" : "Terra", center, radius);
            surface.GetComponent<Renderer>().sharedMaterial = EarthMaterial(scorched ? "EarthScorched" : "EarthAlive",
                $"{NasaDir}/earth_4096.jpg", scorched ? new Color(0.78f, 0.5f, 0.34f) : new Color(1f, 1.04f, 1.1f), 0.25f, false);
            surface.transform.rotation = Quaternion.Euler(0f, 0f, 23f);
            var clouds = Sphere("Nuvens", center, radius * 1.006f);
            clouds.transform.SetParent(surface.transform, true);
            clouds.GetComponent<Renderer>().sharedMaterial = EarthMaterial(scorched ? "EarthDust" : "EarthClouds",
                $"{NasaDir}/clouds_2048.png", scorched ? new Color(0.72f, 0.58f, 0.45f, 0.85f) : new Color(1f, 1f, 1f, 0.45f), 0.1f, true);
        }

        static GameObject Sphere(string name, Vector3 center, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = center;
            go.transform.localScale = Vector3.one * radius * 2f;
            return go;
        }

        static Material EarthMaterial(string name, string texturePath, Color tint, float smoothness, bool transparent)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("HDRP/Lit"));
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (transparent) HDMaterial.SetSurfaceType(mat, true);
            HDMaterial.ValidateMaterial(mat);
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Stars(float radius, float minSize, float maxSize)
        {
            var stars = new GameObject("Estrelas").AddComponent<ParticleSystem>();
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stars.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = stars.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1500) });
            var shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = StarMaterial();
        }

        static Camera SpaceCamera(float far)
        {
            var camGo = new GameObject("Câmera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = far;
            // Fundo preto: as estrelas são partículas.
            var hdCam = camGo.AddComponent<HDAdditionalCameraData>();
            hdCam.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hdCam.backgroundColorHDR = Color.black;
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        // CREDITS.md vai para Resources para a tela de créditos do título (exigência das licenças CC BY).
        static void CopyCredits()
        {
            string dir = Root + "/Resources";
            Directory.CreateDirectory(dir);
            File.Copy("CREDITS.md", dir + "/credits.txt", overwrite: true);
            AssetDatabase.ImportAsset(dir + "/credits.txt", ImportAssetOptions.ForceSynchronousImport);
        }

        // PanelSettings + tema padrão de runtime para o HUD em UI Toolkit.
        static PanelSettings HudPanelSettings()
        {
            string dir = Root + "/UI";
            Directory.CreateDirectory(dir);
            string themePath = dir + "/HudTheme.tss";
            // Tema padrão + estilo do visor (fontes, botões e barras em Hud.uss).
            const string themeText = "@import url(\"unity-theme://default\");\n@import url(\"Hud.uss\");\n";
            if (!File.Exists(themePath) || File.ReadAllText(themePath) != themeText)
            {
                File.WriteAllText(themePath, themeText);
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            var settings = Asset<PanelSettings>(dir + "/HudPanelSettings.asset", ps => { });
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // Aplaina o terreno num círculo de `inner` m em volta de `center` até a altura `height` (local do terreno) e faz a
        // transição de volta ao relevo original até `outer` m (curva suave, sem degrau).
        static void FlattenTerrain(Terrain terrain, Vector3 center, float height, float inner, float outer)
        {
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var origin = terrain.transform.position;
            float cellX = td.size.x / (res - 1), cellZ = td.size.z / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - outer - origin.x) / cellX), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + outer - origin.x) / cellX), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - outer - origin.z) / cellZ), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + outer - origin.z) / cellZ), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = td.GetHeights(x0, z0, w, h);
            float target = height / td.size.y;
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var world = new Vector3(origin.x + (x0 + x) * cellX, 0f, origin.z + (z0 + z) * cellZ);
                float d = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(center.x, center.z));
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                heights[z, x] = Mathf.Lerp(heights[z, x], target, k);
            }
            td.SetHeights(x0, z0, heights);
        }

        // Do raio externo para dentro, o primeiro ponto do ângulo com pelo menos 20 m de altura (acima da água final)
        // e inclinação suave; se nenhum servir, devolve o mais alto encontrado.
        static float FindFirmGround(Terrain terrain, Vector3 center, float angleRad)
        {
            float best = 190f, bestHeight = float.MinValue;
            var dir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
            var td = terrain.terrainData;
            for (float r = 215f; r >= 110f; r -= 4f)
            {
                var p = center + dir * r;
                float h = terrain.SampleHeight(p);
                float slope = td.GetSteepness(p.x / td.size.x, p.z / td.size.z);
                if (h >= 20f && slope < 16f) return r;
                if (h > bestHeight) { bestHeight = h; best = r; }
            }
            return best;
        }

        static void SnapToGround(Transform t, Terrain terrain)
        {
            var p = t.position;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            t.position = p;
        }

        static GameObject Scaled(GameObject go, float scale)
        {
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // Modelos são feitos com a frente em +Y no Blender; confere pelo volume da malha para onde
        // essa frente foi no Unity e gira 180° se ela ficou para trás (AnimalWander anda em +Z).
        static GameObject FaceForward(GameObject model, bool longerSideIsFront = true)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds;
                var center = model.transform.InverseTransformPoint(mf.transform.TransformPoint(mb.center));
                if (first) { b = new Bounds(center, mb.size); first = false; }
                else b.Encapsulate(new Bounds(center, mb.size));
            }
            bool frontIsPositiveZ = longerSideIsFront ? b.max.z >= -b.min.z : b.max.z <= -b.min.z;
            if (!frontIsPositiveZ) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return model;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ---------- Cena ----------

        static void BuildScene(StageDatabase stageDb, Dictionary<string, TerraformStage> stages, GameDatabase db,
            Dictionary<string, ItemDefinition> items, MaterialLibrary m)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Luz física (HDRP): sol de ~100 mil lux; a exposição automática do volume adapta a câmera.
            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 100000f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            // Atmosfera: céu físico terrestre, nuvens volumétricas, neblina e pós-processamento.
            var atmosphereGo = new GameObject("Atmosfera");
            var atmosphere = atmosphereGo.AddComponent<Volume>();
            atmosphere.isGlobal = true;
            atmosphere.sharedProfile = HdrpSetup.AtmosphereProfile();

            // Terreno com uma bacia central que vira lago quando a água sobe
            var terrain = CreateTerrain();
            var center = new Vector3(TerrainSize / 2f, 0f, TerrainSize / 2f);

            // Água: superfície do HDRP (ondas, refração, cáusticas); o relevo é que forma o lago.
            var water = new GameObject("Água");
            water.transform.position = center + Vector3.up * -2f;
            var surface = water.AddComponent<WaterSurface>();
            surface.surfaceType = WaterSurfaceType.OceanSeaLake;
            surface.geometryType = WaterGeometryType.Infinite;
            surface.largeWindSpeed = 6f;
            surface.largeChaos = 0.6f;
            surface.ripplesWindSpeed = 5f;
            // Água clara de lago: cores de espalhamento mais altas evitam o aspecto de piche visto de longe.
            surface.refractionColor = new Color(0.45f, 0.78f, 0.8f);
            surface.scatteringColor = new Color(0.16f, 0.5f, 0.5f);
            surface.absorptionDistance = 6f;
            surface.caustics = true;
            var waterReactor = water.AddComponent<WaterReactor>();
            SetDouble(waterReactor, "tiStart", stages["water"].requiredTi);
            SetDouble(waterReactor, "tiEnd", stages["forest"].requiredTi);

            // Sistemas
            var systems = new GameObject("Sistemas");
            systems.AddComponent<TutorialHints>();
            var threats = systems.AddComponent<Planetcraft.Enemies.ThreatDirector>();
            SetRef(threats, "sentinelPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Enemies/sentinel.prefab"));
            SetRef(threats, "atmosphere", atmosphere);
            var tm = systems.AddComponent<TerraformManager>();
            SetRef(tm, "stages", stageDb);

            var sky = atmosphereGo.AddComponent<AtmosphereReactor>();
            SetRef(sky, "volume", atmosphere);
            SetRef(sky, "sun", sun);
            // A atmosfera limpa entre a Névoa e as Nuvens: cada estágio inicial muda o céu de forma visível.
            SetDouble(sky, "tiStart", 20);
            SetDouble(sky, "tiEnd", stages["rain"].requiredTi);

            var ground = terrain.gameObject.AddComponent<GroundReactor>();
            SetRef(ground, "terrain", terrain);
            SetDouble(ground, "tiStart", stages["moss"].requiredTi * 0.5);
            SetDouble(ground, "tiEnd", stages["forest"].requiredTi * 5);

            // Vegetação por estágio
            var rng = new System.Random(4242);
            VegetationSpacing.Reset();
            var vegetation = new GameObject("Vegetação");
            // Modelos fotoescaneados do Poly Haven (CC0), otimizados para jogo com LOD.
            // Densidade de paisagem real: samambaias e arbustos em tufos, árvores formando bosques. Nada nasce
            // abaixo do nível final da água nem em encostas de rocha.
            AddVegetationLayer(vegetation.transform, "Musgo", stages["moss"].requiredTi, stages["flora"].requiredTi, 2600, rng, terrain, center, (p, r) =>
                Scaled(PolyHaven(Variant(r, "fern_02", 4), p), 0.8f + (float)r.NextDouble() * 0.7f), maxSlope: 32f, clustering: 0.5f, footprint: 0.55f);
            AddVegetationLayer(vegetation.transform, "Flora", stages["flora"].requiredTi, stages["forest"].requiredTi, 1500, rng, terrain, center, (p, r) =>
                Scaled(PolyHaven(Variant(r, new[] { "shrub_02", "shrub_03", "shrub_04" }[r.Next(3)], 4), p), 0.7f + (float)r.NextDouble() * 0.7f),
                maxSlope: 30f, clustering: 0.6f, footprint: 0.7f);
            // Bosques mistos: jacarandás grandes (10-15 m) e árvores pequenas de copa aberta (5-8 m).
            AddVegetationLayer(vegetation.transform, "Florestas", stages["forest"].requiredTi, stages["fauna"].requiredTi * 2, 700, rng, terrain, center, (p, r) =>
            {
                bool big = r.NextDouble() < 0.45;
                var tree = big
                    ? Scaled(PolyHaven("jacaranda_tree", p), 0.5f + (float)r.NextDouble() * 0.25f)
                    : Scaled(PolyHaven("tree_small_02", p), 1.2f + (float)r.NextDouble() * 0.6f);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = big ? 0.9f : 0.3f;
                trunk.height = big ? 8f : 3f;
                trunk.center = new Vector3(0f, trunk.height / 2f, 0f);
                return tree;
            }, minRadius: 40f, maxRadius: 235f, maxSlope: 26f, clustering: 0.8f, footprint: 1f, isTree: true);

            GroundCoverAssets.Configure(terrain, ground);

            // Restos da Terra morta (sempre visíveis): rochas, troncos secos e tocos fossilizados.
            var remains = new GameObject("Restos da Terra").transform;
            string[] remainKinds = { "boulder_01", "namaqualand_boulder_02", "namaqualand_boulder_05", "boulder_01",
                                     "dead_tree_trunk", "dead_tree_trunk_02", "tree_stump_01" };
            for (int i = 0; i < 150; i++)
            {
                string kind = remainKinds[rng.Next(remainKinds.Length)];
                bool rockKind = kind.Contains("boulder");
                var piece = Scaled(PolyHaven(kind, remains), rockKind ? 0.5f + (float)rng.NextDouble() * 1.4f : 0.8f + (float)rng.NextDouble() * 0.5f);
                piece.transform.position = RandomPointInRing(rng, center, 15f, 235f, terrain) - Vector3.up * (rockKind ? 0.3f : 0.05f);
                piece.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            }

            // Fauna (Sketchfab, CC BY): cervos pastando e lobos em bandos, fora d'água.
            SketchfabAnimals.ClearCache();
            var fauna = new GameObject("Fauna");
            AddVegetationLayer(fauna.transform, "Herbívoros", stages["fauna"].requiredTi, stages["fauna"].requiredTi * 5, 45, rng, terrain, center,
                (p, r) => CreateAnimal(p, r, "Cervo", "deer", 1.75f, 1.6f, 30f, 1f), minRadius: 150f, maxRadius: 220f, clustering: 0.6f);
            AddVegetationLayer(fauna.transform, "Lobos", stages["fauna"].requiredTi * 2, stages["fauna"].requiredTi * 6, 18, rng, terrain, center,
                (p, r) => CreateAnimal(p, r, "Lobo", "wolf", 0.9f, 2.6f, 45f, 0.55f), minRadius: 140f, maxRadius: 225f, clustering: 0.9f);

            // Recursos
            var resources = new GameObject("Recursos");
            var nodeKinds = new (string item, float weight)[]
            {
                ("iron", 0.3f), ("silicon", 0.25f), ("ice", 0.2f), ("magnesium", 0.15f), ("titanium", 0.1f),
            };
            for (int i = 0; i < 260; i++)
            {
                float roll = (float)rng.NextDouble(), acc = 0f;
                var kind = nodeKinds[0];
                foreach (var k in nodeKinds)
                {
                    acc += k.weight;
                    if (roll <= acc) { kind = k; break; }
                }
                var pos = RandomPointInRing(rng, center, 30f, 200f, terrain);
                var node = new GameObject("Nó_" + kind.item);
                node.transform.SetParent(resources.transform, false);
                node.transform.SetPositionAndRotation(pos - Vector3.up * 0.05f, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                Ore(kind.item, node, 0.8f + (float)rng.NextDouble() * 0.4f, rng);
                node.AddComponent<ResourceNode>().EditorSetup($"node_{i}", items[kind.item], 1);
            }

            // Ruínas do velho mundo (abrigos de antes da guerra): farol, registro, microchip e urânio em volta.
            var wrecks = new GameObject("Ruínas");
            var sites = new (float angle, float radius, string blueprint, string label)[]
            {
                (22f, 186f, "bp_vegetube2", "Vegetubo II"),
                (95f, 196f, "bp_nuclear", "Reator Nuclear"),
                (165f, 184f, "bp_heater4", "Aquecedor IV"),
                (238f, 192f, "bp_drill3", "Perfuratriz III"),
                (308f, 200f, "bp_decon3", "Filtro de Radiação III"),
                // Ruínas sem projeto: só registro, urânio e história. O raio é buscado de fora para dentro até achar chão firme.
                (52f, 0f, null, null),
                (128f, 0f, null, null),
                (202f, 0f, null, null),
                (275f, 0f, null, null),
                (342f, 0f, null, null),
            };
            var beaconMat = m.Emissive("Beacon", new Color(0.4f, 0.9f, 1f));
            for (int s = 0; s < sites.Length; s++)
            {
                float a = sites[s].angle * Mathf.Deg2Rad;
                float radius = sites[s].radius > 0f ? sites[s].radius : FindFirmGround(terrain, center, a);
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                pos.y = terrain.SampleHeight(pos);
                var site = new GameObject($"Ruína_{s + 1}");
                site.transform.SetParent(wrecks.transform, false);
                site.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                // O corpo do bunker fica atrás do centro da ruína (z de -4 a -13 m no Unity). Em encosta, aplaina um platô
                // sob ele (com transição suave) na altura do centro da ruína, e assenta bunker e entulho nesse chão.
                FlattenTerrain(terrain, site.transform.TransformPoint(new Vector3(0f, 0f, -6f)), pos.y, 10f, 17f);
                var bunker = Model("Props/ruin_bunker", site.transform);
                bunker.transform.position = new Vector3(bunker.transform.position.x, pos.y + terrain.transform.position.y - 0.2f,
                    bunker.transform.position.z);

                // Entulho do velho mundo (scans): barreiras de concreto, pneus, aros e latões na frente do bunker.
                foreach (var (id, x, z, yaw) in new[] { ("concrete_road_barrier_02", -5.5f, -2.5f, 20f),
                             ("concrete_road_barrier_02", 6f, -1.8f, -35f), ("old_tyre", -3.8f, 1.2f, 0f),
                             ("rusted_wheel_rim_01", 4.6f, 2.2f, 60f), ("metal_trash_can", 1.8f, -3.2f, 0f),
                             ("Barrel_01", -6.4f, 0.8f, 0f) })
                {
                    var debris = PolyHaven(id, site.transform);
                    debris.transform.localPosition = new Vector3(x, 0f, z);
                    debris.transform.localRotation = Quaternion.Euler(0f, yaw + rng.Next(-20, 20), 0f);
                    SnapToGround(debris.transform, terrain);
                }

                var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "Farol";
                Object.DestroyImmediate(beacon.GetComponent<Collider>());
                beacon.transform.SetParent(site.transform, false);
                beacon.transform.localPosition = new Vector3(0f, 60f, 0f);
                beacon.transform.localScale = new Vector3(0.35f, 60f, 0.35f);
                var beaconRenderer = beacon.GetComponent<Renderer>();
                beaconRenderer.sharedMaterial = beaconMat;
                beaconRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var terminal = PhProp("vintage_spacecraft_instrument", site.transform, 3.6f);
                terminal.transform.localPosition = new Vector3(3.5f, 0f, -2.5f);
                terminal.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
                SnapToGround(terminal.transform, terrain);
                terminal.AddComponent<LogTerminal>().EditorSetup(db.logs[s]);

                // Sentinelas do velho mundo ainda guardam as ruínas mais afastadas.
                if (s >= 2)
                {
                    var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Enemies/sentinel.prefab");
                    int guards = s % 3 == 0 ? 2 : 1;
                    for (int g = 0; g < guards; g++)
                    {
                        var guard = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab, site.transform);
                        guard.name = "Sentinela guarda";
                        guard.transform.position = pos + new Vector3(g * 3f - 1.5f, terrain.transform.position.y + 3f, 4f);
                    }
                }

                if (sites[s].blueprint != null)
                {
                    var chip = new GameObject("Microchip");
                    chip.transform.SetParent(site.transform, false);
                    chip.transform.localPosition = new Vector3(-3f, 0f, 2f);
                    SnapToGround(chip.transform, terrain);
                    PhProp("ammo_box", chip.transform, 3.4f);
                    chip.AddComponent<ChipPickup>().EditorSetup(sites[s].blueprint, sites[s].label, new[] { beacon });
                }

                for (int i = 0; i < 8; i++)
                {
                    float na = i * Mathf.PI / 4f + (float)rng.NextDouble() * 0.4f;
                    var np = pos + new Vector3(Mathf.Cos(na), 0f, Mathf.Sin(na)) * (6f + (float)rng.NextDouble() * 5f);
                    np.y = terrain.SampleHeight(np) - 0.05f;
                    var node = new GameObject("Nó_uranium");
                    node.transform.SetParent(site.transform, true);
                    node.transform.SetPositionAndRotation(np, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                    Ore("uranium", node, 0.9f, rng);
                    node.AddComponent<ResourceNode>().EditorSetup($"node_wreck_{s}_{i}", items["uranium"], 1);
                }
            }
            // O platô das ruínas mudou o chão: reassenta o que já estava posicionado perto delas.
            var ruinCenters = new List<Vector3>();
            foreach (Transform site in wrecks.transform) ruinCenters.Add(site.TransformPoint(new Vector3(0f, 0f, -6f)));
            foreach (var group in new[] { "Vegetação", "Restos da Terra", "Recursos", "Fauna" })
            {
                var root = GameObject.Find(group);
                if (root == null) continue;
                bool natural = group == "Vegetação" || group == "Restos da Terra";
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t.parent == null || t.parent.parent != null && t.parent.parent.name != group && t.parent.name != group) continue;
                    float nearest = float.MaxValue;
                    foreach (var c in ruinCenters)
                        nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(c.x, c.z), new Vector2(t.position.x, t.position.z)));
                    if (nearest >= 17f || t.parent.name == "Vegetação") continue;
                    if (natural && nearest < 12f)
                    {
                        Object.DestroyImmediate(t.gameObject); // nada cresce de dentro do bunker
                        continue;
                    }
                    var p = t.position;
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.05f;
                    t.position = p;
                }
            }

            // Jogador
            var spawnPos = new Vector3(center.x + 140f, 0f, center.z);
            spawnPos.y = terrain.SampleHeight(spawnPos) + 1.5f;
            var respawn = new GameObject("PontoDeRenascimento").transform;
            respawn.position = spawnPos;
            respawn.rotation = Quaternion.Euler(0f, -90f, 0f);

            var playerGo = new GameObject("Player") { tag = "Player" };
            playerGo.transform.SetPositionAndRotation(spawnPos, respawn.rotation);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var camGo = new GameObject("Câmera") { tag = "MainCamera" };
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            camGo.AddComponent<AudioListener>();

            var fpc = playerGo.AddComponent<FirstPersonController>();
            SetRef(fpc, "cameraPivot", camGo.transform);

            // Início: dá para montar base + gerador + aquecedor nos primeiros minutos.
            var inv = playerGo.AddComponent<InventoryComponent>();
            SetStartingItems(inv, new[]
            {
                C(items["iron"], 5), C(items["silicon"], 2), C(items["titanium"], 1),
                C(items["o2_capsule"], 3), C(items["water_bottle"], 2), C(items["nutrient_bar"], 2),
            });

            // Aglomerado de recursos perto do ponto inicial para o começo não depender de sorte.
            var nearKinds = new[] { "iron", "iron", "iron", "iron", "silicon", "silicon", "silicon", "ice", "ice", "ice", "titanium", "titanium", "magnesium", "magnesium" };
            for (int i = 0; i < nearKinds.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / nearKinds.Length + (float)rng.NextDouble() * 0.3f;
                float radius = 7f + (float)rng.NextDouble() * 18f;
                var p = spawnPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                p.y = terrain.SampleHeight(p) - 0.05f;
                var node = new GameObject("Nó_" + nearKinds[i]);
                node.transform.SetParent(resources.transform, false);
                node.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                Ore(nearKinds[i], node, 0.9f, rng);
                node.AddComponent<ResourceNode>().EditorSetup($"node_start_{i}", items[nearKinds[i]], 1);
            }

            playerGo.AddComponent<FootstepAudio>();
            var survival = playerGo.AddComponent<SurvivalStats>();
            SetRef(survival, "respawnPoint", respawn);
            playerGo.AddComponent<PlayerCombat>();

            var interactor = playerGo.AddComponent<Interactor>();
            SetRef(interactor, "cam", cam);

            var placer = playerGo.AddComponent<BuildPlacer>();
            SetRef(placer, "cam", cam);
            SetRef(placer, "inventory", inv);
            SetRef(placer, "validMaterial", m.Emissive("GhostValid", new Color(0.2f, 0.9f, 0.4f)));
            SetRef(placer, "invalidMaterial", m.Emissive("GhostInvalid", new Color(0.9f, 0.2f, 0.2f)));

            var deconstructor = playerGo.AddComponent<Deconstructor>();
            SetRef(deconstructor, "cam", cam);
            SetRef(deconstructor, "inventory", inv);
            SetRef(deconstructor, "placer", placer);

            var save = systems.AddComponent<SaveSystem>();
            SetRef(save, "database", db);
            SetRef(save, "player", fpc);
            SetRef(save, "playerInventory", inv);
            SetRef(save, "playerSurvival", survival);

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UIDocument>();
            document.panelSettings = HudPanelSettings();
            var hud = hudGo.AddComponent<GameHud>();
            SetRef(hud, "database", db);
            SetRef(hud, "inventory", inv);
            SetRef(hud, "survival", survival);
            SetRef(hud, "interactor", interactor);
            SetRef(hud, "placer", placer);
            SetRef(hud, "deconstructor", deconstructor);
            SetRef(hud, "saveSystem", save);

            var feedbackAudio = systems.AddComponent<AudioSource>();
            feedbackAudio.playOnAwake = false;
            feedbackAudio.spatialBlend = 0f;
            var feedback = systems.AddComponent<StageFeedback>();
            SetRef(feedback, "database", db);
            SetRef(feedback, "audioSource", feedbackAudio);

            // Clima: nuvens volumétricas (no volume Atmosfera) + chuva que segue o jogador
            var rain = CreateRain(RainMaterial());
            var weatherGo = new GameObject("Clima");
            var rainAudio = weatherGo.AddComponent<AudioSource>();
            rainAudio.playOnAwake = false;
            rainAudio.spatialBlend = 0f;
            var weather = weatherGo.AddComponent<WeatherController>();
            SetRef(weather, "atmosphere", atmosphere);
            SetDouble(weather, "cloudTiStart", stages["bluesky"].requiredTi);
            SetDouble(weather, "cloudTiEnd", stages["rain"].requiredTi);
            SetRef(weather, "rainStage", stages["rain"]);
            SetRef(weather, "rain", rain);
            SetRef(weather, "rainAudio", rainAudio);
            SetRef(weather, "followTarget", playerGo.transform);
            SetRef(weather, "sun", sun);

            var dev = systems.AddComponent<Planetcraft.Dev.DevCommandLine>();
            SetRef(dev, "player", fpc);
            SetRef(dev, "weather", weather);
            SetRef(dev, "database", db);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Animal do Sketchfab com Animator (parado/andando); sem o modelo baixado, usa o herbívoro procedural.
        static GameObject CreateAnimal(Transform parent, System.Random r, string label, string id, float height, float speed,
            float wanderRadius, float moveAnimSpeed)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);
            var wander = root.AddComponent<AnimalWander>();
            if (id == "wolf")
            {
                var body = root.AddComponent<CapsuleCollider>();
                body.radius = 0.45f;
                body.height = 1.4f;
                body.center = new Vector3(0f, 0.7f, 0f);
                body.direction = 2;
                var health = root.AddComponent<Planetcraft.Combat.Health>();
                var hso = new SerializedObject(health);
                hso.FindProperty("max").floatValue = 70f;
                hso.FindProperty("team").enumValueIndex = (int)Planetcraft.Combat.Team.Hostile;
                hso.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<Planetcraft.Enemies.WolfAggro>();
            }
            var so = new SerializedObject(wander);
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("wanderRadius").floatValue = wanderRadius;

            var asset = SketchfabAnimals.Model(id);
            if (asset == null)
            {
                root.transform.localScale = Vector3.one * (0.85f + (float)r.NextDouble() * 0.35f);
                so.FindProperty("body").objectReferenceValue = FaceForward(Model("Fauna/grazer", root.transform)).transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                return root;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            model.transform.localPosition = Vector3.zero;
            SketchfabAnimals.FitToHeight(model, height * (0.9f + (float)r.NextDouble() * 0.2f));
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = SketchfabAnimals.Controller(id, moveAnimSpeed);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (root.TryGetComponent<Planetcraft.Enemies.WolfAggro>(out var aggro))
            {
                var aso = new SerializedObject(aggro);
                aso.FindProperty("animator").objectReferenceValue = animator;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }
            return root;
        }

        // Altura final da água (WaterReactor.maxHeight) mais uma margem de praia.
        const float VegetationMinHeight = 17f;

        static string Variant(System.Random rng, string id, int count) => $"{id}_{(char)('a' + rng.Next(count))}";

        static void AddVegetationLayer(Transform parent, string name, double tiStart, double tiEnd, int count, System.Random rng,
            Terrain terrain, Vector3 center, Func<Transform, System.Random, GameObject> make, float minRadius = 30f, float maxRadius = 235f,
            float maxSlope = 35f, float clustering = 0.5f, float footprint = 0f, bool isTree = false)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            // Ruído próprio da camada: `clustering` alto = manchas densas separadas por clareiras.
            float noiseOffset = (float)rng.NextDouble() * 1000f;
            for (int i = 0, tries = 0; i < count && tries < count * 40; tries++)
            {
                var pos = RandomPointInRing(rng, center, minRadius, maxRadius, terrain);
                if (pos.y < VegetationMinHeight) continue;
                var local = pos - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) > maxSlope) continue;
                float patch = Mathf.PerlinNoise(pos.x * 0.018f + noiseOffset, pos.z * 0.018f + noiseOffset);
                if (rng.NextDouble() > Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, (patch - 0.3f) / 0.4f), clustering)) continue;
                var go = make(layer.transform, rng);
                if (footprint > 0f)
                {
                    // Regra de distância: a área ocupada cresce com o tamanho da planta, e árvores nunca encostam as copas.
                    if (!VegetationSpacing.TryPlace(pos, VegetationSpacing.CrownRadius(go) * footprint, isTree)) { Object.DestroyImmediate(go); continue; }
                }
                go.transform.position = pos - Vector3.up * 0.05f;
                go.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
                i++;
            }

            var activator = layer.AddComponent<ProgressiveActivator>();
            SetRef(activator, "container", layer.transform);
            SetDouble(activator, "tiStart", tiStart);
            SetDouble(activator, "tiEnd", tiEnd);
            var so = new SerializedObject(activator);
            so.FindProperty("seed").intValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 RandomPointInRing(System.Random rng, Vector3 center, float minRadius, float maxRadius, Terrain terrain)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)rng.NextDouble()));
            var p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        // `tint` multiplica a cor da textura (diffuseRemapMax): as gramas escaneadas do Poly Haven são pardas demais
        // para um mundo que acabou de reverdecer.
        static TerrainLayer PolyHavenLayer(string id, float tile, float smoothness, Color? tint = null)
        {
            string path = $"{TerrainDir}/{id}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer != null)
            {
                layer.diffuseRemapMax = (Vector4)(tint ?? Color.white);
                EditorUtility.SetDirty(layer);
                return layer;
            }
            string dir = $"{PolyHavenDir}/Textures/{id}";
            layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_diff_2k.jpg"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_nor_gl_2k.jpg"),
                tileSize = new Vector2(tile, tile),
                smoothness = smoothness,
                metallic = 0f,
                diffuseRemapMax = (Vector4)(tint ?? Color.white),
            };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // Erosão hidráulica por gotas: cada gota escorre morro abaixo, arranca sedimento onde acelera e
        // deposita onde desacelera — cria ravinas, leques de sedimento e encostas naturais.
        static void Erode(float[,] h, float heightScale, int iterations, int seed)
        {
            int n = h.GetLength(0);
            var map = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                map[y * n + x] = h[y, x] * heightScale;

            const float inertia = 0.05f, capacityFactor = 4f, minCapacity = 0.01f, depositSpeed = 0.3f,
                erodeSpeed = 0.3f, evaporate = 0.015f, gravity = 4f;
            const int maxLife = 40, radius = 3;
            var rng = new System.Random(seed);

            // Pincel de erosão: pesos decrescentes com a distância.
            var brushOffsets = new List<(int dx, int dy, float w)>();
            float wSum = 0f;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                float w = 1f - dist / radius;
                brushOffsets.Add((dx, dy, w));
                wSum += w;
            }

            (float height, float gx, float gy) Sample(float px, float py)
            {
                int cx = (int)px, cy = (int)py;
                float fx = px - cx, fy = py - cy;
                int i = cy * n + cx;
                float nw = map[i], ne = map[i + 1], sw = map[i + n], se = map[i + n + 1];
                float gx = (ne - nw) * (1 - fy) + (se - sw) * fy;
                float gy = (sw - nw) * (1 - fx) + (se - ne) * fx;
                float hh = nw * (1 - fx) * (1 - fy) + ne * fx * (1 - fy) + sw * (1 - fx) * fy + se * fx * fy;
                return (hh, gx, gy);
            }

            for (int it = 0; it < iterations; it++)
            {
                float px = (float)rng.NextDouble() * (n - 2), py = (float)rng.NextDouble() * (n - 2);
                float dirX = 0f, dirY = 0f, speed = 1f, water = 1f, sediment = 0f;
                for (int life = 0; life < maxLife; life++)
                {
                    int nodeX = (int)px, nodeY = (int)py;
                    float cellX = px - nodeX, cellY = py - nodeY;
                    var (height, gx, gy) = Sample(px, py);
                    dirX = dirX * inertia - gx * (1 - inertia);
                    dirY = dirY * inertia - gy * (1 - inertia);
                    float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                    if (len < 1e-6f) break;
                    dirX /= len;
                    dirY /= len;
                    px += dirX;
                    py += dirY;
                    if (px < 0 || py < 0 || px >= n - 2 || py >= n - 2) break;

                    float deltaH = Sample(px, py).height - height;
                    float capacity = Mathf.Max(-deltaH * speed * water * capacityFactor, minCapacity);
                    int i = nodeY * n + nodeX;
                    if (sediment > capacity || deltaH > 0)
                    {
                        float amount = deltaH > 0 ? Mathf.Min(deltaH, sediment) : (sediment - capacity) * depositSpeed;
                        sediment -= amount;
                        map[i] += amount * (1 - cellX) * (1 - cellY);
                        map[i + 1] += amount * cellX * (1 - cellY);
                        map[i + n] += amount * (1 - cellX) * cellY;
                        map[i + n + 1] += amount * cellX * cellY;
                    }
                    else
                    {
                        float amount = Mathf.Min((capacity - sediment) * erodeSpeed, -deltaH);
                        foreach (var (dx, dy, w) in brushOffsets)
                        {
                            int bx = nodeX + dx, by = nodeY + dy;
                            if (bx < 0 || by < 0 || bx >= n || by >= n) continue;
                            int j = by * n + bx;
                            float take = Mathf.Min(map[j], amount * w / wSum);
                            map[j] -= take;
                            sediment += take;
                        }
                    }
                    speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + deltaH * gravity));
                    water *= 1 - evaporate;
                }
            }

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                h[y, x] = Mathf.Clamp01(map[y * n + x] / heightScale);
        }

        static Terrain CreateTerrain()
        {
            string dataPath = TerrainDir + "/EarthTerrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                // Camadas PBR (Poly Haven, CC0): 0 solo seco e pedregoso, 1 rocha (encostas), 2 grama (cresce com o Ti).
                var dead = PolyHavenLayer("dry_ground_rocks", 10f, 0.05f);
                var rockLayer = PolyHavenLayer("rocky_terrain_02", 12f, 0.1f);
                var grass = PolyHavenLayer("leafy_grass", 6f, 0.15f, new Color(0.6f, 1.1f, 0.42f));

                td = new TerrainData { heightmapResolution = HeightmapRes, alphamapResolution = 256 };
                td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
                var heights = new float[HeightmapRes, HeightmapRes];
                var mid = new Vector2(0.5f, 0.5f);
                for (int y = 0; y < HeightmapRes; y++)
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float u = x / (float)(HeightmapRes - 1), v = y / (float)(HeightmapRes - 1);
                    float noise = Fbm(u * 5f + 11.3f, v * 5f + 7.1f, 6) * 0.75f;
                    float ridges = 1f - Mathf.Abs(Fbm(u * 3f + 2.1f, v * 3f + 9.4f, 4) * 2f - 1f);
                    float d = Vector2.Distance(new Vector2(u, v), mid);
                    float basin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.32f, d));
                    float rim = Mathf.InverseLerp(0.36f, 0.5f, d);
                    // Fundo da bacia com relevo: a água sobe formando poças e ilhas, não uma lâmina rasa.
                    heights[y, x] = Mathf.Lerp(0.02f + noise * 0.1f, 0.3f + noise * 0.25f, basin)
                                    + rim * (0.25f + ridges * 0.2f);
                }
                Erode(heights, TerrainHeight, iterations: 140000, seed: 2026);
                td.SetHeights(0, 0, heights);
                td.terrainLayers = new[] { dead, rockLayer, grass };
                var deadOnly = new float[td.alphamapResolution, td.alphamapResolution, 3];
                for (int y = 0; y < td.alphamapResolution; y++)
                for (int x = 0; x < td.alphamapResolution; x++)
                    deadOnly[y, x, 0] = 1f;
                td.SetAlphamaps(0, 0, deadOnly);
                AssetDatabase.CreateAsset(td, dataPath);
            }

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terreno";
            var terrain = go.GetComponent<Terrain>();
            var terrainShader = Shader.Find("HDRP/TerrainLit");
            if (terrainShader != null)
            {
                string matPath = MatDir + "/Terrain.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(terrainShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                terrain.materialTemplate = mat;
            }
            return terrain;
        }

        static ParticleSystem CreateRain(Material material)
        {
            var go = new GameObject("Chuva");
            // A caixa emite ao longo do Z local; girando 90° em X, as gotas caem.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 22f;
            main.startSize = 0.012f;
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.62f, 0.66f, 0.72f, 0.16f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 70f, 1f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 0.6f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // HDRP/Unlit ignora a cor por vértice das partículas: a cor e a transparência ficam no material.
        static Material RainMaterial() => ParticleMaterial("Rain", new Color(0.72f, 0.76f, 0.82f, 0.07f));

        static Material StarMaterial() => ParticleMaterial("Stars", new Color(1f, 1f, 1f, 0.9f));

        static Material ParticleMaterial(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("HDRP/Unlit"));
            mat.SetColor("_UnlitColor", color);
            HDMaterial.SetSurfaceType(mat, true);
            HDMaterial.ValidateMaterial(mat);
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Utilitários ----------

        static T Asset<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetDouble(Object target, string field, double value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).doubleValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetStartingItems(InventoryComponent inv, ItemAmount[] items)
        {
            var so = new SerializedObject(inv);
            var arr = so.FindProperty("startingItems");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = items[i].item;
                e.FindPropertyRelative("amount").intValue = items[i].amount;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        sealed class MaterialLibrary
        {
            readonly Dictionary<string, Material> cache = new();

            public Material Lit(string name, Color color, float smoothness = 0.2f) => Get(name, color, smoothness, false);
            public Material Emissive(string name, Color color) => Get(name, color, 0.3f, true);

            Material Get(string name, Color color, float smoothness, bool emissive)
            {
                if (cache.TryGetValue(name, out var cached)) return cached;
                string path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("HDRP/Lit"));
                    ModelPostprocessor.ConfigureLit(mat, color, smoothness);
                    if (emissive) ModelPostprocessor.MakeEmissive(mat, color, ModelPostprocessor.GlowNits);
                    HDMaterial.ValidateMaterial(mat);
                    AssetDatabase.CreateAsset(mat, path);
                }
                cache[name] = mat;
                return mat;
            }
        }
    }
}
