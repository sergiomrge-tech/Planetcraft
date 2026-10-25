"""Gera os modelos 3D do Planetcraft com bpy/bmesh e exporta FBX para o Unity.

Uso (ambiente com o pacote `bpy`):
    python tools/gen_models.py <pasta_de_saida> [nome ...]

Sem nomes, gera tudo. Os FBX vão para <saida>/<Categoria>/<nome>.fbx.
Convenções: unidades em metros, base em z=0 (Blender Z-up), frente do modelo em +Y.
Nomes especiais de objeto: "Rotor" (gira, pivô no centro), "Indicator" (luz de funcionamento).
Nomes especiais de material: "glow_*" (emissivo) e "glass_*" (translúcido) são tratados no Unity.
"""
import math
import os
import random
import sys

import bpy  # precisa vir antes de bmesh/mathutils no pacote bpy do PyPI
import bmesh
from mathutils import Euler, Matrix, Vector

I4 = Matrix.Identity(4)


def R(rx=0, ry=0, rz=0):
    return Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ").to_matrix().to_4x4()


def T(x=0, y=0, z=0):
    return Matrix.Translation((x, y, z))


def S(x=1, y=1, z=1):
    return Matrix.Diagonal((x, y, z, 1.0))


def aim(a, b):
    d = (Vector(b) - Vector(a)).normalized()
    return Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()


