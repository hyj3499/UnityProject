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
                // 7 sprites (Strawberry_0..6): stage 6 is harvest-ready
                maxGrowthStage = 6,
                daysPerStage = 1,
                harvestItemId = "strawberry",
                harvestAmount = 1,
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
