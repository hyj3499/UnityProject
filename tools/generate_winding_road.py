# -*- coding: utf-8 -*-
"""
Sprite-0003.png (144x48, 16x16 x 9x3) 를 코너 기반(wang/blob) 오토타일로 해석해서
굵기가 계속 변하는 구불구불한 흙길 맵을 생성한다.

이 버전은 시트에 실제로 그려진 조각만 그대로 잘라 쓴다. 반전(H)도, 밴드를 잘라붙이는
합성도 하지 않는다 - 없는 조합은 그냥 없는 것으로 둔다.

타일 분류 (코너 비트 = TL,TR,BL,BR / 1=흙, 0=잔디), 새 시트에서 실제로 찾은 위치:
  0001 (0,0)                       <- 볼록 코너 (귀퉁이 하나만 흙)
  0010 (0,3)
  0100 (2,0)
  1000 (2,3)
  0011 (0,1) (1,5) (1,6) (1,7)     <- 변 (두 귀퉁이가 흙)
  1100 (0,5) (0,6) (0,7) (2,1)
  0101 (1,0)
  1010 (1,3)
  0111 (0,2) (1,8)                 <- 오목 코너 (귀퉁이 하나만 잔디)
  1011 (1,4)
  1101 (0,8) (2,2)
  1110 (0,4)
  1111 (1,1) (1,2) (2,4)           <- 흙 채움

  0000 (완전 잔디), 0110/1001 (대각) : 시트에 없음 - 아래 "빈 코드" 설명 참고.

빈 코드(0000/0110/1001) 처리:
  - 0110/1001 은 폭이 있는 하나의 길이 코너 한 점에서만 스치는, 사실상 나올 수 없는
    모양이라 fix_diagonals() 가 마스크 단계에서 항상 없애 버린다. 정상적으로는 절대
    필요하지 않고, 정말 만약을 위한 안전장치로만 1111 타일을 대신 쓴다.
  - 0000(길에서 먼 순수 잔디)은 이 시트에 원본 조각이 아예 없다. 억지로 다른 조각을
    잘라붙이는 대신 그 칸은 그냥 투명하게 비워 둔다 - 이 PNG를 잔디 타일맵 위에 얹는
    용도로 쓰면(예: outdoor_tileset 잔디 레이어 위에 얹기) 자연스럽게 밑에 깔린 잔디가
    비치게 된다. 화면 전체를 이 한 장으로 완전히 덮고 싶다면 --fill-empty 로 배경을
    먼저 채우거나, Unity에서 잔디 레이어 위에 이 PNG를 겹쳐 쓰면 된다.

사용법:
  python tools/generate_winding_road.py [--seed 7] [--w 48] [--h 32] [--out <path>]

기본적으로 실제로 쓸 WindingRoad.png 와, 각 칸에 어떤 원본 좌표를 썼는지 글자로 표시한
WindingRoad_표시용.png 를 같이 만든다. --no-debug 로 표시용 이미지는 끌 수 있다.
"""
import argparse, math, os, random
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
TILES = os.path.join(ROOT, "Assets", "Resources", "Sprites", "Tiles")
SRC = os.path.join(TILES, "Sprite-0003.png")
T = 16

# code -> [(row, col), ...]  (시트에 실제로 그려진, 그대로 잘라 쓰는 칸들. 반전/합성 없음)
BASE = {
    0b0001: [(0, 0)],
    0b0010: [(0, 3)],
    0b0100: [(2, 0)],
    0b1000: [(2, 3)],
    0b0011: [(0, 1), (1, 5), (1, 6), (1, 7)],
    0b1100: [(0, 5), (0, 6), (0, 7), (2, 1)],
    0b0101: [(1, 0)],
    0b1010: [(1, 3)],
    0b0111: [(0, 2), (1, 8)],
    0b1011: [(1, 4)],
    0b1101: [(0, 8), (2, 2)],
    0b1110: [(0, 4)],
    0b1111: [(1, 1), (1, 2), (2, 4)],
}


def build_tiles(sheet):
    """code -> [(16x16 Image, 라벨), ...]. 시트에 없는 코드는 키 자체가 없다."""
    out = {}
    for code, cells in BASE.items():
        out[code] = [(sheet.crop((c * T, r * T, c * T + T, r * T + T)), f"{r},{c}")
                      for (r, c) in cells]
    return out


