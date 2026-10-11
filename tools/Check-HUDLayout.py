"""Check the serialized Main HUD at multiple sizes without modifying the scene.

Evaluates RectTransform anchors, pivots and nested scales using CanvasScaler's
Expand policy. Run with: python tools/Check-HUDLayout.py
"""
import math
import re
from pathlib import Path


scene = Path(__file__).resolve().parents[1] / "Assets/Scenes/Main.unity"
blocks = {
    int(m[2]): (int(m[1]), m[3])
    for m in re.finditer(
        r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)",
        scene.read_text(encoding="utf-8"), re.M | re.S
    )
}


def field(block, name):
    return re.search(rf"^  {name}: (.*)$", block, re.M)[1]


def vector(block, name):
    return tuple(float(v) for v in re.findall(r"[xy]: ([-\d.eE+]+)", field(block, name)))


rects = {}
for key, (kind, block) in blocks.items():
    if kind != 224:
        continue
    go = int(re.search(r"m_GameObject: \{fileID: (\d+)", block)[1])
    rects[key] = {
        "name": field(blocks[go][1], "m_Name"),
        "parent": int(re.search(r"m_Father: \{fileID: (\d+)", block)[1]),
        **{name: vector(block, name) for name in (
            "m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition", "m_SizeDelta",
            "m_Pivot", "m_LocalScale")},
    }

canvas = next(key for key, rect in rects.items() if rect["name"] == "Canvas")
hud = next(key for key, rect in rects.items() if rect["name"] == "HUD" and rect["parent"] == canvas)
scaler = next(block for kind, block in blocks.values()
              if kind == 114 and "m_UiScaleMode:" in block)
assert field(scaler, "m_UiScaleMode") == "1", "HUD must scale with screen size"
assert field(scaler, "m_ScreenMatchMode") == "1", "Expand keeps the HUD inside all aspect ratios"
assert vector(scaler, "m_ReferenceResolution") == (1920, 1080)


def descendant(key, ancestor):
    while key in rects:
        if key == ancestor:
            return True
        key = rects[key]["parent"]
    return False


def layout(width, height):
    scale = min(width / 1920, height / 1080)
    result = {canvas: ((0, 0), (width / scale, height / scale), (scale, scale))}

    def resolve(key):
        if key in result:
            return result[key]
        r = rects[key]
        origin, parent_size, parent_scale = resolve(r["parent"])
        amin, amax = r["m_AnchorMin"], r["m_AnchorMax"]
        pivot, pos = r["m_Pivot"], r["m_AnchoredPosition"]
        size = tuple(parent_size[i] * (amax[i] - amin[i]) + r["m_SizeDelta"][i] for i in range(2))
        local_scale = tuple(parent_scale[i] * r["m_LocalScale"][i] for i in range(2))
        lower = tuple(origin[i] + parent_scale[i] * (
            parent_size[i] * (amin[i] + (amax[i] - amin[i]) * pivot[i]) + pos[i]
        ) - size[i] * local_scale[i] * pivot[i] for i in range(2))
        result[key] = lower, size, local_scale
        return result[key]

    for key in rects:
        if descendant(key, canvas):
            resolve(key)
    return result, scale


def bounds(state, key):
    lower, size, scale = state[key]
    return (*lower, *(lower[i] + size[i] * scale[i] for i in range(2)))


def close(actual, expected):
    assert all(math.isclose(a, e, abs_tol=0.01) for a, e in zip(actual, expected)), (actual, expected)


baseline, _ = layout(1920, 1080)
expected_centers = {
    "DatePanel": (1764, 1009.4), "TimePanel": (1764, 959.1),
    "WheatherPanel": (1581, 983.6), "QuickInventoryPanel": (960, 58),
    "QuickSkill": (644.85736, 143.65738),
}
for name, expected in expected_centers.items():
    key = next(k for k, r in rects.items() if r["name"] == name and descendant(k, hud))
    x0, y0, x1, y1 = bounds(baseline, key)
    close(((x0 + x1) / 2, (y0 + y1) / 2), expected)

date = next(k for k, r in rects.items() if r["name"] == "DateTimePanel")
sizes = [(1920, 1080), (960, 540), (1280, 720), (1600, 900),
         (2560, 1440), (1024, 768), (1280, 1024), (2560, 1080), (3440, 1440), (1920, 1080)]
checks = 0
for width, height in sizes:
    state, scale = layout(width, height)
    for key, rect in rects.items():
        if key == hud or not descendant(key, hud) or rect["name"] in ("DateTimePanel", "QuickInventory"):
            continue
        x0, y0, x1, y1 = bounds(state, key)
        assert x0 >= -0.01 and y0 >= -0.01 and x1 <= width + 0.01 and y1 <= height + 0.01, (width, height, rect["name"], (x0, y0, x1, y1))
        bx0, by0, bx1, by1 = bounds(baseline, key)
        if descendant(key, date):
            close(((width - x1) / scale, (height - y1) / scale), (1920 - bx1, 1080 - by1))
        else:
            close(((x0 - width / 2) / scale, y0 / scale), (bx0 - 960, by0))
        checks += 1
    for name in ("MenuBookUI", "ShopBookUI"):
        root = next(k for k, r in rects.items() if r["name"] == name)
        book = next(k for k, r in rects.items() if r["name"] == "Book" and r["parent"] == root)
        x0, y0, x1, y1 = bounds(state, book)
        close(((x0 + x1) / 2, (y0 + y1) / 2), (width / 2, height / 2))
        assert 0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height
        checks += 1
print(f"PASS: {checks} HUD/menu layout checks across {len(sizes)} resolution transitions; authored positions preserved")
