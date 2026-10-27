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
                Status = "Mire no chão";
                return;
            }

            ghost.transform.SetPositionAndRotation(info.point, Quaternion.Euler(0f, yaw, 0f));
            bool valid = Evaluate(info, out var reason);
            Status = reason;
            ShowValidity(valid);

            if (valid && GameInput.Place.WasPressedThisFrame()) Place();
        }

        bool Evaluate(RaycastHit hit, out string reason)
        {
            var tm = TerraformManager.Instance;
            if (tm != null && tm.Ti < selected.requiredTi)
            {
                reason = $"Requer {TiFormat.Format(selected.requiredTi)}";
                return false;
            }
            if (selected.unique && System.Linq.Enumerable.Any(PlacedObject.All, p => p.Definition == selected))
            {
                reason = "Só é possível ter um";
                return false;
            }
            if (!Blueprints.Has(selected.blueprintId))
            {
                reason = "Requer microchip das ruínas";
                return false;
            }
            if (hit.normal.y < minGroundNormalY)
            {
                reason = "Terreno muito inclinado";
                return false;
            }
            if (!freeBuild && !inventory.Inventory.Has(selected.cost))
            {
                reason = "Recursos insuficientes";
                return false;
            }
            reason = "[LMB] construir   [R/scroll] girar   [RMB] cancelar";
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
