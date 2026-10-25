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

        const float TerrainSize = 500f;
        const float TerrainHeight = 60f;
        const int HeightmapRes = 513;
        const string PolyHavenDir = "Assets/ThirdParty/PolyHaven";

        [MenuItem("Planetcraft/Gerar Protótipo")]
        public static void Build()
        {
            HdrpSetup.Configure();
            PolyHavenMaterials.Setup();
            foreach (var dir in new[] { DataDir + "/Stages", DataDir + "/Items", DataDir + "/Recipes", DataDir + "/Buildables", DataDir + "/Logs", PrefabDir, MatDir, TerrainDir, SceneDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var mats = new MaterialLibrary();
            var stages = CreateStages(out var stageDb);
            var items = CreateItems();
            var recipes = CreateRecipes(items);
            var buildables = CreateBuildables(items, mats);
            ConfigureShipyardPrefab(buildables.Find(b => b.id == "shipyard"), CreateShipModules(items), stages["genesis"]);

            var db = Asset<GameDatabase>(DataDir + "/GameDatabase.asset", d => { });
            db.items = new List<ItemDefinition>(items.Values);
            db.recipes = recipes;
            db.buildables = buildables;
            db.logs = CreateLogs();
            EditorUtility.SetDirty(db);

            BuildScene(stageDb, stages, db, items, mats);
            BuildTitleScene(mats);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Planetcraft] Protótipo gerado: " + ScenePath);
        }

        // ---------- Dados ----------

        static Dictionary<string, TerraformStage> CreateStages(out StageDatabase db)
        {
            var data = new (string id, string name, double ti, string desc)[]
            {
                ("barren", "Rocha Morta", 0, "Um planeta sem atmosfera."),
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
                ("genesis", "Gênese", 15_000_000, "A atmosfera está estável. A nave-mãe pode decolar do estaleiro."),
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
                string blueprint = null, bool unique = false)
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
                def.blueprintId = blueprint; // projeto exigido faz parte do design: sempre sincroniza
                def.unique = unique;
                // Prefab é derivado do modelo: sempre regenera para pegar mudanças do gen_models.py.
                def.prefab = SavePrefab(def, isMachine, pressurized, model, scale, storage);
                EditorUtility.SetDirty(def);
                list.Add(def);
            }

            var none = Array.Empty<TerraformRate>();
            Add("base_module", "Módulo de Base", "Abrigo pressurizado: recarrega oxigênio.", 0, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Machines/base_module", pressurized: true);
            Add("storage_chest", "Baú", "Guarda até 30 pilhas de itens. [E] para abrir.", 0, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, "Machines/storage_chest", storage: true);
            Add("wind_turbine", "Gerador Eólico", "+1.2 kW", 0, 1.2f, 0,
                new[] { C(it["iron"], 1), C(it["silicon"], 1) }, none, true, "Machines/wind_turbine");
            Add("heater_t1", "Aquecedor", "Calor +1.2/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 1), C(it["titanium"], 1) }, Rates(TerraformParameter.Heat, 1.2), true, "Machines/heater");
            Add("drill_t1", "Perfuratriz", "Pressão +1/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 2) }, Rates(TerraformParameter.Pressure, 1), true, "Machines/drill");
            Add("vegetube", "Vegetubo", "Oxigênio +0.8/s. Consome 0.5 kW.", 50, 0, 0.5f,
                new[] { C(it["ice"], 1), C(it["silicon"], 1) }, Rates(TerraformParameter.Oxygen, 0.8), true, "Machines/vegetube");
            Add("solar_panel", "Painel Solar", "+6.5 kW", 300, 6.5f, 0,
                new[] { C(it["silicon"], 2), C(it["iron"], 1) }, none, true, "Machines/solar_panel");
            Add("heater_t2", "Aquecedor II", "Calor +12/s. Consome 4 kW.", 1_000, 0, 4f,
                new[] { C(it["titanium"], 2), C(it["alloy"], 1) }, Rates(TerraformParameter.Heat, 12), true, "Machines/heater", 1.5f);
            Add("drill_t2", "Perfuratriz II", "Pressão +10/s. Consome 4 kW.", 2_500, 0, 4f,
                new[] { C(it["iron"], 3), C(it["alloy"], 1) }, Rates(TerraformParameter.Pressure, 10), true, "Machines/drill", 1.5f);
            Add("algae_farm", "Fazenda de Algas", "Biomassa +25/s, Oxigênio +5/s. Consome 2 kW.", 20_000, 0, 2f,
                new[] { C(it["ice"], 2), C(it["magnesium"], 1) },
                new[] { new TerraformRate(TerraformParameter.Biomass, 25), new TerraformRate(TerraformParameter.Oxygen, 5) }, true, "Machines/algae_farm");
            Add("shipyard", "Estaleiro", "Plataforma para montar a nave-mãe e deixar o planeta. Só um por planeta.", 2_500_000, 0, 0,
                new[] { C(it["alloy"], 6), C(it["titanium"], 4), C(it["iron"], 8) }, none, false, "Machines/shipyard", unique: true);
            Add("heater_t3", "Aquecedor III", "Calor +400/s. Consome 15 kW.", 60_000, 0, 15f,
                new[] { C(it["titanium"], 4), C(it["alloy"], 2) }, Rates(TerraformParameter.Heat, 400), true, "Machines/heater", 2.2f);

            // Tier 4: exigem microchip de destroço (blueprint) além do Ti.
            Add("vegetube_t2", "Vegetubo II", "Oxigênio +60/s. Consome 6 kW.", 60_000, 0, 6f,
                new[] { C(it["silicon"], 3), C(it["alloy"], 1), C(it["ice"], 2) }, Rates(TerraformParameter.Oxygen, 60), true,
                "Machines/vegetube", 1.6f, blueprint: "bp_vegetube2");
            Add("nuclear_reactor", "Reator Nuclear", "+80 kW. Precisa de barras de combustível para construir.", 50_000, 80f, 0,
                new[] { C(it["alloy"], 3), C(it["titanium"], 2), C(it["fuel_rod"], 1) }, none, true,
                "Machines/nuclear_reactor", blueprint: "bp_nuclear");
            Add("heater_t4", "Aquecedor IV", "Calor +6000/s. Consome 40 kW.", 300_000, 0, 40f,
                new[] { C(it["titanium"], 5), C(it["alloy"], 3), C(it["fuel_rod"], 1) }, Rates(TerraformParameter.Heat, 6000), true,
                "Machines/heater", 3f, blueprint: "bp_heater4");
            Add("drill_t3", "Perfuratriz III", "Pressão +3000/s. Consome 35 kW.", 300_000, 0, 35f,
                new[] { C(it["iron"], 6), C(it["alloy"], 3), C(it["fuel_rod"], 1) }, Rates(TerraformParameter.Pressure, 3000), true,
                "Machines/drill", 2.2f, blueprint: "bp_drill3");
            return list;
        }

        // Etapas da nave-mãe, em ordem de montagem (a tabela é a fonte da verdade).
        static ShipModule[] CreateShipModules(Dictionary<string, ItemDefinition> it)
        {
            Directory.CreateDirectory(DataDir + "/ShipModules");
            var data = new (string id, string name, string desc, double ti, string obj, ItemAmount[] cost)[]
            {
                ("ship_hull", "Casco", "Estrutura em cunha e blindagem.", 2_500_000, "Hull",
                    new[] { C(it["alloy"], 8), C(it["iron"], 10), C(it["titanium"], 6) }),
                ("ship_engines", "Motores", "Propulsores principais e de manobra.", 4_000_000, "Engines",
                    new[] { C(it["alloy"], 6), C(it["titanium"], 4), C(it["fuel_rod"], 3) }),
                ("ship_superstructure", "Superestrutura", "Conveses, hangares e alojamentos.", 6_000_000, "Superstructure",
                    new[] { C(it["iron"], 10), C(it["silicon"], 8), C(it["alloy"], 4) }),
                ("ship_bridge", "Ponte de comando", "Torre de comando e domos de sensores.", 8_000_000, "Bridge",
                    new[] { C(it["silicon"], 8), C(it["magnesium"], 6), C(it["alloy"], 4) }),
                ("ship_hyperdrive", "Reator de salto", "Núcleo de salto para deixar o sistema.", 12_000_000, "Hyperdrive",
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
                ("log_01", "Diário — Eng. Ilsa Moreau",
                 "Dia 3 depois da queda. A Aurora se partiu na órbita e as cápsulas caíram espalhadas pelo vale.\n\n" +
                 "O ar aqui não perdoa: cada saída é uma conta de oxigênio. Os vegetubos do compartimento de carga ainda " +
                 "funcionam, e eu gravei neste chip a versão aprimorada que estávamos testando. Se alguém encontrar isto, " +
                 "plante. Plante muito."),
                ("log_02", "Relatório técnico — Reator KR-4",
                 "O reator compacto KR-4 sobreviveu ao impacto. Ele roda com barras de urânio refinado misturado com liga " +
                 "metálica.\n\nOs veios verdes que brilham perto das crateras são urânio exposto pela queda das cápsulas. " +
                 "Cuidado ao coletar. O projeto completo do reator está no chip deste módulo."),
                ("log_03", "Comandante Raul Tavares",
                 "Ninguém nos mandou aqui para sobreviver. Mandaram para preparar o planeta.\n\nA frota espera além do " +
                 "cinturão, com quarenta mil colonos em criossono, mas a Aurora era a única nave capaz de voltar até eles. " +
                 "Se alguém ler isto: monte um estaleiro e construa outra nave-mãe com o que sobrou. Decole quando a " +
                 "atmosfera estiver estável e leve a notícia de que este planeta respira."),
                ("log_04", "IA de bordo — MAIA",
                 "Registro automático. Detecto atividade de terraformação na superfície, de origem desconhecida.\n\n" +
                 "Atualizando projeções: com perfuratrizes de alta pressão o índice cresce em ordens de grandeza. " +
                 "Projeto anexado ao chip. Esperança: recalculando... valor positivo."),
                ("log_05", "Última transmissão — Dra. Nadia Okoro",
                 "Liberei o banco genético antes de perdermos energia. Sementes, insetos, animais: tudo em estase no fundo " +
                 "do vale.\n\nQuando o planeta respirar sozinho, eles acordam. Você vai saber que deu certo quando ouvir " +
                 "algo correndo entre as árvores."),
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

        static GameObject SavePrefab(BuildableDefinition def, bool isMachine, bool pressurized, string model, float scale, bool storage)
        {
            string path = $"{PrefabDir}/{def.id}.prefab";
            var root = new GameObject(def.displayName);
            var visual = Model(model, root.transform);
            visual.transform.localScale = Vector3.one * scale;
            var placed = root.AddComponent<PlacedObject>();
            placed.EditorSetDefinition(def);

            var rotor = visual.transform.Find("Rotor");
            if (rotor != null) rotor.gameObject.AddComponent<Spinner>();

            if (isMachine)
            {
                var machine = root.AddComponent<Machine>();
                var indicator = visual.transform.Find("Indicator");
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

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Tela de título: planeta enferrujado, nave-mãe em órbita, estrelas e o menu em UI Toolkit.
        static void BuildTitleScene(MaterialLibrary m)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 100000f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(20f, 120f, 0f);

            var space = new GameObject("Espaço").AddComponent<Volume>();
            space.isGlobal = true;
            space.sharedProfile = HdrpSetup.TitleProfile();

            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planeta";
            Object.DestroyImmediate(planet.GetComponent<Collider>());
            planet.transform.position = new Vector3(0f, -130f, 0f);
            planet.transform.localScale = Vector3.one * 240f;
            planet.GetComponent<Renderer>().sharedMaterial = m.Lit("TitlePlanet", new Color(0.55f, 0.3f, 0.2f), 0.15f);

            var shipRoot = new GameObject("Nave");
            shipRoot.transform.position = new Vector3(0f, 12f, 0f);
            Model("Sky/mothership", shipRoot.transform).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            var stars = new GameObject("Estrelas").AddComponent<ParticleSystem>();
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stars.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = stars.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1500) });
            var shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 600f;
            shape.radiusThickness = 0f;
            stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = StarMaterial();

            var camGo = new GameObject("Câmera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            // Fundo preto: as estrelas são partículas.
            var hdCam = camGo.AddComponent<HDAdditionalCameraData>();
            hdCam.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hdCam.backgroundColorHDR = Color.black;
            camGo.AddComponent<AudioListener>();
            var orbit = camGo.AddComponent<Orbiter>();
            SetRef(orbit, "target", shipRoot.transform);
            var orbitSo = new SerializedObject(orbit);
            orbitSo.FindProperty("lookOffset").vector3Value = new Vector3(0f, -6f, 0f);
            orbitSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = new GameObject("Menu");
            ui.AddComponent<UIDocument>().panelSettings = HudPanelSettings();
            ui.AddComponent<TitleMenu>();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        // PanelSettings + tema padrão de runtime para o HUD em UI Toolkit.
        static PanelSettings HudPanelSettings()
        {
            string dir = Root + "/UI";
            Directory.CreateDirectory(dir);
            string themePath = dir + "/HudTheme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
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
            var vegetation = new GameObject("Vegetação");
            // Modelos fotoescaneados do Poly Haven (CC0), otimizados para jogo com LOD.
            // Densidade de paisagem real: samambaias e arbustos em tufos, árvores formando bosques. Nada nasce
            // abaixo do nível final da água nem em encostas de rocha.
            AddVegetationLayer(vegetation.transform, "Musgo", stages["moss"].requiredTi, stages["flora"].requiredTi, 2600, rng, terrain, center, (p, r) =>
                Scaled(PolyHaven(Variant(r, "fern_02", 4), p), 0.8f + (float)r.NextDouble() * 0.7f), maxSlope: 32f, clustering: 0.5f);
            AddVegetationLayer(vegetation.transform, "Flora", stages["flora"].requiredTi, stages["forest"].requiredTi, 1500, rng, terrain, center, (p, r) =>
                Scaled(PolyHaven(Variant(r, "shrub_02", 4), p), 0.7f + (float)r.NextDouble() * 0.7f), maxSlope: 30f, clustering: 0.6f);
            AddVegetationLayer(vegetation.transform, "Florestas", stages["forest"].requiredTi, stages["fauna"].requiredTi * 2, 900, rng, terrain, center, (p, r) =>
            {
                var tree = Scaled(PolyHaven("island_tree_01", p), 0.8f + (float)r.NextDouble() * 0.6f);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = 0.35f;
                trunk.height = 4f;
                trunk.center = new Vector3(0f, 2f, 0f);
                return tree;
            }, minRadius: 40f, maxRadius: 235f, maxSlope: 26f, clustering: 0.8f);

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

            // Fauna: herbívoros e saltadores que passeiam fora d'água
            var fauna = new GameObject("Fauna");
            AddVegetationLayer(fauna.transform, "Herbívoros", stages["fauna"].requiredTi, stages["fauna"].requiredTi * 5, 45, rng, terrain, center,
                (p, r) => CreateGrazer(p, r, m), minRadius: 150f, maxRadius: 220f);
            AddVegetationLayer(fauna.transform, "Saltadores", stages["fauna"].requiredTi, stages["fauna"].requiredTi * 5, 40, rng, terrain, center,
                (p, r) => CreateHopper(p, r, m), minRadius: 140f, maxRadius: 220f);

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
                Scaled(Model("Ore/ore_" + kind.item, node.transform), 0.8f + (float)rng.NextDouble() * 0.4f);
                node.AddComponent<ResourceNode>().EditorSetup($"node_{i}", items[kind.item], 1);
            }

            // Destroços da expedição Aurora: farol, registro, microchip e urânio em volta.
            var wrecks = new GameObject("Destroços");
            var sites = new (float angle, float radius, string blueprint, string label)[]
            {
                (22f, 186f, "bp_vegetube2", "Vegetubo II"),
                (95f, 196f, "bp_nuclear", "Reator Nuclear"),
                (165f, 184f, "bp_heater4", "Aquecedor IV"),
                (238f, 192f, "bp_drill3", "Perfuratriz III"),
                (308f, 200f, null, null),
            };
            var beaconMat = m.Emissive("Beacon", new Color(0.4f, 0.9f, 1f));
            for (int s = 0; s < sites.Length; s++)
            {
                float a = sites[s].angle * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * sites[s].radius;
                pos.y = terrain.SampleHeight(pos);
                var site = new GameObject($"Destroço_{s + 1}");
                site.transform.SetParent(wrecks.transform, false);
                site.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                Model("Props/wreck_pod", site.transform);

                var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "Farol";
                Object.DestroyImmediate(beacon.GetComponent<Collider>());
                beacon.transform.SetParent(site.transform, false);
                beacon.transform.localPosition = new Vector3(0f, 60f, 0f);
                beacon.transform.localScale = new Vector3(0.35f, 60f, 0.35f);
                var beaconRenderer = beacon.GetComponent<Renderer>();
                beaconRenderer.sharedMaterial = beaconMat;
                beaconRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var terminal = Model("Props/log_terminal", site.transform);
                terminal.transform.localPosition = new Vector3(3.5f, 0f, -2.5f);
                terminal.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
                SnapToGround(terminal.transform, terrain);
                terminal.AddComponent<LogTerminal>().EditorSetup(db.logs[s]);

                if (sites[s].blueprint != null)
                {
                    var chip = new GameObject("Microchip");
                    chip.transform.SetParent(site.transform, false);
                    chip.transform.localPosition = new Vector3(-3f, 0f, 2f);
                    SnapToGround(chip.transform, terrain);
                    Model("Props/chip_crate", chip.transform);
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
                    Scaled(Model("Ore/ore_uranium", node.transform), 0.9f);
                    node.AddComponent<ResourceNode>().EditorSetup($"node_wreck_{s}_{i}", items["uranium"], 1);
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
                Scaled(Model("Ore/ore_" + nearKinds[i], node.transform), 0.9f);
                node.AddComponent<ResourceNode>().EditorSetup($"node_start_{i}", items[nearKinds[i]], 1);
            }

            playerGo.AddComponent<FootstepAudio>();
            var survival = playerGo.AddComponent<SurvivalStats>();
            SetRef(survival, "respawnPoint", respawn);

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

        static GameObject CreateGrazer(Transform parent, System.Random r, MaterialLibrary m)
        {
            var root = new GameObject("Herbívoro");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * (0.85f + (float)r.NextDouble() * 0.35f);
            var body = FaceForward(Model("Fauna/grazer", root.transform)).transform;

            var wander = root.AddComponent<AnimalWander>();
            var so = new SerializedObject(wander);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("speed").floatValue = 1.8f;
            so.FindProperty("wanderRadius").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static GameObject CreateHopper(Transform parent, System.Random r, MaterialLibrary m)
        {
            var root = new GameObject("Saltador");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * (1.1f + (float)r.NextDouble() * 0.3f);
            var body = FaceForward(Model("Fauna/hopper", root.transform)).transform;

            var wander = root.AddComponent<AnimalWander>();
            var so = new SerializedObject(wander);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("hops").boolValue = true;
            so.FindProperty("speed").floatValue = 3.2f;
            so.FindProperty("bobHeight").floatValue = 0.1f;
            so.FindProperty("stepFrequency").floatValue = 2.5f;
            so.FindProperty("wanderRadius").floatValue = 15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Altura final da água (WaterReactor.maxHeight) mais uma margem de praia.
        const float VegetationMinHeight = 17f;

        static string Variant(System.Random rng, string id, int count) => $"{id}_{(char)('a' + rng.Next(count))}";

        static void AddVegetationLayer(Transform parent, string name, double tiStart, double tiEnd, int count, System.Random rng,
            Terrain terrain, Vector3 center, Func<Transform, System.Random, GameObject> make, float minRadius = 30f, float maxRadius = 235f,
            float maxSlope = 35f, float clustering = 0.5f)
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
                var grass = PolyHavenLayer("leafy_grass", 6f, 0.15f, new Color(0.8f, 1.08f, 0.6f));

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
