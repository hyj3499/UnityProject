# -*- coding: utf-8 -*-
"""
Assets/Resources/Sprites/Plants/{풀}_{계절}.png 의 .meta 를 만든다.

시트 한 장을 가로로 <b>3등분</b>해 조각 3개로 자른다. 이름이 곧 코드가 찾는 열쇠다 (PlantArt 참고):
  {풀}_{계절}_0 / _1 / _2   잔디는 성장 0~2단계, 잡초는 생김새 3종

조각 크기는 png에서 읽는다 (가로 1/3 x 세로 전부). 그래서 풀마다 키가 달라도 된다 —
잔디는 45x20(=15x20 셋), 잡초는 45x12(=15x12 셋)처럼.

기준점(pivot)은 <b>아래 가운데</b>다. 풀은 칸 격자(16x16)에 맞지 않는 크기라 한 칸 안에
여러 포기를 조금씩 어긋나게 심는데, 기준점이 밑동이어야 어디에 놓든 땅에 붙어 보이고
지나갈 때 흔들리는 축도 밑동이 된다.

  python tools/build_plant_metas.py
"""
import hashlib
import os
import re

from PIL import Image

DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Sprites", "Plants")

CELLS = 3


def h32(*parts):
    return hashlib.md5("|".join(parts).encode("utf-8")).hexdigest()


def internal_id(*parts):
    v = int(hashlib.md5(("id|" + "|".join(parts)).encode("utf-8")).hexdigest()[:8], 16)
    return v - (1 << 32) if v >= (1 << 31) else v


def sprite_block(sheet, i, cw, ch):
    name = "%s_%d" % (sheet, i)
    return f"""    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {i * cw}
        y: 0
        width: {cw}
        height: {ch}
      alignment: 7
      pivot: {{x: 0.5, y: 0}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {h32(sheet, str(i))}
      internalID: {internal_id(sheet, str(i))}
      vertices: []
      indices: 
      edges: []
      weights: []
"""


def meta(sheet, guid, cw, ch):
    sprites = "".join(sprite_block(sheet, i, cw, ch) for i in range(CELLS))
    names = "".join("  - first:\n      213: %d\n    second: %s_%d\n"
                    % (internal_id(sheet, str(i)), sheet, i) for i in range(CELLS))
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
    """유니티가 이미 매긴 guid는 지킨다 — 그 그림을 가리키던 것이 끊기지 않게."""
    if not os.path.exists(path):
        return None
    m = re.search(r"^guid: ([0-9a-f]{32})", open(path, encoding="utf-8").read(), re.M)
    return m.group(1) if m else None


def main():
    for f in sorted(os.listdir(DIR)):
        if not f.endswith(".png"):
            continue
        sheet = f[:-4]
        w, h = Image.open(os.path.join(DIR, f)).size
        if w % CELLS != 0:
            print("skip ", f, (w, h), "- 가로가 %d로 나누어떨어지지 않습니다" % CELLS)
            continue
        path = os.path.join(DIR, f + ".meta")
        open(path, "w", encoding="utf-8", newline="\n").write(
            meta(sheet, existing_guid(path) or h32("plant-sheet", sheet), w // CELLS, h))
        print("wrote", os.path.normpath(path), "(조각 %dx%d)" % (w // CELLS, h))


if __name__ == "__main__":
    main()
