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

    def poly(self, mat, verts, faces, base=I4, obj=None):
        """Malha livre: lista de vértices e faces (índices). Normais recalculadas no build."""
        tmp = bmesh.new()
        vs = [tmp.verts.new(v) for v in verts]
        for f in faces:
            tmp.faces.new([vs[i] for i in f])
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


def wind_turbine():
    """Gerador eólico (~8 m): base de concreto com parafusos, torre cônica com porta e escada, nacele com
    radiador e para-raios, rotor de três pás com bordas vermelhas (Rotor gira no eixo da frente)."""
    m = Model("wind_turbine", "Machines")
    m.greeble = False
    m.material("white", (0.92, 0.93, 0.95), 0.4)
    m.material("grey", (0.45, 0.47, 0.5), 0.5, 0.5)
    m.material("dark", (0.14, 0.15, 0.16), 0.6, 0.3)
    m.material("concrete", (0.58, 0.57, 0.55), 0.9)
    m.material("red", (0.85, 0.15, 0.12), 0.5)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    m.lathe("concrete", (0, 0, 0), [(0, 0), (1.05, 0), (1.05, 0.18), (0.85, 0.3), (0, 0.3)], seg=24)
    bolt_ring(m, "steel", (0, 0, 0.32), 0.62, 12, size=0.04)
    m.lathe("grey", (0, 0, 0.3), [(0.55, 0), (0.55, 0.14), (0.47, 0.16)], seg=24, caps=False)
    m.lathe("white", (0, 0, 0.3), [(0.44, 0), (0.36, 3.4), (0.24, 6.85)], seg=24)
    for z in (2.4, 4.6):  # emendas dos segmentos da torre
        r = 0.44 - (0.44 - 0.24) * (z - 0.3) / 6.85
        m.lathe("grey", (0, 0, z), [(r + 0.012, -0.03), (r + 0.012, 0.03)], seg=24, caps=False)
    m.box("grey", (0, -0.43, 1.05), (0.42, 0.06, 1.3), bevel=0.02)
    m.box("dark", (0, -0.46, 1.05), (0.34, 0.02, 1.2))
    m.box("steel", (0.12, -0.47, 1.05), (0.03, 0.03, 0.12))
    for z in range(13):  # escada externa
        zz = 1.9 + z * 0.36
        r = 0.44 - (0.44 - 0.24) * (zz - 0.3) / 6.85
        m.box("steel", (0, r + 0.12, zz), (0.32, 0.03, 0.03))
    for sx in (-0.15, 0.15):
        m.tube("steel", (sx, 0.52, 1.9), (sx, 0.38, 6.3), 0.015, seg=5)
    # Nacele
    m.box("white", (0, 0.15, 7.35), (0.72, 1.9, 0.72), bevel=0.14)
    m.box("grey", (0, 0.15, 7.73), (0.5, 1.6, 0.06))
    for i in range(6):
        m.box("dark", (0.37, 0.75 - i * 0.12, 7.3), (0.02, 0.06, 0.45))
    m.box("grey", (0, 1.15, 7.35), (0.5, 0.18, 0.5), bevel=0.04)
    m.tube("steel", (0, 0.95, 7.75), (0, 0.95, 8.35), 0.015, seg=5)
    m.blob("red", (0, 0.95, 8.38), (0.04, 0.04, 0.04), sub=1, noise=0, seed=4)
    m.cyl("grey", (0, -0.85, 7.35), 0.32, 0.26, 0.2, seg=16, rot=(90, 0, 0))
    hub = (0, -1.05, 7.35)
    m.origins["Rotor"] = hub
    m.lathe("white", (0, 0, 0), [(0, -0.45), (0.12, -0.36), (0.26, -0.12), (0.3, 0.1), (0.3, 0.2)], seg=20,
            obj="Rotor", base=T(*hub) @ R(-90, 0, 0))
    for i in range(3):
        rot = R(0, i * 120, 0)
        m.cyl("grey", None, 0.13, 0.13, 0.3, seg=12, M=T(*hub) @ rot @ T(0, 0, 0.36), obj="Rotor")
        m.box("white", (0, 0, 0), (0.42, 0.07, 1.4), taper=(0.75, 1.0), obj="Rotor", base=T(*hub) @ rot @ T(0, 0, 1.2))
        m.box("white", (0, 0, 0), (0.32, 0.05, 2.1), taper=(0.35, 1.0), obj="Rotor", base=T(*hub) @ rot @ T(0, 0, 2.95))
        m.box("red", (0, 0, 0), (0.12, 0.055, 0.45), obj="Rotor", base=T(*hub) @ rot @ T(0, 0, 3.78))
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


