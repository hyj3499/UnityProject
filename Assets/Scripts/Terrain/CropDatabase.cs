using System.Collections.Generic;

namespace FarmMVP
{
    public static class CropDatabase
    {
        private static readonly Dictionary<string, CropDef> _defs = new Dictionary<string, CropDef>();
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new CropDef
            {
                cropId = "strawberry",
                name = "딸기",
                // 6 sprites (Strawberry_0..5): 새싹(0) -> 성장 -> 결실(5, 수확 가능)
                maxGrowthStage = 5,
                daysPerStage = 1,
                dropTableId = "crop_strawberry",
                regrowStage = 4, // 다작: 수확하면 4단계로 되돌아가 하루만 더 자라면 다시 수확 가능
                stageSprites = () => { AssetLibrary.EnsureLoaded(); return AssetLibrary.StrawberryStages; }
            });
        }

        private static void Register(CropDef d) => _defs[d.cropId] = d;

        public static CropDef Get(string id)
        {
            Init();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }
    }
}
