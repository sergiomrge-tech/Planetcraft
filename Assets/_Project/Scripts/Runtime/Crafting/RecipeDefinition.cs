using Planetcraft.Items;
using UnityEngine;

namespace Planetcraft.Crafting
{
    [CreateAssetMenu(menuName = "Planetcraft/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string id;
        public ItemAmount[] ingredients;
        public ItemDefinition output;
        [Min(1)] public int outputAmount = 1;
        [Tooltip("Ti mínimo para desbloquear a receita.")]
        public double requiredTi;
    }

    public static class CraftingService
    {
        public static bool TryCraft(RecipeDefinition recipe, Inventory inventory)
        {
            if (recipe == null || recipe.output == null || !inventory.Has(recipe.ingredients)) return false;
            inventory.TryRemoveAll(recipe.ingredients);
            if (inventory.TryAdd(recipe.output, recipe.outputAmount)) return true;

            // Sem espaço para o resultado: devolve os ingredientes.
            foreach (var i in recipe.ingredients) inventory.TryAdd(i.item, i.amount);
            return false;
        }
    }
}
