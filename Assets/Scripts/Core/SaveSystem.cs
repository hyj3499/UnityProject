using System.IO;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>JSON save/load of the whole GameData (design doc §6/§9/STEP 9).</summary>
    public static class SaveSystem
    {
        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "farm_save.json");

        public static void Save(GameData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path, json);
                Debug.Log($"[SaveSystem] Saved to {Path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            }
        }

        public static bool HasSave => File.Exists(Path);

        public static GameData Load()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<GameData>(json);
                Debug.Log("[SaveSystem] Loaded save.");
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        public static void DeleteSave()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }
}
