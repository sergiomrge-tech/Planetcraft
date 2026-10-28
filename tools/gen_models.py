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
        # Máquinas ganham detalhe automático (emendas, faixas, rebites, anéis) nas peças grandes e retas.
        self.greeble = category == "Machines"

    def material(self, name, color, rough=0.7, metal=0.0):
        self.mats[name] = (color, rough, metal)

    # ---- detalhe automático ------------------------------------------------------
    def _detail_mat(self):
        if "detail_seam" not in self.mats:
            self.material("detail_seam", (0.07, 0.075, 0.08), 0.8, 0.2)
            self.material("detail_bolt", (0.42, 0.43, 0.45), 0.4, 0.8)
        return "detail_seam"

    def _greeble_box(self, mat, pos, size, base, obj):
        """Emendas verticais nas faces laterais, faixa a 85% da altura e rebites no topo de uma caixa alinhada."""
        sx, sy, sz = size
        if mat.startswith(("glow_", "glass_", "detail_")) or min(sx, sy) < 0.45 or sz < 0.35:
            return
        seam = self._detail_mat()
        x0, y0, z0 = pos
        top = z0 + sz / 2
        for axis, length, depth in (("x", sx, sy), ("y", sy, sx)):
            n = max(1, int(length / 0.55))
            for side in (-1, 1):
                for k in range(1, n):
                    t = -length / 2 + k * length / n
                    p = (x0 + t, y0 + side * (depth / 2 + 0.004), z0) if axis == "x" else \
                        (x0 + side * (depth / 2 + 0.004), y0 + t, z0)
                    s = (0.018, 0.01, sz * 0.86) if axis == "x" else (0.01, 0.018, sz * 0.86)
                    self.box(seam, p, s, base=base, obj=obj)
                band = (x0, y0 + side * (depth / 2 + 0.006), top - sz * 0.15) if axis == "x" else \
                       (x0 + side * (depth / 2 + 0.006), y0, top - sz * 0.15)
                bs = (sx * 0.98, 0.012, 0.035) if axis == "x" else (0.012, sy * 0.98, 0.035)
                self.box(seam, band, bs, base=base, obj=obj)
        for axis, length, depth in (("x", sx, sy), ("y", sy, sx)):
            n = max(2, int(length / 0.22))
            for side in (-1, 1):
                for k in range(n):
                    t = -length / 2 + (k + 0.5) * length / n
                    p = (x0 + t, y0 + side * (depth / 2 - 0.06), top + 0.012) if axis == "x" else \
                        (x0 + side * (depth / 2 - 0.06), y0 + t, top + 0.012)
                    self.box("detail_bolt", p, (0.035, 0.035, 0.025), base=base, obj=obj)

    def _greeble_cyl(self, mat, pos, r, h, seg, base, obj):
        """Anéis de reforço perto da base e do topo de um cilindro vertical."""
        if mat.startswith(("glow_", "glass_", "detail_")) or r < 0.22 or h < 0.5:
            return
        seam = self._detail_mat()
        x0, y0, z0 = pos
        for zr in (-0.38, 0.38):
            self.lathe(seam, (x0, y0, z0 + zr * h), [(r + 0.012, -0.03), (r + 0.012, 0.03)], seg=max(seg, 16),
                       caps=False, base=base, obj=obj)

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
        if self.greeble and rot == (0, 0, 0) and taper is None:
            self._greeble_box(mat, pos, size, base, obj)

    def cyl(self, mat, pos, r1, r2, h, seg=12, rot=(0, 0, 0), smooth=True, M=None, base=I4, obj=None):
        seg = min(48, max(seg * 2, 24))  # silhueta redonda: o dobro de lados
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=seg,
                              radius1=r1, radius2=r2, depth=h)
        tmp.faces.ensure_lookup_table()
        for f in tmp.faces:
            f.smooth = smooth and abs(f.normal.z) < 0.9
        bmesh.ops.transform(tmp, matrix=M if M is not None else T(*pos) @ R(*rot), verts=tmp.verts)
        self._emit(mat, obj, tmp, base)
        if self.greeble and M is None and rot == (0, 0, 0) and abs(r1 - r2) < 0.05:
            self._greeble_cyl(mat, pos, max(r1, r2), h, seg, base, obj)

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

    def poly(self, mat, verts, faces, base=I4, obj=None, smooth=False):
        """Malha livre: lista de vértices e faces (índices). Normais recalculadas no build."""
        tmp = bmesh.new()
        vs = [tmp.verts.new(v) for v in verts]
        for f in faces:
            tmp.faces.new([vs[i] for i in f]).smooth = smooth
        self._emit(mat, obj, tmp, base)

    def lathe(self, mat, pos, profile, seg=12, smooth=True, base=I4, obj=None, caps=True):
        seg = min(96, max(seg * 2, 24))
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
def _pebbles(m, mat, count, radius, seed, size=(0.04, 0.1), obj=None):
    """Pedregulhos espalhados em volta da base do minério (assentam o objeto no chão)."""
    rng = random.Random(seed)
    for i in range(count):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(radius * 0.7, radius * 1.25)
        s = rng.uniform(*size)
        m.blob(mat, (math.cos(a) * r, math.sin(a) * r, s * 0.4), (s, s * rng.uniform(0.7, 1.1), s * 0.7), sub=2,
               noise=0.3, seed=seed + i, obj=obj, smooth=True)


def _rock_cluster(m, mat, seed, extent=0.5, count=5, height=0.3):
    """Aglomerado de rochas irregulares: uma grande ao centro e outras menores meio enterradas em volta."""
    rng = random.Random(seed)
    m.blob(mat, (0, 0, height * 0.55), (extent, extent * 0.85, height), sub=3, noise=0.26, seed=seed, smooth=True)
    for i in range(count - 1):
        a = rng.uniform(0, 2 * math.pi)
        d = extent * rng.uniform(0.55, 0.95)
        r = extent * rng.uniform(0.28, 0.5)
        m.blob(mat, (math.cos(a) * d, math.sin(a) * d, r * 0.45), (r, r * rng.uniform(0.7, 1.0), r * 0.75), sub=3,
               noise=0.26, seed=seed + 10 + i, rot=(0, 0, rng.uniform(0, 360)), smooth=True)


def ore_iron():
    """Jazida de ferro: rocha escura escavada, veios de óxido laranja e lascas de minério nas fendas."""
    m = Model("ore_iron", "Ore")
    m.material("rock", (0.30, 0.24, 0.22), 0.9)
    m.material("rust", (0.66, 0.32, 0.14), 0.7)
    m.material("ore_metal", (0.38, 0.36, 0.38), 0.4, 0.7)
    rng = random.Random(5)
    for i in range(9):  # veios de óxido: placas finas coladas na superfície
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.12, 0.42)
        z = 0.12 + (0.5 - r) * 0.7
        m.blob("rust", (math.cos(a) * r, math.sin(a) * r, z), (rng.uniform(0.1, 0.2), rng.uniform(0.04, 0.09), 0.035),
               sub=2, smooth=True, noise=0.25, seed=40 + i, rot=(rng.uniform(-25, 25), rng.uniform(-25, 25), math.degrees(a)))
    for i in range(5):  # lascas metálicas
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.2, 0.46)
        m.box("ore_metal", (math.cos(a) * r, math.sin(a) * r, 0.2 + rng.uniform(0, 0.14)),
              (rng.uniform(0.07, 0.13), rng.uniform(0.05, 0.09), rng.uniform(0.04, 0.07)),
              rot=(rng.uniform(0, 60), rng.uniform(0, 60), rng.uniform(0, 360)), bevel=0.012)
    return m


def crystals(name, base, tip, rough, metal, seeds, glow=False):
    """Aglomerado de cristais hexagonais: prismas com facetas e pontas, tamanhos variados, rocha-mãe e cacos."""
    m = Model(name, "Ore")
    crystal, crystal_tip = ("glow_crystal", "glow_crystal_tip") if glow else ("crystal", "crystal_tip")
    m.material("base_rock", (0.24, 0.24, 0.27), 0.9)
    m.material(crystal, base, rough, metal)
    m.material(crystal_tip, tip, rough, metal)
    rng = random.Random(seeds)
    for i in range(11):
        a = math.radians(i * 33 + rng.uniform(-14, 14))
        tilt = 0.0 if i == 0 else rng.uniform(0.15, 0.55)
        d = Vector((math.cos(a) * tilt, math.sin(a) * tilt, 1.0)).normalized()
        length = 1.2 if i == 0 else rng.uniform(0.28, 0.95)
        radius = 0.12 if i == 0 else rng.uniform(0.045, 0.1)
        r0 = 0 if i == 0 else rng.uniform(0.06, 0.34)
        b = Vector((math.cos(a) * r0, math.sin(a) * r0, 0.1))
        tip_pos = b + d * length
        cap = radius * 2.2
        m.tube(crystal, b, tip_pos - d * cap, radius, radius * 0.92, seg=6)
        m.tube(crystal_tip, tip_pos - d * cap, tip_pos, radius * 0.92, radius * 0.1, seg=6)
    for i in range(8):  # cacos soltos
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.42, 0.66)
        d = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 1.0)).normalized()
        b = Vector((math.cos(a) * r, math.sin(a) * r, 0.04))
        m.tube(crystal_tip, b, b + d * rng.uniform(0.1, 0.22), 0.025, 0.004, seg=5)
    return m


def ore_magnesium():
    """Magnésio: seixos claros e lisos com crosta cristalina branca nas fendas."""
    m = Model("ore_magnesium", "Ore")
    m.material("stone", (0.84, 0.82, 0.76), 0.5)
    m.material("stone_b", (0.66, 0.64, 0.6), 0.55)
    m.material("salt", (0.96, 0.96, 0.94), 0.3)
    rng = random.Random(21)
    for i in range(14):  # crosta cristalina
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.1, 0.5)
        m.box("salt", (math.cos(a) * r, math.sin(a) * r, 0.24 + (0.5 - r) * 0.5),
              (rng.uniform(0.04, 0.09), rng.uniform(0.04, 0.09), rng.uniform(0.03, 0.07)),
              rot=(rng.uniform(0, 70), rng.uniform(0, 70), rng.uniform(0, 360)), bevel=0.01)
    return m


def ore_titanium():
    """Titânio: rocha escura com placas e lingotes angulares de metal claro brilhante."""
    m = Model("ore_titanium", "Ore")
    m.material("base_rock", (0.24, 0.24, 0.27), 0.9)
    m.material("ti", (0.82, 0.84, 0.88), 0.22, 0.9)
    m.material("ti_dark", (0.5, 0.52, 0.58), 0.3, 0.9)
    rng = random.Random(33)
    for i in range(7):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.4)
        s = rng.uniform(0.14, 0.3)
        m.box("ti" if i % 2 == 0 else "ti_dark", (math.cos(a) * r, math.sin(a) * r, 0.34 + rng.uniform(0, 0.12)),
              (s, s * rng.uniform(0.5, 0.9), s * rng.uniform(0.7, 1.2)),
              rot=(rng.uniform(0, 70), rng.uniform(0, 70), rng.uniform(0, 360)), bevel=0.035)
    for i in range(9):  # lascas nas bordas
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.38, 0.62)
        m.box("ti", (math.cos(a) * r, math.sin(a) * r, 0.06), (0.07, 0.05, 0.04),
              rot=(rng.uniform(0, 60), rng.uniform(0, 60), rng.uniform(0, 360)), bevel=0.01)
    return m


def _hex_crystal(m, mat, tip_mat, base, direction, radius, length, rng, flat=1.0):
    """Cristal hexagonal de verdade: prisma de 6 faces, ponta piramidal com 6 facetas, seção levemente achatada
    e torcida (cristais naturais não são regulares). Base afundada para nascer de dentro da rocha."""
    d = Vector(direction).normalized()
    up = Vector((0, 0, 1))
    rot = up.rotation_difference(d).to_matrix().to_4x4()
    twist = rng.uniform(0, math.pi / 3)
    tip_h = radius * rng.uniform(1.4, 2.2)
    body = max(length - tip_h, radius)
    ring = []
    for k in range(6):
        a = twist + k * math.pi / 3
        r = radius * rng.uniform(0.85, 1.12)
        ring.append((math.cos(a) * r, math.sin(a) * r * flat))
    verts = [(x, y, -radius * 0.8) for x, y in ring] + [(x * 0.97, y * 0.97, body) for x, y in ring]
    verts.append((rng.uniform(-0.15, 0.15) * radius, rng.uniform(-0.15, 0.15) * radius, body + tip_h))
    faces = [(k, (k + 1) % 6, 6 + (k + 1) % 6, 6 + k) for k in range(6)]
    faces.append(tuple(reversed(range(6))))
    M = T(*base) @ rot
    m.poly(mat, verts[:12], faces, base=M)
    tip_faces = [(k, (k + 1) % 6, 6) for k in range(6)]
    m.poly(tip_mat, [verts[6 + k] for k in range(6)] + [verts[12]], tip_faces, base=M)


def druse(name, color, tip, glow=False, count=34, seed=1, max_len=0.34, max_r=0.045):
    """Drusa (~0.3 m): dezenas de cristais finos de tamanhos variados saindo de um ponto, o maior no centro.
    Instanciada várias vezes sobre a rocha escaneada do minério, alinhada à normal da superfície."""
    m = Model(name, "Ore")
    body, point = ("glow_crystal", "glow_crystal_tip") if glow else ("glass_crystal", "glass_crystal_tip")
    m.material(body, color, 0.08, 0.0)
    m.material(point, tip, 0.05, 0.0)
    rng = random.Random(seed)
    for i in range(count):
        main = i < 3
        spread = rng.uniform(0.0, 0.25) if main else rng.uniform(0.25, 0.95)
        a = rng.uniform(0, 2 * math.pi)
        direction = (math.cos(a) * spread, math.sin(a) * spread, 1.0)
        r0 = rng.uniform(0.0, 0.03) if main else rng.uniform(0.02, 0.13)
        base = (math.cos(a) * r0, math.sin(a) * r0, 0.0)
        length = max_len * (rng.uniform(0.75, 1.0) if main else rng.uniform(0.18, 0.7))
        radius = max_r * (rng.uniform(0.8, 1.0) if main else rng.uniform(0.3, 0.75))
        _hex_crystal(m, body, point, base, direction, radius, length, rng, flat=rng.uniform(0.75, 1.0))
    return m


