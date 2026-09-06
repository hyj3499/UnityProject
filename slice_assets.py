"""
Slices the provided sprite sheets into individual PNG sprites used by the Unity project.
Output goes to Assets/Sprites/... in the Unity project tree.
All sprites kept at native pixel resolution (16px tile grid, 32px character frames).
"""
from PIL import Image
import numpy as np
import os

SRC = "/mnt/user-data/uploads"
OUT = "/home/claude/FarmMVP/UnityProject/Assets/Sprites"

def ensure(p):
    os.makedirs(p, exist_ok=True)

def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGBA")

def crop_save(img, box, path):
    """box = (left, top, right, bottom)"""
    ensure(os.path.dirname(path))
    img.crop(box).save(path)

def grid_save(img, col, row, cw, ch, path, cols_w=None):
    x = col * cw
    y = row * ch
    crop_save(img, (x, y, x + cw, y + ch), path)

# ---------------------------------------------------------------
# PLAYER — Idle (128x96, 32x32 frames, 4 cols x 3 rows)
#   row0 = down, row1 = up, row2 = side (right)
# ---------------------------------------------------------------
idle = load("Idle.png")
ensure(f"{OUT}/Player")
dirs_idle = {0: "Down", 1: "Up", 2: "Side"}
for r, d in dirs_idle.items():
    for c in range(4):
        grid_save(idle, c, r, 32, 32, f"{OUT}/Player/Idle_{d}_{c}.png")

# PLAYER — Walk (192x96, 32x32 frames, 6 cols x 3 rows)
walk = load("Walk.png")
dirs_walk = {0: "Down", 1: "Up", 2: "Side"}
for r, d in dirs_walk.items():
    for c in range(6):
        grid_save(walk, c, r, 32, 32, f"{OUT}/Player/Walk_{d}_{c}.png")

# ---------------------------------------------------------------
# TILES — Tileset_Spring (16px). Solid grass + tilled dirt.
#   Pure grass at (row2,col9). Solid dirt at (row10,col9)=(237,156,81).
# ---------------------------------------------------------------
ts = load("Tileset_Spring.png")
ensure(f"{OUT}/Tiles")
# Grass (pure fill)
grid_save(ts, 9, 2, 16, 16, f"{OUT}/Tiles/Grass.png")
# Dirt path (solid orange-brown)
grid_save(ts, 9, 10, 16, 16, f"{OUT}/Tiles/Dirt.png")

# Generate a "tilled soil" tile procedurally from the dirt tile (darker, with furrow lines)
dirt = ts.crop((9*16, 10*16, 10*16, 11*16)).convert("RGBA")
arr = np.array(dirt).astype(int)
# darken
arr[:, :, 0] = (arr[:, :, 0] * 0.62).clip(0, 255)
arr[:, :, 1] = (arr[:, :, 1] * 0.52).clip(0, 255)
arr[:, :, 2] = (arr[:, :, 2] * 0.42).clip(0, 255)
tilled = arr.astype(np.uint8)
# add horizontal furrow lines
for y in [3, 8, 13]:
    tilled[y, :, 0:3] = (tilled[y, :, 0:3] * 0.7).astype(np.uint8)
Image.fromarray(tilled, "RGBA").save(f"{OUT}/Tiles/Tilled.png")

# Watered tilled = tilled with blue tint
watered = arr.copy()
watered[:, :, 2] = (watered[:, :, 2] + 40).clip(0, 255)
watered[:, :, 0] = (watered[:, :, 0] * 0.8).clip(0, 255)
wt = watered.astype(np.uint8)
for y in [3, 8, 13]:
    wt[y, :, 0:3] = (wt[y, :, 0:3] * 0.7).astype(np.uint8)
Image.fromarray(wt, "RGBA").save(f"{OUT}/Tiles/TilledWatered.png")

# ---------------------------------------------------------------
# TREES — Maple_Tree (160x48). Pick the largest full tree.
#   Grid 10x3 of 16px. The big tree occupies right-center cluster.
# ---------------------------------------------------------------
tree = load("Maple_Tree.png")
ensure(f"{OUT}/Environment")
# Find largest connected component = full grown tree
arr_t = np.array(tree)[:, :, 3]
try:
    from scipy import ndimage
    lab, n = ndimage.label(arr_t > 10)
    best = None
    for i in range(1, n + 1):
        ys, xs = np.where(lab == i)
        area = len(xs)
        box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1, area)
        if best is None or area > best[4]:
            best = box
    crop_save(tree, best[:4], f"{OUT}/Environment/Tree.png")
    # A smaller stage (sapling) = second largest
    comps = []
    for i in range(1, n + 1):
        ys, xs = np.where(lab == i)
        comps.append((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1, len(xs)))
    comps.sort(key=lambda b: b[4], reverse=True)
    if len(comps) > 2:
        crop_save(tree, comps[2][:4], f"{OUT}/Environment/Stump.png")
except ImportError:
    crop_save(tree, (96, 0, 144, 48), f"{OUT}/Environment/Tree.png")

