#!/usr/bin/env python3
"""Extract square icon tiles from source PNG sheets and export 32px/48px variants."""

from __future__ import annotations

import argparse
import re
import sys
from collections import deque
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

OUTPUT_SIZES = (32, 48)
OUTPUT_PATTERN = re.compile(r"^.+_(32|48)px\.png$", re.IGNORECASE)
LIGHT_EDGE_THRESHOLD = 110
LIGHT_EDGE_RATIO = 0.08


@dataclass(frozen=True)
class Square:
    left: int
    top: int
    right: int
    bottom: int

    @property
    def width(self) -> int:
        return self.right - self.left + 1

    @property
    def height(self) -> int:
        return self.bottom - self.top + 1

    def contains(self, other: Square) -> bool:
        return (
            self.left <= other.left
            and self.top <= other.top
            and self.right >= other.right
            and self.bottom >= other.bottom
        )


def slugify_name(name: str) -> str:
    cleaned = name.strip().lower()
    cleaned = cleaned.replace("/", " ")
    cleaned = re.sub(r"[^a-z0-9]+", "", cleaned)
    return cleaned or "icon"


def pixel_luminance(pixel: tuple[int, ...]) -> float:
    return sum(pixel[:3]) / 3


def is_light_pixel(pixel: tuple[int, ...], threshold: int = LIGHT_EDGE_THRESHOLD) -> bool:
    return pixel_luminance(pixel) > threshold


def is_dark(pixel: tuple[int, int, int], threshold: int) -> bool:
    return pixel[0] < threshold and pixel[1] < threshold and pixel[2] < threshold


def is_foreground_pixel(red: int, green: int, blue: int) -> bool:
    if green > 70 and green > red + 12 and green > blue + 12:
        return True
    if max(red, green, blue) > 105:
        return True
    return False


def find_connected_squares(image: Image.Image, dark_threshold: int) -> list[Square]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    visited = [[False] * width for _ in range(height)]
    squares: list[Square] = []

    for y in range(height):
        for x in range(width):
            if visited[y][x] or not is_dark(pixels[x, y], dark_threshold):
                continue

            queue: deque[tuple[int, int]] = deque([(x, y)])
            visited[y][x] = True
            min_x = max_x = x
            min_y = max_y = y

            while queue:
                cx, cy = queue.popleft()
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)

                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx - 1, cy)):
                    if 0 <= nx < width and 0 <= ny < height and not visited[ny][nx]:
                        if is_dark(pixels[nx, ny], dark_threshold):
                            visited[ny][nx] = True
                            queue.append((nx, ny))

            box_width = max_x - min_x + 1
            box_height = max_y - min_y + 1
            if box_width < 80 or box_height < 80:
                continue

            aspect = box_width / box_height
            if not 0.82 <= aspect <= 1.22:
                continue

            squares.append(Square(min_x, min_y, max_x, max_y))

    return squares


def find_square_by_projection(image: Image.Image, dark_threshold: int) -> Square | None:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()

    row_counts = []
    for y in range(height):
        count = sum(1 for x in range(width) if is_dark(pixels[x, y], dark_threshold))
        row_counts.append(count)

    max_count = max(row_counts)
    if max_count < width * 0.35:
        return None

    active_rows = [y for y, count in enumerate(row_counts) if count >= max_count * 0.55]
    if not active_rows:
        return None

    top = active_rows[0]
    bottom = active_rows[-1]

    col_counts = []
    for x in range(width):
        count = sum(
            1
            for y in range(top, bottom + 1)
            if is_dark(pixels[x, y], dark_threshold)
        )
        col_counts.append(count)

    span = bottom - top + 1
    active_cols = [x for x, count in enumerate(col_counts) if count >= span * 0.55]
    if not active_cols:
        return None

    left = active_cols[0]
    right = active_cols[-1]
    return Square(left, top, right, bottom)


