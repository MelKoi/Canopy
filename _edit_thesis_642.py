# -*- coding: utf-8 -*-
"""Replace thesis section 6.4.2 pseudo-code with 思路/落地期望/结果后续 (project-grounded)."""
import sys

from docx import Document
from docx.oxml.ns import qn
from docx.oxml import OxmlElement


def paragraph_text(p_el):
    parts = []
    for t in p_el.iter(qn("w:t")):
        if t.text:
            parts.append(t.text)
    return "".join(parts)


def new_paragraph():
    return OxmlElement("w:p")


def add_text_run(p_el, text, bold=False):
    r = OxmlElement("w:r")
    if bold:
        rpr = OxmlElement("w:rPr")
        rpr.append(OxmlElement("w:b"))
        r.append(rpr)
    t = OxmlElement("w:t")
    t.set(qn("xml:space"), "preserve")
    t.text = text
    r.append(t)
    p_el.append(r)


def insert_block_before_anchor(body, anchor_el, lines_doc_top_to_bottom):
    idx = list(body).index(anchor_el)
    for text, bold in reversed(lines_doc_top_to_bottom):
        p = new_paragraph()
        add_text_run(p, text, bold=bold)
        body.insert(idx, p)


def find_para_el(doc, starts_with_space_suffix, must_contain):
    """starts_with_space_suffix e.g. '6.4.2 ' to avoid matching 6.4.21."""
    for el in doc.element.body:
        if el.tag != qn("w:p"):
            continue
        tx = paragraph_text(el).strip().replace("\u00a0", " ")
        if not tx.startswith(starts_with_space_suffix):
            continue
        if must_contain not in tx:
            continue
        if "\n" in tx or len(tx) > 160:
            continue
        return el
    return None


def main():
    if len(sys.argv) < 2:
        print("usage: _edit_thesis_642.py <input.docx> [output.docx]", file=sys.stderr)
        sys.exit(2)
    path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else path
    doc = Document(path)
    body = doc.element.body

    el_h = find_para_el(doc, "6.4.2 ", "状态机")
    el_next = find_para_el(doc, "6.4.3 ", "硬直")
    if el_h is None or el_next is None:
        print("Could not find 6.4.2 or 6.4.3 headings", file=sys.stderr)
        sys.exit(1)

    children = list(body)
    i_h = children.index(el_h)
    i_n = children.index(el_next)
    if i_n <= i_h:
        print("bad order", i_h, i_n, file=sys.stderr)
        sys.exit(1)

    for j in range(i_n - 1, i_h, -1):
        body.remove(children[j])

    blocks = [
        (
            "思路：表 6.2 中的待机/追击/攻击可理解为「巡逻推进—接战挂起—按冷却射击」三段；硬直与死亡则对应受击反馈与销毁时机。"
            "工程上不必把所有分支塞进单一 Update(switch)，而是用职责清晰的组件拼出等价行为，便于单独调参与复用。",
            False,
        ),
        (
            "落地期望：地面单位由 EnemyPatrolAgent 沿 EnemyPatrolPath 折线移动；"
            "同物体若挂 TestEnemyCombat 等并实现 IEnemyPatrolSuspendCondition，在感知与视线满足时 ShouldSuspendPatrol 为真，巡逻暂停并在 Update 中走开火间隔。"
            "飞机由 PlaneDistanceHoverAI 维持与玩家 Mesh 的水平环绕距离与目标高度，PlaneCombat 在接战距离内驱动 GUN 齐射与主炮爆发/冷却。"
            "追击式位移在飞机侧体现在 HoverAI 对目标点的连续修正，而非论文示例里对 transform.position 的直接 +=。",
            False,
        ),
        (
            "结果后续：若要将五态与代码标识一一对应，可在外包一层薄枚举状态机，仅负责转换条件，"
            "具体移动与射击仍委托现有组件，避免推倒重来；测试关可继续以 TestEnemyCombat 为基线快速迭代，再迁移到正式敌人预制体。",
            False,
        ),
    ]

    insert_block_before_anchor(body, el_next, blocks)
    doc.save(out)
    print("OK saved", out)


if __name__ == "__main__":
    main()
