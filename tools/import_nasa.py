"""Converte modelos 3D de domínio público da NASA (github.com/nasa/NASA-3D-Resources, GLB) para FBX do Unity.

Uso: python tools/import_nasa.py <pasta_dos_glb> <pasta_de_saida>
Entradas esperadas em <pasta_dos_glb>: lunar_module.glb, glove.glb
Gera <saida>/lunar_module/lunar_module.fbx (reduzido para ~30 mil triângulos, 9,6 m de largura) e
<saida>/glove/glove.fbx (luva de ~20 cm), com as texturas copiadas ao lado.
Licença: os modelos da NASA são livres e sem copyright (diretrizes de uso da NASA); créditos no CREDITS.md.
"""
import os
import sys

import bpy
from mathutils import Vector


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def decimate(objs, target):
    total = sum(len(o.data.polygons) for o in objs)
    if total <= target:
        return
    ratio = target / total
    for o in objs:
        if len(o.data.polygons) < 30:
            continue
        bpy.context.view_layer.objects.active = o
        mod = o.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = max(0.02, ratio)
        bpy.ops.object.modifier_apply(modifier=mod.name)


def merge_by_material(objs):
    """Junta as peças que usam o mesmo material (o módulo lunar vem com ~130 objetos: importar e desenhar isso é lento)."""
    groups = {}
    for o in objs:
        key = o.data.materials[0].name if o.data.materials and o.data.materials[0] else "_sem_material"
        groups.setdefault(key, []).append(o)
    merged = []
    for key, items in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in items:
            o.select_set(True)
        bpy.context.view_layer.objects.active = items[0]
        if len(items) > 1:
            bpy.ops.object.join()
        result = bpy.context.view_layer.objects.active
        result.name = key
        merged.append(result)
    print(f"[import_nasa] {len(objs)} peças -> {len(merged)} objetos")
    return merged


def prepare(objs, target_size, axis_longest=True):
    """Aplica transformações, reduz, escala para `target_size` m na maior dimensão e assenta a base em z=0, centrado em xy."""
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mn, mx = bounds(objs)
    k = target_size / max(mx - mn)
    for o in objs:
        o.scale = (k, k, k)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mn, mx = bounds(objs)
    shift = Vector(((mn.x + mx.x) / -2, (mn.y + mx.y) / -2, -mn.z))
    for o in objs:
        o.location += shift
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def neutralize_lunar_textures():
    """Tira a bandeira dos EUA e o 'UNITED STATES' das texturas (no jogo a nave é da Estação Horizonte) e clareia o painel escuro."""
    import numpy as np
    for img in bpy.data.images:
        n = img.name.upper()
        if img.type != "IMAGE" or not img.size[0]:
            continue
        px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        if n.startswith("TEXTUREA") or n.startswith("TEXTURE_"):
            px.reshape(-1, 4)[:] = px[-4:].copy()  # o canto superior direito é o dourado liso da manta térmica
        elif n.startswith("BOOSTER3"):
            rgb = px.reshape(-1, 4)
            rgb[:, :3] = np.clip(rgb[:, :3] * 2.2, 0, 1)
        else:
            continue
        if img.packed_file:
            img.unpack(method="REMOVE")  # sem isso o save() grava o arquivo embutido original, ignorando as alterações
        img.pixels.foreach_set(px)
        img.update()


def export(out_dir, name):
    os.makedirs(os.path.join(out_dir, name), exist_ok=True)
    # As texturas vêm embutidas no GLB: grava cada uma ao lado do FBX para o Unity achar.
    folder = os.path.join(out_dir, name)
    for img in bpy.data.images:
        if img.type == "IMAGE" and img.name not in ("Render Result", "Viewer Node"):
            img.filepath_raw = os.path.join(folder, img.name.replace(" ", "_").replace(".", "_") + ".png")
            img.file_format = "PNG"
            img.save()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, name, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="COPY", embed_textures=False)
    tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == "MESH")
    print(f"[import_nasa] {name}: {tris} faces")


def main():
    src, out = sys.argv[1], sys.argv[2]
    objs = load(os.path.join(src, "lunar_module.glb"))
    decimate(objs, 30000)
    objs = merge_by_material(objs)
    prepare(objs, 9.6)
    neutralize_lunar_textures()
    export(out, "lunar_module")
    objs = load(os.path.join(src, "glove.glb"))
    prepare(objs, 0.24)
    export(out, "glove")


if __name__ == "__main__":
    main()