# Wood item = generate small log icon from tree trunk colors (fallback simple)
# Use a 16x16 crop of trunk area of the big tree bottom
tw = Image.open(f"{OUT}/Environment/Tree.png")
tw_w, tw_h = tw.size
log = tw.crop((tw_w//2 - 8, tw_h - 16, tw_w//2 + 8, tw_h))
log.save(f"{OUT}/Environment/Wood.png")

# ---------------------------------------------------------------
# HOUSE exterior — House.png. Full house w/ chimney at x148-219,y3-97
# ---------------------------------------------------------------
house = load("House.png")
crop_save(house, (148, 3, 220, 98), f"{OUT}/Environment/House.png")
# door area of that house ~ relative; crop a door tile from left house door (x148..)
# The main house door: approximate at x196-212, y70-97
crop_save(house, (196, 68, 214, 98), f"{OUT}/Environment/HouseDoor.png")

# ---------------------------------------------------------------
# INTERIOR — Interior.png furniture
# ---------------------------------------------------------------
interior = load("Interior.png")
ensure(f"{OUT}/Interior")
crop_save(interior, (87, 8, 105, 42), f"{OUT}/Interior/Bed.png")          # vertical bed
crop_save(interior, (115, 8, 141, 48), f"{OUT}/Interior/Fireplace.png")   # fireplace
crop_save(interior, (144, 19, 161, 42), f"{OUT}/Interior/Door.png")       # door
crop_save(interior, (0, 82, 48, 112), f"{OUT}/Interior/Rug.png")          # green rug
crop_save(interior, (96, 120, 160, 138), f"{OUT}/Interior/Desks.png")     # desks row (decor)
crop_save(interior, (4, 0, 12, 16), f"{OUT}/Interior/Plant.png")          # potted plant

# Interior floor + wall: synthesize simple tiles (wood floor + wall) 16x16
def solid_tile(color, path, noise=8):
    a = np.zeros((16, 16, 4), dtype=np.uint8)
    a[:, :, 0], a[:, :, 1], a[:, :, 2], a[:, :, 3] = color[0], color[1], color[2], 255
    n = (np.random.RandomState(1).randint(-noise, noise, (16, 16, 1)))
    a[:, :, 0:3] = (a[:, :, 0:3].astype(int) + n).clip(0, 255).astype(np.uint8)
    # plank lines
    for x in [0, 8]:
        a[:, x, 0:3] = (a[:, x, 0:3].astype(int) * 0.75).astype(np.uint8)
    ensure(os.path.dirname(path))
    Image.fromarray(a, "RGBA").save(path)

solid_tile((150, 111, 68), f"{OUT}/Interior/Floor.png")   # wood floor
solid_tile((92, 64, 48), f"{OUT}/Interior/Wall.png", noise=5)  # wall

# ---------------------------------------------------------------
# CROPS — Spring_Crops (224x128, 16px, 14x8). Strawberry = row0.
#   cols 0-5 = growth stages, col6 = harvest-ready (with berries)
#   Seed packet (strawberry) = row0 col7. Harvested strawberry = row0 col8.
# ---------------------------------------------------------------
crops = load("Spring_Crops.png")
ensure(f"{OUT}/Crops")
# Strawberry growth stages: use cols 0..6 (7 sprites) row 0
for c in range(7):
    grid_save(crops, c, 0, 16, 16, f"{OUT}/Crops/Strawberry_{c}.png")
# Seed packet (col 7 row0)
grid_save(crops, 7, 0, 16, 16, f"{OUT}/Crops/StrawberrySeed.png")
# Harvested strawberry fruit (col 8 row0)
grid_save(crops, 8, 0, 16, 16, f"{OUT}/Crops/StrawberryFruit.png")

# ---------------------------------------------------------------
# TOOLS — synthesize simple 16x16 icons (hoe, watering can, axe) tinted
#   Use recognizable colored glyphs since sheets lack dedicated tool icons.
# ---------------------------------------------------------------
ensure(f"{OUT}/Tools")
def make_icon(path, draw_fn):
    a = np.zeros((16, 16, 4), dtype=np.uint8)
    draw_fn(a)
    Image.fromarray(a, "RGBA").save(path)

def px(a, x, y, col):
    if 0 <= x < 16 and 0 <= y < 16:
        a[y, x] = [col[0], col[1], col[2], 255]

def hoe(a):
    wood = (140, 92, 50); metal = (200, 200, 210)
    for i in range(10):  # handle diagonal
        px(a, 3 + i, 12 - i, wood)
        px(a, 4 + i, 12 - i, wood)
    for x in range(10, 14):  # blade
        px(a, x, 2, metal); px(a, x, 3, metal)
    px(a, 10, 4, metal); px(a, 11, 4, metal)

def watering(a):
    body = (90, 150, 200); dark = (60, 110, 160)
    for y in range(6, 13):
        for x in range(4, 11):
            px(a, x, y, body)
    for x in range(4, 11):
        px(a, x, 6, dark)
    # spout
    for i in range(3):
        px(a, 11 + i, 7 + i, dark)
    # handle
    for y in range(3, 6):
        px(a, 7, y, dark); px(a, 8, y, dark)

def axe(a):
    wood = (140, 92, 50); metal = (180, 180, 190)
    for i in range(9):
        px(a, 6 + (i//3), 13 - i, wood)
    for x in range(8, 13):  # head
        px(a, x, 2, metal); px(a, x, 3, metal); px(a, x, 4, metal)
    px(a, 7, 3, metal); px(a, 13, 3, metal)

make_icon(f"{OUT}/Tools/Hoe.png", hoe)
make_icon(f"{OUT}/Tools/WateringCan.png", watering)
make_icon(f"{OUT}/Tools/Axe.png", axe)

print("Asset slicing complete.")
# Report counts
total = 0
for root, _, files in os.walk(OUT):
    for f in files:
        if f.endswith(".png"):
            total += 1
print(f"Total sprites written: {total}")
