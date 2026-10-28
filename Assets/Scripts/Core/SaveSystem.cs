using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Five independent characters, one current snapshot per character.</summary>
    public static class SaveSystem
    {
        public const int MaxSaves = 5;
        private const string LegacyId = "legacy";
        // Editor regression checks use a workspace directory, never the player's saves.
#if UNITY_EDITOR
        internal static string TestSaveRoot;
        private static string Root => TestSaveRoot ?? Application.persistentDataPath;
#else
        private static string Root => Application.persistentDataPath;
#endif
        private static string Folder => Path.Combine(Root, "Saves");
        private static string LegacyPath => Path.Combine(Root, "farm_save.json");
        private static bool ValidId(string id) => id == LegacyId || Guid.TryParseExact(id, "N", out _);
        private static string SavePath(string id) => id == LegacyId ? LegacyPath : Path.Combine(Folder, id + ".json");
        private static IEnumerable<string> Paths()
        {
            if (File.Exists(LegacyPath)) yield return LegacyPath;
            if (!Directory.Exists(Folder)) yield break;
            foreach (var path in Directory.GetFiles(Folder, "*.json"))
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _)) yield return path;
        }
        public static bool CanCreate
        {
            get
            {
                try { return Paths().Count() < MaxSaves; }
                catch (Exception e) { Debug.LogWarning("[SaveSystem] " + e.Message); return false; }
            }
        }
        public static bool HasSave => GetSaves().Count > 0;
        public static List<GameData> GetSaves()
        {
            var saves = new List<GameData>();
            try
            {
                foreach (var path in Paths())
                {
                    var data = Read(path);
                    if (data != null) saves.Add(data);
                }
            }
            catch (Exception e) { Debug.LogWarning("[SaveSystem] Cannot list saves: " + e.Message); }
            return saves.OrderByDescending(d => d.lastSavedUtcTicks).ThenBy(d => d.saveId, StringComparer.Ordinal).ToList();
        }
        private static GameData Read(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                // JsonUtility accepts {} as a new game; that is not an existing save.
                if (!json.Contains("\"farmer\"") || !json.Contains("\"currentDay\"")) return null;
                var data = JsonUtility.FromJson<GameData>(json);
                if (data?.farmer == null || data.currentDay < 1) return null;
                data.saveId = path == LegacyPath ? LegacyId : Path.GetFileNameWithoutExtension(path);
                if (data.lastSavedUtcTicks <= 0) data.lastSavedUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
                data.locations = data.locations ?? new List<LocationEntry>();
                data.MigrateLocations();
                ShopSellService.PruneExpired(data);
                return data;
            }
            catch (Exception e) { Debug.LogWarning("[SaveSystem] Cannot read " + Path.GetFileName(path) + ": " + e.Message); return null; }
        }
        public static GameData Load() => GetSaves().FirstOrDefault();
        public static GameData Load(string id)
        {
            if (!ValidId(id)) return null;
            string path = SavePath(id);
            return File.Exists(path) ? Read(path) : null;
        }
        public static void Save(GameData data) => TrySave(data);
        public static bool TrySave(GameData data)
        {
            if (data?.farmer == null) return false;
            string oldId = data.saveId;
            long oldTime = data.lastSavedUtcTicks;
            try
            {
                string id = string.IsNullOrEmpty(oldId) ? Guid.NewGuid().ToString("N") : oldId;
                if (!ValidId(id)) return false;
                string path = SavePath(id);
                if (!File.Exists(path) && !CanCreate) return false;
                Directory.CreateDirectory(Folder);
                data.saveId = id;
                data.lastSavedUtcTicks = Math.Max(DateTime.UtcNow.Ticks, oldTime + 1);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                // Keep the last good snapshot intact until the replacement is fully written.
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e)
            {
                data.saveId = oldId;
                data.lastSavedUtcTicks = oldTime;
                Debug.LogError("[SaveSystem] Save failed: " + e.Message);
                return false;
            }
        }
        public static bool DeleteSave(string id)
        {
            if (!ValidId(id)) return false;
            try
            {
                string path = SavePath(id);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e) { Debug.LogError("[SaveSystem] Delete failed: " + e.Message); return false; }
        }
        // Compatibility for callers that previously operated on the single save.
        public static void DeleteSave()
        {
            var latest = Load();
            if (latest != null) DeleteSave(latest.saveId);
        }
    }
}
