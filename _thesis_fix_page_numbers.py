# -*- coding: utf-8 -*-
"""
修复页眉页脚与页码（对照理工类规范 4.1）：
  第1节 封面：无页眉、无页码；
  第2节 摘要～目录：页眉学校名；页脚小写罗马 -i- …；TNR 五号居中；
  第3节 正文：页眉同前；页脚阿拉伯 -1- … 自 1 起。
并确保共 3 个分节，更新域，切换为页面视图以便看到页脚。
"""
from __future__ import annotations

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import win32com.client as win32

from _thesis_word_header_footer import (
    configure_section1_cover_only,
    configure_section2_abstract_through_toc,
    configure_section3_body,
    relocate_first_section_break_after_cover,
)

WD_SECTION_BREAK_NEXT_PAGE = 2
WD_COLLAPSE_START = 1
WD_FIND_STOP = 0
WD_HEADER_FOOTER_PRIMARY = 1
WD_FIELD_EMPTY = -1
WD_PRINT_VIEW = 3


def _para_text(p) -> str:
    return p.Range.Text.strip().replace("\r", "").replace("\x07", "")


def find_paragraph(doc, predicate, start=1):
    for i in range(start, doc.Paragraphs.Count + 1):
        if predicate(doc.Paragraphs(i), i):
            return i
    return None


def insert_section_break_before_paragraph(doc, para_index: int):
    rng = doc.Paragraphs(para_index).Range
    rng.Collapse(WD_COLLAPSE_START)
    rng.InsertBreak(WD_SECTION_BREAK_NEXT_PAGE)


def ensure_three_sections(doc):
    """保证：封面末、摘要前、第一章前各有一处分节（共 3 节）。"""
    if doc.Sections.Count >= 3:
        return

    # 在「摘 要」前分节（若仍只有 1 节或摘要与封面同节）
    if doc.Sections.Count < 2:
        pi = find_paragraph(
            doc,
            lambda p, _: _para_text(p).replace(" ", "") in ("摘要", "摘要")
            or _para_text(p) == "摘 要",
        )
        if pi:
            insert_section_break_before_paragraph(doc, pi)

    if doc.Sections.Count < 3:
        def is_chapter1(p, _):
            t = _para_text(p)
            if not t or len(t) > 50 or "......" in t or "\n" in t:
                return False
            if not re.match(r"^第[一二三四五六七八九十百千]+章", t):
                return False
            try:
                sn = p.Style.NameLocal
                if "标题 1" in sn or "Heading 1" in sn:
                    return True
            except Exception:
                pass
            return "前言" in t or len(t) <= 25

        ci = find_paragraph(doc, is_chapter1)
        if ci:
            insert_section_break_before_paragraph(doc, ci)

    if doc.Sections.Count < 3:
        raise RuntimeError(
            f"分节后仍为 {doc.Sections.Count} 节，请检查是否存在「摘 要」「第一章」标题。"
        )


def ensure_three_sections_from_path(path: str):
    relocate_first_section_break_after_cover(path)


def set_footer_page_field_roman(sect):
    """页脚：- i - 形式，使用 PAGE \\* roman 域（避免 PageNumbers API 异常）。"""
    ft = sect.Footers(WD_HEADER_FOOTER_PRIMARY)
    ft.LinkToPrevious = False
    r = ft.Range
    r.Text = ""
    r.ParagraphFormat.Alignment = 1
    r.Font.Name = "Times New Roman"
    r.Font.Size = 10.5
    r.InsertAfter("-")
    r.Collapse(0)
    r.Fields.Add(r, WD_FIELD_EMPTY, r"PAGE \\* roman \\* MERGEFORMAT", False)
    r.Collapse(0)
    r.InsertAfter("-")
    r.ParagraphFormat.Alignment = 1
  # 本节页码从 1 起
    try:
        sect.PageSetup.SectionStart = 2  # wdSectionNewPage
    except Exception:
        pass
    try:
        ft.PageNumbers.RestartNumberingAtSection = True
        ft.PageNumbers.StartingNumber = 1
    except Exception:
        pass


def set_footer_page_field_arabic_dash(sect):
    """页脚：- 1 - 形式，NumberInDash 或手写短横线+PAGE。"""
    ft = sect.Footers(WD_HEADER_FOOTER_PRIMARY)
    ft.LinkToPrevious = False
    r = ft.Range
    r.Text = ""
    r.ParagraphFormat.Alignment = 1
    r.Font.Name = "Times New Roman"
    r.Font.Size = 10.5
    pns = ft.PageNumbers
    try:
        pns.RestartNumberingAtSection = True
        pns.StartingNumber = 1
    except Exception:
        pass
    try:
        pns.Add(PageNumberAlignment=1)
        pns.NumberStyle = 57  # wdPageNumberStyleNumberInDash
    except Exception:
        r.InsertAfter("- ")
        r.Collapse(0)
        r.Fields.Add(r, WD_FIELD_EMPTY, "PAGE", False)
        r.Collapse(0)
        r.InsertAfter(" -")
    r.ParagraphFormat.Alignment = 1
    for fld in r.Fields:
        try:
            fld.Update()
        except Exception:
            pass


def apply_all_sections(doc):
    n = doc.Sections.Count
    if n < 3:
        raise RuntimeError(f"需要 3 节，当前 {n}")
    configure_section1_cover_only(doc.Sections(1))
    configure_section2_abstract_through_toc(doc.Sections(2))
    # 若 PageNumbers 仍显示阿拉伯，改用域方式罗马
    try:
        t = doc.Sections(2).Footers(1).Range.Text.strip().lower()
        if "1" in t and "i" not in t and "ii" not in t:
            set_footer_page_field_roman(doc.Sections(2))
    except Exception:
        set_footer_page_field_roman(doc.Sections(2))
    configure_section3_body(doc.Sections(3))
    try:
        set_footer_page_field_arabic_dash(doc.Sections(3))
    except Exception:
        pass


def prepare_view_for_user(doc):
    try:
        doc.ActiveWindow.View.Type = WD_PRINT_VIEW
        doc.ActiveWindow.View.ShowFootersAndEndnotes = True
        doc.ActiveWindow.View.SeekView = 0  # wdSeekMainDocument
    except Exception:
        pass


def main():
    if len(sys.argv) < 2:
        print("usage: _thesis_fix_page_numbers.py <论文.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])

    ensure_three_sections_from_path(path)

    app = win32.Dispatch("Word.Application")
    app.Visible = False
    try:
        app.DisplayAlerts = 0
    except Exception:
        pass

    doc = None
    try:
        doc = app.Documents.Open(path, ReadOnly=False, AddToRecentFiles=False)
        ensure_three_sections(doc)
        apply_all_sections(doc)
        try:
            doc.Fields.Update()
        except Exception:
            pass
        prepare_view_for_user(doc)
        doc.Save()
        print("OK sections=", doc.Sections.Count, path)
        for si in range(1, doc.Sections.Count + 1):
            ft = doc.Sections(si).Footers(1).Range.Text.replace("\x07", "").strip()[:40]
            ht = doc.Sections(si).Headers(1).Range.Text.replace("\x07", "").strip()[:40]
            print(f"  sec{si} footer={ft!r} header={ht!r}")
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
