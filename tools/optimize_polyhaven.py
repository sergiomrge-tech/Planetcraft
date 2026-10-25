"""Converte os FBX brutos do Poly Haven (qualidade de render) em versões para jogo.

Uso (ambiente com bpy):  python tools/optimize_polyhaven.py
Lê ThirdPartySource/PolyHaven/Models/<id>/ e grava Assets/ThirdParty/PolyHaven/Models/<id>/:
  - <id>.fbx com objetos <id>_LOD0 e <id>_LOD1 (o Unity cria o LODGroup pelos nomes);
  - texturas por material: <mat>_diff (com alfa juntado quando há <mat>_alpha) e <mat>_nor_gl.
Partes sólidas: decimação por colapso até o orçamento. Folhas: mantém uma fração dos cachos
(ilhas de faces) e aumenta os que ficam, para a copa continuar cheia com bem menos polígonos.
"""
import bpy  # precisa vir antes de bmesh/mathutils no pacote bpy do PyPI
import bmesh
import os
import random
import shutil
import sys

import numpy as np
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ThirdPartySource", "PolyHaven", "Models")
DST = os.path.join(ROOT, "Assets", "ThirdParty", "PolyHaven", "Models")

# id: (triângulos alvo do LOD0 para partes sólidas, fração de cachos de folha mantida, escala dos cachos,
#      tudo é folhagem?) — folhagem com textura recortada não pode ser colapsada: só raleada.
ASSETS = {
    "boulder_01": (6000, None, 1.0, False),
    "namaqualand_boulder_02": (6000, None, 1.0, False),
    "namaqualand_boulder_05": (6000, None, 1.0, False),
    "dead_tree_trunk": (8000, None, 1.0, False),
    "dead_tree_trunk_02": (8000, None, 1.0, False),
    "tree_stump_01": (5000, None, 1.0, False),
    "shrub_02": (0, 0.5, 1.2, True),
    "fern_02": (0, 1.0, 1.0, True),
    "jacaranda_tree": (9000, 0.03, 4.5, False),
    "tree_small_02": (6000, 0.04, 4.0, False),
}
LEAF_WORDS = ("leaves", "leaf", "twig", "foliage", "branches")
LOD1_FACTOR = 0.3


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def is_leaf_material(name):
    return any(w in name.lower() for w in LEAF_WORDS)


def split_leaves(ob):
    """Separa as faces de material de folha num objeto próprio; devolve (sólido, folhas|None)."""
    leaf_slots = [i for i, m in enumerate(ob.data.materials) if m and is_leaf_material(m.name)]
    if not leaf_slots:
        return ob, None
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    for slot in leaf_slots:
        ob.active_material_index = slot
        bpy.ops.object.material_slot_select()
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    leaves = [o for o in bpy.context.selected_objects if o != ob][0]
    return ob, leaves