def remove_nested_squares(squares: list[Square]) -> list[Square]:
    kept: list[Square] = []
    for candidate in sorted(squares, key=lambda square: square.width * square.height, reverse=True):
        if any(other.contains(candidate) for other in kept):
            continue
        kept.append(candidate)
    return kept


def detect_squares(image: Image.Image, dark_threshold: int) -> list[Square]:
    connected = find_connected_squares(image, dark_threshold)
    connected = remove_nested_squares(connected)

    if connected:
        return sorted(connected, key=lambda square: (square.top, square.left))

    projected = find_square_by_projection(image, dark_threshold)
    return [projected] if projected else []


def tighten_bounds(square: Square, pixels) -> Square:
    left, top, right, bottom = square.left, square.top, square.right, square.bottom

    while top < bottom:
        row = [pixels[x, top] for x in range(left, right + 1)]
        if sum(1 for pixel in row if is_light_pixel(pixel)) > len(row) * LIGHT_EDGE_RATIO:
            top += 1
        else:
            break

    while bottom > top:
        row = [pixels[x, bottom] for x in range(left, right + 1)]
        if sum(1 for pixel in row if is_light_pixel(pixel)) > len(row) * LIGHT_EDGE_RATIO:
            bottom -= 1
        else:
            break

    while left < right:
        column = [pixels[left, y] for y in range(top, bottom + 1)]
        if sum(1 for pixel in column if is_light_pixel(pixel)) > len(column) * LIGHT_EDGE_RATIO:
            left += 1
        else:
            break

    while right > left:
        column = [pixels[right, y] for y in range(top, bottom + 1)]
        if sum(1 for pixel in column if is_light_pixel(pixel)) > len(column) * LIGHT_EDGE_RATIO:
            right -= 1
        else:
            break

    return Square(left, top, right, bottom)


def crop_square(image: Image.Image, square: Square) -> Image.Image:
    rgb = image.convert("RGB")
    pixels = rgb.load()
    tight = tighten_bounds(square, pixels)
    side = min(tight.width, tight.height)
    center_x = (tight.left + tight.right) // 2
    center_y = (tight.top + tight.bottom) // 2
    left = center_x - side // 2
    top = center_y - side // 2
    return rgb.crop((left, top, left + side, top + side))


def apply_transparency(icon: Image.Image) -> Image.Image:
    rgba = icon.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size

    for y in range(height):
        for x in range(width):
            red, green, blue, _alpha = pixels[x, y]
            if is_foreground_pixel(red, green, blue):
                pixels[x, y] = (red, green, blue, 255)
            else:
                pixels[x, y] = (0, 0, 0, 0)

    return rgba


def load_names(source_path: Path, icon_count: int, cli_names: list[str] | None) -> list[str]:
    if cli_names:
        if len(cli_names) != icon_count:
            raise ValueError(
                f"{source_path.name}: expected {icon_count} names, got {len(cli_names)}"
            )
        return [slugify_name(name) for name in cli_names]

    sidecar = source_path.with_suffix(".names.txt")
    if sidecar.exists():
        names = [slugify_name(line) for line in sidecar.read_text(encoding="utf-8").splitlines() if line.strip()]
        if len(names) != icon_count:
            raise ValueError(
                f"{sidecar.name}: expected {icon_count} names, found {len(names)}"
            )
        return names

    return [f"icon{index:02d}" for index in range(1, icon_count + 1)]


def is_source_png(path: Path) -> bool:
    if path.suffix.lower() != ".png":
        return False
    if path.name.lower().startswith("tmp_"):
        return False
    if OUTPUT_PATTERN.match(path.name):
        return False
    return True


def export_icon(icon: Image.Image, output_dir: Path, name: str) -> list[Path]:
    if icon.width != icon.height:
        raise RuntimeError(f"{name}: cropped icon is not square ({icon.width}x{icon.height})")

    written: list[Path] = []
    for size in OUTPUT_SIZES:
        resized = icon.resize((size, size), Image.Resampling.LANCZOS)
        transparent = apply_transparency(resized)
        target = output_dir / f"{name}_{size}px.png"
        transparent.save(target)
        written.append(target)
    return written


