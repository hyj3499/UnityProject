using UnityEngine;

namespace FarmMVP
{
    public partial class GameLocation
    {
        // 월드의 타일·오브젝트 아트는 16px = 1타일이다. magic9의 트리 크기는 이 기준으로 64px을 유지한다.
        private const float WorldPixelsPerTile = 16f;
        /// <summary>큰 바위/통나무의 여러 칸이 범위에 들어와도 한 번만 맞도록 기준 칸을 돌려준다.</summary>
        public Vector2Int MagicTargetAnchor(MagicType type, Vector2Int tile)
        {
            if (type == MagicType.Blade && HasChoppableTree(tile.x, tile.y)) return tile;
            if (type == MagicType.Rock && HasBreakableRock(tile.x, tile.y)) return tile;
            if ((type == MagicType.Blade && HasDebris(tile.x, tile.y, DebrisKind.Wood)) ||
                (type == MagicType.Rock && HasDebris(tile.x, tile.y, DebrisKind.Stone)))
                return _debrisAnchors[tile];
            return tile;
        }

        /// <summary>파괴 전에 대상 종류를 읽는다. 계절이 바뀌어도 같은 종류는 같은 이펙트를 쓴다.</summary>
        public int MagicEffectSheet(MagicType type, Vector2Int tile)
        {
            // 실제 TryApply와 같은 우선순위: 나무/바위가 잔해보다 먼저 맞는다.
            if (type == MagicType.Blade && HasChoppableTree(tile.x, tile.y)) return 9;
            if (type == MagicType.Rock && HasBreakableRock(tile.x, tile.y)) return 3;
            var anchor = MagicTargetAnchor(type, tile);
            if (debris.TryGetValue(anchor, out var target))
            {
                if (type == MagicType.Rock && target.kind == DebrisKind.Stone)
                {
                    if (target.variant == 2) return 7; // Stone_spring의 세 번째
                    if (target.variant == 3) return 8; // Stone_spring의 네 번째
                }
                if (type == MagicType.Blade && target.kind == DebrisKind.Wood)
                {
                    if (target.variant == 2) return 9;
                    if (target.variant == 3) return 10;
                }
            }
            return MagicEffect.DefaultSheet(type);
        }

        /// <summary>파괴로 렌더러가 사라지기 전에 타격 위치를 얻는다.</summary>
        public Vector3 MagicEffectPosition(MagicType type, Vector2Int tile)
        {
            tile = MagicTargetAnchor(type, tile);
            if (type == MagicType.Water || type == MagicType.Wind || type == MagicType.Shovel || type == MagicType.Earth)
                return new Vector3(tile.x, tile.y, 0);

            int sheet = MagicEffectSheet(type, tile);
            // 나뭇가지는 그림 중심이 아니라 해당 16×16 타일과 magic5의 오른쪽 아래 칸을 겹친다.
            if (sheet == 5) return new Vector3(tile.x, tile.y, 0);
            Bounds bounds = MagicTargetBounds(type, tile);
            return sheet == 9 || sheet == 10
                ? new Vector3(bounds.center.x, bounds.min.y, 0)
                : bounds.center;
        }

        /// <summary>파괴 전 오브젝트 크기를 확보한다. Trees의 번개는 종류·성장 단계와 무관하게 64×64로 통일한다.</summary>
        public Vector2 MagicEffectSize(MagicType type, Vector2Int tile)
        {
            tile = MagicTargetAnchor(type, tile);
            if (type == MagicType.Blade && HasChoppableTree(tile.x, tile.y))
                return Vector2.one * (64f / WorldPixelsPerTile);

            int sheet = MagicEffectSheet(type, tile);
            if (sheet == 7 || sheet == 8 || sheet == 9 || sheet == 10)
            {
                // 시트의 투명 여백을 감안해 대상보다 약간 크게 덮는다.
                float multiplier = sheet == 7 ? 2f : 1f;
                return (Vector2)MagicTargetBounds(type, tile).size * 1.15f * multiplier;
            }
            return (Vector2)MagicEffect.FrameSize(sheet) / MagicEffect.PixelsPerUnit;
        }

        private Bounds MagicTargetBounds(MagicType type, Vector2Int tile)
        {
            SpriteRenderer target = null;
            if (type == MagicType.Blade) _treeRenderers.TryGetValue(tile, out target);
            if (type == MagicType.Rock) _rockRenderers.TryGetValue(tile, out target);
            if (target == null && (type == MagicType.Blade || type == MagicType.Rock))
                _debrisRenderers.TryGetValue(tile, out target);
            if (target != null && target.sprite != null) return target.bounds;

            // 렌더러 생성 전에도 발판 크기와 바닥선을 사용한다.
            if (debris.TryGetValue(tile, out var feature))
            {
                Vector2 size = feature.Def.size;
                return new Bounds(new Vector3(tile.x + (size.x - 1) * 0.5f, tile.y + (size.y - 1) * 0.5f, 0),
                    new Vector3(size.x, size.y, 0));
            }
            return new Bounds(new Vector3(tile.x, tile.y, 0), Vector3.one);
        }
    }
}
