using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Building
{
    // Grade do kit de base: células de 4 m, pé-direito de 3 m. Guarda onde estão pisos e paredes para encaixar peças novas
    // e descobrir quais salas estão seladas (piso, quatro paredes e teto) com um gerador de O2 ligado dentro.
    public static class BaseGrid
    {
        public const float Cell = 4f;
        public const float Level = 3f;

        static readonly Dictionary<(int x, int z, int l), BasePiece> Floors = new();
        static readonly Dictionary<(int axis, int x, int z, int l), BasePiece> Walls = new();
        static readonly List<BasePiece> Sources = new();
        static readonly HashSet<(int x, int z, int l)> Sealed = new();
        static float nextRefresh;
        static bool dirty = true;
        static int lastSealedCount;

        public static bool HasDatum { get; private set; }
        public static float DatumY { get; private set; }

        // O alerta de brecha sai quando uma sala selada perde a vedação.
        public static event Action Breached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Floors.Clear();
            Walls.Clear();
            Sources.Clear();
            Sealed.Clear();
            HasDatum = false;
            dirty = true;
            lastSealedCount = 0;
            Breached = null;
        }

        public static int LevelOf(float y) => Mathf.RoundToInt((y - DatumY) / Level);
        public static int CellOf(float v) => Mathf.RoundToInt(v / Cell);

        public static Vector3 CellCenter(int x, int z, int level) => new(x * Cell, DatumY + level * Level, z * Cell);

        public static void SetDatum(float y)
        {
            HasDatum = true;
            DatumY = y;
        }

        public static bool HasFloor(int x, int z, int level) => Floors.ContainsKey((x, z, level));

        // Aresta entre (x,z) e o vizinho na direção dada (0 = +X, 1 = +Z, 2 = -X, 3 = -Z).
        public static (int axis, int x, int z) EdgeKey(int x, int z, int dir) => dir switch
        {
            0 => (1, x, z),
            1 => (0, x, z),
            2 => (1, x - 1, z),
            _ => (0, x, z - 1),
        };

        public static bool HasWall(int axis, int x, int z, int level) => Walls.ContainsKey((axis, x, z, level));

        // Pelo menos um dos dois lados da aresta tem piso: a parede tem onde se apoiar.
        public static bool EdgeSupported(int axis, int x, int z, int level)
        {
            if (axis == 0) return HasFloor(x, z, level) || HasFloor(x, z + 1, level);
            return HasFloor(x, z, level) || HasFloor(x + 1, z, level);
        }

        public static void Register(BasePiece piece)
        {
            var p = piece.transform.position;
            if (piece.Kind == PieceKind.Foundation && !HasDatum) SetDatum(p.y);
            int level = HasDatum ? LevelOf(p.y) : 0;
            switch (piece.Kind)
            {
                case PieceKind.Foundation:
                case PieceKind.Floor:
                    Floors[(CellOf(p.x), CellOf(p.z), level)] = piece;
                    break;
                case PieceKind.Wall:
                {
                    float yaw = Mathf.Repeat(piece.transform.eulerAngles.y, 180f);
                    bool alongZ = yaw > 45f && yaw < 135f;
                    var key = alongZ ? (1, CellOf(p.x - Cell / 2f), CellOf(p.z), level) : (0, CellOf(p.x), CellOf(p.z - Cell / 2f), level);
                    Walls[key] = piece;
                    break;
                }
            }
            if (piece.IsAirSource && !Sources.Contains(piece)) Sources.Add(piece);
            dirty = true;
        }

        public static void Unregister(BasePiece piece)
        {
            RemoveValue(Floors, piece);
            RemoveValue(Walls, piece);
            Sources.Remove(piece);
            dirty = true;
        }

        static void RemoveValue<TKey>(Dictionary<TKey, BasePiece> map, BasePiece piece)
        {
            TKey found = default;
            bool any = false;
            foreach (var kv in map)
                if (kv.Value == piece)
                {
                    found = kv.Key;
                    any = true;
                    break;
                }
            if (any) map.Remove(found);
        }

        public static void MarkDirty() => dirty = true;

        public static bool IsPressurized(Vector3 position)
        {
            if (!HasDatum || Sources.Count == 0) return false;
            Refresh();
            int level = Mathf.FloorToInt((position.y - DatumY + 0.6f) / Level);
            return Sealed.Contains((CellOf(position.x), CellOf(position.z), level));
        }

        // Células seladas agora (para a bússola de brechas e para testes).
        public static int SealedCellCount
        {
            get
            {
                Refresh();
                return Sealed.Count;
            }
        }

        static void Refresh()
        {
            // Também reavalia a cada segundo: ligar ou desligar o gerador de O2 não passa por Register.
            if (!dirty && Time.unscaledTime < nextRefresh) return;
            dirty = false;
            nextRefresh = Time.unscaledTime + 1f;
            Sealed.Clear();
            foreach (var source in Sources)
            {
                if (source == null || !source.AirSourceRunning) continue;
                var p = source.transform.position;
                int level = LevelOf(p.y);
                var start = (CellOf(p.x), CellOf(p.z), level);
                if (Sealed.Contains(start) || !Floors.ContainsKey(start)) continue;
                var room = new List<(int x, int z, int l)>();
                if (Flood(start, room)) foreach (var c in room) Sealed.Add(c);
            }
            if (Sealed.Count < lastSealedCount) Breached?.Invoke();
            lastSealedCount = Sealed.Count;
        }

        // Preenche a sala a partir de uma célula; devolve false se houver brecha (aresta aberta, chão ou teto faltando).
        static bool Flood((int x, int z, int l) start, List<(int x, int z, int l)> room)
        {
            var seen = new HashSet<(int x, int z, int l)> { start };
            var queue = new Queue<(int x, int z, int l)>();
            queue.Enqueue(start);
            ReadOnlySpan<(int dx, int dz)> dirs = stackalloc (int, int)[] { (1, 0), (0, 1), (-1, 0), (0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                room.Add(c);
                if (room.Count > 160) return false;
                if (!Floors.ContainsKey((c.x, c.z, c.l + 1))) return false; // sem teto
                for (int d = 0; d < 4; d++)
                {
                    var (axis, ex, ez) = EdgeKey(c.x, c.z, d);
                    if (Walls.ContainsKey((axis, ex, ez, c.l))) continue;
                    var n = (c.x + dirs[d].dx, c.z + dirs[d].dz, c.l);
                    if (!Floors.ContainsKey(n)) return false;  // abertura para fora
                    if (seen.Add(n)) queue.Enqueue(n);
                }
            }
            return true;
        }
    }
}