class Model:
    def __init__(self, name, category):
        self.name = name
        self.category = category
        self.mats = {}
        self.bms = {}
        self.origins = {}

    def material(self, name, color, rough=0.7, metal=0.0):
        self.mats[name] = (color, rough, metal)

    # ---- acumulação de geometria -------------------------------------------------
    def _emit(self, mat, obj, tmp, base):
        key = (obj or self.name, mat)
        bm = self.bms.setdefault(key, bmesh.new())
        tmp.verts.index_update()
        vmap = [bm.verts.new(base @ v.co) for v in tmp.verts]
        for f in tmp.faces:
            try:
                nf = bm.faces.new([vmap[v.index] for v in f.verts])
                nf.smooth = f.smooth
            except ValueError:
                pass
        tmp.free()

    def box(self, mat, pos, size, rot=(0, 0, 0), bevel=0.0, taper=None, base=I4, obj=None):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.transform(tmp, matrix=S(*size), verts=tmp.verts)
        if taper:
            for v in tmp.verts:
                if v.co.z > 0:
                    v.co.x *= taper[0]
                    v.co.y *= taper[1]
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=list(tmp.verts) + list(tmp.edges) + list(tmp.faces),
                            offset=bevel, segments=2, affect="EDGES")
        bmesh.ops.transform(tmp, matrix=T(*pos) @ R(*rot), verts=tmp.verts)
        self._emit(mat, obj, tmp, base)

    def cyl(self, mat, pos, r1, r2, h, seg=12, rot=(0, 0, 0), smooth=True, M=None, base=I4, obj=None):
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=seg,
                              radius1=r1, radius2=r2, depth=h)
        tmp.faces.ensure_lookup_table()
        for f in tmp.faces:
            f.smooth = smooth and abs(f.normal.z) < 0.9
        bmesh.ops.transform(tmp, matrix=M if M is not None else T(*pos) @ R(*rot), verts=tmp.verts)
        self._emit(mat, obj, tmp, base)

    def tube(self, mat, a, b, r1, r2=None, seg=8, base=I4, obj=None):
        a, b = Vector(a), Vector(b)
        self.cyl(mat, None, r1, r1 if r2 is None else r2, (b - a).length, seg=seg,
                 M=T(*((a + b) / 2)) @ aim(a, b), base=base, obj=obj)

    def blob(self, mat, pos, radii, sub=2, noise=0.15, seed=0, rot=(0, 0, 0), smooth=False, base=I4, obj=None):
        rng = random.Random(seed)
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=1.0)
        for v in tmp.verts:
            v.co *= 1.0 + rng.uniform(-noise, noise)
        for f in tmp.faces:
            f.smooth = smooth
        bmesh.ops.transform(tmp, matrix=T(*pos) @ R(*rot) @ S(*radii), verts=tmp.verts)
        self._emit(mat, obj, tmp, base)

    def lathe(self, mat, pos, profile, seg=12, smooth=True, base=I4, obj=None, caps=True):
        tmp = bmesh.new()
        rings = []
        for r, z in profile:
            if r < 1e-4:
                rings.append([tmp.verts.new((0, 0, z))])
            else:
                rings.append([tmp.verts.new((r * math.cos(2 * math.pi * i / seg),
                                             r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
        for lo, hi in zip(rings, rings[1:]):
            for i in range(seg):
                j = (i + 1) % seg
                quad = []
                for v in (lo[i % len(lo)], lo[j % len(lo)], hi[j % len(hi)], hi[i % len(hi)]):
                    if v not in quad:
                        quad.append(v)
                if len(quad) >= 3:
                    f = tmp.faces.new(quad)
                    f.smooth = smooth
        if caps and len(rings[0]) > 1:
            tmp.faces.new(list(reversed(rings[0])))
        if caps and len(rings[-1]) > 1:
            tmp.faces.new(rings[-1])
        bmesh.ops.transform(tmp, matrix=T(*pos), verts=tmp.verts)
        self._emit(mat, obj, tmp, base)

    # ---- criação dos objetos Blender ---------------------------------------------
    def build(self):
        scene = bpy.context.scene
        bpy_mats = {}
        for name, (color, rough, metal) in self.mats.items():
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            b = m.node_tree.nodes["Principled BSDF"]
            b.inputs["Base Color"].default_value = (*color, 1.0)
            b.inputs["Roughness"].default_value = rough
            b.inputs["Metallic"].default_value = metal
            if name.startswith("glow_"):
                b.inputs["Emission Color"].default_value = (*color, 1.0)
                b.inputs["Emission Strength"].default_value = 2.0
            m.diffuse_color = (*color, 1.0)
            bpy_mats[name] = m

        by_obj = {}
        for (obj, mat), bm in self.bms.items():
            by_obj.setdefault(obj, []).append((mat, bm))

        for objname, parts in by_obj.items():
            combined = bmesh.new()
            slots = []
            for slot, (mat, bm) in enumerate(parts):
                slots.append(mat)
                bm.verts.index_update()
                vmap = [combined.verts.new(v.co) for v in bm.verts]
                for f in bm.faces:
                    nf = combined.faces.new([vmap[v.index] for v in f.verts])
                    nf.smooth = f.smooth
                    nf.material_index = slot
                bm.free()
            origin = Vector(self.origins.get(objname, (0, 0, 0)))
            for v in combined.verts:
                v.co -= origin
            bmesh.ops.recalc_face_normals(combined, faces=combined.faces)
            mesh = bpy.data.meshes.new(objname)
            combined.to_mesh(mesh)
            combined.free()
            for mat in slots:
                mesh.materials.append(bpy_mats[mat])
            ob = bpy.data.objects.new(objname, mesh)
            ob.location = origin
            scene.collection.objects.link(ob)

    def export(self, out_dir):
        folder = os.path.join(out_dir, self.category)
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, self.name + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=False, object_types={"MESH"},
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
            bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)
        return path


# =============================================================================
# Natureza
# =============================================================================
LEAVES = [((0.17, 0.46, 0.19), (0.26, 0.55, 0.21)),
          ((0.30, 0.53, 0.17), (0.42, 0.58, 0.20)),
          ((0.14, 0.40, 0.24), (0.22, 0.50, 0.28))]


def tree_broad(variant):
    m = Model(f"tree_broad_{variant}", "Nature")
    rng = random.Random(100 + variant)
    la, lb = LEAVES[variant % 3]
    m.material("bark", (0.33, 0.21, 0.12), 0.95)
    m.material("leaf_a", la, 0.85)
    m.material("leaf_b", lb, 0.85)
    m.lathe("bark", (0, 0, 0), [(0.52, 0), (0.34, 0.25), (0.25, 0.9), (0.21, 2.3), (0.17, 3.3)], seg=9)
    for i in range(4):
        a = math.radians(i * 90 + rng.uniform(-25, 25))
        m.tube("bark", (math.cos(a) * 0.2, math.sin(a) * 0.2, 0.55),
               (math.cos(a) * 0.75, math.sin(a) * 0.75, 0.02), 0.13, 0.05, seg=6)
    for i in range(3):
        a = math.radians(i * 120 + rng.uniform(-20, 20))
        m.tube("bark", (0, 0, 2.7), (math.cos(a) * 1.0, math.sin(a) * 1.0, 3.7), 0.11, 0.05, seg=6)
    spots = [(0, 0, 4.5, 1.55)]
    for i in range(5):
        a = math.radians(i * 72 + rng.uniform(-15, 15))
        spots.append((math.cos(a) * 1.15, math.sin(a) * 1.15, 3.9 + rng.uniform(0, 0.9), rng.uniform(0.95, 1.3)))
    for i, (x, y, z, r) in enumerate(spots):
        m.blob("leaf_a" if i % 2 else "leaf_b", (x, y, z), (r, r, r * 0.88), sub=2, noise=0.17, seed=variant * 31 + i)
    return m


def tree_conifer(variant):
    m = Model(f"tree_conifer_{variant}", "Nature")
    rng = random.Random(200 + variant)
    m.material("bark", (0.30, 0.20, 0.12), 0.95)
    m.material("pine_a", (0.09, 0.32, 0.17) if variant == 0 else (0.12, 0.36, 0.20), 0.85)
    m.material("pine_b", (0.14, 0.40, 0.21) if variant == 0 else (0.18, 0.44, 0.24), 0.85)
    m.lathe("bark", (0, 0, 0), [(0.4, 0), (0.22, 0.4), (0.16, 2.0), (0.1, 3.2)], seg=8)
    tiers = 5 + variant
    for i in range(tiers):
        t = i / (tiers - 1)
        z = 1.2 + i * (4.2 / tiers)
        r = 1.9 - t * 1.25
        m.cyl("pine_a" if i % 2 == 0 else "pine_b", (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), z + 0.7),
              r, 0.04, 1.7, seg=8, rot=(0, 0, rng.uniform(0, 45)), smooth=False)
    return m


def bush():
    m = Model("bush", "Nature")
    rng = random.Random(7)
    m.material("leaf_a", (0.22, 0.52, 0.2), 0.85)
    m.material("leaf_b", (0.32, 0.58, 0.22), 0.85)
    m.material("berry", (0.75, 0.12, 0.18), 0.4)
    for i in range(5):
        a = math.radians(i * 72)
        r = rng.uniform(0.38, 0.55)
        m.blob("leaf_a" if i % 2 else "leaf_b", (math.cos(a) * 0.45, math.sin(a) * 0.45, r * 0.8), (r, r, r * 0.85),
               sub=2, noise=0.2, seed=i)
    m.blob("leaf_b", (0, 0, 0.7), (0.6, 0.6, 0.55), sub=2, noise=0.18, seed=9)
    for i in range(7):
        a = rng.uniform(0, 6.28)
        m.blob("berry", (math.cos(a) * 0.7, math.sin(a) * 0.7, rng.uniform(0.35, 0.9)), (0.07, 0.07, 0.07), sub=1, noise=0, seed=i)
    return m


def moss():
    m = Model("moss", "Nature")
    m.material("moss_a", (0.24, 0.42, 0.14), 0.95)
    m.material("moss_b", (0.34, 0.5, 0.18), 0.95)
    m.blob("moss_a", (0, 0, 0), (1.4, 1.4, 0.32), sub=2, noise=0.18, seed=3)
    m.blob("moss_b", (0.4, 0.2, 0.05), (0.7, 0.6, 0.3), sub=2, noise=0.22, seed=4)
    m.blob("moss_b", (-0.5, -0.3, 0.04), (0.6, 0.55, 0.28), sub=2, noise=0.22, seed=5)
    return m


# =============================================================================
# Minérios
# =============================================================================
def ore_iron():
    m = Model("ore_iron", "Ore")
    m.material("rock", (0.30, 0.22, 0.20), 0.9)
    m.material("rust", (0.62, 0.3, 0.14), 0.7)
    m.blob("rock", (0, 0, 0.25), (0.5, 0.42, 0.34), sub=1, noise=0.28, seed=1)
    m.blob("rock", (0.38, 0.1, 0.17), (0.3, 0.28, 0.22), sub=1, noise=0.25, seed=2)
    m.blob("rust", (-0.3, 0.2, 0.18), (0.28, 0.22, 0.2), sub=1, noise=0.28, seed=3)
    m.blob("rust", (0.05, 0.05, 0.52), (0.2, 0.18, 0.12), sub=1, noise=0.3, seed=4)
    return m


def crystals(name, base, tip, rough, metal, seeds, glow=False):
    m = Model(name, "Ore")
    crystal, crystal_tip = ("glow_crystal", "glow_crystal_tip") if glow else ("crystal", "crystal_tip")
    m.material("base_rock", (0.27, 0.27, 0.3), 0.9)
    m.material(crystal, base, rough, metal)
    m.material(crystal_tip, tip, rough, metal)
    m.blob("base_rock", (0, 0, 0.1), (0.5, 0.48, 0.18), sub=1, noise=0.25, seed=seeds)
    rng = random.Random(seeds)
    for i in range(6):
        a = math.radians(i * 60 + rng.uniform(-15, 15))
        d = Vector((math.cos(a) * 0.35, math.sin(a) * 0.35, 1.0)).normalized() if i else Vector((0, 0, 1))
        length = rng.uniform(0.5, 0.95) if i else 1.15
        b = Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, 0.12)) if i else Vector((0, 0, 0.12))
        tip_pos = b + d * length
        m.tube(crystal, b, tip_pos - d * 0.12, 0.1, 0.085, seg=6)
        m.tube(crystal_tip, tip_pos - d * 0.12, tip_pos, 0.085, 0.01, seg=6)
    return m


