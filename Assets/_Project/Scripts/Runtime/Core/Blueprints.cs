using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Core
{
    // Projetos obtidos com microchips de destroços. Construções/receitas com `blueprintId`
    // só aparecem depois do chip (além do Ti mínimo).
    public static class Blueprints
    {
        static readonly HashSet<string> unlocked = new();

        public static event Action<string> Unlocked;
        public static IEnumerable<string> All => unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            unlocked.Clear();
            Unlocked = null;
        }

        public static bool Has(string id) => string.IsNullOrEmpty(id) || unlocked.Contains(id);

        public static bool Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || !unlocked.Add(id)) return false;
            Unlocked?.Invoke(id);
            return true;
        }

        // Usado pelo save: não dispara eventos.
        public static void Restore(IEnumerable<string> ids)
        {
            unlocked.Clear();
            if (ids != null) unlocked.UnionWith(ids);
        }
    }
}
