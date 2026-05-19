# -*- coding: utf-8 -*-
"""
按《毕业设计（论文）要求与撰写规范（本部 理工类）20230323》第四部分，
修正原文档：页眉/页脚距、标题 1～4 与正文样式、页眉页码分节。
"""
from __future__ import annotations

import os
import sys

# 保证与 _thesis_word_header_footer 同目录可导入
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import win32com.client as win32

from _thesis_word_header_footer import (
    configure_section1_cover_only,
    configure_section2_abstract_through_toc,
    configure_section3_body,
    relocate_first_section_break_after_cover,
)

# Word 常量
WD_LINE_SPACE_MULTIPLE = 5
WD_ALIGN_PARAGRAPH_CENTER = 1

# 4.1.1 页眉 20mm、页脚 15mm（Word 用磅，1mm≈2.83465pt）
MM_TO_PT = 72.0 / 25.4
HEADER_DISTANCE_MM = 20.0
FOOTER_DISTANCE_MM = 15.0

# 4.1.1 页边距 mm
MARGIN_TOP_MM = 30.0
MARGIN_LR_BOTTOM_MM = 25.0

# 4.2 字号（磅）
SZ_CHAPTER = 18  # 小二号
SZ_SECTION = 14  # 四号
SZ_SUBSECTION = 12  # 小四号（条）
SZ_BODY = 12  # 小四号（正文）
SZ_HEADER_FOOTER_NUM = 10.5  # 五号

LINE_SPACING_BODY = 1.25
LINE_UNIT_BEFORE_AFTER_HEADING = 0.5  # 章/节/条 段前段后各 0.5 行


def _style_by_names(doc, names):
    for name in names:
        try:
            return doc.Styles(name)
        except Exception:
            continue
    return None


def _set_east_asia_font(font, name="宋体"):
    font.Name = name
    try:
        font.NameFarEast = name
    except Exception:
        pass


def _apply_heading_style(st, size_pt, space_before_lines, space_after_lines):
    _set_east_asia_font(st.Font)
    st.Font.Size = size_pt
    st.Font.Bold = True
    pf = st.ParagraphFormat
    try:
        pf.LineUnitBefore = space_before_lines
        pf.LineUnitAfter = space_after_lines
        pf.SpaceBeforeAuto = False
        pf.SpaceAfterAuto = False
    except Exception:
        pf.SpaceBefore = size_pt * space_before_lines * 0.5
        pf.SpaceAfter = size_pt * space_after_lines * 0.5
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = LINE_SPACING_BODY
    try:
        pf.CharacterUnitFirstLineIndent = 0
        pf.FirstLineIndent = 0
    except Exception:
        pf.FirstLineIndent = 0


def _apply_heading4_style(st):
    """款、项：小四加粗，段前段后不空，1.25 倍行距，首行缩进 2 字符。"""
    _set_east_asia_font(st.Font)
    st.Font.Size = SZ_SUBSECTION
    st.Font.Bold = True
    pf = st.ParagraphFormat
    pf.LineUnitBefore = 0
    pf.LineUnitAfter = 0
    pf.SpaceBefore = 0
    pf.SpaceAfter = 0
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = LINE_SPACING_BODY
    try:
        pf.CharacterUnitFirstLineIndent = 2
    except Exception:
        pf.FirstLineIndent = SZ_SUBSECTION * 2  # 近似两字符


def _apply_body_style(st):
    _set_east_asia_font(st.Font)
    st.Font.Size = SZ_BODY
    st.Font.Bold = False
    pf = st.ParagraphFormat
    pf.LineUnitBefore = 0
    pf.LineUnitAfter = 0
    pf.SpaceBefore = 0
    pf.SpaceAfter = 0
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = LINE_SPACING_BODY
    try:
        pf.CharacterUnitFirstLineIndent = 2
    except Exception:
        pf.FirstLineIndent = SZ_BODY * 2


def _mm_to_points(mm: float) -> float:
    return mm * MM_TO_PT


