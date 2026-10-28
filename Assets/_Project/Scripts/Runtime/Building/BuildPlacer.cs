using Planetcraft.Core;
using Planetcraft.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Planetcraft.Building
{
    // Modo construção: fantasma na mira, validação e colocação.
    public class BuildPlacer : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] InventoryComponent inventory;
        [SerializeField] float maxDistance = 15f;
        [SerializeField, Range(0f, 1f)] float minGroundNormalY = 0.75f;
        [SerializeField] float rotateStep = 15f;
        [SerializeField] Material validMaterial;
        [SerializeField] Material invalidMaterial;
        [Tooltip("Debug: constrói sem gastar recursos.")]
        [SerializeField] bool freeBuild;

        BuildableDefinition selected;
        GameObject ghost;
        Renderer[] ghostRenderers;
        float yaw;
        int shownValidity = -1;

        public BuildableDefinition Selected => selected;
        public string Status { get; private set; }
        // Frame em que Esc/RMB cancelou a construção (para o mesmo Esc não abrir a pausa).
        public int LastCancelFrame { get; private set; } = -1;

        public void Select(BuildableDefinition def)
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;
            selected = def;
            Status = null;
            shownValidity = -1;
            if (def == null || def.prefab == null) return;
            ghost = CreateGhost(def.prefab);
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            if (selected == null || ghost == null) return;
            if (UIState.MenuOpen)
            {
                ghost.SetActive(false);
                return;
            }
            if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
            {
                Select(null);
                LastCancelFrame = Time.frameCount;
                return;
            }

            if (GameInput.Rotate.WasPressedThisFrame()) yaw += rotateStep;
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f) yaw += Mathf.Sign(scroll) * rotateStep;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            bool hit = Physics.Raycast(ray, out var info, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ghost.SetActive(hit);
            if (!hit)
            {
                Status = Loc.T("Mire no chão");
                return;
            }

            var pos = info.point;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            string snapProblem = null;
            if (selected.snap != PieceKind.None) Snap(info, ref pos, ref rot, out snapProblem);
            ghost.transform.SetPositionAndRotation(pos, rot);
            bool valid = Evaluate(info, snapProblem, out var reason);
            Status = reason;
            ShowValidity(valid);

            if (valid && GameInput.Place.WasPressedThisFrame()) Place();
        }

        bool Evaluate(RaycastHit hit, string snapProblem, out string reason)
        {
            var tm = TerraformManager.Instance;
            if (tm != null && tm.Ti < selected.requiredTi)
            {
                reason = $"{Loc.T("Requer")} {TiFormat.Format(selected.requiredTi)}";
                return false;
            }
            if (selected.unique && System.Linq.Enumerable.Any(PlacedObject.All, p => p.Definition == selected))
            {
                reason = Loc.T("Só é possível ter um");
                return false;
            }
            if (!Blueprints.Has(selected.blueprintId))
            {
                reason = Loc.T("Requer microchip das ruínas");
                return false;
            }
            if (snapProblem != null)
            {
                reason = snapProblem;
                return false;
            }
            if (selected.snap == PieceKind.None && hit.normal.y < minGroundNormalY)
            {
                reason = Loc.T("Terreno muito inclinado");
                return false;
            }
            if (!freeBuild && !inventory.Inventory.Has(selected.cost))
            {
                reason = Loc.T("Recursos insuficientes");
                return false;
            }
            reason = $"{GameInput.Hint(GameInput.Place)} {Loc.T("construir")}   {GameInput.Hint(GameInput.Rotate)} {Loc.T("girar")}   " +
                     $"{GameInput.Hint(GameInput.Cancel)} {Loc.T("cancelar")}";
            return true;
        }

        // Encaixe das peças do kit de base na grade de 4 m (ver BaseGrid). `problem` explica por que não dá.
        void Snap(RaycastHit hit, ref Vector3 pos, ref Quaternion rot, out string problem)
        {
            problem = null;
            var p = hit.point;
            var hitPiece = hit.collider.GetComponentInParent<BasePiece>();
            switch (selected.snap)
            {
                case PieceKind.Foundation:
                {
                    int cx = BaseGrid.CellOf(p.x), cz = BaseGrid.CellOf(p.z);
                    float y = BaseGrid.HasDatum ? BaseGrid.DatumY : Mathf.Round(p.y * 2f) / 2f;
                    pos = new Vector3(cx * BaseGrid.Cell, y, cz * BaseGrid.Cell);
                    rot = Quaternion.identity;
                    if (BaseGrid.HasFloor(cx, cz, 0)) problem = Loc.T("Já existe uma fundação aqui");
                    else if (!GroundFits(pos)) problem = Loc.T("Terreno irregular demais para a fundação");
                    break;
                }
                case PieceKind.Floor:
                {
                    if (!BaseGrid.HasDatum) { problem = Loc.T("Construa uma fundação primeiro"); break; }
                    if (hitPiece == null) { problem = Loc.T("Pisos e tetos vão sobre paredes e outros pisos"); break; }
                    int level = hitPiece.Kind == PieceKind.Wall
                        ? Mathf.CeilToInt((p.y - BaseGrid.DatumY) / BaseGrid.Level - 0.1f)
                        : BaseGrid.LevelOf(p.y) + 1;
                    level = Mathf.Clamp(level, 1, 3);
                    int cx = BaseGrid.CellOf(p.x), cz = BaseGrid.CellOf(p.z);
                    pos = BaseGrid.CellCenter(cx, cz, level);
                    rot = Quaternion.identity;
                    if (BaseGrid.HasFloor(cx, cz, level)) problem = Loc.T("Já existe um piso aqui");
                    else if (!FloorSupported(cx, cz, level)) problem = Loc.T("Precisa de paredes ou de um piso vizinho");
                    break;
                }
                case PieceKind.Wall:
                {
                    if (!BaseGrid.HasDatum) { problem = Loc.T("Construa uma fundação primeiro"); break; }
                    int level = Mathf.Clamp(Mathf.FloorToInt((p.y - BaseGrid.DatumY + 0.5f) / BaseGrid.Level), 0, 2);
                    int cx = BaseGrid.CellOf(p.x), cz = BaseGrid.CellOf(p.z);
                    float fx = p.x - cx * BaseGrid.Cell, fz = p.z - cz * BaseGrid.Cell;
                    int dir = Mathf.Abs(fx) > Mathf.Abs(fz) ? (fx > 0f ? 0 : 2) : (fz > 0f ? 1 : 3);
                    var (axis, ex, ez) = BaseGrid.EdgeKey(cx, cz, dir);
                    float y = BaseGrid.DatumY + level * BaseGrid.Level;
                    if (axis == 0)
                    {
                        pos = new Vector3(ex * BaseGrid.Cell, y, ez * BaseGrid.Cell + BaseGrid.Cell / 2f);
                        rot = Quaternion.identity;
                    }
                    else
                    {
                        pos = new Vector3(ex * BaseGrid.Cell + BaseGrid.Cell / 2f, y, ez * BaseGrid.Cell);
                        rot = Quaternion.Euler(0f, 90f, 0f);
                    }
                    if (BaseGrid.HasWall(axis, ex, ez, level)) problem = Loc.T("Já existe uma parede aqui");
                    else if (!BaseGrid.EdgeSupported(axis, ex, ez, level)) problem = Loc.T("Precisa de um piso ao lado");
                    break;
                }
                case PieceKind.Pillar:
                {
                    if (!BaseGrid.HasDatum) { problem = Loc.T("Construa uma fundação primeiro"); break; }
                    int level = Mathf.Clamp(Mathf.FloorToInt((p.y - BaseGrid.DatumY + 0.5f) / BaseGrid.Level), 0, 2);
                    float x = Mathf.Round((p.x - 2f) / BaseGrid.Cell) * BaseGrid.Cell + 2f;
                    float z = Mathf.Round((p.z - 2f) / BaseGrid.Cell) * BaseGrid.Cell + 2f;
                    pos = new Vector3(x, BaseGrid.DatumY + level * BaseGrid.Level, z);
                    rot = Quaternion.identity;
                    int cx0 = BaseGrid.CellOf(x - 2f), cz0 = BaseGrid.CellOf(z - 2f);
                    if (!(BaseGrid.HasFloor(cx0, cz0, level) || BaseGrid.HasFloor(cx0 + 1, cz0, level) ||
                          BaseGrid.HasFloor(cx0, cz0 + 1, level) || BaseGrid.HasFloor(cx0 + 1, cz0 + 1, level)))
                        problem = Loc.T("Precisa de um piso ao lado");
                    break;
                }
                case PieceKind.Stairs:
                {
                    if (!BaseGrid.HasDatum) { problem = Loc.T("Construa uma fundação primeiro"); break; }
                    int level = Mathf.Clamp(Mathf.FloorToInt((p.y - BaseGrid.DatumY + 0.5f) / BaseGrid.Level), 0, 2);
                    int cx = BaseGrid.CellOf(p.x), cz = BaseGrid.CellOf(p.z);
                    pos = BaseGrid.CellCenter(cx, cz, level);
                    rot = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
                    if (!BaseGrid.HasFloor(cx, cz, level)) problem = Loc.T("Precisa de um piso");
                    break;
                }
                case PieceKind.Furniture:
                    if (hitPiece == null || (hitPiece.Kind != PieceKind.Foundation && hitPiece.Kind != PieceKind.Floor))
                        problem = Loc.T("Coloque sobre um piso da base");
                    break;
            }
        }

        static bool FloorSupported(int cx, int cz, int level)
        {
            for (int d = 0; d < 4; d++)
            {
                var (axis, ex, ez) = BaseGrid.EdgeKey(cx, cz, d);
                if (BaseGrid.HasWall(axis, ex, ez, level - 1)) return true;
            }
            return BaseGrid.HasFloor(cx + 1, cz, level) || BaseGrid.HasFloor(cx - 1, cz, level) ||
                   BaseGrid.HasFloor(cx, cz + 1, level) || BaseGrid.HasFloor(cx, cz - 1, level);
        }

        // A fundação tem estacas de 2,5 m: aceita terreno até 1,5 m abaixo do topo e 0,9 m acima.
        static bool GroundFits(Vector3 pos)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return true;
            foreach (var o in new[] { Vector2.zero, new Vector2(1.7f, 1.7f), new Vector2(-1.7f, 1.7f), new Vector2(1.7f, -1.7f), new Vector2(-1.7f, -1.7f) })
            {
                float h = terrain.SampleHeight(new Vector3(pos.x + o.x, 0f, pos.z + o.y)) + terrain.transform.position.y;
                if (h > pos.y + 0.9f || h < pos.y - 1.5f) return false;
            }
            return true;
        }

        void Place()
        {
            if (!freeBuild && !inventory.Inventory.TryRemoveAll(selected.cost)) return;
            PlacedObject.Spawn(selected, ghost.transform.position, ghost.transform.rotation);
            Sfx.Build();
        }

        void ShowValidity(bool valid)
        {
            int state = valid ? 1 : 0;
            if (state == shownValidity) return;
            shownValidity = state;
            var mat = valid ? validMaterial : invalidMaterial;
            if (mat == null) return;
            foreach (var r in ghostRenderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        static GameObject CreateGhost(GameObject prefab)
        {
            // Instancia sob um pai inativo para que nenhum Awake/OnEnable rode antes da limpeza.
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            var g = Instantiate(prefab, holder.transform);
            foreach (var b in g.GetComponentsInChildren<MonoBehaviour>(true))
                if (b is not PlacedObject) DestroyImmediate(b);
            foreach (var b in g.GetComponentsInChildren<PlacedObject>(true)) DestroyImmediate(b);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            g.transform.SetParent(null, false);
            g.name = "BuildGhost";
            Destroy(holder);
            return g;
        }
    }
}
