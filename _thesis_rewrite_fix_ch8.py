# -*- coding: utf-8 -*-
"""删除第八章结论后残留的旧模板句。"""
import os
import sys

from docx import Document

REMOVE_PREFIXES = (
    "实现了基于线性预测的",
    "采用三层感知机制",
    "本系统的设计与实现为同类高速机甲战斗游戏",
)


def delete_paragraph(paragraph):
    el = paragraph._element
    el.getparent().remove(el)


def main():
    path = os.path.abspath(sys.argv[1])
    doc = Document(path)
    to_remove = []
    in_ch8 = False
    for i, para in enumerate(doc.paragraphs):
        t = para.text.strip()
        if t.startswith("第八章"):
            in_ch8 = True
            continue
        if in_ch8 and t.startswith("参考文献"):
            break
        if in_ch8 and any(t.startswith(p) for p in REMOVE_PREFIXES):
            to_remove.append(para)
    for para in reversed(to_remove):
        delete_paragraph(para)
    doc.save(path)
    print("removed", len(to_remove), "paragraphs")


if __name__ == "__main__":
    main()
