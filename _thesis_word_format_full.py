# -*- coding: utf-8 -*-
"""
对照《毕业设计（论文）要求与撰写规范（本部 理工类）20230323》第四部分，
对原文档做版式检查并修正：页边距、页眉页脚、样式库、摘要关键词、章/节/条标题、表题。
"""
from __future__ import annotations

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import win32com.client as win32

from _thesis_word_format_apply import (
    configure_document_styles,
    configure_page_setup_all_sections,
    sync_paragraphs_to_styles,
)
from _thesis_word_header_footer import (
    configure_section1_cover_only,
    configure_section2_abstract_through_toc,
    configure_section3_body,
    relocate_first_section_break_after_cover,
)

WD_ALIGN_CENTER = 1
WD_ALIGN_LEFT = 0
WD_LINE_SPACE_MULTIPLE = 5
WD_STYLE_HEADING_1 = -2  # not used; use name
WD_OUTLINE_LEVEL_BODY = 10

SZ_ABSTRACT_TITLE = 18  # 小二号
SZ_BODY = 12  # 小四号
SZ_KEYWORD_LABEL = 12
LINE_SPACING = 1.25
FIRST_LINE_CHARS = 2


def _style(doc, names):
    for n in names:
        try:
            return doc.Styles(n)
        except Exception:
            continue
    return None


def _set_songti(rng, size, bold=False):
    rng.Font.Name = "宋体"
    try:
        rng.Font.NameFarEast = "宋体"
    except Exception:
        pass
    rng.Font.Size = size
    rng.Font.Bold = bold


def _set_tnr(rng, size, bold=False):
    rng.Font.Name = "Times New Roman"
    rng.Font.Size = size
    rng.Font.Bold = bold


def _body_paragraph_format(pf):
    pf.Alignment = WD_ALIGN_LEFT
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = LINE_SPACING
    pf.SpaceBefore = 0
    pf.SpaceAfter = 0
    try:
        pf.CharacterUnitFirstLineIndent = FIRST_LINE_CHARS
    except Exception:
        pf.FirstLineIndent = SZ_BODY * 2


def format_abstract_and_keywords(doc):
    """4.9 摘要、关键词、英文摘要。"""
    in_abstract_body = False
    in_english_body = False
    for i in range(1, doc.Paragraphs.Count + 1):
        p = doc.Paragraphs(i)
        t = p.Range.Text.strip().replace("\r", "").replace("\x07", "")
        if not t:
            continue
        t_compact = re.sub(r"\s+", "", t)

        if t_compact == "摘要" or t == "摘 要":
            in_abstract_body = True
            in_english_body = False
            r = p.Range
            _set_songti(r, SZ_ABSTRACT_TITLE, bold=True)
            p.Format.Alignment = WD_ALIGN_CENTER
            p.Format.SpaceBefore = 0
            p.Format.SpaceAfter = 0
            p.Format.FirstLineIndent = 0
            try:
                p.Format.CharacterUnitFirstLineIndent = 0
            except Exception:
                pass
            continue

        if t_compact == "ABSTRACT" or t.upper() == "ABSTRACT":
            in_abstract_body = False
            in_english_body = True
            r = p.Range
            _set_tnr(r, SZ_ABSTRACT_TITLE, bold=True)
            p.Format.Alignment = WD_ALIGN_CENTER
            p.Format.FirstLineIndent = 0
            continue

        if t.startswith("关键词") or t.startswith("关键词："):
            in_abstract_body = False
            rng = p.Range
            colon = t.find("：")
            if colon < 0:
                colon = t.find(":")
            if colon > 0:
                label = rng.Duplicate
                label.SetRange(rng.Start, rng.Start + colon + 1)
                _set_songti(label, SZ_KEYWORD_LABEL, bold=True)
                body = rng.Duplicate
                body.SetRange(label.End, rng.End)
                _set_songti(body, SZ_BODY, bold=False)
            else:
                _set_songti(rng, SZ_BODY, bold=False)
            p.Format.Alignment = WD_ALIGN_LEFT
            p.Format.FirstLineIndent = 0
            try:
                p.Format.CharacterUnitFirstLineIndent = 0
            except Exception:
                pass
            continue

        if t.startswith("Keywords") or t.startswith("KeyWords"):
            in_english_body = False
            rng = p.Range
            colon = t.find(":")
            if colon > 0:
                label = rng.Duplicate
                label.SetRange(rng.Start, rng.Start + colon + 1)
                _set_tnr(label, SZ_KEYWORD_LABEL, bold=True)
                body = rng.Duplicate
                body.SetRange(label.End, rng.End)
                _set_tnr(body, SZ_BODY, bold=False)
            p.Format.FirstLineIndent = 0
            continue

        if t.startswith("目") and "录" in t:
            in_abstract_body = False
            in_english_body = False
            continue

        if in_abstract_body and len(t) > 20:
            _set_songti(p.Range, SZ_BODY, bold=False)
            _body_paragraph_format(p.Format)
            continue

        if in_english_body and len(t) > 20:
            _set_tnr(p.Range, SZ_BODY, bold=False)
            _body_paragraph_format(p.Format)
            continue


