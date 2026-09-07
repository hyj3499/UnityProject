using System;

namespace FarmMVP
{
    /// <summary>선물을 얼마나 좋아하는지.</summary>
    public enum GiftTier { Loved, Liked, Neutral, Disliked, Hated }

    /// <summary>
    /// 초상화 번호. Resources/Sprites/NPC/&lt;id&gt;/Portrait_&lt;n&gt;.png 와 1:1로 대응한다.
    /// 새 NPC를 추가할 때도 같은 번호 규칙으로 8장을 넣으면 된다.
    /// </summary>
    public enum NpcEmotion
    {
        Neutral = 0, Smile = 1, Happy = 2, Think = 3,
        Angry = 4, Sad = 5, Surprised = 6, Shocked = 7
    }

    [Serializable]
    public class NpcHome
    {
        public string location = "Farm1"; // LocationId 이름
        public int x;
        public int y;
    }

    /// <summary>플레이어가 고를 수 있는 선택지 하나와 그에 대한 반응.</summary>
    [Serializable]
    public class DialogueChoice
    {
        public string text;        // 버튼에 표시될 플레이어 대사
        public string[] reply;     // 선택 후 NPC 반응
        public int emotion;        // 반응할 때의 표정
        public int affection;      // 호감도 변화(음수 가능)
    }

    /// <summary>일상 대화 한 덩어리. 호감도 구간이 맞는 것들 중에서 무작위로 하나가 뽑힌다.</summary>
    [Serializable]
    public class DialogueEntry
    {
        public string id;
        public int minHearts;       // 이 하트 수 이상일 때 등장 (0이면 호감도 무관)
        public int maxHearts = 10;  // 이 하트 수 이하일 때 등장
        public int emotion;
        public string[] lines;
        public DialogueChoice[] choices; // 없으면 그냥 대사만 하고 끝
    }

    /// <summary>선물 등급별 기본 대사.</summary>
    [Serializable]
    public class GiftLineSet
    {
        public string[] loved;
        public string[] liked;
        public string[] neutral;
        public string[] disliked;
        public string[] hated;

        public string[] For(GiftTier tier)
        {
            switch (tier)
            {
                case GiftTier.Loved: return loved;
                case GiftTier.Liked: return liked;
                case GiftTier.Disliked: return disliked;
                case GiftTier.Hated: return hated;
                default: return neutral;
            }
        }
    }

    /// <summary>특정 아이템에만 붙는 전용 선물 대사 (없으면 등급별 기본 대사를 쓴다).</summary>
    [Serializable]
    public class ItemGiftLine
    {
        public string itemId;
        public int emotion;
        public string[] lines;
    }

    /// <summary>
    /// NPC 한 명의 모든 콘텐츠. Resources/NPC/&lt;id&gt;.json 파일 하나가 이 클래스로 그대로 읽힌다.
    /// 새 NPC 추가 = json 파일 1개 + 스프라이트 폴더 1개. 코드는 건드릴 필요가 없다.
    /// </summary>
    [Serializable]
    public class NpcDefinition
    {
        public string id;
        public string displayName;
        public NpcHome home = new NpcHome();

        public string[] lovedItems;
        public string[] likedItems;
        public string[] dislikedItems;
        public string[] hatedItems;

        public GiftLineSet giftLines = new GiftLineSet();
        public ItemGiftLine[] itemGiftLines;

        public string[] alreadyTalkedLines; // 오늘 이미 대화한 경우
        public string[] giftLimitLines;     // 이번 주 선물 횟수를 다 쓴 경우
        public DialogueEntry[] dialogues;

        public LocationId HomeLocation =>
            Enum.TryParse(home.location, out LocationId id) ? id : LocationId.Farm1;

        public GiftTier TierOf(string itemId)
        {
            if (Contains(lovedItems, itemId)) return GiftTier.Loved;
            if (Contains(likedItems, itemId)) return GiftTier.Liked;
            if (Contains(dislikedItems, itemId)) return GiftTier.Disliked;
            if (Contains(hatedItems, itemId)) return GiftTier.Hated;
            return GiftTier.Neutral;
        }

        /// <summary>이 아이템 전용 선물 대사가 있으면 돌려준다.</summary>
        public ItemGiftLine FindItemGiftLine(string itemId)
        {
            if (itemGiftLines == null) return null;
            foreach (var l in itemGiftLines)
                if (l != null && l.itemId == itemId && l.lines != null && l.lines.Length > 0)
                    return l;
            return null;
        }

        private static bool Contains(string[] list, string value)
        {
            if (list == null) return false;
            foreach (var s in list)
                if (s == value) return true;
            return false;
        }
    }
}
