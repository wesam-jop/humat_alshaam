using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Adventure3D.EditorTools
{
    /// <summary>
    /// Makes Bootstrap the natural entry point inside the editor too:
    ///  - pressing Play starts from Bootstrap.unity (then the Arabic menu), whatever scene is open;
    ///  - when the project opens on an empty "Untitled" scene, Bootstrap.unity is opened instead.
    /// Play Mode test runs are left alone (they use their own "InitTestScene").
    /// Toggle: Adventure3D/Play From Bootstrap.
    /// </summary>
    [InitializeOnLoad]
    public static class BootstrapPlayMode
    {
        const string PrefKey = "Adventure3D.PlayFromBootstrap";
        const string MenuPath = "Adventure3D/Play From Bootstrap";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static BootstrapPlayMode()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += OpenBootstrapIfEmpty;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            var active = SceneManager.GetActiveScene();
            bool testRun = active.name.StartsWith("InitTestScene") || UnityEngine.Application.isBatchMode;
            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePaths[0]);
            EditorSceneManager.playModeStartScene = Enabled && !testRun ? bootstrap : null;
        }

        static void OpenBootstrapIfEmpty()
        {
            if (UnityEngine.Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty && AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePaths[0]) != null)
                EditorSceneManager.OpenScene(ProjectSetup.ScenePaths[0]);
        }

        [MenuItem(MenuPath)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
