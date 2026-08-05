from __future__ import annotations

import os
from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    HRFlowable,
    Image,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf"
TRAINING = ROOT / "src" / "SuperiorAes.App" / "Assets" / "Training"
BRANDING = ROOT / "src" / "SuperiorAes.App" / "Assets" / "Branding" / "superior-aes-icon.png"
PRODUCT = ROOT / "src" / "SuperiorAes.App" / "Assets" / "Hardware" / "aes-7794-intellipro.jpg"
PDF_PATH = OUTPUT / "AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.pdf"
TEXT_PATH = TRAINING / "AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.txt"

NAVY = colors.HexColor("#10253D")
NAVY_DEEP = colors.HexColor("#091A2C")
RED = colors.HexColor("#C5202F")
BLUE = colors.HexColor("#176FAE")
PALE_BLUE = colors.HexColor("#EDF5FA")
PALE_RED = colors.HexColor("#FFF2F3")
PALE_AMBER = colors.HexColor("#FFF7E8")
INK = colors.HexColor("#213043")
MUTED = colors.HexColor("#68788B")
LINE = colors.HexColor("#DDE4EA")
GREEN = colors.HexColor("#16825D")

SOURCES = [
    ("AES 7794 product page", "https://aes-corp.com/product/7794-subscriber-add-on-module/"),
    ("AES 7794 Installation Manual", "https://aes-corp.com/wp-content/uploads/2022/09/7794-Manual.pdf"),
    ("AES 7794 Quick Start Guide", "https://aes-corp.com/wp-content/uploads/2020/07/7794-Quick-Start-Guide.pdf"),
    ("AES 7744F Installation Manual", "https://aes-corp.com/wp-content/uploads/2020/07/7744-Install-Manual.pdf"),
    ("AES 7788F Installation Manual", "https://aes-corp.com/wp-content/uploads/2020/07/40-7788-Rev-6.pdf"),
    ("AES discontinued-product replacements", "https://aes-corp.com/products/discontinued-products/"),
    ("AES 7794A product page", "https://aes-corp.com/product/7794a-accessory-board/"),
    ("AES 7794A Installation Manual", "https://aes-corp.com/wp-content/uploads/2023/04/40-7794A-Install-Manual-Rev-3-9-20-2018.pdf"),
    ("AES legacy network best practices", "https://aes-corp.com/best-practices-guideline/"),
]


class SignalPath(Flowable):
    def __init__(self, width: float = 500, height: float = 120):
        super().__init__()
        self.width = width
        self.height = height

    def draw(self):
        c = self.canv
        boxes = [
            (0, "FACP", "Contact ID dialer"),
            (175, "7794", "IntelliPro Fire"),
            (350, "7744F / 7788F", "AES mesh radio"),
        ]
        for x, title, subtitle in boxes:
            c.setFillColor(NAVY if x != 175 else RED)
            c.roundRect(x, 35, 145, 58, 8, fill=1, stroke=0)
            c.setFillColor(colors.white)
            c.setFont("Helvetica-Bold", 11)
            c.drawCentredString(x + 72.5, 70, title)
            c.setFont("Helvetica", 8)
            c.drawCentredString(x + 72.5, 53, subtitle)
        c.setStrokeColor(MUTED)
        c.setLineWidth(2)
        for start in (145, 320):
            c.line(start, 64, start + 30, 64)
            c.line(start + 22, 69, start + 30, 64)
            c.line(start + 22, 59, start + 30, 64)
        c.setFillColor(MUTED)
        c.setFont("Helvetica", 8)
        c.drawCentredString(160, 78, "TIP / RING")
        c.drawCentredString(335, 78, "AES cable")
        c.setFillColor(RED)
        c.setFont("Helvetica-Bold", 8)
        c.drawCentredString(250, 14, "Programming attaches to 7794 J2 - never to 7794 J1 TO RADIO")


