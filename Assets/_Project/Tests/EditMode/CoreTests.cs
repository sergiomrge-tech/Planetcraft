using System.Linq;
using NUnit.Framework;
using Planetcraft.Core;
using Planetcraft.Data;
using Planetcraft.Items;
using UnityEditor;
using UnityEngine;

namespace Planetcraft.Tests
{
    public class InventoryTests
    {
        static ItemDefinition Item(int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.maxStack = maxStack;
            return item;
        }

        [Test]
        public void AddStacksIntoExistingSlotsBeforeEmptyOnes()
        {
            var iron = Item(10);
            var inv = new Inventory(3);
            Assert.IsTrue(inv.TryAdd(iron, 7));
            Assert.IsTrue(inv.TryAdd(iron, 5));
            Assert.AreEqual(12, inv.Count(iron));
            Assert.AreEqual(10, inv[0].amount);
            Assert.AreEqual(2, inv[1].amount);
        }

        [Test]
        public void AddFailsWithoutChangingAnythingWhenThereIsNoRoom()
        {
            var iron = Item(5);
            var inv = new Inventory(1);
            Assert.IsTrue(inv.TryAdd(iron, 4));
            Assert.IsFalse(inv.TryAdd(iron, 2));
            Assert.AreEqual(4, inv.Count(iron));
        }

        [Test]
        public void TransferIsLimitedByTheTargetSpace()
        {
            var ice = Item(10);
            var from = new Inventory(2);
            var to = new Inventory(1);
            from.TryAdd(ice, 15);
            to.TryAdd(ice, 8);
            Assert.AreEqual(2, from.TransferTo(to, ice, 15));
            Assert.AreEqual(13, from.Count(ice));
            Assert.AreEqual(10, to.Count(ice));
        }

        [Test]
        public void RemoveAllTakesEveryCostOrNothing()
        {
            var iron = Item(10);
            var ice = Item(10);
            var inv = new Inventory(4);
            inv.TryAdd(iron, 3);
            inv.TryAdd(ice, 1);
            var costs = new[] { new ItemAmount(iron, 2), new ItemAmount(ice, 2) };
            Assert.IsFalse(inv.TryRemoveAll(costs));
            Assert.AreEqual(3, inv.Count(iron));
            inv.TryAdd(ice, 1);
            Assert.IsTrue(inv.TryRemoveAll(costs));
            Assert.AreEqual(1, inv.Count(iron));
            Assert.AreEqual(0, inv.Count(ice));
        }
    }

    public class TerraformTests
    {
        class FixedRates : ITerraformContributor
        {
            public void AccumulateRates(double[] rates)
            {
                rates[(int)TerraformParameter.Heat] += 2;
                rates[(int)TerraformParameter.Decontamination] += 3;
            }
        }

        TerraformManager manager;
        FixedRates contributor;

        [SetUp]
        public void SetUp()
        {
            manager = new GameObject("tm").AddComponent<TerraformManager>();
            contributor = new FixedRates();
        }

        [TearDown]
        public void TearDown()
        {
            TerraformManager.Unregister(contributor);
            Object.DestroyImmediate(manager.gameObject);
        }

        [Test]
        public void TiIsTheSumOfAllFiveParameters()
        {
            Assert.AreEqual(5, TerraformParameters.Count);
            for (int i = 0; i < TerraformParameters.Count; i++) manager.Add((TerraformParameter)i, 10);
            Assert.AreEqual(50, manager.Ti, 1e-9);
        }

        [Test]
        public void SimulateAppliesContributorRates()
        {
            TerraformManager.Register(contributor);
            manager.Simulate(10f);
            Assert.AreEqual(20, manager.Get(TerraformParameter.Heat), 1e-9);
            Assert.AreEqual(30, manager.Get(TerraformParameter.Decontamination), 1e-9);
            Assert.AreEqual(50, manager.Ti, 1e-9);
        }

        [Test]
        public void OldSavesWithFourParametersStillLoad()
        {
            manager.SetValues(new double[] { 1, 2, 3, 4 });
            Assert.AreEqual(10, manager.Ti, 1e-9);
            Assert.AreEqual(0, manager.Get(TerraformParameter.Decontamination), 1e-9);
        }
    }

