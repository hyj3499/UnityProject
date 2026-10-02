using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>Sprite Editor에서 잘라 둔 그림들을 순서대로 반복하는 런타임 타일.</summary>
    public sealed class SlicedAnimationTile : Tile
    {
        public Sprite[] frames;
        public float framesPerSecond = AnimationTileset.FramesPerSecond;

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
                                                  ref TileAnimationData animationData)
        {
            if (frames == null || frames.Length < 2) return false;

            animationData.animatedSprites = frames;
            animationData.animationSpeed = framesPerSecond;
            animationData.animationStartTime = 0f;
            return true;
        }
    }
}
