# -*- coding: utf-8 -*-
"""
修正「目录」：删除手工粘贴的目录段，插入 Word 自动目录（TOC）并更新域。
规范：目 录 小二号宋体加粗居中；目录条目共用小四宋体、引导符点线、页码右对齐。
"""
import os
import re
import sys

import win32com.client as win32

WD_COLLAPSE_END = 0
WD_ALIGN_CENTER = 1
WD_TAB_LEADER_DOTS = 2
SZ_TOC_TITLE = 18  # 小二号
SZ_TOC_ENTRY = 12  # 小四号


def _set_songti(rng, size, bold=False):
    rng.Font.Name = "宋体"
    try:
        rng.Font.NameFarEast = "宋体"
    except Exception:
        pass
    rng.Font.Size = size
    rng.Font.Bold = bold


def find_toc_title_index(doc):
    for i in range(1, doc.Paragraphs.Count + 1):
        t = doc.Paragraphs(i).Range.Text.strip().replace("\r", "").replace("\x07", "")
        if t.replace(" ", "") == "目录" or t == "目 录":
            return i
    return None


def find_body_start_index(doc, after_idx):
    """正文第一章标题：须为短标题行或已套用标题 1，排除目录里含「第一章」的长段。"""
    for i in range(after_idx + 1, doc.Paragraphs.Count + 1):
        p = doc.Paragraphs(i)
        t = p.Range.Text.strip().replace("\r", "").replace("\x07", "")
        if not t:
            continue
        if "......" in t or "\n" in t or len(t) > 80:
            continue
        try:
            sn = p.Style.NameLocal
            if ("标题 1" in sn or "Heading 1" in sn) and t.startswith("第"):
                return i
        except Exception:
            pass
        if re.match(r"^第[一二三四五六七八九十百千]+章\s+\S", t) and len(t) <= 40:
            return i
    return None


def format_toc_styles(doc):
    for name, size in [("TOC 1", SZ_TOC_ENTRY), ("TOC 2", SZ_TOC_ENTRY), ("TOC 3", SZ_TOC_ENTRY)]:
        try:
            st = doc.Styles(name)
            _set_songti(st.Font, size, bold=False)
            st.ParagraphFormat.LineSpacingRule = 5
            st.ParagraphFormat.LineSpacing = 1.25
        except Exception:
            pass


def main():
    if len(sys.argv) < 2:
        print("usage: _thesis_fix_toc.py <论文.docx>", file=sys.stderr)
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

        ti = find_toc_title_index(doc)
        if ti is None:
            raise RuntimeError("未找到「目录」标题段落")

        bi = find_body_start_index(doc, ti)
        if bi is None:
            raise RuntimeError("未找到「第一章」起始段落")

        title_p = doc.Paragraphs(ti)
        _set_songti(title_p.Range, SZ_TOC_TITLE, bold=True)
        title_p.Format.Alignment = WD_ALIGN_CENTER
        title_p.Format.FirstLineIndent = 0
        try:
            title_p.Format.CharacterUnitFirstLineIndent = 0
        except Exception:
            pass

        # 删除标题与第一章之间的手工目录内容
        rng_del = doc.Range(title_p.Range.End, doc.Paragraphs(bi).Range.Start)
        if rng_del.Text.strip():
            rng_del.Delete()

        # 删除已有 TOC 对象（若有）
        while doc.TablesOfContents.Count > 0:
            doc.TablesOfContents(1).Delete()

        ins = doc.Range(title_p.Range.End, title_p.Range.End)
        ins.InsertParagraphAfter()
        ins = doc.Range(title_p.Range.End, title_p.Range.End)
        ins.Collapse(WD_COLLAPSE_END)

        toc = doc.TablesOfContents.Add(
            ins,
            True,
            1,
            3,
            False,
            True,
            True,
        )
        try:
            toc.TabLeader = WD_TAB_LEADER_DOTS
        except Exception:
            pass
        toc.Update()

        try:
            format_toc_styles(doc)
        except Exception as ex:
            print("warn: toc styles", ex)

        try:
            doc.Fields.Update()
        except Exception:
            pass

        doc.Save()
        print("OK TOC rebuilt", path, "title_para", ti, "body_para", bi)
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
