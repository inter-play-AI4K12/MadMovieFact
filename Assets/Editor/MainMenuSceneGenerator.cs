using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainMenuSceneGenerator
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("MadMovieFact/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.GetComponent<Camera>().backgroundColor = new Color(0.025f, 0.035f, 0.075f);

        new GameObject("Main Menu", typeof(MainMenuController));
        EditorSceneManager.SaveScene(scene, ScenePath);

        var existing = EditorBuildSettings.scenes;
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        foreach (var entry in existing)
        {
            if (entry.path != ScenePath)
                scenes.Add(entry);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Created MainMenu scene and placed it first in Build Settings.");
    }
}