def verify_outputs(output_dir: Path) -> list[str]:
    issues: list[str] = []

    for path in sorted(output_dir.glob("*_*px.png")):
        icon = Image.open(path).convert("RGBA")
        if icon.width != icon.height:
            issues.append(f"{path.name}: not square ({icon.width}x{icon.height})")
            continue

        transparent = apply_transparency(icon.copy())
        pixels = transparent.load()
        width, height = transparent.size
        opaque_background = 0

        for y in range(height):
            for x in range(width):
                red, green, blue, alpha = pixels[x, y]
                if alpha == 0:
                    continue
                if not is_foreground_pixel(red, green, blue):
                    opaque_background += 1

        if opaque_background > 0:
            issues.append(f"{path.name}: {opaque_background} opaque background pixels detected")

    return issues


def process_source(
    source_path: Path,
    output_dir: Path,
    dark_threshold: int,
    cli_names: list[str] | None,
    dry_run: bool,
) -> list[Path]:
    image = Image.open(source_path)
    squares = detect_squares(image, dark_threshold)
    if not squares:
        raise RuntimeError(f"No square icons detected in {source_path.name}")

    names = load_names(source_path, len(squares), cli_names)
    written: list[Path] = []

    for square, name in zip(squares, names, strict=True):
        cropped = crop_square(image, square)
        if dry_run:
            print(
                f"[dry-run] {source_path.name}: {name} "
                f"({square.left},{square.top})-({square.right},{square.bottom}) "
                f"crop={cropped.width}x{cropped.height} "
                f"-> {name}_32px.png, {name}_48px.png"
            )
            continue

        written.extend(export_icon(cropped, output_dir, name))
        print(f"Wrote {name}_32px.png and {name}_48px.png from {source_path.name}")

    return written


def parse_args() -> argparse.Namespace:
    default_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(
        description="Extract square icons from PNG sheets and write 32px/48px PNGs."
    )
    parser.add_argument(
        "sources",
        nargs="*",
        type=Path,
        help="Source PNG files. Defaults to all non-output PNGs in --output-dir.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=default_dir,
        help="Directory to write extracted icons (default: script directory).",
    )
    parser.add_argument(
        "--dark-threshold",
        type=int,
        default=100,
        help="RGB threshold for detecting dark icon tiles (default: 100).",
    )
    parser.add_argument(
        "--names",
        help="Comma-separated icon names for a single source file, in top-to-bottom, left-to-right order.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Detect icons and print planned output without writing files.",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="Verify generated icon outputs and exit.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    if args.verify:
        issues = verify_outputs(output_dir)
        if issues:
            for issue in issues:
                print(f"VERIFY FAIL: {issue}", file=sys.stderr)
            return 1
        print(f"Verified {len(list(output_dir.glob('*_*px.png')))} icon outputs.")
        return 0

    if args.sources:
        sources = [path.resolve() for path in args.sources]
    else:
        sources = sorted(path for path in output_dir.iterdir() if path.is_file() and is_source_png(path))

    if not sources:
        print("No source PNG files found.", file=sys.stderr)
        return 1

    cli_names = [name.strip() for name in args.names.split(",")] if args.names else None
    if cli_names and len(sources) != 1:
        print("--names can only be used when processing a single source file.", file=sys.stderr)
        return 1

    exit_code = 0
    for source in sources:
        try:
            process_source(source, output_dir, args.dark_threshold, cli_names, args.dry_run)
        except Exception as exc:
            print(f"Error processing {source.name}: {exc}", file=sys.stderr)
            exit_code = 1

    if exit_code == 0 and not args.dry_run:
        issues = verify_outputs(output_dir)
        if issues:
            for issue in issues:
                print(f"VERIFY FAIL: {issue}", file=sys.stderr)
            return 1
        print(f"Verified {len(list(output_dir.glob('*_*px.png')))} icon outputs.")

    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