def ore_magnesium():
    m = Model("ore_magnesium", "Ore")
    m.material("stone", (0.82, 0.8, 0.74), 0.5)
    m.material("stone_b", (0.68, 0.66, 0.62), 0.55)
    for i, (x, y, r) in enumerate([(0, 0, 0.38), (0.45, 0.15, 0.27), (-0.3, 0.32, 0.23)]):
        m.blob("stone" if i != 1 else "stone_b", (x, y, r * 0.8), (r, r, r * 0.85), sub=2, noise=0.07, seed=i + 20, smooth=True)
    return m


def ore_titanium():
    m = Model("ore_titanium", "Ore")
    m.material("base_rock", (0.26, 0.26, 0.28), 0.9)
    m.material("ti", (0.82, 0.84, 0.88), 0.25, 0.9)
    m.blob("base_rock", (0, 0, 0.18), (0.52, 0.45, 0.26), sub=1, noise=0.25, seed=30)
    for i, (x, y, z, s) in enumerate([(0.05, 0, 0.45, 0.3), (0.3, 0.15, 0.3, 0.2), (-0.28, -0.12, 0.34, 0.22)]):
        m.box("ti", (x, y, z), (s, s * 0.8, s * 1.1), rot=(i * 25, i * 40, i * 70), bevel=0.03)
    return m


