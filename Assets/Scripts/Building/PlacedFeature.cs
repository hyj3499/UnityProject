using System;

namespace FarmMVP
{
    /// <summary>
    /// 맵에 실제로 놓인 설치물 하나 (울타리 한 칸, 길 한 칸). TreeFeature/RockFeature와 같은
    /// 자리에 있는 개념이고, 저장은 LocationData.placed가 맡는다.
    /// </summary>
    [Serializable]
    public class PlacedFeature
    {
        public int x, y;
        public string defId;
        /// <summary>문일 때만 의미가 있다. 열려 있으면 지나갈 수 있다.</summary>
        public bool open;

        public PlacedFeature(int x, int y, string defId, bool open = false)
        {
            this.x = x;
            this.y = y;
            this.defId = defId;
            this.open = open;
        }

        public PlaceableDef Def => PlaceableDatabase.Get(defId);

        /// <summary>지금 이 칸이 막혀 있는지 (열린 문은 지나갈 수 있다).</summary>
        public bool BlocksNow
        {
            get { var d = Def; return d != null && d.BlocksNow(open); }
        }

        public bool IsGate
        {
            get { var d = Def; return d != null && d.isGate; }
        }
    }
}
