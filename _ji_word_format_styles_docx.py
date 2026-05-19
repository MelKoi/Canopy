# -*- coding: utf-8 -*-
"""用 python-docx 设置 JI.docx 章/节/条/款/正文/附录格式（无需启动 Word）。"""
from __future__ import annotations

import os
import re
import shutil
import sys
from datetime import datetime

from docx import Document
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.text import WD_LINE_SPACING
from docx.oxml.ns import qn
from docx.shared import Pt

SZ_CHAPTER = Pt(18)
SZ_SECTION = Pt(14)
SZ_SUBSECTION = Pt(12)
SZ_BODY = Pt(12)
LINE_SPACING = 1.25
HALF_LINE_CHAPTER = Pt(9)
HALF_LINE_SECTION = Pt(7)
HALF_LINE_SUBSECTION = Pt(6)

RE_CHAPTER = re.compile(r"^第[一二三四五六七八九十百千\d]+章\s*\S")
RE_SECTION = re.compile(r"^\d+\.\d+\s+\S")
RE_SUBSECTION = re.compile(r"^\d+\.\d+\.\d+\s+\S")
RE_APPENDIX_CH = re.compile(r"^附录\s*[A-ZＡ-Ｚ]?\s*\S")
RE_APPENDIX_SEC = re.compile(r"^[A-ZＡ-Ｚ]\.\d+\s+\S")
RE_TABLE_CAP = re.compile(r"^表\s*\d")
RE_ITEM = re.compile(r"^[\(（][一二三四五六七八九十\d]+[\)）]")
RE_CODE = re.compile(
    r"^(using |public |private |void |class |#|\{|\}|//|\[Header|IEnumerator|Vector3|float |int |return |if |for |while )",
    re.I,
)


def _set_east_asia_font(font, name="宋体", size=None, bold=None):
    font.name = name
    try:
        r = font.element
        rPr = r.get_or_add_rPr()
        rFonts = rPr.rFonts
        if rFonts is None:
            from docx.oxml import OxmlElement

            rFonts = OxmlElement("w:rFonts")
            rPr.append(rFonts)
        rFonts.set(qn("w:eastAsia"), name)
        rFonts.set(qn("w:ascii"), name)
        rFonts.set(qn("w:hAnsi"), name)
    except Exception:
        pass
    if size is not None:
        font.size = size
    if bold is not None:
        font.bold = bold


def _configure_style(style, size, bold, space_before, space_after, first_indent_chars=0):
    _set_east_asia_font(style.font, size=size, bold=bold)
    pf = style.paragraph_format
    pf.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    pf.line_spacing = LINE_SPACING
    pf.space_before = space_before
    pf.space_after = space_after
    pf.first_line_indent = Pt(12 * first_indent_chars) if first_indent_chars else Pt(0)


def _apply_para_format(pf, size_pt: int, space_before, space_after, first_indent_chars=0):
    pf.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    pf.line_spacing = LINE_SPACING
    pf.space_before = space_before
    pf.space_after = space_after
    pf.first_line_indent = Pt(size_pt * first_indent_chars) if first_indent_chars else Pt(0)


def _apply_run_font(p, size, bold=False):
    if not p.runs:
        return
    for run in p.runs:
        _set_east_asia_font(run.font, size=size, bold=bold)


def configure_styles(doc: Document):
    mapping = [
        ("Heading 1", SZ_CHAPTER, True, HALF_LINE_CHAPTER, HALF_LINE_CHAPTER, 0),
        ("Heading 2", SZ_SECTION, True, HALF_LINE_SECTION, HALF_LINE_SECTION, 0),
        ("Heading 3", SZ_SUBSECTION, True, HALF_LINE_SUBSECTION, HALF_LINE_SUBSECTION, 0),
        ("Heading 4", SZ_SUBSECTION, True, Pt(0), Pt(0), 2),
        ("Normal", SZ_BODY, False, Pt(0), Pt(0), 2),
    ]
    for name, size, bold, sb, sa, ind in mapping:
        try:
            st = doc.styles[name]
        except KeyError:
            continue
        _configure_style(st, size, bold, sb, sa, ind)


def _is_toc(p) -> bool:
    return (p.style and p.style.name or "").lower().startswith("toc")


def _skip_title(t: str, tc: str) -> bool:
    return tc in ("摘要", "ABSTRACT", "目录", "致谢", "参考文献") or t == "摘 要"


def process_document(doc: Document):
    in_appendix = False
    h1, h2, h3, h4 = "Heading 1", "Heading 2", "Heading 3", "Heading 4"

    for p in doc.paragraphs:
        t = (p.text or "").strip()
        if not t:
            continue
        tc = re.sub(r"\s+", "", t)
        sn = p.style.name if p.style else ""

        if RE_APPENDIX_CH.match(t):
            in_appendix = True
        if tc in ("参考文献", "致谢", "致 谢"):
            in_appendix = False

        if _is_toc(p):
            continue

        if RE_TABLE_CAP.match(t):
            p.style = doc.styles["Normal"]
            _apply_para_format(p.paragraph_format, 12, Pt(0), Pt(0), 0)
            p.paragraph_format.alignment = 1  # center
            _apply_run_font(p, 12, False)
            continue

        target = None
        if RE_CHAPTER.match(t):
            target = h1
        elif in_appendix and RE_APPENDIX_CH.match(t):
            target = h2
        elif in_appendix and RE_APPENDIX_SEC.match(t):
            target = h3
        elif RE_SUBSECTION.match(t):
            target = h3
        elif RE_SECTION.match(t):
            target = h2
        elif RE_ITEM.match(t):
            target = h4

        if target:
            p.style = doc.styles[target]
            if target == h1:
                _apply_para_format(p.paragraph_format, 18, HALF_LINE_CHAPTER, HALF_LINE_CHAPTER, 0)
                _apply_run_font(p, 18, True)
            elif target == h2:
                _apply_para_format(p.paragraph_format, 14, HALF_LINE_SECTION, HALF_LINE_SECTION, 0)
                _apply_run_font(p, 14, True)
            elif target == h3:
                _apply_para_format(p.paragraph_format, 12, HALF_LINE_SUBSECTION, HALF_LINE_SUBSECTION, 0)
                _apply_run_font(p, 12, True)
            elif target == h4:
                _apply_para_format(p.paragraph_format, 12, Pt(0), Pt(0), 2)
                _apply_run_font(p, 12, True)
            continue

        if sn.startswith("Heading") or "标题" in sn:
            continue
        if _skip_title(t, tc):
            continue
        if sn not in ("Normal", "正文", "正文文本", "Plain Text"):
            continue

        p.style = doc.styles["Normal"]
        indent = 0 if (in_appendix and RE_CODE.match(t)) else 2
        _apply_para_format(p.paragraph_format, 12, Pt(0), Pt(0), indent)
        _apply_run_font(p, 12, False)


def backup_file(path: str) -> str:
    base, ext = os.path.splitext(path)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    dest = f"{base}_排版前备份_{stamp}{ext}"
    shutil.copy2(path, dest)
    return dest


def main():
    if len(sys.argv) < 2:
        print("usage: _ji_word_format_styles_docx.py <JI.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])
    bak = backup_file(path)
    print("backup:", bak)
    doc = Document(path)
    configure_styles(doc)
    process_document(doc)
    doc.save(path)
    print("OK", path)


if __name__ == "__main__":
    main()
