using System;
using System.Collections.Generic;

namespace FarmMVP
{
    // 문자열 명령을 사용하므로 JSON에는 enum 숫자 대신 읽기 쉬운 이름을 쓴다.
    [Serializable]
    public class StoryEventDefinition
    {
        public string id;
        public bool disabled;
        public string trigger = "EnterLocation"; // NewGame, EnterLocation, DayStarted, InteractNpc, Manual
        public string triggerId; // InteractNpc의 NPC id / Manual의 신호 이름
        public int priority;
        public string repeat = "Once"; // Once, Daily, Always (발생 신호 한 번당 최대 한 번)
        public StoryCondition[] conditions;
        public StoryCommand[] commands;
    }

    [Serializable]
    public class StoryCondition
    {
        public string type; // Money, Hearts, Day, Time, Location, Flag, Completed
        public string id;
        public int min;
        public int max = int.MaxValue;
        public bool not;
    }

    [Serializable]
    public class StoryCommand
    {
        public string op; // Say, Choice, Move, Face, Animate, Wait, Flag, Money, Affection, If, Goto, End
        public string label;
        public string actor; // player 또는 NPC id; Say/Choice에서 생략하면 내레이션
        public string speaker;
        public int emotion;
        public string[] lines;
        public StoryChoice[] choices;
        public StoryCondition[] conditions;
        public string target; // 분기 목적지 label
        public string otherwise;
        public string id; // Flag 이름 / Affection의 NPC id
        public bool value = true;
        public int amount;
        public StoryPoint[] path;
        public float speed = 2.6f;
        public float seconds;
        public string direction = "Down";
        public string animation = "Idle";
    }

    [Serializable]
    public class StoryChoice
    {
        public string text;
        public string target;
    }

    [Serializable]
    public class StoryPoint { public int x, y; }

    [Serializable]
    public class StoryEventState
    {
        public int version = 1;
        public bool introPending;
        public List<string> flags = new List<string>();
        public List<StoryCompletion> completed = new List<StoryCompletion>();

        public void Normalize()
        {
            if (flags == null) flags = new List<string>();
            if (completed == null) completed = new List<StoryCompletion>();
        }

        public int LastDay(string id)
        {
            foreach (var entry in completed)
                if (entry != null && entry.id == id) return entry.day;
            return -1;
        }

        public void Complete(string id, int day)
        {
            foreach (var entry in completed)
                if (entry != null && entry.id == id) { entry.day = day; return; }
            completed.Add(new StoryCompletion { id = id, day = day });
        }
    }

    [Serializable]
    public class StoryCompletion { public string id; public int day; }
}