def vegetube():
    """Vegetubo (~2.6 m): base técnica com bomba e mangueiras, cilindro de vidro com anéis de reforço e grade de
    luz de cultivo, planta em espiral com folhas, tampa com ventilação e cabo."""
    m = Model("vegetube", "Machines")
    m.greeble = False
    m.material("dark", (0.2, 0.21, 0.24), 0.55, 0.3)
    m.material("steel", (0.6, 0.62, 0.65), 0.35, 0.8)
    m.material("glass_tube", (0.65, 0.9, 0.8), 0.1)
    m.material("plant", (0.3, 0.7, 0.28), 0.8)
    m.material("plant_b", (0.45, 0.78, 0.3), 0.8)
    m.material("soil", (0.28, 0.2, 0.14), 0.95)
    m.material("glow_grow", (1.0, 0.35, 0.85), 0.4)
    m.lathe("dark", (0, 0, 0), [(0, 0), (0.78, 0), (0.78, 0.08), (0.7, 0.12), (0.7, 0.3), (0.6, 0.34), (0, 0.34)], seg=24)
    bolt_ring(m, "steel", (0, 0, 0.35), 0.64, 12, size=0.025)
    m.cyl("soil", (0, 0, 0.36), 0.5, 0.5, 0.05, seg=20)
    m.cyl("glass_tube", (0, 0, 1.35), 0.52, 0.52, 1.95, seg=24)
    for z in (0.42, 1.0, 1.7, 2.28):
        m.lathe("steel", (0, 0, z), [(0.53, -0.025), (0.55, 0), (0.53, 0.025)], seg=24, caps=False)
    for i in range(4):  # montantes verticais
        a = math.radians(45 + i * 90)
        m.box("steel", (math.cos(a) * 0.54, math.sin(a) * 0.54, 1.35), (0.04, 0.04, 1.95), rot=(0, 0, math.degrees(a)))
    for i in range(3):  # barras de luz de cultivo
        a = math.radians(i * 120)
        m.box("glow_grow", (math.cos(a) * 0.46, math.sin(a) * 0.46, 1.35), (0.02, 0.05, 1.6), rot=(0, 0, math.degrees(a)))
    m.lathe("dark", (0, 0, 2.32), [(0.58, 0), (0.58, 0.12), (0.4, 0.22), (0.2, 0.26), (0, 0.26)], seg=24)
    m.cyl("steel", (0, 0, 2.62), 0.12, 0.12, 0.08, seg=12)
    for i in range(6):
        m.box("dark", (0, 0, 2.67), (0.22, 0.02, 0.02), rot=(0, 0, i * 30))
    # Planta: caule em espiral e folhas
    prev = Vector((0, 0, 0.38))
    for i in range(1, 13):
        a = i * 0.9
        p = Vector((math.cos(a) * 0.08, math.sin(a) * 0.08, 0.38 + i * 0.13))
        m.tube("plant", prev, p, 0.035 - i * 0.002, seg=6)
        prev = p
        if i % 2 == 0:
            la = a + 1.2
            m.blob("plant" if i % 4 else "plant_b", (math.cos(la) * 0.22, math.sin(la) * 0.22, p.z),
                   (0.2, 0.08, 0.025), sub=1, noise=0.08, seed=i, rot=(0, -15, math.degrees(la)))
    m.blob("plant_b", (0, 0, 2.0), (0.16, 0.16, 0.12), sub=2, noise=0.15, seed=11)
    # Bomba lateral, mangueiras e cabo
    m.box("dark", (0.72, 0.0, 0.2), (0.3, 0.36, 0.4), bevel=0.03)
    m.cyl("steel", (0.72, 0.0, 0.43), 0.08, 0.08, 0.06, seg=10)
    m.tube("steel", (0.72, 0.1, 0.35), (0.55, 0.1, 2.35), 0.025, seg=6)
    m.tube("steel", (0.72, -0.1, 0.35), (0.55, -0.1, 0.5), 0.025, seg=6)
    cable(m, "dark", (0.86, 0, 0.15), (1.3, 0.3, 0.02), sag=0.05, r=0.02, segments=4)
    indicator(m, (0, -0.72, 0.28))
    return m


