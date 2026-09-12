// Check-StoryEvents.ps1 전용. Unity Editor 시작이 불가능할 때 순수 로직과 콘텐츠를 검사한다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using FarmMVP;
using Newtonsoft.Json;

public static class StoryEventOfflineChecks
{
    public static int Main(string[] args)
    {
        string data = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string folder in new[] { "Temp", data + "/Managed/UnityEngine", data + "/Managed", "Library/ScriptAssemblies" })
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        int checks = FarmMVP.Editor.StoryEventChecks.RunPure();
        var npcs = new HashSet<string>();
        foreach (string file in Directory.GetFiles("Assets/Resources/NPC", "*.json"))
            npcs.Add(JsonConvert.DeserializeObject<NpcDefinition>(File.ReadAllText(file)).id);
        var ids = new HashSet<string>();
        var strict = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        foreach (string file in Directory.GetFiles("Assets/Resources/Events", "*.json", SearchOption.AllDirectories))
        {
            var def = JsonConvert.DeserializeObject<StoryEventDefinition>(File.ReadAllText(file), strict);
            var errors = StoryEventRules.Validate(def, id => npcs.Contains(id));
            if (!ids.Add(def.id)) errors.Add("Duplicate event id");
            if (errors.Count > 0) throw new Exception(file + ": " + string.Join(" / ", errors));
        }
        Console.WriteLine("PASS: all project C# compiled; " + checks + " pure assertions; " + ids.Count + " event JSON definitions validated.");
        Console.WriteLine("Not tested here: Unity JsonUtility, coroutine/UI playback. Use the Unity regression menu and Play Mode checklist.");
    }
}
