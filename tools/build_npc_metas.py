# -*- coding: utf-8 -*-
"""
Assets/Resources/Sprites/NPC/{NPC}/ 의 .meta 를 만든다 (유니티 임포트 설정 + 조각 자르기).

NPC 한 명의 아트는 파일 두 종류뿐이다.

  {NPC}/{NPC}_sprites.png      걸어 다니는 32x32 시트. 한 줄이 한 동작이고, 줄 순서는 정해져 있다:
                                 idle_down / idle_side / idle_up / walk_down / walk_side / walk_up
                               왼쪽을 볼 때는 side 를 좌우 반전해서 쓰므로 왼쪽 그림은 그리지 않는다.
                               칸이 통째로 비어 있으면 그 칸은 만들지 않는다 — 그래서 대기 동작이
                               한 장이든 네 장이든 같은 시트에 섞어 둘 수 있다.
                               조각 이름: {NPC}_idle_down_0, {NPC}_walk_side_2 ...

  {NPC}/Portraits/*.png        대화창에 띄우는 초상화. 파일 하나에 초상화 하나.
                               캔버스에 여백이 많고 같은 그림이 여러 벌 들어 있기도 해서,
                               <b>비어 있지 않은 부분의 가장 왼쪽 덩어리</b>만 잘라 쓴다.
                               조각 이름 = 파일 이름 (Leona_happy).

이미 .meta 가 있는 파일은 건드리지 않는다 — 유니티가 임포트하며 채워 넣은 내용과 그것을
가리키는 참조가 끊기기 때문이다. 다시 만들려면 .meta 를 지우고 돌린다.

  python tools/build_npc_metas.py
"""
import hashlib
import os
import re

from PIL import Image

DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Sprites", "NPC")

CELL = 32
# 시트의 줄 순서. NpcDatabase 가 찾는 이름과 같아야 한다.
ROWS = ["idle_down", "idle_side", "idle_up", "walk_down", "walk_side", "walk_up"]


def h32(*parts):
    """이름에서 항상 같은 값이 나오는 32자리 16진수 (스프라이트 id / guid 용)."""
    return hashlib.md5("|".join(parts).encode("utf-8")).hexdigest()


def internal_id(*parts):
    """조각마다 겹치지 않는 부호 있는 32비트 정수."""
    v = int(hashlib.md5(("id|" + "|".join(parts)).encode("utf-8")).hexdigest()[:8], 16)
    return v - (1 << 32) if v >= (1 << 31) else v


def sprite_block(key, name, x, y, w, h):
    return f"""    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {w}
        height: {h}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {h32(key, name)}
      internalID: {internal_id(key, name)}
      vertices: []
      indices:
      edges: []
      weights: []
"""


def meta(key, guid, parts):
    """parts: (이름, x, y, w, h) 목록. y 는 유니티 기준(아래에서부터)."""
    sprites = "".join(sprite_block(key, *p) for p in parts)
    names = "".join(
        "  - first:\n      213: %d\n    second: %s\n" % (internal_id(key, p[0]), p[0])
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


def folder_meta(guid):
    return "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid


def sheet_parts(npc, img):
    """시트를 32x32 칸으로 훑어 비어 있지 않은 칸만 조각으로 만든다."""
    w, h = img.size
    parts = []
    for row, clip in enumerate(ROWS):
        if (row + 1) * CELL > h:
            break
        frame = 0
        for col in range(w // CELL):
            box = (col * CELL, row * CELL, (col + 1) * CELL, (row + 1) * CELL)
            if img.crop(box).getbbox() is None:
                continue
            # 유니티 y 는 아래에서부터라 줄 번호가 뒤집힌다.
            parts.append(("%s_%s_%d" % (npc, clip, frame),
                          col * CELL, h - (row + 1) * CELL, CELL, CELL))
            frame += 1
    return parts


def portrait_part(name, img):
    """가장 왼쪽 덩어리만 잘라 낸다 (같은 그림이 여러 벌 들어 있는 내보내기 대비)."""
    box = img.getbbox()
    if box is None:
        return None
    w, h = img.size
    left, top, right, bottom = box
    # 세로로 통째로 비어 있는 열을 경계로 덩어리를 나눈다.
    alpha = img.split()[3]
    filled = [alpha.crop((x, 0, x + 1, h)).getbbox() is not None for x in range(w)]
    x = left
    while x < right and filled[x]:
        x += 1
    right = x
    return (name, left, h - bottom, right - left, bottom - top)


def write(path, text):
    if os.path.exists(path):
        print("skip ", os.path.normpath(path), "(이미 있음)")
        return False
    open(path, "w", encoding="utf-8", newline="\n").write(text)
    print("write", os.path.normpath(path))
    return True


def main():
    for npc in sorted(os.listdir(DIR)):
        folder = os.path.join(DIR, npc)
        if not os.path.isdir(folder):
            continue
        write(folder + ".meta", folder_meta(h32("npc-folder", npc)))

        sheet = os.path.join(folder, "%s_sprites.png" % npc)
        if os.path.exists(sheet):
            img = Image.open(sheet).convert("RGBA")
            parts = sheet_parts(npc, img)
            key = "npc-sheet|%s" % npc
            write(sheet + ".meta", meta(key, h32(key), parts))
            print("      %s: 조각 %d개" % (npc, len(parts)))

        portraits = os.path.join(folder, "Portraits")
        if not os.path.isdir(portraits):
            continue
        write(portraits + ".meta", folder_meta(h32("npc-portraits", npc)))
        for f in sorted(os.listdir(portraits)):
            if not f.endswith(".png"):
                continue
            path = os.path.join(portraits, f)
            img = Image.open(path).convert("RGBA")
            part = portrait_part(f[:-4], img)
            if part is None:
                print("skip ", f, "(빈 그림)")
                continue
            key = "npc-portrait|%s|%s" % (npc, f[:-4])
            write(path + ".meta", meta(key, h32(key), [part]))


if __name__ == "__main__":
    main()