def thin_leaves(ob, keep, scale, seed=7):
    """Mantém `keep` dos cachos (ilhas) de folhas, aumentando cada um em torno do próprio centro."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    seen = set()
    islands = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, island = [f], []
        seen.add(f.index)
        while stack:
            cur = stack.pop()
            island.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in seen:
                        seen.add(nf.index)
                        stack.append(nf)
        islands.append(island)
    # Sorteia cachos até atingir `keep` dos triângulos: cachos enormes não estouram o orçamento.
    rng = random.Random(seed)
    rng.shuffle(islands)
    budget = keep * sum(len(f.verts) - 2 for isl in islands for f in isl)
    used = 0
    doomed = []
    for island in islands:
        size = sum(len(f.verts) - 2 for f in island)
        if used + size > budget:
            doomed.extend(island)
            continue
        used += size
        verts = {v for face in island for v in face.verts}
        c = sum((v.co for v in verts), Vector()) / len(verts)
        for v in verts:
            v.co = c + (v.co - c) * scale
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    print(f"    folhas: {len(islands)} cachos, {int(used)} triângulos mantidos")


def decimate(ob, target):
    current = tris(ob)
    if current <= target:
        return
    mod = ob.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = max(0.001, target / current)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)


def merge_alpha(diff_path, alpha_path, out_path):
    diff = bpy.data.images.load(diff_path)
    alpha = bpy.data.images.load(alpha_path)
    w, h = diff.size
    d = np.empty(w * h * 4, dtype=np.float32)
    diff.pixels.foreach_get(d)
    a = np.empty(w * h * 4, dtype=np.float32)
    alpha.pixels.foreach_get(a)
    d[3::4] = a[0::4]
    out = bpy.data.images.new("merged", w, h, alpha=True)
    out.pixels.foreach_set(d)
    out.filepath_raw = out_path
    out.file_format = "PNG"
    out.save()


def texture_prefixes(material):
    """O Poly Haven nem sempre nomeia a textura igual ao material: `tree_small_02_branches` usa
    `tree_small_02_branch_*` e o tronco `tree_small_02_trunk` usa só `tree_small_02_*`."""
    yield material
    if material.endswith("es"):
        yield material[:-2]
    if material.endswith("s"):
        yield material[:-1]
    if "_" in material:
        yield material.rsplit("_", 1)[0]


def find_texture(folder, material, kind):
    for prefix in texture_prefixes(material):
        for ext in (".png", ".jpg", ".exr"):
            p = os.path.join(folder, f"{prefix}_{kind}_1k{ext}")
            if os.path.exists(p):
                return p
    return None


def copy_textures(asset_id, materials, out_dir):
    src_tex = os.path.join(SRC, asset_id, "textures")
    os.makedirs(out_dir, exist_ok=True)
    for mat in materials:
        diff = find_texture(src_tex, mat, "diff")
        alpha = find_texture(src_tex, mat, "alpha")
        normal = find_texture(src_tex, mat, "nor_gl")
        if diff and alpha:
            merge_alpha(diff, alpha, os.path.join(out_dir, f"{mat}_diff.png"))
        elif diff:
            shutil.copy(diff, os.path.join(out_dir, f"{mat}_diff{os.path.splitext(diff)[1]}"))
        if normal:
            shutil.copy(normal, os.path.join(out_dir, f"{mat}_nor_gl{os.path.splitext(normal)[1]}"))
        print(f"    material {mat}: diff={'+alpha' if alpha else bool(diff)} normal={bool(normal)}")


def build_lod(objs, name, solid_budget, keep, scale, all_foliage):
    """Junta tudo num objeto (sólido decimado + folhas raleadas) chamado `name`."""
    solids, foliage = [], []
    for ob in objs:
        if all_foliage:
            foliage.append(ob)
            continue
        solid, leaves = split_leaves(ob)
        solids.append(solid)
        if leaves is not None:
            foliage.append(leaves)
    for f in foliage:
        if keep is not None and keep < 1.0:
            thin_leaves(f, keep, scale)
    total_solid = sum(tris(p) for p in solids) or 1
    for p in solids:
        decimate(p, solid_budget * tris(p) / total_solid)
    parts = solids + foliage
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    result = bpy.context.view_layer.objects.active
    result.name = name
    result.data.name = name
    return result


def variant_of(asset_id, name):
    """Pacotes como shrub_02 trazem várias plantas lado a lado (shrub_02_a, _b...); devolve a letra."""
    rest = name[len(asset_id):].split(".")[0].replace("_LOD0", "").strip("_")
    return rest if len(rest) == 1 else None


def source_variants(asset_id):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, asset_id, f"{asset_id}_1k.fbx"))
    found = {variant_of(asset_id, o.name) for o in bpy.context.scene.objects if o.type == "MESH"}
    return sorted(found) if None not in found and len(found) > 1 else [None]


def optimize_all(asset_id, solid_budget, keep, scale, all_foliage):
    # Cada variante vira um modelo próprio (<id>_<letra>), centrado na origem, para o jogo sortear.
    for variant in source_variants(asset_id):
        optimize(asset_id, solid_budget, keep, scale, all_foliage, variant)


def optimize(asset_id, solid_budget, keep, scale, all_foliage, variant=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, asset_id, f"{asset_id}_1k.fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # Usa só o LOD0 de cada parte quando o modelo já vem com LODs.
    lod0 = [o for o in meshes if ("_LOD" not in o.name or o.name.endswith("_LOD0"))
            and (variant is None or variant_of(asset_id, o.name) == variant)]
    for o in meshes:
        if o not in lod0:
            bpy.data.objects.remove(o, do_unlink=True)
    src_id = asset_id
    if variant is not None:
        for o in lod0:
            o.location.x = o.location.y = 0.0
        asset_id = f"{asset_id}_{variant}"
    bpy.ops.object.select_all(action="DESELECT")
    for o in lod0:  # aplica transformações para o LOD1 copiar certo
        o.select_set(True)
    bpy.context.view_layer.objects.active = lod0[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    materials = sorted({m.name for o in lod0 for m in o.data.materials if m})

    copies = []
    for o in lod0:
        c = o.copy()
        c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        copies.append(c)
    lod0_obj = build_lod(lod0, f"{asset_id}_LOD0", solid_budget, keep, scale, all_foliage)
    lod1_obj = build_lod(copies, f"{asset_id}_LOD1", solid_budget * LOD1_FACTOR,
                         None if keep is None else keep * 0.25, scale * 1.6, all_foliage)

    out_dir = os.path.join(DST, asset_id)
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, f"{asset_id}.fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP")
    copy_textures(src_id, materials, os.path.join(out_dir, "textures"))
    print(f"[optimize] {asset_id}: LOD0={tris(lod0_obj)} LOD1={tris(lod1_obj)} materiais={materials}")
    sys.stdout.flush()


def main():
    names = sys.argv[1:] or list(ASSETS)
    for asset_id in names:
        optimize_all(asset_id, *ASSETS[asset_id])


if __name__ == "__main__":
    main()
