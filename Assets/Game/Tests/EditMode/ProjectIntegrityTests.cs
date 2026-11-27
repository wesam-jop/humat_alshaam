using System.IO;
using System.Linq;
using Adventure3D.Bootstrap;
using Adventure3D.Gameplay;
using Adventure3D.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Adventure3D.Tests
{
    /// <summary>Structural checks: scenes, references, and the assembly boundaries that keep the server headless.</summary>
    public class ProjectIntegrityTests
    {
        static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Bootstrap.unity",
            "Assets/Game/Scenes/MainMenu.unity",
            "Assets/Game/Scenes/TestArena.unity",
        };

        [Test]
        public void BuildSettingsStartWithBootstrapThenMenuThenArena()
        {
            var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(Scenes, paths);
        }

        [Test]
        public void ScenesHaveNoMissingScriptsAndEntryPointsAreWired()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), $"Missing script on {t.name} in {path}");
            }

            EditorSceneManager.OpenScene(Scenes[0]);
            Assert.IsNotNull(Object.FindFirstObjectByType<AppBootstrap>());
            EditorSceneManager.OpenScene(Scenes[1]);
            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenuController>());
            EditorSceneManager.OpenScene(Scenes[2]);
            var entry = Object.FindFirstObjectByType<ArenaEntryPoint>();
            Assert.IsNotNull(entry);
            var so = new SerializedObject(entry);
            Assert.IsNotNull(so.FindProperty("playerPrefab").objectReferenceValue, "ArenaEntryPoint.playerPrefab not assigned");
            Assert.GreaterOrEqual(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).Length, 2);
            Assert.IsNull(Object.FindFirstObjectByType<Camera>(), "Arena must not contain a camera: the server stays camera-less");
            Assert.IsNull(Object.FindFirstObjectByType<Canvas>(), "Arena must not contain a Canvas: HUD is created only for local players");
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        }

        [Test]
        public void PlayerPrefabHasSimulationAndViewRoots()
        {
            var prefab = Resources.Load<PlayerAvatar>(WorldSimulation.PlayerPrefabResource);
            Assert.IsNotNull(prefab);
            var so = new SerializedObject(prefab);
            Assert.IsNotNull(so.FindProperty("controller").objectReferenceValue);
            var view = (Transform)so.FindProperty("viewRoot").objectReferenceValue;
            var anchor = (Transform)so.FindProperty("cameraAnchor").objectReferenceValue;
            Assert.IsNotNull(view);
            Assert.IsNotNull(anchor);
            Assert.AreNotEqual(prefab.transform, view, "view root must be separate from the simulation root");
            Assert.IsTrue(anchor.IsChildOf(view));
            Assert.IsNull(view.GetComponentInChildren<Collider>(), "view root must not contain colliders");
        }

        [TestCase("Core")]
        [TestCase("Configuration")]
        [TestCase("Gameplay")]
        [TestCase("Identity")]
        [TestCase("Networking")]
        public void ServerSideAssembliesDoNotDependOnUiOrInput(string folder)
        {
            string asmdef = Directory.GetFiles(Path.Combine(Application.dataPath, "Game", folder), "*.asmdef").Single();
            string json = File.ReadAllText(asmdef);
            foreach (var forbidden in new[] { "UnityEngine.UI", "Unity.InputSystem", "Adventure3D.Presentation", "Adventure3D.Input", "Adventure3D.Bootstrap" })
                StringAssert.DoesNotContain("\"" + forbidden + "\"", json, $"{folder} must not reference {forbidden}");

            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "Game", folder), "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(file);
                StringAssert.DoesNotContain("using UnityEngine.UI", code, file);
                StringAssert.DoesNotContain("UnityEngine.InputSystem", code, file);
                StringAssert.DoesNotContain("Canvas", code, file);
            }
        }
    }
}