def styles():
    base = getSampleStyleSheet()
    return {
        "body": ParagraphStyle(
            "Body",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=9.3,
            leading=13.2,
            textColor=INK,
            spaceAfter=7,
        ),
        "small": ParagraphStyle(
            "Small",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=7.7,
            leading=10.5,
            textColor=MUTED,
        ),
        "h1": ParagraphStyle(
            "H1",
            parent=base["Heading1"],
            fontName="Helvetica-Bold",
            fontSize=25,
            leading=29,
            textColor=NAVY,
            spaceAfter=8,
        ),
        "h2": ParagraphStyle(
            "H2",
            parent=base["Heading2"],
            fontName="Helvetica-Bold",
            fontSize=15,
            leading=19,
            textColor=NAVY,
            spaceBefore=11,
            spaceAfter=7,
        ),
        "h3": ParagraphStyle(
            "H3",
            parent=base["Heading3"],
            fontName="Helvetica-Bold",
            fontSize=11,
            leading=14,
            textColor=BLUE,
            spaceBefore=8,
            spaceAfter=4,
        ),
        "cover": ParagraphStyle(
            "Cover",
            parent=base["Heading1"],
            fontName="Helvetica-Bold",
            fontSize=26,
            leading=31,
            textColor=colors.white,
            alignment=TA_CENTER,
        ),
        "cover_sub": ParagraphStyle(
            "CoverSub",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=12,
            leading=17,
            textColor=colors.HexColor("#D6E4F0"),
            alignment=TA_CENTER,
        ),
        "callout": ParagraphStyle(
            "Callout",
            parent=base["BodyText"],
            fontName="Helvetica-Bold",
            fontSize=9,
            leading=13,
            textColor=NAVY,
        ),
        "bullet": ParagraphStyle(
            "Bullet",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=9,
            leading=12.5,
            leftIndent=15,
            firstLineIndent=-8,
            bulletIndent=4,
            textColor=INK,
            spaceAfter=3,
        ),
        "source": ParagraphStyle(
            "Source",
            parent=base["BodyText"],
            fontName="Helvetica",
            fontSize=7.4,
            leading=10,
            textColor=INK,
            leftIndent=8,
            firstLineIndent=-8,
            spaceAfter=3,
        ),
    }


def para(text, style):
    return Paragraph(text, style)


def bullet(text, s):
    return Paragraph(f"- {text}", s["bullet"])


def callout(text, s, background=PALE_AMBER, border=colors.HexColor("#E4B65F")):
    return Table(
        [[Paragraph(text, s["callout"])]],
        colWidths=[7.0 * inch],
        style=TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), background),
                ("BOX", (0, 0), (-1, -1), 1, border),
                ("LEFTPADDING", (0, 0), (-1, -1), 12),
                ("RIGHTPADDING", (0, 0), (-1, -1), 12),
                ("TOPPADDING", (0, 0), (-1, -1), 9),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 9),
            ]
        ),
    )


def table(data, widths, header=True, row_heights=None):
    header_style = ParagraphStyle(
        "TableHeader",
        fontName="Helvetica-Bold",
        fontSize=7.7,
        leading=9.6,
        textColor=colors.white,
        splitLongWords=True,
    )
    cell_style = ParagraphStyle(
        "TableCell",
        fontName="Helvetica",
        fontSize=7.6,
        leading=9.7,
        textColor=INK,
        splitLongWords=True,
    )
    wrapped = []
    for row_index, row in enumerate(data):
        style = header_style if header and row_index == 0 else cell_style
        wrapped.append(
            [
                cell if isinstance(cell, Flowable) else Paragraph(escape(str(cell)) or " ", style)
                for cell in row
            ]
        )
    commands = [
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.5, LINE),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ("TEXTCOLOR", (0, 0), (-1, -1), INK),
    ]
    if header:
        commands.extend(
            [
                ("BACKGROUND", (0, 0), (-1, 0), NAVY),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
            ]
        )
    for row in range(1 if header else 0, len(data)):
        if row % 2 == 0:
            commands.append(("BACKGROUND", (0, row), (-1, row), colors.HexColor("#F7F9FA")))
    return Table(
        wrapped,
        colWidths=widths,
        rowHeights=row_heights,
        repeatRows=1 if header else 0,
        style=TableStyle(commands),
    )


