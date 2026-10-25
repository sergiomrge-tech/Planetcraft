using System;
using System.Collections.Generic;
using Planetcraft.Building;
using Planetcraft.Crafting;
using Planetcraft.Items;
using UnityEngine;

namespace Planetcraft.Data
{
    // Registro central de conteúdo; o save resolve ids por aqui.
    [CreateAssetMenu(menuName = "Planetcraft/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<ItemDefinition> items = new();
        public List<RecipeDefinition> recipes = new();
        public List<BuildableDefinition> buildables = new();

        Dictionary<string, ItemDefinition> itemById;
        Dictionary<string, BuildableDefinition> buildableById;

        void OnEnable()
        {
            itemById = null;
            buildableById = null;
        }

        public ItemDefinition FindItem(string id)
        {
            itemById ??= Index(items, i => i.id);
            return id != null && itemById.TryGetValue(id, out var v) ? v : null;
        }

        public BuildableDefinition FindBuildable(string id)
        {
            buildableById ??= Index(buildables, b => b.id);
            return id != null && buildableById.TryGetValue(id, out var v) ? v : null;
        }

        static Dictionary<string, T> Index<T>(List<T> list, Func<T, string> key) where T : UnityEngine.Object
        {
            var d = new Dictionary<string, T>();
            foreach (var x in list)
                if (x != null && !string.IsNullOrEmpty(key(x))) d[key(x)] = x;
            return d;
        }
    }
}
