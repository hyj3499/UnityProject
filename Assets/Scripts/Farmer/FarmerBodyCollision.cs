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
        private static Vector2[] idleFootProbes;
        private static Vector2[][] idleFootLines;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            edges = null;
            idleFootProbes = null;
            idleFootLines = null;
        }

        public static Vector2[] LeadingEdge(Direction facing, Direction movement)
        {
            if (edges == null) Load();
            return edges[(int)facing][(int)movement];
        }

        /// <summary>
        /// 오브젝트 마스크에 대는 발끝 한 점. Idle 첫 프레임의 실제 발바닥 행을 기준으로 잡는다.
        /// 오른쪽은 발바닥의 맨 오른쪽 끝, 왼쪽과 위쪽은 맨 왼쪽 끝을 쓴다.
        /// 아래쪽은 두 발 사이를 쓴다.
        /// </summary>
        public static Vector2 IdleFootProbe(Direction facing)
        {
            if (idleFootProbes == null) Load();
            return idleFootProbes[(int)facing];
        }

        /// <summary>
        /// 색칠 오브젝트에 대는 idle 발바닥 선. 한 끝점만 검사하면 반대 발이 마스크에
        /// 걸린 채 통과할 수 있으므로, 같은 발바닥 행의 불투명 픽셀 전체를 쓴다.
        /// 각 방향에서 요청한 바깥 끝점도 첫 샘플로 유지한다.
        /// </summary>
        public static Vector2[] IdleFootLine(Direction facing)
        {
            if (idleFootLines == null) Load();
            return idleFootLines[(int)facing];
        }

        private static void Load()
        {
            var texture = Resources.Load<Texture2D>(FarmerPoses.SheetPath);
            var pixels = texture != null && texture.isReadable ? texture.GetPixels32() : null;
            if (pixels == null)
                Debug.LogError("[FarmerBodyCollision] Readable base_animations is required; using conservative body outline.");
            edges = new Vector2[4][][];
            idleFootProbes = new Vector2[4];
            idleFootLines = new Vector2[4][];
            int w = FarmerPoses.FrameWidth, h = FarmerPoses.FrameHeight;
            for (int f = 0; f < 4; f++)
            {
                idleFootProbes[f] = ReadIdleFootProbe(pixels, texture, (Direction)f, w, h,
                    out idleFootLines[f]);
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

        private static Vector2 ReadIdleFootProbe(Color32[] pixels, Texture2D texture, Direction facing, int w, int h,
            out Vector2[] footLine)
        {
            // Texture read failure keeps a conservative pair of visible feet.
            if (pixels == null || texture == null)
                return FallbackIdleFootProbe(facing, w, h, out footLine);

            var clip = FarmerPoses.Get(FarmerAnim.Idle, facing);
            int bottom = texture.height - (clip.row + 1) * h;
            if (bottom < 0 || bottom + h > texture.height) return FallbackIdleFootProbe(facing, w, h, out footLine);

            int footRow = h, left = w, right = -1;
            var solePixels = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sourceX = FarmerPoses.Mirrored(facing) ? w - 1 - x : x;
                    if (pixels[(bottom + y) * texture.width + sourceX].a == 0) continue;
                    if (y < footRow)
                    {
                        footRow = y;
                        left = right = x;
                        solePixels.Clear();
                        solePixels.Add(x);
                    }
                    else if (y == footRow)
                    {
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        solePixels.Add(x);
                    }
                }
            if (right < 0) return FallbackIdleFootProbe(facing, w, h, out footLine);

            float xPoint;
            switch (facing)
            {
                case Direction.Right: xPoint = right + 1.001f; break; // foot's outer-right edge
                case Direction.Left:
                case Direction.Up: xPoint = left - 0.001f; break;   // requested leftmost foot edge
                default: xPoint = (left + right + 1f) * 0.5f; break;
            }
            float yPoint = (footRow - h * 0.5f - 0.001f) / FarmerPoses.PixelsPerUnit + FarmerPoses.BodyOffsetY;
            var samples = new List<Vector2>(solePixels.Count + 1)
            {
                new Vector2((xPoint - w * 0.5f) / FarmerPoses.PixelsPerUnit, yPoint)
            };
            foreach (int x in solePixels)
            {
                var sample = new Vector2((x + 0.5f - w * 0.5f) / FarmerPoses.PixelsPerUnit, yPoint);
                bool duplicate = false;
                foreach (var existing in samples)
                    if ((existing - sample).sqrMagnitude < 0.0000001f) { duplicate = true; break; }
                if (!duplicate) samples.Add(sample);
            }
            footLine = samples.ToArray();
            return samples[0];
        }

        private static Vector2 FallbackIdleFootProbe(Direction facing, int w, int h, out Vector2[] footLine)
        {
            float left = w * 0.25f - 0.001f, right = w * 0.75f + 0.001f;
            float x = facing == Direction.Right ? right : facing == Direction.Down ? (left + right) * 0.5f : left;
            float y = (-h * 0.5f - 0.001f) / FarmerPoses.PixelsPerUnit + FarmerPoses.BodyOffsetY;
            var probe = new Vector2((x - w * 0.5f) / FarmerPoses.PixelsPerUnit, y);
            footLine = new[]
            {
                probe,
                new Vector2((w * 0.25f + 0.5f - w * 0.5f) / FarmerPoses.PixelsPerUnit, y),
                new Vector2((w * 0.75f - 0.5f - w * 0.5f) / FarmerPoses.PixelsPerUnit, y),
            };
            return probe;
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
