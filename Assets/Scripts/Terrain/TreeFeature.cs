using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>나무 한 번 벤 결과.</summary>
    public enum ChopResult
    {
        /// <summary>아직 버틴다 — 휘청이기만 한다.</summary>
        Hit,
        /// <summary>다 자란 나무가 쓰러졌다 — 윗부분이 옆으로 넘어가고 그루터기만 남는다.</summary>
        Felled,
        /// <summary>자리에서 아예 없어졌다 (덜 자란 나무를 뽑았거나 그루터기를 치웠다).</summary>
        Cleared,
    }

    /// <summary>
    /// 심고 자라고 벨 수 있는 나무 한 그루 (design doc §10).
    ///
    /// 다 자란 나무(레벨3)는 그림이 두 장으로 나뉜다 — 바닥에 붙어 있는 <b>그루터기</b>와
    /// 그 위에 얹히는 <b>윗부분</b>. 벨 때 윗부분만 옆으로 넘어뜨리면 그루터기가 그대로 남으므로,
    /// "쓰러진 나무"를 위한 그림을 따로 그리지 않아도 된다.
    /// </summary>
    [Serializable]
    public class TreeFeature
    {
        public int x, y;
        public string treeId;
        public int growthStage;
        public int dayCounter;
        public int hp;

        /// <summary>쓰러뜨리고 남은 그루터기인지. 자라지 않고, 한 번 더 치면 자리가 비워진다.</summary>
        public bool isStump;

        public TreeFeature(int x, int y, string treeId, int growthStage)
        {
            this.x = x;
            this.y = y;
            this.treeId = TreeDatabase.ResolveId(treeId);
            this.growthStage = growthStage;
            hp = Def.maxHp;
        }

        public TreeDef Def => TreeDatabase.Get(treeId);
        public bool IsAlive => hp > 0;

        /// <summary>다 자란(=벨 것이 있는) 나무인지. 그루터기는 다 자란 나무가 아니다.</summary>
        public bool IsMature => !isStump && growthStage >= Def.maxGrowthStage;

        /// <summary>바닥에 붙어 그려지는 그림. 다 자랐으면 그루터기가 밑동이 된다.</summary>
        public Sprite GetSprite()
        {
            var season = Seasons.Current;
            return growthStage >= Def.maxGrowthStage
                ? TreeArt.Stump(Def.sheet, season)
                : TreeArt.Stage(Def.sheet, growthStage, season);
        }

        /// <summary>
        /// 그루터기 위에 겹쳐 그리는 윗부분. 다 자랐고 아직 쓰러지지 않았을 때만 있다.
        /// 기준점이 그루터기와 같아서 같은 자리에 그대로 놓으면 맞물린다.
        /// </summary>
        public Sprite GetTopSprite()
            => IsMature ? TreeArt.Top(Def.sheet, Seasons.Current) : null;

        /// <summary>벨 때 흩날릴 나뭇잎 그림들 (지금 계절 것).</summary>
        public Sprite[] GetLeafSprites() => TreeArt.Leaves(Def.sheet, Seasons.Current);

        /// <summary>하루가 지나 한 단계씩 자란다. 그루터기는 자라지 않는다.</summary>
        public void Grow()
        {
            if (isStump || growthStage >= Def.maxGrowthStage) return;

            dayCounter++;
            if (dayCounter < Def.daysPerStage) return;

            dayCounter = 0;
            growthStage = Mathf.Min(growthStage + 1, Def.maxGrowthStage);
        }

        /// <summary>
        /// 한 번 벤다.
        ///  · 아직 다 자라지 않은 나무는 한 번에 뽑히고 아무것도 남기지 않는다.
        ///  · 다 자란 나무는 hp만큼 때려야 쓰러지고, 쓰러지면 그루터기가 남는다.
        ///  · 남은 그루터기는 한 번 더 치우면 자리가 빈다.
        /// </summary>
        public ChopResult Chop()
        {
            if (!isStump && growthStage < Def.maxGrowthStage)
            {
                hp = 0;
                return ChopResult.Cleared;
            }

            hp--;
            if (hp > 0) return ChopResult.Hit;

            if (isStump) return ChopResult.Cleared;

            // 쓰러졌다 — 윗부분만 넘어가고 그루터기로 남는다.
            isStump = true;
            hp = Mathf.Max(1, Def.stumpHp);
            return ChopResult.Felled;
        }
    }
}