def header_footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(LINE)
    canvas.line(0.62 * inch, 0.48 * inch, 7.88 * inch, 0.48 * inch)
    canvas.setFillColor(MUTED)
    canvas.setFont("Helvetica", 7)
    canvas.drawString(0.62 * inch, 0.28 * inch, "AES Contact ID / Dialer Capture Field Guide")
    canvas.drawRightString(7.88 * inch, 0.28 * inch, f"Page {doc.page}")
    canvas.setFillColor(RED)
    canvas.setFont("Helvetica-Bold", 7)
    canvas.drawCentredString(4.25 * inch, 0.28 * inch, "AES Programmer & Troubleshooter")
    canvas.restoreState()


def build_story(s):
    story = []

    # Cover
    cover_table = Table(
        [
            [
                Image(str(BRANDING), width=1.65 * inch, height=1.65 * inch),
                Paragraph("CONTACT ID<br/>DIALER CAPTURE", s["cover"]),
                Image(str(PRODUCT), width=1.55 * inch, height=1.55 * inch),
            ]
        ],
        colWidths=[1.8 * inch, 3.4 * inch, 1.8 * inch],
        style=TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), NAVY_DEEP),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("ALIGN", (0, 0), (-1, -1), "CENTER"),
                ("LEFTPADDING", (0, 0), (-1, -1), 10),
                ("RIGHTPADDING", (0, 0), (-1, -1), 10),
                ("TOPPADDING", (0, 0), (-1, -1), 18),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 18),
            ]
        ),
    )
    story.append(Spacer(1, 0.4 * inch))
    story.append(cover_table)
    story.append(Spacer(1, 0.22 * inch))
    story.append(Paragraph("7794 IntelliPro Fire + 7067 IntelliTap II", s["h1"]))
    story.append(
        Paragraph(
            "Field installation, configuration, test, and migration guide for AES 7744F and 7788F legacy fire subscribers",
            ParagraphStyle("coverline", parent=s["body"], fontSize=13, leading=18, textColor=BLUE, alignment=TA_CENTER),
        )
    )
    story.append(Spacer(1, 0.15 * inch))
    story.append(SignalPath())
    story.append(Spacer(1, 0.08 * inch))
    story.append(
        callout(
            "FIELD COMPANION - NOT A REPLACEMENT FOR THE AES MANUAL. Use the original 7794 or 7067 manual for connector drawings and exact field wiring. Put the account on test and obtain AHJ/central-station authorization before work.",
            s,
            PALE_RED,
            RED,
        )
    )
    story.append(Spacer(1, 0.3 * inch))
    story.append(
        Paragraph(
            "AES Programmer & Troubleshooter",
            ParagraphStyle("brand", parent=s["h2"], textColor=RED, alignment=TA_CENTER, fontSize=13),
        )
    )
    story.append(Paragraph("Independent field guide - Revision 1.1 - August 2026", s["small"]))
    story.append(PageBreak())

    story.append(Paragraph("1. What works with what", s["h1"]))
    story.append(
        para(
            "The 7744F and 7788F can carry Contact ID full data. The currently recommended legacy-fire accessory is the <b>7794 IntelliPro Fire</b>. The older <b>7067 IntelliTap II</b> was supported by the subscriber family but AES now lists it as discontinued and no longer supported.",
            s["body"],
        )
    )
    compatibility = [
        ["Subscriber family", "Dialer-capture accessory", "Status / action"],
        ["7744F / 7788F legacy fire", "7794 IntelliPro Fire", "Recommended legacy-fire Contact ID path"],
        ["7744F / 7788F legacy fire", "7067 IntelliTap II", "Historical only; discontinued and unsupported - verify with AES/AHJ"],
        ["7707 / 7177 IntelliNet 2.0 fire", "7794A IntelliPro", "Current 2.0 fire accessory; not for legacy 7744F/7788F"],
        ["7058E / 7350 legacy burglary", "7094 IntelliPro", "Legacy burglary path"],
        ["7007 IntelliNet 2.0 burglary", "7094A IntelliPro", "Current 2.0 burglary path"],
    ]
    story.append(table(compatibility, [1.75 * inch, 1.75 * inch, 3.5 * inch]))
    story.append(Spacer(1, 0.1 * inch))
    story.append(
        callout(
            "<b>Do not install a 7794A in a 7744F or 7788F.</b> AES identifies the 7794A as a 2.0 Fire accessory only. Use a legacy 7794 IntelliPro Fire for these subscribers.",
            s,
            PALE_RED,
            RED,
        )
    )

    story.append(Paragraph("2. Before arriving on site", s["h2"]))
    for item in [
        "Confirm subscriber model, firmware, network owner requirements, receiver/automation Contact ID handling, and whether the communicator will be primary or supplemental.",
        "For a UL-listed primary legacy-fire communicator, verify the AES 7762 Hardware Supervisor requirement. The AES 7795 kit combines the 7794, 7762, and 7740 annunciator.",
        "Obtain the current FACP programming sheet: dialer account, Contact ID format, receiver number, dial attempts, line supervision, and test plan.",
        "Have the official AES manual, correct standoffs/insulating washers, factory interconnect cable, approved programmer/cable, meter, and required conduit hardware.",
        "Notify the monitoring center, place the account on test, record existing subscriber and FACP programming, and coordinate impairment/AHJ requirements.",
    ]:
        story.append(bullet(item, s))

    story.append(PageBreak())
    story.append(Paragraph("3. Critical connector safety", s["h1"]))
    safety = [
        ["Connection", "Correct use", "Never do this"],
        ["Subscriber J1", "Factory AES accessory cable to approved 7794/7067; carries data and module power", "Never connect J1 pin 6 (+12 V) to USB, RS-232, TTL, FTDI, or a PC"],
        ["7794 J1 - TO RADIO", "Factory cable to subscriber", "Never attach the handheld or PC programmer here"],
        ["7794 J2 - HandHeld", "7041E programmer or a verified compatible PC-terminal interface", "Do not substitute an unverified RJ cable or pinout"],
        ["FACP TIP/RING", "Wire from documented dialer output to the exact 7794 terminals shown in the AES manual", "Do not infer terminal order from this companion guide"],
    ]
    story.append(table(safety, [1.3 * inch, 2.65 * inch, 3.05 * inch]))

    story.append(Paragraph("4. 7794 IntelliPro Fire installation", s["h2"]))
    story.append(
        callout(
            "De-energize the FACP and subscriber AC/battery sources using the manufacturers' shutdown sequence before mounting or wiring. Only qualified fire-alarm personnel should perform this work.",
            s,
            PALE_RED,
            RED,
        )
    )
    steps = [
        ("1", "Mount the module", "Install the 7794 on the documented subscriber-board standoffs. Fit the insulating washer at H3 and retain the green earth-ground hardware exactly as shown in the AES manual."),
        ("2", "Connect radio interface", "Use the supplied six-wire AES modular cord from 7794 J1 TO RADIO to subscriber J1. This approved accessory connection is different from the PC programming cable."),
        ("3", "Connect panel dialer", "Route FACP TIP and RING to the 7794 AP TIP/RING terminals using the exact primary or supplemental figure in the manual. AES limits the FACP/subscriber arrangement to the same room and no more than 20 ft of conduit."),
        ("4", "Add supervision if required", "For primary communication, install and wire the 7762 Hardware Supervisor and annunciation path per its manual and listing requirements."),
        ("5", "Restore power and inspect", "Confirm no shorts, correct earth ground, correct battery sizing, normal subscriber state, and a slow 7794 heartbeat blink before programming."),
    ]
    step_rows = [["Step", "Task", "Field action"]] + [[a, b, c] for a, b, c in steps]
    story.append(table(step_rows, [0.55 * inch, 1.55 * inch, 4.9 * inch]))

    story.append(Paragraph("5. 7794 Contact ID programming", s["h2"]))
    story.append(
        para(
            "Connect the AES 7041E programmer to <b>7794 J2 HandHeld</b>. The 7794 can also be configured by a PC terminal only through a verified compatible interface. Configure with the panel disconnected when practical so live panel traffic does not interrupt menu navigation.",
            s["body"],
        )
    )
    programming = [
        ["Control", "Action"],
        ["F1", "Enter CONFIG mode"],
        ["F3", "Change the displayed option"],
        ["F4 / F5", "Move up / down through configuration options"],
        ["E or ESC", "Exit configuration"],
        ["AP report format", "Select C for Contact ID"],
        ["Intercept number", "Match the FACP dialed number; AES default is 555 and the allowed entry is 3-20 digits"],
        ["Phone line / POTS", "Set to match the approved primary or supplemental architecture"],
        ["AP input gain", "Use 10 or 20 only as required by the FACP/interface test"],
        ["CID 4xx letter", "Choose U or C to match receiver/automation requirements"],
    ]
    story.append(table(programming, [1.7 * inch, 5.3 * inch]))
    story.append(
        callout(
            "Subscriber TTL: AES network guidance recommends 3 hours for Alarm, Trouble, Restoral, and IntelliTap traffic, but network owners must approve values for their own system. Program the subscriber INTELLITAP TTL and verify it after reconnecting the module.",
            s,
            PALE_BLUE,
            BLUE,
        )
    )

    story.append(Paragraph("6. End-to-end acceptance test", s["h2"]))
    for item in [
        "Verify 7794 LED: slow blink (about one pulse/second) indicates normal operation; a fast blink indicates no subscriber communication; solid/no blink requires corrective action per the manual.",
        "Send a known Contact ID alarm, trouble/fault, supervisory event when applicable, and each required restoral.",
        "Confirm complete account, qualifier, event, partition/group, and zone/user data at receiver automation - not merely RF receipt.",
        "Test line-cut/POTS behavior and FACP dialer supervision for the approved architecture.",
        "Rerun subscriber local status and routing table; confirm accepted NETCON, routes, power, voltage, and antenna condition.",
        "Reconnect every accessory/programming cable, restore the account to service, and retain the exported session/troubleshooting record.",
    ]:
        story.append(bullet(item, s))
    story.append(Paragraph("Configuration values recorded at acceptance", s["h2"]))
    config_record = [
        ["Parameter", "Programmed value", "Verified"],
        ["AP report format", "", "Contact ID (C)"],
        ["Intercept number", "", ""],
        ["Phone line / POTS mode", "", ""],
        ["AP input gain", "", ""],
        ["CID 4xx letter", "", ""],
        ["Subscriber INTELLITAP TTL", "", ""],
    ]
    story.append(
        table(
            config_record,
            [2.25 * inch, 2.75 * inch, 2.0 * inch],
            row_heights=[None] + [0.28 * inch] * (len(config_record) - 1),
        )
    )
    story.append(PageBreak())

    story.append(Paragraph("7. 7067 IntelliTap II - historical workflow", s["h1"]))
    story.append(
        callout(
            "LEGACY / DISCONTINUED / NO LONGER SUPPORTED. Do not treat historical jumper settings as a current approval. Confirm the exact unit, listing, FACP, receiver, network, and AHJ requirements with AES before service or reuse.",
            s,
            PALE_RED,
            RED,
        )
    )
    story.append(
        para(
            "Later AES 7744F/7788F manuals identify the 7067 IntelliTap II as a supported J1 dialer-capture accessory, but AES now lists the 7067 as discontinued. The replacement matrix directs legacy fire applications to the 7794 IntelliPro Fire.",
            s["body"],
        )
    )
    historical = [
        ["Historical item", "Technician treatment"],
        ["Contact ID format", "Use only the exact jumper position documented by the supplied 7067 manual; do not infer from a 7794 menu"],
        ["Line-cut delay", "Verify jumper combination against the exact 7067 revision and approved system design"],
        ["No-phone-line operation", "Never combine incompatible jumper modes; verify reset procedure after changes"],
        ["Subscriber connection", "Use only the factory accessory cord; disconnect the module while the subscriber programming interface is attached"],
        ["Use classification", "Treat as supplemental unless the exact listing/AHJ documentation says otherwise"],
    ]
    story.append(table(historical, [2.0 * inch, 5.0 * inch]))
    story.append(Paragraph("Migration recommendation", s["h2"]))
    story.append(
        para(
            "Document the existing 7067 settings and successful signals, then plan conversion to a 7794/7795 legacy-fire solution or an approved IntelliNet 2.0 subscriber/module combination. Do not migrate by copying jumper positions directly into IntelliPro settings; rebuild the configuration from the FACP and central-station requirements.",
            s["body"],
        )
    )

    story.append(Paragraph("8. Troubleshooting decision table", s["h2"]))
    troubleshooting = [
        ["Symptom", "Evidence to collect", "Recommended next action"],
        ["No FACP handshake/kiss-off", "FACP dial format, TIP/RING voltage, dialed digits, POTS mode, AP input gain", "Verify wiring against official figure; match CID and intercept settings; avoid blind gain changes"],
        ["7794 fast LED blink", "Subscriber J1 cable, module power, connector seating, subscriber state", "Remove power safely, inspect factory cable, reconnect, and verify subscriber communication"],
        ["FACP receives dialer trouble", "Both dialer supervision settings and whether panel tests both lines simultaneously", "Check manual compatibility restriction and approved primary/supplemental diagram"],
        ["Signal reaches AES but lacks full CID data", "Receiver line card/output, account, qualifier, event, group, zone/user", "Verify automation receives IntelliTap-type full-data packets and correct receiver configuration"],
        ["Weak or delayed delivery", "NETCON, routes, Q, RF power, voltage, antenna/coax", "Correct radio-network or site RF issues separately from dialer capture"],
    ]
    story.append(table(troubleshooting, [1.55 * inch, 2.45 * inch, 3.0 * inch]))

    story.append(PageBreak())
    story.append(Paragraph("9. Field record", s["h1"]))
    story.append(
        para(
            "Complete this worksheet during acceptance, then attach the exported session log and troubleshooting-only report. Do not record subscriber cipher values or API keys.",
            s["body"],
        )
    )
    record = [
        ["Item", "Record"],
        ["Site / account", " "],
        ["Subscriber / module / firmware", " "],
        ["FACP / Contact ID account", " "],
        ["POTS / intercept / input gain", " "],
        ["INTELLITAP TTL", " "],
        ["Alarm / trouble / supervisory / restoral confirmed", " "],
        ["Central-station operator / time", " "],
        ["Final NETCON / routes / Q", " "],
    ]
    story.append(
        table(
            record,
            [2.25 * inch, 4.75 * inch],
            row_heights=[None]
            + [0.34 * inch] * 5
            + [0.48 * inch]
            + [0.34 * inch] * 2,
        )
    )
    story.append(Paragraph("Acceptance closeout", s["h2"]))
    for item in [
        "All required Contact ID event types and restorals were received with complete account and point data.",
        "The final subscriber status, routing table, NETCON, power, voltage, antenna, and route evidence were retained.",
        "Every temporary programming lead was removed and all factory accessory cables were restored.",
        "The central station released the account from test and the impairment/AHJ process was closed.",
    ]:
        story.append(bullet(item, s))
    signatures = [
        ["Technician / date", "Central-station operator / time"],
        ["", ""],
    ]
    story.append(
        table(
            signatures,
            [3.5 * inch, 3.5 * inch],
            row_heights=[None, 0.55 * inch],
        )
    )
    story.append(PageBreak())

    story.append(Paragraph("10. Official references", s["h1"]))
    story.append(
        para(
            "The original readable AES 7794, 7794A, 7744F, and 7788F PDFs are the controlling technical references. The supplied 7067 manual is retained as historical material. Check AES Product Support for revisions before field use.",
            s["body"],
        )
    )
    for label, url in SOURCES:
        story.append(Paragraph(f"- <b>{label}:</b> <link href=\"{url}\" color=\"#176FAE\">{url}</link>", s["source"]))
    story.append(Spacer(1, 0.15 * inch))
    story.append(
        callout(
            "Physical bench validation with known-good 7744F and 7788F units remains required before deployment. Verify the 7794 PC-terminal interface and every programming operation against the actual hardware/firmware revision.",
            s,
            PALE_RED,
            RED,
        )
    )
    story.append(Spacer(1, 0.22 * inch))
    story.append(
        Paragraph(
            "AES Programmer & Troubleshooter",
            ParagraphStyle("brand2", parent=s["h2"], textColor=RED, alignment=TA_CENTER),
        )
    )
    story.append(Paragraph("Independent field-use reference", ParagraphStyle("sf", parent=s["body"], alignment=TA_CENTER)))
    return story


