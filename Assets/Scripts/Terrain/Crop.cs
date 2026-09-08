using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 한 작물이 여러 색으로 자랄 때의 색 하나 (파프리카). 다 자란 모습과 수확물이 색마다 다르므로
    /// 그 두 가지 그림 번호와, 인벤토리에 들어갈 아이템 id를 들고 있다.
    /// </summary>
    public class CropVariant
    {
        public string id;            // 아이템 id (예: "bell_pepper_red")
        public string name;          // 표시 이름
        public int ripeStageIndex;   // 다 자란 모습의 시트 번호
        public int fruitIndex;       // 수확물 아이콘의 시트 번호
        public string dropSpriteName; // Sprites/Crops/Drops/{이것} — 바닥에 떨어진 모습
    }

    /// <summary>
    /// Static crop definition (design doc §17). New crops = new CropDef.
    ///
    /// 그림은 전부 <b>이름 규칙</b>으로 찾는다 — Resources/Sprites/Crops/{sheet}.png 를 잘라
    /// 조각마다 "{sheet}_{번호}"라고 이름 붙여 두면 여기서 번호만 적으면 된다:
    ///
    ///   {sheet}_0 ... {sheet}_N   자라는 단계 (0 = 막 심은 씨앗)
    ///   {sheet}_(마지막 번호)      수확물 = 인벤토리 아이콘
    ///
    /// 단계 번호가 연속이 아닌 시트도 있어서(쓰지 않는 칸을 "_nouse"로 빼 둔 것 등)
    /// 단계는 개수가 아니라 <b>번호 목록</b>으로 적는다.
    /// </summary>
    public class CropDef
    {
        public string cropId;
        public string name;

        /// <summary>Resources/Sprites/Crops 안의 시트 이름 (예: "Adzuki Bean").</summary>
        public string sheet;

        /// <summary>자라는 단계로 쓸 시트 번호들. 첫 번째가 막 심은 모습.</summary>
        public int[] stageIndices;

        /// <summary>수확물(인벤토리) 아이콘의 시트 번호 = 시트의 마지막 번호.</summary>
        public int fruitIndex;

        /// <summary>Sprites/Crops/Drops/{이것} — 바닥에 떨어졌을 때의 그림. 없으면 인벤토리 아이콘을 쓴다.</summary>
        public string dropSpriteName;

        public int daysPerStage;     // days needed to advance one stage

        /// <summary>이 작물을 심을 수 있는 계절. 다른 계절이 되면 심어 둔 것이 시들어 사라진다.</summary>
        public SeasonFlags seasons = SeasonFlags.All;

        /// <summary>true면 다작 — 수확해도 사라지지 않고 한 단계 앞으로 돌아가 다시 열매를 맺는다.</summary>
        public bool regrow;

        public int seedPrice;        // 상점 판매가
        public int sellPrice;        // 수확물 1개를 배송함에 넣었을 때

        /// <summary>색이 여러 가지인 작물(파프리카)의 색 목록. null이면 색이 하나뿐.</summary>
        public CropVariant[] variants;

        public bool HasVariants => variants != null && variants.Length > 0;

        /// <summary>다 자란 단계의 번호. 색이 여러 개면 마지막 단계가 색마다 다른 그림이라 한 칸 더 있다.</summary>
        public int MaxGrowthStage => stageIndices.Length - 1 + (HasVariants ? 1 : 0);

        /// <summary>
        /// -1이면 단일 수확 작물: 수확하면 완전히 사라진다.
        /// 0 이상이면 다작 작물: 수확 후 이 단계로 되돌아가 계속 자라며, 다시 MaxGrowthStage에
        /// 도달하면 또 수확할 수 있다 (예: 딸기).
        /// </summary>
        public int RegrowStage => regrow ? Mathf.Max(0, MaxGrowthStage - 1) : -1;

        public string SeedItemId => cropId + "_seed";
        public string SeedSpriteName => Naming.NoSpace(sheet) + "Seed";

        /// <summary>공백을 뺀 시트 이름 — 드랍 그림 파일 이름의 기본값.</summary>
        public string DefaultDropSpriteName => Naming.NoSpace(sheet);

        /// <summary>색이 여러 개면 색마다, 하나면 작물 하나 — 수확했을 때 인벤토리에 들어가는 것들.</summary>
        public IEnumerable<CropVariant> Harvests
        {
            get
            {
                if (HasVariants)
                {
                    foreach (var v in variants) yield return v;
                    yield break;
                }
                yield return new CropVariant
                {
                    id = cropId,
                    name = name,
                    ripeStageIndex = stageIndices[stageIndices.Length - 1],
                    fruitIndex = fruitIndex,
                    dropSpriteName = dropSpriteName ?? DefaultDropSpriteName
                };
            }
        }

        public string DropTableIdFor(int variant) => "crop_" + HarvestIdFor(variant);

        public string HarvestIdFor(int variant)
            => HasVariants ? variants[Mathf.Clamp(variant, 0, variants.Length - 1)].id : cropId;

        /// <summary>단계 번호에 해당하는 그림. 색이 여러 개면 마지막 단계는 그 색의 그림이 나온다.</summary>
        public Sprite StageSprite(int stage, int variant)
        {
            if (HasVariants && stage >= stageIndices.Length)
                return SheetSprite(variants[Mathf.Clamp(variant, 0, variants.Length - 1)].ripeStageIndex);

            int i = Mathf.Clamp(stage, 0, stageIndices.Length - 1);
            return SheetSprite(stageIndices[i]);
        }

        /// <summary>시트 안의 번호로 그림 하나 ("Carrot_3").</summary>
        public Sprite SheetSprite(int index) => AssetLibrary.GetSprite($"Sprites/Crops/{sheet}_{index}");
    }

    /// <summary>Runtime crop instance planted in a HoeDirt tile.</summary>
    [Serializable]
    public class Crop
    {
        public string cropId;
        public int growthStage;   // 0 = seed
        public int dayCounter;    // days accumulated toward next stage
        public int variant;       // 색이 여러 가지인 작물에서 몇 번째 색으로 자랐는지 (아니면 0)

        public Crop(string cropId) : this(cropId, RollVariant(cropId)) { }

        public Crop(string cropId, int variant)
        {
            this.cropId = cropId;
            this.variant = variant;
            growthStage = 0;
            dayCounter = 0;
        }

        /// <summary>심는 순간 색을 정한다 — 파프리카는 세 가지 색 중 하나로 랜덤하게 자란다.</summary>
        private static int RollVariant(string cropId)
        {
            var def = CropDatabase.Get(cropId);
            return def != null && def.HasVariants ? UnityEngine.Random.Range(0, def.variants.Length) : 0;
        }

        public CropDef Def => CropDatabase.Get(cropId);
        public bool IsHarvestable => growthStage >= Def.MaxGrowthStage;
        public bool IsRegrowable => Def.RegrowStage >= 0;

        /// <summary>수확했을 때 쓸 드랍 테이블 (색이 여러 개면 그 색의 것).</summary>
        public string DropTableId => Def.DropTableIdFor(variant);

        /// <summary>Advance growth by one day if watered.</summary>
        public void Grow(bool watered)
        {
            if (!watered) return;
            if (IsHarvestable) return;
            dayCounter++;
            if (dayCounter >= Def.daysPerStage)
            {
                dayCounter = 0;
                growthStage = Mathf.Min(growthStage + 1, Def.MaxGrowthStage);
            }
        }

        public Sprite GetSprite() => Def.StageSprite(growthStage, variant);

        /// <summary>
        /// 수확 처리. 다작 작물이면 RegrowStage로 되돌리고 계속 살아남아 true를 반환한다.
        /// 단일 수확 작물이면 아무것도 바꾸지 않고 false를 반환 — 호출자(HoeDirt)가 제거해야 한다.
        /// </summary>
        public bool HarvestAndRegrow()
        {
            if (!IsRegrowable) return false;
            growthStage = Def.RegrowStage;
            dayCounter = 0;
            return true;
        }
    }

    internal static class Naming
    {
        /// <summary>"Adzuki Bean" -> "AdzukiBean". 공백이 들어간 시트 이름을 파일/아이템 이름으로 쓸 때.</summary>
        public static string NoSpace(string s) => s == null ? null : s.Replace(" ", "");
    }
}
