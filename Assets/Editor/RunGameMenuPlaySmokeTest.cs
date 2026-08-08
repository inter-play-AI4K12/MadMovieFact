using System;
using System.Collections.Generic;
using MadFact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-friendly Play Mode smoke check for the authored production menu.</summary>
public static class RunGameMenuPlaySmokeTest
{
    static readonly List<string> Errors = new List<string>();
    static int _frames;
    static bool _previousOptionsEnabled;
    static EnterPlayModeOptions _previousOptions;

    public static void Run()
    {
        EditorSceneManager.OpenScene(LevelSceneCatalog.GameMenu, OpenSceneMode.Single);
        _previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += CaptureLog;
        EditorApplication.update += Update;
        EditorApplication.EnterPlaymode();
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        if (++_frames < 30) return;

        Require("GameMenuCanvas");
        Require("ConsentPanel");
        Require("LevelSelectPanel");
        Require("MainMenuTitle");
        for (int level = 1; level <= LevelSceneCatalog.MaxPlayableLevel; level++)
            Require("Level" + level);

        Finish(Errors.Count == 0 ? 0 : 1);
    }

    static void Require(string objectName)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            if (candidate.name == objectName && candidate.scene.IsValid()) return;
        Errors.Add("Missing GameObject: " + objectName);
    }

    static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        bool isError = type == LogType.Exception || type == LogType.Error || type == LogType.Assert;
        // Batch mode can emit Editor/MCP/Search infrastructure errors without a graphics device.
        // Fail this game smoke test only for errors whose stack enters production game scripts.
        if (isError && stackTrace.Contains("Assets/Scripts/"))
            Errors.Add(condition + "\n" + stackTrace);
    }

    static void Finish(int exitCode)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= CaptureLog;
        EditorSettings.enterPlayModeOptionsEnabled = _previousOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _previousOptions;
        if (exitCode == 0) Debug.Log("ROC AI 26 GameMenu Play Mode smoke test passed.");
        else Debug.LogError("ROC AI 26 GameMenu Play Mode smoke test failed:\n" + string.Join("\n", Errors));
        EditorApplication.Exit(exitCode);
    }
}