def write_text_companion():
    content = """AES CONTACT ID / DIALER CAPTURE FIELD GUIDE
7794 INTELLIPRO FIRE + 7067 INTELLITAP II
AES Programmer & Troubleshooter

VERIFIED COMPATIBILITY
- 7744F / 7788F legacy fire: 7794 IntelliPro Fire is the recommended Contact ID accessory.
- 7744F / 7788F legacy fire: 7067 IntelliTap II is historically compatible but discontinued and unsupported.
- 7707 / 7177 IntelliNet 2.0 fire: 7794A IntelliPro; do not use 7794A in legacy subscribers.
- 7058E / 7350 legacy burglary: 7094 IntelliPro.
- 7007 IntelliNet 2.0 burglary: 7094A IntelliPro.

CRITICAL SAFETY
- Put the account on test and follow impairment/AHJ procedures.
- De-energize AC and battery sources before mounting or wiring.
- Subscriber J1 pin 6 carries +12 V. Never connect it to USB, RS-232, TTL, FTDI, or a PC.
- Use only the AES factory accessory cable between the subscriber and approved module.
- Program the 7794 through J2 HandHeld. Never attach a programmer to J1 TO RADIO.
- Use official AES manual wiring figures; never infer terminal order from this companion guide.

7794 INTELLIPRO CONTACT ID
- Mount with required standoffs/insulating washer/earth hardware.
- Connect 7794 J1 TO RADIO to subscriber J1 with the supplied AES cable.
- Connect FACP TIP/RING using the exact primary or supplemental manual figure.
- Keep FACP and subscriber in the same room and within 20 ft of conduit.
- For UL primary use, verify the 7762 Hardware Supervisor requirement.
- F1 enters configuration; F3 changes; F4/F5 browse; E/ESC exits.
- Select C for Contact ID and match intercept/POTS/gain/account behavior to the approved design.
- AES network guidance recommends 3-hour TTL for Alarm, Trouble, Restoral, and IntelliTap traffic, subject to network approval.

7067 INTELLITAP II
- Historical, discontinued, and no longer supported.
- Retain the supplied original manual for existing-unit service evidence.
- Verify all jumper positions against the exact hardware/manual revision and AES/AHJ approval.
- Plan migration to 7794/7795 or an approved IntelliNet 2.0 solution.

ACCEPTANCE
- Confirm alarm, trouble/fault, supervisory where applicable, and required restorals.
- Verify the complete Contact ID account, qualifier, event, partition/group, and zone/user at automation.
- Reconnect accessories, rerun subscriber status/routes, and return the account to service only after acceptance.

See the PDF field guide and original AES manuals in the Training section for full details and official links.
"""
    TEXT_PATH.write_text(content, encoding="utf-8")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    TRAINING.mkdir(parents=True, exist_ok=True)
    s = styles()
    doc = BaseDocTemplate(
        str(PDF_PATH),
        pagesize=letter,
        rightMargin=0.62 * inch,
        leftMargin=0.62 * inch,
        topMargin=0.55 * inch,
        bottomMargin=0.62 * inch,
        title="AES Contact ID / Dialer Capture Field Guide",
        author="AES Programmer Project",
        subject="7794 IntelliPro Fire and 7067 IntelliTap II field installation and programming companion",
    )
    frame = Frame(doc.leftMargin, doc.bottomMargin, doc.width, doc.height, id="normal")
    doc.addPageTemplates([PageTemplate(id="guide", frames=frame, onPage=header_footer)])
    doc.build(build_story(s))
    write_text_companion()
    print(PDF_PATH)


if __name__ == "__main__":
    main()
