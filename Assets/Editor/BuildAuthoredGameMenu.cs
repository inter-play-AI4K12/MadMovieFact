using MadFact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes the complete runtime Game Menu into GameMenu.unity. The saved hierarchy is
/// the production UI: Scene-view edits are preserved and used when the game runs.
/// </summary>
public static class BuildAuthoredGameMenu
{
    const string ScenePath = "Assets/Scenes/GameMenu.unity";

    [MenuItem("MadFact/Rebuild Authored Game Menu")]
    public static void Rebuild()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>();
        if (controller == null)
            controller = new GameObject("MainMenuController").AddComponent<MainMenuController>();

        EnsureCameraAndLight();
        controller.BuildAuthoredHierarchy();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        GameObject settings = GameObject.Find("SettingsPanel");
        Selection.activeGameObject = settings != null ? settings : controller.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        SceneView.RepaintAll();
        Debug.Log("Rebuilt GameMenu with editable Main, Settings, Consent, confirmation, and sequential level panels.");
    }

    static void EnsureCameraAndLight()
    {
        if (Object.FindAnyObjectByType<Camera>() == null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().orthographic = true;
        }

        if (Object.FindAnyObjectByType<Light>() == null)
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            lightObject.GetComponent<Light>().type = LightType.Directional;
        }
    }
}