# =============================================================================
# Fauna (frente em +Y)
# =============================================================================
def grazer():
    m = Model("grazer", "Fauna")
    m.material("fur", (0.50, 0.34, 0.2), 0.9)
    m.material("belly", (0.82, 0.72, 0.55), 0.9)
    m.material("dark", (0.14, 0.11, 0.09), 0.8)
    m.material("horn", (0.7, 0.62, 0.48), 0.8)
    m.blob("fur", (0, 0, 1.18), (0.42, 0.95, 0.46), sub=2, noise=0.04, seed=1, smooth=True)
    m.blob("belly", (0, 0.05, 1.0), (0.34, 0.8, 0.3), sub=2, noise=0.03, seed=2, smooth=True)
    m.blob("fur", (0, 0.6, 1.3), (0.4, 0.45, 0.45), sub=2, noise=0.04, seed=3, smooth=True)
    m.tube("fur", (0, 0.85, 1.5), (0, 1.25, 2.05), 0.2, 0.14, seg=8)
    m.blob("fur", (0, 1.38, 2.12), (0.17, 0.27, 0.19), sub=2, noise=0.03, seed=4, smooth=True)
    m.blob("belly", (0, 1.6, 2.02), (0.1, 0.12, 0.09), sub=2, noise=0.02, seed=5, smooth=True)
    m.blob("dark", (0, 1.7, 2.04), (0.05, 0.04, 0.04), sub=1, noise=0, seed=6)
    for sx in (-1, 1):
        m.blob("dark", (sx * 0.13, 1.5, 2.17), (0.035, 0.035, 0.035), sub=1, noise=0, seed=7)
        m.blob("fur", (sx * 0.17, 1.28, 2.3), (0.05, 0.1, 0.17), sub=1, noise=0.03, seed=8, rot=(0, sx * 25, 0))
        a = (sx * 0.1, 1.3, 2.28)
        m.tube("horn", a, (sx * 0.22, 1.2, 2.75), 0.035, 0.025, seg=5)
        m.tube("horn", (sx * 0.22, 1.2, 2.75), (sx * 0.34, 1.3, 3.05), 0.025, 0.012, seg=5)
        m.tube("horn", (sx * 0.2, 1.22, 2.68), (sx * 0.3, 1.1, 2.95), 0.02, 0.01, seg=5)
    for sx in (-1, 1):
        for fy, back in ((0.62, False), (-0.62, True)):
            hip = (sx * 0.24, fy, 1.05)
            knee = (sx * 0.26, fy + (-0.12 if back else 0.04), 0.58)
            hoof = (sx * 0.26, fy + (0.02 if back else 0.04), 0.1)
            m.tube("fur", hip, knee, 0.13, 0.09, seg=7)
            m.tube("fur", knee, hoof, 0.08, 0.05, seg=7)
            m.cyl("dark", (hoof[0], hoof[1], 0.05), 0.065, 0.075, 0.1, seg=7)
    m.blob("belly", (0, -0.98, 1.3), (0.1, 0.12, 0.14), sub=1, noise=0.05, seed=9, rot=(25, 0, 0))
    return m


def hopper():
    m = Model("hopper", "Fauna")
    m.material("fur", (0.86, 0.83, 0.78), 0.95)
    m.material("pink", (0.9, 0.62, 0.62), 0.9)
    m.material("dark", (0.1, 0.08, 0.08), 0.6)
    m.blob("fur", (0, 0, 0.27), (0.2, 0.34, 0.24), sub=2, noise=0.03, seed=1, smooth=True)
    m.blob("fur", (0, 0.3, 0.38), (0.14, 0.16, 0.14), sub=2, noise=0.03, seed=2, smooth=True)
    for sx in (-1, 1):
        m.blob("fur", (sx * 0.065, 0.3, 0.68), (0.035, 0.05, 0.19), sub=1, noise=0.03, seed=3, rot=(0, sx * 12, 0), smooth=True)
        m.blob("pink", (sx * 0.065, 0.315, 0.66), (0.018, 0.03, 0.14), sub=1, noise=0.0, seed=4, rot=(0, sx * 12, 0))
        m.blob("dark", (sx * 0.085, 0.42, 0.42), (0.02, 0.02, 0.02), sub=1, noise=0, seed=5)
        m.blob("fur", (sx * 0.14, -0.05, 0.14), (0.07, 0.17, 0.12), sub=1, noise=0.03, seed=6, smooth=True)
        m.blob("fur", (sx * 0.07, 0.34, 0.07), (0.035, 0.07, 0.04), sub=1, noise=0.03, seed=7, smooth=True)
    m.blob("pink", (0, 0.45, 0.37), (0.025, 0.02, 0.02), sub=1, noise=0, seed=8)
    m.blob("fur", (0, -0.36, 0.3), (0.075, 0.075, 0.075), sub=1, noise=0.02, seed=9, smooth=True)
    return m


# =============================================================================
# Construções
# =============================================================================
def base_module():
    m = Model("base_module", "Machines")
    m.material("wall", (0.86, 0.86, 0.82), 0.6)
    m.material("trim", (0.28, 0.3, 0.33), 0.5, 0.4)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("glass_win", (0.5, 0.75, 0.9), 0.1)
    m.material("glow_lamp", (1.0, 0.95, 0.75), 0.4)
    m.material("floor", (0.45, 0.46, 0.48), 0.8)
    m.box("floor", (0, 0, 0.1), (4.0, 4.0, 0.2), bevel=0.02)
    m.box("trim", (0, 0, 3.12), (4.3, 4.3, 0.22), bevel=0.04)
    m.box("wall", (0, 1.925, 1.6), (4.0, 0.15, 2.8))
    m.box("wall", (-1.925, 0, 1.6), (0.15, 3.7, 2.8))
    m.box("wall", (1.925, 0, 1.6), (0.15, 3.7, 2.8))
    for sx in (-1, 1):
        m.box("wall", (sx * 1.35, -1.925, 1.6), (1.3, 0.15, 2.8))
    m.box("wall", (0, -1.925, 2.75), (1.4, 0.15, 0.5))
    m.box("trim", (-0.72, -1.93, 1.2), (0.08, 0.2, 2.0))
    m.box("trim", (0.72, -1.93, 1.2), (0.08, 0.2, 2.0))
    m.box("trim", (0, -1.93, 2.18), (1.52, 0.2, 0.08))
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("trim", (sx * 1.98, sy * 1.98, 1.65), (0.2, 0.2, 2.95), bevel=0.03)
    m.box("accent", (0, 1.99, 0.45), (3.9, 0.04, 0.3))
    for sx in (-1, 1):
        m.box("accent", (sx * 1.99, 0, 0.45), (0.04, 3.7, 0.3))
        m.box("trim", (sx * 1.99, 0, 1.75), (0.06, 1.5, 0.95), bevel=0.02)
        m.box("glass_win", (sx * 2.01, 0, 1.75), (0.04, 1.36, 0.82))
    m.box("trim", (0, 2.0, 1.75), (1.5, 0.06, 0.95), bevel=0.02)
    m.box("glass_win", (0, 2.02, 1.75), (1.36, 0.04, 0.82))
    m.box("glow_lamp", (0, -2.0, 2.45), (0.6, 0.06, 0.1))
    m.box("glow_lamp", (0, 0, 2.9), (2.2, 0.3, 0.06))
    m.box("trim", (1.1, 1.0, 3.35), (0.9, 0.9, 0.3), bevel=0.04)
    m.cyl("trim", (-1.1, -1.0, 3.5), 0.22, 0.22, 0.5, seg=10)
    m.tube("trim", (-1.4, 1.3, 3.2), (-1.4, 1.3, 4.8), 0.03, 0.02, seg=6)
    m.blob("accent", (-1.4, 1.3, 4.85), (0.07, 0.07, 0.07), sub=1, noise=0, seed=1)
    return m


