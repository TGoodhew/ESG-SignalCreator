"""
Generate the ESG-SignalCreator app/installer icon (multi-resolution .ico).

Motif (abstracted from the Agilent/Keysight E4438C front panel):
  - a warm-grey instrument front panel (rounded square)
  - a large dark-green LCD showing two phase-offset I/Q sine waves (green = I, cyan = Q)
  - the big RPG knob on the right, with an indicator
  - a small red brand accent (a nod to the Keysight logo)

Rendered at 4x then downsampled per icon size for crisp anti-aliasing.

Usage (from the repo root, needs Pillow):
    python tools/make_icon.py
    python tools/make_icon.py <out.ico> <preview.png>
Defaults write the icon next to the app project and a 256px preview into tools/.
"""
import math
import os
import sys
from PIL import Image, ImageDraw

S = 4               # supersample factor
N = 256 * S         # master canvas


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def vgrad(size, top, bot):
    """Vertical gradient image."""
    g = Image.new("RGBA", (1, size[1]))
    for y in range(size[1]):
        g.putpixel((0, y), lerp(top, bot, y / max(1, size[1] - 1)) + (255,))
    return g.resize(size)


def rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=255)
    return m


def render():
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # panel (instrument body)
    pm, prad = 10 * S, 46 * S
    panel_box = [pm, pm, N - pm, N - pm]
    panel = vgrad((N - 2 * pm, N - 2 * pm), (0xEC, 0xEA, 0xE3), (0xC4, 0xC2, 0xB8))
    img.paste(panel, (pm, pm), rounded_mask(panel.size, prad))
    draw.rounded_rectangle(panel_box, radius=prad, outline=(0x8C, 0x8A, 0x80, 255), width=2 * S)
    draw.rounded_rectangle([pm + 3 * S, pm + 3 * S, N - pm - 3 * S, N - pm - 3 * S],
                           radius=prad - 3 * S, outline=(255, 255, 255, 120), width=S)

    # red brand accent (top-left)
    draw.rounded_rectangle([28 * S, 30 * S, 64 * S, 46 * S], radius=4 * S, fill=(0xD4, 0x1A, 0x2C, 255))

    # LCD screen
    lcd = [30 * S, 64 * S, 150 * S, 196 * S]
    bezel = 4 * S
    draw.rounded_rectangle([lcd[0] - bezel, lcd[1] - bezel, lcd[2] + bezel, lcd[3] + bezel],
                           radius=10 * S, fill=(0x33, 0x33, 0x30, 255))
    draw.rounded_rectangle(lcd, radius=6 * S, fill=(0x06, 0x2A, 0x16, 255))

    gx0, gy0, gx1, gy1 = lcd
    for i in range(1, 5):
        x = gx0 + (gx1 - gx0) * i / 5
        draw.line([(x, gy0 + 3 * S), (x, gy1 - 3 * S)], fill=(0x1C, 0x55, 0x32, 255), width=max(1, S // 2))
    for i in range(1, 4):
        y = gy0 + (gy1 - gy0) * i / 4
        draw.line([(gx0 + 3 * S, y), (gx1 - 3 * S, y)], fill=(0x1C, 0x55, 0x32, 255), width=max(1, S // 2))

    cy = (gy0 + gy1) / 2
    amp = (gy1 - gy0) * 0.30
    x_lo, x_hi = gx0 + 7 * S, gx1 - 7 * S
    cycles = 2.0

    def sine_pts(phase):
        pts = []
        steps = 240
        for k in range(steps + 1):
            t = k / steps
            x = x_lo + (x_hi - x_lo) * t
            y = cy - amp * math.sin(2 * math.pi * cycles * t + phase)
            pts.append((x, y))
        return pts

    draw.line(sine_pts(0.0), fill=(0x35, 0xE6, 0x7A, 255), width=4 * S, joint="curve")          # I
    draw.line(sine_pts(math.pi / 2), fill=(0x4F, 0xD8, 0xE8, 255), width=4 * S, joint="curve")  # Q

    # RPG knob (right)
    kc, kr = (196 * S, 130 * S), 36 * S
    draw.ellipse([kc[0] - kr, kc[1] - kr, kc[0] + kr, kc[1] + kr], fill=(0x53, 0x53, 0x57, 255))
    draw.ellipse([kc[0] - kr, kc[1] - kr, kc[0] + kr, kc[1] + kr], outline=(0x7A, 0x7A, 0x7E, 255), width=2 * S)
    ir = kr - 9 * S
    cap = vgrad((2 * ir, 2 * ir), (0x40, 0x40, 0x44), (0x24, 0x24, 0x27))
    img.paste(cap, (kc[0] - ir, kc[1] - ir), rounded_mask((2 * ir, 2 * ir), ir))
    ang = math.radians(-118)
    ix = kc[0] + math.cos(ang) * (ir - 4 * S)
    iy = kc[1] + math.sin(ang) * (ir - 4 * S)
    draw.line([(kc[0], kc[1]), (ix, iy)], fill=(0xEC, 0xEC, 0xEC, 255), width=5 * S)

    # softkey buttons (far right)
    bx0, bx1 = 224 * S, 232 * S
    for i in range(4):
        by = (74 + i * 26) * S
        draw.rounded_rectangle([bx0, by, bx1, by + 16 * S], radius=2 * S, fill=(0xA8, 0xA6, 0x9C, 255))

    return img


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(here)
    out_ico = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        root, "ESG-SignalCreator.App", "ESG-SignalCreator.ico")
    out_png = sys.argv[2] if len(sys.argv) > 2 else os.path.join(here, "icon-preview.png")

    img = render()
    sizes = [256, 128, 64, 48, 32, 16]
    frames = [img.resize((s, s), Image.LANCZOS) for s in sizes]
    frames[0].save(out_ico, format="ICO", sizes=[(s, s) for s in sizes])
    img.resize((256, 256), Image.LANCZOS).save(out_png)
    print("wrote", out_ico, "and", out_png)


if __name__ == "__main__":
    main()