    public class LocalizationTests
    {
        [Test]
        public void PortugueseIsReturnedUnchanged()
        {
            Assert.AreEqual("Ferro", Loc.Translate("Ferro", english: false));
            Assert.AreEqual("Iron", Loc.Translate("Ferro", english: true));
            Assert.AreEqual("texto sem tradução", Loc.Translate("texto sem tradução", english: true));
        }

        // Toda a tabela de dados do jogo precisa ter inglês: se alguém mudar um texto no PrototypeBootstrap sem
        // atualizar LocData, este teste aponta qual.
        [Test]
        public void EveryGameDataTextHasEnglish()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            Assert.IsNotNull(db, "GameDatabase não encontrado (rode Planetcraft → Gerar Protótipo)");
            var texts = db.items.Where(i => i != null).Select(i => i.displayName)
                .Concat(db.buildables.Where(b => b != null).SelectMany(b => new[] { b.displayName, b.description }))
                .Concat(db.logs.Where(l => l != null).SelectMany(l => new[] { l.title, l.body }))
                .Concat(AssetDatabase.FindAssets("t:TerraformStage").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<TerraformStage>).SelectMany(s => new[] { s.displayName, s.description }))
                .Concat(AssetDatabase.FindAssets("t:ShipModule").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Planetcraft.Building.ShipModule>).SelectMany(m => new[] { m.displayName, m.description }))
                .Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
            var missing = texts.Where(t => !Loc.HasEnglish(t)).ToList();
            Assert.IsEmpty(missing, "Sem inglês: " + string.Join(" | ", missing));
        }
    }

    public class InputTests
    {
        [Test]
        public void EveryRebindableActionHasKeyboardAndDisplayText()
        {
            GameInput.Init();
            foreach (var (label, action) in GameInput.Rebindable)
            {
                Assert.IsNotNull(action, label);
                Assert.AreNotEqual("—", GameInput.Display(action), label);
            }
            Assert.AreEqual("E", GameInput.Display(GameInput.Interact));
        }
    }

    public class CombatTests
    {
        [Test]
        public void HealthTakesDamageArmorAndDies()
        {
            var go = new GameObject("alvo");
            var h = go.AddComponent<Planetcraft.Combat.Health>();
            h.Max = 100f;
            h.Restore();
            h.Team = Planetcraft.Combat.Team.Hostile;
            typeof(Planetcraft.Combat.Health).GetField("destroyOnDeath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(h, false);
            bool died = false;
            h.Died += _ => died = true;
            Assert.IsTrue(h.Damage(30f));
            Assert.AreEqual(70f, h.Current, 0.01f);
            h.Heal(500f);
            Assert.AreEqual(100f, h.Current, 0.01f);
            h.Damage(1000f);
            Assert.IsTrue(h.IsDead);
            Assert.IsTrue(died);
            Assert.IsFalse(h.Damage(5f), "alvo morto não leva mais dano");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GridEdgesAreSharedBetweenNeighbours()
        {
            // A aresta leste da célula (2,3) é a aresta oeste da célula (3,3); norte/sul idem.
            Assert.AreEqual(Planetcraft.Building.BaseGrid.EdgeKey(2, 3, 0), Planetcraft.Building.BaseGrid.EdgeKey(3, 3, 2));
            Assert.AreEqual(Planetcraft.Building.BaseGrid.EdgeKey(2, 3, 1), Planetcraft.Building.BaseGrid.EdgeKey(2, 4, 3));
            Assert.AreNotEqual(Planetcraft.Building.BaseGrid.EdgeKey(2, 3, 0), Planetcraft.Building.BaseGrid.EdgeKey(2, 3, 1));
        }

        [Test]
        public void ThreatLevelsScaleFromOffToHigh()
        {
            int old = Planetcraft.Core.Settings.Threat;
            Planetcraft.Core.Settings.Threat = 0;
            Assert.AreEqual(0f, Planetcraft.Core.Settings.ThreatScale);
            Planetcraft.Core.Settings.Threat = 3;
            Assert.Greater(Planetcraft.Core.Settings.ThreatScale, 1f);
            Planetcraft.Core.Settings.Threat = old;
        }
    }
}
