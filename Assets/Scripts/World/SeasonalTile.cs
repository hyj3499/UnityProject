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
        /// <summary>한 계절의 그림. 물처럼 움직이는 타일은 여러 장, 보통은 한 장.</summary>
        [System.Serializable]
        public class Frames
        {
            public Sprite[] sprites;

            public Sprite First => sprites != null && sprites.Length > 0 ? sprites[0] : null;
            public bool Animated => sprites != null && sprites.Length > 1;
        }

        [Tooltip("봄, 여름, 가을, 겨울 순서")]
        public Frames[] seasonFrames = new Frames[Seasons.SeasonCount];

        /// <summary>초당 프레임 수. 여러 장일 때만 쓰인다.</summary>
        public float fps = 4f;

        public Tile.ColliderType colliderType = Tile.ColliderType.None;

        /// <summary>그 계절의 그림들. 아직 없으면 있는 계절 것(보통 봄)으로 대체한다.</summary>
        public Frames FramesFor(Season season)
        {
            if (seasonFrames == null || seasonFrames.Length == 0) return null;

            int i = (int)season;
            if (i < seasonFrames.Length && seasonFrames[i] != null && seasonFrames[i].First != null)
                return seasonFrames[i];

            foreach (var f in seasonFrames)
                if (f != null && f.First != null) return f;
            return null;
        }

        public Sprite SpriteFor(Season season) => FramesFor(season)?.First;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = SpriteFor(Seasons.Current);
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.colliderType = colliderType;
            tileData.flags = TileFlags.LockColor;
        }

        /// <summary>
        /// 여러 장짜리면 Tilemap이 알아서 돌려 준다 — 물결이 흐르는 동안 C# 코드는 한 줄도 돌지 않는다.
        /// 계절이 바뀌면 RefreshAllTiles()가 여기를 다시 물어보므로 계절마다 다른 물결도 가능하다.
        /// </summary>
        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
                                                  ref TileAnimationData tileAnimationData)
        {
            var frames = FramesFor(Seasons.Current);
            if (frames == null || !frames.Animated) return false;

            tileAnimationData.animatedSprites = frames.sprites;
            tileAnimationData.animationSpeed = fps;
            // 이 시트의 한 모양은 4x4 조각이 합쳐진 오토타일이다. 칸별 위상을 어긋나게 하면
            // 한 모양 안의 모서리/가장자리가 서로 다른 프레임이 되어 물결이 깨져 보인다.
            // 따라서 모든 조각이 같은 프레임을 보도록 하나의 공통 시작 시각을 쓴다.
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
