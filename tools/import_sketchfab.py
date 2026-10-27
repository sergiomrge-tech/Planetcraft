"""Prepara os animais baixados do Sketchfab (CC BY) para o Unity.

Uso (ambiente com o pacote `bpy`): python tools/import_sketchfab.py [id ...]
Entrada: ThirdPartySource/Sketchfab/<id>/ com o zip do Sketchfab ou os arquivos já extraídos (FBX ou glTF).
Saída: Assets/ThirdParty/Sketchfab/<id>/<id>.fbx com as animações e as texturas ao lado (pasta textures/).
glTF/GLB é convertido para FBX pelo Blender (o Unity não importa glTF sem pacote extra).
"""
import glob
import os
import shutil
import sys
import zipfile

import bpy  # precisa vir antes de qualquer outro módulo do Blender

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ThirdPartySource", "Sketchfab")
DST = os.path.join(ROOT, "Assets", "ThirdParty", "Sketchfab")
IDS = ["deer", "wolf"]
TEXTURE_EXT = (".png", ".jpg", ".jpeg", ".tga")


def extract(folder):
    for z in glob.glob(os.path.join(folder, "*.zip")):
        out = os.path.join(folder, "extracted", os.path.splitext(os.path.basename(z))[0])
        if not os.path.isdir(out):
            with zipfile.ZipFile(z) as f:
                f.extractall(out)


def find(folder, exts):
    hits = [p for p in glob.glob(os.path.join(folder, "**", "*"), recursive=True) if p.lower().endswith(exts)]
    return sorted(hits, key=len)


def convert_gltf(src, out_fbx, tex_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    # Extrai as texturas embutidas para o Unity encontrar pelos materiais.
    for img in bpy.data.images:
        if img.packed_file is None and not img.filepath:
            continue
        name = bpy.path.clean_name(img.name) + ".png"
        img.filepath_raw = os.path.join(tex_dir, name)
        img.file_format = "PNG"
        img.save()
    bpy.ops.export_scene.fbx(
        filepath=out_fbx, use_selection=False, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        path_mode="STRIP")


def prepare(asset_id):
    folder = os.path.join(SRC, asset_id)
    if not os.path.isdir(folder):
        print(f"[sketchfab] {asset_id}: pasta {folder} não existe")
        return
    extract(folder)
    out = os.path.join(DST, asset_id)
    tex_dir = os.path.join(out, "textures")
    os.makedirs(tex_dir, exist_ok=True)
    out_fbx = os.path.join(out, f"{asset_id}.fbx")

    fbx = find(folder, (".fbx",))
    gltf = find(folder, (".gltf", ".glb"))
    if fbx:
        shutil.copy(fbx[0], out_fbx)
        source = fbx[0]
    elif gltf:
        convert_gltf(gltf[0], out_fbx, tex_dir)
        source = gltf[0]
    else:
        print(f"[sketchfab] {asset_id}: nenhum FBX ou glTF em {folder}")
        return
    for tex in find(folder, TEXTURE_EXT):
        shutil.copy(tex, os.path.join(tex_dir, os.path.basename(tex)))
    print(f"[sketchfab] {asset_id}: {os.path.relpath(source, folder)} -> {os.path.relpath(out_fbx, ROOT)}")


def main():
    for asset_id in sys.argv[1:] or IDS:
        prepare(asset_id)


if __name__ == "__main__":
    main()
