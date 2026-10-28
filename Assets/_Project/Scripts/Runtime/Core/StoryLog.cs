using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Core
{
    [CreateAssetMenu(menuName = "Planetcraft/Story Log")]
    public class StoryLog : ScriptableObject
    {
        [Tooltip("Id estável usado no save.")]
        public string id;
        public string title;
        [TextArea(6, 20)] public string body;
    }

    // Registros já lidos pelo jogador (salvos no save).
    public static class StoryLogs
    {
        static readonly HashSet<string> read = new();

        public static IEnumerable<string> All => read;

        // Quantos registros existem no jogo (definido pelo HUD a partir do banco de dados).
        public static int Total { get; set; } = 10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => read.Clear();

        public static bool IsRead(string id) => read.Contains(id);
        public static void MarkRead(string id) => read.Add(id);

        public static void Restore(IEnumerable<string> ids)
        {
            read.Clear();
            if (ids != null) read.UnionWith(ids);
        }
    }
}
