#!/usr/bin/env python3
"""Generate the Cloudy iOS app icon as a deterministic 1024px PNG."""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "FriendMapSeed" / "FriendMapSeed" / "Assets.xcassets" / "AppIcon.appiconset" / "cloudy-app-icon.png"
LEGACY_OUT = ROOT / "FriendMapSeed" / "FriendMapSeed" / "cloudy.icon" / "Assets" / "IMG_1966.png"


def rounded_rect_mask(size: int, radius: int) -> Image.Image:
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, size, size), radius=radius, fill=255)
    return mask


def vertical_gradient(size: int, top: tuple[int, int, int], bottom: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        t = y / (size - 1)
        r = round(top[0] * (1 - t) + bottom[0] * t)
        g = round(top[1] * (1 - t) + bottom[1] * t)
        b = round(top[2] * (1 - t) + bottom[2] * t)
        for x in range(size):
            px[x, y] = (r, g, b)
    return img


def add_ellipse(draw: ImageDraw.ImageDraw, cx: int, cy: int, rx: int, ry: int, fill, outline=None, width=1) -> None:
    draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=fill, outline=outline, width=width)


def star_points(cx: float, cy: float, outer: float, inner: float, points: int = 5) -> list[tuple[float, float]]:
    coords: list[tuple[float, float]] = []
    start = -math.pi / 2
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        a = start + i * math.pi / points
        coords.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return coords


def generate(size: int = 1024) -> Image.Image:
    scale = 3
    canvas_size = size * scale
    img = vertical_gradient(canvas_size, (86, 210, 238), (30, 132, 223)).convert("RGBA")
    draw = ImageDraw.Draw(img, "RGBA")

    # Soft radial glow behind the mark.
    glow = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow, "RGBA")
    for i in range(42, 0, -1):
        alpha = int(4.0 * i)
        radius = int(canvas_size * (0.13 + i * 0.009))
        glow_draw.ellipse(
            (
                canvas_size // 2 - radius,
                int(canvas_size * 0.42) - radius,
                canvas_size // 2 + radius,
                int(canvas_size * 0.42) + radius,
            ),
            fill=(255, 255, 255, alpha),
        )
    img.alpha_composite(glow)
    draw = ImageDraw.Draw(img, "RGBA")

    def s(v: float) -> int:
        return round(v * scale)

    # Main cloud shadow.
    shadow = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow, "RGBA")
    cloud_parts = [
        (512, 465, 310, 170),
        (330, 475, 165, 138),
        (475, 370, 185, 175),
        (655, 395, 170, 150),
        (760, 480, 140, 130),
    ]
    for cx, cy, rx, ry in cloud_parts:
        add_ellipse(shadow_draw, s(cx), s(cy + 26), s(rx), s(ry), fill=(13, 63, 112, 78))
    shadow = shadow.filter(ImageFilter.GaussianBlur(s(18)))
    img.alpha_composite(shadow)

    # Cloud body.
    cloud_outline = (37, 129, 184, 225)
    cloud_fill = (226, 250, 255, 255)
    cloud_inner = (183, 233, 248, 255)
    for cx, cy, rx, ry in cloud_parts:
        add_ellipse(draw, s(cx), s(cy), s(rx), s(ry), fill=cloud_outline)
    inner_parts = [(cx, cy + 4, rx - 22, ry - 22) for cx, cy, rx, ry in cloud_parts]
    for cx, cy, rx, ry in inner_parts:
        add_ellipse(draw, s(cx), s(cy), s(rx), s(ry), fill=cloud_fill)
    add_ellipse(draw, s(525), s(528), s(300), s(94), fill=cloud_inner)
    draw.rounded_rectangle((s(250), s(445), s(800), s(635)), radius=s(90), fill=cloud_inner)

    # Cloud highlight.
    draw.arc((s(350), s(250), s(650), s(520)), start=205, end=302, fill=(255, 255, 255, 150), width=s(18))
    add_ellipse(draw, s(430), s(315), s(28), s(13), fill=(255, 255, 255, 185))

    # Face, kept minimal at app-icon scale.
    draw.arc((s(355), s(442), s(430), s(520)), start=200, end=340, fill=(25, 91, 132, 255), width=s(18))
    draw.arc((s(595), s(442), s(670), s(520)), start=200, end=340, fill=(25, 91, 132, 255), width=s(18))
    draw.arc((s(412), s(465), s(612), s(610)), start=20, end=160, fill=(25, 91, 132, 255), width=s(18))

    # Location pin as core product signal.
    pin_shadow = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    pin_shadow_draw = ImageDraw.Draw(pin_shadow, "RGBA")
    pin_shadow_draw.ellipse((s(355), s(665), s(670), s(775)), fill=(24, 83, 121, 70))
    pin_shadow = pin_shadow.filter(ImageFilter.GaussianBlur(s(14)))
    img.alpha_composite(pin_shadow)
    draw = ImageDraw.Draw(img, "RGBA")

    pin = [(s(512), s(825)), (s(345), s(600)), (s(512), s(360)), (s(680), s(600))]
    draw.polygon(pin, fill=(255, 181, 40, 255))
    draw.line(pin + [pin[0]], fill=(236, 131, 33, 255), width=s(16), joint="curve")
    add_ellipse(draw, s(512), s(575), s(78), s(78), fill=(255, 248, 218, 255))
    draw.polygon(star_points(s(512), s(575), s(48), s(20)), fill=(255, 184, 43, 255))

    # Social presence dots around the pin.
    for cx, cy, fill in [
        (298, 675, (255, 138, 78, 255)),
        (728, 675, (255, 138, 78, 255)),
        (250, 565, (255, 199, 74, 255)),
        (774, 565, (255, 199, 74, 255)),
    ]:
        add_ellipse(draw, s(cx), s(cy), s(45), s(45), fill=fill)
        add_ellipse(draw, s(cx), s(cy), s(45), s(45), outline=(255, 255, 255, 220), width=s(9), fill=fill)

    # Subtle flare spark.
    for angle in range(0, 360, 45):
        rad = math.radians(angle)
        x1 = s(795 + math.cos(rad) * 30)
        y1 = s(270 + math.sin(rad) * 30)
        x2 = s(795 + math.cos(rad) * 70)
        y2 = s(270 + math.sin(rad) * 70)
        draw.line((x1, y1, x2, y2), fill=(255, 236, 155, 190), width=s(7))
    add_ellipse(draw, s(795), s(270), s(20), s(20), fill=(255, 244, 190, 255))

    img = img.resize((size, size), Image.Resampling.LANCZOS)
    mask = rounded_rect_mask(size, 226)
    final = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    final.alpha_composite(img)
    final.putalpha(mask)
    return final


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    icon = generate()
    icon.save(OUT)
    LEGACY_OUT.parent.mkdir(parents=True, exist_ok=True)
    icon.resize((460, 460), Image.Resampling.LANCZOS).save(LEGACY_OUT)
    print(OUT)


if __name__ == "__main__":
    main()
