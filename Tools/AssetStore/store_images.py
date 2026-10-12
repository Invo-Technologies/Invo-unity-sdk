"""Asset Store marketing images for Invo SDK: icon, card, cover, social and captioned screenshots."""
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SHOTS, FONTS, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
BG = (11, 14, 24)
PURPLE_TOP, PURPLE_BOTTOM = (100, 86, 255), (139, 61, 250)
WHITE, MUTED = (240, 242, 248), (160, 168, 190)


def font(weight, size):
    return ImageFont.truetype(os.path.join(FONTS, f"Inter-{weight}.ttf"), size)


def gradient(w, h, top, bottom):
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return im


def logo(size, color=(255, 255, 255)):
    """The INVO 'iv' mark, redrawn from the 86 px original at any size (transparent background)."""
    k = size / 86.0
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    P = lambda pts: [(x * k, y * k) for x, y in pts]
    d.rectangle(P([(21, 15), (30, 24.5)]), fill=color)
    d.polygon(P([(21, 31), (30, 31), (30, 63.5), (21, 72.5)]), fill=color)
    d.polygon(P([(37, 31), (45, 31), (45, 40), (54, 31), (64.5, 31), (37, 58.5)]), fill=color)
    return im


def tile(size, radius_frac=0.22):
    g = gradient(size, size, PURPLE_TOP, PURPLE_BOTTOM).convert("RGBA")
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), int(size * radius_frac), fill=255)
    g.putalpha(mask)
    mark = logo(int(size * 0.86))
    off = (size - mark.width) // 2
    g.alpha_composite(mark, (off + int(size * 0.02), off))
    return g


def glow(im, center, radius, color, alpha=110):
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    cx, cy = center
    ImageDraw.Draw(layer).ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=color + (alpha,))
    return Image.alpha_composite(im, layer.filter(ImageFilter.GaussianBlur(radius // 2)))


def canvas(w, h):
    im = Image.new("RGBA", (w, h), BG + (255,))
    im = glow(im, (int(w * 0.85), int(h * 0.2)), int(min(w, h) * 0.55), PURPLE_BOTTOM, 70)
    return glow(im, (int(w * 0.1), int(h * 0.95)), int(min(w, h) * 0.45), PURPLE_TOP, 50)


def sheet(name, crop=(668, 140, 1252, 940)):
    """The SDK window from a 1920x1080 capture."""
    im = Image.open(os.path.join(SHOTS, name + ".png")).convert("RGBA").crop(crop)
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width - 1, im.height - 1), 22, fill=255)
    im.putalpha(mask)
    return im


def paste_shadowed(base, im, xy, blur=30):
    sh = Image.new("RGBA", base.size, (0, 0, 0, 0))
    m = Image.new("RGBA", im.size, (0, 0, 0, 170))
    m.putalpha(im.getchannel("A").point(lambda a: int(a * 0.7)))
    sh.alpha_composite(m, (xy[0] + 10, xy[1] + 24))
    base.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur)))
    base.alpha_composite(im, xy)


def text_block(d, x, y, lines):
    for txt, f, color, gap in lines:
        d.text((x, y), txt, font=f, fill=color)
        y += f.size + gap
    return y


def save(im, name):
    im.convert("RGB").save(os.path.join(OUT, name), optimize=True)
    print(name, im.size)


# Icon 160x160
icon = Image.new("RGBA", (160, 160), BG + (255,))
icon.alpha_composite(tile(160, 0.0))
save(icon, "icon-160x160.png")

# Card 420x280
card = canvas(420, 280)
card.alpha_composite(tile(96), (28, 34))
d = ImageDraw.Draw(card)
d.text((140, 46), "Invo SDK", font=font("Bold", 38), fill=WHITE)
d.text((141, 94), "for Unity", font=font("Medium", 20), fill=MUTED)
text_block(d, 28, 162, [("In-game currency: store, sends,", font("SemiBold", 19), WHITE, 6),
                        ("transfers and phone approval.", font("SemiBold", 19), WHITE, 14),
                        ("Unity 2021.3+  ·  Free", font("Medium", 15), MUTED, 0)])
save(card, "card-420x280.png")

# Cover 1950x1300
cover = canvas(1950, 1300)
d = ImageDraw.Draw(cover)
cover.alpha_composite(tile(150), (110, 120))
y = text_block(d, 110, 320, [("Invo SDK", font("Bold", 120), WHITE, 18),
                             ("for Unity", font("Medium", 54), MUTED, 60)])