def configure_page_setup_all_sections(doc):
    for i in range(1, doc.Sections.Count + 1):
        ps = doc.Sections(i).PageSetup
        ps.TopMargin = _mm_to_points(MARGIN_TOP_MM)
        ps.BottomMargin = _mm_to_points(MARGIN_LR_BOTTOM_MM)
        ps.LeftMargin = _mm_to_points(MARGIN_LR_BOTTOM_MM)
        ps.RightMargin = _mm_to_points(MARGIN_LR_BOTTOM_MM)
        ps.HeaderDistance = _mm_to_points(HEADER_DISTANCE_MM)
        ps.FooterDistance = _mm_to_points(FOOTER_DISTANCE_MM)
        try:
            ps.PaperSize = 7  # wdPaperA4
        except Exception:
            pass


def configure_document_styles(doc):
    pairs = [
        (["Heading 1", "标题 1", "标题1"], lambda s: _apply_heading_style(
            s, SZ_CHAPTER, LINE_UNIT_BEFORE_AFTER_HEADING, LINE_UNIT_BEFORE_AFTER_HEADING)),
        (["Heading 2", "标题 2", "标题2"], lambda s: _apply_heading_style(
            s, SZ_SECTION, LINE_UNIT_BEFORE_AFTER_HEADING, LINE_UNIT_BEFORE_AFTER_HEADING)),
        (["Heading 3", "标题 3", "标题3"], lambda s: _apply_heading_style(
            s, SZ_SUBSECTION, LINE_UNIT_BEFORE_AFTER_HEADING, LINE_UNIT_BEFORE_AFTER_HEADING)),
        (["Heading 4", "标题 4", "标题4"], _apply_heading4_style),
        (["Normal", "正文", "正文文本"], _apply_body_style),
    ]
    for names, fn in pairs:
        st = _style_by_names(doc, names)
        if st is None:
            print("warn: style not found", names)
            continue
        fn(st)
        print("styled", names[0])


def sync_paragraphs_to_styles(doc):
    """将已套用标题/正文样式的段落去直连格式，按样式库重刷。"""
    style_keys = [
        (["Heading 1", "标题 1"], SZ_CHAPTER),
        (["Heading 2", "标题 2"], SZ_SECTION),
        (["Heading 3", "标题 3"], SZ_SUBSECTION),
        (["Heading 4", "标题 4"], SZ_SUBSECTION),
        (["Normal", "正文"], SZ_BODY),
    ]
    for i in range(1, doc.Paragraphs.Count + 1):
        para = doc.Paragraphs(i)
        try:
            local = para.Style.NameLocal
        except Exception:
            continue
        for names, _ in style_keys:
            if not any(n in local or local == n for n in names):
                continue
            st = _style_by_names(doc, names)
            if st is None:
                break
            try:
                para.Style = st
                rng = para.Range
                rng.Font.Name = st.Font.Name
                try:
                    rng.Font.NameFarEast = st.Font.NameFarEast
                except Exception:
                    pass
                rng.Font.Size = st.Font.Size
                rng.Font.Bold = st.Font.Bold
            except Exception:
                pass
            break


def refresh_headers_footers(doc):
    if doc.Sections.Count < 3:
        raise RuntimeError(f"需要 3 节，当前 {doc.Sections.Count}")
    configure_section1_cover_only(doc.Sections(1))
    configure_section2_abstract_through_toc(doc.Sections(2))
    configure_section3_body(doc.Sections(3))


def main():
    if len(sys.argv) < 2:
        print("usage: _thesis_word_format_apply.py <论文.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])
    if not os.path.isfile(path):
        print("not found:", path, file=sys.stderr)
        sys.exit(1)

    relocate_first_section_break_after_cover(path)

    app = win32.Dispatch("Word.Application")
    app.Visible = False
    try:
        app.DisplayAlerts = 0
    except Exception:
        pass

    doc = None
    try:
        doc = app.Documents.Open(path, ReadOnly=False, AddToRecentFiles=False)
        configure_page_setup_all_sections(doc)
        configure_document_styles(doc)
        sync_paragraphs_to_styles(doc)
        refresh_headers_footers(doc)
        try:
            doc.Fields.Update()
        except Exception:
            pass
        doc.Save()
        print("OK", path)
    finally:
        if doc is not None:
            try:
                doc.Close(False)
            except Exception:
                pass
        try:
            app.Quit()
        except Exception:
            pass


if __name__ == "__main__":
    main()
