using System;
using UnityEngine;

namespace FarmMVP
{
    [Serializable]
    public class CharacterProfile
    {
        public string playerName = "Ros";
        public string gender = "";
        public int birthdaySeason;
        public int birthdayDay = 1;

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(playerName)) return Localization.Get("ui.charactercreationdraft.please_enter_a_name");
            if (gender != "male" && gender != "female") return Localization.Get("ui.charactercreationdraft.please_select_a_gender");
            return null;
        }

        public void Apply(FarmerData farmer)
        {
            farmer.playerName = playerName.Trim();
            farmer.gender = gender;
            farmer.birthdaySeason = Mathf.Clamp(birthdaySeason, 0, 3);
            farmer.birthdayDay = Mathf.Clamp(birthdayDay, 1, 30);
        }
    }

    /// <summary>Confirmed state and tentative slot selection are deliberately separate.</summary>
    public sealed class CharacterCreationDraft
    {
        public CharacterProfile Profile { get; } = new CharacterProfile();
        public PlayerAppearance Appearance { get; } = new PlayerAppearance();
        public int ActivePart { get; private set; } = -1;
        public string Pending { get; private set; }
        public int Facing { get; private set; }
        public void Rotate(int delta) => Facing = Wrap(Facing + delta, 4);
        public void StepSeason(int delta) => Profile.birthdaySeason = Wrap(Profile.birthdaySeason + delta, 4);
        public void StepDay(int delta) => Profile.birthdayDay = Wrap(Profile.birthdayDay - 1 + delta, 30) + 1;
        public static int Wrap(int value, int count) => (value % count + count) % count;
        public void Open(int part) { ActivePart = part; Pending = GetItem(Appearance, part); }
        public void Select(string item) { if (ActivePart > 0) Pending = item ?? ""; }
        public void Commit() { if (ActivePart > 0) SetItem(Appearance, ActivePart, Pending); Discard(); }
        public void Discard() { ActivePart = -1; Pending = null; }
        public PlayerAppearance Preview()
        {
            var result = Appearance.Clone();
            if (ActivePart > 0) SetItem(result, ActivePart, Pending);
            return result;
        }
        public static string GetItem(PlayerAppearance a, int part)
        {
            switch (part) {
                case 1: return a.eyes; case 2: return a.hair; case 3: return a.top;
                case 4: return a.pants; case 5: return a.outfit; case 6: return a.shoes;
                case 7: return a.headGear; default: return "";
            }
        }
        public static void SetItem(PlayerAppearance a, int part, string value)
        {
            switch (part) {
                case 1: a.eyes = value; break; case 2: a.hair = value; break;
                case 3: a.top = value; break; case 4: a.pants = value; break;
                case 5: a.outfit = value; break; case 6: a.shoes = value; break;
                case 7: a.headGear = value; break;
            }
        }
        public static AppearanceColor ColorFor(PlayerAppearance a, int part)
        {
            switch (part) {
                case 0: return a.skin; case 1: return a.eyeColor; case 2: return a.hairColor;
                case 3: return a.topColor; case 4: return a.pantsColor; case 5: return a.outfitColor;
                case 6: return a.shoesColor; default: return a.accessoryColor;
            }
        }
    }
}
