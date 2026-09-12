# -*- coding: utf-8 -*-
"""
Assets/Resources/Sprites/Trees/*.png 의 .meta 를 만든다 (유니티 임포트 설정 + 조각 자르기).

시트 한 장(336x96)을 조각 10개로 자른다. 이름이 곧 코드가 찾는 열쇠다 (TreeArt 참고):
  {시트}_stage0 / _stage1 / _stage2   자라는 모습
  {시트}_stump                        그루터기
  {시트}_top                          레벨3 윗부분 (그루터기 위에 겹쳐 그린다)
  {시트}_leaf0 .. _leaf3              벨 때 흩날리는 나뭇잎 4개
  {시트}_full                         다 자란 나무 한 장 — 장식용 ("Fixed_{맵}"에 칠할 때)

나뭇잎을 뺀 조각들은 48x96 칸 전체를 그대로 쓰고 기준점(pivot)을 <b>아래 가운데</b>로 둔다 —
그래서 어느 조각을 같은 자리에 놓아도 밑동이 딱 맞고, 그루터기 위에 윗부분이 정확히 얹힌다.
나뭇잎은 날아다니는 것이라 기준점이 가운데다.

<b>완전히 비어 있는 조각은 아예 만들지 않는다</b> — 겨울 자작나무처럼 그 계절에 잎이 없는
나무가 있기 때문이다. 없는 조각은 코드(TreeArt)가 남은 것으로 채운다.

  python tools/build_tree_metas.py
"""
import hashlib
import os
import re

from PIL import Image

DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Sprites", "Trees")

CELL_W, CELL_H = 48, 96
SHEET_H = 96
# (이름 꼬리표, x, y, w, h, 기준점) — y는 유니티 기준(아래에서부터).
PARTS = [
    ("stage0", 0 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
    ("stage1", 1 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
    ("stage2", 2 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
    ("stump",  4 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
    ("top",    5 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
    # 나뭇잎 4개는 3번 칸 가운데 아래 16x16 안에 8x8씩. 유니티 y는 아래에서부터라 위/아래가 뒤집힌다.
    ("leaf0", 3 * CELL_W + 16, 8,  8, 8, "center"),
    ("leaf1", 3 * CELL_W + 24, 8,  8, 8, "center"),
    ("leaf2", 3 * CELL_W + 16, 0,  8, 8, "center"),
    ("leaf3", 3 * CELL_W + 24, 0,  8, 8, "center"),
    ("full",   6 * CELL_W, 0, CELL_W, CELL_H, "bottom"),
]


def h32(*parts):
    """이름에서 항상 같은 값이 나오는 32자리 16진수 (스프라이트 id / guid 용)."""
    return hashlib.md5("|".join(parts).encode("utf-8")).hexdigest()


def internal_id(*parts):
    """조각마다 겹치지 않는 부호 있는 32비트 정수."""
    v = int(hashlib.md5(("id|" + "|".join(parts)).encode("utf-8")).hexdigest()[:8], 16)
    return v - (1 << 32) if v >= (1 << 31) else v


def is_empty(img, x, y, w, h):
    """유니티 좌표(아래에서부터)의 조각이 통째로 투명한지."""
    top = img.size[1] - y - h
    return img.crop((x, top, x + w, top + h)).getbbox() is None


def sprite_block(sheet, tag, x, y, w, h, pivot):
    name = "%s_%s" % (sheet, tag)
    align, px, py = (7, 0.5, 0) if pivot == "bottom" else (0, 0.5, 0.5)
    return f"""    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {w}
        height: {h}
      alignment: {align}
      pivot: {{x: {px}, y: {py}}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {h32(sheet, tag)}
      internalID: {internal_id(sheet, tag)}
      vertices: []
      indices: 
      edges: []
      weights: []
"""


def meta(sheet, guid, parts):
    sprites = "".join(sprite_block(sheet, *p) for p in parts)
    names = "".join(
        "  - first:\n      213: %d\n    second: %s_%s\n" % (internal_id(sheet, p[0]), sheet, p[0])
        for p in parts
    )
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
{names}  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 16
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
{sprites}    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def existing_guid(path):
    """이미 임포트된 적이 있으면 guid를 지킨다 — 씬/타일이 가리키던 것이 끊기지 않게."""
    if not os.path.exists(path):
        return None
    m = re.search(r"^guid: ([0-9a-f]{32})", open(path, encoding="utf-8").read(), re.M)
    return m.group(1) if m else None


def main():
    for f in sorted(os.listdir(DIR)):
        if not f.endswith(".png"):
            continue
        sheet = f[:-4]
        img = Image.open(os.path.join(DIR, f)).convert("RGBA")
        parts = [p for p in PARTS if not is_empty(img, *p[1:5])]
        path = os.path.join(DIR, f + ".meta")
        guid = existing_guid(path) or h32("tree-sheet", sheet)
        open(path, "w", encoding="utf-8", newline="\n").write(meta(sheet, guid, parts))
        print("wrote", os.path.normpath(path), "(조각 %d/%d)" % (len(parts), len(PARTS)))

    folder = os.path.join(DIR, "..", "Trees.meta")
    if not os.path.exists(folder):
        open(folder, "w", encoding="utf-8", newline="\n").write(
            "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
            % h32("tree-folder")
        )
        print("wrote", os.path.normpath(folder))


if __name__ == "__main__":
    main()
