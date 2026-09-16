using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// <see cref="MapGraph"/>에 적힌 통로를 <b>맵의 변</b>에 걸어 준다 — 그 변에서 맵 밖으로 한 발
    /// 내디디면 이어진 맵으로 넘어간다.
    ///
    /// 출구 <b>칸</b>("Obj_Exit{맵}" 마커)과 다른 점이 둘 있다.
    ///   · 밟는다고 넘어가지 않는다. 가장자리 칸에 서 있는 것은 자유롭고, 바깥쪽으로 한 번 더
    ///     움직여야 넘어간다 — 그래서 도착하자마자 되돌아가 버리는 일이 없다.
    ///   · 칠할 것이 없다. 맵 밖에는 타일을 칠할 수 없으니(맵 크기는 칠한 바닥이 정한다)
    ///     "맵 끝을 넘어가면 다음 맵"은 마커가 아니라 이렇게 변으로 다루는 편이 자연스럽다.
    ///
    /// 어느 한쪽을 특별한 자리(다리·동굴 입구)로 만들고 싶으면 그 맵에 "Obj_Exit{맵}"을 칠하면 된다.
    /// 칠해 둔 출구 칸이 있는 이웃은 변을 열지 않는다 — <b>칠한 쪽이 언제나 이긴다</b>.
    /// </summary>
    internal static class MapExits
    {
        public static void EnsureEdgeExits(GameLocation loc)
        {
            if (loc == null || MapGraph.IsIndoor(loc.id)) return;

            foreach (var neighbor in MapGraph.Neighbors(loc.id))
            {
                if (loc.HasExitTo(neighbor.Key)) continue;   // 출구 칸을 칠해 뒀다
                loc.AddEdgeExit(neighbor.Value, neighbor.Key);
            }
        }
    }
}
