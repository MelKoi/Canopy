# -*- coding: utf-8 -*-
"""Replace thesis sections 6.2–6.3 code blocks with project-grounded 思路/落地期望/结果后续."""
import sys
from copy import deepcopy

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
    """Insert so final reading order matches lines_doc_top_to_bottom (each item: text, bold)."""
    idx = list(body).index(anchor_el)
    for text, bold in reversed(lines_doc_top_to_bottom):
        p = new_paragraph()
        add_text_run(p, text, bold=bold)
        body.insert(idx, p)


def find_para_element_by_text_start(doc, prefix, must_contain=None):
    body = doc.element.body
    for el in body:
        if el.tag != qn("w:p"):
            continue
        tx = paragraph_text(el).strip().replace("\u00a0", " ")
        # 例如 "6.2 " 避免匹配到 "6.2.1"
        if not tx.startswith(prefix + " "):
            continue
        if must_contain and must_contain not in tx:
            continue
        if "\n" in tx or len(tx) > 160:
            continue
        return el
    return None


def main():
    if len(sys.argv) < 2:
        print("usage: _edit_thesis_62_63.py <input.docx> [output.docx]", file=sys.stderr)
        sys.exit(2)
    path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else path
    doc = Document(path)
    body = doc.element.body

    el_62 = find_para_element_by_text_start(doc, "6.2", "敌人锁定玩家系统")
    el_64 = find_para_element_by_text_start(doc, "6.4", "敌人行为状态机")
    if el_62 is None or el_64 is None:
        print("Could not find 6.2 or 6.4 heading elements", file=sys.stderr)
        sys.exit(1)

    children = list(body)
    i62 = children.index(el_62)
    i64 = children.index(el_64)
    if i64 <= i62:
        print("bad order", i62, i64, file=sys.stderr)
        sys.exit(1)

    # 保留「表6.1」题注段之后的 Word 表格（与段落混排在 body 中）
    table_after_61 = None
    for j in range(i62 + 1, i64):
        el, nxt = children[j], children[j + 1] if j + 1 < i64 else None
        if el.tag != qn("w:p") or nxt is None or nxt.tag != qn("w:tbl"):
            continue
        if paragraph_text(el).strip() in ("表6.1", "表 6.1"):
            table_after_61 = deepcopy(nxt)
            break

    # Remove everything strictly after 6.2 heading until (not including) 6.4
    for j in range(i64 - 1, i62, -1):
        body.remove(children[j])

    anchor = el_64  # insert new content immediately before 6.4

    def triplet(heading, idea, deploy, follow):
        return [
            (heading, True),
            ("思路：" + idea, False),
            ("落地期望：" + deploy, False),
            ("结果后续：" + follow, False),
        ]

    blocks = []

    # 6.2.1
    blocks.extend(
        triplet(
            "6.2.1 距离检测",
            "接战半径把三维空间中的威胁距离压缩成可调的「是否值得进入射击/停巡逻辑」布尔条件，避免全图索敌带来的噪声与性能浪费。",
            "《天幕》测试关小兵在 TestEnemyCombat 中以 detectionRadius（默认约 5m 量级，可按预制体调）对比机甲根 Transform 与可选瞄准高度偏移；飞机 Boss 在 PlaneCombat 中以 engagementDistanceMeters 对比机体与玩家瞄准点（优先 Mesh/Body/DuZi）的直线距离，距离外不打火、不消耗主武器爆发计数。",
            "该实现与论文中「距离门」一致但数值随关卡体量配置；后续若统一 AI 表，可把两类半径收敛到 ScriptableObject 行并与关卡刷怪表对齐，便于复测与论文数据回填。",
        )
    )

    # 6.2.2 — 工程以朝向门闩为主，视锥叙述对齐实现
    blocks.extend(
        triplet(
            "6.2.2 视锥与朝向门闩",
            "纯距离锁定在转角战点会出现「背后开火」的不公平感；工业上常用前向半角或炮口指向夹角约束首发。",
            "工程里 TestEnemyCombat 可选 facePlayerWhenEngaged 水平转向玩家，并可用 requireFacingToFire + fireFacingMaxAngleDeg，以 enemyfront（或机体 forward）与指向玩家的水平方向夹角作为开火门闩；这与教材式 Vector3.Angle(transform.forward, dir) 的视锥判定同构，只是默认更偏「炮口对准再射」。PlaneCombat 当前以距离接战为主，未单独做半角视锥，可视为宽视场目标。",
            "若需在论文叙述上严格对应「后方不锁」，可在巡逻/追击状态机层增加半角检测，与现有朝向门闩合并为同一参数族，减少敌人「甩头瞬狙」的观感问题。",
        )
    )

    # 6.2.3
    blocks.extend(
        triplet(
            "6.2.3 视线检测（射线）",
            "高速场景下掩体博弈依赖「看得见才算数」：射线应忽略自身碰撞体，并按距离排序找到第一个有效遮挡物。",
            "TestEnemyCombat 在 requireLineOfSight 为真时，自 enemyfront 或机体位置发出 Physics.RaycastAll，排序后跳过自身与子碰撞体，仅当首击命中落在玩家根或其子层级上才认为 LOS 干净；PlaneCombat 未再叠加独立 LOS，由关卡几何与接战距离共同约束。",
            "与论文示例的单次 Raycast 相比，RaycastAll 更利于处理复合碰撞体边缘情况；代价是分配略高，可在热点单位上改为非分配射线或缓存掩体。",
        )
    )

    # 6.2.4 — 无代码，保留叙述并贴近工程
    blocks.extend(
        triplet(
            "6.2.4 锁定维持与丢失处理",
            "锁定状态应在「目标无效、超距、被遮挡」之间折中：过灵敏会抖动，过钝会被滥用拉脱。",
            "当前测试敌人以每帧 TryBuildEngagement 重新计算：玩家离开 detectionRadius 或 LOS 失败即立刻失去交战条件，并驱动 IEnemyPatrolSuspendCondition 恢复巡逻；飞机 Boss 以与瞄准点的距离瞬时决定 IsEngaged。",
            "论文中的「延迟丢失」尚未接入为独立计时器；若要做记忆窗，可在 LOS 失败分支增加短 grace 秒表，并与 UI 锁定提示共用事件，便于第七章用例对比前后手感。",
        )
    )

    # 6.3 节标题 + 总起
    blocks.append(("6.3 敌人攻击系统设计", True))
    blocks.append(
        (
            "工程侧把「能否打」交给接战与冷却，「怎么打」交给枪口 Transform 与弹体组件；玩家侧重火力在 WeaponRaycastShooter 中示范停步—发射硬直模板，可供敌人重武器复用。",
            False,
        )
    )

    # 6.3.1 弹道 — 诚实写当前为瞬时瞄准 + 弹体表现补偿
    blocks.extend(
        triplet(
            "6.3.1 弹道与命中可读性",
            "高速目标下理想方案是线性/迭代前置量；即便 AI 暂不预测，也应通过弹体轨迹与速度差让玩家读得到威胁。",
            "PlaneCombat 中 AimDirToPlayer 取当前瞄准点方向；主炮蓝色弹使用 ProjectileBullet 并打开机体水平速度的横向漂移参数，使飞线在屏幕上仍具「被扫到」的可读威胁；橙色副炮与 TestEnemyCombat 使用 EnemyProjectileBullet 直线球体，数值在 Inspector 分档。",
            "若要将第二章式（4-1）（4-2）预测完全落地，可在 AimDirToPlayer 前插入 travelTime 迭代并以 Rigidbody 水平速度外推瞄准点；表 6.1 可继续作为设计表，正文以组件字段解释替代附录式伪代码。",
        )
    )

    # 6.3.2（表 6.1 仍保留在节末一句，供排版插入题注/表格）
    blocks.extend(
        triplet(
            "6.3.2 AI 武器与射速配置",
            "不同职能敌人应在射程、爆发长度、冷却与单发伤害上拉开梯度，使玩家形成距离与资源交换，而不是单一 DPS 曲线。",
            "PlaneCombat 将 GUN1/GUN4 蓝色主炮做成「齐射计数 + 长冷却」爆发管；GUN2/GUN3 橙色弹按 orangePairFireInterval 齐射，弹速由 minionBulletSpeedMultiplier 相对底速缩放；地面测试敌人由 fireInterval、bulletSpeed、bulletMaxRange 与 projectileHealthDamage 等序列化字段独立配置。",
            "统一敌人武器表时，可把上述字段映射到表 6.1 行，Prefab 只填引用；测试关卡记录命中率与 TTK 后再反填论文数值列，减少文实不一致。",
        )
    )
    blocks.append(
        (
            "表 6.1 仍作为武器/射速分档的策划对照；正文以组件字段说明各档差异，表中数值以当前 Prefab 与 Inspector 为准。",
            False,
        )
    )

    # 6.3.3
    blocks.extend(
        triplet(
            "6.3.3 攻击实现",
            "攻击管线应短：交战成立 → 取枪口世界位姿 → 生成弹体并忽略自身碰撞 → 命中链路到玩家资源组件 → 冷却。",
            "TestEnemyCombat 冷却结束时 CreatePrimitive 球体，套用 ProjectileBullet 的高对比材质与尾迹，EnemyProjectileBullet.Setup 传入速度、方向、寿命与伤害，命中链路到 PlayerMechResources；PlaneCombat 解析 FrontGun/GUNx/Fire 挂点，主副炮分别走 ProjectileBullet 与 EnemyProjectileBullet；玩家火箭筒在 WeaponRaycastShooter 中通过协程实现停步与射后短移动锁，可作为重敌人模板。",
            "后续若抽象 IEnemyWeapon，可把「实体弹/射线」分支与受击回调、受击 UI、音效订阅到同一事件，第七章功能测试用例即可按管线逐步勾选。",
        )
    )

    insert_block_before_anchor(body, anchor, blocks)

    intro_62 = (
        "敌人锁定玩家是 AI 攻击的前提。本系统以距离、朝向门闩与射线视线为主干组织接战条件，"
        "并与巡逻暂停条件解耦，便于在测试关卡中快速迭代参数。"
    )
    intro_el = new_paragraph()
    add_text_run(intro_el, intro_62, bold=False)
    el_62.addnext(intro_el)

    if table_after_61 is not None:
        needle = "表 6.1 仍作为武器/射速分档的策划对照"
        for p in doc.paragraphs:
            if needle in p.text:
                p._element.addnext(table_after_61)
                break

    doc.save(out)
    print("OK saved", out)


if __name__ == "__main__":
    main()
