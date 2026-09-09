"""
동작 폴더마다 흩어져 있던 머리/눈/악세 그림을 <b>정면·후면·측면 3장</b>으로 뽑아내고,
프레임마다 그것을 어디에 그릴지 적은 '위치표'를 만든다.

왜 이게 되는가: 조사해 보니 머리·눈·악세는 동작이 바뀌어도 그림이 같고 <b>위치만 1~2px
움직였다</b> (악세는 17개 동작 전부 0px로 완전히 동일). 좌우도 정확한 거울상이라 측면 한 장이면 된다.
그래서 그림은 3장만 남기고, 나머지는 이 표가 대신한다.

옷은 다르다 — 다리를 따라 모양이 변형되므로 그림으로 못 옮긴다. 대신 옷 픽셀이 100% 몸 실루엣
안에 있다는 점을 이용해, 몸의 어느 세로 구간이 옷인지만 여기에 적어 두고 런타임에 몸에서 만든다.
"""
import os, glob, shutil
from PIL import Image

SRC   = "Assets/Resources/Sprites/Player"
PARTS = "Assets/Resources/Sprites/PlayerParts"
DATA  = "Assets/Resources/PlayerFrameData.txt"
BACKUP= "Assets/_ArtBackup/PlayerLayers"

ANIMS = ["Idle","Walk","Run","Carrying - Idle","Carrying - Walk","Carrying - Run",
         "Carrying - Pick Up","Carrying - Throwing items","Pickaxe","Shovel","Watering",
         "Axe and Sickle","Fishing - Cast","Fishing - Wait","Fishing - Bite",
         "Fishing - Reel","Fishing - Catch"]

FS = 32
DIRS = ["Down","Up","Right","Left"]     # 시트에 그려진 순서
REF_ANIM = "Idle"                        # 3장을 떠 올 기준 동작

def sheet(path):
    im = Image.open(path).convert('RGBA')
    n = im.width // FS
    return [im.crop((i*FS,0,i*FS+FS,FS)) for i in range(n)], n//4

def frame(path, d, i):
    fl, per = sheet(path)
    return fl[d*per + i], per

def origin(img):
    bb = img.getbbox()
    return (bb[0], bb[1]) if bb else None

def find_src(rel):
    """그 파츠가 들어 있는 동작을 찾는다 (기준 동작 우선). 대소문자 차이도 흡수한다."""
    for a in [REF_ANIM] + ANIMS:
        p = os.path.join(SRC, a, rel + ".png")
        if os.path.exists(p): return a, p
        d = os.path.dirname(p)
        if os.path.isdir(d):
            want = os.path.basename(p).lower()
            for f in os.listdir(d):
                if f.lower() == want: return a, os.path.join(d, f)
    return None, None


def collect_items():
    """폴더를 훑어 파츠 목록을 만든다. 이름이 대소문자만 다른 것은 하나로 합친다."""
    groups = {"Hair's": {}, "Eyes": {}, "Acc": {}}
    for a in ANIMS:
        for sub in groups:
            root = os.path.join(SRC, a, sub)
            if not os.path.isdir(root): continue
            for p in glob.glob(os.path.join(root, "**", "*.png"), recursive=True):
                rel = os.path.relpath(p, root).replace("\\", "/")[:-4]
                groups[sub].setdefault(rel.lower(), rel)   # 먼저 만난 표기를 대표로
    return {k: sorted(v.values()) for k, v in groups.items()}


def export_part(sub, rel, out_root):
    """정면(Down)·후면(Up)·측면(Right) 세 장을 가로로 이어 붙여 저장한다."""
    anim, path = find_src(f"{sub}/{rel}")
    if path is None: return None

    out = Image.new('RGBA', (FS*3, FS), (0,0,0,0))
    ref_origins = {}
    for slot, d in enumerate([0, 1, 2]):          # Down, Up, Right
        f, _ = frame(path, d, 0)
        out.paste(f, (slot*FS, 0))
        ref_origins[DIRS[d]] = origin(f)

    # 왼쪽은 측면을 좌우로 뒤집어 쓴다 — 기준 위치도 뒤집어 계산해 둔다.
    rf, _ = frame(path, 2, 0)
    bb = rf.getbbox()
    ref_origins["Left"] = (FS - bb[2], bb[1]) if bb else None

    dst = os.path.join(out_root, sub.replace("'", ""), rel + ".png")
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    out.save(dst)
    return ref_origins


def build_table(ref_origins_by_dir):
    """동작·방향·프레임마다 (머리/눈/악세를 얼마나 옮길지, 옷이 덮는 세로 구간)을 적는다."""
    lines = []
    for a in ANIMS:
        hair = os.path.join(SRC, a, "Hair's/Standard/Brown.png")
        cloth = os.path.join(SRC, a, "Clothers/Farm/Blue.png")
        hl, per = sheet(hair)
        cl, _   = sheet(cloth)
        for d in range(4):
            ref = ref_origins_by_dir[DIRS[d]]
            for i in range(per):
                o = origin(hl[d*per+i])
                dx, dy = (0, 0) if (o is None or ref is None) else (o[0]-ref[0], o[1]-ref[1])

                cb = cl[d*per+i].getbbox()
                gt, gb = (cb[1], cb[3]-1) if cb else (-1, -1)
                lines.append(f"{a}|{DIRS[d]}|{i}|{dx}|{dy}|{gt}|{gb}")
    return lines


def main():
    items = collect_items()
    print("파츠 개수:", {k: len(v) for k, v in items.items()})

    exported = 0
    ref_for_table = None
    for sub, rels in items.items():
        for rel in rels:
            refs = export_part(sub, rel, PARTS)
            if refs is None:
                print(f"  건너뜀(원본 없음): {sub}/{rel}"); continue
            exported += 1
            # 위치표의 기준은 머리 Standard/Brown 으로 통일한다 (셋의 이동량이 같기 때문).
            if sub == "Hair's" and rel == "Standard/Brown":
                ref_for_table = refs

    print(f"3장짜리 파츠 {exported}개 저장 -> {PARTS}")

    assert ref_for_table, "기준(Hair Standard/Brown)을 찾지 못했습니다"
    lines = build_table(ref_for_table)
    os.makedirs(os.path.dirname(DATA), exist_ok=True)
    header = ("# 동작|방향|프레임|머리·눈·악세 X이동|Y이동|옷 시작y|옷 끝y\n"
              "# 이 파일은 tools/extract_player_parts.py 가 원본 그림에서 자동으로 뽑은 것이다.\n")
    with open(DATA, "w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n".join(lines) + "\n")
    print(f"위치표 {len(lines)}줄 저장 -> {DATA}")


def archive():
    """동작 폴더의 머리/눈/악세/옷을 백업 폴더로 옮긴다 (Resources 밖이라 빌드에 안 들어간다)."""
    moved = 0
    for a in ANIMS:
        for sub in ["Hair's", "Eyes", "Acc", "Clothers"]:
            src = os.path.join(SRC, a, sub)
            if not os.path.isdir(src): continue
            dst = os.path.join(BACKUP, a, sub)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.move(src, dst)
            meta = src + ".meta"
            if os.path.exists(meta): os.remove(meta)
            moved += 1
    print(f"{moved}개 폴더를 {BACKUP} 로 옮겼습니다")


if __name__ == "__main__":
    import sys
    if "--archive" in sys.argv: archive()
    else: main()
