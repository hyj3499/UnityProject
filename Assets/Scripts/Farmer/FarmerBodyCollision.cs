using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Base's opaque locomotion silhouette, cached once for each facing and movement axis.
    /// Union Idle/Walk/Run so animation cannot shrink the body into a wall between frames.
    /// Only the leading contour is probed: retreating from an existing overlap remains possible.
    /// Back-facing movement uses the bottom of the feet, including its sideways slide probes.
    /// </summary>
    public static class FarmerBodyCollision
    {
        private static Vector2[][][] edges;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => edges = null;

        public static Vector2[] LeadingEdge(Direction facing, Direction movement)
        {
            if (edges == null) Load();
            return edges[(int)facing][(int)movement];
        }

        private static void Load()
        {
            var texture = Resources.Load<Texture2D>(FarmerPoses.SheetPath);
            var pixels = texture != null && texture.isReadable ? texture.GetPixels32() : null;
            if (pixels == null)
                Debug.LogError("[FarmerBodyCollision] Readable base_animations is required; using conservative body outline.");
            edges = new Vector2[4][][];
            int w = FarmerPoses.FrameWidth, h = FarmerPoses.FrameHeight;
            for (int f = 0; f < 4; f++)
            {
                var occupied = new bool[w, h];
                if (pixels != null)
                {
                    foreach (var animation in new[] { FarmerAnim.Idle, FarmerAnim.Walk, FarmerAnim.Run })
                    {
                        var clip = FarmerPoses.Get(animation, (Direction)f);
                        int bottom = texture.height - (clip.row + 1) * h;
                        if (bottom < 0) continue;
                        for (int frame = 0; frame < clip.count && (frame + 1) * w <= texture.width; frame++)
                            for (int y = 0; y < h; y++)
                                for (int x = 0; x < w; x++)
                                    if (pixels[(bottom + y) * texture.width + frame * w + x].a != 0)
                                        occupied[FarmerPoses.Mirrored((Direction)f) ? w - 1 - x : x, y] = true;
                    }
                }
                else
                {
                    for (int y = 0; y < h; y++)
                        for (int x = w / 4; x < w * 3 / 4; x++) occupied[x, y] = true;
                }

                int feetRow = -1;
                if ((Direction)f == Direction.Up)
                {
                    // 뒷모습은 머리/팔 대신 실제 발끝의 불투명 픽셀만 기준으로 삼는다.
                    // 보정용 좌우 검사도 같은 높이를 써야 상체가 절벽에 겹친 상태에서 걸리지 않는다.
                    for (int y = 0; y < h && feetRow < 0; y++)
                        for (int x = 0; x < w; x++)
                            if (occupied[x, y]) { feetRow = y; break; }
                    for (int y = 0; y < h; y++)
                        if (y != feetRow)
                            for (int x = 0; x < w; x++) occupied[x, y] = false;
                }

                edges[f] = new Vector2[4][];
                for (int d = 0; d < 4; d++)
                {
                    var edge = BuildEdge(occupied, (Direction)d);
                    if (feetRow >= 0)
                    {
                        float skin = (Direction)d == Direction.Up ? 0.001f
                            : (Direction)d == Direction.Down ? -0.001f : 0f;
                        float feetY = (feetRow - h * 0.5f + skin) / FarmerPoses.PixelsPerUnit
                            + FarmerPoses.BodyOffsetY;
                        for (int i = 0; i < edge.Length; i++) edge[i].y = feetY;
                    }
                    edges[f][d] = edge;
                }
            }
        }

        private static Vector2[] BuildEdge(bool[,] occupied, Direction movement)
        {
            int w = occupied.GetLength(0), h = occupied.GetLength(1);
            bool horizontal = movement == Direction.Left || movement == Direction.Right;
            bool positive = movement == Direction.Right || movement == Direction.Up;
            var result = new List<Vector2>();
            int lines = horizontal ? h : w, length = horizontal ? w : h;
            for (int line = 0; line < lines; line++)
            {
                for (int i = 0; i < length; i++)
                {
                    int along = positive ? length - 1 - i : i;
                    if (!occupied[horizontal ? along : line, horizontal ? line : along]) continue;
                    // Probe just outside the pixel edge, with samples across its width.
                    // Avoid treating transparent padding or empty silhouette corners as body.
                    float edge = along + (positive ? 1.001f : -0.001f);
                    for (int sample = 0; sample < 3; sample++)
                    {
                        float across = line + (sample == 0 ? 0.001f : sample == 1 ? 0.5f : 0.999f);
                        var pixel = horizontal ? new Vector2(edge, across) : new Vector2(across, edge);
                        result.Add(new Vector2((pixel.x - w * 0.5f) / FarmerPoses.PixelsPerUnit,
                            (pixel.y - h * 0.5f) / FarmerPoses.PixelsPerUnit + FarmerPoses.BodyOffsetY));
                    }
                    break;
                }
            }
            return result.ToArray();
        }
    }
}