def wind_turbine():
    m = Model("wind_turbine", "Machines")
    m.material("white", (0.92, 0.93, 0.95), 0.4)
    m.material("grey", (0.45, 0.47, 0.5), 0.5, 0.5)
    m.material("red", (0.85, 0.15, 0.12), 0.5)
    m.cyl("grey", (0, 0, 0.12), 0.85, 0.85, 0.24, seg=14)
    m.lathe("white", (0, 0, 0.2), [(0.42, 0), (0.3, 3.5), (0.2, 7.0)], seg=14)
    m.box("white", (0, 0, 7.35), (0.55, 1.5, 0.55), bevel=0.08)
    m.box("grey", (0, 0.7, 7.35), (0.4, 0.3, 0.4), bevel=0.05)
    m.cyl("red", (0, -0.78, 7.35), 0.3, 0.2, 0.3, seg=12, rot=(90, 0, 0))
    hub = (0, -0.95, 7.35)
    m.origins["Rotor"] = hub
    m.blob("grey", hub, (0.26, 0.26, 0.26), sub=2, noise=0, seed=1, smooth=True, obj="Rotor")
    for i in range(3):
        rot = R(0, i * 120, 0)
        m.box("white", (0, 0, 0), (0.34, 0.08, 3.3), taper=(0.35, 1.0), obj="Rotor",
              base=T(*hub) @ rot @ T(0, 0, 1.95))
        m.box("red", (0, 0, 0), (0.14, 0.09, 0.4), obj="Rotor", base=T(*hub) @ rot @ T(0, 0, 3.55))
    return m


def indicator(m, pos):
    m.material("glow_run", (0.3, 1.0, 0.4), 0.4)
    m.blob("glow_run", pos, (0.09, 0.09, 0.09), sub=1, noise=0, seed=1, obj="Indicator", smooth=True)


def heater():
    m = Model("heater", "Machines")
    m.material("body", (0.30, 0.31, 0.34), 0.55, 0.3)
    m.material("dark", (0.15, 0.15, 0.17), 0.6, 0.3)
    m.material("glow_heat", (1.0, 0.42, 0.1), 0.5)
    m.material("pipe", (0.6, 0.45, 0.3), 0.4, 0.7)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("dark", (sx * 0.62, sy * 0.62, 0.1), (0.28, 0.28, 0.2), bevel=0.03)
    m.box("body", (0, 0, 0.62), (1.5, 1.5, 0.9), bevel=0.07)
    for i in range(5):
        m.box("dark", (0, 0.75 + 0.02, 0.28 + i * 0.16), (1.2, 0.05, 0.07))
        m.box("dark", (0, -0.77, 0.28 + i * 0.16), (1.2, 0.05, 0.07))
    m.cyl("dark", (0, 0, 1.12), 0.62, 0.62, 0.1, seg=16)
    m.cyl("glow_heat", (0, 0, 1.19), 0.5, 0.5, 0.05, seg=16)
    for i in range(4):
        m.box("dark", (0, 0, 1.23), (1.0, 0.04, 0.03), rot=(0, 0, i * 45))
    m.tube("pipe", (0.55, 0.45, 1.1), (0.55, 0.45, 2.0), 0.14, 0.12, seg=10)
    m.cyl("dark", (0.55, 0.45, 2.04), 0.19, 0.19, 0.08, seg=10)
    m.tube("pipe", (-0.45, 0.5, 1.1), (-0.45, 0.5, 1.6), 0.09, 0.08, seg=8)
    indicator(m, (-0.6, -0.78, 0.95))
    return m


def drill():
    m = Model("drill", "Machines")
    m.material("yellow", (0.92, 0.7, 0.14), 0.55)
    m.material("dark", (0.18, 0.18, 0.2), 0.6, 0.3)
    m.material("steel", (0.65, 0.67, 0.7), 0.35, 0.8)
    m.box("dark", (0, 0, 0.12), (2.2, 2.2, 0.24), bevel=0.04)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.tube("yellow", (sx * 0.85, sy * 0.85, 0.24), (sx * 0.25, sy * 0.25, 3.2), 0.07, 0.06, seg=6)
    for z in (1.0, 1.9, 2.7):
        k = 1 - (z - 0.24) / 2.96 * 0.7
        for i in range(4):
            a, b = (i * 90) % 360, ((i + 1) * 90) % 360
            pa = Vector((math.cos(math.radians(45 + a)) * 1.2 * k, math.sin(math.radians(45 + a)) * 1.2 * k, z))
            pb = Vector((math.cos(math.radians(45 + b)) * 1.2 * k, math.sin(math.radians(45 + b)) * 1.2 * k, z))
            m.tube("yellow", pa, pb, 0.04, 0.04, seg=5)
    m.box("yellow", (0, 0, 3.4), (0.9, 0.9, 0.55), bevel=0.06)
    m.cyl("dark", (0, 0, 3.78), 0.3, 0.3, 0.25, seg=10)
    m.cyl("steel", (0, 0, 1.5), 0.13, 0.13, 3.1, seg=10)
    for i in range(6):
        m.cyl("steel", (0, 0, 0.15 + i * 0.12), 0.2, 0.2, 0.05, seg=8)
    m.cyl("steel", (0, 0, 0.0), 0.2, 0.01, 0.4, seg=8)
    m.cyl("dark", (0.9, -0.9, 0.5), 0.2, 0.2, 0.5, seg=10)
    m.tube("dark", (0.9, -0.9, 0.75), (0.15, -0.1, 3.3), 0.05, 0.05, seg=6)
    indicator(m, (0, -1.12, 0.3))
    return m


