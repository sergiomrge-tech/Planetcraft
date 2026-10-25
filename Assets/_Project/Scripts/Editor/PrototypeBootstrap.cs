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
                R("alloy", "alloy", 300, C(it["iron"], 1), C(it["titanium"], 1), C(it["magnesium"], 1)),
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
            Add("heater_t1", "Aquecedor", "Calor +1.2/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 1), C(it["titanium"], 1) }, Rates(TerraformParameter.Heat, 1.2), true, (t, mm) => ShapeHeater(t, mm, 1f));
            Add("drill_t1", "Perfuratriz", "Pressão +1/s. Consome 1 kW.", 0, 0, 1f,
                new[] { C(it["iron"], 2) }, Rates(TerraformParameter.Pressure, 1), true, (t, mm) => ShapeDrill(t, mm, 1f));
            Add("vegetube", "Vegetubo", "Oxigênio +0.8/s. Consome 0.5 kW.", 50, 0, 0.5f,
                new[] { C(it["ice"], 1), C(it["silicon"], 1) }, Rates(TerraformParameter.Oxygen, 0.8), true, ShapeVegetube);
            Add("solar_panel", "Painel Solar", "+6.5 kW", 300, 6.5f, 0,
                new[] { C(it["silicon"], 2), C(it["iron"], 1) }, none, true, ShapeSolar);
            Add("heater_t2", "Aquecedor II", "Calor +12/s. Consome 4 kW.", 1_000, 0, 4f,
                new[] { C(it["titanium"], 2), C(it["alloy"], 1) }, Rates(TerraformParameter.Heat, 12), true, (t, mm) => ShapeHeater(t, mm, 1.6f));
            Add("drill_t2", "Perfuratriz II", "Pressão +10/s. Consome 4 kW.", 2_500, 0, 4f,
                new[] { C(it["iron"], 3), C(it["alloy"], 1) }, Rates(TerraformParameter.Pressure, 10), true, (t, mm) => ShapeDrill(t, mm, 1.6f));
            Add("algae_farm", "Fazenda de Algas", "Biomassa +25/s, Oxigênio +5/s. Consome 2 kW.", 20_000, 0, 2f,
                new[] { C(it["ice"], 2), C(it["magnesium"], 1) },
                new[] { new TerraformRate(TerraformParameter.Biomass, 25), new TerraformRate(TerraformParameter.Oxygen, 5) }, true, ShapeAlgae);
            Add("heater_t3", "Aquecedor III", "Calor +400/s. Consome 15 kW.", 60_000, 0, 15f,
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
            var waterRenderer = water.GetComponent<Renderer>();
            waterRenderer.sharedMaterial = ShaderMaterial("WaterSurface", "Planetcraft/Water");
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            AssetDatabase.DeleteAsset(MatDir + "/Water.mat"); // material Lit da primeira versão
            var waterReactor = water.AddComponent<WaterReactor>();
            SetDouble(waterReactor, "tiStart", stages["water"].requiredTi);
            SetDouble(waterReactor, "tiEnd", stages["forest"].requiredTi);

            // Sistemas
            var systems = new GameObject("Sistemas");
            var tm = systems.AddComponent<TerraformManager>();
            SetRef(tm, "stages", stageDb);

            var sky = new GameObject("Céu").AddComponent<SkyReactor>();
            SetRef(sky, "sun", sun);
            // Céu muda forte nos primeiros estágios: Névoa ~1/3, Céu Azul ~2/3, Nuvens completo.
            SetDouble(sky, "tiStart", 20);
            SetDouble(sky, "tiEnd", stages["clouds"].requiredTi);

            var ground = terrain.gameObject.AddComponent<GroundReactor>();
            SetRef(ground, "terrain", terrain);
            SetDouble(ground, "tiStart", stages["moss"].requiredTi * 0.5);
            SetDouble(ground, "tiEnd", stages["forest"].requiredTi * 5);

            // Vegetação por estágio
            var rng = new System.Random(4242);
            var vegetation = new GameObject("Vegetação");
            AddVegetationLayer(vegetation.transform, "Musgo", stages["moss"].requiredTi, stages["flora"].requiredTi, 350, rng, terrain, center, (p, r) =>
                Part(p, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.6f, 0.25f, 1.6f) * (0.6f + (float)r.NextDouble()), m.Lit("Moss", new Color(0.25f, 0.4f, 0.15f)), false));
            AddVegetationLayer(vegetation.transform, "Flora", stages["flora"].requiredTi, stages["forest"].requiredTi, 200, rng, terrain, center, (p, r) =>
                Part(p, PrimitiveType.Sphere, Vector3.up * 0.4f, Vector3.one * (0.6f + (float)r.NextDouble() * 0.6f), m.Lit("Bush", new Color(0.3f, 0.6f, 0.2f)), false));
            AddVegetationLayer(vegetation.transform, "Florestas", stages["forest"].requiredTi, stages["fauna"].requiredTi * 2, 260, rng, terrain, center, (p, r) =>
            {
                float s = 1f + (float)r.NextDouble() * 1.1f;
                var tree = new GameObject("Árvore");
                tree.transform.SetParent(p, false);
                var bark = m.Lit("Bark", new Color(0.35f, 0.22f, 0.12f));
                if (r.NextDouble() < 0.4)
                {
                    // Conífera: tronco fino e copa em camadas
                    Part(tree.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f) * s, new Vector3(0.25f, 1.6f, 0.25f) * s, bark);
                    var pine = m.Lit("Pine", new Color(0.1f, 0.35f, 0.18f));
                    for (int k = 0; k < 3; k++)
                        Part(tree.transform, PrimitiveType.Sphere, new Vector3(0f, (2.4f + k * 1.1f) * s, 0f), new Vector3(2.2f - k * 0.6f, 1.1f, 2.2f - k * 0.6f) * s, pine, false);
                }
                else
                {
                    Part(tree.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f) * s, new Vector3(0.35f, 1.5f, 0.35f) * s, bark);
                    var leaves = m.Lit(r.NextDouble() < 0.5 ? "Leaves" : "LeavesLight", r.NextDouble() < 0.5 ? new Color(0.18f, 0.5f, 0.2f) : new Color(0.32f, 0.58f, 0.22f));
                    Part(tree.transform, PrimitiveType.Sphere, new Vector3(0f, 3.6f, 0f) * s, Vector3.one * 2.6f * s, leaves, false);
                    Part(tree.transform, PrimitiveType.Sphere, new Vector3(0.8f, 3.1f, 0.3f) * s, Vector3.one * 1.7f * s, leaves, false);
                }
                return tree;
            }, minRadius: 110f, maxRadius: 225f);

            // Fauna: herbívoros e saltadores que passeiam fora d'água
            var fauna = new GameObject("Fauna");
            AddVegetationLayer(fauna.transform, "Herbívoros", stages["fauna"].requiredTi, stages["fauna"].requiredTi * 5, 45, rng, terrain, center,
                (p, r) => CreateGrazer(p, r, m), minRadius: 150f, maxRadius: 220f);
            AddVegetationLayer(fauna.transform, "Saltadores", stages["fauna"].requiredTi, stages["fauna"].requiredTi * 5, 40, rng, terrain, center,
                (p, r) => CreateHopper(p, r, m), minRadius: 140f, maxRadius: 220f);

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

            var feedbackAudio = systems.AddComponent<AudioSource>();
            feedbackAudio.playOnAwake = false;
            feedbackAudio.spatialBlend = 0f;
            var feedback = systems.AddComponent<StageFeedback>();
            SetRef(feedback, "database", db);
            SetRef(feedback, "audioSource", feedbackAudio);

            // Clima: camada de nuvens + chuva que segue o jogador
            var clouds = GameObject.CreatePrimitive(PrimitiveType.Plane);
            clouds.name = "Nuvens";
            Object.DestroyImmediate(clouds.GetComponent<Collider>());
            clouds.transform.position = new Vector3(center.x, 180f, center.z);
            clouds.transform.localScale = new Vector3(300f, 1f, 300f);
            var cloudRenderer = clouds.GetComponent<Renderer>();
            cloudRenderer.sharedMaterial = ShaderMaterial("CloudLayer", "Planetcraft/CloudLayer");
            cloudRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cloudRenderer.receiveShadows = false;

            var rain = CreateRain(RainMaterial());
            var weatherGo = new GameObject("Clima");
            var rainAudio = weatherGo.AddComponent<AudioSource>();
            rainAudio.playOnAwake = false;
            rainAudio.spatialBlend = 0f;
            var weather = weatherGo.AddComponent<WeatherController>();
            SetRef(weather, "cloudLayer", cloudRenderer);
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

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static GameObject CreateGrazer(Transform parent, System.Random r, MaterialLibrary m)
        {
            float s = 0.9f + (float)r.NextDouble() * 0.4f;
            var root = new GameObject("Herbívoro");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * s;
            var fur = r.NextDouble() < 0.5 ? m.Lit("FurBrown", new Color(0.45f, 0.3f, 0.18f)) : m.Lit("FurSand", new Color(0.72f, 0.6f, 0.4f));
            var dark = m.Lit("Hoof", new Color(0.15f, 0.12f, 0.1f));
            foreach (var x in new[] { -0.3f, 0.3f })
            foreach (var z in new[] { -0.6f, 0.6f })
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(x, 0.35f, z), new Vector3(0.14f, 0.35f, 0.14f), dark, false);
            var body = new GameObject("Corpo").transform;
            body.SetParent(root.transform, false);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.75f, 1.7f), fur, false);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 1.25f, 1.0f), new Vector3(0.45f, 0.45f, 0.6f), fur, false);
            Part(body, PrimitiveType.Cylinder, new Vector3(-0.12f, 1.55f, 1.0f), new Vector3(0.05f, 0.18f, 0.05f), dark, false);
            Part(body, PrimitiveType.Cylinder, new Vector3(0.12f, 1.55f, 1.0f), new Vector3(0.05f, 0.18f, 0.05f), dark, false);

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
            var fur = m.Lit("FurWhite", new Color(0.9f, 0.88f, 0.84f));
            var body = new GameObject("Corpo").transform;
            body.SetParent(root.transform, false);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.4f, 0.55f), fur, false);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0.25f), Vector3.one * 0.28f, fur, false);
            Part(body, PrimitiveType.Sphere, new Vector3(-0.07f, 0.7f, 0.22f), new Vector3(0.07f, 0.25f, 0.05f), fur, false);
            Part(body, PrimitiveType.Sphere, new Vector3(0.07f, 0.7f, 0.22f), new Vector3(0.07f, 0.25f, 0.05f), fur, false);

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

        static void AddVegetationLayer(Transform parent, string name, double tiStart, double tiEnd, int count, System.Random rng,
            Terrain terrain, Vector3 center, Func<Transform, System.Random, GameObject> make, float minRadius = 60f, float maxRadius = 210f)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                var go = make(layer.transform, rng);
                go.transform.position = RandomPointInRing(rng, center, minRadius, maxRadius, terrain);
                go.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
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
                    // Alfa = suavidade no Terrain Lit do URP: 0 deixa a rocha fosca.
                    tex.SetPixel(x, y, new Color(0.55f * n, 0.36f * n, 0.26f * n, 0f));
                }
                tex.Apply();
                AssetDatabase.CreateAsset(tex, TerrainDir + "/RockAlbedo.asset");

                var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(8f, 8f), smoothness = 0.05f, metallic = 0f };
                AssetDatabase.CreateAsset(layer, TerrainDir + "/Rock.terrainlayer");

                // Camada verde que o GroundReactor espalha conforme o planeta verdeja.
                var grassTex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "GrassAlbedo" };
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = 0.8f + (float)rnd.NextDouble() * 0.4f;
                    grassTex.SetPixel(x, y, new Color(0.24f * n, 0.42f * n, 0.16f * n, 0f));
                }
                grassTex.Apply();
                AssetDatabase.CreateAsset(grassTex, TerrainDir + "/GrassAlbedo.asset");
                var grass = new TerrainLayer { diffuseTexture = grassTex, tileSize = new Vector2(6f, 6f), smoothness = 0.1f, metallic = 0f };
                AssetDatabase.CreateAsset(grass, TerrainDir + "/Grass.terrainlayer");

                td = new TerrainData { heightmapResolution = HeightmapRes, alphamapResolution = 128 };
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
                    // Fundo da bacia com relevo: a água sobe formando poças e ilhas, não uma lâmina rasa.
                    heights[y, x] = Mathf.Lerp(0.02f + noise * 0.1f, 0.3f + noise * 0.25f, basin) + rim * 0.35f;
                }
                td.SetHeights(0, 0, heights);
                td.terrainLayers = new[] { layer, grass };
                var rockOnly = new float[td.alphamapResolution, td.alphamapResolution, 2];
                for (int y = 0; y < td.alphamapResolution; y++)
                for (int x = 0; x < td.alphamapResolution; x++)
                    rockOnly[y, x, 0] = 1f;
                td.SetAlphamaps(0, 0, rockOnly);
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
            main.startSize = 0.025f;
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.8f, 0.88f, 1f, 0.3f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 70f, 1f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static Material RainMaterial()
        {
            string path = MatDir + "/Rain.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material ShaderMaterial(string name, string shaderName)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(mat, path);
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
