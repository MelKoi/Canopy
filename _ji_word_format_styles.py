# -*- coding: utf-8 -*-
"""
按理工类论文规范设置 JI.docx 章/节/条/款/正文/附录样式（仅样式与段落格式，不改页眉页脚）。
"""
from __future__ import annotations

import os
import re
import shutil
import sys
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import win32com.client as win32

from _thesis_word_format_apply import (
    SZ_BODY,
    SZ_CHAPTER,
    SZ_SECTION,
    SZ_SUBSECTION,
    configure_document_styles,
)

WD_LINE_SPACE_MULTIPLE = 5
WD_ALIGN_LEFT = 0
WD_ALIGN_CENTER = 1

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


def _body_pf(pf, first_line_chars=2):
    pf.Alignment = WD_ALIGN_LEFT
    pf.LineUnitBefore = 0
    pf.LineUnitAfter = 0
    pf.SpaceBefore = 0
    pf.SpaceAfter = 0
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = 1.25
    try:
        pf.CharacterUnitFirstLineIndent = first_line_chars
    except Exception:
        pf.FirstLineIndent = SZ_BODY * first_line_chars if first_line_chars else 0


def _heading_pf(pf, size_pt, half_line=True):
    if half_line:
        try:
            pf.LineUnitBefore = 0.5
            pf.LineUnitAfter = 0.5
            pf.SpaceBeforeAuto = False
            pf.SpaceAfterAuto = False
        except Exception:
            pf.SpaceBefore = size_pt * 0.25
            pf.SpaceAfter = size_pt * 0.25
    else:
        pf.LineUnitBefore = 0
        pf.LineUnitAfter = 0
        pf.SpaceBefore = 0
        pf.SpaceAfter = 0
    pf.LineSpacingRule = WD_LINE_SPACE_MULTIPLE
    pf.LineSpacing = 1.25
    try:
        pf.CharacterUnitFirstLineIndent = 0
    except Exception:
        pf.FirstLineIndent = 0


def _para_text(p) -> str:
    return p.Range.Text.strip().replace("\r", "").replace("\x07", "")


def _is_toc_style(local: str) -> bool:
    return local.lower().startswith("toc")


def _is_skip_title(t: str, t_compact: str) -> bool:
    if t_compact in ("摘要", "ABSTRACT", "目录", "致谢", "参考文献"):
        return True
    if t == "摘 要" or (t.startswith("目") and "录" in t):
        return True
    if t.startswith("关键词") or t.startswith("Keywords"):
        return True
    return False


def apply_outline_heading_styles(doc):
    h1 = _style(doc, ["Heading 1", "标题 1", "标题1"])
    h2 = _style(doc, ["Heading 2", "标题 2", "标题2"])
    h3 = _style(doc, ["Heading 3", "标题 3", "标题3"])
    h4 = _style(doc, ["Heading 4", "标题 4", "标题4"])

    in_appendix = False
    for i in range(1, doc.Paragraphs.Count + 1):
        p = doc.Paragraphs(i)
        t = _para_text(p)
        if not t or len(t) > 150:
            continue
        t_compact = re.sub(r"\s+", "", t)

        if RE_APPENDIX_CH.match(t):
            in_appendix = True
        if in_appendix and t_compact in ("参考文献", "致谢", "致 谢"):
            in_appendix = False

        try:
            local = p.Style.NameLocal
        except Exception:
            local = ""
        if _is_toc_style(local):
            continue

        if RE_TABLE_CAP.match(t):
            p.Format.Alignment = WD_ALIGN_CENTER
            _set_songti(p.Range, SZ_BODY, bold=False)
            p.Format.FirstLineIndent = 0
            try:
                p.Format.CharacterUnitFirstLineIndent = 0
            except Exception:
                pass
            continue

        st = None
        size = SZ_BODY
        half = True
        if RE_CHAPTER.match(t) and h1:
            st, size, half = h1, SZ_CHAPTER, True
        elif in_appendix and RE_APPENDIX_CH.match(t) and h2:
            st, size, half = h2, SZ_SECTION, True
        elif in_appendix and RE_APPENDIX_SEC.match(t) and h3:
            st, size, half = h3, SZ_SUBSECTION, True
        elif RE_SUBSECTION.match(t) and h3:
            st, size, half = h3, SZ_SUBSECTION, True
        elif RE_SECTION.match(t) and h2:
            st, size, half = h2, SZ_SECTION, True
        elif RE_ITEM.match(t) and h4:
            st, size, half = h4, SZ_SUBSECTION, False

        if st is None:
            continue
        try:
            p.Style = st
        except Exception:
            pass
        _set_songti(p.Range, size, bold=True)
        _heading_pf(p.Format, size, half_line=half)
        if not half:
            _body_pf(p.Format, first_line_chars=2)