def vegetube():
    m = Model("vegetube", "Machines")
    m.material("dark", (0.2, 0.21, 0.24), 0.55, 0.3)
    m.material("glass_tube", (0.65, 0.9, 0.8), 0.1)
    m.material("plant", (0.3, 0.7, 0.28), 0.8)
    m.material("plant_b", (0.45, 0.78, 0.3), 0.8)
    m.material("soil", (0.28, 0.2, 0.14), 0.95)
    m.cyl("dark", (0, 0, 0.12), 0.7, 0.7, 0.24, seg=14)
    m.cyl("soil", (0, 0, 0.27), 0.5, 0.5, 0.06, seg=14)
    m.cyl("glass_tube", (0, 0, 1.3), 0.5, 0.5, 2.0, seg=14)
    m.cyl("dark", (0, 0, 2.38), 0.56, 0.56, 0.16, seg=14)
    m.tube("plant", (0, 0, 0.3), (0, 0, 1.5), 0.05, 0.03, seg=6)
    for i in range(7):
        a = math.radians(i * 51)
        z = 0.5 + i * 0.22
        m.blob("plant" if i % 2 else "plant_b", (math.cos(a) * 0.22, math.sin(a) * 0.22, z),
               (0.2, 0.09, 0.05), sub=1, noise=0.1, seed=i, rot=(0, 0, math.degrees(a)))
    m.blob("plant_b", (0, 0, 1.65), (0.2, 0.2, 0.14), sub=1, noise=0.1, seed=11)
    indicator(m, (0, -0.72, 0.28))
    return m


def solar_panel():
    m = Model("solar_panel", "Machines")
    m.material("metal", (0.65, 0.67, 0.7), 0.4, 0.7)
    m.material("cell", (0.08, 0.14, 0.38), 0.25, 0.3)
    m.material("grid", (0.7, 0.72, 0.78), 0.4, 0.6)
    m.cyl("metal", (0, 0, 0.1), 0.45, 0.45, 0.2, seg=12)
    m.cyl("metal", (0, 0, 0.8), 0.1, 0.12, 1.6, seg=10)
    tilt = T(0, 0, 1.75) @ R(-28, 0, 0)
    m.box("metal", (0, 0, 0), (2.5, 1.5, 0.08), bevel=0.02, base=tilt)
    m.box("cell", (0, 0, 0.05), (2.36, 1.36, 0.03), base=tilt)
    for i in range(1, 4):
        m.box("grid", (-1.18 + i * 0.59, 0, 0.075), (0.02, 1.36, 0.015), base=tilt)
    for i in range(1, 3):
        m.box("grid", (0, -0.68 + i * 0.45, 0.075), (2.36, 0.02, 0.015), base=tilt)
    m.box("metal", (0, 0.5, -0.12), (0.12, 0.12, 0.3), base=tilt)
    return m


def algae_farm():
    m = Model("algae_farm", "Machines")
    m.material("tank", (0.82, 0.84, 0.82), 0.55)
    m.material("dark", (0.2, 0.22, 0.24), 0.55, 0.3)
    m.material("algae", (0.14, 0.52, 0.26), 0.35)
    m.material("pipe", (0.6, 0.62, 0.66), 0.4, 0.6)
    m.material("glow_ok", (0.4, 1.0, 0.6), 0.4)
    m.box("dark", (0, 0, 0.1), (3.2, 2.2, 0.2), bevel=0.03)
    m.box("tank", (0, 0, 0.5), (3.0, 2.0, 0.6), bevel=0.07)
    m.box("dark", (0, 0, 0.83), (3.1, 2.1, 0.08), bevel=0.02)
    m.box("algae", (0, 0, 0.8), (2.7, 1.7, 0.06))
    for i in range(4):
        m.blob("algae", (-1.0 + i * 0.66, (i % 2) * 0.5 - 0.25, 0.86), (0.28, 0.24, 0.07), sub=1, noise=0.15, seed=i)
    for sx in (-1, 1):
        m.tube("pipe", (sx * 1.2, 0.7, 0.8), (sx * 1.2, 0.7, 2.0), 0.08, 0.08, seg=8)
    m.tube("pipe", (-1.2, 0.7, 2.0), (1.2, 0.7, 2.0), 0.08, 0.08, seg=8)
    m.box("dark", (1.35, -0.9, 1.1), (0.5, 0.3, 0.7), bevel=0.04)
    m.box("glow_ok", (1.35, -1.06, 1.25), (0.22, 0.02, 0.1))
    indicator(m, (-1.3, -1.12, 0.65))
    return m