def solar_panel():
    """Painel solar (~2.6 x 1.6 m): pedestal com sapata e parafusos, motor de rastreamento, braço articulado,
    moldura de alumínio com células em grade 6 x 4, caixa de junção e cabo."""
    m = Model("solar_panel", "Machines")
    m.greeble = False
    m.material("metal", (0.65, 0.67, 0.7), 0.4, 0.7)
    m.material("dark", (0.14, 0.15, 0.16), 0.6, 0.3)
    m.material("cell", (0.06, 0.1, 0.3), 0.2, 0.4)
    m.material("grid", (0.75, 0.77, 0.8), 0.35, 0.7)
    m.material("concrete", (0.58, 0.57, 0.55), 0.9)
    m.box("concrete", (0, 0, 0.1), (0.9, 0.9, 0.2), bevel=0.03)
    bolt_ring(m, "metal", (0, 0, 0.21), 0.28, 8, size=0.025)
    m.cyl("metal", (0, 0, 0.23), 0.22, 0.22, 0.06, seg=16)
    m.cyl("metal", (0, 0, 0.85), 0.09, 0.11, 1.25, seg=14)
    m.box("dark", (0, 0, 1.52), (0.36, 0.3, 0.26), bevel=0.03)
    m.cyl("metal", (0, 0, 1.68), 0.07, 0.07, 0.12, seg=10, rot=(0, 90, 0))
    tilt = T(0, 0, 1.78) @ R(-28, 0, 0)
    m.box("metal", (0, 0, -0.06), (0.16, 1.1, 0.08), base=tilt)
    for x in (-0.9, 0.9):
        m.box("metal", (x, 0, -0.06), (0.06, 1.3, 0.06), base=tilt)
    m.box("metal", (0, 0, -0.06), (2.0, 0.08, 0.06), base=tilt)
    m.box("metal", (0, 0, 0), (2.56, 1.56, 0.06), bevel=0.015, base=tilt)
    cols, rows = 6, 4
    cw, ch = 2.44 / cols, 1.44 / rows
    for i in range(cols):
        for j in range(rows):
            m.box("cell", (-1.22 + (i + 0.5) * cw, -0.72 + (j + 0.5) * ch, 0.04), (cw - 0.025, ch - 0.025, 0.02),
                  base=tilt)
    for i in range(cols):
        m.box("grid", (-1.22 + (i + 0.5) * cw, 0, 0.052), (0.008, 1.44, 0.004), base=tilt)
    m.box("dark", (0.6, 0.3, -0.07), (0.22, 0.16, 0.06), base=tilt)
    cable(m, "dark", (0.08, 0.08, 1.45), (0.3, 0.6, 0.05), sag=0.15, r=0.015)
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
    "ruin_bunker": ruin_bunker, "decontaminator": decontaminator,
    "mothership": mothership, "shipyard": shipyard, "station": station,
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
