using System.Collections.Generic;
using System.IO;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Data;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Planetcraft.Save
{
    // Salva só estado (valores, inventário, o que foi construído/coletado), nunca a cena.
    // Carregar recarrega a cena e aplica o estado em cima de um mundo limpo.
    [DefaultExecutionOrder(-50)]
    public class SaveSystem : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] FirstPersonController player;
        [SerializeField] InventoryComponent playerInventory;
        [SerializeField] SurvivalStats playerSurvival;
        [SerializeField] string fileName = "planetcraft_save.json";

        static SaveData pendingLoad;

        public string LastMessage { get; private set; }
        public float LastMessageTime { get; private set; }

        string FilePath => Path.Combine(Application.persistentDataPath, fileName);
        public bool HasSave => File.Exists(FilePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pendingLoad = null;

        void Start()
        {
            if (pendingLoad == null) return;
            var data = pendingLoad;
            pendingLoad = null;
            Apply(data);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f5Key.wasPressedThisFrame) Save();
            if (kb.f9Key.wasPressedThisFrame) Load();
        }

        public void Save()
        {
            var data = new SaveData
            {
                terraform = TerraformManager.Instance.GetValues(),
                playerPosition = player.transform.position,
                playerYaw = player.Yaw,
                oxygen = playerSurvival.Oxygen,
                water = playerSurvival.Water,
                food = playerSurvival.Food,
            };

            WriteSlots(playerInventory.Inventory, data.inventory);

            foreach (var p in PlacedObject.All)
            {
                if (p.Definition == null) continue;
                var entry = new PlacedData
                {
                    guid = p.Guid,
                    buildableId = p.Definition.id,
                    position = p.transform.position,
                    rotation = p.transform.rotation,
                };
                if (p.TryGetComponent<InventoryComponent>(out var storage)) WriteSlots(storage.Inventory, entry.contents);
                data.placed.Add(entry);
            }

            data.collectedNodes.AddRange(ResourceNode.CollectedIds);
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Notify("Jogo salvo");
        }

        public void Load()
        {
            if (!HasSave)
            {
                Notify("Nenhum save encontrado");
                return;
            }
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data == null || data.version > SaveData.CurrentVersion)
            {
                Notify("Save incompatível");
                return;
            }
            pendingLoad = data;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Apply(SaveData data)
        {
            if (data.terraform != null) TerraformManager.Instance.SetValues(data.terraform);

            ReadSlots(data.inventory, playerInventory.Inventory);

            foreach (var p in data.placed)
            {
                var def = database.FindBuildable(p.buildableId);
                if (def == null || def.prefab == null) continue;
                var placed = PlacedObject.Spawn(def, p.position, p.rotation, p.guid);
                if (p.contents != null && placed.TryGetComponent<InventoryComponent>(out var storage))
                    ReadSlots(p.contents, storage.Inventory);
            }

            ResourceNode.RestoreCollected(data.collectedNodes);
            player.Teleport(data.playerPosition, data.playerYaw);
            playerSurvival.SetValues(data.oxygen, data.water, data.food);
            Notify("Jogo carregado");
        }

        static void WriteSlots(Inventory inv, List<SlotData> target)
        {
            for (int i = 0; i < inv.Capacity; i++)
            {
                var s = inv[i];
                if (!s.IsEmpty) target.Add(new SlotData { slot = i, itemId = s.item.id, amount = s.amount });
            }
        }

        void ReadSlots(List<SlotData> source, Inventory inv)
        {
            inv.Clear();
            foreach (var s in source)
            {
                var item = database.FindItem(s.itemId);
                if (item != null && s.slot >= 0 && s.slot < inv.Capacity) inv.SetSlot(s.slot, item, s.amount);
            }
        }

        void Notify(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.time;
        }
    }
}