def storage_chest():
    m = Model("storage_chest", "Machines")
    m.material("body", (0.42, 0.45, 0.5), 0.5, 0.5)
    m.material("dark", (0.16, 0.17, 0.19), 0.6, 0.3)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("glow_lock", (0.4, 0.85, 1.0), 0.4)
    m.box("dark", (0, 0, 0.06), (1.5, 0.95, 0.12), bevel=0.02)
    m.box("body", (0, 0, 0.45), (1.4, 0.85, 0.66), bevel=0.05)
    m.box("body", (0, 0, 0.88), (1.46, 0.9, 0.22), bevel=0.06)
    for sx in (-0.5, 0.5):
        m.box("dark", (sx, 0, 0.55), (0.1, 0.9, 0.9))
    m.box("accent", (0, 0, 0.3), (1.42, 0.87, 0.06))
    m.box("dark", (0, 0.46, 0.72), (0.26, 0.05, 0.22), bevel=0.02)
    m.box("glow_lock", (0, 0.49, 0.74), (0.1, 0.02, 0.08))
    for sx in (-1, 1):
        m.box("dark", (sx * 0.74, 0, 0.6), (0.06, 0.4, 0.08), bevel=0.01)
    return m


def nuclear_reactor():
    m = Model("nuclear_reactor", "Machines")
    m.material("concrete", (0.62, 0.62, 0.6), 0.85)
    m.material("metal", (0.55, 0.57, 0.6), 0.4, 0.7)
    m.material("dark", (0.16, 0.17, 0.19), 0.6, 0.3)
    m.material("hazard", (0.95, 0.75, 0.1), 0.6)
    m.material("glow_core", (0.35, 1.0, 0.5), 0.4)
    m.box("concrete", (0, 0, 0.2), (4.2, 4.2, 0.4), bevel=0.05)
    for i in range(8):
        m.box("hazard" if i % 2 == 0 else "dark", (-1.75 + i * 0.5, -2.08, 0.25), (0.5, 0.06, 0.25))
    m.lathe("concrete", (0, 0, 0.4), [(1.4, 0), (1.35, 1.6), (1.15, 2.4), (0.7, 2.9), (0.0, 3.05)], seg=20)
    m.cyl("glow_core", (0, 0, 1.25), 1.43, 1.43, 0.28, seg=20)
    m.cyl("metal", (0, 0, 3.5), 0.18, 0.18, 0.5, seg=10)
    for sx in (-1, 1):
        m.lathe("concrete", (sx * 1.6, 1.3, 0.4), [(0.55, 0), (0.4, 1.0), (0.45, 2.0), (0.5, 2.1)], seg=14)
        m.tube("metal", (sx * 0.9, 0.7, 1.0), (sx * 1.3, 1.1, 1.6), 0.12, 0.12, seg=8)
    m.box("dark", (1.6, -1.3, 0.9), (0.6, 0.5, 1.0), bevel=0.05)
    m.box("glow_core", (1.6, -1.56, 1.1), (0.3, 0.02, 0.15))
    return m


def wreck_pod():
    m = Model("wreck_pod", "Props")
    m.material("hull", (0.55, 0.56, 0.6), 0.5, 0.6)
    m.material("scorch", (0.12, 0.11, 0.1), 0.95)
    m.material("panel", (0.85, 0.42, 0.12), 0.6)
    m.material("glow_light", (1.0, 0.35, 0.2), 0.4)
    lay = T(0, 0, 0.9) @ R(78, 0, 18)
    m.lathe("hull", (0, 0, -3.0), [(0.0, -0.2), (0.9, 0.2), (1.35, 1.0), (1.45, 3.5), (1.2, 5.2), (0.6, 5.8), (0.0, 5.9)],
            seg=16, base=lay)
    m.lathe("scorch", (0, 0, -3.25), [(0.0, 0), (1.0, 0.2), (1.1, 0.5), (0.0, 0.55)], seg=16, base=lay)
    for i in range(3):
        a = i * 120
        m.box("hull", (0, 0, 0), (0.08, 1.4, 1.3), base=lay @ R(0, 0, a) @ T(1.3, 0, -2.0), taper=(1.0, 0.4))
    m.box("panel", (0, 0, 0), (1.6, 0.06, 1.0), base=lay @ T(0, -1.42, 0.8))
    m.box("scorch", (0, 0, 0), (0.9, 0.08, 0.7), base=lay @ T(0.4, -1.44, 2.0))
    m.blob("glow_light", (0, 0, 0), (0.12, 0.12, 0.12), sub=1, noise=0, seed=1, base=lay @ T(0, -1.4, 2.8))
    rng = random.Random(77)
    for i in range(7):
        a = rng.uniform(0, 6.28)
        r = rng.uniform(2.5, 5.0)
        m.box("hull" if i % 2 else "scorch", (math.cos(a) * r, math.sin(a) * r, 0.08),
              (rng.uniform(0.4, 1.1), rng.uniform(0.3, 0.8), 0.06), rot=(rng.uniform(-15, 15), rng.uniform(-15, 15), rng.uniform(0, 360)))
    return m


def chip_crate():
    m = Model("chip_crate", "Props")
    m.material("case", (0.25, 0.27, 0.3), 0.5, 0.5)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("glow_chip", (0.3, 0.9, 1.0), 0.4)
    m.box("case", (0, 0, 0.25), (0.8, 0.55, 0.5), bevel=0.04)
    m.box("accent", (0, 0, 0.42), (0.82, 0.57, 0.06))
    m.box("glow_chip", (0, 0, 0.52), (0.34, 0.24, 0.04))
    m.box("case", (0, 0, 0.55), (0.22, 0.14, 0.03))
    return m


