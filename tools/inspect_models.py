"""Mostra triângulos por objeto/material dos FBX brutos (para decidir o orçamento de decimação)."""
import bpy  # precisa vir antes de bmesh/mathutils no pacote bpy do PyPI
import os
import sys

src = sys.argv[1]
for asset_id in sys.argv[2:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(src, asset_id, f"{asset_id}_1k.fbx")
    bpy.ops.import_scene.fbx(filepath=path)
    total = 0
    for ob in bpy.context.scene.objects:
        if ob.type != "MESH":
            continue
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        total += tris
        mats = [m.name for m in ob.data.materials if m]
        print(f"  {asset_id}: {ob.name} tris={tris} mats={mats}")
    print(f"{asset_id}: TOTAL {total}")
    sys.stdout.flush()
