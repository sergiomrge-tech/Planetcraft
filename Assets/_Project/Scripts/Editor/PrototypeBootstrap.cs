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

        const float TerrainSize = 500f;
        const float TerrainHeight = 60f;
        const int HeightmapRes = 257;

        [MenuItem("Planetcraft/Gerar Protótipo")]
        public static void Build()
        {
            foreach (var dir in new[] { DataDir + "/Stages", DataDir + "/Items", DataDir + "/Recipes", DataDir + "/Buildables", PrefabDir, MatDir, TerrainDir, SceneDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var mats = new MaterialLibrary();
            var stages = CreateStages(out var stageDb);
            var items = CreateItems();
            var recipes = CreateRecipes(items);
            var buildables = CreateBuildables(items, mats);

            var db = Asset<GameDatabase>(DataDir + "/GameDatabase.asset", d => { });
            db.items = new List<ItemDefinition>(items.Values);
            db.recipes = recipes;
            db.buildables = buildables;
            EditorUtility.SetDirty(db);

            BuildScene(stageDb, stages, db, items, mats);
            AssetDatabase.SaveAssets();
            Debug.Log("[Planetcraft] Protótipo gerado: " + ScenePath);
        }

        // ---------- Dados ----------

        static Dictionary<string, TerraformStage> CreateStages(out StageDatabase db)
        {
            var data = new (string id, string name, double ti, string desc)[]
            {
                ("barren", "Rocha Morta", 0, "Um planeta sem atmosfera."),
                ("haze", "Névoa", 200, "Os primeiros gases engrossam o ar."),
                ("bluesky", "Céu Azul", 2_000, "A luz começa a se espalhar no céu."),
                ("clouds", "Nuvens", 20_000, "Vapor condensa em nuvens."),
                ("rain", "Chuva", 100_000, "A primeira chuva cai."),
                ("water", "Água Líquida", 500_000, "A água fica líquida na superfície."),
                ("lakes", "Lagos", 3_000_000, "A bacia começa a encher."),
                ("moss", "Musgo", 10_000_000, "Musgo cobre as rochas úmidas."),
                ("flora", "Flora", 50_000_000, "Plantas se espalham pelo vale."),
                ("forest", "Florestas", 200_000_000, "Árvores crescem sozinhas."),
            };

            var result = new Dictionary<string, TerraformStage>();
            foreach (var s in data)
            {
                result[s.id] = Asset<TerraformStage>($"{DataDir}/Stages/{s.id}.asset", a =>
                {
                    a.displayName = s.name;
                    a.requiredTi = s.ti;
                    a.description = s.desc;
                });
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
            RecipeDefinition R(string id, string output, double ti, params ItemAmount[] ingredients) =>
                Asset<RecipeDefinition>($"{DataDir}/Recipes/{id}.asset", a =>
                {
                    a.id = id;
                    a.output = it[output];
                    a.outputAmount = 1;
                    a.requiredTi = ti;
                    a.ingredients = ingredients;
                });

            return new List<RecipeDefinition>
            {
                R("o2_capsule", "o2_capsule", 0, C(it["ice"], 1), C(it["silicon"], 1)),
                R("water_bottle", "water_bottle", 0, C(it["ice"], 1)),
                R("nutrient_bar", "nutrient_bar", 0, C(it["ice"], 1), C(it["magnesium"], 1)),
                R("alloy", "alloy", 500, C(it["iron"], 1), C(it["titanium"], 1), C(it["magnesium"], 1)),
            };
        }

        static List<BuildableDefinition> CreateBuildables(Dictionary<string, ItemDefinition> it, MaterialLibrary m)
        {
            var list = new List<BuildableDefinition>();

            void Add(string id, string name, string desc, double ti, float gen, float demand, ItemAmount[] cost,
                TerraformRate[] rates, bool isMachine, Action<Transform, MaterialLibrary> shape, bool pressurized = false)
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
                if (def.prefab == null)
                {
                    def.prefab = SavePrefab(def, isMachine, pressurized, t => shape(t, m), m);
                    EditorUtility.SetDirty(def);
                }
                list.Add(def);
            }

            var none = Array.Empty<TerraformRate>();
            Add("base_module", "Módulo de Base", "Abrigo pressurizado: recarrega oxigênio.", 0, 0, 0,
                new[] { C(it["iron"], 2) }, none, false, ShapeBaseModule, pressurized: true);
            Add("wind_turbine", "Gerador Eólico", "+1.2 kW", 0, 1.2f, 0,
                new[] { C(it["iron"], 1), C(it["silicon"], 1) }, none, true, ShapeWindTurbine);
            Add("heater_t1", "Aquecedor", "Calor +0.5/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 1), C(it["titanium"], 1) }, Rates(TerraformParameter.Heat, 0.5), true, (t, mm) => ShapeHeater(t, mm, 1f));
            Add("drill_t1", "Perfuratriz", "Pressão +0.4/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 2) }, Rates(TerraformParameter.Pressure, 0.4), true, (t, mm) => ShapeDrill(t, mm, 1f));
            Add("vegetube", "Vegetubo", "Oxigênio +0.3/s. Consome 0.5 kW.", 50, 0, 0.5f,
                new[] { C(it["ice"], 1), C(it["silicon"], 1) }, Rates(TerraformParameter.Oxygen, 0.3), true, ShapeVegetube);
            Add("solar_panel", "Painel Solar", "+6.5 kW", 500, 6.5f, 0,
                new[] { C(it["silicon"], 2), C(it["iron"], 1) }, none, true, ShapeSolar);
            Add("heater_t2", "Aquecedor II", "Calor +12/s. Consome 4 kW.", 2_000, 0, 4f,
                new[] { C(it["titanium"], 2), C(it["alloy"], 1) }, Rates(TerraformParameter.Heat, 12), true, (t, mm) => ShapeHeater(t, mm, 1.6f));
            Add("drill_t2", "Perfuratriz II", "Pressão +10/s. Consome 4 kW.", 5_000, 0, 4f,
                new[] { C(it["iron"], 3), C(it["alloy"], 1) }, Rates(TerraformParameter.Pressure, 10), true, (t, mm) => ShapeDrill(t, mm, 1.6f));
            Add("algae_farm", "Fazenda de Algas", "Biomassa +25/s, Oxigênio +5/s. Consome 2 kW.", 100_000, 0, 2f,
                new[] { C(it["ice"], 2), C(it["magnesium"], 1) },
                new[] { new TerraformRate(TerraformParameter.Biomass, 25), new TerraformRate(TerraformParameter.Oxygen, 5) }, true, ShapeAlgae);
            Add("heater_t3", "Aquecedor III", "Calor +400/s. Consome 15 kW.", 500_000, 0, 15f,
                new[] { C(it["titanium"], 4), C(it["alloy"], 2) }, Rates(TerraformParameter.Heat, 400), true, (t, mm) => ShapeHeater(t, mm, 2.4f));
            return list;
        }

        static TerraformRate[] Rates(TerraformParameter p, double v) => new[] { new TerraformRate(p, v) };
        static ItemAmount C(ItemDefinition item, int amount) => new ItemAmount(item, amount);

        // ---------- Prefabs (primitivas; trocar por modelos depois) ----------

        static GameObject SavePrefab(BuildableDefinition def, bool isMachine, bool pressurized, Action<Transform> build, MaterialLibrary m)
        {
            string path = $"{PrefabDir}/{def.id}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(def.displayName);
            build(root.transform);
            var placed = root.AddComponent<PlacedObject>();
            placed.EditorSetDefinition(def);

            if (isMachine)
            {
                var machine = root.AddComponent<Machine>();
                if (def.powerDemand > 0f)
                {
                    var indicator = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.9f), Vector3.one * 0.18f, m.Emissive("Indicator", new Color(0.3f, 1f, 0.4f)), false);
                    indicator.name = "RunningIndicator";
                    SetRef(machine, "runningIndicator", indicator);
                }
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

        static void ShapeBaseModule(Transform t, MaterialLibrary m)
        {
            var wall = m.Lit("Module", new Color(0.85f, 0.85f, 0.8f));
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(4f, 0.2f, 4f), wall);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 3f, 0f), new Vector3(4f, 0.2f, 4f), wall);
            foreach (var x in new[] { -1.9f, 1.9f })
            foreach (var z in new[] { -1.9f, 1.9f })
                Part(t, PrimitiveType.Cube, new Vector3(x, 1.5f, z), new Vector3(0.2f, 3f, 0.2f), wall);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.5f, 1.95f), new Vector3(3.6f, 2.8f, 0.1f), m.Lit("Glass", new Color(0.5f, 0.75f, 0.9f), 0.9f));
        }

        static void ShapeWindTurbine(Transform t, MaterialLibrary m)
        {
            var metal = m.Lit("Metal", new Color(0.7f, 0.72f, 0.75f), 0.5f);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 0f), new Vector3(0.3f, 2.5f, 0.3f), metal);
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0f, 5f, 0.25f);
            Part(rotor, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.5f, metal);
            for (int i = 0; i < 3; i++)
            {
                var blade = Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(0.25f, 2.2f, 0.05f), metal, false);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, 1.1f, 0f);
            }
            rotor.gameObject.AddComponent<Spinner>();
        }

        static void ShapeHeater(Transform t, MaterialLibrary m, float scale)
        {
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f) * scale, new Vector3(1.5f, 1f, 1.5f) * scale, m.Lit("Dark", new Color(0.25f, 0.25f, 0.28f)));
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.15f, 0f) * scale, new Vector3(0.9f, 0.15f, 0.9f) * scale, m.Emissive("Heat", new Color(1f, 0.45f, 0.1f)));
        }

        static void ShapeDrill(Transform t, MaterialLibrary m, float scale)
        {
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f) * scale, new Vector3(2f, 0.8f, 2f) * scale, m.Lit("Yellow", new Color(0.9f, 0.7f, 0.15f)));
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.8f, 0f) * scale, new Vector3(0.45f, 1.2f, 0.45f) * scale, m.Lit("Metal", new Color(0.7f, 0.72f, 0.75f), 0.5f));
        }

        static void ShapeVegetube(Transform t, MaterialLibrary m)
        {
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(1f, 0.1f, 1f), m.Lit("Dark", new Color(0.25f, 0.25f, 0.28f)));
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.8f, 0.9f, 0.8f), m.Lit("Plant", new Color(0.3f, 0.7f, 0.3f)));
        }

        static void ShapeSolar(Transform t, MaterialLibrary m)
        {
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.15f, 0.6f, 0.15f), m.Lit("Metal", new Color(0.7f, 0.72f, 0.75f), 0.5f));
            var panel = Part(t, PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(2f, 0.08f, 1.2f), m.Lit("SolarCell", new Color(0.1f, 0.2f, 0.5f), 0.8f));
            panel.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
        }

        static void ShapeAlgae(Transform t, MaterialLibrary m)
        {
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(3f, 0.6f, 2f), m.Lit("Module", new Color(0.85f, 0.85f, 0.8f)));
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f), new Vector3(2.7f, 0.05f, 1.7f), m.Lit("Algae", new Color(0.15f, 0.5f, 0.25f), 0.7f));
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

            // Luz e céu
            var sunGo = new GameObject("Sol");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.sun = sun;

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Sky.mat");
            if (skyMat == null)
            {
                skyMat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(skyMat, MatDir + "/Sky.mat");
            }
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            // Terreno com uma bacia central que vira lago quando a água sobe
            var terrain = CreateTerrain();
            var center = new Vector3(TerrainSize / 2f, 0f, TerrainSize / 2f);

            // Água
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Água";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = center + Vector3.up * -2f;
            water.transform.localScale = new Vector3(TerrainSize / 10f, 1f, TerrainSize / 10f);
            water.GetComponent<Renderer>().sharedMaterial = m.Lit("Water", new Color(0.12f, 0.35f, 0.5f), 0.95f);
            var waterReactor = water.AddComponent<WaterReactor>();
            SetDouble(waterReactor, "tiStart", stages["water"].requiredTi);
            SetDouble(waterReactor, "tiEnd", stages["forest"].requiredTi);

            // Sistemas
            var systems = new GameObject("Sistemas");
            var tm = systems.AddComponent<TerraformManager>();
            SetRef(tm, "stages", stageDb);

            var sky = new GameObject("Céu").AddComponent<SkyReactor>();
            SetRef(sky, "sun", sun);
            SetDouble(sky, "tiStart", stages["haze"].requiredTi / 4);
            SetDouble(sky, "tiEnd", stages["rain"].requiredTi);

            // Vegetação por estágio
            var rng = new System.Random(4242);
            var vegetation = new GameObject("Vegetação");
            AddVegetationLayer(vegetation.transform, "Musgo", stages["moss"], 350, rng, terrain, center, (p, r) =>
                Part(p, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.6f, 0.25f, 1.6f) * (0.6f + (float)r.NextDouble()), m.Lit("Moss", new Color(0.25f, 0.4f, 0.15f)), false));
            AddVegetationLayer(vegetation.transform, "Flora", stages["flora"], 200, rng, terrain, center, (p, r) =>
                Part(p, PrimitiveType.Sphere, Vector3.up * 0.4f, Vector3.one * (0.6f + (float)r.NextDouble() * 0.6f), m.Lit("Bush", new Color(0.3f, 0.6f, 0.2f)), false));
            AddVegetationLayer(vegetation.transform, "Florestas", stages["forest"], 140, rng, terrain, center, (p, r) =>
            {
                float s = 0.8f + (float)r.NextDouble() * 0.8f;
                var tree = new GameObject("Árvore");
                tree.transform.SetParent(p, false);
                Part(tree.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f) * s, new Vector3(0.3f, 1.5f, 0.3f) * s, m.Lit("Bark", new Color(0.35f, 0.22f, 0.12f)));
                Part(tree.transform, PrimitiveType.Sphere, new Vector3(0f, 3.6f, 0f) * s, Vector3.one * 2.4f * s, m.Lit("Leaves", new Color(0.18f, 0.5f, 0.2f)), false);
                return tree;
            });

            // Recursos
            var resources = new GameObject("Recursos");
            var nodeKinds = new (string item, float weight, PrimitiveType shape, Color color)[]
            {
                ("iron", 0.3f, PrimitiveType.Cube, new Color(0.5f, 0.3f, 0.2f)),
                ("silicon", 0.25f, PrimitiveType.Sphere, new Color(0.45f, 0.5f, 0.6f)),
                ("ice", 0.2f, PrimitiveType.Cube, new Color(0.75f, 0.9f, 1f)),
                ("magnesium", 0.15f, PrimitiveType.Sphere, new Color(0.85f, 0.82f, 0.75f)),
                ("titanium", 0.1f, PrimitiveType.Cylinder, new Color(0.95f, 0.95f, 0.95f)),
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
                var node = Part(resources.transform, kind.shape, pos + Vector3.up * 0.25f, Vector3.one * 0.6f, m.Lit("Ore_" + kind.item, kind.color, 0.3f));
                node.name = "Nó_" + kind.item;
                node.transform.rotation = Quaternion.Euler(rng.Next(0, 360), rng.Next(0, 360), rng.Next(0, 360));
                node.AddComponent<ResourceNode>().EditorSetup($"node_{i}", items[kind.item], 1);
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

            var inv = playerGo.AddComponent<InventoryComponent>();
            SetStartingItems(inv, new[]
            {
                C(items["iron"], 4), C(items["silicon"], 2),
                C(items["o2_capsule"], 2), C(items["water_bottle"], 1), C(items["nutrient_bar"], 1),
            });

            var survival = playerGo.AddComponent<SurvivalStats>();
            SetRef(survival, "respawnPoint", respawn);

            var interactor = playerGo.AddComponent<Interactor>();
            SetRef(interactor, "cam", cam);

            var placer = playerGo.AddComponent<BuildPlacer>();
            SetRef(placer, "cam", cam);
            SetRef(placer, "inventory", inv);
            SetRef(placer, "validMaterial", m.Emissive("GhostValid", new Color(0.2f, 0.9f, 0.4f)));
            SetRef(placer, "invalidMaterial", m.Emissive("GhostInvalid", new Color(0.9f, 0.2f, 0.2f)));

            var save = systems.AddComponent<SaveSystem>();
            SetRef(save, "database", db);
            SetRef(save, "player", fpc);
            SetRef(save, "playerInventory", inv);
            SetRef(save, "playerSurvival", survival);

            var hud = systems.AddComponent<DebugHud>();
            SetRef(hud, "database", db);
            SetRef(hud, "inventory", inv);
            SetRef(hud, "survival", survival);
            SetRef(hud, "interactor", interactor);
            SetRef(hud, "placer", placer);
            SetRef(hud, "saveSystem", save);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void AddVegetationLayer(Transform parent, string name, TerraformStage stage, int count, System.Random rng,
            Terrain terrain, Vector3 center, Func<Transform, System.Random, GameObject> make)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                var go = make(layer.transform, rng);
                go.transform.position = RandomPointInRing(rng, center, 60f, 210f, terrain);
                go.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            }
            layer.SetActive(false);

            var activator = new GameObject(name + " (ativador)").AddComponent<StageActivator>();
            activator.transform.SetParent(parent, false);
            SetRef(activator, "stage", stage);
            var so = new SerializedObject(activator);
            var targets = so.FindProperty("targets");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = layer;
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

        static Terrain CreateTerrain()
        {
            string dataPath = TerrainDir + "/PlanetTerrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "RockAlbedo" };
                var rnd = new System.Random(7);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = 0.85f + (float)rnd.NextDouble() * 0.3f;
                    tex.SetPixel(x, y, new Color(0.55f * n, 0.36f * n, 0.26f * n));
                }
                tex.Apply();
                AssetDatabase.CreateAsset(tex, TerrainDir + "/RockAlbedo.asset");

                var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(8f, 8f) };
                AssetDatabase.CreateAsset(layer, TerrainDir + "/Rock.terrainlayer");

                td = new TerrainData { heightmapResolution = HeightmapRes };
                td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
                var heights = new float[HeightmapRes, HeightmapRes];
                var mid = new Vector2(0.5f, 0.5f);
                for (int y = 0; y < HeightmapRes; y++)
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float u = x / (float)(HeightmapRes - 1), v = y / (float)(HeightmapRes - 1);
                    float noise = Mathf.PerlinNoise(u * 6f + 11.3f, v * 6f + 7.1f) * 0.6f
                                  + Mathf.PerlinNoise(u * 18f + 3.7f, v * 18f + 1.9f) * 0.15f;
                    float d = Vector2.Distance(new Vector2(u, v), mid);
                    float basin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.32f, d));
                    float rim = Mathf.InverseLerp(0.36f, 0.5f, d);
                    heights[y, x] = Mathf.Lerp(0.05f, 0.3f + noise * 0.25f, basin) + rim * 0.35f;
                }
                td.SetHeights(0, 0, heights);
                td.terrainLayers = new[] { layer };
                AssetDatabase.CreateAsset(td, dataPath);
            }

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terreno";
            var terrain = go.GetComponent<Terrain>();
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
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
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                    if (emissive)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", color * 1.5f);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    AssetDatabase.CreateAsset(mat, path);
                }
                cache[name] = mat;
                return mat;
            }
        }
    }
}
