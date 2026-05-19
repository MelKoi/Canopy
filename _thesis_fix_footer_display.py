# -*- coding: utf-8 -*-
"""
修复页脚页码“只显示横线、数字不显示”：去掉 NumberInDash API，
改为「-」+ PAGE 域 +「-」，全文 Times New Roman 五号居中，并重启各节页码。
"""
from __future__ import annotations

import os
import sys

import win32com.client as win32

WD_ALIGN_PARAGRAPH_CENTER = 1
WD_COLLAPSE_END = 0
WD_COLLAPSE_START = 1
WD_HEADER_FOOTER_PRIMARY = 1
WD_FIELD_EMPTY = -1
WD_SECTION_START_NEW_PAGE = 2

FOOTER_FONT = "Times New Roman"
FOOTER_SIZE = 10.5

HEADER_TEXT = "湖南科技大学本科生毕业设计（论文）"
HEADER_FONT = "宋体"
HEADER_SIZE = 10.5


def _clear_footer(ft):
    ft.LinkToPrevious = False
    rng = ft.Range
    try:
        while rng.Fields.Count > 0:
            rng.Fields(1).Delete()
    except Exception:
        pass
    rng.Text = ""
    rng.ParagraphFormat.Alignment = WD_ALIGN_PARAGRAPH_CENTER
    rng.ParagraphFormat.SpaceBefore = 0
    rng.ParagraphFormat.SpaceAfter = 0


def _apply_footer_font(rng):
    rng.Font.Name = FOOTER_FONT
    rng.Font.Size = FOOTER_SIZE
    rng.Font.Bold = False
    try:
        rng.Font.NameAscii = FOOTER_FONT
        rng.Font.NameOther = FOOTER_FONT
    except Exception:
        pass
    try:
        rng.Font.Color = 0  # wdColorAutomatic / black
    except Exception:
        pass


def _build_dash_page_footer(sect, roman: bool):
    """页脚格式：- 1 - 或 - i -，中间为 PAGE 域。"""
    _clear_footer(sect.Footers(WD_HEADER_FOOTER_PRIMARY))
    ft = sect.Footers(WD_HEADER_FOOTER_PRIMARY)

    try:
        sect.PageSetup.SectionStart = WD_SECTION_START_NEW_PAGE
    except Exception:
        pass

    pns = ft.PageNumbers
    try:
        pns.RestartNumberingAtSection = True
        pns.StartingNumber = 1
    except Exception:
        pass

    rng = ft.Range
    rng.Text = ""
    rng.Collapse(WD_COLLAPSE_START)

    # 前半横线（ASCII 连字符，避免 en-dash 显示成细线）
    rng.InsertAfter("- ")
    _apply_footer_font(rng)

    pos = rng.End
    rng.Collapse(WD_COLLAPSE_END)
    code = r"PAGE \* roman \* MERGEFORMAT" if roman else r"PAGE \* Arabic \* MERGEFORMAT"
    fld = rng.Fields.Add(rng, WD_FIELD_EMPTY, code, False)
    try:
        fld.Update()
    except Exception:
        pass

    rng.Collapse(WD_COLLAPSE_END)
    rng.InsertAfter(" -")
    _apply_footer_font(ft.Range)

    # 整段页脚统一字体（含域结果）
    whole = ft.Range
    _apply_footer_font(whole)
    whole.ParagraphFormat.Alignment = WD_ALIGN_PARAGRAPH_CENTER

    for i in range(1, whole.Fields.Count + 1):
        try:
            whole.Fields(i).Update()
        except Exception:
            pass


def _setup_header(sect, link_previous: bool):
    h = sect.Headers(WD_HEADER_FOOTER_PRIMARY)
    h.LinkToPrevious = link_previous
    if link_previous:
        return
    r = h.Range
    r.Text = HEADER_TEXT
    r.ParagraphFormat.Alignment = WD_ALIGN_PARAGRAPH_CENTER
    r.Font.Name = HEADER_FONT
    r.Font.Size = HEADER_SIZE
    try:
        r.Font.NameFarEast = HEADER_FONT
    except Exception:
        pass


def _clear_header(sect):
    for idx in (WD_HEADER_FOOTER_PRIMARY, 2):
        try:
            h = sect.Headers(idx)
            h.LinkToPrevious = False
            h.Range.Text = ""
        except Exception:
            pass


def main():
    if len(sys.argv) < 2:
        print("usage: _thesis_fix_footer_display.py <论文.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])

    app = win32.Dispatch("Word.Application")
    app.Visible = False
    try:
        app.DisplayAlerts = 0
    except Exception:
        pass

    doc = None
    try:
        doc = app.Documents.Open(path, ReadOnly=False, AddToRecentFiles=False)
        n = doc.Sections.Count
        if n < 3:
            print("warn: sections=", n, "run _thesis_fix_page_numbers.py first", file=sys.stderr)

        for si in range(1, n + 1):
            sect = doc.Sections(si)
            try:
                sect.PageSetup.DifferentFirstPageHeaderFooter = False
            except Exception:
                pass

        # 第 1 节：无页眉页脚
        _clear_header(doc.Sections(1))
        _clear_footer(doc.Sections(1).Footers(WD_HEADER_FOOTER_PRIMARY))

        if n >= 2:
            _setup_header(doc.Sections(2), link_previous=False)
            _build_dash_page_footer(doc.Sections(2), roman=True)

        if n >= 3:
            _setup_header(doc.Sections(3), link_previous=True)
            _build_dash_page_footer(doc.Sections(3), roman=False)

        try:
            doc.Fields.Update()
        except Exception:
            pass
        try:
            doc.Repaginate()
        except Exception:
            pass

        doc.Save()
        print("OK", path)
        for si in range(1, doc.Sections.Count + 1):
            ft = doc.Sections(si).Footers(1).Range
            print(
                f"  sec{si}",
                repr(ft.Text.replace("\r", "\\r").replace("\x07", "")),
                "font",
                ft.Font.Name,
                ft.Font.Size,
            )
            for i in range(1, ft.Fields.Count + 1):
                f = ft.Fields(i)
                print("   ", f.Code.Text.strip(), "=>", repr(f.Result.Text))
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