def _refresh_headings_only(doc):
    keys = [
        ["Heading 1", "标题 1", "标题1"],
        ["Heading 2", "标题 2", "标题2"],
        ["Heading 3", "标题 3", "标题3"],
        ["Heading 4", "标题 4", "标题4"],
    ]
    for i in range(1, doc.Paragraphs.Count + 1):
        para = doc.Paragraphs(i)
        try:
            local = para.Style.NameLocal
        except Exception:
            continue
        for names in keys:
            if not any(n in local or local == n for n in names):
                continue
            st = _style(doc, names)
            if st is None:
                break
            try:
                para.Style = st
            except Exception:
                pass
            break


def apply_body_paragraphs(doc):
    in_appendix = False
    for i in range(1, doc.Paragraphs.Count + 1):
        p = doc.Paragraphs(i)
        t = _para_text(p)
        if not t:
            continue
        t_compact = re.sub(r"\s+", "", t)

        if RE_APPENDIX_CH.match(t):
            in_appendix = True
        if t_compact in ("参考文献", "致谢", "致 谢") and not in_appendix:
            pass
        if t_compact in ("参考文献", "致谢", "致 谢"):
            in_appendix = False

        try:
            local = p.Style.NameLocal
        except Exception:
            continue

        if _is_toc_style(local) or "Heading" in local or "标题" in local:
            continue
        if local not in ("Normal", "正文", "正文文本", "Plain Text"):
            continue
        if _is_skip_title(t, t_compact):
            continue
        if RE_CHAPTER.match(t) or RE_SECTION.match(t) or RE_SUBSECTION.match(t):
            continue
        if RE_APPENDIX_CH.match(t) or RE_APPENDIX_SEC.match(t):
            continue
        if RE_TABLE_CAP.match(t):
            continue
        if RE_ITEM.match(t):
            continue

        _set_songti(p.Range, SZ_BODY, bold=False)
        if in_appendix and (RE_CODE.match(t) or local == "Plain Text"):
            _body_pf(p.Format, first_line_chars=0)
        else:
            _body_pf(p.Format, first_line_chars=2)


def backup_file(path: str) -> str:
    base, ext = os.path.splitext(path)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    dest = f"{base}_排版前备份_{stamp}{ext}"
    shutil.copy2(path, dest)
    return dest


def main():
    if len(sys.argv) < 2:
        print("usage: _ji_word_format_styles.py <JI.docx>", file=sys.stderr)
        sys.exit(2)
    path = os.path.abspath(sys.argv[1])
    if not os.path.isfile(path):
        print("not found:", path, file=sys.stderr)
        sys.exit(1)

    bak = backup_file(path)
    print("backup:", bak)

    app = win32.Dispatch("Word.Application")
    app.Visible = False
    try:
        app.DisplayAlerts = 0
    except Exception:
        pass

    doc = None
    try:
        doc = app.Documents.Open(path, ReadOnly=False, AddToRecentFiles=False)
        print("1 configure styles")
        configure_document_styles(doc)
        print("2 assign heading levels by outline")
        apply_outline_heading_styles(doc)
        print("3 body paragraphs")
        apply_body_paragraphs(doc)
        print("4 refresh heading styles only")
        _refresh_headings_only(doc)
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
