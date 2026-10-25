using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Building
{
    // Tudo que o jogador constrói. O save guarda só o id da definição, o transform e o guid.
    public class PlacedObject : MonoBehaviour
    {
        static readonly List<PlacedObject> all = new();

        [SerializeField] BuildableDefinition definition;
        [SerializeField] string guid;

        public static IReadOnlyList<PlacedObject> All => all;
        public BuildableDefinition Definition => definition;
        public string Guid => guid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static PlacedObject Spawn(BuildableDefinition def, Vector3 position, Quaternion rotation, string guid = null)
        {
            var go = Instantiate(def.prefab, position, rotation);
            go.name = def.displayName;
            if (!go.TryGetComponent<PlacedObject>(out var placed)) placed = go.AddComponent<PlacedObject>();
            placed.definition = def;
            placed.guid = string.IsNullOrEmpty(guid) ? System.Guid.NewGuid().ToString("N") : guid;
            return placed;
        }

#if UNITY_EDITOR
        public void EditorSetDefinition(BuildableDefinition def) => definition = def;
#endif
    }
}
