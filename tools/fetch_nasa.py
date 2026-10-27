"""Baixa as imagens da Terra (NASA Visible Earth, Blue Marble — domínio público) usadas no título e no epílogo.

Uso: python tools/fetch_nasa.py
Saída: Assets/ThirdParty/NASA/earth_4096.jpg (superfície) e clouds_2048.png (nuvens com transparência).
"""
import os
import urllib.request

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ThirdPartySource", "NASA")
DST = os.path.join(ROOT, "Assets", "ThirdParty", "NASA")
BASE = "https://eoimages.gsfc.nasa.gov/images/imagerecords/57000"
FILES = {
    "land_8192.tif": f"{BASE}/57752/land_shallow_topo_8192.tif",
    "clouds_2048.jpg": f"{BASE}/57747/cloud_combined_2048.jpg",
}


def main():
    os.makedirs(SRC, exist_ok=True)
    os.makedirs(DST, exist_ok=True)
    for name, url in FILES.items():
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            print("baixando", url)
            urllib.request.urlretrieve(url, path)

    Image.MAX_IMAGE_PIXELS = None
    land = Image.open(os.path.join(SRC, "land_8192.tif")).convert("RGB")
    land.resize((4096, 2048), Image.LANCZOS).save(os.path.join(DST, "earth_4096.jpg"), quality=92)

    # Nuvens: branco puro com a intensidade da foto como transparência.
    gray = Image.open(os.path.join(SRC, "clouds_2048.jpg")).convert("L")
    clouds = Image.new("RGBA", gray.size, (255, 255, 255, 0))
    # Gama na transparência: só as nuvens densas ficam opacas; a névoa fina não esconde os oceanos.
    clouds.putalpha(gray.point(lambda v: int(255 * (v / 255) ** 3.0)))
    clouds.save(os.path.join(DST, "clouds_2048.png"))
    print("ok:", os.listdir(DST))


if __name__ == "__main__":
    main()
