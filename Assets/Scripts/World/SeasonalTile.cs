using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// 계절에 따라 그림이 바뀌는 바닥 타일.
    ///
    /// 맵을 계절마다 다시 칠하지 않기 위한 핵심 조각이다. 에셋 팩의 사계절 잔디 타일셋은
    /// 격자 배치가 완전히 같아서(실루엣 픽셀 차이 0) 같은 칸 번호 = 같은 타일이다.
    /// 그래서 타일 하나가 스프라이트 4장을 들고 있다가 지금 계절 것을 내주면 된다.
    ///
    /// 계절이 바뀌면 Tilemap.RefreshAllTiles()를 부르는 쪽(GameLocation)이 다시 물어본다.
    /// 비어 있는 계절은 봄 그림으로 대체하므로, 아직 겨울 타일셋을 안 넣었어도 깨지지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonalTile", menuName = "Farm/Seasonal Tile")]
    public class SeasonalTile : TileBase
    {
        [Tooltip("봄, 여름, 가을, 겨울 순서")]
        public Sprite[] seasonSprites = new Sprite[Seasons.SeasonCount];

        public Tile.ColliderType colliderType = Tile.ColliderType.None;

        public Sprite SpriteFor(Season season)
        {
            if (seasonSprites == null || seasonSprites.Length == 0) return null;

            int i = (int)season;
            if (i < seasonSprites.Length && seasonSprites[i] != null) return seasonSprites[i];

            // 그 계절 그림이 아직 없으면 있는 것 중 첫 번째(보통 봄)로 대체한다
            foreach (var s in seasonSprites)
                if (s != null) return s;
            return null;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = SpriteFor(Seasons.Current);
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.colliderType = colliderType;
            tileData.flags = TileFlags.LockColor;
        }
    }
}
