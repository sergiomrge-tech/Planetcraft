"""Baixa texturas PBR do Poly Haven (CC0) e prepara as superfícies das máquinas e props gerados por gen_models.py.

Uso: python tools/fetch_surfaces.py
Saída: Assets/ThirdParty/PolyHaven/Surfaces/<família>/{albedo,normal,mask}.png
- albedo: tons de cinza claros (a cor de cada peça vem do material do Blender e multiplica a textura);
  `keep_color` mantém a cor original (ferrugem).
- mask: máscara do HDRP (R metal, G oclusão, B detalhe, A lisura).
"""
import io
import json
import os
import urllib.request

from PIL import Image, ImageOps, ImageStat

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ThirdPartySource", "PolyHaven", "Surfaces")
DST = os.path.join(ROOT, "Assets", "ThirdParty", "PolyHaven", "Surfaces")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "ReturnToEarth-build/1.0"}
RES = "2k"

# família: (id do Poly Haven, mantém a cor do albedo?)
FAMILIES = {
    "panel": ("metal_plate_02", False),
    "concrete": ("concrete_floor_worn_001", False),
    "rust": ("rusty_metal_02", True),
}
MAPS = {"Diffuse": "diff", "nor_gl": "nor_gl", "Rough": "rough", "AO": "ao", "Metal": "metal"}


def fetch(url, path):
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r, open(path, "wb") as f:
            f.write(r.read())
    return Image.open(path)


def main():
    credits = []
    for family, (asset_id, keep_color) in FAMILIES.items():
        with urllib.request.urlopen(urllib.request.Request(f"{API}/files/{asset_id}", headers=UA)) as r:
            files = json.load(r)
        maps = {}
        for key, short in MAPS.items():
            if key not in files:
                continue
            entry = files[key][RES]
            fmt = "png" if "png" in entry else "jpg"
            maps[short] = fetch(entry[fmt]["url"], os.path.join(SRC, asset_id, f"{asset_id}_{short}_{RES}.{fmt}"))
        out = os.path.join(DST, family)
        os.makedirs(out, exist_ok=True)

        albedo = maps["diff"].convert("RGB")
        if not keep_color:
            # Cinza claro com toda a variação do desgaste: média em ~0.77.
            gray = ImageOps.grayscale(albedo)
            mean = ImageStat.Stat(gray).mean[0]
            gray = gray.point(lambda v: max(0, min(255, int(196 + (v - mean) * 1.0))))
            albedo = Image.merge("RGB", (gray, gray, gray))
        albedo.save(os.path.join(out, "albedo.png"))
        maps["nor_gl"].convert("RGB").save(os.path.join(out, "normal.png"))

        size = albedo.size
        metal = maps["metal"].convert("L").resize(size) if "metal" in maps else Image.new("L", size, 0)
        ao = maps["ao"].convert("L").resize(size) if "ao" in maps else Image.new("L", size, 255)
        smooth = ImageOps.invert(maps["rough"].convert("L").resize(size))
        Image.merge("RGBA", (metal, ao, Image.new("L", size, 0), smooth)).save(os.path.join(out, "mask.png"))

        with urllib.request.urlopen(urllib.request.Request(f"{API}/info/{asset_id}", headers=UA)) as r:
            info = json.load(r)
        credits.append(f"- Superfície ({family}): [{info.get('name', asset_id)}](https://polyhaven.com/a/{asset_id}) — "
                       + ", ".join(info.get("authors", {}).keys()))
        print("ok", family, asset_id)
    print("\n".join(credits))


if __name__ == "__main__":
    main()
