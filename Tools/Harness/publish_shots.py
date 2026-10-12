"""Copy matrix screenshots into Docs/screenshots/<unity version>/ (each under ~500 KB) and build
side-by-side comparisons of key screens in Docs/screenshots/compare/.

Usage: uv run --with pillow python -I publish_shots.py <matrix dir> <repo dir>
The matrix dir holds u2021t/, u2022/ and u6000/ from run-matrix.sh."""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROWS = [("u2021t", "2021.3"), ("u2022", "2022.3"), ("u6000", "6000.0")]
KEY_SCREENS = ["01-store-top-up", "04-send-1-details", "05-send-1-choose-game",
               "09-send-3-approve-qr", "17-collect", "19-phone-change"]
MAX_BYTES = 500 * 1024


def save_small(im, path):
    """PNG, quantized and downscaled until it fits MAX_BYTES."""
    scale = 1.0
    while True:
        out = im if scale == 1.0 else im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
        out.convert("RGB").quantize(colors=256, method=Image.MEDIANCUT).save(path, optimize=True)
        if os.path.getsize(path) <= MAX_BYTES or scale < 0.3:
            return
        scale *= 0.85


def main():
    matrix, repo = sys.argv[1], sys.argv[2]
    docs = os.path.join(repo, "Docs", "screenshots")
    found = {}
    for folder, version in ROWS:
        src = os.path.join(matrix, folder, "shots")
        if not os.path.isdir(src):
            print("missing", src)
            continue
        dst = os.path.join(docs, version)
        os.makedirs(dst, exist_ok=True)
        for name in sorted(os.listdir(src)):
            if name.endswith(".png"):
                save_small(Image.open(os.path.join(src, name)), os.path.join(dst, name))
        found[version] = src
        print(version, len(os.listdir(dst)), "files")

    compare = os.path.join(docs, "compare")
    os.makedirs(compare, exist_ok=True)
    try:
        font = ImageFont.truetype("arial.ttf", 28)
    except OSError:
        font = ImageFont.load_default()
    for screen in KEY_SCREENS:
        tiles = []
        for _, version in ROWS:
            path = os.path.join(found.get(version, ""), screen + ".png")
            if os.path.exists(path):
                # The SDK sheet sits in the middle of a 1920x1080 shot.
                tiles.append((version, Image.open(path).convert("RGB").crop((640, 130, 1280, 950))))
        if not tiles:
            continue
        w, h = tiles[0][1].size
        sheet = Image.new("RGB", (w * len(tiles) + 20 * (len(tiles) - 1), h + 50), (24, 26, 32))
        draw = ImageDraw.Draw(sheet)
        for i, (version, tile) in enumerate(tiles):
            x = i * (w + 20)
            sheet.paste(tile, (x, 50))
            draw.text((x + 10, 10), "Unity " + version, fill=(230, 233, 240), font=font)
        save_small(sheet, os.path.join(compare, screen + ".png"))
        print("compare", screen, len(tiles), "versions")


if __name__ == "__main__":
    main()
