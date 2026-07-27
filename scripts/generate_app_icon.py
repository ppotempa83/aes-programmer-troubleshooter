from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "src" / "SuperiorAes.App" / "Assets" / "Branding"
ICO_PATH = OUTPUT / "superior-aes.ico"
PNG_PATH = OUTPUT / "superior-aes-icon.png"


def font(size: int) -> ImageFont.FreeTypeFont:
    candidates = [
        Path(r"C:\Windows\Fonts\segoeuib.ttf"),
        Path(r"C:\Windows\Fonts\arialbd.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default(size=size)


def render(size: int) -> Image.Image:
    scale = size / 256
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    inset = round(7 * scale)
    radius = round(52 * scale)
    draw.rounded_rectangle(
        (inset, inset, size - inset, size - inset),
        radius=radius,
        fill=(197, 32, 47, 255),
        outline=(9, 26, 44, 255),
        width=max(1, round(10 * scale)),
    )
    draw.rounded_rectangle(
        (round(19 * scale), round(19 * scale), size - round(19 * scale), size - round(19 * scale)),
        radius=round(41 * scale),
        outline=(255, 255, 255, 92),
        width=max(1, round(3 * scale)),
    )

    label = "AES"
    label_font = font(round(82 * scale))
    bounds = draw.textbbox((0, 0), label, font=label_font, stroke_width=max(1, round(2 * scale)))
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    position = ((size - width) / 2, (size - height) / 2 - bounds[1] - round(4 * scale))
    draw.text(
        position,
        label,
        font=label_font,
        fill=(255, 255, 255, 255),
        stroke_width=max(1, round(2 * scale)),
        stroke_fill=(121, 10, 21, 255),
    )
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    large = render(512)
    large.save(PNG_PATH)
    frames = [render(size) for size in (16, 24, 32, 48, 64, 128, 256)]
    frames[-1].save(
        ICO_PATH,
        format="ICO",
        append_images=frames[:-1],
        sizes=[(size, size) for size in (16, 24, 32, 48, 64, 128, 256)],
    )
    print(ICO_PATH)


if __name__ == "__main__":
    main()
