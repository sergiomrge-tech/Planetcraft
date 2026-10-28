using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.EditorTools
{
    // Regra de distância da vegetação gerada: cada planta ocupa um raio (a copa, medida nos renderers) e nenhuma
    // pode invadir a vizinha além do permitido. Árvores guardam espaço entre as copas e ninguém nasce no tronco.
    public static class VegetationSpacing
    {
        // Distância mínima entre troncos de árvores, qualquer que seja o tamanho (metros).
        const float MinTreeGap = 6.5f;
        const float Cell = 12f;

        struct Entry
        {
            public Vector2 pos;
            public float radius;
            public bool tree;
        }

        static readonly Dictionary<(int, int), List<Entry>> Grid = new();

        public static void Reset() => Grid.Clear();

        // Raio horizontal da copa/folhagem: metade da maior dimensão dos renderers (o LODGroup soma todos os níveis).
        public static float CrownRadius(GameObject go)
        {
            Bounds? b = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (b == null) b = r.bounds;
                else { var v = b.Value; v.Encapsulate(r.bounds); b = v; }
            }
            return b == null ? 0.5f : Mathf.Max(b.Value.extents.x, b.Value.extents.z);
        }

        static float Need(Entry a, Entry b)
        {
            if (a.tree && b.tree) return Mathf.Max(MinTreeGap, 0.75f * (a.radius + b.radius));
            // Árvore x arbusto/samambaia: só livra o tronco; a folhagem baixa pode crescer sob a copa.
            if (a.tree || b.tree)
            {
                var other = a.tree ? b : a;
                var tree = a.tree ? a : b;
                return 0.8f + 0.12f * tree.radius + other.radius * 0.6f;
            }
            return 0.55f * (a.radius + b.radius);
        }

        public static bool TryPlace(Vector3 world, float radius, bool tree)
        {
            var e = new Entry { pos = new Vector2(world.x, world.z), radius = radius, tree = tree };
            int cx = Mathf.FloorToInt(e.pos.x / Cell), cy = Mathf.FloorToInt(e.pos.y / Cell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!Grid.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                foreach (var o in list)
                    if ((o.pos - e.pos).sqrMagnitude < Need(e, o) * Need(e, o)) return false;
            }
            if (!Grid.TryGetValue((cx, cy), out var cell)) Grid[(cx, cy)] = cell = new List<Entry>();
            cell.Add(e);
            return true;
        }
    }
}