def nuggets(name, color, metal, rough, seed, count=9, size=0.06, flat=False):
    """Minério para cravar na rocha: nódulos arredondados (ferro) ou placas chanfradas (titânio)."""
    m = Model(name, "Ore")
    m.material("ore_metal", color, rough, metal)
    rng = random.Random(seed)
    for i in range(count):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.12)
        s = size * rng.uniform(0.5, 1.3)
        dims = (s * rng.uniform(1.2, 2.2), s * rng.uniform(0.9, 1.6), s * 0.25) if flat else \
               (s * rng.uniform(0.7, 1.3), s * rng.uniform(0.7, 1.3), s * rng.uniform(0.5, 1.0))
        pos = (math.cos(a) * r, math.sin(a) * r, dims[2] * 0.3)
        rot = (rng.uniform(-35, 35), rng.uniform(-35, 35), rng.uniform(0, 360))
        if flat:
            m.box("ore_metal", pos, dims, rot=rot, bevel=min(dims) * 0.18)
        else:  # nódulos arredondados e irregulares, como hematita
            m.blob("ore_metal", pos, tuple(x / 2 for x in dims), sub=2, noise=0.22, seed=seed + i, rot=rot, smooth=True)
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
    """Módulo de base pressurizado (4 x 4 m, entra-se pela câmara de ar na face -Y): paredes em painéis com relevo,
    janelas com mainel, tanques de O2, tubulação, climatização e antena no teto e mobília por dentro."""
    m = Model("base_module", "Machines")
    m.greeble = False  # detalhe feito à mão abaixo
    m.material("wall", (0.86, 0.86, 0.82), 0.6)
    m.material("panel", (0.8, 0.8, 0.77), 0.55)
    m.material("trim", (0.28, 0.3, 0.33), 0.5, 0.4)
    m.material("steel", (0.55, 0.57, 0.6), 0.4, 0.8)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("stripe", (0.92, 0.72, 0.1), 0.6)
    m.material("dark", (0.1, 0.105, 0.11), 0.7, 0.3)
    m.material("glass_win", (0.5, 0.75, 0.9), 0.1)
    m.material("glow_lamp", (1.0, 0.95, 0.75), 0.4)
    m.material("glow_screen", (0.35, 0.95, 0.8), 0.4)
    m.material("floor", (0.45, 0.46, 0.48), 0.8)
    H = 2.8  # altura das paredes

    # Piso elevado sobre pés niveladores.
    m.box("floor", (0, 0, 0.32), (4.0, 4.0, 0.2), bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 1.75, sy * 1.75
            m.box("steel", (x, y, 0.03), (0.42, 0.42, 0.06), bevel=0.01)
            m.cyl("steel", (x, y, 0.15), 0.07, 0.07, 0.24, seg=10)
            m.cyl("trim", (x, y, 0.2), 0.11, 0.11, 0.06, seg=6)
            for bx in (-0.15, 0.15):
                for by in (-0.15, 0.15):
                    m.cyl("dark", (x + bx, y + by, 0.07), 0.025, 0.025, 0.02, seg=6)
    z0 = 0.42  # base das paredes

    # Paredes (a frente, -Y, tem o vão da porta).
    m.box("wall", (0, 1.925, z0 + H / 2), (4.0, 0.15, H))
    for sx in (-1, 1):
        m.box("wall", (sx * 1.925, 0, z0 + H / 2), (0.15, 3.7, H))
        m.box("wall", (sx * 1.35, -1.925, z0 + H / 2), (1.3, 0.15, H))
    m.box("wall", (0, -1.925, z0 + H - 0.25), (1.4, 0.15, 0.5))
    m.box("trim", (0, 0, z0 + H + 0.11), (4.3, 4.3, 0.22), bevel=0.04)
    m.box("trim", (0, 0, z0 + 0.06), (4.12, 4.12, 0.12))

    # Painéis em relevo nas faces externas (3 por parede, com emendas escuras entre eles).
    def wall_panels(face, skip_middle=False):
        for k in (-1, 0, 1):
            if skip_middle and k == 0:
                continue
            for row, (zc, hgt) in enumerate(((z0 + 0.75, 1.1), (z0 + 2.05, 1.2))):
                if face in ("+y", "-y"):
                    y = 2.0 if face == "+y" else -2.0
                    m.box("panel", (k * 1.27, y + (0.012 if face == "+y" else -0.012), zc), (1.18, 0.03, hgt), bevel=0.01)
                else:
                    x = 2.0 if face == "+x" else -2.0
                    m.box("panel", (x + (0.012 if face == "+x" else -0.012), k * 1.2, zc), (0.03, 1.1, hgt), bevel=0.01)
    wall_panels("+y")
    wall_panels("+x")
    wall_panels("-x")
    wall_panels("-y", skip_middle=True)

    # Pilares de canto com placas de rebites.
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("trim", (sx * 1.98, sy * 1.98, z0 + H / 2 + 0.05), (0.22, 0.22, H + 0.1), bevel=0.03)
            for z in (z0 + 0.3, z0 + H - 0.3):
                m.box("steel", (sx * 2.06, sy * 2.06, z), (0.1, 0.1, 0.18), bevel=0.01)

    # Faixa de identificação.
    m.box("accent", (0, 2.03, z0 + 0.32), (3.7, 0.03, 0.22))
    for sx in (-1, 1):
        m.box("accent", (sx * 2.03, 0, z0 + 0.32), (0.03, 3.5, 0.22))

    # Janelas com moldura grossa, mainel e rebites.
    def window(cx, cy, along_x):
        w, h, zc = 1.36, 0.82, z0 + 1.55
        d = (0.0, 0.035) if along_x else (0.035, 0.0)
        sgn = 1 if (cy if along_x else cx) > 0 else -1
        off = (0, sgn * 0.03) if along_x else (sgn * 0.03, 0)
        frame = (w + 0.16, 0.08, h + 0.16) if along_x else (0.08, w + 0.16, h + 0.16)
        m.box("trim", (cx + off[0], cy + off[1], zc), frame, bevel=0.02)
        glass = (w, 0.04, h) if along_x else (0.04, w, h)
        m.box("glass_win", (cx + off[0] * 1.8, cy + off[1] * 1.8, zc), glass)
        mull = (0.05, 0.06, h) if along_x else (0.06, 0.05, h)
        m.box("trim", (cx + off[0] * 2.0, cy + off[1] * 2.0, zc), mull)
        for t in (-0.6, -0.2, 0.2, 0.6):
            for zz in (zc - h / 2 - 0.05, zc + h / 2 + 0.05):
                p = (cx + t * w + off[0] * 2.4, cy + off[1] * 2.4, zz) if along_x else \
                    (cx + off[0] * 2.4, cy + t * w + off[1] * 2.4, zz)
                m.box("steel", p, (0.04, 0.04, 0.04))
    window(0, 2.0, True)
    window(2.0, 0, False)
    window(-2.0, 0, False)

    # Câmara de ar: moldura grossa, faixas de alerta, teclado, luz e degraus.
    m.box("trim", (-0.74, -2.03, z0 + 1.0), (0.12, 0.3, 2.05), bevel=0.02)
    m.box("trim", (0.74, -2.03, z0 + 1.0), (0.12, 0.3, 2.05), bevel=0.02)
    m.box("trim", (0, -2.03, z0 + 2.06), (1.6, 0.3, 0.14), bevel=0.02)
    for i in range(6):
        m.box("stripe" if i % 2 == 0 else "dark", (-0.62 + i * 0.25, -2.19, z0 + 2.06), (0.24, 0.02, 0.1), rot=(0, 0, 0))
    for sx in (-1, 1):
        for i in range(7):
            m.box("stripe" if i % 2 == 0 else "dark", (sx * 0.74, -2.19, z0 + 0.15 + i * 0.28), (0.1, 0.02, 0.26))
    m.box("dark", (1.0, -2.02, z0 + 1.15), (0.2, 0.06, 0.28), bevel=0.01)
    m.box("glow_screen", (1.0, -2.06, z0 + 1.2), (0.13, 0.01, 0.1))
    m.box("trim", (0, -2.18, z0 + 2.32), (0.8, 0.25, 0.08))
    m.box("glow_lamp", (0, -2.2, z0 + 2.27), (0.6, 0.12, 0.03))
    for k in range(2):
        m.box("steel", (0, -2.25 - k * 0.3, 0.12 + k * 0.0 + (1 - k) * 0.16), (1.3, 0.3, 0.08 + (1 - k) * 0.0), bevel=0.01)
        m.box("trim", (0, -2.25 - k * 0.3, 0.06 + (1 - k) * 0.08), (1.3, 0.3, 0.04))

    # Tanques de O2 na lateral +X, presos com cintas, e tubulação até a parede.
    for k, y in enumerate((-1.25, -0.85)):
        m.lathe("accent" if k == 0 else "panel", (2.42, y, z0), [(0, 0), (0.17, 0.03), (0.18, 0.15), (0.18, 1.25),
                                                                (0.12, 1.4), (0.05, 1.45), (0, 1.45)], seg=16)
        for zz in (0.35, 1.0):
            m.lathe("dark", (2.42, y, z0 + zz), [(0.19, -0.025), (0.19, 0.025)], seg=16, caps=False)
        m.tube("steel", (2.42, y, z0 + 1.45), (2.08, y, z0 + 1.7), 0.025, seg=6)
    m.tube("steel", (2.08, -1.25, z0 + 1.7), (2.08, -0.5, z0 + 1.7), 0.04, seg=8)
    m.box("dark", (2.08, -0.45, z0 + 1.7), (0.12, 0.12, 0.16))

    # Conduítes ao longo da parede de trás, com suportes.
    for z in (z0 + 2.45, z0 + 2.58):
        m.tube("steel", (-1.8, 2.09, z), (1.8, 2.09, z), 0.035, seg=8)
    for x in (-1.6, -0.6, 0.4, 1.4):
        m.box("trim", (x, 2.06, z0 + 2.52), (0.06, 0.1, 0.24))

    # Venezianas na lateral -X.
    for i in range(6):
        m.box("dark", (-2.07, 1.2, z0 + 0.5 + i * 0.09), (0.05, 0.7, 0.04), rot=(0, 25, 0))
    m.box("trim", (-2.05, 1.2, z0 + 0.72), (0.04, 0.8, 0.62))

    # Teto: unidade de climatização com hélice, painel solar, antena parabólica, luzes de topo.
    roof = z0 + H + 0.22
    m.box("trim", (0.95, 0.9, roof + 0.25), (1.2, 1.2, 0.5), bevel=0.04)
    m.cyl("dark", (0.95, 0.9, roof + 0.51), 0.45, 0.45, 0.04, seg=24)
    for i in range(5):
        m.box("steel", (0.95, 0.9, roof + 0.54), (0.8, 0.07, 0.02), rot=(0, 0, i * 36))
    for i in range(5):
        m.box("dark", (0.95, 0.31, roof + 0.1 + i * 0.07), (1.0, 0.02, 0.03))
    m.box("dark", (-0.9, 0.6, roof + 0.18), (1.4, 0.9, 0.04), rot=(12, 0, 0))
    m.box("steel", (-0.9, 0.6, roof + 0.08), (0.08, 0.08, 0.16))
    m.tube("trim", (-1.4, -1.2, roof), (-1.4, -1.2, roof + 0.9), 0.05, seg=8)
    m.lathe("panel", (-1.4, -1.2, roof + 0.95), [(0, -0.12), (0.42, 0.0), (0.45, 0.04)], seg=20, caps=False,
            base=I4)
    m.tube("steel", (-1.4, -1.2, roof + 0.85), (-1.4, -1.2, roof + 1.3), 0.015, seg=6)
    m.tube("trim", (1.6, -1.6, roof), (1.6, -1.6, roof + 1.6), 0.025, 0.015, seg=6)
    m.blob("glow_lamp", (1.6, -1.6, roof + 1.62), (0.05, 0.05, 0.05), sub=1, noise=0, seed=2)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.blob("accent", (sx * 1.98, sy * 1.98, roof + 0.05), (0.06, 0.06, 0.06), sub=1, noise=0, seed=3)

    # Interior: luz de teto, console com tela, bancada e armários.
    m.box("glow_lamp", (0, 0, z0 + H - 0.04), (2.2, 0.3, 0.05))
    m.box("trim", (0, 1.62, z0 + 0.45), (2.4, 0.5, 0.9), bevel=0.02)
    m.box("glow_screen", (0, 1.4, z0 + 1.15), (1.2, 0.03, 0.5), rot=(-15, 0, 0))
    m.box("dark", (0, 1.43, z0 + 1.15), (1.3, 0.04, 0.6), rot=(-15, 0, 0))
    for y in (-0.9, -0.2, 0.5):
        m.box("panel", (-1.62, y, z0 + 1.0), (0.5, 0.62, 2.0), bevel=0.015)
        m.box("dark", (-1.36, y, z0 + 1.0), (0.02, 0.56, 1.9))
    m.box("trim", (1.55, -0.2, z0 + 0.45), (0.6, 1.6, 0.08))
    for y in (-0.85, 0.45):
        m.box("trim", (1.55, y, z0 + 0.22), (0.5, 0.06, 0.44))
    return m


# ---- detalhes reutilizáveis das máquinas -------------------------------------------
def bolt_ring(m, mat, center, radius, count, size=0.035, base=I4, obj=None):
    """Parafusos em círculo no plano horizontal (flanges, tampas)."""
    x, y, z = center
    for i in range(count):
        a = 2 * math.pi * i / count
        m.cyl(mat, (x + math.cos(a) * radius, y + math.sin(a) * radius, z), size, size, size * 0.8, seg=6,
              base=base, obj=obj)


def hazard_strip(m, center, length, axis="x", height=0.08, depth=0.015, base=I4):
    """Faixa amarela e preta (alerta) de `length` metros ao longo do eixo dado."""
    m.material("hazard_y", (0.95, 0.72, 0.08), 0.6)
    m.material("hazard_k", (0.06, 0.06, 0.07), 0.7)
    n = max(2, int(length / 0.14))
    for i in range(n):
        t = -length / 2 + (i + 0.5) * length / n
        p = (center[0] + t, center[1], center[2]) if axis == "x" else (center[0], center[1] + t, center[2])
        s = (length / n, depth, height) if axis == "x" else (depth, length / n, height)
        m.box("hazard_y" if i % 2 == 0 else "hazard_k", p, s, base=base)


def pipe_run(m, mat, clamp_mat, a, b, r, clamps=3, base=I4, obj=None):
    """Tubo reto com abraçadeiras igualmente espaçadas."""
    a, b = Vector(a), Vector(b)
    m.tube(mat, a, b, r, seg=10, base=base, obj=obj)
    for k in range(1, clamps + 1):
        p = a + (b - a) * (k / (clamps + 1))
        m.cyl(clamp_mat, None, r * 1.35, r * 1.35, r * 1.2, seg=10, M=T(*p) @ aim(a, b), base=base, obj=obj)


def cable(m, mat, a, b, sag=0.25, r=0.02, segments=6, base=I4):
    """Cabo pendurado entre dois pontos (catenária aproximada)."""
    a, b = Vector(a), Vector(b)
    pts = [a + (b - a) * (i / segments) - Vector((0, 0, sag * math.sin(math.pi * i / segments)))
           for i in range(segments + 1)]
    for p, q in zip(pts, pts[1:]):
        m.tube(mat, p, q, r, seg=6, base=base)


def indicator(m, pos):
    m.material("glow_run", (0.3, 1.0, 0.4), 0.4)
    m.blob("glow_run", pos, (0.09, 0.09, 0.09), sub=1, noise=0, seed=1, obj="Indicator", smooth=True)


def airfoil_blade(m, mat, length, root_chord, tip_chord, root_twist, tip_twist, thick=0.15, sections=18,
                  base=I4, obj=None, camber=0.035, tip_mat=None, tip_from=0.9):
    """Pá aerodinâmica: seções de perfil NACA (espessura simétrica + curvatura), corda que afina da raiz à ponta e
    torção que gira cada seção em torno do eixo da pá. Eixo da pá = +Z, corda em X, espessura em Y."""
    n_pts = 14
    betas = [math.pi * k / (n_pts - 1) for k in range(n_pts)]
    xs = [(1 - math.cos(b)) / 2 for b in betas]

    def thickness(x):
        return 5 * thick * (0.2969 * math.sqrt(x) - 0.126 * x - 0.3516 * x ** 2 + 0.2843 * x ** 3 - 0.1015 * x ** 4)

    outline = [(x, thickness(x) + camber * 4 * x * (1 - x)) for x in xs] + \
              [(x, -thickness(x) + camber * 4 * x * (1 - x)) for x in reversed(xs[1:-1])]
    ring = len(outline)
    verts = []
    for s in range(sections):
        t = s / (sections - 1)
        # Raiz cilíndrica arredondada (primeiros 8%), depois corda máxima perto de 18% e afina até a ponta.
        if t < 0.08:
            chord = root_chord * (0.45 + 0.55 * t / 0.08)
            th = 1.0 + (0.08 - t) / 0.08 * 1.8
        else:
            u = (t - 0.08) / 0.92
            chord = tip_chord + (root_chord - tip_chord) * (1 - u) ** 1.7
            th = 1.0
        twist = math.radians(root_twist * (1 - t) ** 1.6 + tip_twist * t)
        ct, st = math.cos(twist), math.sin(twist)
        for (x, y) in outline:
            px, py = (x - 0.3) * chord, y * chord * th
            verts.append((px * ct - py * st, px * st + py * ct, t * length))
    faces = []
    for s in range(sections - 1):
        for j in range(ring):
            j2 = (j + 1) % ring
            a, b = s * ring + j, s * ring + j2
            faces.append((a, b, b + ring, a + ring))
    root_cap = tuple(reversed(range(ring)))
    tip_cap = tuple(range((sections - 1) * ring, sections * ring))
    if tip_mat:
        # Ponta pintada: separa as últimas seções num material próprio.
        split = int(tip_from * (sections - 1))
        body = [f for f in faces if f[0] // ring < split]
        tipf = [f for f in faces if f[0] // ring >= split]
        m.poly(mat, verts, body + [root_cap], base=base, obj=obj, smooth=True)
        m.poly(tip_mat, verts, tipf + [tip_cap], base=base, obj=obj, smooth=True)
    else:
        m.poly(mat, verts, faces + [root_cap, tip_cap], base=base, obj=obj, smooth=True)


def wind_turbine():
    """Gerador eólico (~8,5 m): base de concreto com parafusos, torre cônica com flanges, porta e escada, nacele
    aerodinâmica com aletas de resfriamento, anemômetro e biruta, cubo carenado e rotor de três pás de perfil
    aerodinâmico torcido com pontas vermelhas (Rotor gira no eixo da frente)."""
    m = Model("wind_turbine", "Machines")
    m.greeble = False
    m.material("white", (0.92, 0.93, 0.95), 0.35, 0.05)
    m.material("grey", (0.45, 0.47, 0.5), 0.5, 0.5)
    m.material("dark", (0.14, 0.15, 0.16), 0.6, 0.3)
    m.material("concrete", (0.58, 0.57, 0.55), 0.9)
    m.material("red", (0.85, 0.15, 0.12), 0.45)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    # Fundação com parafusos de ancoragem e flange da torre.
    m.lathe("concrete", (0, 0, 0), [(0, 0), (1.15, 0), (1.15, 0.18), (0.95, 0.3), (0, 0.3)], seg=32)
    bolt_ring(m, "steel", (0, 0, 0.32), 0.62, 16, size=0.04)
    m.lathe("grey", (0, 0, 0.3), [(0.58, 0), (0.58, 0.14), (0.47, 0.16)], seg=32, caps=False)
    # Torre em três seções cônicas, com flange e anel de parafusos entre elas.
    sections = [(0.3, 2.5, 0.44, 0.36), (2.8, 2.2, 0.36, 0.30), (5.0, 2.15, 0.30, 0.24)]
    for z, h, r0, r1 in sections:
        m.lathe("white", (0, 0, z), [(r0, 0), (r1, h)], seg=36, caps=False)
    for z, r in ((2.8, 0.36), (5.0, 0.30)):
        m.lathe("grey", (0, 0, z - 0.04), [(r + 0.04, 0), (r + 0.04, 0.09)], seg=36, caps=False)
        bolt_ring(m, "steel", (0, 0, z + 0.06), r + 0.045, 20, size=0.018)
    m.box("grey", (0, -0.43, 1.05), (0.42, 0.06, 1.3), bevel=0.02)
    m.box("dark", (0, -0.46, 1.05), (0.34, 0.02, 1.2))
    m.box("steel", (0.12, -0.47, 1.05), (0.03, 0.03, 0.12))
    for z in range(14):  # escada externa e guarda-corpo
        zz = 1.9 + z * 0.36
        r = 0.44 - (0.44 - 0.24) * (zz - 0.3) / 6.85
        m.box("steel", (0, r + 0.12, zz), (0.32, 0.03, 0.03))
    for sx in (-0.15, 0.15):
        m.tube("steel", (sx, 0.52, 1.9), (sx, 0.38, 6.8), 0.015, seg=5)
    # Nacele aerodinâmica (revolução ao longo de Y) + capô de acesso e aletas.
    nac = T(0, 0.15, 7.45) @ R(-90, 0, 0)
    m.lathe("white", (0, 0, 0), [(0.0, -0.98), (0.2, -0.93), (0.34, -0.74), (0.41, -0.35), (0.43, 0.3), (0.4, 0.8),
                                  (0.32, 1.1), (0.18, 1.24), (0.0, 1.28)], seg=32, base=nac)
    m.box("white", (0, 0.35, 7.88), (0.34, 0.9, 0.1), bevel=0.04)
    m.box("grey", (0, 0.35, 7.935), (0.26, 0.74, 0.02))
    for i in range(7):
        m.box("dark", (0.0, 0.1 + i * 0.12, 7.85), (0.2, 0.025, 0.06))
    m.box("grey", (0, 1.28, 7.45), (0.12, 0.08, 0.16))
    # Anemômetro de copos e biruta no topo.
    m.tube("steel", (0, 0.9, 7.9), (0, 0.9, 8.5), 0.018, seg=8)
    for k in range(3):
        a = k * 2 * math.pi / 3
        m.tube("steel", (0, 0.9, 8.5), (math.cos(a) * 0.14, 0.9 + math.sin(a) * 0.14, 8.5), 0.008, seg=5)
        m.blob("steel", (math.cos(a) * 0.15, 0.9 + math.sin(a) * 0.15, 8.5), (0.035, 0.035, 0.035), sub=1, noise=0, seed=k)
    m.tube("steel", (0.3, 0.75, 7.9), (0.3, 0.75, 8.35), 0.012, seg=6)
    m.box("red", (0.3, 0.98, 8.34), (0.015, 0.4, 0.07))
    m.blob("red", (0.3, 0.75, 8.4), (0.03, 0.03, 0.03), sub=1, noise=0, seed=7)
    # Cubo carenado e pás. O rotor gira em torno do eixo Y a partir do cubo.
    hub = (0, -1.1, 7.45)
    m.origins["Rotor"] = hub
    m.lathe("white", (0, 0, 0), [(0.0, -0.5), (0.1, -0.46), (0.22, -0.34), (0.3, -0.12), (0.34, 0.1), (0.33, 0.2),
                                  (0.0, 0.2)], seg=32, obj="Rotor", base=T(*hub) @ R(-90, 0, 0))
    for i in range(3):
        rot = R(0, i * 120, 0)
        # Mancal de passo (anel) e pá torcida de 3,9 m.
        m.lathe("grey", (0, 0, 0), [(0.13, 0.0), (0.145, 0.04), (0.145, 0.22), (0.13, 0.26)], seg=24, caps=False,
                obj="Rotor", base=T(*hub) @ rot @ T(0, 0, 0.3))
        airfoil_blade(m, "white", 3.9, 0.62, 0.1, 16, -3, thick=0.16, obj="Rotor", tip_mat="red", tip_from=0.9,
                      base=T(*hub) @ rot @ T(0, 0, 0.4))
    indicator(m, (0.25, -0.47, 1.8))
    return m




def heater():
    """Aquecedor (~1.6 x 1.6 m): chassi sobre pés amortecidos, aletas de dissipação nas laterais, grade de
    exaustão sobre a câmara incandescente, chaminés com flange, caixa de controle e cabo de força."""
    m = Model("heater", "Machines")
    m.greeble = False
    m.material("body", (0.30, 0.31, 0.34), 0.55, 0.3)
    m.material("dark", (0.12, 0.12, 0.14), 0.6, 0.3)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    m.material("glow_heat", (1.0, 0.42, 0.1), 0.5)
    m.material("pipe", (0.6, 0.45, 0.3), 0.4, 0.7)
    m.material("rust_edge", (0.4, 0.22, 0.12), 0.8, 0.5)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("dark", (sx * 0.62, sy * 0.62, 0.04), (0.32, 0.32, 0.08), bevel=0.02)
            m.cyl("steel", (sx * 0.62, sy * 0.62, 0.14), 0.09, 0.11, 0.14, seg=10)
    m.box("body", (0, 0, 0.62), (1.5, 1.5, 0.86), bevel=0.06)
    m.box("dark", (0, 0, 0.21), (1.56, 1.56, 0.06))
    m.box("dark", (0, 0, 1.04), (1.56, 1.56, 0.06))
    for side in (-1, 1):  # aletas
        for i in range(9):
            m.box("dark", (-0.64 + i * 0.16, side * 0.79, 0.62), (0.04, 0.09, 0.7))
            m.box("dark", (side * 0.79, -0.64 + i * 0.16, 0.62), (0.09, 0.04, 0.7))
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("steel", (sx * 0.74, sy * 0.74, 0.62), (0.08, 0.08, 0.9), bevel=0.01)
    # Câmara com grade
    m.cyl("dark", (0, 0, 1.12), 0.64, 0.64, 0.12, seg=24)
    m.cyl("glow_heat", (0, 0, 1.17), 0.54, 0.54, 0.04, seg=24)
    for i in range(9):
        m.box("steel", (-0.48 + i * 0.12, 0, 1.22), (0.025, 1.08 * math.sqrt(max(0.05, 1 - ((-0.48 + i * 0.12) / 0.54) ** 2)), 0.03))
    m.box("steel", (0, 0, 1.235), (1.1, 0.04, 0.03))
    bolt_ring(m, "steel", (0, 0, 1.19), 0.6, 16, size=0.025)
    # Chaminés
    for (x, y, r, h) in [(0.55, 0.45, 0.13, 1.0), (-0.45, 0.52, 0.09, 0.6)]:
        m.cyl("pipe", (x, y, 1.12 + h / 2), r, r * 0.92, h, seg=14)
        m.cyl("dark", (x, y, 1.15), r * 1.5, r * 1.5, 0.06, seg=14)
        bolt_ring(m, "steel", (x, y, 1.19), r * 1.3, 8, size=0.018)
        m.cyl("dark", (x, y, 1.12 + h + 0.03), r * 1.35, r * 1.35, 0.07, seg=14)
        m.cyl("rust_edge", (x, y, 1.12 + h * 0.7), r + 0.008, r + 0.008, 0.05, seg=14)
    # Caixa de controle, visor e cabo
    m.box("body", (0.0, -0.84, 0.78), (0.52, 0.16, 0.4), bevel=0.02)
    m.box("dark", (0.0, -0.93, 0.82), (0.3, 0.02, 0.16))
    m.box("glow_heat", (-0.08, -0.94, 0.82), (0.12, 0.01, 0.08))
    hazard_strip(m, (0, -0.79, 0.3), 1.3, "x", height=0.07)
    cable(m, "dark", (0.2, -0.9, 0.6), (0.6, -1.6, 0.03), sag=0.1, r=0.025)
    indicator(m, (-0.6, -0.8, 0.95))
    return m


def drill():
    """Perfuratriz (~4 m): plataforma com sapatas, torre de treliça amarela com contraventamento, cabeçote do motor
    com aletas, haste de broca com colar, mangueira hidráulica, painel de controle e corrimão."""
    m = Model("drill", "Machines")
    m.greeble = False
    m.material("yellow", (0.92, 0.7, 0.14), 0.55)
    m.material("dark", (0.15, 0.15, 0.17), 0.6, 0.3)
    m.material("steel", (0.65, 0.67, 0.7), 0.35, 0.8)
    m.material("rubber", (0.06, 0.06, 0.06), 0.9)
    m.material("glow_panel", (0.4, 0.95, 0.6), 0.4)
    m.box("dark", (0, 0, 0.12), (2.2, 2.2, 0.24), bevel=0.04)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("steel", (sx * 1.0, sy * 1.0, 0.03), (0.4, 0.4, 0.06), bevel=0.01)
            bolt_ring(m, "steel", (sx * 1.0, sy * 1.0, 0.27), 0.12, 4, size=0.025)
    m.box("steel", (0, 0, 0.25), (0.7, 0.7, 0.04))
    hazard_strip(m, (0, -1.11, 0.12), 2.1, "x", height=0.12)
    hazard_strip(m, (0, 1.11, 0.12), 2.1, "x", height=0.12)
    # Torre: quatro pernas, travessas e diagonais em X
    legs = [Vector((sx * 0.85, sy * 0.85, 0.24)) for sx in (-1, 1) for sy in (-1, 1)]
    tops = [Vector((sx * 0.28, sy * 0.28, 3.15)) for sx in (-1, 1) for sy in (-1, 1)]
    for a, b in zip(legs, tops):
        m.tube("yellow", a, b, 0.07, 0.06, seg=8)
    def at(i, t):
        return legs[i] + (tops[i] - legs[i]) * t
    faces = [(0, 1), (1, 3), (3, 2), (2, 0)]
    for t0, t1 in ((0.0, 0.33), (0.33, 0.66), (0.66, 0.92)):
        for i, j in faces:
            m.tube("yellow", at(i, t1), at(j, t1), 0.04, seg=6)
            m.tube("yellow", at(i, t0), at(j, t1), 0.025, seg=5)
            m.tube("yellow", at(j, t0), at(i, t1), 0.025, seg=5)
    # Cabeçote do motor
    m.box("yellow", (0, 0, 3.4), (0.95, 0.95, 0.6), bevel=0.06)
    for i in range(6):
        m.box("dark", (0.49, -0.3 + i * 0.12, 3.4), (0.03, 0.05, 0.45))
        m.box("dark", (-0.49, -0.3 + i * 0.12, 3.4), (0.03, 0.05, 0.45))
    m.cyl("dark", (0, 0, 3.8), 0.32, 0.32, 0.22, seg=16)
    bolt_ring(m, "steel", (0, 0, 3.92), 0.26, 10, size=0.02)
    m.cyl("steel", (0, 0, 4.05), 0.12, 0.08, 0.3, seg=10)
    m.blob("glow_panel", (0, 0, 4.25), (0.05, 0.05, 0.05), sub=1, noise=0, seed=3)
    # Haste da broca com colares e ponta
    m.cyl("steel", (0, 0, 1.55), 0.12, 0.12, 3.2, seg=14)
    for z in (0.9, 1.7, 2.5):
        m.cyl("dark", (0, 0, z), 0.17, 0.17, 0.08, seg=14)
    for i in range(6):
        m.cyl("steel", (0, 0, 0.15 + i * 0.1), 0.2, 0.2, 0.04, seg=12)
    m.cyl("steel", (0, 0, -0.05), 0.2, 0.01, 0.4, seg=12)
    # Mangueira hidráulica e unidade de potência
    m.box("dark", (0.85, -0.85, 0.48), (0.45, 0.45, 0.48), bevel=0.03)
    m.cyl("steel", (0.85, -0.85, 0.76), 0.12, 0.12, 0.1, seg=10)
    cable(m, "rubber", (0.85, -0.85, 0.8), (0.25, -0.25, 3.25), sag=0.35, r=0.04, segments=8)
    # Painel e corrimão
    m.box("dark", (-0.75, -1.0, 0.75), (0.5, 0.12, 0.6), bevel=0.02, rot=(-15, 0, 0))
    m.box("glow_panel", (-0.75, -1.07, 0.82), (0.3, 0.01, 0.2), rot=(-15, 0, 0))
    for x in (-1.05, -0.45):
        m.tube("yellow", (x, -1.05, 0.24), (x, -1.05, 1.1), 0.025, seg=6)
    m.tube("yellow", (-1.05, -1.05, 1.1), (-0.45, -1.05, 1.1), 0.025, seg=6)
    indicator(m, (0, -1.12, 0.3))
    return m


def solar_panel():
    """Painel solar (~2,6 x 1,6 m): sapata com parafusos, mastro com motor de rastreamento e atuador linear, estrutura
    traseira em perfis C, moldura de alumínio com lábio, 60 células monocristalinas com barramentos e caixa de
    junção com cabo."""
    m = Model("solar_panel", "Machines")
    m.greeble = False
    m.material("metal", (0.65, 0.67, 0.7), 0.4, 0.7)
    m.material("alu", (0.78, 0.8, 0.83), 0.3, 0.85)
    m.material("dark", (0.1, 0.11, 0.12), 0.6, 0.3)
    m.material("cell", (0.035, 0.07, 0.22), 0.12, 0.45)
    m.material("cell_edge", (0.012, 0.02, 0.05), 0.2, 0.3)
    m.material("grid", (0.82, 0.84, 0.88), 0.25, 0.9)
    m.material("concrete", (0.58, 0.57, 0.55), 0.9)
    m.box("concrete", (0, 0, 0.1), (0.95, 0.95, 0.2), bevel=0.03)
    bolt_ring(m, "metal", (0, 0, 0.21), 0.3, 8, size=0.028)
    m.cyl("metal", (0, 0, 0.23), 0.22, 0.22, 0.06, seg=16)
    m.cyl("metal", (0, 0, 0.85), 0.09, 0.11, 1.25, seg=14)
    for z in (0.5, 1.2):
        m.lathe("dark", (0, 0, z), [(0.115, -0.02), (0.115, 0.02)], seg=16, caps=False)
    # Cabeça de rastreamento: caixa do motor, eixo horizontal e atuador linear até a traseira do painel.
    m.box("dark", (0, 0, 1.52), (0.4, 0.34, 0.26), bevel=0.03)
    m.cyl("metal", (0, 0, 1.68), 0.07, 0.07, 0.5, seg=12, rot=(0, 90, 0))
    tilt = T(0, 0, 1.78) @ R(-28, 0, 0)
    m.tube("metal", (0, -0.12, 1.12), (0, -0.42, 1.68), 0.035, seg=10)
    m.tube("alu", (0, -0.42, 1.68), (0, -0.55, 1.96), 0.022, seg=10)
    m.cyl("dark", (0, -0.12, 1.12), 0.05, 0.05, 0.1, seg=10, rot=(0, 90, 0))
    # Estrutura traseira: perfis C longitudinais e travessas.
    for x in (-0.95, -0.35, 0.35, 0.95):
        m.box("metal", (x, 0, -0.07), (0.05, 1.42, 0.07), bevel=0.008, base=tilt)
    for y in (-0.55, 0.0, 0.55):
        m.box("metal", (0, y, -0.12), (2.2, 0.06, 0.05), bevel=0.008, base=tilt)
    # Moldura de alumínio: perfil com aba externa e lábio sobre o vidro.
    W, H = 2.56, 1.56
    for sy in (-1, 1):
        m.box("alu", (0, sy * (H / 2 - 0.022), 0.0), (W, 0.044, 0.056), bevel=0.006, base=tilt)
        m.box("alu", (0, sy * (H / 2 - 0.05), 0.026), (W - 0.04, 0.012, 0.014), base=tilt)
    for sx in (-1, 1):
        m.box("alu", (sx * (W / 2 - 0.022), 0, 0.0), (0.044, H - 0.088, 0.056), bevel=0.006, base=tilt)
        m.box("alu", (sx * (W / 2 - 0.05), 0, 0.026), (0.012, H - 0.1, 0.014), base=tilt)
    m.box("cell_edge", (0, 0, 0.012), (W - 0.1, H - 0.1, 0.02), base=tilt)
    # 60 células (10 x 6) com cantos chanfrados e 3 barramentos cada.
    cols, rows = 10, 6
    cw, ch = (W - 0.16) / cols, (H - 0.16) / rows
    for i in range(cols):
        for j in range(rows):
            cx, cy = -(W - 0.16) / 2 + (i + 0.5) * cw, -(H - 0.16) / 2 + (j + 0.5) * ch
            m.box("cell", (cx, cy, 0.027), (cw - 0.016, ch - 0.016, 0.012), bevel=0.012, base=tilt)
            for b in (-1, 0, 1):
                m.box("grid", (cx + b * cw * 0.28, cy, 0.0343), (0.0035, ch - 0.03, 0.002), base=tilt)
    # Caixa de junção, diodos e cabo.
    m.box("dark", (0.5, 0.2, -0.1), (0.24, 0.17, 0.07), bevel=0.012, base=tilt)
    m.box("metal", (0.5, 0.2, -0.14), (0.05, 0.05, 0.03), base=tilt)
    cable(m, "dark", (0.08, 0.08, 1.45), (0.3, 0.6, 0.05), sag=0.15, r=0.016)
    return m


def vegetube():
    """Vegetubo (~2,7 m): base técnica com bomba e mangueiras, cilindro de vidro com anéis e montantes, barras de luz
    de cultivo, solo e raízes, tampa com ventilação e cabo. As plantas são scans do Poly Haven montados no Unity."""
    m = Model("vegetube", "Machines")
    m.greeble = False
    m.material("dark", (0.2, 0.21, 0.24), 0.55, 0.3)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    m.material("glass_tube", (0.65, 0.9, 0.8), 0.1)
    m.material("soil", (0.22, 0.15, 0.1), 0.95)
    m.material("root", (0.34, 0.26, 0.18), 0.9)
    m.material("glow_grow", (1.0, 0.35, 0.85), 0.4)
    m.lathe("dark", (0, 0, 0), [(0, 0), (0.78, 0), (0.78, 0.08), (0.7, 0.12), (0.7, 0.3), (0.6, 0.34), (0, 0.34)], seg=24)
    bolt_ring(m, "steel", (0, 0, 0.35), 0.64, 16, size=0.025)
    m.lathe("soil", (0, 0, 0.34), [(0, 0.02), (0.3, 0.05), (0.5, 0.0), (0.5, 0.0)], seg=32, caps=False)
    m.cyl("soil", (0, 0, 0.37), 0.5, 0.5, 0.06, seg=32)
    rng = random.Random(8)
    for i in range(22):  # montículos de terra e raízes aparentes
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.05, 0.42)
        m.blob("soil", (math.cos(a) * r, math.sin(a) * r, 0.41), (0.09, 0.08, 0.04), sub=1, noise=0.25, seed=i, smooth=True)
    for i in range(7):
        a = rng.uniform(0, 2 * math.pi)
        p0 = Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.43))
        p1 = Vector((math.cos(a) * rng.uniform(0.18, 0.4), math.sin(a) * rng.uniform(0.18, 0.4), 0.41))
        m.tube("root", p0, p1, 0.012, 0.006, seg=6)
    m.cyl("glass_tube", (0, 0, 1.4), 0.52, 0.52, 2.0, seg=32)
    for z in (0.42, 1.0, 1.7, 2.33):
        m.lathe("steel", (0, 0, z), [(0.53, -0.03), (0.56, 0), (0.53, 0.03)], seg=32, caps=False)
        bolt_ring(m, "steel", (0, 0, z + 0.035), 0.545, 12, size=0.014)
    for i in range(4):  # montantes verticais
        a = math.radians(45 + i * 90)
        m.box("steel", (math.cos(a) * 0.55, math.sin(a) * 0.55, 1.4), (0.045, 0.045, 2.0), rot=(0, 0, math.degrees(a)), bevel=0.008)
    for i in range(3):  # barras de luz de cultivo com suportes
        a = math.radians(i * 120 + 20)
        x, y = math.cos(a) * 0.47, math.sin(a) * 0.47
        m.box("glow_grow", (x, y, 1.4), (0.02, 0.05, 1.7), rot=(0, 0, math.degrees(a)))
        for z in (0.6, 1.4, 2.2):
            m.box("dark", (x * 1.05, y * 1.05, z), (0.04, 0.06, 0.05), rot=(0, 0, math.degrees(a)))
    m.lathe("dark", (0, 0, 2.38), [(0.58, 0), (0.58, 0.12), (0.4, 0.22), (0.2, 0.26), (0, 0.26)], seg=32)
    m.cyl("steel", (0, 0, 2.68), 0.12, 0.12, 0.08, seg=16)
    for i in range(6):
        m.box("dark", (0, 0, 2.73), (0.22, 0.02, 0.02), rot=(0, 0, i * 30))
    # Bomba lateral, manômetro, mangueiras com abraçadeiras e cabo
    m.box("dark", (0.74, 0.0, 0.22), (0.32, 0.38, 0.4), bevel=0.03)
    m.cyl("steel", (0.74, 0.0, 0.46), 0.08, 0.08, 0.08, seg=16)
    m.cyl("steel", (0.92, 0.0, 0.3), 0.07, 0.07, 0.04, seg=16, rot=(0, 90, 0))
    m.cyl("dark", (0.935, 0.0, 0.3), 0.055, 0.055, 0.015, seg=16, rot=(0, 90, 0))
    pipe_run(m, "steel", "dark", (0.74, 0.1, 0.42), (0.56, 0.1, 2.4), 0.028, clamps=4)
    pipe_run(m, "steel", "dark", (0.74, -0.1, 0.42), (0.56, -0.1, 0.52), 0.028, clamps=1)
    cable(m, "dark", (0.9, 0, 0.12), (1.35, 0.3, 0.02), sag=0.05, r=0.02, segments=4)
    indicator(m, (0, -0.72, 0.28))
    return m


