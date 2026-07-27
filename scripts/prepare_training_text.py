from pathlib import Path

from pypdf import PdfReader


ROOT = Path(__file__).resolve().parents[1]
TRAINING = ROOT / "src" / "SuperiorAes.App" / "Assets" / "Training"
PDF_NAMES = (
    "AES-7067-IntelliTap-II-Historical-Manual.pdf",
    "AES-7794-IntelliPro-Fire-Installation-Manual.pdf",
    "AES-7794-IntelliPro-Quick-Start-Guide.pdf",
    "AES-7794A-IntelliPro-2.0-Installation-Manual.pdf",
)


def main() -> None:
    for pdf_name in PDF_NAMES:
        pdf_path = TRAINING / pdf_name
        reader = PdfReader(pdf_path)
        sections = []
        for page_number, page in enumerate(reader.pages, start=1):
            text = (page.extract_text() or "").strip()
            sections.append(f"--- PAGE {page_number} ---\n{text}")
        text_path = pdf_path.with_suffix(".txt")
        text_path.write_text(
            "\n\n".join(sections).rstrip() + "\n",
            encoding="utf-8",
        )
        print(f"{text_path.name}: {len(reader.pages)} pages")


if __name__ == "__main__":
    main()
