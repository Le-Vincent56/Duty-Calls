#nullable enable
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Editor
{
    /// <summary>
    /// Enforces a dedicated boot scene as the Play Mode start scene in the Unity Editor.
    /// This is required to guarantee "inject before Awake": pressing Play in an arbitrary
    /// open scene can run that scene's Awake before the boot sequence has a chance to
    /// load/activate the correct SceneGroup(s) deterministically.
    /// </summary>
    /// <remarks>
    /// Policy:
    /// - Build Settings scene at index 0 is treated as the Boot scene.
    /// - This script forces Unity to always enter Play Mode from that scene.
    /// </remarks>
    [InitializeOnLoad]
    public static class PlayModeStartSceneEnforcer
    {
        private const string LogPrefix = "[Bootstrapping]";

        static PlayModeStartSceneEnforcer()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Duty Calls/Bootstrapping/Apply Play Mode Start Scene")]
        private static void ApplyMenu() => Apply();

        private static void Apply()
        {
            // Exit case - we are already in Play Mode
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            
            // Exit case - no scenes in Build Settings
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError(LogPrefix + " No scenes in Build Settings. Add the Boot scene as Build index 0.");
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            EditorBuildSettingsScene boot = scenes[0];
            
            // Exit case - the boot scene is not enabled
            if (!boot.enabled)
            {
                Debug.LogError(LogPrefix + " Build Settings scene 0 is disabled. Enable the Boot scene at index 0.");
                return;
            }

            string path = boot.path;
            
            // Exit case - the path is empty
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError(LogPrefix + " Build Settings scene 0 has an empty path.");
                return;
            }

            SceneAsset? sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            
            // Exit case no scene asset
            if (!sceneAsset)
            {
                Debug.LogError(LogPrefix + " Could not load SceneAsset at path: " + path);
                return;
            }

            // Exit case - the play mode scene is already the scene asset
            if (EditorSceneManager.playModeStartScene == sceneAsset) return;

            EditorSceneManager.playModeStartScene = sceneAsset;
            Debug.Log(LogPrefix + " Set Play Mode Start Scene to: " + path);
        }
    }
}
#endif