"""Publisher profile images: a square logo and a wide promo banner, reusing store_images.py's helpers."""
import os, sys

here = os.path.dirname(os.path.abspath(__file__))
SHOTS, FONTS, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
src = open(os.path.join(here, "store_images.py"), encoding="utf-8").read().split("# Icon 160x160")[0]
sys.argv = ["store_images.py", SHOTS, FONTS, OUT]
exec(compile(src, "store_images.py", "exec"))

pic = Image.new("RGBA", (512, 512), BG + (255,))
pic.alpha_composite(tile(512, 0.0))
save(pic, "profile-512x512.png")

# Promo banner, wide.
W, H = 2400, 800
banner = canvas(W, H)
d = ImageDraw.Draw(banner)
banner.alpha_composite(tile(140), (140, 150))
text_block(d, 140, 330, [("Invo Network", font("Bold", 96), WHITE, 26),
                         ("One currency layer for every game: top ups, items,", font("SemiBold", 40), WHITE, 12),
                         ("cross-game sends and transfers, approved on the player’s phone.", font("SemiBold", 40), WHITE, 0)])
for name, xy, scale in [("05-send-1-choose-game", (1640, 120), 0.78), ("09-send-3-approve-qr", (1900, 60), 0.84)]:
    s = sheet(name)
    s = s.resize((int(s.width * scale), int(s.height * scale)), Image.LANCZOS)
    paste_shadowed(banner, s, xy)
save(banner, "promo-banner-2400x800.png")