def centerline(w, h, rng):
    """(x, y, halfwidth) 샘플 리스트. 단위는 타일."""
    def octaves(n, lo_f, hi_f):
        return [(rng.uniform(lo_f, hi_f), rng.uniform(0, math.tau)) for _ in range(n)]

    ys = octaves(3, 1.2, 1.9), octaves(3, 2.6, 3.6), octaves(3, 5.0, 6.6)
    fy = [(1.00, ys[0][0]), (0.42, ys[1][0]), (0.17, ys[2][0])]
    fw = [(1.00, octaves(1, 2.0, 2.8)[0]), (0.45, octaves(1, 4.2, 5.4)[0]),
          (0.25, octaves(1, 8.0, 10.0)[0])]

    amp = (h - 6) * 0.42
    hw_mid, hw_amp = 1.85, 1.05
    pts = []
    x = -2.5
    while x <= w + 2.5:
        t = (x + 2.5) / (w + 5.0)
        sy = sum(a * math.sin(math.tau * f * t + p) for a, (f, p) in fy)
        norm = sum(a for a, _ in fy)
        y = h * 0.5 + amp * sy / norm
        y = min(max(y, 2.2), h - 2.2)

        sw = sum(a * math.sin(math.tau * f * t + p) for a, (f, p) in fw)
        hw = hw_mid + hw_amp * sw / sum(a for a, _ in fw)
        hw = min(max(hw, 0.80), 3.10)
        pts.append((x, y, hw))
        x += 0.05
    return pts


def road_mask(w, h, pts, rng):
    """코너 격자 (w+1) x (h+1) 의 흙 여부."""
    # 가장자리를 자연스럽게 흔들기 위한 저주파 노이즈
    jit = [(rng.uniform(0.9, 1.6), rng.uniform(0, math.tau)) for _ in range(3)]

    mask = [[False] * (h + 1) for _ in range(w + 1)]
    for i in range(w + 1):
        for j in range(h + 1):
            best = 1e9
            for (cx, cy, hw) in pts:
                dx = i - cx
                if dx * dx > 36:          # 넉넉한 조기 컷
                    continue
                d = math.hypot(dx, j - cy)
                # 경계선을 살짝 울퉁불퉁하게
                ang = math.atan2(j - cy, dx)
                wob = sum(0.13 * math.sin(k * ang + p) for k, p in jit)
                best = min(best, d - (hw + wob))
                if best <= 0:
                    break
            mask[i][j] = best <= 0
    return mask


def code_at(mask, i, j):
    return ((mask[i][j] << 3) | (mask[i + 1][j] << 2) |
            (mask[i][j + 1] << 1) | mask[i + 1][j + 1])


def fix_diagonals(mask, w, h):
    """0110 / 1001 (대각 접촉) 을 제거 - 잔디 코너 하나를 흙으로 채운다.
    이 시트엔 애초에 0110/1001 조각이 없으므로, 이 함수가 없애지 못한 경우는
    render()의 안전장치(1111로 대체)가 잡아 준다."""
    for _ in range(12):
        changed = False
        for i in range(w):
            for j in range(h):
                c = code_at(mask, i, j)
                if c == 0b1001:
                    mask[i + 1][j] = True      # TR 채움 -> 1101
                    changed = True
                elif c == 0b0110:
                    mask[i][j] = True          # TL 채움 -> 1110
                    changed = True
        if not changed:
            break


def render(mask, w, h, tiles, rng):
    """일반 출력 이미지와, 표시용 이미지를 만들기 위한 칸별 선택 기록을 같이 반환한다.
    0000(순수 잔디)은 시트에 조각이 없으므로 그냥 투명하게 비워 둔다."""
    img = Image.new("RGBA", (w * T, h * T))
    picks = [[None] * h for _ in range(w)]   # picks[i][j] = (code, label 또는 None)
    for j in range(h):
        for i in range(w):
            c = code_at(mask, i, j)
            variants = tiles.get(c)
            if variants is None:
                if c == 0b0000:
                    picks[i][j] = (c, None)          # 비움 (원본에 조각 없음)
                    continue
                variants = tiles[0b1111]             # 0110/1001 안전장치 (정상 경로에선 안 옴)
            variant, label = rng.choice(variants)
            img.paste(variant, (i * T, j * T))
            picks[i][j] = (c, label)
    return img, picks