def algae_farm():
    """Fazenda de algas (~3.2 x 2.2 m): tanque com nervuras e borda de inspeção, água verde com bolhas, aeradores,
    torre de circulação com tubulação e bomba, painel com visor, escada de acesso e grade de passarela."""
    m = Model("algae_farm", "Machines")
    m.greeble = False
    m.material("tank", (0.82, 0.84, 0.82), 0.55)
    m.material("dark", (0.18, 0.2, 0.22), 0.55, 0.3)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    m.material("algae", (0.14, 0.52, 0.26), 0.25)
    m.material("algae_b", (0.22, 0.62, 0.3), 0.35)
    m.material("pipe", (0.6, 0.62, 0.66), 0.4, 0.6)
    m.material("glow_ok", (0.4, 1.0, 0.6), 0.4)
    m.box("dark", (0, 0, 0.1), (3.3, 2.3, 0.2), bevel=0.03)
    m.box("tank", (0, 0, 0.52), (3.0, 2.0, 0.64), bevel=0.06)
    for i in range(7):  # nervuras
        x = -1.35 + i * 0.45
        for sy in (-1, 1):
            m.box("tank", (x, sy * 1.02, 0.52), (0.08, 0.06, 0.6), bevel=0.01)
    for sx in (-1, 1):
        for j in range(4):
            m.box("tank", (sx * 1.52, -0.75 + j * 0.5, 0.52), (0.06, 0.08, 0.6), bevel=0.01)
    m.box("steel", (0, 0, 0.87), (3.08, 2.08, 0.06), bevel=0.01)
    m.box("algae", (0, 0, 0.83), (2.82, 1.82, 0.04))
    rng = random.Random(51)
    for i in range(14):
        m.blob("algae_b", (rng.uniform(-1.3, 1.3), rng.uniform(-0.8, 0.8), 0.855),
               (rng.uniform(0.12, 0.3), rng.uniform(0.1, 0.24), 0.02), sub=1, noise=0.2, seed=i)
    for i in range(10):
        m.blob("glow_ok", (rng.uniform(-1.3, 1.3), rng.uniform(-0.8, 0.8), 0.86), (0.025, 0.025, 0.015), sub=1,
               noise=0, seed=i)
    for x in (-0.9, 0.0, 0.9):  # barras de aeração
        m.box("steel", (x, 0, 0.86), (0.04, 1.8, 0.03))
    # Passarela com grade sobre o tanque
    for k in range(9):
        m.box("steel", (-0.6 + k * 0.15, 0, 0.93), (0.03, 2.05, 0.03))
    m.box("steel", (0, 1.0, 0.93), (1.3, 0.04, 0.05))
    m.box("steel", (0, -1.0, 0.93), (1.3, 0.04, 0.05))
    # Torre de circulação e tubulação
    m.cyl("tank", (1.25, 0.75, 1.3), 0.28, 0.28, 1.0, seg=18)
    m.cyl("dark", (1.25, 0.75, 1.82), 0.3, 0.3, 0.06, seg=18)
    bolt_ring(m, "steel", (1.25, 0.75, 1.86), 0.24, 8, size=0.02)
    pipe_run(m, "pipe", "dark", (1.25, 0.75, 1.9), (1.25, 0.75, 2.2), 0.07, clamps=1)
    pipe_run(m, "pipe", "dark", (1.25, 0.75, 2.2), (-1.2, 0.75, 2.2), 0.07, clamps=4)
    pipe_run(m, "pipe", "dark", (-1.2, 0.75, 2.2), (-1.2, 0.75, 0.9), 0.07, clamps=2)
    m.box("dark", (-1.2, 0.75, 1.3), (0.24, 0.24, 0.3), bevel=0.03)
    # Painel e escada
    m.box("dark", (1.35, -0.92, 1.12), (0.5, 0.25, 0.72), bevel=0.04)
    m.box("glow_ok", (1.35, -1.06, 1.25), (0.26, 0.01, 0.14))
    m.box("steel", (1.35, -1.06, 0.98), (0.3, 0.01, 0.05))
    for x in (-0.5, -0.1):
        m.tube("steel", (x, -1.2, 0.0), (x, -1.06, 1.05), 0.022, seg=6)
    for k in range(4):
        m.tube("steel", (-0.5, -1.18 + k * 0.035, 0.22 + k * 0.22), (-0.1, -1.18 + k * 0.035, 0.22 + k * 0.22), 0.018, seg=5)
    hazard_strip(m, (0.6, -1.16, 0.2), 1.0, "x", height=0.08)
    indicator(m, (-1.3, -1.12, 0.65))
    # Banco de fotobiorreatores: tubos de vidro verticais com cultura verde, abraçadeiras e cabeçotes ligados à torre.
    m.material("glass_tube", (0.75, 0.92, 0.85), 0.05)
    m.material("culture", (0.1, 0.55, 0.2), 0.2)
    for k in range(6):
        x = 0.42 + k * 0.26
        m.cyl("glass_tube", (x, -1.05, 1.1), 0.095, 0.095, 1.3, seg=16)
        m.cyl("culture", (x, -1.05, 0.95), 0.078, 0.078, 0.95, seg=16)
        m.cyl("steel", (x, -1.05, 1.78), 0.1, 0.1, 0.05, seg=16)
        m.cyl("steel", (x, -1.05, 0.46), 0.1, 0.1, 0.05, seg=16)
        for z in (0.8, 1.45):
            m.lathe("dark", (x, -1.05, z), [(0.1, -0.015), (0.1, 0.015)], seg=16, caps=False)
        for z in (0.6, 1.0, 1.35):  # bolhas de aeração
            m.blob("glass_tube", (x + 0.02 * (k % 2), -1.05, z), (0.014, 0.014, 0.014), sub=1, noise=0, seed=k, smooth=True)
    m.box("steel", (1.05, -1.05, 1.85), (1.7, 0.08, 0.05))
    m.box("steel", (1.05, -1.05, 0.42), (1.7, 0.1, 0.06))
    pipe_run(m, "pipe", "dark", (0.35, -1.05, 1.85), (-0.5, 0.75, 2.2), 0.045, clamps=2)
    # Bomba de recirculação com motor aletado.
    m.cyl("dark", (-1.0, -1.1, 0.35), 0.16, 0.16, 0.4, seg=20, rot=(0, 90, 0))
    for i in range(7):
        m.lathe("steel", (-1.0 + (i - 3) * 0.05, -1.1, 0.35), [(0.17, -0.01), (0.17, 0.01)], seg=20, caps=False,
                base=R(0, 90, 0))
    m.box("dark", (-1.35, -1.1, 0.35), (0.2, 0.26, 0.26), bevel=0.02)
    return m


def storage_chest():
    """Baú de carga (~1.5 m): caixa reforçada com cantoneiras, tampa com dobradiças e alças, travas laterais,
    trava eletrônica com visor e etiqueta de inventário."""
    m = Model("storage_chest", "Machines")
    m.greeble = False
    m.material("body", (0.42, 0.45, 0.5), 0.5, 0.5)
    m.material("dark", (0.14, 0.15, 0.17), 0.6, 0.3)
    m.material("steel", (0.62, 0.64, 0.67), 0.35, 0.8)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("label", (0.85, 0.85, 0.8), 0.7)
    m.material("glow_lock", (0.4, 0.85, 1.0), 0.4)
    m.box("dark", (0, 0, 0.06), (1.5, 0.95, 0.12), bevel=0.02)
    for sx in (-1, 1):
        m.box("dark", (sx * 0.5, 0, 0.02), (0.14, 0.98, 0.04))
    m.box("body", (0, 0, 0.45), (1.4, 0.85, 0.66), bevel=0.04)
    for z in (0.3, 0.6):  # nervuras horizontais
        m.box("body", (0, 0, z), (1.43, 0.88, 0.05), bevel=0.01)
    m.box("body", (0, 0, 0.88), (1.46, 0.9, 0.2), bevel=0.05)
    m.box("dark", (0, 0, 0.78), (1.47, 0.91, 0.02))
    for sx in (-1, 1):  # cantoneiras
        for sy in (-1, 1):
            m.box("steel", (sx * 0.7, sy * 0.42, 0.5), (0.08, 0.08, 0.78), bevel=0.01)
            m.box("steel", (sx * 0.71, sy * 0.43, 0.95), (0.1, 0.1, 0.1), bevel=0.015)
    for sx in (-0.45, 0.45):  # dobradiças e cintas
        m.box("dark", (sx, 0, 0.55), (0.1, 0.9, 0.9))
        m.cyl("steel", (sx, -0.46, 0.8), 0.03, 0.03, 0.14, seg=8, rot=(0, 90, 0))
    for sx in (-1, 1):  # alças e travas laterais
        m.tube("dark", (sx * 0.74, -0.15, 0.55), (sx * 0.82, -0.15, 0.6), 0.02, seg=6)
        m.tube("dark", (sx * 0.82, -0.15, 0.6), (sx * 0.82, 0.15, 0.6), 0.02, seg=6)
        m.tube("dark", (sx * 0.82, 0.15, 0.6), (sx * 0.74, 0.15, 0.6), 0.02, seg=6)
        m.box("steel", (sx * 0.72, 0, 0.8), (0.04, 0.14, 0.14), bevel=0.01)
    m.box("accent", (0, 0, 0.18), (1.42, 0.87, 0.05))
    m.box("dark", (0, 0.46, 0.7), (0.3, 0.05, 0.26), bevel=0.02)
    m.box("glow_lock", (0, 0.49, 0.74), (0.12, 0.02, 0.08))
    m.box("steel", (0, 0.49, 0.64), (0.16, 0.02, 0.03))
    m.box("label", (0.38, 0.435, 0.45), (0.3, 0.01, 0.18))
    for i in range(4):
        m.box("dark", (0.3 + i * 0.05, 0.43, 0.45), (0.02, 0.012, 0.12))
    return m


def nuclear_reactor():
    """Reator compacto (~4.2 m): laje de concreto com faixas de alerta e grade de drenagem, vaso de contenção com
    anéis de aço e parafusos, janela do núcleo, torres de resfriamento com escadas, tubulação com flanges,
    painel de controle e luzes de aviso."""
    m = Model("nuclear_reactor", "Machines")
    m.greeble = False
    m.material("concrete", (0.62, 0.62, 0.6), 0.85)
    m.material("metal", (0.55, 0.57, 0.6), 0.4, 0.7)
    m.material("dark", (0.14, 0.15, 0.17), 0.6, 0.3)
    m.material("glow_core", (0.35, 1.0, 0.5), 0.4)
    m.material("glow_warn", (1.0, 0.3, 0.15), 0.4)
    m.box("concrete", (0, 0, 0.2), (4.2, 4.2, 0.4), bevel=0.05)
    for side in (-1, 1):
        hazard_strip(m, (0, side * 2.11, 0.25), 3.9, "x", height=0.2)
        hazard_strip(m, (side * 2.11, 0, 0.25), 3.9, "y", height=0.2)
    for i in range(10):  # grade de drenagem
        m.box("dark", (-1.6 + i * 0.08, -1.55, 0.405), (0.03, 0.6, 0.01))
    # Vaso de contenção
    m.lathe("concrete", (0, 0, 0.4), [(1.45, 0), (1.4, 1.6), (1.18, 2.4), (0.72, 2.9), (0.0, 3.05)], seg=32)
    for z, r in ((0.55, 1.46), (1.0, 1.44), (1.95, 1.33)):
        m.lathe("metal", (0, 0, z), [(r, -0.05), (r + 0.04, -0.03), (r + 0.04, 0.03), (r, 0.05)], seg=32, caps=False)
        bolt_ring(m, "dark", (0, 0, z + 0.06), r + 0.02, 24, size=0.025)
    m.lathe("glow_core", (0, 0, 1.25), [(1.435, -0.14), (1.435, 0.14)], seg=32, caps=False)
    for i in range(12):  # montantes sobre a janela do núcleo
        a = 2 * math.pi * i / 12
        m.box("metal", (math.cos(a) * 1.45, math.sin(a) * 1.45, 1.25), (0.06, 0.08, 0.32), rot=(0, 0, math.degrees(a)))
    m.cyl("metal", (0, 0, 3.4), 0.22, 0.22, 0.4, seg=14)
    m.cyl("dark", (0, 0, 3.62), 0.3, 0.3, 0.06, seg=14)
    bolt_ring(m, "metal", (0, 0, 3.66), 0.25, 10, size=0.02)
    m.blob("glow_warn", (0, 0, 3.75), (0.07, 0.07, 0.07), sub=1, noise=0, seed=2)
    # Torres de resfriamento com escada
    for sx in (-1, 1):
        cx, cy = sx * 1.6, 1.3
        m.lathe("concrete", (cx, cy, 0.4), [(0.58, 0), (0.42, 1.0), (0.46, 2.0), (0.52, 2.12), (0.46, 2.14)], seg=24,
                caps=False)
        m.lathe("metal", (cx, cy, 2.48), [(0.53, -0.03), (0.55, 0), (0.53, 0.03)], seg=24, caps=False)
        for k in range(10):
            z = 0.6 + k * 0.18
            r = 0.58 - (0.58 - 0.42) * min(1, (z - 0.4) / 1.0) if z < 1.4 else 0.42 + 0.04 * (z - 1.4)
            m.box("metal", (cx + sx * (r + 0.06), cy, z), (0.03, 0.28, 0.025))
        pipe_run(m, "metal", "dark", (sx * 0.9, 0.7, 1.0), (sx * 1.3, 1.1, 1.6), 0.12, clamps=1)
    # Tubulação de vapor na frente
    pipe_run(m, "metal", "dark", (-1.2, -1.2, 0.7), (1.2, -1.2, 0.7), 0.09, clamps=4)
    for x in (-1.2, 1.2):
        m.cyl("dark", (x, -1.2, 0.55), 0.13, 0.13, 0.3, seg=12)
    # Painel de controle
    m.box("dark", (1.6, -1.45, 0.9), (0.7, 0.5, 1.0), bevel=0.04)
    m.box("glow_core", (1.6, -1.71, 1.12), (0.4, 0.02, 0.2))
    for i in range(4):
        m.box("glow_warn" if i == 0 else "metal", (1.4 + i * 0.13, -1.71, 0.85), (0.06, 0.02, 0.06))
    # Nervuras meridianas no vaso de contenção (32 costelas de concreto armado) e para-raios.
    prof = [(1.45, 0), (1.4, 1.6), (1.18, 2.4), (0.72, 2.9), (0.0, 3.05)]
    for i in range(24):
        a = 2 * math.pi * i / 24
        pts = [(r * math.cos(a), r * math.sin(a), 0.4 + z) for r, z in prof[:-1]]
        for (x0, y0, z0), (x1, y1, z1) in zip(pts, pts[1:]):
            m.tube("concrete", (x0 * 1.012, y0 * 1.012, z0), (x1 * 1.012, y1 * 1.012, z1), 0.028, seg=6)
    for k in range(3):
        a = 2 * math.pi * k / 3
        m.tube("metal", (math.cos(a) * 0.5, math.sin(a) * 0.5, 3.35), (math.cos(a) * 0.5, math.sin(a) * 0.5, 4.1), 0.014, seg=5)
    # Casa das turbinas: nave com telhado em arco, nervuras e janelas altas, ligada ao vaso por tubulação grossa.
    hall = T(-0.5, -1.55, 0.4)
    m.box("concrete", (0, 0, 0.55), (2.6, 1.3, 1.1), bevel=0.04, base=hall)
    m.lathe("metal", (0, 0, 1.1), [(0.65, 0), (0.62, 0.25), (0.45, 0.42), (0.2, 0.5), (0, 0.52)], seg=24,
            base=hall @ R(0, 90, 0) @ S(1.0, 1.0, 1.0))
    for i in range(9):
        m.box("dark", (-1.2 + i * 0.3, 0.652, 0.7), (0.07, 0.03, 0.42), base=hall)
        m.box("glow_core", (-1.2 + i * 0.3, 0.655, 0.7), (0.05, 0.01, 0.36), base=hall)
    pipe_run(m, "metal", "dark", (0.2, -0.95, 1.3), (0.9, -0.4, 1.3), 0.16, clamps=1)
    # Chaminé de exaustão e escada de acesso ao topo do vaso.
    m.cyl("metal", (-1.3, 1.5, 3.2), 0.1, 0.07, 3.0, seg=14)
    for k in range(20):
        m.box("metal", (1.05, 0.0, 0.7 + k * 0.1), (0.04, 0.3, 0.02), rot=(0, 0, 0))
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


def ruin_bunker():
    """Abrigo de concreto do velho mundo, meio enterrado e rachado (~12 x 9 m), com a porta blindada voltada para a
    origem (onde ficam o terminal e o microchip). O corpo fica atrás, entre y=4 e y=13."""
    m = Model("ruin_bunker", "Props")
    m.material("concrete", (0.52, 0.5, 0.47), 0.95)
    m.material("concrete_dark", (0.3, 0.29, 0.27), 0.95)
    m.material("rust", (0.42, 0.22, 0.12), 0.8, 0.6)
    m.material("stripe", (0.85, 0.65, 0.12), 0.7)
    m.material("scorch", (0.1, 0.09, 0.08), 0.95)
    m.material("glow_lamp", (1.0, 0.45, 0.2), 0.4)
    cy = 8.5
    # Fundação enterrada: em terreno inclinado o bunker é apoiado no ponto mais baixo e o resto entra no chão.
    m.box("concrete_dark", (0, cy, -1.3), (12.3, 9.5, 2.6))
    # Paredes grossas, a de trás mais alta; a frente tem o vão da porta.
    m.box("concrete", (0, cy + 4.2, 1.6), (12, 0.9, 4.2), bevel=0.06)
    for sx in (-1, 1):
        m.box("concrete", (sx * 5.6, cy, 1.4), (0.9, 9.2, 3.8), bevel=0.06)
        m.box("concrete", (sx * 3.6, cy - 4.2, 1.3), (3.9, 0.9, 3.6), bevel=0.06)
    m.box("concrete_dark", (0, cy - 4.2, 3.0), (4.2, 0.9, 0.6), bevel=0.04)
    # Laje do teto partida: placas caídas em ângulos diferentes.
    rng = random.Random(1189)
    for (x, y, rx, ry) in [(-3.0, cy + 2.0, 6, -4), (2.8, cy + 2.4, -9, 7), (-2.4, cy - 2.2, 14, 3), (3.4, cy - 1.6, 4, -18)]:
        m.box("concrete", (x, y, 3.55 + rng.uniform(-0.6, 0.1)), (5.4, 4.0, 0.5), rot=(rx, ry, rng.uniform(-6, 6)),
              bevel=0.05)
    # Porta blindada torta, com faixas de alerta, e a moldura enferrujada.
    m.box("rust", (0, cy - 4.55, 1.25), (3.0, 0.25, 2.5), rot=(0, 0, 4), bevel=0.03)
    for i in range(5):
        m.box("stripe", (-1.2 + i * 0.6, cy - 4.7, 2.3), (0.3, 0.05, 0.25), rot=(0, 0, 4))
    m.box("rust", (0, cy - 4.66, 1.2), (0.5, 0.1, 0.5), rot=(0, 0, 4))
    m.blob("glow_lamp", (1.8, cy - 4.75, 2.8), (0.12, 0.12, 0.12), sub=1, noise=0, seed=3)
    # Marcas de queimado e vergalhões expostos.
    m.box("scorch", (-3.5, cy - 4.68, 2.0), (2.0, 0.05, 1.4), rot=(0, 0, 0))
    for i in range(14):
        x = rng.uniform(-5.5, 5.5)
        y = rng.choice((cy - 4.2, cy + 4.2))
        base = (x, y, rng.uniform(3.2, 3.9))
        tip = (x + rng.uniform(-0.6, 0.6), y + rng.uniform(-0.6, 0.6), base[2] + rng.uniform(0.6, 1.6))
        m.tube("rust", base, tip, 0.03, seg=5)
    # Entulho em volta.
    for i in range(26):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(6.5, 9.5)
        x, y = math.cos(a) * r, cy + math.sin(a) * r * 0.8
        if abs(x) < 2 and y < cy:
            continue
        s = rng.uniform(0.3, 1.2)
        m.box(rng.choice(("concrete", "concrete_dark")), (x, y, s * 0.3), (s, s * rng.uniform(0.6, 1.4), s * 0.6),
              rot=(rng.uniform(-20, 20), rng.uniform(-20, 20), rng.uniform(0, 360)), bevel=0.04)
    return m


