using System;

namespace FarmMVP
{
    /// <summary>선물을 얼마나 좋아하는지.</summary>
    public enum GiftTier { Loved, Liked, Neutral, Disliked, Hated }

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
        public string portrait;    // 반응할 때 띄울 초상화 (Portraits 폴더의 파일 이름)
        public int affection;      // 호감도 변화(음수 가능)
    }

    /// <summary>일상 대화 한 덩어리. 호감도 구간이 맞는 것들 중에서 무작위로 하나가 뽑힌다.</summary>
    [Serializable]
    public class DialogueEntry
    {
        public string id;
        public int minHearts;       // 이 하트 수 이상일 때 등장 (0이면 호감도 무관)
        public int maxHearts = 10;  // 이 하트 수 이하일 때 등장
        public string portrait;     // 띄울 초상화. 비우면 defaultPortrait
        public string[] lines;
        public DialogueChoice[] choices; // 없으면 그냥 대사만 하고 끝
    }

    /// <summary>선물 등급별 기본 대사와 그때 띄울 초상화.</summary>
    [Serializable]
    public class GiftLineSet
    {
        public string[] loved;
        public string[] liked;
        public string[] neutral;
        public string[] disliked;
        public string[] hated;

        public string lovedPortrait;
        public string likedPortrait;
        public string neutralPortrait;
        public string dislikedPortrait;
        public string hatedPortrait;

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

        public string PortraitFor(GiftTier tier)
        {
            switch (tier)
            {
                case GiftTier.Loved: return lovedPortrait;
                case GiftTier.Liked: return likedPortrait;
                case GiftTier.Disliked: return dislikedPortrait;
                case GiftTier.Hated: return hatedPortrait;
                default: return neutralPortrait;
            }
        }
    }

    /// <summary>특정 아이템에만 붙는 전용 선물 대사 (없으면 등급별 기본 대사를 쓴다).</summary>
    [Serializable]
    public class ItemGiftLine
    {
        public string itemId;
        public string portrait;
        public string[] lines;
    }

    /// <summary>
    /// NPC 한 명의 모든 콘텐츠. Resources/NPC/&lt;id&gt;.json 파일 하나가 이 클래스로 그대로 읽힌다.
    /// 새 NPC 추가 = json 파일 1개 + 스프라이트 폴더 1개. 코드는 건드릴 필요가 없다.
    ///
    /// 스프라이트 폴더는 이렇게 생겼다 (조각 자르기는 tools/build_npc_metas.py 가 만든다):
    ///   Resources/Sprites/NPC/&lt;id&gt;/&lt;id&gt;_sprites.png   32x32 걷기 시트
    ///   Resources/Sprites/NPC/&lt;id&gt;/Portraits/*.png       대화창 초상화 (파일 이름이 곧 이름)
    /// </summary>
    [Serializable]
    public class NpcDefinition
    {
        public string id;
        public string displayName;

        /// <summary>스케줄이 없거나 오늘 갈 곳이 없을 때 서 있는 자리.</summary>
        public NpcHome home = new NpcHome();

        /// <summary>초상화를 따로 지정하지 않은 대사에 쓸 기본 초상화. 비우면 폴더의 첫 장.</summary>
        public string defaultPortrait;

        public string[] lovedItems;
        public string[] likedItems;
        public string[] dislikedItems;
        public string[] hatedItems;

        public GiftLineSet giftLines = new GiftLineSet();
        public ItemGiftLine[] itemGiftLines;

        public string[] alreadyTalkedLines; // 오늘 이미 대화한 경우
        public string alreadyTalkedPortrait;
        public string[] giftLimitLines;     // 이번 주 선물 횟수를 다 쓴 경우
        public string giftLimitPortrait;
        public DialogueEntry[] dialogues;

        /// <summary>요일·계절·날씨에 따라 하루를 어떻게 보내는지. 없으면 home에 가만히 서 있는다.</summary>
        public NpcSchedule schedule = new NpcSchedule();

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