text_block(d, 110, y, [("Real-money top ups, item store,", font("SemiBold", 44), WHITE, 14),
                       ("cross-game sends and transfers,", font("SemiBold", 44), WHITE, 14),
                       ("approved on the player’s phone.", font("SemiBold", 44), WHITE, 60),
                       ("Drop-in themeable UI  ·  PC, mobile, consoles, WebGL", font("Medium", 32), MUTED, 16),
                       ("Unity 2021.3 LTS and newer  ·  Free", font("Medium", 32), MUTED, 0)])
for name, xy, scale in [("05-send-1-choose-game", (1020, 300), 0.92), ("01-store-top-up", (1300, 170), 0.98)]:
    s = sheet(name)
    s = s.resize((int(s.width * scale), int(s.height * scale)), Image.LANCZOS)
    paste_shadowed(cover, s, xy)
save(cover, "cover-1950x1300.png")

# Social 1200x630
social = canvas(1200, 630)
d = ImageDraw.Draw(social)
social.alpha_composite(tile(110), (70, 70))
text_block(d, 70, 215, [("Invo SDK for Unity", font("Bold", 64), WHITE, 22),
                        ("In-game currency, approved on the", font("SemiBold", 32), WHITE, 10),
                        ("player’s phone. Free on the Asset Store.", font("SemiBold", 32), WHITE, 0)])
s = sheet("09-send-3-approve-qr")
s = s.resize((int(s.width * 0.62), int(s.height * 0.62)), Image.LANCZOS)
paste_shadowed(social, s, (790, 60))
save(social, "social-1200x630.png")

# Screenshots 1920x1080: caption on the left, the window on the right.
SHOTS_LIST = [
    ("01-store-top-up", "Top up with real money", ["Currency packs through INVO’s hosted", "checkout, with a live balance."]),
    ("02-store-items", "Spend the balance on items", ["Your catalog and your art. Purchases", "carry an idempotency key, so retries", "never charge twice."]),
    ("05-send-1-choose-game", "Send to a friend in any game", ["Recent recipients, and a game search", "that scales from five titles to hundreds."]),
    ("09-send-3-approve-qr", "Approved on the player’s phone", ["A QR on PC and consoles; phones open", "INVO’s page. No SMS codes to type."]),
    ("13-transfer-1-details", "Move currency between games", ["Players move their own balance to", "another game on the network."]),
    ("17-collect", "Collect what was sent to you", ["Everything waiting for the player,", "each collected with one approval."]),
    ("19-phone-change", "Players change their own number", ["Secured accounts confirm through", "INVO’s emailed link."]),
    ("23-theme-light", "Restyle it from one theme asset", ["Colours, font, radius and spacing in", "one InvoTheme. Dark and light included."]),
]
for i, (name, title, body) in enumerate(SHOTS_LIST, 1):
    im = canvas(1920, 1080)
    d = ImageDraw.Draw(im)
    im.alpha_composite(tile(72), (130, 300))
    y = text_block(d, 130, 410, [(title, font("Bold", 58), WHITE, 34)])
    text_block(d, 130, y, [(line, font("Medium", 34), MUTED, 14) for line in body])
    s = sheet(name)
    s = s.resize((int(s.width * 1.12), int(s.height * 1.12)), Image.LANCZOS)
    paste_shadowed(im, s, (1920 - s.width - 150, (1080 - s.height) // 2))
    save(im, f"screenshot-{i:02d}-{name}.png")

# Phone layout.
im = canvas(1920, 1080)
d = ImageDraw.Draw(im)
im.alpha_composite(tile(72), (130, 300))
y = text_block(d, 130, 410, [("PC, phones, consoles, WebGL", font("Bold", 58), WHITE, 34)])
text_block(d, 130, y, [(l, font("Medium", 34), MUTED, 14) for l in
                       ["Portrait layout, safe areas, phone keypads,", "and gamepad and keyboard navigation."]])
phone = Image.open(os.path.join(SHOTS, "04-send-1-details-1080x1920.png")).convert("RGBA")
phone = phone.resize((int(phone.width * 0.43), int(phone.height * 0.43)), Image.LANCZOS)
frame = Image.new("RGBA", (phone.width + 28, phone.height + 28), (0, 0, 0, 0))
ImageDraw.Draw(frame).rounded_rectangle((0, 0, frame.width - 1, frame.height - 1), 54, fill=(30, 33, 44, 255))
pm = Image.new("L", phone.size, 0)
ImageDraw.Draw(pm).rounded_rectangle((0, 0, phone.width - 1, phone.height - 1), 42, fill=255)
phone.putalpha(pm)
frame.alpha_composite(phone, (14, 14))
paste_shadowed(im, frame, (1920 - frame.width - 170, (1080 - frame.height) // 2))
save(im, "screenshot-09-mobile.png")