def decontaminator():
    """Filtro de radiação (~2.4 x 1.8 x 3.2 m): dois tanques de filtro com anéis brilhantes, carcaça central com a
    turbina de admissão na frente (Rotor, gira no eixo da frente) e a chaminé de exaustão."""
    m = Model("decontaminator", "Machines")
    m.material("body", (0.82, 0.83, 0.8), 0.5, 0.2)
    m.material("dark", (0.16, 0.17, 0.19), 0.6, 0.3)
    m.material("stripe", (0.9, 0.7, 0.1), 0.6)
    m.material("pipe", (0.55, 0.57, 0.6), 0.4, 0.7)
    m.material("glow_filter", (0.3, 1.0, 0.65), 0.4)
    m.box("dark", (0, 0, 0.1), (2.4, 1.8, 0.2), bevel=0.04)
    m.box("body", (0, 0, 0.95), (1.2, 1.3, 1.5), bevel=0.08)
    m.box("stripe", (0, 0.66, 0.3), (1.2, 0.02, 0.12))
    for sx in (-1, 1):
        x = sx * 0.85
        m.lathe("body", (x, 0, 0.2), [(0.42, 0), (0.42, 2.3), (0.3, 2.55), (0, 2.6)], seg=20)
        for z in (0.7, 1.3, 1.9):
            m.lathe("glow_filter", (x, 0, z), [(0.43, -0.06), (0.43, 0.06)], seg=20, caps=False)
        m.tube("pipe", (x, 0, 2.8), (sx * 0.35, 0, 2.0), 0.08, seg=8)
    m.lathe("dark", (0, 0, 1.7), [(0.28, 0), (0.24, 1.4), (0.3, 1.5), (0.3, 1.55)], seg=16, caps=False)
    hub = (0, 0.68, 1.05)
    m.cyl("dark", (0, 0.66, 1.05), 0.52, 0.52, 0.06, seg=24, rot=(90, 0, 0))
    m.origins["Rotor"] = hub
    m.blob("pipe", hub, (0.1, 0.1, 0.1), sub=1, noise=0, seed=1, smooth=True, obj="Rotor")
    for i in range(6):
        m.box("pipe", (0, 0, 0), (0.08, 0.03, 0.42), rot=(0, 20, 0), obj="Rotor",
              base=T(*hub) @ R(0, i * 60, 0) @ T(0, 0.04, 0.24))
    indicator(m, (0.45, 0.66, 1.6))
    return m


def chip_crate():
    """Caixa de microchip (~0.8 m): maleta reforçada com cantoneiras, fechos, alça, fileira de ranhuras e etiqueta."""
    m = Model("chip_crate", "Props")
    m.greeble = False
    m.material("case", (0.25, 0.27, 0.3), 0.5, 0.5)
    m.material("steel", (0.62, 0.64, 0.67), 0.35, 0.8)
    m.material("dark", (0.08, 0.085, 0.09), 0.7, 0.3)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("label", (0.85, 0.85, 0.8), 0.7)
    m.material("glow_chip", (0.3, 0.9, 1.0), 0.4)
    m.box("case", (0, 0, 0.22), (0.8, 0.55, 0.44), bevel=0.04)
    m.box("case", (0, 0, 0.47), (0.82, 0.57, 0.1), bevel=0.03)
    m.box("dark", (0, 0, 0.405), (0.81, 0.56, 0.015))
    m.box("accent", (0, 0, 0.3), (0.82, 0.57, 0.05))
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("steel", (sx * 0.38, sy * 0.26, 0.24), (0.06, 0.06, 0.46), bevel=0.01)
            m.box("steel", (sx * 0.385, sy * 0.265, 0.5), (0.07, 0.07, 0.07), bevel=0.012)
    for sx in (-0.2, 0.2):  # fechos
        m.box("dark", (sx, 0.285, 0.4), (0.1, 0.03, 0.12), bevel=0.01)
        m.box("steel", (sx, 0.3, 0.4), (0.07, 0.015, 0.05))
    m.tube("steel", (-0.12, 0, 0.52), (-0.12, 0, 0.58), 0.015, seg=6)
    m.tube("steel", (0.12, 0, 0.52), (0.12, 0, 0.58), 0.015, seg=6)
    m.tube("steel", (-0.12, 0, 0.58), (0.12, 0, 0.58), 0.015, seg=6)
    for i in range(6):  # ranhuras com chips
        m.box("dark", (-0.25 + i * 0.1, -0.1, 0.535), (0.07, 0.2, 0.012))
        m.box("glow_chip" if i % 2 == 0 else "steel", (-0.25 + i * 0.1, -0.12, 0.543), (0.04, 0.08, 0.008))
    m.box("label", (0.22, -0.285, 0.26), (0.26, 0.01, 0.1))
    for i in range(4):
        m.box("dark", (0.14 + i * 0.05, -0.292, 0.26), (0.02, 0.006, 0.07))
    return m