def log_terminal():
    m = Model("log_terminal", "Props")
    m.material("metal", (0.4, 0.42, 0.46), 0.5, 0.5)
    m.material("dark", (0.12, 0.13, 0.15), 0.6, 0.3)
    m.material("glow_screen", (0.45, 0.95, 0.75), 0.4)
    m.box("dark", (0, 0, 0.08), (0.9, 0.7, 0.16), bevel=0.03)
    m.box("metal", (0, 0, 0.6), (0.3, 0.3, 1.0), bevel=0.04)
    tilt = T(0, 0.05, 1.2) @ R(-30, 0, 0)
    m.box("dark", (0, 0, 0), (0.85, 0.12, 0.6), bevel=0.03, base=tilt)
    m.box("glow_screen", (0, 0, 0), (0.72, 0.02, 0.48), base=tilt @ T(0, 0.065, 0))
    return m


def mothership():
    """Nave-mãe Gênese (~64 m). Pivô no centro, comprimento ao longo de +Y (frente)."""
    m = Model("mothership", "Sky")
    m.material("hull", (0.78, 0.8, 0.84), 0.45, 0.5)
    m.material("hull_dark", (0.3, 0.32, 0.36), 0.5, 0.5)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("glow_engine", (0.4, 0.8, 1.0), 0.4)
    m.material("glow_window", (1.0, 0.85, 0.55), 0.4)
    m.material("glow_landing", (1.0, 0.35, 0.25), 0.4)
    along = R(-90, 0, 0)  # eixo do torno (Z) passa a apontar para +Y
    m.lathe("hull", (0, 0, -30), [(0.0, -2), (4.5, 0), (7.5, 8), (8.0, 30), (7.0, 48), (4.0, 58), (0.0, 64)], seg=24, base=along)
    m.lathe("hull_dark", (0, 0, 6), [(12.5, -1.4), (14.5, -1.4), (14.5, 1.4), (12.5, 1.4), (12.5, -1.4)], seg=32, base=along, caps=False)
    for i in range(4):
        a = i * 90 + 45
        m.box("hull_dark", (0, 0, 0), (1.2, 7.0, 1.6), base=R(0, a, 0) @ T(0, 0, 0) @ R(-90, 0, 0) @ T(0, 6, 10.5) @ R(90, 0, 0))
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl("hull_dark", (sx * 3.2, -31, sz * 3.2), 1.8, 2.2, 4.0, seg=16, rot=(90, 0, 0))
            m.cyl("glow_engine", (sx * 3.2, -33.05, sz * 3.2), 1.6, 1.6, 0.1, seg=16, rot=(90, 0, 0))
    m.box("hull_dark", (0, 18, 7.6), (5, 12, 2.2), bevel=0.4)
    for i in range(10):
        m.box("glow_window", (0, 13 + i * 1.1, 8.75), (3.6, 0.5, 0.1))
    for i in range(6):
        y = -18 + i * 7
        m.box("accent", (0, y, 0), (16.4, 0.8, 0.3), base=T(0, 0, -0.2))
        for sx in (-1, 1):
            m.blob("glow_landing", (sx * 5.5, y, -6.8), (0.45, 0.45, 0.45), sub=1, noise=0, seed=i)
    for sx in (-1, 1):
        m.box("hull", (sx * 11, -12, 0), (12, 9, 0.8), taper=(1, 1), rot=(0, 0, sx * 8), bevel=0.2)
    return m


# =============================================================================
BUILDERS = {
    "tree_broad_0": lambda: tree_broad(0), "tree_broad_1": lambda: tree_broad(1), "tree_broad_2": lambda: tree_broad(2),
    "tree_conifer_0": lambda: tree_conifer(0), "tree_conifer_1": lambda: tree_conifer(1),
    "bush": bush, "moss": moss,
    "ore_iron": ore_iron,
    "ore_silicon": lambda: crystals("ore_silicon", (0.38, 0.5, 0.72), (0.6, 0.72, 0.92), 0.25, 0.4, 41),
    "ore_ice": lambda: crystals("ore_ice", (0.7, 0.88, 0.98), (0.88, 0.97, 1.0), 0.15, 0.1, 42),
    "ore_magnesium": ore_magnesium, "ore_titanium": ore_titanium,
    "grazer": grazer, "hopper": hopper,
    "base_module": base_module, "wind_turbine": wind_turbine, "heater": heater, "drill": drill,
    "vegetube": vegetube, "solar_panel": solar_panel, "algae_farm": algae_farm,
    "storage_chest": storage_chest,
    "nuclear_reactor": nuclear_reactor,
    "ore_uranium": lambda: crystals("ore_uranium", (0.25, 0.9, 0.35), (0.6, 1.0, 0.6), 0.3, 0.2, 43, glow=True),
    "wreck_pod": wreck_pod, "chip_crate": chip_crate, "log_terminal": log_terminal,
    "mothership": mothership,
}


def render_preview(path):
    """Render rápido (Workbench) visto de frente-lado, para conferir o modelo sem abrir o Unity."""
    scene = bpy.context.scene
    pts = [o.matrix_world @ Vector(c) for o in scene.objects if o.type == "MESH" for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center, size = (mn + mx) / 2, max(mx - mn)
    cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
    scene.collection.objects.link(cam)
    cam.location = center + Vector((1.0, 1.7, 0.9)).normalized() * size * 2.1
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = scene.render.resolution_y = 360
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[1:]
    preview_dir = None
    if "--preview" in argv:
        i = argv.index("--preview")
        preview_dir = argv[i + 1]
        del argv[i:i + 2]
    out_dir = argv[0]
    names = argv[1:] or list(BUILDERS)
    for name in names:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        model = BUILDERS[name]()
        model.build()
        path = model.export(out_dir)
        tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects)
        print(f"[gen_models] {name}: {len(bpy.context.scene.objects)} objetos, {tris} faces -> {path}")
        if preview_dir:
            os.makedirs(preview_dir, exist_ok=True)
            render_preview(os.path.join(preview_dir, name + ".png"))


if __name__ == "__main__":
    main()
