# -*- coding: utf-8 -*-
"""
tools/tree_sources/*.png (스타듀 형식 48x160 나무 시트) 를
Assets/Resources/Sprites/Trees/{나무}_{계절}.png (288x96) 로 다시 배치한다.

가로 7칸(각 48x96), 왼쪽부터:
  0 레벨0(묘목) · 1 레벨1 · 2 레벨2 · 3 나뭇잎 이펙트 4개 · 4 그루터기 · 5 레벨3 윗부분
  6 다 자란 나무 한 장 (4+5를 미리 겹쳐 둔 것)

마지막 칸은 게임이 쓰지 않는다 — 게임은 그루터기 위에 윗부분을 얹어 그린다(그래야 벨 때
윗부분만 넘어뜨릴 수 있다). 이건 <b>장식용</b>이다: "Fixed_{맵}" 레이어는 한 칸에 그림 한 장을
그대로 놓을 뿐이라, 겹쳐 그려 주지 않기 때문에 합쳐 둔 한 장이 따로 필요하다.

원본 48x160 안의 조각 위치는 스타듀 규격 그대로다. 여섯 칸 모두
"원본 조각을 칸 가운데에 두고 <b>바닥을 칸 아래끝(y=96)에 맞춘다</b>" 는 한 가지 규칙으로
배치하므로, 계절이 달라 그림 크기가 달라져도 밑동 위치가 흔들리지 않는다.
그루터기와 레벨3 윗부분도 같은 규칙이라 그대로 겹쳐 그리면 맞물린다.

  python tools/build_tree_sheets.py
"""
import os
from PIL import Image

SRC = os.path.join(os.path.dirname(__file__), "tree_sources")
DST = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Sprites", "Trees")

CELL_W, CELL_H = 48, 96

# 원본(48x160) 안에서 잘라 올 곳 — 왼쪽 칸부터 순서대로.
RECTS = [
    (0, 128, 16, 16),   # 0 레벨0 묘목
    (16, 128, 16, 16),  # 1 레벨1
    (0, 96, 16, 32),    # 2 레벨2
    (16, 96, 16, 32),   # 3 나뭇잎 이펙트 4개 (아래 16x16 안에 8x8씩 네 개)
    (32, 96, 16, 32),   # 4 그루터기
    (0, 0, 48, 96),     # 5 레벨3 윗부분
]

# 만들 시트 -> 원본 파일. tree2(자작나무)는 여름판이 따로 없어 봄B(꽃이 진 초록)를 쓴다.
SHEETS = {
    "tree1_spring": "tree1_spring.png",
    "tree1_summer": "tree1_summer.png",
    "tree1_fall":   "tree1_fall.png",
    "tree1_winter": "tree1_winter.png",
    "tree2_spring": "tree3_spring_A.png",
    "tree2_summer": "tree3_spring_B.png",
    "tree2_fall":   "tree3_fall.png",
    "tree2_winter": "tree3_winter.png",
}


def build(src_path):
    src = Image.open(src_path).convert("RGBA")
    if src.size != (48, 160):
        raise SystemExit("원본 크기가 48x160이 아닙니다: %s %s" % (src_path, src.size))

    out = Image.new("RGBA", (CELL_W * (len(RECTS) + 1), CELL_H), (0, 0, 0, 0))

    def put(cell, rect):
        x, y, w, h = rect
        piece = src.crop((x, y, x + w, y + h))
        out.alpha_composite(piece, (cell * CELL_W + (CELL_W - w) // 2, CELL_H - h))

    for i, rect in enumerate(RECTS):
        put(i, rect)

    # 마지막 칸 = 그루터기 위에 윗부분. 둘 다 같은 규칙으로 놓이므로 그냥 겹치면 맞물린다.
    put(len(RECTS), RECTS[4])
    put(len(RECTS), RECTS[5])
    return out


def main():
    for name, src_file in SHEETS.items():
        path = os.path.join(SRC, src_file)
        if not os.path.exists(path):
            raise SystemExit("원본이 없습니다: " + path)
        sheet = build(path)
        dst = os.path.join(DST, name + ".png")
        sheet.save(dst)
        print("wrote", os.path.normpath(dst))


if __name__ == "__main__":
    main()
