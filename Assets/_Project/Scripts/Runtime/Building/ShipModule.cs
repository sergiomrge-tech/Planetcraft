using System;
using System.Collections.Generic;
using Planetcraft.Items;
using UnityEngine;

namespace Planetcraft.Building
{
    // Uma etapa da montagem da nave-mãe no estaleiro. `objectName` é o filho do modelo que aparece quando instalado.
    [CreateAssetMenu(menuName = "Planetcraft/Ship Module")]
    public class ShipModule : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public ItemAmount[] cost;
        public double requiredTi;
        public string objectName;
    }

    // Progresso global da nave (só existe um estaleiro). Salvo no save.
    public static class ShipProgress
    {
        static readonly HashSet<string> installed = new();

        public static bool Launched { get; set; }
        public static event Action Changed;
        public static IEnumerable<string> Installed => installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            installed.Clear();
            Launched = false;
            Changed = null;
        }

        public static bool IsInstalled(string id) => installed.Contains(id);
        public static int Count => installed.Count;

        public static void Install(string id)
        {
            if (installed.Add(id)) Changed?.Invoke();
        }

        public static void Restore(IEnumerable<string> ids, bool launched)
        {
            installed.Clear();
            if (ids != null) installed.UnionWith(ids);
            Launched = launched;
            Changed?.Invoke();
        }
    }
}
