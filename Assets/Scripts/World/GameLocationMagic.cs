using UnityEngine;

namespace FarmMVP
{
    public partial class GameLocation
    {
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

        /// <summary>파괴로 렌더러가 사라지기 전에 타격 위치를 얻는다.</summary>
        public Vector3 MagicEffectPosition(MagicType type, Vector2Int tile)
        {
            if (type == MagicType.Water || type == MagicType.Wind || type == MagicType.Shovel)
                return new Vector3(tile.x, tile.y - 0.5f, 0);

            SpriteRenderer target = null;
            if (type == MagicType.Blade) _treeRenderers.TryGetValue(tile, out target);
            if (type == MagicType.Rock) _rockRenderers.TryGetValue(tile, out target);
            if (target == null && (type == MagicType.Blade || type == MagicType.Rock))
                _debrisRenderers.TryGetValue(tile, out target);
            return target != null ? target.bounds.center : new Vector3(tile.x, tile.y, 0);
        }
    }
}
