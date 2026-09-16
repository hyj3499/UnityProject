using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 전용 빌더가 없는 바깥 맵(계곡·마을·산·숲·바다...)을 짓는다.
    ///
    /// 이 맵들은 <b>전부 씬에 칠해서</b> 만든다 — 바닥은 "Location_{맵}", 못 지나갈 곳은
    /// "Blocked_{맵}", 배경 오브젝트는 "Fixed_{맵}", 옆 맵으로 가는 칸은 "Objects_{맵}"에 칠한
    /// "Obj_Exit{맵}" 마커다. 그래서 맵을 하나 더 늘려도 이 파일은 길어지지 않고,
    /// 맵마다 빌더 파일을 따로 만들 필요도 없다.
    ///
    /// 특별한 것(상점·배송함처럼 코드가 알아야 하는 시설)이 생기면 그때 그 맵 전용 빌더를 만들어
    /// <see cref="GameLocation.Build"/>의 switch에 한 줄 추가하면 된다 — Farm1이 그렇게 하고 있다.
    /// </summary>
    internal static class OutdoorMapBuilder
    {
        /// <summary>아직 바닥을 한 칸도 안 칠했을 때 쓰는 크기. 칠하는 순간 칠한 영역이 크기가 된다.</summary>
        private const int DefaultWidth = 30, DefaultHeight = 25;

        public static void Build(GameLocation loc, GameData data)
        {
            loc.SetDefaultSize(DefaultWidth, DefaultHeight);

            // 아직 아무것도 안 칠한 맵은 맨땅(= 아무것도 안 보임)이 된다. 그러면 만들다 만 것인지
            // 깨진 것인지 알 수 없으니 임시로 풀밭을 깔아 준다. 바닥을 칠하면 이 줄은 지나간다.
            if (!loc.HasPaintedGround)
            {
                for (int x = 0; x < loc.width; x++)
                    for (int y = 0; y < loc.height; y++)
                        loc.PlaceTile(AssetLibrary.Grass, x, y, GameLocation.GroundOrder);
            }
            loc.TryPlaceGroundTilemap(loc.id);   // Resources/Prefabs/{맵}Ground.prefab 이 있으면 그것도 함께

            var locData = data.GetLocation(loc.id);
            if (loc.HasObjectMarkers) loc.ApplyObjectMarkers(locData);

            // 칠하지 않은 통로는 MapExits가 맵의 변에 걸어 준다 (GameLocation.Build에서 호출) —
            // 그 변에서 맵 밖으로 한 발 내디디면 이어진 맵으로 넘어간다.
            loc.RestoreFeatures(locData);
        }
    }
}
