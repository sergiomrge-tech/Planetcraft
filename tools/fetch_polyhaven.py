"""Baixa assets CC0 do Poly Haven (api.polyhaven.com) para Assets/ThirdParty/PolyHaven e atualiza CREDITS.md.

Uso: python tools/fetch_polyhaven.py            (baixa o que faltar da lista abaixo)
Licença de todos os assets: CC0 (domínio público) — uso comercial liberado, crédito opcional (damos mesmo assim).
"""
import json
import os
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEST = os.path.join(ROOT, "Assets", "ThirdParty", "PolyHaven")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "ReturnToEarth-dev/1.0"}

# Texturas de terreno: só difusa + normal (OpenGL, a convenção do Unity).
TEXTURES = ["dry_ground_rocks", "rocky_terrain_02", "leafy_grass"]
TEXTURE_RES = "2k"

# Modelos FBX com as texturas incluídas.
MODELS = [
    "boulder_01", "namaqualand_boulder_02", "namaqualand_boulder_05",
    "dead_tree_trunk", "dead_tree_trunk_02", "tree_stump_01",
    "jacaranda_tree", "tree_small_02",
    "shrub_02", "fern_02",
    # Minérios (rochas fotoescaneadas como base) e props de máquinas/ruínas.
    "moon_rock_01", "moon_rock_03", "moon_rock_05", "rock_07", "stone_01",
    "barrel_stove", "drill_press_01", "metal_tool_chest", "ammo_box",
    "vintage_spacecraft_instrument", "old_military_compressor", "industrial_pastic_container", "portable_generator",
    # Peças escaneadas montadas nos modelos próprios (base, reator, turbina, estaleiro, ruínas).
    "exterior_aircon_unit", "power_box_01", "security_light", "small_lpg_tank", "utility_box_01", "utility_box_02",
    "portable_welding_cart", "tool_cart", "concrete_road_barrier_02", "old_tyre", "rusted_wheel_rim_01",
    "metal_trash_can", "Barrel_01",
]
# Superfícies das máquinas (baixadas e preparadas por tools/fetch_surfaces.py; aqui só para os créditos).
SURFACES = ["metal_plate_02", "cracked_concrete_wall", "rusty_metal_02", "dark_rock"]
MODEL_RES = "1k"


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.load(r)


def download(url, path):
    if os.path.exists(path):
        return False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r, open(path, "wb") as f:
        f.write(r.read())
    return True


def fetch_texture(asset_id):
    files = get_json(f"{API}/files/{asset_id}")
    out = os.path.join(DEST, "Textures", asset_id)
    for key, suffix in (("Diffuse", "diff"), ("nor_gl", "nor_gl")):
        entry = files[key][TEXTURE_RES]["jpg"]
        download(entry["url"], os.path.join(out, f"{asset_id}_{suffix}_{TEXTURE_RES}.jpg"))


def fetch_model(asset_id):
    files = get_json(f"{API}/files/{asset_id}")
    entry = files["fbx"][MODEL_RES]["fbx"]
    out = os.path.join(ROOT, "ThirdPartySource", "PolyHaven", "Models", asset_id)
    download(entry["url"], os.path.join(out, f"{asset_id}_{MODEL_RES}.fbx"))
    for rel, inc in entry.get("include", {}).items():
        download(inc["url"], os.path.join(out, rel.replace("/", os.sep)))


def write_credits(assets):
    lines = [
        "# Créditos",
        "",
        "## Poly Haven (CC0)",
        "",
        "Assets de [Poly Haven](https://polyhaven.com), licença CC0 (domínio público).",
        "",
    ]
    for kind, ids in assets:
        for asset_id in ids:
            info = get_json(f"{API}/info/{asset_id}")
            authors = ", ".join(info.get("authors", {}).keys())
            lines.append(f"- {kind}: [{info.get('name', asset_id)}](https://polyhaven.com/a/{asset_id}) — {authors}")
    # Mantém as seções de outras fontes (ex.: NASA) que vêm depois da do Poly Haven.
    path = os.path.join(ROOT, "CREDITS.md")
    others = ""
    if os.path.exists(path):
        old = open(path, encoding="utf-8").read()
        cut = old.find("\n## ", old.find("## Poly Haven") + 1)
        others = old[cut:] if cut >= 0 else ""
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n" + others)


def main():
    for asset_id in TEXTURES:
        print("textura", asset_id); sys.stdout.flush()
        fetch_texture(asset_id)
    for asset_id in MODELS:
        print("modelo", asset_id); sys.stdout.flush()
        fetch_model(asset_id)
    write_credits([("Textura", TEXTURES), ("Modelo", MODELS), ("Superfície", SURFACES)])
    print("ok")


if __name__ == "__main__":
    main()
