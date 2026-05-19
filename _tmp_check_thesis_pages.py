# -*- coding: utf-8 -*-
import os
import sys

import win32com.client as win32

MM_TO_PT = 72.0 / 25.4
HEADER_TEXT = "湖南科技大学本科生毕业设计（论文）"
SZ_WUHAO = 10.5  # 五号


def mm_from_pt(pt):
    return round(pt / MM_TO_PT, 1)


def main():
    path = os.path.abspath(
        r"C:\Users\Narrator-鲤\Downloads\基于Unity3d的高速战斗系统制作.docx"
    )
    app = win32.Dispatch("Word.Application")
    app.Visible = False
    try:
        app.DisplayAlerts = 0
    except Exception:
        pass
    try:
        doc = app.Documents.Open(
            FileName=path,
            ReadOnly=True,
            AddToRecentFiles=False,
            Visible=False,
        )
    except Exception as e:
        print("Open failed:", e)
        app.Quit()
        sys.exit(1)
    if doc is None:
        print("Open returned None")
        app.Quit()
        sys.exit(1)
    try:
        print("FILE:", path)
        print("SECTIONS:", doc.Sections.Count)
        for si in range(1, doc.Sections.Count + 1):
            sect = doc.Sections(si)
            ps = sect.PageSetup
            print(f"\n--- Section {si} ---")
            print("  Top/Bottom/Left/Right mm:",
                  mm_from_pt(ps.TopMargin), mm_from_pt(ps.BottomMargin),
                  mm_from_pt(ps.LeftMargin), mm_from_pt(ps.RightMargin))
            print("  Header/Footer distance mm:",
                  mm_from_pt(ps.HeaderDistance), mm_from_pt(ps.FooterDistance))
            try:
                print("  PaperSize (7=A4):", ps.PaperSize)
            except Exception:
                pass
            h = sect.Headers(1)
            ft = sect.Footers(1)
            hr = h.Range.Text.replace("\x07", "").replace("\r", "\\r").strip()
            fr = ft.Range.Text.replace("\x07", "").replace("\r", "\\r").strip()
            print("  Header LinkToPrevious:", h.LinkToPrevious)
            print("  Header:", repr(hr[:100]))
            if hr:
                print("  Header font:", h.Range.Font.Name, h.Range.Font.Size,
                      "align", h.Range.ParagraphFormat.Alignment)
            print("  Footer:", repr(fr[:100]))
            if fr or ft.Range.Fields.Count:
                print("  Footer font:", ft.Range.Font.Name, ft.Range.Font.Size,
                      "align", ft.Range.ParagraphFormat.Alignment)
            for fi in range(1, ft.Range.Fields.Count + 1):
                f = ft.Range.Fields(fi)
                print("   Field:", f.Code.Text.strip(), "=>", repr(f.Result.Text.strip()))
            try:
                pns = ft.PageNumbers
                print("  PageNumbers.Count:", pns.Count,
                      "Style:", pns.NumberStyle if pns.Count else "-",
                      "Restart:", pns.RestartNumberingAtSection if pns.Count else "-",
                      "Start:", pns.StartingNumber if pns.Count else "-")
            except Exception as e:
                print("  PageNumbers err:", e)

        markers = ["摘 要", "目 录", "第一章", "前言", "参考文献"]
        print("\n--- Markers (section, page) ---")
        for text in markers:
            rng = doc.Content
            f = rng.Find
            f.ClearFormatting()
            f.Text = text
            f.Forward = True
            f.Wrap = 0
            if f.Execute():
                sec = rng.Information(2)
                page = rng.Information(3)
                print(f"  {text!r}: section={sec}, page={page}")
            else:
                print(f"  {text!r}: NOT FOUND")
    finally:
        doc.Close(False)
        app.Quit()


if __name__ == "__main__":
    main()