def log_terminal():
    """Terminal de registros (~1.3 m): base parafusada, coluna com tampa de acesso, console inclinado com moldura,
    tela, teclado de teclas, botões, cabo e antena."""
    m = Model("log_terminal", "Props")
    m.greeble = False
    m.material("metal", (0.4, 0.42, 0.46), 0.5, 0.5)
    m.material("steel", (0.62, 0.64, 0.67), 0.35, 0.8)
    m.material("dark", (0.1, 0.11, 0.13), 0.6, 0.3)
    m.material("accent", (0.9, 0.5, 0.12), 0.6)
    m.material("glow_screen", (0.45, 0.95, 0.75), 0.4)
    m.material("glow_btn", (1.0, 0.45, 0.2), 0.4)
    m.box("dark", (0, 0, 0.06), (0.95, 0.75, 0.12), bevel=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.cyl("steel", (sx * 0.4, sy * 0.3, 0.13), 0.04, 0.04, 0.03, seg=6)
    m.box("metal", (0, 0, 0.62), (0.32, 0.3, 1.0), bevel=0.04)
    m.box("dark", (0, -0.155, 0.42), (0.22, 0.01, 0.34))
    m.box("steel", (0.07, -0.165, 0.42), (0.03, 0.015, 0.06))
    for z in (0.18, 0.95):
        m.box("steel", (0, 0, z), (0.35, 0.33, 0.05), bevel=0.01)
    tilt = T(0, 0.05, 1.25) @ R(-30, 0, 0)
    m.box("dark", (0, 0, 0), (0.9, 0.14, 0.64), bevel=0.03, base=tilt)
    m.box("steel", (0, 0.0, 0.004), (0.86, 0.13, 0.6), bevel=0.012, base=tilt)
    m.box("dark", (0, 0, 0.006), (0.82, 0.12, 0.56), base=tilt @ T(0, -0.005, 0))
    m.box("glow_screen", (0, 0, 0), (0.7, 0.02, 0.44), base=tilt @ T(0, 0.068, 0.04))
    for i in range(5):  # linhas de texto na tela
        m.box("dark", (0, 0, 0), (0.5 - i * 0.04, 0.005, 0.02), base=tilt @ T(-0.04, 0.08, 0.14 - i * 0.07))
    kb = tilt @ T(0, 0.09, -0.24)
    m.box("metal", (0, 0, 0), (0.74, 0.07, 0.12), bevel=0.01, base=kb)
    for r in range(2):
        for c in range(9):
            m.box("dark", (-0.32 + c * 0.08, 0.04, 0.025 - r * 0.045), (0.06, 0.012, 0.032), base=kb)
    for i in range(3):
        m.blob("glow_btn" if i == 0 else "accent", (0.28 + i * 0.05, 0.25, 1.0), (0.015, 0.015, 0.012), sub=1, noise=0, seed=i)
    m.tube("dark", (0.16, 0.12, 0.08), (0.45, 0.45, 0.04), 0.02, seg=6)
    m.tube("steel", (-0.1, 0.0, 0.98), (-0.1, 0.0, 1.55), 0.01, seg=5)
    m.blob("glow_btn", (-0.1, 0.0, 1.56), (0.025, 0.025, 0.025), sub=1, noise=0, seed=9)
    return m


# Planta do casco (x, y) em volta da proa: ponta de flecha facetada com asas recuadas e entalhes na popa.
SHIP_OUTLINE = [(0, 23), (-3.5, 12), (-7, -2), (-14, -18), (-12.5, -21), (-7.5, -17.5), (-5, -21),
                (5, -21), (7.5, -17.5), (12.5, -21), (14, -18), (7, -2), (3.5, 12)]
SHIP_APEX_TOP = (0.0, -8.0, 3.2)
SHIP_APEX_BOTTOM = (0.0, -8.0, -2.4)
SHIP_EDGE = 0.35


def _hull_top_z(x, y):
    """Altura da face superior facetada no ponto (x, y); None se fora do casco."""
    ax, ay, az = SHIP_APEX_TOP
    n = len(SHIP_OUTLINE)
    for i in range(n):
        (x1, y1), (x2, y2) = SHIP_OUTLINE[i], SHIP_OUTLINE[(i + 1) % n]
        det = (y1 - ay) * (x2 - ax) - (x1 - ax) * (y2 - ay)
        if abs(det) < 1e-9:
            continue
        # Coordenadas baricêntricas em relação ao triângulo (ápice, p1, p2).
        l1 = ((y - ay) * (x2 - ax) - (x - ax) * (y2 - ay)) / det
        l2 = ((x - ax) * (y1 - ay) - (y - ay) * (x1 - ax)) / det
        l0 = 1 - l1 - l2
        if min(l0, l1, l2) >= 0:
            return l0 * az + (l1 + l2) * SHIP_EDGE
    return None


def mothership():
    """Nave-mãe (~44 m) em ponta de flecha facetada, em 5 objetos = 5 módulos montados no estaleiro:
    Hull, Engines, Superstructure, Bridge, Hyperdrive. Pivô no centro; proa em +Y (y=23)."""
    m = Model("mothership", "Sky")
    m.material("hull", (0.74, 0.75, 0.78), 0.45, 0.45)
    m.material("hull_dark", (0.27, 0.29, 0.32), 0.5, 0.5)
    m.material("panel", (0.55, 0.57, 0.6), 0.5, 0.4)
    m.material("accent", (0.78, 0.36, 0.12), 0.55, 0.3)
    m.material("glow_engine", (0.45, 0.8, 1.0), 0.4)
    m.material("glow_window", (1.0, 0.88, 0.62), 0.4)
    m.material("glow_hyper", (0.35, 0.65, 1.0), 0.4)

    # --- Casco (Hull): faces em leque a partir de um ápice em cima e outro embaixo (visual facetado).
    n = len(SHIP_OUTLINE)
    verts = [SHIP_APEX_TOP, SHIP_APEX_BOTTOM]
    verts += [(x, y, SHIP_EDGE) for x, y in SHIP_OUTLINE]
    verts += [(x, y, -SHIP_EDGE) for x, y in SHIP_OUTLINE]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((0, 2 + i, 2 + j))
        faces.append((1, 2 + n + j, 2 + n + i))
        faces.append((2 + i, 2 + n + i, 2 + n + j, 2 + j))
    m.poly("hull", verts, faces, obj="Hull")
    rng = random.Random(2077)
    placed = 0
    while placed < 150:
        x, y = rng.uniform(-13, 13), rng.uniform(-20, 20)
        z = _hull_top_z(x, y)
        if z is None or (abs(x) < 2.6 and -20 < y < 4):
            continue
        sx, sy, sz = rng.uniform(0.3, 1.4), rng.uniform(0.4, 2.4), rng.uniform(0.1, 0.35)
        m.box(rng.choice(("panel", "panel", "hull_dark")), (x, y, z + sz / 2 - 0.08), (sx, sy, sz), obj="Hull")
        placed += 1
    for side in (-1, 1):  # faixas de cor no bordo de ataque das asas
        m.tube("accent", (side * 7.2, -2.5, 0.45), (side * 13.6, -17.6, 0.45), 0.12, seg=6, obj="Hull")
        for k in range(10):  # vigias ao longo do flanco
            t = 0.15 + k * 0.07
            x, y = side * (3.5 + (7 - 3.5) * t * 1.4), 12 - 14 * t * 1.4
            if y < -2:
                break
            m.box("glow_window", (x * 1.01, y, 0.0), (0.1, 0.9, 0.14), rot=(0, 0, side * -17), obj="Hull")

    # --- Motores (Engines): dois propulsores centrais na popa e duas naceles sob as asas.
    for x in (-2.6, 2.6):
        m.cyl("hull_dark", (x, -21.9, 0.2), 2.0, 1.7, 2.0, seg=24, rot=(90, 0, 0), obj="Engines")
        m.cyl("glow_engine", (x, -22.95, 0.2), 1.5, 1.5, 0.1, seg=24, rot=(90, 0, 0), obj="Engines")
    for side in (-1, 1):
        x = side * 10.2
        m.lathe("hull", (0, 0, 0), [(0, 0), (0.7, 0.8), (1.25, 3.0), (1.3, 11.5), (1.1, 12.6)], seg=20,
                obj="Engines", base=T(x, -7.6, -1.1) @ R(90, 0, 0))
        m.cyl("glow_engine", (x, -20.3, -1.1), 0.95, 0.95, 0.1, seg=20, rot=(90, 0, 0), obj="Engines")
        m.box("hull_dark", (x, -13.5, -0.45), (0.3, 7.0, 0.9), obj="Engines")

    # --- Superestrutura (Superstructure): espinha dorsal baixa e alongada, com hangares laterais.
    spine = T(0, -9.5, 2.9) @ R(-90, 0, 0) @ S(0.9, 0.5, 1.0)
    m.lathe("hull", (0, 0, 0), [(0, -11), (1.6, -10), (2.6, -6), (2.7, 2), (2.2, 7), (1.2, 10), (0, 11)], seg=24,
            obj="Superstructure", base=spine)
    for side in (-1, 1):
        m.box("hull_dark", (side * 2.5, -12, 2.6), (0.4, 6.0, 0.9), obj="Superstructure")
        for k in range(12):
            m.box("glow_window", (side * 2.2, -16 + k * 1.1, 3.35), (0.06, 0.5, 0.14), obj="Superstructure")
    m.box("panel", (0, -9.5, 4.55), (1.2, 12, 0.25), bevel=0.05, obj="Superstructure")

    # --- Ponte de comando (Bridge): cabine afilada à frente da espinha, faixa de vidro e mastro de sensores.
    m.box("hull", (0, 1.5, 3.0), (3.4, 5.2, 1.3), taper=(0.75, 0.55), bevel=0.12, obj="Bridge")
    m.box("glow_window", (0, 4.05, 3.15), (2.0, 0.06, 0.3), rot=(28, 0, 0), obj="Bridge")
    for side in (-1, 1):
        m.box("glow_window", (side * 1.62, 1.6, 3.15), (0.06, 3.2, 0.22), obj="Bridge")
        m.box("hull_dark", (side * 1.1, -0.6, 4.1), (0.12, 1.6, 1.3), taper=(1.0, 0.3), obj="Bridge")
    m.tube("hull_dark", (0, -0.2, 3.6), (0, -0.6, 6.0), 0.07, 0.04, seg=6, obj="Bridge")

    # --- Reator de salto (Hyperdrive): anel ventral e faixas de energia no bordo de fuga das asas.
    belly = T(0, -8, -2.25)
    m.lathe("hull_dark", (0, 0, 0), [(3.4, -0.5), (3.8, 0), (3.4, 0.5)], seg=32, obj="Hyperdrive", base=belly)
    m.lathe("glow_hyper", (0, 0, 0), [(2.4, -0.25), (3.3, -0.05), (3.3, 0.05), (2.4, 0.25)], seg=32,
            obj="Hyperdrive", base=belly)
    m.blob("glow_hyper", (0, -8, -2.4), (1.6, 1.6, 0.5), sub=2, noise=0, seed=4, smooth=True, obj="Hyperdrive")
    for side in (-1, 1):
        m.tube("glow_hyper", (side * 7.6, -17.7, 0.42), (side * 12.4, -20.8, 0.42), 0.1, seg=6, obj="Hyperdrive")
    return m


STATION_RING_R = 150.0
STATION_TUBE_R = 11.0
STATION_PORT_Z = 58.0  # ponta do porto de acoplagem no eixo (vira +Z no Unity)


def _torus(m, mat, R, r, seg_major=128, seg_minor=20, base=I4, obj=None):
    tmp = bmesh.new()
    vs = []
    for i in range(seg_major):
        a = 2 * math.pi * i / seg_major
        for j in range(seg_minor):
            b = 2 * math.pi * j / seg_minor
            rad = R + r * math.cos(b)
            vs.append(tmp.verts.new((rad * math.cos(a), rad * math.sin(a), r * math.sin(b))))
    for i in range(seg_major):
        for j in range(seg_minor):
            i2, j2 = (i + 1) % seg_major, (j + 1) % seg_minor
            f = tmp.faces.new([vs[i * seg_minor + j], vs[i2 * seg_minor + j], vs[i2 * seg_minor + j2], vs[i * seg_minor + j2]])
            f.smooth = True
    bmesh.ops.transform(tmp, matrix=base, verts=tmp.verts)
    m._emit(mat, obj, tmp, I4)


def station():
    """Estação Horizonte (~320 m): anel habitacional em torno do eixo do hub, raios, painéis solares e radiadores
    na popa e o porto de acoplagem na proa. Construída com o eixo em Z e girada para o eixo ficar em +Y (frente):
    no Unity o porto fica em +Z local, a STATION_PORT_Z metros do centro."""
    m = Model("station", "Sky")
    m.material("hull", (0.8, 0.81, 0.83), 0.45, 0.35)
    m.material("hull_dark", (0.3, 0.32, 0.35), 0.5, 0.5)
    m.material("panel", (0.6, 0.62, 0.65), 0.5, 0.4)
    m.material("solar", (0.06, 0.09, 0.2), 0.25, 0.6)
    m.material("radiator", (0.92, 0.92, 0.9), 0.6, 0.0)
    m.material("gold", (0.85, 0.65, 0.3), 0.35, 1.0)
    m.material("glow_window", (1.0, 0.86, 0.6), 0.4)
    m.material("glow_dock", (0.45, 0.85, 1.0), 0.4)
    B = R(-90, 0, 0)  # eixo Z da construção -> +Y do Blender (frente)
    rng = random.Random(1_000_000)

    # --- Hub central com flanges e porto de acoplagem
    m.lathe("hull", (0, 0, 0), [(0, -66), (10, -66), (16, -60), (18, -54), (18, -14), (21, -12), (21, 12),
                                (18, 14), (18, 36), (14, 44), (9, 50), (7, 54), (7, STATION_PORT_Z - 1.5),
                                (5.5, STATION_PORT_Z), (0, STATION_PORT_Z)], seg=48, base=B)
    for z in (-40, -24, 24, 32):
        m.lathe("hull_dark", (0, 0, z), [(18.2, -1.2), (19.4, -0.8), (19.4, 0.8), (18.2, 1.2)], seg=48, caps=False, base=B)
    m.lathe("glow_dock", (0, 0, STATION_PORT_Z), [(5.6, 0.0), (6.4, 0.25), (6.4, 0.45), (5.6, 0.6)], seg=48,
            caps=False, base=B)
    for i in range(12):
        a = 2 * math.pi * i / 12
        m.blob("glow_dock", (math.cos(a) * 8.8, math.sin(a) * 8.8, 51.5), (0.35, 0.35, 0.35), sub=1, noise=0,
               seed=i, base=B)
    for i in range(36):  # vigias do hub
        a = 2 * math.pi * i / 36
        m.box("glow_window", (math.cos(a) * 21.05, math.sin(a) * 21.05, rng.choice((-6, 0, 6))), (0.2, 1.4, 0.6),
              rot=(0, 0, math.degrees(a)), base=B)

    # --- Anel habitacional
    _torus(m, "hull", STATION_RING_R, STATION_TUBE_R, base=B)
    for z, w in ((0.0, 1.0), (-5.5, 0.6), (5.5, 0.6)):
        rad = STATION_RING_R + math.sqrt(STATION_TUBE_R ** 2 - z ** 2) + 0.08
        m.lathe("glow_window", (0, 0, z), [(rad, -w / 2), (rad, w / 2)], seg=192, caps=False, smooth=False, base=B)
    for i in range(260):  # módulos e painéis na face externa e nas laterais do anel
        a = rng.uniform(0, 2 * math.pi)
        b = rng.choice((-1, 1)) * rng.uniform(0.5, 1.3)
        rad = STATION_RING_R + STATION_TUBE_R * math.cos(b)
        z = STATION_TUBE_R * math.sin(b)
        size = (rng.uniform(0.8, 2.0), rng.uniform(3, 12), rng.uniform(1.5, 4))
        m.box(rng.choice(("panel", "hull_dark", "panel", "gold")), (math.cos(a) * rad, math.sin(a) * rad, z),
              size, rot=(0, 0, math.degrees(a)), base=B)
    for i in range(6):  # domos de observação
        a = 2 * math.pi * (i + 0.5) / 6
        rad = STATION_RING_R + STATION_TUBE_R - 1
        m.blob("hull_dark", (math.cos(a) * rad, math.sin(a) * rad, 0), (5, 5, 5), sub=3, noise=0, seed=i,
               smooth=True, base=B)

    # --- Raios: seis principais, cabos de tensão e elevadores
    for i in range(6):
        a = 2 * math.pi * i / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        m.tube("hull", d * 19, d * (STATION_RING_R - STATION_TUBE_R + 1), 3.2, seg=16, base=B)
        for s in (-1, 1):
            m.tube("hull_dark", d * 19 + Vector((0, 0, 9 * s)), d * (STATION_RING_R - 8) + Vector((0, 0, 3 * s)),
                   0.5, seg=8, base=B)
        for t in (0.35, 0.65):
            p = d * (19 + t * (STATION_RING_R - 30))
            m.cyl("panel", None, 4.2, 4.2, 6, seg=16, M=T(*p) @ aim((0, 0, 0), d), base=B)

    # --- Popa: treliças com painéis solares e radiadores
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        m.tube("hull_dark", d * 16 + Vector((0, 0, -58)), d * 120 + Vector((0, 0, -58)), 1.2, seg=8, base=B)
        side = Vector((-d.y, d.x, 0))
        for k in range(4):
            c = d * (40 + k * 21) + Vector((0, 0, -58))
            for s in (-1, 1):
                center = c + side * s * 9
                m.box("solar", tuple(center), (18, 14, 0.25), rot=(0, 0, math.degrees(a)), base=B)
                m.box("gold", tuple(center - Vector((0, 0, 0.2))), (18.4, 14.4, 0.1), rot=(0, 0, math.degrees(a)),
                      base=B)
    for i in range(4):
        a = i * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        for k in range(3):
            m.box("radiator", tuple(d * (30 + k * 9) + Vector((0, 0, -30))), (8, 0.3, 22),
                  rot=(0, 0, math.degrees(a) + 90), base=B)
    return m


def shipyard():
    """Estaleiro (52 x 34 m): plataforma baixa, berços de apoio, torres de montagem e console."""
    m = Model("shipyard", "Machines")
    m.material("concrete", (0.5, 0.5, 0.5), 0.9)
    m.material("stripe", (0.95, 0.75, 0.12), 0.6)
    m.material("steel", (0.55, 0.57, 0.6), 0.45, 0.6)
    m.material("dark", (0.18, 0.19, 0.21), 0.6, 0.3)
    m.material("glow_lamp", (1.0, 0.85, 0.55), 0.4)
    m.material("glow_console", (0.45, 0.95, 0.75), 0.4)
    m.greeble = False
    m.material("glass_booth", (0.5, 0.75, 0.9), 0.1)
    m.material("hazard_y", (0.95, 0.72, 0.08), 0.6)
    m.material("hazard_k", (0.06, 0.06, 0.07), 0.7)
    # Laje em placas com juntas de dilatação e faixas de borda.
    m.box("concrete", (0, 0, 0.12), (34, 52, 0.25))
    for i in range(1, 6):
        m.box("dark", (-17 + i * 34 / 6, 0, 0.252), (0.06, 52, 0.01))
    for j in range(1, 9):
        m.box("dark", (0, -26 + j * 52 / 9, 0.252), (34, 0.06, 0.01))
    for side in (-1, 1):
        m.box("stripe", (side * 16.6, 0, 0.26), (0.5, 50, 0.04))
        m.box("stripe", (0, side * 25.6, 0.26), (32, 0.5, 0.04))
    # Marcação de pouso: anel tracejado e eixo central.
    for i in range(36):
        a = 2 * math.pi * i / 36
        m.box("stripe", (math.cos(a) * 9, math.sin(a) * 9 - 2, 0.255), (0.35, 1.2, 0.02), rot=(0, 0, math.degrees(a)))
    for k in range(10):
        m.box("stripe", (0, -22 + k * 4.6, 0.255), (0.3, 2.2, 0.02))
    # Berços hidráulicos.
    for (x, y) in [(0, 10), (-6, -12), (6, -12), (0, -2)]:
        m.cyl("dark", (x, y, 0.4), 1.4, 1.4, 0.3, seg=16)
        bolt_ring(m, "steel", (x, y, 0.56), 1.2, 12, size=0.06)
        m.cyl("dark", (x, y, 1.9), 0.8, 1.0, 3.0, seg=16)
        m.cyl("steel", (x, y, 2.8), 0.55, 0.55, 1.4, seg=16)
        m.box("steel", (x, y, 3.75), (2.4, 2.4, 0.3), bevel=0.05)
        m.box("dark", (x, y, 3.93), (2.0, 2.0, 0.06))
        hazard_strip(m, (x, y - 1.22, 3.75), 2.3, "x", height=0.2)
    # Torres de montagem: treliça com diagonais, plataformas, escada, holofotes voltados para dentro.
    for sx in (-1, 1):
        for sy in (-1, 1):
            bx, by = sx * 15.5, sy * 20
            corners = [(bx + dx, by + dy) for dx in (-0.9, 0.9) for dy in (-0.9, 0.9)]
            for (cx, cy) in corners:
                m.tube("steel", (cx, cy, 0.25), (cx, cy, 15.5), 0.14, 0.14, seg=8)
            for z in range(2, 15, 3):
                m.box("steel", (bx, by, z), (2.0, 2.0, 0.12))
                for (ax, ay), (bx2, by2) in (((-0.9, -0.9), (0.9, -0.9)), ((0.9, -0.9), (0.9, 0.9)),
                                             ((0.9, 0.9), (-0.9, 0.9)), ((-0.9, 0.9), (-0.9, -0.9))):
                    m.tube("steel", (bx + ax, by + ay, z), (bx + bx2, by + by2, z + 3), 0.05, seg=5)
            for k in range(45):
                m.box("steel", (bx - sx * 1.05, by, 0.5 + k * 0.33), (0.04, 0.5, 0.04))
            m.box("stripe", (bx - sx * 5, by, 15.6), (11, 1.2, 0.8), bevel=0.05)
            m.blob("glow_lamp", (bx, by, 16.2), (0.35, 0.35, 0.35), sub=1, noise=0, seed=1)
            for lz in (6.0, 12.0):
                m.box("dark", (bx - sx * 1.2, by, lz), (0.5, 0.7, 0.45), bevel=0.04)
                m.box("glow_lamp", (bx - sx * 1.46, by, lz), (0.02, 0.55, 0.32))
    # Ponte rolante entre as torres de cada ponta, com carro e gancho.
    for sy in (-1, 1):
        y = sy * 20
        for dy in (-0.6, 0.6):
            m.box("stripe", (0, y + dy, 16.4), (31, 0.4, 0.9), bevel=0.04)
        for k in range(15):
            m.tube("stripe", (-14 + k * 2, y - 0.6, 16.0), (-13 + k * 2, y + 0.6, 16.8), 0.05, seg=5)
        m.box("dark", (sy * 4, y, 16.0), (2.2, 2.0, 0.9), bevel=0.05)
        m.tube("steel", (sy * 4, y, 15.5), (sy * 4, y, 11.5), 0.03, seg=5)
        m.box("dark", (sy * 4, y, 11.3), (0.5, 0.3, 0.5), bevel=0.03)
    # Cabine de controle com janelas, tanques de combustível e calhas de cabo.
    m.box("dark", (17.8, -22, 1.6), (2.6, 3.4, 2.8), bevel=0.06)
    m.box("glass_booth", (16.48, -22, 2.2), (0.05, 2.8, 1.0))
    m.box("glow_console", (16.6, -22, 1.4), (0.05, 1.6, 0.4))
    m.box("steel", (17.8, -22, 3.05), (2.8, 3.6, 0.12))
    m.tube("steel", (17.8, -21, 3.1), (17.8, -21, 5.0), 0.04, seg=6)
    for k, y in enumerate((-14, -11)):
        m.lathe("steel", (18.4, y, 0.25), [(0, 0), (1.1, 0.05), (1.2, 0.3), (1.2, 3.6), (0.9, 4.1), (0, 4.2)], seg=20)
        for z in (1.2, 2.8):
            m.lathe("dark", (18.4, y, 0.25 + z), [(1.22, -0.06), (1.22, 0.06)], seg=20, caps=False)
    pipe_run(m, "steel", "dark", (17.2, -14, 1.0), (17.2, -20.5, 1.0), 0.12, clamps=3)
    for side in (-1, 1):
        m.box("dark", (side * 16.0, 0, 0.32), (0.4, 40, 0.14))
    for k in range(12):  # balizas de borda
        for side in (-1, 1):
            y = -24 + k * 4.4
            m.cyl("hazard_y", (side * 17.3, y, 0.55), 0.1, 0.1, 0.6, seg=8)
            m.blob("glow_lamp", (side * 17.3, y, 0.9), (0.08, 0.08, 0.08), sub=1, noise=0, seed=k)
    return m


# =============================================================================
# Máquinas de terraformação por nível (aquecedor 1-4, perfuratriz 1-3, filtro 1-3)
# Convenções de animação (lidas por MachineFx no Unity): objetos "Spin_<eixo>_nome" giram, "Bob_Y_nome" sobem e
# descem, "Drill_Y_nome" giram e descem, "Fx_<tipo>" marcam onde nascem partículas. Eixos em coordenadas do Unity
# (Blender Z -> Y, Blender Y -> Z, Blender X -> X). Materiais "glow_*" pulsam com a máquina ligada.
# =============================================================================
def _mats(m):
    m.material("body", (0.30, 0.31, 0.34), 0.55, 0.3)
    m.material("body_light", (0.74, 0.75, 0.73), 0.5, 0.25)
    m.material("dark", (0.12, 0.12, 0.14), 0.6, 0.3)
    m.material("steel", (0.62, 0.64, 0.68), 0.35, 0.85)
    m.material("pipe", (0.62, 0.45, 0.3), 0.4, 0.7)
    m.material("rust_edge", (0.4, 0.22, 0.12), 0.8, 0.5)
    m.material("yellow", (0.92, 0.7, 0.14), 0.55)
    m.material("rubber", (0.06, 0.06, 0.06), 0.9)
    m.material("glow_heat", (1.0, 0.42, 0.1), 0.5)
    m.material("glow_core", (0.5, 0.85, 1.0), 0.4)
    m.material("glow_filter", (0.3, 1.0, 0.65), 0.4)
    m.material("glow_screen", (0.4, 0.95, 0.6), 0.4)
    m.material("gauge_face", (0.9, 0.9, 0.84), 0.5)
    m.material("glass_tube", (0.75, 0.9, 1.0), 0.05, 0.0)


def _plinth(m, w, d, h=0.14, feet=True):
    m.box("dark", (0, 0, h / 2), (w, d, h), bevel=0.03)
    if feet:
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.box("steel", (sx * (w / 2 - 0.12), sy * (d / 2 - 0.12), 0.02), (0.22, 0.22, 0.04), bevel=0.01)
                bolt_ring(m, "steel", (sx * (w / 2 - 0.12), sy * (d / 2 - 0.12), h + 0.01), 0.07, 4, size=0.02)
    hazard_strip(m, (0, d / 2 + 0.008, h * 0.5), w * 0.92, "x", height=min(0.1, h * 0.8))


def _gauge(m, pos, r=0.07):
    x, y, z = pos
    m.cyl("dark", (x, y, z), r * 1.25, r * 1.25, 0.03, rot=(90, 0, 0))
    m.cyl("gauge_face", (x, y + 0.018, z), r, r, 0.012, rot=(90, 0, 0))
    m.box("glow_heat", (x + r * 0.2, y + 0.026, z + r * 0.25), (r * 0.9, 0.006, 0.008), rot=(0, 35, 0))


def _valve(m, pos, r=0.1):
    x, y, z = pos
    m.tube("steel", (x, y - 0.1, z), (x, y, z), 0.02, seg=6)
    base = T(x, y, z) @ R(90, 0, 0)
    m.lathe("rust_edge", (0, 0, 0), [(r, -0.015), (r, 0.015)], seg=12, caps=False, base=base)
    for a in (0, 60, 120):
        m.box("rust_edge", (0, 0, 0), (r * 2, 0.018, 0.018), rot=(0, 0, a), base=base)


def _ctrl_cabinet(m, pos, w=0.5, h=0.45):
    x, y, z = pos
    m.box("body", (x, y, z), (w, 0.16, h), bevel=0.02)
    m.box("dark", (x, y + 0.085, z + h * 0.15), (w * 0.72, 0.012, h * 0.4))
    m.box("glow_screen", (x - w * 0.12, y + 0.093, z + h * 0.15), (w * 0.38, 0.006, h * 0.28))
    for k in range(4):
        m.cyl("steel", (x - w * 0.3 + k * w * 0.2, y + 0.09, z - h * 0.3), 0.018, 0.018, 0.025, rot=(90, 0, 0), seg=8)
    m.box("yellow", (x + w * 0.3, y + 0.09, z - h * 0.3), (0.08, 0.02, 0.05))


def _ladder(m, x, y, z0, z1, side="x", width=0.42):
    h = z1 - z0
    for s in (-1, 1):
        dx, dy = (0, s * width / 2) if side == "x" else (s * width / 2, 0)
        m.box("steel", (x + dx, y + dy, z0 + h / 2), (0.035, 0.035, h))
    for k in range(int(h / 0.28)):
        zz = z0 + 0.2 + k * 0.28
        m.box("steel", (x, y, zz), (0.03, width, 0.03) if side == "x" else (width, 0.03, 0.03))


def _railing(m, a, b, z, h=0.55, posts=4, mat="yellow"):
    a, b = Vector(a), Vector(b)
    for k in range(posts + 1):
        p = a + (b - a) * (k / posts)
        m.tube(mat, (p.x, p.y, z), (p.x, p.y, z + h), 0.02, seg=6)
    m.tube(mat, (a.x, a.y, z + h), (b.x, b.y, z + h), 0.02, seg=6)
    m.tube(mat, (a.x, a.y, z + h * 0.5), (b.x, b.y, z + h * 0.5), 0.015, seg=6)


def _fan(m, name, center, r, blades=7, mat="steel", axis="z", shroud=True, hub=0.1):
    """Ventoinha: pás numa peça própria (gira no centro), dentro de um aro fixo."""
    c = Vector(center)
    rax = {"z": I4, "y": R(-90, 0, 0), "x": R(0, 90, 0)}[axis]
    base = T(*c) @ rax
    m.origins[name] = tuple(c)
    for i in range(blades):
        a = 360 * i / blades
        px, py = r * 0.52 * math.cos(math.radians(a)), r * 0.52 * math.sin(math.radians(a))
        m.box(mat, (px, py, 0), (r * 0.82, r * 0.28, 0.012), rot=(0, 0, a + 14), base=base, obj=name)
        m.box(mat, (px, py, 0), (r * 0.82, r * 0.2, 0.012), rot=(22, 0, a + 14), base=base, obj=name)
    m.cyl("dark", (0, 0, 0), hub, hub, 0.1, seg=10, base=base, obj=name)
    if shroud:
        m.lathe("dark", (0, 0, 0), [(r * 1.08, -0.07), (r * 1.08, 0.07)], seg=24, caps=False, base=base)
        m.lathe("steel", (0, 0, 0), [(r * 1.12, -0.08), (r * 1.12, -0.05)], seg=24, caps=False, base=base)
        for a in (0, 90):
            m.box("dark", (0, 0, 0.06), (r * 2.1, 0.03, 0.02), rot=(0, 0, a), base=base)


def _viewport(m, pos, r=0.1, glow="glow_heat"):
    x, y, z = pos
    m.cyl("steel", (x, y, z), r * 1.3, r * 1.3, 0.04, rot=(90, 0, 0), seg=12)
    m.cyl(glow, (x, y + 0.022, z), r, r, 0.012, rot=(90, 0, 0), seg=12)
    for i in range(8):
        a = 2 * math.pi * i / 8
        m.cyl("steel", (x + math.cos(a) * r * 1.15, y + 0.03, z + math.sin(a) * r * 1.15), 0.016, 0.016, 0.013, seg=6, rot=(90, 0, 0))


def _fx(m, name, pos):
    m.origins[name] = tuple(pos)
    m.box("dark", pos, (0.02, 0.02, 0.02), obj=name)


def _stack(m, x, y, z0, r, h, flare=False):
    """Chaminé com flange, cintas e boca (glow_heat se `flare`)."""
    m.cyl("pipe", (x, y, z0 + h / 2), r, r * 0.9, h, seg=14)
    m.cyl("dark", (x, y, z0 + 0.04), r * 1.5, r * 1.5, 0.08, seg=14)
    bolt_ring(m, "steel", (x, y, z0 + 0.09), r * 1.3, 8, size=0.018)
    for t in (0.35, 0.65):
        m.cyl("rust_edge", (x, y, z0 + h * t), r * 1.06, r * 1.06, 0.05, seg=14)
    m.cyl("dark", (x, y, z0 + h + 0.03), r * 1.3, r * 1.3, 0.07, seg=14)
    if flare:
        m.cyl("glow_heat", (x, y, z0 + h + 0.075), r * 0.85, r * 0.85, 0.02, seg=14)


def _lattice(m, half0, half1, z0, z1, levels=3, mat="yellow", r=0.06):
    """Torre de treliça de quatro pernas com travessas e diagonais em X."""
    legs = [Vector((sx * half0, sy * half0, z0)) for sx in (-1, 1) for sy in (-1, 1)]
    tops = [Vector((sx * half1, sy * half1, z1)) for sx in (-1, 1) for sy in (-1, 1)]
    for a, b in zip(legs, tops):
        m.tube(mat, a, b, r, r * 0.85, seg=8)

    def at(i, t):
        return legs[i] + (tops[i] - legs[i]) * t
    faces = [(0, 1), (1, 3), (3, 2), (2, 0)]
    for k in range(levels):
        t0, t1 = k / levels, (k + 1) / levels
        for i, j in faces:
            m.tube(mat, at(i, t1), at(j, t1), r * 0.65, seg=6)
            m.tube(mat, at(i, t0), at(j, t1), r * 0.4, seg=5)
            m.tube(mat, at(j, t0), at(i, t1), r * 0.4, seg=5)
    return at


# ------------------------------------------------------------------ aquecedor
def heater_tier(tier):
    """Aquecedor, 4 níveis: forno elétrico compacto -> torre térmica -> forno industrial duplo -> núcleo de plasma."""
    m = Model(f"heater_{tier}", "Machines")
    m.greeble = False
    _mats(m)
    if tier == 1:
        _plinth(m, 1.7, 1.7, 0.2)
        m.box("body", (0, 0, 0.68), (1.5, 1.5, 0.9), bevel=0.06)
        m.box("dark", (0, 0, 1.15), (1.56, 1.56, 0.06))
        for side in (-1, 1):
            for i in range(9):
                m.box("dark", (-0.64 + i * 0.16, side * 0.79, 0.68), (0.04, 0.09, 0.72))
                m.box("dark", (side * 0.79, -0.64 + i * 0.16, 0.68), (0.09, 0.04, 0.72))
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.box("steel", (sx * 0.74, sy * 0.74, 0.68), (0.08, 0.08, 0.94), bevel=0.01)
        m.cyl("dark", (0, 0, 1.24), 0.64, 0.64, 0.12, seg=24)
        m.cyl("glow_heat", (0, 0, 1.29), 0.54, 0.54, 0.04, seg=24)
        for i in range(9):
            m.box("steel", (-0.48 + i * 0.12, 0, 1.34),
                  (0.025, 1.08 * math.sqrt(max(0.05, 1 - ((-0.48 + i * 0.12) / 0.54) ** 2)), 0.03))
        bolt_ring(m, "steel", (0, 0, 1.31), 0.6, 16, size=0.025)
        _stack(m, 0.55, -0.45, 1.24, 0.13, 1.0, flare=True)
        _stack(m, -0.45, -0.52, 1.24, 0.09, 0.6)
        _fx(m, "Fx_Smoke", (0.55, -0.45, 2.3))
        _ctrl_cabinet(m, (0, 0.84, 0.82), 0.55, 0.42)
        _gauge(m, (-0.5, 0.92, 0.55))
        _valve(m, (0.5, 0.9, 0.5), 0.09)
        _fan(m, "Spin_Y_Fan", (-0.35, 0.3, 1.31), 0.22, blades=6, axis="z", shroud=False)
        cable(m, "dark", (0.2, 0.86, 0.6), (0.7, 1.7, 0.03), sag=0.1, r=0.025)
        indicator(m, (-0.6, 0.8, 1.22))
    elif tier == 2:
        _plinth(m, 2.4, 2.0, 0.22)
        m.cyl("body", (0, 0, 1.15), 0.62, 0.62, 1.8, seg=22)
        for z in (0.5, 1.15, 1.8):
            m.cyl("steel", (0, 0, z), 0.66, 0.66, 0.06, seg=22)
            bolt_ring(m, "steel", (0, 0, z + 0.04), 0.62, 14, size=0.02)
        m.lathe("body", (0, 0, 2.05), [(0.62, 0), (0.55, 0.14), (0.3, 0.28), (0.22, 0.34)], seg=22)
        _stack(m, 0, 0, 2.38, 0.2, 1.3, flare=True)
        _fx(m, "Fx_Smoke", (0, 0, 3.8))
        for side in (-1, 1):
            x = side * 1.02
            m.box("body", (x, 0, 0.78), (0.5, 1.3, 1.16), bevel=0.04)
            for iz in range(5):
                for iy in range(6):
                    m.tube("pipe", (x - 0.26, -0.55 + iy * 0.22, 0.4 + iz * 0.2),
                           (x + 0.26, -0.55 + iy * 0.22, 0.4 + iz * 0.2), 0.035, seg=8)
            m.box("dark", (x, 0, 1.4), (0.56, 1.36, 0.08))
            m.box("glow_heat", (x, 0.66, 0.78), (0.36, 0.01, 0.7))
        m.tube("pipe", (-0.76, 0, 1.4), (-0.3, 0, 1.55), 0.07, seg=10)
        m.tube("pipe", (0.76, 0, 1.4), (0.3, 0, 1.55), 0.07, seg=10)
        m.box("dark", (1.02, 0, 1.5), (0.6, 1.0, 0.04))
        _fan(m, "Spin_Y_Fan", (1.02, 0, 1.6), 0.4, blades=8, axis="z")
        _ctrl_cabinet(m, (0, 1.06, 0.82), 0.7, 0.5)
        _gauge(m, (-0.5, 1.16, 0.5), 0.08)
        _gauge(m, (0.5, 1.16, 0.5), 0.08)
        _valve(m, (0.0, 1.14, 0.4), 0.1)
        _ladder(m, -1.28, 0.5, 0.2, 1.45, side="y")
        _viewport(m, (0, 0.64, 1.1), 0.12)
        _viewport(m, (0, 0.64, 1.55), 0.12)
        cable(m, "dark", (-0.2, 1.1, 0.5), (-0.9, 2.0, 0.03), sag=0.1, r=0.03)
        indicator(m, (0.8, 1.0, 1.5))
    elif tier == 3:
        _plinth(m, 3.4, 2.6, 0.25)
        m.box("body", (0, 0, 0.55), (3.2, 2.4, 0.6), bevel=0.05)
        for side in (-1, 1):
            x = side * 0.85
            m.lathe("body", (x, 0, 0.85), [(0.7, 0), (0.7, 1.6), (0.6, 1.85), (0.3, 2.0), (0.0, 2.05)], seg=22)
            for z in (0.3, 0.9, 1.5):
                m.cyl("steel", (x, 0, 0.85 + z), 0.73, 0.73, 0.06, seg=22)
                bolt_ring(m, "steel", (x, 0, 0.85 + z + 0.04), 0.68, 14, size=0.02)
            for a in (-35, 0, 35):
                ar = math.radians(a)
                m.cyl("steel", (x + 0.7 * math.sin(ar), 0.7 * math.cos(ar), 2.35), 0.15, 0.15, 0.05, rot=(90, 0, -a), seg=12)
                m.cyl("glow_heat", (x + 0.74 * math.sin(ar), 0.74 * math.cos(ar), 2.35), 0.11, 0.11, 0.012, rot=(90, 0, -a), seg=12)
            m.tube("pipe", (x, 0, 2.85), (x * 0.2, 0, 3.4), 0.12, seg=12)
        m.box("pipe", (0, 0, 3.4), (0.7, 0.5, 0.35), bevel=0.05)
        _stack(m, 0, 0, 3.55, 0.28, 1.5, flare=True)
        _fx(m, "Fx_Smoke", (0, 0, 5.2))
        for sx in (-1, 1):
            m.box("steel", (sx * 1.65, 0, 1.0), (0.14, 2.2, 0.06))
        m.box("steel", (0, -1.28, 1.0), (3.1, 0.5, 0.06))
        _railing(m, (-1.55, -1.5, 0), (1.55, -1.5, 0), 1.03, posts=7)
        _ladder(m, 1.72, -1.1, 0.25, 1.05, side="y", width=0.45)
        for k, y in enumerate((-0.6, 0.6)):
            m.box("dark", (1.72, y, 0.58), (0.08, 0.95, 0.9))
            _fan(m, f"Spin_X_Fan{k}", (1.78, y, 0.58), 0.4, blades=7, axis="x")
        for i in range(12):
            m.box("dark", (-1.4 + i * 0.255, -1.22, 0.55), (0.06, 0.1, 0.55))
        _ctrl_cabinet(m, (0, 1.28, 0.62), 0.9, 0.5)
        for k in range(3):
            _gauge(m, (-1.0 + k * 0.28, 1.36, 0.82), 0.075)
        _valve(m, (1.0, 1.34, 0.55), 0.11)
        pipe_run(m, "pipe", "rust_edge", (-1.1, 1.25, 0.32), (1.1, 1.25, 0.32), 0.06, clamps=4)
        cable(m, "dark", (-0.9, 1.3, 0.5), (-1.7, 2.4, 0.03), sag=0.12, r=0.04)
        indicator(m, (1.5, 1.1, 0.95))
    else:
        _plinth(m, 4.2, 4.2, 0.28)
        m.cyl("body", (0, 0, 0.45), 2.0, 2.0, 0.34, seg=28)
        m.cyl("dark", (0, 0, 0.64), 1.7, 1.7, 0.06, seg=28)
        m.lathe("steel", (0, 0, 0.67), [(0.95, 0), (0.95, 0.25), (0.7, 0.4)], seg=26)
        m.cyl("glass_tube", (0, 0, 2.15), 0.62, 0.62, 2.7, seg=24)
        m.cyl("glow_core", (0, 0, 2.15), 0.28, 0.28, 2.3, seg=18)
        m.blob("glow_heat", (0, 0, 2.15), (0.4, 0.4, 0.5), sub=2, noise=0.05, seed=2, smooth=True)
        m.lathe("steel", (0, 0, 3.5), [(0.7, 0), (0.7, 0.1), (0.45, 0.3), (0.2, 0.4)], seg=26)
        for k, (z, rr) in enumerate(((1.55, 0.82), (2.15, 0.9), (2.75, 0.82))):
            name = f"Spin_Y_Ring{k}"
            m.origins[name] = (0, 0, z)
            m.lathe("glow_heat", (0, 0, z), [(rr, -0.03), (rr, 0.03)], seg=30, caps=False, obj=name)
            m.lathe("steel", (0, 0, z), [(rr + 0.02, -0.05), (rr + 0.02, -0.03)], seg=30, caps=False, obj=name)
            for a in (0, 120, 240):
                r = math.radians(a + k * 40)
                m.box("steel", (rr * math.cos(r), rr * math.sin(r), z), (0.12, 0.12, 0.14), obj=name)
        for sx in (-1, 1):
            for sy in (-1, 1):
                x, y = sx * 1.55, sy * 1.55
                m.box("body", (x, y, 1.15), (0.5, 0.5, 2.0), bevel=0.04)
                for i in range(10):
                    m.box("dark", (x + sx * 0.28, y, 0.4 + i * 0.19), (0.05, 0.42, 0.1))
                    m.box("dark", (x, y + sy * 0.28, 0.4 + i * 0.19), (0.42, 0.05, 0.1))
                m.box("glow_heat", (x, y, 2.18), (0.36, 0.36, 0.04))
                m.tube("pipe", (x * 0.82, y * 0.82, 1.55), (x * 0.4, y * 0.4, 2.5), 0.07, seg=10)
                m.cyl("steel", (x, y, 2.22), 0.1, 0.1, 0.22, seg=8)
        _stack(m, 0, 0, 3.9, 0.3, 0.6, flare=True)
        _fx(m, "Fx_Smoke", (0, 0, 4.7))
        _fx(m, "Fx_Spark", (0, 0, 3.6))
        _ctrl_cabinet(m, (0, 2.22, 0.75), 1.1, 0.6)
        for k in range(4):
            _gauge(m, (-0.9 + k * 0.5, 2.32, 1.08), 0.09)
        _ladder(m, 2.2, 0.0, 0.3, 1.2, side="y", width=0.5)
        cable(m, "dark", (0.3, 2.2, 0.5), (0.8, 3.2, 0.03), sag=0.1, r=0.05)
        indicator(m, (0.9, 2.18, 1.2))
    return m


# ------------------------------------------------------------------ perfuratriz
def _bit(m, name, z_tip, r):
    """Broca tricônica: corpo cônico e três cones de dentes."""
    m.cyl("steel", (0, 0, z_tip + r * 0.9), r * 0.95, r * 0.35, r * 1.8, seg=12, obj=name)
    for k in range(3):
        a = math.radians(k * 120 + 20)
        c = (math.cos(a) * r * 0.55, math.sin(a) * r * 0.55, z_tip + r * 0.5)
        m.cyl("dark", c, r * 0.5, r * 0.12, r * 0.9, seg=8, obj=name)
        for t in range(4):
            aa = math.radians(t * 90)
            m.box("steel", (c[0] + math.cos(aa) * r * 0.3, c[1] + math.sin(aa) * r * 0.3, c[2] - 0.02),
                  (0.03, 0.03, 0.05), obj=name)


def drill_tier(tier):
    """Perfuratriz, 3 níveis: sonda compacta -> torre de perfuração com pistões -> plataforma pesada de pressão."""
    m = Model(f"drill_{tier}", "Machines")
    m.greeble = False
    _mats(m)
    if tier == 1:
        _plinth(m, 2.4, 2.4, 0.24)
        m.box("steel", (0, 0, 0.27), (0.8, 0.8, 0.04))
        at = _lattice(m, 0.85, 0.28, 0.24, 3.15, levels=3)
        m.box("yellow", (0, 0, 3.4), (0.95, 0.95, 0.6), bevel=0.06)
        for i in range(6):
            m.box("dark", (0.49, -0.3 + i * 0.12, 3.4), (0.03, 0.05, 0.45))
            m.box("dark", (-0.49, -0.3 + i * 0.12, 3.4), (0.03, 0.05, 0.45))
        m.cyl("dark", (0, 0, 3.8), 0.32, 0.32, 0.22, seg=16)
        bolt_ring(m, "steel", (0, 0, 3.92), 0.26, 10, size=0.02)
        m.origins["Drill_Y_Stem"] = (0, 0, 1.6)
        m.cyl("steel", (0, 0, 1.75), 0.12, 0.12, 3.2, seg=14, obj="Drill_Y_Stem")
        for z in (0.9, 1.7, 2.5):
            m.cyl("dark", (0, 0, z), 0.17, 0.17, 0.08, seg=14, obj="Drill_Y_Stem")
        for i in range(8):
            a = i * 0.7
            m.box("steel", (math.cos(a) * 0.13, math.sin(a) * 0.13, 0.4 + i * 0.12), (0.12, 0.04, 0.06), rot=(0, 0, math.degrees(a)), obj="Drill_Y_Stem")
        _bit(m, "Drill_Y_Stem", 0.12, 0.22)
        _fx(m, "Fx_Dust", (0, 0, 0.15))
        m.box("dark", (0.9, -0.9, 0.5), (0.5, 0.5, 0.5), bevel=0.03)
        m.cyl("steel", (0.9, -0.9, 0.8), 0.12, 0.12, 0.1, seg=10)
        cable(m, "rubber", (0.9, -0.9, 0.85), (0.25, -0.25, 3.25), sag=0.35, r=0.04, segments=8)
        _ctrl_cabinet(m, (-0.8, 1.2, 0.8), 0.6, 0.55)
        _gauge(m, (-0.5, 1.29, 0.5), 0.07)
        _railing(m, (-1.1, 1.15, 0.24), (-0.3, 1.15, 0.24), 0.24, posts=3)
        indicator(m, (0.1, 1.2, 0.3))
    elif tier == 2:
        _plinth(m, 3.2, 3.2, 0.28)
        m.box("steel", (0, 0, 0.32), (1.2, 1.2, 0.05))
        at = _lattice(m, 1.15, 0.4, 0.28, 5.0, levels=5)
        # Coroa com duas roldanas giratórias
        m.box("yellow", (0, 0, 5.15), (1.2, 0.9, 0.3), bevel=0.04)
        for k, x in enumerate((-0.28, 0.28)):
            nm = f"Spin_X_Sheave{k}"
            m.origins[nm] = (x, 0, 5.4)
            m.lathe("steel", (0, 0, 0), [(0.3, -0.05), (0.3, 0.05)], seg=20, base=T(x, 0, 5.4) @ R(0, 90, 0), obj=nm)
            m.lathe("dark", (0, 0, 0), [(0.26, -0.07), (0.26, 0.07)], seg=20, caps=False, base=T(x, 0, 5.4) @ R(0, 90, 0), obj=nm)
            for a in (0, 60, 120):
                m.box("steel", (0, 0, 0), (0.5, 0.02, 0.06), rot=(0, a, 0), base=T(x, 0, 5.4) @ R(0, 90, 0), obj=nm)
        m.box("steel", (0, 0, 5.62), (0.9, 0.5, 0.06))
        # Cabeçote giratório e haste com roscas
        m.box("dark", (0, 0, 3.5), (0.9, 0.9, 0.55), bevel=0.05)
        m.cyl("yellow", (0, 0, 3.95), 0.36, 0.36, 0.3, seg=16)
        bolt_ring(m, "steel", (0, 0, 4.12), 0.3, 12, size=0.02)
        m.origins["Drill_Y_Stem"] = (0, 0, 1.8)
        m.cyl("steel", (0, 0, 1.85), 0.15, 0.15, 3.4, seg=14, obj="Drill_Y_Stem")
        for z in (0.9, 1.5, 2.1, 2.7):
            m.cyl("dark", (0, 0, z), 0.2, 0.2, 0.1, seg=14, obj="Drill_Y_Stem")
            bolt_ring(m, "steel", (0, 0, z + 0.06), 0.17, 8, size=0.015, obj="Drill_Y_Stem")
        for i in range(10):
            a = i * 0.65
            m.box("steel", (math.cos(a) * 0.17, math.sin(a) * 0.17, 0.5 + i * 0.13), (0.14, 0.05, 0.07), rot=(0, 0, math.degrees(a)), obj="Drill_Y_Stem")
        _bit(m, "Drill_Y_Stem", 0.1, 0.28)
        _fx(m, "Fx_Dust", (0, 0, 0.15))
        # Pistões hidráulicos dos dois lados
        for k, sx in enumerate((-1, 1)):
            x = sx * 0.62
            m.cyl("dark", (x, 0.0, 3.0), 0.1, 0.1, 1.4, seg=12)
            m.cyl("steel", (x, 0.0, 3.75), 0.12, 0.12, 0.06, seg=12)
            nm = f"Bob_Y_Piston{k}"
            m.origins[nm] = (x, 0, 3.4)
            m.cyl("steel", (x, 0.0, 3.4), 0.05, 0.05, 1.2, seg=10, obj=nm)
            m.cyl("rust_edge", (x, 0.0, 3.95), 0.08, 0.08, 0.08, seg=10, obj=nm)
        # Casa de máquinas: gerador com chaminé, tanque de lama e cabine de comando
        m.box("body", (1.9, -0.9, 0.75), (1.2, 1.5, 1.0), bevel=0.05)
        for i in range(5):
            m.box("dark", (2.52, -0.9 + (i - 2) * 0.22, 0.75), (0.04, 0.12, 0.7))
        _stack(m, 1.5, -1.3, 1.25, 0.1, 0.9)
        _fx(m, "Fx_Smoke", (1.5, -1.3, 2.4))
        m.cyl("body_light", (-1.95, -0.7, 0.55), 0.55, 0.55, 0.9, seg=20)
        m.lathe("body_light", (-1.95, -0.7, 1.0), [(0.55, 0), (0.45, 0.14), (0.15, 0.2)], seg=20)
        m.cyl("steel", (-1.95, -0.7, 0.8), 0.58, 0.58, 0.06, seg=20)
        _ladder(m, -1.95, -1.3, 0.2, 1.05, side="y", width=0.4)
        m.box("body", (-1.7, 1.3, 0.8), (1.3, 0.9, 1.1), bevel=0.05)
        m.box("dark", (-1.7, 1.77, 1.0), (0.9, 0.02, 0.4))
        m.box("glow_screen", (-1.7, 1.78, 1.0), (0.84, 0.005, 0.34))
        m.box("steel", (-1.7, 1.3, 1.4), (1.4, 1.0, 0.06))
        _ctrl_cabinet(m, (0.4, 1.7, 0.7), 0.7, 0.5)
        _gauge(m, (0.7, 1.79, 0.95), 0.07)
        pipe_run(m, "pipe", "rust_edge", (-1.4, -0.4, 0.4), (-0.3, -0.4, 0.4), 0.07, clamps=3)
        pipe_run(m, "pipe", "rust_edge", (1.3, -0.4, 0.4), (0.3, -0.4, 0.4), 0.07, clamps=3)
        _railing(m, (-1.5, 1.6, 0.28), (1.5, 1.6, 0.28), 0.28, posts=6)
        cable(m, "rubber", (1.9, -0.2, 1.2), (0.5, -0.2, 3.3), sag=0.4, r=0.05, segments=8)
        indicator(m, (0.3, 1.4, 0.3))
    else:
        _plinth(m, 4.4, 4.4, 0.3, feet=False)
        m.cyl("body", (0, 0, 0.4), 2.2, 2.2, 0.22, seg=28)
        # Quatro estabilizadores hidráulicos com sapatas
        for sx in (-1, 1):
            for sy in (-1, 1):
                x, y = sx * 1.9, sy * 1.9
                m.tube("steel", (x * 0.7, y * 0.7, 1.0), (x, y, 0.3), 0.12, seg=10)
                m.cyl("dark", (x, y, 0.2), 0.34, 0.34, 0.12, seg=14)
                m.cyl("steel", (x, y, 0.3), 0.14, 0.14, 0.3, seg=10)
                bolt_ring(m, "steel", (x, y, 0.27), 0.28, 8, size=0.02)
        at = _lattice(m, 1.2, 0.45, 0.3, 6.4, levels=6, mat="steel", r=0.08)
        # Condutos de energia azuis ao longo das quatro pernas
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.tube("glow_core", (sx * 1.1, sy * 1.1, 0.5), (sx * 0.5, sy * 0.5, 6.1), 0.025, seg=6)
        # Cabeça giratória brilhante
        m.origins["Spin_Y_Head"] = (0, 0, 3.4)
        m.lathe("glow_core", (0, 0, 3.4), [(0.75, -0.04), (0.75, 0.04)], seg=28, caps=False, obj="Spin_Y_Head")
        m.lathe("steel", (0, 0, 3.4), [(0.78, -0.08), (0.78, -0.05)], seg=28, caps=False, obj="Spin_Y_Head")
        for a in (0, 90, 180, 270):
            r = math.radians(a)
            m.box("steel", (0.75 * math.cos(r), 0.75 * math.sin(r), 3.4), (0.14, 0.14, 0.18), obj="Spin_Y_Head")
        m.box("dark", (0, 0, 3.75), (1.0, 1.0, 0.5), bevel=0.05)
        m.cyl("yellow", (0, 0, 4.15), 0.42, 0.42, 0.3, seg=16)
        # Coroa e roldanas
        m.box("yellow", (0, 0, 6.55), (1.3, 1.0, 0.3), bevel=0.05)
        for k, x in enumerate((-0.32, 0.32)):
            nm = f"Spin_X_Sheave{k}"
            m.origins[nm] = (x, 0, 6.85)
            B = T(x, 0, 6.85) @ R(0, 90, 0)
            m.lathe("steel", (0, 0, 0), [(0.34, -0.05), (0.34, 0.05)], seg=20, base=B, obj=nm)
            for a in (0, 60, 120):
                m.box("glow_core", (0, 0, 0), (0.55, 0.02, 0.06), rot=(0, a, 0), base=B, obj=nm)
        # Haste grossa com aletas helicoidais e broca
        m.origins["Drill_Y_Stem"] = (0, 0, 2.2)
        m.cyl("steel", (0, 0, 2.3), 0.22, 0.22, 4.2, seg=16, obj="Drill_Y_Stem")
        for i in range(18):
            a = i * 0.7
            m.box("dark", (math.cos(a) * 0.28, math.sin(a) * 0.28, 0.4 + i * 0.2), (0.2, 0.06, 0.1), rot=(0, 0, math.degrees(a)), obj="Drill_Y_Stem")
        for z in (1.2, 2.2, 3.2):
            m.cyl("rust_edge", (0, 0, z), 0.27, 0.27, 0.1, seg=16, obj="Drill_Y_Stem")
        _bit(m, "Drill_Y_Stem", 0.1, 0.4)
        _fx(m, "Fx_Dust", (0, 0, 0.15))
        # Dois pistões grandes e tanques de pressão com visores
        for k, sx in enumerate((-1, 1)):
            x = sx * 0.9
            m.cyl("dark", (x, 0, 4.4), 0.14, 0.14, 1.8, seg=12)
            nm = f"Bob_Y_Piston{k}"
            m.origins[nm] = (x, 0, 4.9)
            m.cyl("steel", (x, 0, 4.9), 0.07, 0.07, 1.4, seg=10, obj=nm)
            m.cyl("glow_core", (x, 0, 5.62), 0.1, 0.1, 0.05, seg=10, obj=nm)
        for k, (x, y) in enumerate(((1.7, -0.6), (1.7, 0.6))):
            m.lathe("body_light", (x, y, 0.3), [(0.42, 0), (0.42, 1.6), (0.3, 1.85), (0.0, 1.9)], seg=20)
            m.lathe("glow_core", (x, y, 0.8), [(0.43, -0.03), (0.43, 0.03)], seg=20, caps=False)
            _gauge(m, (x, y + 0.42, 1.2), 0.07)
            m.tube("steel", (x, y, 2.1), (0.5, y * 0.4, 3.0), 0.07, seg=8)
        _stack(m, -1.7, -1.1, 0.3, 0.12, 2.2)
        _fx(m, "Fx_Smoke", (-1.7, -1.1, 2.7))
        m.box("body", (-1.7, 0.7, 0.9), (1.2, 1.2, 1.2), bevel=0.05)
        m.box("dark", (-1.7, 1.31, 1.1), (0.9, 0.02, 0.5))
        m.box("glow_core", (-1.7, 1.32, 1.1), (0.84, 0.005, 0.44))
        _ctrl_cabinet(m, (0.0, 2.35, 0.9), 1.2, 0.7)
        for k in range(3):
            _gauge(m, (-0.5 + k * 0.5, 2.45, 1.3), 0.08)
        _ladder(m, 2.3, 0.0, 0.3, 2.0, side="y", width=0.5)
        _railing(m, (-1.9, 2.1, 0.3), (1.9, 2.1, 0.3), 0.3, posts=8)
        hazard_strip(m, (0, -2.22, 0.15), 4.0, "x", height=0.14)
        cable(m, "rubber", (-1.2, 0.5, 1.3), (-0.4, 0.2, 3.4), sag=0.5, r=0.06, segments=8)
        indicator(m, (0.2, 2.3, 0.4))
    return m


# ------------------------------------------------------------------ filtro de radiação
def decon_tier(tier):
    """Filtro de radiação, 3 níveis: depurador compacto -> unidade de quatro filtros -> torre ionizadora."""
    m = Model(f"decon_{tier}", "Machines")
    m.greeble = False
    _mats(m)
    if tier == 1:
        m.box("dark", (0, 0, 0.1), (2.4, 1.8, 0.2), bevel=0.04)
        m.box("body_light", (0, 0, 0.95), (1.2, 1.3, 1.5), bevel=0.08)
        hazard_strip(m, (0, 0.67, 0.3), 1.1, "x", height=0.12)
        for sx in (-1, 1):
            x = sx * 0.85
            m.lathe("body_light", (x, 0, 0.2), [(0.42, 0), (0.42, 2.3), (0.3, 2.55), (0, 2.6)], seg=20)
            for z in (0.7, 1.3, 1.9):
                m.lathe("glow_filter", (x, 0, z), [(0.43, -0.06), (0.43, 0.06)], seg=20, caps=False)
                bolt_ring(m, "steel", (x, 0, z + 0.07), 0.4, 12, size=0.016)
            m.tube("pipe", (x, 0, 2.8), (sx * 0.35, 0, 2.0), 0.08, seg=8)
        m.lathe("dark", (0, 0, 1.7), [(0.28, 0), (0.24, 1.4), (0.3, 1.5), (0.3, 1.55)], seg=16, caps=False)
        _fan(m, "Spin_Z_Fan", (0, 0.7, 1.05), 0.44, blades=6, axis="y")
        _fx(m, "Fx_Mist", (0, 0, 3.2))
        _ctrl_cabinet(m, (0, 0.72, 0.45), 0.6, 0.35)
        _gauge(m, (-0.35, 0.8, 1.7), 0.07)
        indicator(m, (0.45, 0.66, 1.65))
    elif tier == 2:
        _plinth(m, 3.2, 2.4, 0.2)
        m.box("body_light", (0, 0, 1.05), (1.4, 1.5, 1.7), bevel=0.08)
        for k in range(4):
            sx, y = (-1, 1)[k % 2], (-0.45, 0.45)[k // 2]
            x = sx * 1.05
            m.lathe("body_light", (x, y, 0.2), [(0.4, 0), (0.4, 2.5), (0.3, 2.75), (0, 2.8)], seg=20)
            for z in (0.6, 1.2, 1.8, 2.3):
                m.lathe("glow_filter", (x, y, z), [(0.41, -0.05), (0.41, 0.05)], seg=20, caps=False)
                bolt_ring(m, "steel", (x, y, z + 0.06), 0.37, 12, size=0.014)
            m.tube("pipe", (x, y, 3.0), (sx * 0.4, y * 0.3, 2.15), 0.08, seg=8)
        m.box("steel", (0, 0, 2.2), (1.5, 1.6, 0.12))
        _stack(m, 0, -0.35, 2.28, 0.26, 1.2)
        m.cyl("glow_filter", (0, -0.35, 3.52), 0.2, 0.2, 0.03, seg=14)
        _fx(m, "Fx_Mist", (0, -0.35, 3.7))
        _fan(m, "Spin_Z_Fan", (0, 0.76, 1.2), 0.56, blades=8, axis="y")
        _ctrl_cabinet(m, (0, 1.1, 0.55), 0.8, 0.45)
        for k in range(3):
            _gauge(m, (-0.5 + k * 0.5, 0.82, 1.95), 0.07)
        _ladder(m, -1.6, 0.0, 0.2, 2.4, side="y", width=0.42)
        pipe_run(m, "pipe", "rust_edge", (-1.05, -1.0, 0.45), (1.05, -1.0, 0.45), 0.07, clamps=4)
        indicator(m, (0.55, 0.78, 1.95))
    else:
        _plinth(m, 4.6, 4.0, 0.24)
        m.cyl("body", (0, 0, 0.5), 1.7, 1.7, 0.5, seg=28)
        # Coluna central ionizadora com três anéis giratórios
        m.cyl("glass_tube", (0, 0, 2.5), 0.5, 0.5, 4.0, seg=22)
        m.cyl("glow_filter", (0, 0, 2.5), 0.2, 0.2, 3.7, seg=14)
        m.lathe("steel", (0, 0, 4.5), [(0.58, 0), (0.58, 0.1), (0.3, 0.35), (0.12, 0.5)], seg=22)
        for k, z in enumerate((1.4, 2.5, 3.6)):
            nm = f"Spin_Y_Ring{k}"
            m.origins[nm] = (0, 0, z)
            m.lathe("glow_filter", (0, 0, z), [(0.72, -0.03), (0.72, 0.03)], seg=28, caps=False, obj=nm)
            m.lathe("steel", (0, 0, z), [(0.74, -0.05), (0.74, -0.03)], seg=28, caps=False, obj=nm)
            for a in (0, 120, 240):
                r = math.radians(a + k * 40)
                m.box("steel", (0.72 * math.cos(r), 0.72 * math.sin(r), z), (0.1, 0.1, 0.12), obj=nm)
        for k in range(6):
            a = math.radians(30 + k * 60)
            x, y = math.cos(a) * 1.5, math.sin(a) * 1.5
            m.lathe("body_light", (x, y, 0.24), [(0.38, 0), (0.38, 2.2), (0.28, 2.45), (0, 2.5)], seg=18)
            for z in (0.7, 1.3, 1.9):
                m.lathe("glow_filter", (x, y, z), [(0.39, -0.05), (0.39, 0.05)], seg=18, caps=False)
            m.tube("pipe", (x, y, 2.6), (x * 0.3, y * 0.3, 3.4), 0.07, seg=8)
        m.lathe("steel", (0, 0, 2.7), [(1.9, 0), (1.9, 0.05)], seg=32, caps=False)
        _stack(m, 0, 0, 5.0, 0.3, 0.7)
        m.cyl("glow_filter", (0, 0, 5.77), 0.24, 0.24, 0.03, seg=14)
        _fx(m, "Fx_Spark", (0, 0, 5.9))
        _fx(m, "Fx_Mist", (0, 0, 2.0))
        for k, x in enumerate((-0.55, 0.55)):
            _fan(m, f"Spin_Z_Fan{k}", (x, 1.82, 0.95), 0.46, blades=8, axis="y")
        _ctrl_cabinet(m, (0, 2.1, 0.45), 1.2, 0.5)
        for k in range(4):
            _gauge(m, (-0.9 + k * 0.6, 2.2, 1.45), 0.08)
        _ladder(m, 2.4, 0.0, 0.24, 2.5, side="y", width=0.5)
        hazard_strip(m, (0, -2.02, 0.12), 4.2, "x", height=0.12)
        pipe_run(m, "pipe", "rust_edge", (-1.8, -1.6, 0.45), (1.8, -1.6, 0.45), 0.08, clamps=5)
        indicator(m, (1.0, 2.05, 1.5))
    return m


# =============================================================================
# Kit de construção de base (grade de 4 m, pé-direito de 3 m), mobília, defesas e sentinela hostil.
# Origem dos pisos/fundações = face SUPERIOR (z=0). Paredes: origem no centro da base, 4 m de largura ao longo de X,
# 0,2 m de espessura em Y. Interior de uma sala = lado +Y da parede (frente do Blender).
# =============================================================================
def _kit_mats(m):
    _mats(m)
    m.material("panel", (0.64, 0.67, 0.70), 0.55, 0.3)
    m.material("panel_dark", (0.22, 0.24, 0.27), 0.6, 0.3)
    m.material("concrete", (0.52, 0.52, 0.5), 0.92, 0.0)
    m.material("alloy", (0.42, 0.46, 0.52), 0.35, 0.8)
    m.material("glass_win", (0.6, 0.8, 0.9), 0.05, 0.0)
    m.material("glow_light", (1.0, 0.95, 0.8), 0.4)
    m.material("glow_red", (1.0, 0.2, 0.15), 0.4)
    m.material("cloth", (0.35, 0.4, 0.3), 0.95, 0.0)


def _panel_face(m, mat_in, w, h, y, z0, sign=1, ribs=3):
    """Painéis em relevo numa face da parede (y = posição da face, sign = lado)."""
    pw = (w - 0.3) / ribs
    for i in range(ribs):
        x = -w / 2 + 0.15 + pw * (i + 0.5)
        m.box(mat_in, (x, y + sign * 0.012, z0 + h / 2), (pw - 0.12, 0.024, h - 0.5), bevel=0.01)
    for z in (0.7, 1.55, 2.4):
        m.box("detail_seam" if "detail_seam" in m.mats else "panel_dark", (0, y + sign * 0.01, z0 + z), (w - 0.1, 0.02, 0.035))


def _wall_shell(m, mat="panel", heavy=False, door=False, window=False, hatch=False):
    t = 0.3 if heavy else 0.2
    body = "alloy" if heavy else mat
    _kit_mats(m)
    # Montantes das extremidades, rodapé e viga superior
    for x in (-1.95, 1.95):
        m.box("panel_dark", (x, 0, 1.5), (0.1, t + 0.04, 3.0), bevel=0.01)
    m.box("panel_dark", (0, 0, 0.09), (4.0, t + 0.06, 0.18))
    m.box("panel_dark", (0, 0, 2.93), (4.0, t + 0.05, 0.14))
    if not (door or hatch):
        lo, hi = (0.18, 2.86)
        if window:
            m.box(body, (0, 0, 0.5), (3.8, t, 0.64), bevel=0.01)       # peitoril
            m.box(body, (0, 0, 2.62), (3.8, t, 0.5), bevel=0.01)       # verga
            m.box("panel_dark", (-1.0, 0, 1.55), (0.1, t, 1.5)); m.box("panel_dark", (1.0, 0, 1.55), (0.1, t, 1.5))
            m.box("body", (-1.5, 0, 1.55), (1.0, t, 1.5), bevel=0.01); m.box("body", (1.5, 0, 1.55), (1.0, t, 1.5), bevel=0.01)
            m.box("glass_win", (0, 0, 1.55), (1.95, 0.04, 1.42))
            m.box("steel", (0, 0.03, 1.55), (0.04, 0.05, 1.42)); m.box("steel", (0, 0.03, 1.55), (1.95, 0.05, 0.04))
            for dz in (-0.72, 0.72):
                m.box("steel", (0, 0, 1.55 + dz), (2.05, t + 0.03, 0.05))
        else:
            m.box(body, (0, 0, 1.5), (3.8, t, 2.68), bevel=0.012)
            _panel_face(m, "panel_dark", 3.8, 2.68, t / 2, 0.18, 1)
            _panel_face(m, "panel_dark", 3.8, 2.68, -t / 2, 0.18, -1)
            for x in (-1.2, 1.2):
                m.box("glow_screen", (x, t / 2 + 0.03, 1.6), (0.18, 0.012, 0.1))
    else:
        ow = 1.5 if door else 1.3
        side = (4.0 - ow) / 2
        for sgn in (-1, 1):
            m.box(body, (sgn * (ow / 2 + side / 2), 0, 1.5), (side - 0.1, t, 2.68), bevel=0.012)
            _panel_face(m, "panel_dark", side - 0.1, 2.68, t / 2, 0.18, 1, ribs=1)
        m.box(body, (0, 0, 2.7), (ow + 0.2, t, 0.35), bevel=0.012)
        for sgn in (-1, 1):
            m.box("steel", (sgn * (ow / 2 + 0.05), 0, 1.25), (0.1, t + 0.08, 2.3), bevel=0.01)
        m.box("steel", (0, 0, 2.38), (ow, t + 0.08, 0.1))
        hazard_strip(m, (0, t / 2 + 0.05, 2.5), ow, "x", height=0.08)
        if door:
            m.box("panel_dark", (ow * 0.28, -0.02, 1.2), (ow * 0.5, 0.07, 2.2), bevel=0.01)   # folha meio aberta
            m.box("glow_screen", (ow * 0.28, 0.02, 1.9), (0.4, 0.012, 0.08))
        else:
            m.cyl("steel", (0, 0, 1.2), 0.62, 0.62, 0.12, rot=(90, 0, 0), seg=20)          # escotilha redonda
            m.cyl("panel_dark", (0, 0.07, 1.2), 0.52, 0.52, 0.03, rot=(90, 0, 0), seg=20)
            _valve(m, (0, 0.1, 1.2), 0.2)
            for k in range(8):
                a = k * math.pi / 4
                m.cyl("steel", (math.cos(a) * 0.57, 0.07, 1.2 + math.sin(a) * 0.57), 0.025, 0.025, 0.03, rot=(90, 0, 0), seg=8)
            m.box("glow_red", (0, t / 2 + 0.04, 2.55), (0.24, 0.015, 0.07))
            m.box("glow_screen", (0.78, t / 2 + 0.03, 1.35), (0.1, 0.012, 0.1))
    # Rebites no rodapé e na viga
    for z in (0.09, 2.93):
        for k in range(14):
            for sgn in (-1, 1):
                m.cyl("steel", (-1.8 + k * 0.28, sgn * (t / 2 + 0.035), z), 0.018, 0.018, 0.02, rot=(90, 0, 0), seg=6)


def kit_wall(variant):
    m = Model(variant, "Base")
    m.greeble = False
    if variant == "base_wall":
        _wall_shell(m)
    elif variant == "base_wall_heavy":
        _wall_shell(m, heavy=True)
        for k in range(5):
            m.box("panel_dark", (-1.6 + k * 0.8, 0.18, 1.5), (0.06, 0.04, 2.6))
        hazard_strip(m, (0, 0.17, 0.3), 3.6, "x", height=0.1)
    elif variant == "base_wall_door":
        _wall_shell(m, door=True)
    elif variant == "base_wall_window":
        _wall_shell(m, window=True)
    else:
        _wall_shell(m, hatch=True)
    return m


def kit_floor(foundation):
    m = Model("base_foundation" if foundation else "base_floor", "Base")
    m.greeble = False
    _kit_mats(m)
    th = 0.5 if foundation else 0.22
    m.box("panel_dark", (0, 0, -th / 2 - 0.0), (4.0, 4.0, th), bevel=0.02)
    m.box("panel", (0, 0, -0.03), (3.8, 3.8, 0.06), bevel=0.01)
    for k in range(1, 4):
        m.box("panel_dark", (-2.0 + k, 0, 0.002), (0.025, 3.8, 0.008))
        m.box("panel_dark", (0, -2.0 + k, 0.002), (3.8, 0.025, 0.008))
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box("steel", (sx * 1.93, sy * 1.93, -th / 2), (0.14, 0.14, th + 0.02), bevel=0.01)
    for i in range(4):
        hazard_strip(m, (0, -1.99 if i % 2 == 0 else 1.99, -0.02), 1.0, "x", height=0.04, depth=0.012)
    for k in range(8):
        for sgn in (-1, 1):
            m.cyl("steel", (-1.75 + k * 0.5, sgn * 1.85, 0.004), 0.02, 0.02, 0.015, seg=6)
    if foundation:
        for sx in (-1, 1):
            for sy in (-1, 1):
                x, y = sx * 1.6, sy * 1.6
                m.cyl("concrete", (x, y, -1.8), 0.19, 0.19, 2.6, seg=12)
                m.cyl("steel", (x, y, -0.55), 0.27, 0.27, 0.1, seg=12)
                bolt_ring(m, "steel", (x, y, -0.49), 0.22, 6, size=0.018)
        m.box("steel", (0, 0, -0.51), (1.0, 1.0, 0.02))
    else:
        m.box("glow_light", (0, 0, -th - 0.01), (1.2, 0.5, 0.02))
        m.box("steel", (0, 0, -th - 0.03), (1.3, 0.6, 0.02))
    return m


def kit_pillar():
    m = Model("base_pillar", "Base")
    m.greeble = False
    _kit_mats(m)
    m.cyl("panel_dark", (0, 0, 1.5), 0.17, 0.17, 3.0, seg=14)
    for z in (0.1, 1.5, 2.9):
        m.cyl("steel", (0, 0, z), 0.22, 0.22, 0.12, seg=14)
        bolt_ring(m, "steel", (0, 0, z + 0.07), 0.18, 8, size=0.015)
    m.box("hazard_y", (0, 0, 0.55), (0.35, 0.35, 0.05)) if "hazard_y" in m.mats else None
    return m


def kit_stairs():
    m = Model("base_stairs", "Base")
    m.greeble = False
    _kit_mats(m)
    n = 12
    for i in range(n):
        m.box("panel", (0, -1.8 + i * 0.3 + 0.15, 0.125 * (i + 1) * 2 - 0.125 + 0.0), (1.5, 0.3, 0.25), bevel=0.01) if False else None
    steps = 12
    for i in range(steps):
        z = 3.0 * (i + 0.5) / steps
        m.box("panel", (0, -1.85 + 3.7 * (i + 0.5) / steps, z / 2), (1.5, 3.7 / steps + 0.01, z), bevel=0.008)
        m.box("steel", (0, -1.85 + 3.7 * (i + 0.5) / steps, z - 0.005), (1.5, 3.7 / steps, 0.02))
    for sx in (-1, 1):
        m.tube("yellow", (sx * 0.82, -1.85, 1.0), (sx * 0.82, 1.85, 4.0), 0.025, seg=8)
        for k in range(5):
            t = k / 4
            m.tube("yellow", (sx * 0.82, -1.85 + 3.7 * t, 3.0 * t), (sx * 0.82, -1.85 + 3.7 * t, 3.0 * t + 1.0), 0.02, seg=6)
    return m


# ------------------------------------------------------------------ mobília e utilidades
def furniture(name):
    m = Model(name, "Base")
    m.greeble = False
    _kit_mats(m)
    if name == "base_bunk":
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.box("steel", (sx * 0.95, sy * 0.4, 0.9), (0.06, 0.06, 1.8))
        for z in (0.4, 1.3):
            m.box("panel_dark", (0, 0, z), (2.0, 0.86, 0.1), bevel=0.01)
            m.box("cloth", (-0.05, 0, z + 0.12), (1.85, 0.8, 0.16), bevel=0.03)
            m.box("body_light", (-0.78, 0, z + 0.25), (0.4, 0.5, 0.1), bevel=0.04)
        _ladder(m, 1.0, 0.0, 0.1, 1.5, side="y", width=0.4)
        m.box("glow_light", (0, 0.43, 1.65), (0.5, 0.02, 0.05))
    elif name == "base_locker":
        for k in range(2):
            x = -0.2 + k * 0.42
            m.box("panel", (x, 0, 1.0), (0.4, 0.55, 2.0), bevel=0.015)
            m.box("panel_dark", (x, 0.28, 1.5), (0.3, 0.012, 0.5))
            for z in (1.7, 1.78, 1.86):
                m.box("dark", (x, 0.285, z), (0.2, 0.01, 0.02))
            m.box("steel", (x + 0.1, 0.29, 1.0), (0.03, 0.03, 0.2))
            m.box("yellow", (x, 0.285, 0.35), (0.3, 0.01, 0.1))
    elif name == "base_workbench":
        m.box("panel_dark", (0, 0, 0.45), (2.0, 0.8, 0.08), bevel=0.01) if False else None
        m.box("steel", (0, 0, 0.9), (2.0, 0.8, 0.07), bevel=0.01)
        for sx in (-1, 1):
            m.box("panel_dark", (sx * 0.9, 0, 0.45), (0.08, 0.7, 0.9)); m.box("panel_dark", (sx * 0.9, 0, 0.14), (0.2, 0.7, 0.08))
        m.box("panel", (0.45, 0, 0.45), (0.8, 0.7, 0.8), bevel=0.01)
        for z in (0.2, 0.45, 0.7):
            m.box("panel_dark", (0.45, 0.36, z), (0.7, 0.012, 0.16)); m.box("steel", (0.45, 0.375, z), (0.2, 0.02, 0.025))
        m.box("body", (-0.55, 0.0, 1.0), (0.3, 0.16, 0.12), bevel=0.02)
        m.cyl("steel", (-0.55, 0, 1.1), 0.05, 0.05, 0.12, seg=8)
        m.box("dark", (0.2, -0.3, 1.45), (0.7, 0.04, 0.5)); m.box("glow_screen", (0.2, -0.275, 1.45), (0.62, 0.01, 0.42))
        m.box("dark", (-0.5, -0.38, 1.4), (0.02, 0.02, 0.9))
        for k in range(5):
            m.tube("steel", (-0.8 + k * 0.1, -0.38, 1.0), (-0.8 + k * 0.1, -0.38, 1.25 - (k % 2) * 0.1), 0.012, seg=6)
    elif name == "base_kitchen":
        m.box("panel", (0, 0, 0.45), (1.8, 0.7, 0.9), bevel=0.015)
        m.box("steel", (0, 0, 0.92), (1.84, 0.74, 0.05))
        for k in range(2):
            for j in range(2):
                m.cyl("dark", (-0.45 + k * 0.38, -0.1 + j * 0.3, 0.955), 0.12, 0.12, 0.015, seg=14)
                m.cyl("glow_heat", (-0.45 + k * 0.38, -0.1 + j * 0.3, 0.965), 0.09, 0.09, 0.008, seg=14)
        m.box("steel", (0.5, 0.0, 0.94), (0.5, 0.45, 0.08)); m.box("dark", (0.5, 0, 0.97), (0.44, 0.38, 0.02))
        m.tube("steel", (0.5, 0.3, 0.97), (0.5, 0.3, 1.25), 0.02, seg=8); m.tube("steel", (0.5, 0.3, 1.25), (0.5, 0.12, 1.25), 0.02, seg=8)
        m.box("panel", (0, 0.2, 1.7), (1.8, 0.3, 0.6), bevel=0.015)
        for x in (-0.5, 0.0, 0.5):
            m.box("panel_dark", (x, 0.36, 1.7), (0.5, 0.012, 0.5))
        m.box("body_light", (-0.5, 0, 1.15), (0.3, 0.2, 0.3), bevel=0.03)
    elif name == "base_medbay":
        m.box("panel", (0, 0, 0.35), (2.0, 0.9, 0.7), bevel=0.03)
        m.box("body_light", (0, 0, 0.8), (1.9, 0.8, 0.12), bevel=0.04)
        m.box("body_light", (-0.7, 0, 0.95), (0.5, 0.5, 0.12), bevel=0.05)
        m.tube("steel", (0.85, -0.5, 0.0), (0.85, -0.5, 2.0), 0.05, seg=8)
        m.tube("steel", (0.85, -0.5, 2.0), (0.2, -0.1, 1.55), 0.04, seg=8)
        m.box("dark", (0.2, -0.1, 1.45), (0.5, 0.35, 0.12), bevel=0.03)
        m.box("glow_screen", (0.2, -0.1, 1.38), (0.44, 0.3, 0.012))
        m.box("dark", (-0.8, -0.5, 1.5), (0.6, 0.04, 0.4)); m.box("glow_screen", (-0.8, -0.475, 1.5), (0.52, 0.01, 0.32))
        m.box("red" if False else "glow_red", (0.0, 0.46, 0.45), (0.3, 0.012, 0.3)); m.box("body_light", (0.0, 0.47, 0.45), (0.1, 0.012, 0.26)); m.box("body_light", (0.0, 0.47, 0.45), (0.26, 0.012, 0.1))
    elif name == "base_o2gen":
        m.box("dark", (0, 0, 0.08), (1.3, 1.3, 0.16), bevel=0.03)
        m.cyl("body_light", (0, 0, 1.0), 0.5, 0.5, 1.7, seg=22)
        m.lathe("body_light", (0, 0, 1.85), [(0.5, 0), (0.4, 0.15), (0.12, 0.25)], seg=22)
        for z in (0.5, 1.1, 1.7):
            m.lathe("glow_core", (0, 0, z), [(0.51, -0.03), (0.51, 0.03)], seg=22, caps=False)
            bolt_ring(m, "steel", (0, 0, z + 0.05), 0.46, 12, size=0.014)
        _fan(m, "Spin_Y_Fan", (0, 0, 2.15), 0.42, blades=6, axis="z")
        m.box("dark", (0, 0, 2.08), (1.0, 1.0, 0.03))
        _ctrl_cabinet(m, (0, 0.62, 0.7), 0.7, 0.45)
        _fx(m, "Fx_Mist", (0, 0, 2.5))
        indicator(m, (0.5, 0.55, 1.95))
        hazard_strip(m, (0, 0.67, 0.08), 1.1, "x", height=0.1)
    elif name == "base_battery":
        m.box("dark", (0, 0, 0.08), (1.2, 0.8, 0.16), bevel=0.02)
        for k in range(4):
            m.box("panel", (-0.45 + k * 0.3, 0, 0.7), (0.26, 0.7, 1.0), bevel=0.02)
            m.box("dark", (-0.45 + k * 0.3, 0.355, 0.7), (0.2, 0.012, 0.8))
            for j in range(5):
                m.box("glow_screen", (-0.45 + k * 0.3, 0.362, 0.4 + j * 0.15), (0.16, 0.008, 0.07))
            m.cyl("steel", (-0.45 + k * 0.3, 0, 1.24), 0.07, 0.07, 0.06, seg=8)
        m.box("yellow", (0, 0.36, 0.18), (1.1, 0.01, 0.08))
        indicator(m, (0.5, 0.3, 1.3))
    elif name == "base_purifier":
        m.box("dark", (0, 0, 0.08), (1.4, 1.0, 0.16), bevel=0.02)
        m.cyl("glass_tube", (-0.35, 0, 0.9), 0.38, 0.38, 1.5, seg=20)
        m.cyl("glow_core", (-0.35, 0, 0.8), 0.3, 0.3, 1.1, seg=16)
        m.lathe("steel", (-0.35, 0, 1.65), [(0.4, 0), (0.4, 0.06), (0.2, 0.15)], seg=20)
        m.box("body", (0.4, 0, 0.6), (0.5, 0.6, 1.0), bevel=0.03)
        for k in range(3):
            m.cyl("pipe", (0.4, 0.31, 0.3 + k * 0.28), 0.06, 0.06, 0.04, rot=(90, 0, 0), seg=10)
        pipe_run(m, "pipe", "rust_edge", (-0.35, 0, 1.7), (0.4, 0, 1.3), 0.05, clamps=2)
        _gauge(m, (0.4, 0.32, 0.95), 0.07)
        indicator(m, (0.6, 0.3, 1.15))
    return m


# ------------------------------------------------------------------ defesas
def defense(name):
    m = Model(name, "Base")
    m.greeble = False
    _kit_mats(m)
    if name == "base_turret_1":
        m.cyl("dark", (0, 0, 0.05), 0.5, 0.5, 0.1, seg=16)
        for k in range(3):
            a = math.radians(k * 120 + 30)
            m.tube("steel", (0, 0, 0.9), (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.05), 0.04, seg=8)
        m.cyl("panel_dark", (0, 0, 0.95), 0.18, 0.18, 0.3, seg=12)
        m.origins["Aim_Y_Head"] = (0, 0, 1.2)
        B = I4
        m.box("panel", (0, 0, 1.2), (0.42, 0.5, 0.34), bevel=0.04, obj="Aim_Y_Head")
        m.cyl("steel", (0, 0.45, 1.22), 0.06, 0.06, 0.6, rot=(90, 0, 0), seg=10, obj="Aim_Y_Head")
        m.cyl("glow_core", (0, 0.76, 1.22), 0.05, 0.05, 0.02, rot=(90, 0, 0), seg=10, obj="Aim_Y_Head")
        m.box("glow_red", (0, 0.26, 1.34), (0.14, 0.02, 0.05), obj="Aim_Y_Head")
        for sx in (-1, 1):
            m.box("steel", (sx * 0.24, 0.05, 1.2), (0.05, 0.3, 0.2), obj="Aim_Y_Head")
        _fx(m, "Fx_Muzzle", (0, 0.8, 1.22))
        indicator(m, (0.0, -0.2, 0.7))
    elif name == "base_turret_2":
        m.cyl("dark", (0, 0, 0.1), 0.85, 0.85, 0.2, seg=22)
        bolt_ring(m, "steel", (0, 0, 0.22), 0.75, 14, size=0.03)
        m.cyl("alloy", (0, 0, 0.55), 0.55, 0.45, 0.7, seg=20)
        m.cyl("panel_dark", (0, 0, 1.0), 0.5, 0.5, 0.2, seg=20)
        m.origins["Aim_Y_Head"] = (0, 0, 1.4)
        m.box("alloy", (0, 0, 1.4), (0.9, 0.9, 0.55), bevel=0.08, obj="Aim_Y_Head")
        m.box("panel_dark", (0, 0.05, 1.78), (0.7, 0.6, 0.12), bevel=0.04, obj="Aim_Y_Head")
        for sx in (-1, 1):
            m.cyl("steel", (sx * 0.22, 0.85, 1.4), 0.075, 0.075, 1.0, rot=(90, 0, 0), seg=12, obj="Aim_Y_Head")
            m.cyl("glow_core", (sx * 0.22, 1.38, 1.4), 0.06, 0.06, 0.02, rot=(90, 0, 0), seg=12, obj="Aim_Y_Head")
            for t in range(3):
                m.lathe("glow_core", (sx * 0.22, 0.5 + t * 0.25, 1.4), [(0.095, -0.02), (0.095, 0.02)], seg=12, caps=False, base=T(sx * 0.22, 0.5 + t * 0.25, 1.4) @ R(90, 0, 0) @ T(-sx * 0.22, -(0.5 + t * 0.25), -1.4), obj="Aim_Y_Head") if False else None
            m.box("glow_core", (sx * 0.4, 0.2, 1.4), (0.04, 0.5, 0.06), obj="Aim_Y_Head")
        m.box("glow_red", (0, 0.47, 1.55), (0.3, 0.02, 0.08), obj="Aim_Y_Head")
        _fx(m, "Fx_Muzzle", (0, 1.4, 1.4))
        _ladder(m, 0, -0.9, 0.1, 0.9, side="y", width=0.3) if False else None
        indicator(m, (0.4, -0.4, 0.6))
    elif name == "base_floodlight":
        m.cyl("dark", (0, 0, 0.06), 0.3, 0.3, 0.12, seg=14)
        m.tube("steel", (0, 0, 0.1), (0, 0, 3.4), 0.05, 0.035, seg=10)
        for z in (0.5, 1.5, 2.5):
            m.cyl("panel_dark", (0, 0, z), 0.07, 0.07, 0.06, seg=10)
        m.box("panel_dark", (0, 0.1, 3.55), (0.9, 0.3, 0.55), bevel=0.04)
        for sx in (-1, 0, 1):
            for sz in (-1, 1):
                m.cyl("steel", (sx * 0.27, 0.27, 3.55 + sz * 0.14), 0.1, 0.1, 0.06, rot=(90, 0, 0), seg=14)
                m.cyl("glow_light", (sx * 0.27, 0.3, 3.55 + sz * 0.14), 0.085, 0.085, 0.015, rot=(90, 0, 0), seg=14)
        m.tube("steel", (0, 0.0, 3.4), (0, 0.1, 3.55), 0.03, seg=6)
        cable(m, "dark", (0.05, 0, 0.4), (0.8, 0.8, 0.02), sag=0.05, r=0.02)
    else:  # base_shock_fence
        for sx in (-1, 1):
            m.cyl("dark", (sx * 1.9, 0, 0.05), 0.2, 0.2, 0.1, seg=10)
            m.tube("panel_dark", (sx * 1.9, 0, 0.05), (sx * 1.9, 0, 1.9), 0.06, seg=10)
            for z in (0.5, 1.0, 1.5):
                m.cyl("body_light", (sx * 1.9, 0, z), 0.09, 0.09, 0.1, rot=(0, 90, 0), seg=8)
            m.cyl("glow_core", (sx * 1.9, 0, 1.95), 0.07, 0.07, 0.1, seg=8)
        for z in (0.5, 1.0, 1.5):
            m.tube("glow_core", (-1.9, 0, z), (1.9, 0, z), 0.012, seg=6)
        _fx(m, "Fx_Spark", (0, 0, 1.0))
        indicator(m, (-1.9, 0.0, 2.1))
    return m


# ------------------------------------------------------------------ inimigo: sentinela do velho mundo
def sentinel():
    m = Model("sentinel", "Fauna")
    m.greeble = False
    _kit_mats(m)
    m.blob("alloy", (0, 0, 0), (0.42, 0.42, 0.22), sub=2, noise=0.0, seed=1, smooth=True)
    m.lathe("panel_dark", (0, 0, 0), [(0.46, -0.03), (0.46, 0.03)], seg=24, caps=False)
    m.blob("glow_red", (0, 0.36, 0.02), (0.14, 0.05, 0.14), sub=2, noise=0.0, seed=2, smooth=True)
    m.cyl("dark", (0, 0.34, 0.02), 0.19, 0.19, 0.04, rot=(90, 0, 0), seg=14)
    m.origins["Spin_Y_Ring"] = (0, 0, 0.0)
    m.lathe("steel", (0, 0, 0), [(0.62, 0.0), (0.62, 0.05)], seg=26, caps=False, obj="Spin_Y_Ring")
    for k in range(4):
        a = math.radians(k * 90 + 45)
        m.box("panel_dark", (math.cos(a) * 0.62, math.sin(a) * 0.62, 0.02), (0.16, 0.16, 0.1), obj="Spin_Y_Ring")
        m.cyl("glow_core", (math.cos(a) * 0.62, math.sin(a) * 0.62, -0.05), 0.06, 0.06, 0.02, seg=8, obj="Spin_Y_Ring")
    for sx in (-1, 1):
        m.box("alloy", (sx * 0.34, 0.15, -0.16), (0.1, 0.4, 0.1), bevel=0.02)
        m.cyl("steel", (sx * 0.34, 0.4, -0.16), 0.03, 0.03, 0.3, rot=(90, 0, 0), seg=8)
    m.tube("steel", (0, 0, 0.2), (0, -0.1, 0.5), 0.015, seg=6)
    m.blob("glow_red", (0, -0.1, 0.52), (0.04, 0.04, 0.04), sub=1, noise=0, seed=3)
    _fx(m, "Fx_Muzzle", (0, 0.58, -0.16))
    return m


# =============================================================================
# Estação Horizonte por dentro (cabine -> corredor -> hangar), nave de descida e braço com comunicador de pulso.
# Interior: o jogador anda de +Y para -Y do Blender (= +Z no Unity). Cabine y 0..-7, corredor -7..-27, hangar -27..-51.
# =============================================================================
def _st_mats(m):
    _kit_mats(m)
    m.material("hull_metal", (0.62, 0.65, 0.7), 0.35, 0.9)
    m.material("hull_dark_metal", (0.26, 0.29, 0.34), 0.4, 0.9)
    m.material("floor_deck_metal", (0.36, 0.39, 0.44), 0.4, 0.9)
    m.material("suit", (0.82, 0.84, 0.86), 0.7, 0.0)
    m.material("suit_dark", (0.12, 0.13, 0.15), 0.8, 0.0)
    m.material("orange", (0.9, 0.38, 0.1), 0.6)
    m.material("glass_shield", (0.35, 0.7, 1.0), 0.0, 0.0)
    m.material("glow_blue", (0.35, 0.7, 1.0), 0.4)
    m.material("glow_light_dim", (1.0, 0.95, 0.8), 0.4)


def _shell_box(m, x0, x1, y0, y1, z0, z1, t=0.3, openings=(), mat="hull_metal"):
    """Sala vazia: piso, teto e quatro paredes. `openings` = ('x+'|'x-'|'y+'|'y-', centro, largura, base_z, topo_z)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, d, h = x1 - x0, y1 - y0, z1 - z0
    m.box("floor_deck_metal", (cx, cy, z0 - t / 2), (w + 2 * t, d + 2 * t, t))
    m.box("hull_dark_metal", (cx, cy, z1 + t / 2), (w + 2 * t, d + 2 * t, t))

    def wall(side):
        ops = [o for o in openings if o[0] == side]
        horizontal = side in ("y+", "y-")
        length = w if horizontal else d
        lo = (x0 if horizontal else y0)
        pos = {"x-": x0 - t / 2, "x+": x1 + t / 2, "y-": y0 - t / 2, "y+": y1 + t / 2}[side]
        wall_len = length + 2 * t
        cuts = sorted(((o[1] - o[2] / 2, o[1] + o[2] / 2, o[3], o[4]) for o in ops))
        cur = lo - t
        segs = []
        for (a, b, zb, zt) in cuts:
            segs.append((cur, a, z0, z1))
            segs.append((a, b, z0, zb)) if zb > z0 else None
            segs.append((a, b, zt, z1)) if zt < z1 else None
            cur = b
        segs.append((cur, lo + length + t, z0, z1))
        for (a, b, zb, zt) in segs:
            if b - a < 0.01 or zt - zb < 0.01:
                continue
            c, hh = (a + b) / 2, zt - zb
            if horizontal:
                m.box(mat, (c, pos, zb + hh / 2), (b - a, t, hh))
            else:
                m.box(mat, (pos, c, zb + hh / 2), (t, b - a, hh))
    for s in ("x-", "x+", "y-", "y+"):
        wall(s)
    _plating(m, x0, x1, y0, y1, z0, z1, openings)


def _plating(m, x0, x1, y0, y1, z0, z1, openings=()):
    """Chapas de metal: costuras verticais a cada 1,2 m, faixas horizontais e rebites nas quatro paredes por dentro."""
    m.material("plate_seam", (0.08, 0.09, 0.1), 0.5, 0.8)
    m.material("plate_rivet_metal", (0.75, 0.77, 0.8), 0.3, 1.0)
    h = z1 - z0
    for side in ("x-", "x+", "y-", "y+"):
        horizontal = side in ("y+", "y-")
        length = (x1 - x0) if horizontal else (y1 - y0)
        lo = x0 if horizontal else y0
        face = {"x-": x0 + 0.015, "x+": x1 - 0.015, "y-": y0 + 0.015, "y+": y1 - 0.015}[side]
        gaps = [(o[1] - o[2] / 2 - 0.1, o[1] + o[2] / 2 + 0.1) for o in openings if o[0] == side]
        n = max(1, int(length / 1.2))
        for k in range(n + 1):
            t = lo + k * length / n
            if any(a <= t <= b for a, b in gaps):
                continue
            if horizontal:
                m.box("plate_seam", (t, face, z0 + h / 2), (0.035, 0.03, h - 0.2))
            else:
                m.box("plate_seam", (face, t, z0 + h / 2), (0.03, 0.035, h - 0.2))
            for zz in (0.25, h / 2, h - 0.25):
                if horizontal:
                    m.cyl("plate_rivet_metal", (t + 0.1, face, z0 + zz), 0.022, 0.022, 0.02, rot=(90, 0, 0), seg=6)
                else:
                    m.cyl("plate_rivet_metal", (face, t + 0.1, z0 + zz), 0.022, 0.022, 0.02, rot=(0, 90, 0), seg=6)
        for zz in (h * 0.33, h * 0.66):
            if horizontal:
                m.box("plate_seam", ((x0 + x1) / 2, face, z0 + zz), (length, 0.03, 0.03))
            else:
                m.box("plate_seam", (face, (y0 + y1) / 2, z0 + zz), (0.03, length, 0.03))



def _door_frame(m, x, y, along, width, height, glow="glow_screen"):
    """Moldura de porta com faixa de alerta e luz de estado (along='x' = porta numa parede perpendicular a Y)."""
    if along == "x":
        m.box("steel", (x - width / 2 - 0.06, y, height / 2), (0.12, 0.5, height + 0.1))
        m.box("steel", (x + width / 2 + 0.06, y, height / 2), (0.12, 0.5, height + 0.1))
        m.box("steel", (x, y, height + 0.06), (width + 0.24, 0.5, 0.12))
        hazard_strip(m, (x, y + 0.26, height + 0.2), width + 0.24, "x", height=0.1)
        m.box(glow, (x, y + 0.26, height - 0.1), (0.5, 0.02, 0.07))


def _rack_panel(m, x, y, side, depth=1.4, facing="x"):
    """Rack de equipamento: gabinete com porta de acesso, grade de ventilação, tela de status e puxador amarelo."""
    if facing == "x":
        sgn = -side
        m.box("hull_dark_metal", (x, y, 1.15), (0.3, depth, 1.7), bevel=0.01)
        fx = x + sgn * 0.16
        m.box("hull_metal", (fx, y, 1.3), (0.02, depth - 0.16, 1.3))
        for k in range(6):
            m.box("plate_seam" if "plate_seam" in m.mats else "dark", (fx + sgn * 0.012, y, 0.5 + k * 0.045), (0.012, depth - 0.4, 0.015))
        m.box("dark", (fx + sgn * 0.012, y - depth * 0.15, 1.65), (0.012, 0.5, 0.26)); m.box("glow_screen", (fx + sgn * 0.02, y - depth * 0.15, 1.65), (0.008, 0.42, 0.2))
        m.box("yellow", (fx + sgn * 0.02, y + depth * 0.3, 1.2), (0.03, 0.04, 0.16))
        m.box("glow_screen", (fx + sgn * 0.02, y + depth * 0.3, 1.9), (0.008, 0.1, 0.03))
    else:
        m.box("hull_dark_metal", (x, y, 1.2), (depth, 0.3, 2.3), bevel=0.01)
        fy = y + 0.16
        m.box("hull_metal", (x, fy, 1.3), (depth - 0.16, 0.02, 2.0))
        for k in range(6):
            m.box("dark", (x, fy + 0.012, 0.45 + k * 0.045), (depth - 0.4, 0.012, 0.015))
        m.box("dark", (x, fy + 0.012, 1.75), (0.5, 0.012, 0.26)); m.box("glow_screen", (x, fy + 0.02, 1.75), (0.42, 0.008, 0.2))
        m.box("yellow", (x + depth * 0.3, fy + 0.02, 1.15), (0.04, 0.03, 0.16))


def station_interior():
    m = Model("station_interior", "Station")
    m.greeble = False
    _st_mats(m)
    # --- Cabine (7 x 6 x 3,2) com porta no fundo (y = -7)
    _shell_box(m, -3, 3, -7, 0, 0, 3.2, openings=[("y-", 0, 1.8, 0, 2.5), ("x+", -3.8, 3.2, 0.9, 2.6)])
    _door_frame(m, 0, -7, "x", 1.8, 2.5)
    # Janela da cabine (parede +X) com vidro e moldura
    m.box("glass_win", (3.12, -3.8, 1.75), (0.06, 3.2, 1.7))
    for yy in (-2.2, -5.4):
        m.box("steel", (3.14, yy, 1.75), (0.14, 0.12, 1.8))
    for zz in (0.9, 2.6):
        m.box("steel", (3.14, -3.8, zz), (0.14, 3.3, 0.12))
    # Cápsula de sono (aberta) encostada na parede -X
    m.box("hull_dark_metal", (-1.6, -1.8, 0.2), (2.4, 1.2, 0.4), bevel=0.04)
    m.box("hull_metal", (-1.6, -1.8, 0.55), (2.2, 1.0, 0.3), bevel=0.06)
    m.box("cloth", (-1.55, -1.8, 0.78), (2.0, 0.86, 0.16), bevel=0.05)
    m.box("body_light", (-2.35, -1.8, 0.92), (0.45, 0.5, 0.1), bevel=0.04)
    m.box("hull_metal", (-1.6, -2.6, 1.2), (2.3, 0.1, 1.2), rot=(-35, 0, 0), bevel=0.03)          # tampa aberta
    m.box("glow_blue", (-1.6, -2.55, 1.15), (1.8, 0.02, 0.5), rot=(-35, 0, 0))
    m.box("glow_screen", (-0.45, -1.8, 0.62), (0.03, 0.5, 0.12))
    # Mesa com tela e armário da cabine
    m.box("panel", (2.3, -0.6, 0.45), (1.1, 1.6, 0.9), bevel=0.02)
    m.box("steel", (2.3, -0.6, 0.93), (1.14, 1.64, 0.05))
    m.box("dark", (2.7, -0.6, 1.5), (0.06, 0.9, 0.6)); m.box("glow_screen", (2.66, -0.6, 1.5), (0.01, 0.8, 0.5))
    for k in range(2):
        m.box("panel", (-2.6, -5.8 + k * 0.7, 1.0), (0.6, 0.62, 2.0), bevel=0.02)
        m.box("panel_dark", (-2.3, -5.8 + k * 0.7, 1.4), (0.01, 0.5, 0.6))
    for z in (0.3, 1.6, 2.9):
        m.box("glow_light", (0, -3.5, 3.18), (2.0, 0.3, 0.02)) if z == 2.9 else None
    for sx in (-1, 1):                                   # racks de armazenamento ao lado da porta
        _rack_panel(m, sx * 2.3, -6.55, 0, depth=1.6, facing="y")
    m.tube("steel", (-2.8, -0.6, 1.15), (-2.8, -4.4, 1.15), 0.025, seg=8)   # corrimão junto da cápsula
    m.box("dark", (-2.84, -5.0, 1.8), (0.03, 0.34, 0.26)); m.box("glow_screen", (-2.82, -5.0, 1.8), (0.01, 0.28, 0.2))   # foto da família
    m.tube("pipe", (-2.6, -0.3, 3.0), (-2.6, -6.7, 3.0), 0.06, seg=10)
    m.tube("pipe", (2.6, -0.3, 3.0), (2.6, -6.7, 3.0), 0.06, seg=10)
    m.box("glow_light_dim", (0, -1.5, 3.17), (1.6, 0.3, 0.03))
    m.box("glow_light_dim", (0, -5.5, 3.17), (1.6, 0.3, 0.03))
    # --- Corredor (3,6 x 20 x 3): costelas, luzes, janelas laterais
    _shell_box(m, -1.8, 1.8, -27, -7, 0, 3.0, openings=[("x+", -12.5, 2.4, 0.9, 2.5), ("x+", -21.0, 2.4, 0.9, 2.5), ("y-", 0, 2.0, 0, 2.6), ("y+", 0, 1.8, 0, 2.5)])
    for yy in (-12.5, -21.0):
        m.box("glass_win", (1.92, yy, 1.7), (0.06, 2.4, 1.6))
        for dy in (-1.25, 0.0, 1.25):
            m.box("steel", (1.94, yy + dy, 1.7), (0.14, 0.1, 1.7))
        for zz in (0.9, 2.5):
            m.box("steel", (1.94, yy, zz), (0.14, 2.5, 0.1))
    for k in range(10):
        yy = -8.0 - k * 2.0
        m.box("hull_dark_metal", (0, yy, 2.9), (3.72, 0.16, 0.2))   # costela: viga do teto (as colunas vêm abaixo)
        m.box("floor_deck_metal", (0, yy, 0.05), (2.4, 0.18, 0.04))
        m.box("glow_light_dim", (0, yy - 1.0, 2.97), (1.4, 0.3, 0.03))
        for s in (-1, 1):
            m.box("steel", (s * 1.72, yy, 1.5), (0.1, 0.22, 2.9))
        for s in (-1, 1):
            m.box("glow_blue", (s * 1.2, yy - 1.0, 0.03), (0.04, 1.4, 0.02))
    for s in (-1, 1):
        m.tube("pipe", (s * 1.5, -7.5, 2.7), (s * 1.5, -26.5, 2.7), 0.07, seg=10)
        m.tube("steel", (s * 1.5, -7.5, 2.45), (s * 1.5, -26.5, 2.45), 0.04, seg=8)

    # --- Referências (ISS, nós com "quatro racks" e corredores de ficção): racks com painéis de acesso, grades de ventilação e
    # telas ao longo das paredes, corrimãos com suportes, feixes de cabos, cantos chanfrados no teto e vigas em arco.
    for side, ys in ((-1, (-9.0, -11.0, -13.0, -18.5, -20.5, -23.0, -25.0)), (1, (-9.0, -16.5, -18.5, -24.5))):
        for yy in ys:
            _rack_panel(m, side * 1.64, yy, side)
    for side in (-1, 1):
        m.tube("steel", (side * 1.5, -7.6, 1.15), (side * 1.5, -26.4, 1.15), 0.025, seg=8)
        for k in range(10):
            m.tube("steel", (side * 1.62, -8.2 - k * 2.0, 1.15), (side * 1.5, -8.2 - k * 2.0, 1.15), 0.02, seg=6)
        for dz in (0.22, 0.34):
            m.tube("dark", (side * 1.7, -7.6, dz), (side * 1.7, -26.4, dz), 0.035, seg=8)
        m.box("hull_metal", (side * 1.5, -17.0, 2.78), (0.55, 19.2, 0.06), rot=(0, side * 45, 0))
    for k in range(10):
        for side in (-1, 1):
            m.box("steel", (side * 1.4, -8.0 - k * 2.0, 2.82), (0.5, 0.14, 0.05), rot=(0, side * 45, 0))
    hazard_strip(m, (0, -7.2, 2.7), 3.4, "x", height=0.08)
    _door_frame(m, 0, -27, "x", 2.0, 2.6)
    # --- Hangar (20 x 24 x 10)
    _shell_box(m, -10, 10, -51, -27, 0, 10, t=0.35, openings=[("y-", 0, 14, 1.2, 8.2), ("y+", 0, 2.0, 0, 2.6)])
    m.box("glass_shield", (0, -51.1, 4.7), (14, 0.05, 7.0))
    for s in (-1, 1):
        m.box("steel", (s * 7.2, -51.0, 4.7), (0.4, 0.5, 7.2)); m.box("glow_red", (s * 7.2, -50.7, 4.7), (0.12, 0.05, 6.8))
    m.box("steel", (0, -51.0, 8.3), (14.8, 0.5, 0.4)); m.box("steel", (0, -51.0, 1.1), (14.8, 0.5, 0.4))
    # Pad de pouso: anel e faixas de alerta no piso
    m.material("pad_mark", (0.95, 0.72, 0.08), 0.6)
    m.lathe("pad_mark", (0, -39, 0.012), [(5.7, 0), (6.1, 0.0)], seg=48, caps=False)
    for a in range(0, 360, 45):
        r = math.radians(a)
        m.box("pad_mark", (math.cos(r) * 4.2, -39 + math.sin(r) * 4.2, 0.012), (0.18, 1.4, 0.01), rot=(0, 0, a))
    for yy in (-30, -34, -44, -48):
        for s in (-1, 1):
            m.box("glow_light", (s * 9.9, yy, 6.0), (0.08, 1.4, 1.4))
            m.box("glow_blue", (s * 9.4, yy, 0.05), (0.06, 2.0, 0.02))
    for k in range(5):
        m.box("glow_light_dim", (-6 + k * 3.0, -39, 9.95), (1.4, 5.0, 0.04))
    # Passarela lateral com guarda-corpo, cabine de controle e portões de carga
    m.box("steel", (-9.0, -38, 3.2), (1.8, 14, 0.12))
    _railing(m, (-8.1, -31.0, 0), (-8.1, -45.0, 0), 3.26, posts=7)
    for k in range(8):
        m.box("hull_dark_metal", (-9.7, -30.5 - k * 2.0, 1.6), (0.3, 0.2, 3.2))
    m.box("hull_metal", (8.6, -33, 4.6), (2.6, 4.0, 2.4), bevel=0.04)
    m.box("glass_win", (7.28, -33, 4.7), (0.04, 3.4, 1.2))
    m.box("glow_screen", (8.6, -33, 3.5), (2.2, 3.6, 0.04))
    for k, (x, y) in enumerate(((-7.4, -29.5), (-7.4, -31.0), (7.8, -46.0), (8.8, -46.0), (8.3, -44.5))):
        m.box("orange" if k % 2 == 0 else "panel", (x, y, 0.55), (1.2, 1.2, 1.1), bevel=0.03)
        m.box("steel", (x, y, 1.12), (1.24, 1.24, 0.05))
    m.cyl("panel", (7.6, -29.0, 1.6), 0.7, 0.7, 3.2, seg=16)
    m.cyl("steel", (7.6, -29.0, 3.25), 0.75, 0.75, 0.1, seg=16)
    for s in (-1, 1):
        m.tube("pipe", (s * 9.5, -27.5, 8.8), (s * 9.5, -50.5, 8.8), 0.14, seg=12)
        m.tube("steel", (s * 9.1, -27.5, 8.4), (s * 9.1, -50.5, 8.4), 0.07, seg=10)
    return m


def suit_locker():
    m = Model("suit_locker", "Station")
    m.greeble = False
    _st_mats(m)
    m.box("hull_metal", (0, 0, 1.15), (1.1, 0.7, 2.3), bevel=0.03)
    m.box("hull_dark_metal", (0, 0.36, 1.2), (0.9, 0.02, 2.1))
    m.box("glass_win", (0, 0.38, 1.2), (0.84, 0.02, 2.0))
    # Traje dentro: torso, cabeça (capacete) e braços
    m.box("suit", (0, 0.1, 1.3), (0.5, 0.26, 0.7), bevel=0.06)
    m.blob("suit", (0, 0.1, 1.9), (0.2, 0.2, 0.22), sub=2, noise=0.0, seed=1, smooth=True)
    m.blob("glow_blue", (0, 0.3, 1.9), (0.13, 0.03, 0.1), sub=2, noise=0.0, seed=1, smooth=True)
    for s in (-1, 1):
        m.tube("suit", (s * 0.3, 0.1, 1.55), (s * 0.34, 0.1, 0.9), 0.07, seg=8)
        m.tube("suit", (s * 0.14, 0.1, 0.95), (s * 0.14, 0.1, 0.1), 0.09, seg=8)
        m.box("orange", (s * 0.3, 0.1, 1.6), (0.16, 0.2, 0.1))
    m.box("glow_screen", (0, 0.37, 2.2), (0.5, 0.02, 0.06))
    m.box("yellow", (0, 0.37, 0.15), (0.8, 0.02, 0.1))
    return m


def lander():
    """Nave de descida (~9 m): fuselagem lisa, cabine de vidro, asas, dois motores, três pernas e rampa lateral aberta.
    Nariz para -Y do Blender (= +Z no Unity), origem no centro da base dos trens de pouso."""
    m = Model("lander", "Station")
    m.greeble = False
    _st_mats(m)
    m.material("lander_white", (0.86, 0.87, 0.88), 0.45, 0.2)
    B = T(0, 4.5, 1.75) @ R(90, 0, 0)
    prof = [(0.0, 0.0), (0.7, 0.2), (1.25, 1.2), (1.4, 3.0), (1.35, 6.2), (1.0, 8.0), (0.5, 9.0), (0.0, 9.2)]
    m.lathe("lander_white", (0, 0, 0), prof, seg=28, base=B)
    m.lathe("hull_dark_metal", (0, 0, 0), [(1.43, 2.0), (1.43, 2.35)], seg=28, caps=False, base=B)
    m.lathe("hull_dark_metal", (0, 0, 0), [(1.38, 5.7), (1.38, 6.0)], seg=28, caps=False, base=B)
    m.blob("glass_win", (0, -3.9, 2.05), (0.95, 1.6, 0.8), sub=3, noise=0.0, seed=2, smooth=True)       # cabine
    m.box("hull_dark_metal", (0, -3.9, 1.55), (1.8, 2.6, 0.12))
    for s in (-1, 1):
        # asas em delta
        m.poly("lander_white", [(s * 1.2, 1.5, 1.5), (s * 4.6, 3.8, 1.35), (s * 4.6, 4.5, 1.35), (s * 1.3, 4.0, 1.7),
                                (s * 1.2, 1.5, 1.25), (s * 4.6, 3.8, 1.25), (s * 4.6, 4.5, 1.25), (s * 1.3, 4.0, 1.3)],
               [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)])
        m.box("orange", (s * 4.4, 4.1, 1.38), (0.35, 0.9, 0.18))
        m.cyl("hull_dark_metal", (s * 1.9, 5.0, 1.4), 0.5, 0.45, 1.9, rot=(90, 0, 0), seg=18)
        m.cyl("glow_heat", (s * 1.9, 5.95, 1.4), 0.4, 0.4, 0.05, rot=(90, 0, 0), seg=18)
        m.cyl("steel", (s * 1.9, 5.9, 1.4), 0.52, 0.52, 0.12, rot=(90, 0, 0), seg=18)
        # trens de pouso
    for k, (x, y) in enumerate(((-1.7, -3.2), (1.7, -3.2), (0.0, 3.6))):
        m.tube("steel", (x * 0.4, y, 1.3), (x, y, 0.2), 0.1, seg=10)
        m.tube("hull_dark_metal", (x * 0.7, y, 0.7), (x, y, 0.25), 0.06, seg=8)
        m.cyl("hull_dark_metal", (x, y, 0.07), 0.45, 0.45, 0.14, seg=14)
    # Rampa lateral (lado +X) aberta e porta com luz interna
    m.box("hull_dark_metal", (1.3, 0.6, 1.75), (0.1, 2.4, 1.8), rot=(0, 0, 0))
    m.box("glow_light", (1.28, 0.6, 1.85), (0.06, 2.0, 1.5))
    m.box("steel", (1.6, 0.6, 0.75), (1.2, 2.2, 0.08), rot=(0, 28, 0))
    for yy in (-0.3, 0.6, 1.5):
        m.box("hull_dark_metal", (1.68, yy, 0.75), (1.1, 0.05, 0.02), rot=(0, 28, 0))
    # Detalhes: faixa de alerta, antena, números e luzes de posição
    hazard_strip(m, (0, -1.0, 2.7), 0.9, "x", height=0.1)
    m.tube("steel", (0, 2.5, 3.0), (0, 2.5, 3.9), 0.025, seg=6)
    m.blob("glow_red", (-4.6, 4.2, 1.45), (0.08, 0.08, 0.08), sub=1, noise=0, seed=1)
    m.blob("glow_screen", (4.6, 4.2, 1.45), (0.08, 0.08, 0.08), sub=1, noise=0, seed=1)
    m.box("orange", (0, -5.8, 1.45), (1.2, 0.05, 0.3))
    m.box("glow_blue", (0, -0.4, 3.12), (0.4, 1.2, 0.03))
    return m


def wrist_arm():
    """Antebraço esquerdo de traje espacial com o comunicador de pulso (primeira pessoa). Origem no centro do
    mostrador. O cotovelo fica em +Y (perto da câmera) e a mão aponta para -Y; o relógio fica em cima (+Z).
    A luva (NASA, importada à parte) encaixa no anel de vedação do punho, em y = 0,075, logo à frente do relógio."""
    m = Model("wrist_arm", "Station")
    m.greeble = False
    _st_mats(m)
    m.material("sleeve", (0.80, 0.81, 0.80), 0.82, 0.0)
    m.material("strap", (0.12, 0.13, 0.14), 0.7, 0.0)
    m.material("cuff_orange", (0.9, 0.38, 0.1), 0.6)
    # Manga de tecido em camadas: afina para o punho e tem dobras (sanfona) como os trajes reais.
    prof = [(0.0, 0.0)]
    for i in range(41):
        t = i / 40
        r = 0.071 + 0.03 * t ** 1.4 + 0.0022 * math.sin(t * 46) * (1 - t * 0.6)
        prof.append((r, t * 0.62))
    prof.append((0.0, 0.62))
    m.lathe("sleeve", (0, 0, 0), prof, seg=16, base=T(0, 0.08, 0) @ aim((0, 0, 0), (0, 1, 0)))
    m.tube("cuff_orange", (0, 0.40, 0.0), (0, 0.36, 0.0), 0.0905, seg=18)
    # Anel metálico de vedação da luva (wrist disconnect) na ponta da manga.
    m.tube("hull_dark_metal", (0, 0.105, 0.0), (0, 0.075, 0.0), 0.076, seg=20)
    m.tube("steel", (0, 0.092, 0.0), (0, 0.082, 0.0), 0.079, seg=20)
    # Comunicador: pulseira em volta da manga, caixa redonda compacta, aro luminoso, tela e projetor.
    m.tube("strap", (0, 0.155, 0.0), (0, 0.105, 0.0), 0.079, seg=20)
    m.cyl("hull_dark_metal", (0, 0.13, 0.096), 0.06, 0.056, 0.036, seg=20)
    m.lathe("glow_blue", (0, 0, 0), [(0.057, 0), (0.057, 0.006)], seg=26, caps=False, base=T(0, 0.13, 0.114))
    m.cyl("glow_screen", (0, 0.13, 0.117), 0.046, 0.046, 0.005, seg=20)
    for s_ in (-1, 1):
        m.cyl("steel", (s_ * 0.064, 0.13, 0.1), 0.009, 0.009, 0.016, rot=(0, 90, 0), seg=8)
    m.cyl("steel", (0, 0.13, 0.128), 0.016, 0.013, 0.022, seg=10)                   # lente do projetor
    m.blob("glow_core", (0, 0.13, 0.145), (0.009, 0.009, 0.009), sub=1, noise=0, seed=1)
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
    # Minerais cravados nas rochas escaneadas dos minérios (ver PrototypeBootstrap.Ore).
    "druse_silicon": lambda: druse("druse_silicon", (0.62, 0.72, 0.9), (0.8, 0.88, 1.0), seed=11),
    "druse_ice": lambda: druse("druse_ice", (0.82, 0.93, 1.0), (0.95, 0.98, 1.0), seed=12, max_r=0.06),
    "druse_uranium": lambda: druse("druse_uranium", (0.25, 0.85, 0.35), (0.55, 1.0, 0.6), glow=True, seed=13,
                                   count=26, max_len=0.22),
    "druse_magnesium": lambda: druse("druse_magnesium", (0.95, 0.95, 0.92), (1.0, 1.0, 1.0), seed=14, count=40,
                                     max_len=0.12, max_r=0.025),
    "nuggets_iron": lambda: nuggets("nuggets_iron", (0.42, 0.3, 0.26), 0.6, 0.45, seed=21),
    "nuggets_titanium": lambda: nuggets("nuggets_titanium", (0.8, 0.82, 0.86), 0.95, 0.2, seed=22, flat=True),
    "wreck_pod": wreck_pod, "chip_crate": chip_crate, "log_terminal": log_terminal,
    "ruin_bunker": ruin_bunker, "decontaminator": decontaminator,
    "mothership": mothership, "shipyard": shipyard, "station": station,
    **{f"heater_{t}": (lambda t=t: heater_tier(t)) for t in (1, 2, 3, 4)},
    **{f"drill_{t}": (lambda t=t: drill_tier(t)) for t in (1, 2, 3)},
    **{f"decon_{t}": (lambda t=t: decon_tier(t)) for t in (1, 2, 3)},
    **{n: (lambda n=n: kit_wall(n)) for n in ("base_wall", "base_wall_heavy", "base_wall_door", "base_wall_window", "base_airlock")},
    "base_foundation": lambda: kit_floor(True), "base_floor": lambda: kit_floor(False),
    "base_pillar": kit_pillar, "base_stairs": kit_stairs,
    **{n: (lambda n=n: furniture(n)) for n in ("base_bunk", "base_locker", "base_workbench", "base_kitchen", "base_medbay",
                                               "base_o2gen", "base_battery", "base_purifier")},
    **{n: (lambda n=n: defense(n)) for n in ("base_turret_1", "base_turret_2", "base_floodlight", "base_shock_fence")},
    "sentinel": sentinel,
    "station_interior": station_interior, "suit_locker": suit_locker, "lander": lander, "wrist_arm": wrist_arm,
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
