"""Generate WPT broken-chain app icon (Fluent tile style)."""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
WINUI_ASSETS = ROOT / "WPT.WinUI" / "Assets"

ICO_OUTPUT_PATHS = (
    ROOT / "WPT.Wpf" / "Resources" / "app.ico",
    WINUI_ASSETS / "app.ico",
    WINUI_ASSETS / "AppIcon.ico",
)

PNG_OUTPUTS: dict[str, tuple[int, int, bool]] = {
    "StoreLogo.png": (50, 50, True),
    "Square44x44Logo.scale-200.png": (88, 88, True),
    "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24, False),
    "Square44x44Logo.targetsize-48_altform-lightunplated.png": (48, 48, False),
    "Square150x150Logo.scale-200.png": (300, 300, True),
    "LockScreenLogo.scale-200.png": (48, 48, True),
    "Wide310x150Logo.scale-200.png": (620, 300, True),
    "SplashScreen.scale-200.png": (1240, 600, True),
}

BASE_SIZE = 16
ICON_COLOR = (255, 255, 255, 255)
BACKGROUND_COLOR = (43, 43, 43, 255)
CORNER_RADIUS_RATIO = 0.22
LINK_RADIUS = 3.15
STROKE = 1.35
GAP_DEGREES = 52.0

# Lower-left complete link, upper-right broken link (diagonal layout).
COMPLETE_CENTER = (5.15, 10.85)
BROKEN_CENTER = (10.85, 5.15)


def ring_arc_polygon(
    cx: float,
    cy: float,
    radius: float,
    stroke: float,
    start_deg: float,
    end_deg: float,
    steps: int = 48,
) -> list[tuple[float, float]]:
    outer = radius + stroke / 2
    inner = max(radius - stroke / 2, 0.05)
    points: list[tuple[float, float]] = []

    for i in range(steps + 1):
        t = i / steps
        angle = math.radians(start_deg + (end_deg - start_deg) * t)
        points.append((cx + outer * math.cos(angle), cy + outer * math.sin(angle)))

    for i in range(steps + 1):
        t = i / steps
        angle = math.radians(end_deg - (end_deg - start_deg) * t)
        points.append((cx + inner * math.cos(angle), cy + inner * math.sin(angle)))

    return points


def draw_ring(
    draw: ImageDraw.ImageDraw,
    cx: float,
    cy: float,
    radius: float,
    stroke: float,
    gap_degrees: float = 0.0,
) -> None:
    if gap_degrees <= 0:
        polygon = ring_arc_polygon(cx, cy, radius, stroke, 0, 360)
    else:
        gap_center = -math.pi / 2
        gap_half = math.radians(gap_degrees / 2)
        gap_start = gap_center - gap_half
        gap_end = gap_center + gap_half
        start_deg = math.degrees(gap_end)
        end_deg = math.degrees(gap_start + 2 * math.pi)
        polygon = ring_arc_polygon(cx, cy, radius, stroke, start_deg, end_deg)

    draw.polygon(polygon, fill=ICON_COLOR)


def draw_background(draw: ImageDraw.ImageDraw, size: int) -> None:
    radius = max(2, int(size * CORNER_RADIUS_RATIO))
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=BACKGROUND_COLOR)


def render_icon(size: int, *, with_background: bool) -> Image.Image:
    scale = size / BASE_SIZE
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")

    if with_background:
        draw_background(draw, size)

    radius = LINK_RADIUS * scale
    stroke = max(1.0, STROKE * scale)
    complete = (COMPLETE_CENTER[0] * scale, COMPLETE_CENTER[1] * scale)
    broken = (BROKEN_CENTER[0] * scale, BROKEN_CENTER[1] * scale)

    draw_ring(draw, *broken, radius, stroke, GAP_DEGREES)
    draw_ring(draw, *complete, radius, stroke)

    return image


def render_canvas(width: int, height: int, *, with_background: bool) -> Image.Image:
    icon_size = min(width, height)
    icon = render_icon(icon_size, with_background=with_background)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    offset = ((width - icon_size) // 2, (height - icon_size) // 2)
    canvas.alpha_composite(icon, offset)
    return canvas


def save_icon(path: Path) -> None:
    sizes = (16, 20, 24, 32, 48, 64, 128, 256)
    images = [render_icon(size, with_background=True) for size in sizes]
    path.parent.mkdir(parents=True, exist_ok=True)
    images[-1].save(
        path,
        format="ICO",
        sizes=[(size, size) for size in sizes],
        append_images=images[:-1],
        bitmap_format="bmp",
    )


def save_png_assets() -> None:
    WINUI_ASSETS.mkdir(parents=True, exist_ok=True)
    for filename, (width, height, with_background) in PNG_OUTPUTS.items():
        image = (
            render_canvas(width, height, with_background=with_background)
            if width != height
            else render_icon(width, with_background=with_background)
        )
        image.save(WINUI_ASSETS / filename, format="PNG")
        print(f"Saved {WINUI_ASSETS / filename}")


def main() -> None:
    for output_path in ICO_OUTPUT_PATHS:
        save_icon(output_path)
        print(f"Saved {output_path}")

    save_png_assets()


if __name__ == "__main__":
    main()
