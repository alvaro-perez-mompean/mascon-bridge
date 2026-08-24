"""Rasterise assets/icon.svg into the multi-resolution mascon-bridge.ico.

    python assets/build-icon.py

Run from the repository root. Needs: svglib, rlPyCairo, pillow.

renderPM cannot produce a transparent background, so the rounded square is cut
out here with a mask that matches the rx in the SVG.
"""

import io
import os
import sys

from PIL import Image, ImageDraw
from reportlab.graphics import renderPM
from svglib.svglib import svg2rlg

SVG = os.path.join("assets", "icon.svg")
ICO = "mascon-bridge.ico"
PREVIEW = os.path.join("assets", "preview")

VIEWBOX = 256
CORNER_RADIUS = 56          # keep in step with the rx on the background rect
MASTER = 1024
SIZES = [256, 128, 64, 48, 32, 24, 16]


def render_master() -> Image.Image:
    drawing = svg2rlg(SVG)
    drawing.scale(MASTER / drawing.width, MASTER / drawing.height)
    drawing.width = drawing.height = MASTER

    png = renderPM.drawToString(drawing, fmt="PNG", bg=None)
    image = Image.open(io.BytesIO(png)).convert("RGBA")

    radius = CORNER_RADIUS / VIEWBOX * MASTER
    mask = Image.new("L", (MASTER, MASTER), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, MASTER - 1, MASTER - 1], radius=radius, fill=255
    )
    image.putalpha(mask)
    return image


def main() -> int:
    if not os.path.exists(SVG):
        print(f"{SVG} not found. Run this from the repository root.", file=sys.stderr)
        return 1

    master = render_master()
    os.makedirs(PREVIEW, exist_ok=True)
    master.resize((256, 256), Image.LANCZOS).save(os.path.join(PREVIEW, "icon-256.png"))

    # Downsampling each size from the 1024 master keeps the small ones crisp;
    # letting the ico writer scale from one bitmap does not.
    frames = [master.resize((s, s), Image.LANCZOS) for s in SIZES]
    frames[0].save(ICO, format="ICO", sizes=[(s, s) for s in SIZES], append_images=frames[1:])

    print(f"{ICO}: {', '.join(f'{s}x{s}' for s in SIZES)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