def apply_outline_heading_styles(doc):
    """4.3 章/节/条：按标题样式套用（已用标题样式的段落重刷）。"""
    h1 = _style(doc, ["Heading 1", "标题 1", "标题1"])
    h2 = _style(doc, ["Heading 2", "标题 2"])
    h3 = _style(doc, ["Heading 3", "标题 3"])
    normal = _style(doc, ["Normal", "正文"])

    re_chapter = re.compile(r"^第[一二三四五六七八九十百千]+章\s+")
    re_section = re.compile(r"^\d+\.\d+\s+\S")
    re_subsection = re.compile(r"^\d+\.\d+\.\d+\s+\S")
    re_table_cap = re.compile(r"^表\s*\d")

    for i in range(1, doc.Paragraphs.Count + 1):
        p = doc.Paragraphs(i)
        t = p.Range.Text.strip().replace("\r", "").replace("\x07", "")
        if not t or len(t) > 120:
            continue
        if re_table_cap.match(t):
            p.Format.Alignment = WD_ALIGN_CENTER
            _set_songti(p.Range, SZ_BODY, bold=False)
            p.Format.FirstLineIndent = 0
            try:
                p.Format.CharacterUnitFirstLineIndent = 0
            except Exception:
                pass
            continue
        if re_chapter.match(t) and h1:
            p.Style = h1
            p.Range.Font.Size = h1.Font.Size
            p.Range.Font.Bold = True
            continue
        if re_subsection.match(t) and h3:
            p.Style = h3
            p.Range.Font.Size = h3.Font.Size
            p.Range.Font.Bold = True
            continue
        if re_section.match(t) and h2:
            p.Style = h2
            p.Range.Font.Size = h2.Font.Size
            p.Range.Font.Bold = True
            continue


def _refresh_heading_paragraphs_only(doc):
    """仅重刷已套用标题 1～3 的段落，避免全文遍历过慢。"""
    keys = [
        (["Heading 1", "标题 1", "标题1"], None),
        (["Heading 2", "标题 2", "标题2"], None),
        (["Heading 3", "标题 3", "标题3"], None),
    ]
    for i in range(1, doc.Paragraphs.Count + 1):
        para = doc.Paragraphs(i)
        try:
            local = para.Style.NameLocal
        except Exception:
            continue
        for names, _ in keys:
            if not any(n in local or local == n for n in names):
                continue
            st = _style(doc, names)
            if st is None:
                break
            try:
                para.Style = st
                para.Range.Font.Name = st.Font.Name
                para.Range.Font.Size = st.Font.Size
                para.Range.Font.Bold = st.Font.Bold
            except Exception:
                pass
            break


def format_tables_three_line(doc):
    """4.5 表格：尽量设为三线表（顶线、表头下线、底线）。"""
    wd_line_style_single = 1
    try:
        for ti in range(1, doc.Tables.Count + 1):
            tbl = doc.Tables(ti)
            tbl.Rows.AllowBreakAcrossPages = False
            try:
                tbl.Borders.Enable = False
            except Exception:
                pass
            tbl.Rows(1).Borders(wd_line_style_single).LineStyle = 1
            tbl.Rows(1).Borders(wd_line_style_single).LineWidth = 6
            if tbl.Rows.Count > 1:
                tbl.Rows(2).Borders(wd_line_style_single).LineStyle = 1
                tbl.Rows(2).Borders(wd_line_style_single).LineWidth = 4
            tbl.Rows(tbl.Rows.Count).Borders(wd_line_style_single).LineStyle = 1
            tbl.Rows(tbl.Rows.Count).Borders(wd_line_style_single).LineWidth = 6
    except Exception as e:
        print("warn: tables", e)


def main():
    if len(sys.argv) < 2:
        print("usage: _thesis_word_format_full.py <论文.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])
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
        print("1 page setup")
        configure_page_setup_all_sections(doc)
        print("2 styles")
        configure_document_styles(doc)
        print("3 abstract/keywords")
        format_abstract_and_keywords(doc)
        print("4 headings")
        apply_outline_heading_styles(doc)
        # 全文逐段 sync 极慢，仅刷新章/节/条标题段落
        print("5 refresh heading paragraphs")
        _refresh_heading_paragraphs_only(doc)
        print("6 headers/footers")
        if doc.Sections.Count >= 3:
            configure_section1_cover_only(doc.Sections(1))
            configure_section2_abstract_through_toc(doc.Sections(2))
            configure_section3_body(doc.Sections(3))
        print("7 tables")
        format_tables_three_line(doc)
        try:
            doc.Fields.Update()
        except Exception:
            pass
        doc.Save()
        print("OK", path)
    finally:
        if doc:
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
