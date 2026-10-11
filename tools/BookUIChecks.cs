using System;
using System.IO;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BookUIChecks
{
    public static void Run()
    {
        if (!File.Exists("book-validation-project")) throw new Exception("Isolated project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CheckEditor();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var bootstrap = new GameObject("Disabled Bootstrap").AddComponent<GameBootstrap>();
        bootstrap.enabled = false;
        new GameObject("Book UI Checks").AddComponent<BookUIRuntimeChecks>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Checks.unity");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }

    private static void CheckEditor()
    {
        BookUILayoutTools.CreateMenuBook();
        var root = Selection.activeGameObject;
        BookUILayoutTools.BuildInteractiveMenu();
        var view = root.GetComponent<BookMenuView>();
        if (view == null || !view.configured || view.tabs.Length != 8) throw new Exception("Editor generator failed.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        if (root.GetComponent<BookMenuView>() != null) throw new Exception("Generator Undo did not restore skeleton.");
        Undo.PerformRedo();
        view = root.GetComponent<BookMenuView>();
        if (view == null || view.slots[29].button == null) throw new Exception("Generator Redo lost references.");
        Selection.activeGameObject = root;
        BookUILayoutTools.PreviewDisplay();
        if (!view.pages[7].activeSelf || !view.settingPanels[1].activeSelf) throw new Exception("Editor display preview failed.");
        BookUILayoutTools.PreviewExit();
        if (!view.settingPanels[4].activeSelf) throw new Exception("Editor exit preview failed.");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/BookLayout.unity");
        EditorSceneManager.OpenScene("Assets/BookLayout.unity");
        view = UnityEngine.Object.FindFirstObjectByType<BookMenuView>();
        if (view == null || view.slots[29].button == null || view.opening.Length != 4) throw new Exception("Scene reload lost book references.");
        File.WriteAllText("editor-checks.txt", "PASS: generator upgrade, Undo, Redo, display preview, exit preview, serialized scene reload.");
    }
}