def _font(size):
    for name in ("malgun.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except Exception:
            continue
    return ImageFont.load_default()


def render_debug(img, picks, w, h, scale=4):
    """실제 이미지를 scale배로 키우고, 칸마다 코드와 원본 좌표(행,열)를 글자로 얹는다.
    투명하게 비워 둔 칸은 회색 바둑판 무늬로 보이게 해서 '원래 비어 있다'는 걸 눈에 띄게 한다."""
    cell = T * scale
    top_h = 34

    # 체크무늬 배경 위에 합성 - 투명 칸(0000)이 하얀 배경에 묻히지 않도록.
    checker = Image.new("RGB", img.size, (235, 235, 235))
    cdraw = ImageDraw.Draw(checker)
    step = max(2, scale)
    for cy in range(0, img.height, step):
        for cx in range(0, img.width, step):
            if (cx // step + cy // step) % 2 == 0:
                cdraw.rectangle([cx, cy, cx + step - 1, cy + step - 1], fill=(210, 210, 210))
    checker.paste(img, (0, 0), img)
    base = checker.resize((w * cell, h * cell), Image.NEAREST)

    out = Image.new("RGB", (w * cell, top_h + h * cell), (255, 255, 255))
    out.paste(base, (0, top_h))
    d = ImageDraw.Draw(out)

    f_title = _font(16)
    f_code = _font(max(9, scale * 2))
    f_label = _font(max(9, scale * 2))

    d.text((6, 6), "각 칸: 위=코드(TL TR BL BR), 아래=원본 좌표(행,열). 회색 바둑판=원본에 조각이 "
                   "없어 비워 둔 칸(0000). 반전/회전 없음 - 시트에 그려진 조각 그대로.",
           font=f_title, fill=(0, 0, 0))

    for i in range(w):
        for j in range(h):
            code, label = picks[i][j]
            x0, y0 = i * cell, top_h + j * cell
            d.rectangle([x0, y0, x0 + cell, y0 + cell], outline=(255, 0, 255), width=1)
            code_str = format(code, "04b")
            d.rectangle([x0 + 1, y0 + 1, x0 + cell - 1, y0 + 24], fill=(0, 0, 0))
            d.text((x0 + 3, y0 + 1), code_str, font=f_code, fill=(255, 255, 0))
            d.text((x0 + 3, y0 + 13), label if label else "(비움)", font=f_label,
                   fill=(120, 220, 255) if label else (255, 140, 140))

    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--w", type=int, default=48, help="가로 타일 수")
    ap.add_argument("--h", type=int, default=32, help="세로 타일 수")
    ap.add_argument("--out", default=os.path.join(TILES, "WindingRoad.png"))
    ap.add_argument("--debug-out", default=None,
                     help="표시용 이미지 경로 (기본: <out>에서 확장자 앞에 _표시용을 붙임)")
    ap.add_argument("--no-debug", action="store_true", help="표시용 이미지를 만들지 않는다")
    ap.add_argument("--debug-scale", type=int, default=4, help="표시용 이미지의 칸당 확대 배율")
    a = ap.parse_args()

    rng = random.Random(a.seed)
    sheet = Image.open(SRC).convert("RGBA")
    tiles = build_tiles(sheet)

    pts = centerline(a.w, a.h, rng)
    mask = road_mask(a.w, a.h, pts, rng)
    fix_diagonals(mask, a.w, a.h)

    img, picks = render(mask, a.w, a.h, tiles, rng)
    img.save(a.out)

    hw = [p[2] for p in pts]
    print(f"saved {a.out}  {img.width}x{img.height}px  ({a.w}x{a.h} tiles, seed={a.seed})")
    print(f"road width: {min(hw)*2:.1f} ~ {max(hw)*2:.1f} tiles "
          f"({min(hw)*2*T:.0f} ~ {max(hw)*2*T:.0f} px)")

    if not a.no_debug:
        debug_out = a.debug_out
        if debug_out is None:
            root, ext = os.path.splitext(a.out)
            debug_out = f"{root}_표시용{ext}"
        dbg = render_debug(img, picks, a.w, a.h, scale=a.debug_scale)
        dbg.save(debug_out)
        print(f"saved {debug_out}  {dbg.width}x{dbg.height}px  (표시용)")


if __name__ == "__main__":
    main()
