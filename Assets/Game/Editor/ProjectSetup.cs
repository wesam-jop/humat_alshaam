using System.IO;
using Adventure3D.Bootstrap;
using Adventure3D.Configuration;
using Adventure3D.Gameplay;
using Adventure3D.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Adventure3D.EditorTools
{
    /// <summary>
    /// Creates / refreshes the project content that is not code: profile assets, the player prefab,
    /// the three scenes, build settings and player settings. Idempotent: existing profile values are
    /// kept, scenes and prefab are regenerated.
    /// Menu: Adventure3D/Setup Project. Batch: -executeMethod Adventure3D.EditorTools.ProjectSetup.RunBatch
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/Game";
        public const string ConfigDir = Root + "/Configuration/Resources/Adventure3D";
        public const string PrefabDir = Root + "/Gameplay/Resources/Adventure3D";
        public const string MaterialDir = Root + "/Gameplay/Arena/Materials";
        public const string SceneDir = Root + "/Scenes";
        public const string PlayerPrefabPath = PrefabDir + "/Player.prefab";

        public static readonly string[] ScenePaths =
        {
            SceneDir + "/" + SceneNames.Bootstrap + ".unity",
            SceneDir + "/" + SceneNames.MainMenu + ".unity",
            SceneDir + "/" + SceneNames.TestArena + ".unity",
        };

        [MenuItem("Adventure3D/Setup Project")]
        public static void Run()
        {
            EnsureFolder(ConfigDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(SceneDir);

            var config = CreateConfig();
            var prefab = CreatePlayerPrefab(config);
            var palette = CreatePalette();
            CreateBootstrapScene();
            CreateMenuScene();
            CreateArenaScene(prefab, palette);
            ConfigureBuildSettings();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[A3D] Project setup complete");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameConfig CreateConfig()
        {
            var cfg = LoadOrCreate<GameConfig>(ConfigDir + "/GameConfig.asset");
            cfg.Movement = LoadOrCreate<MovementProfile>(ConfigDir + "/MovementProfile.asset");
            cfg.Air = LoadOrCreate<AirMovementProfile>(ConfigDir + "/AirMovementProfile.asset");
            cfg.Physics = LoadOrCreate<PhysicsProfile>(ConfigDir + "/PhysicsProfile.asset");
            cfg.Camera = LoadOrCreate<CameraProfile>(ConfigDir + "/CameraProfile.asset");
            cfg.Input = LoadOrCreate<InputProfile>(ConfigDir + "/InputProfile.asset");
            cfg.Network = LoadOrCreate<NetworkProfile>(ConfigDir + "/NetworkProfile.asset");
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        static PlayerAvatar CreatePlayerPrefab(GameConfig config)
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = config.Physics.CapsuleHeight;
            cc.radius = config.Physics.CapsuleRadius;
            cc.center = Vector3.up * (config.Physics.CapsuleHeight * 0.5f);
            cc.skinWidth = config.Physics.SkinWidth;
            cc.minMoveDistance = 0f;
            cc.stepOffset = config.Movement.StepHeight;
            cc.slopeLimit = config.Movement.MaxWalkableSlope;
            var avatar = root.AddComponent<PlayerAvatar>();

            var view = new GameObject("View").transform;
            view.SetParent(root.transform, false);

            // Placeholder body: replace "Body" (and add an Animator) under View when the character is ready.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(view, false);
            body.transform.localPosition = Vector3.up * (config.Physics.CapsuleHeight * 0.5f);
            body.transform.localScale = new Vector3(config.Physics.CapsuleRadius * 2f, config.Physics.CapsuleHeight * 0.5f, config.Physics.CapsuleRadius * 2f);

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "FacingMarker";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.transform.SetParent(view, false);
            visor.transform.localPosition = new Vector3(0f, config.Physics.EyeHeight, config.Physics.CapsuleRadius);
            visor.transform.localScale = new Vector3(0.4f, 0.12f, 0.12f);

            var anchor = new GameObject("CameraAnchor").transform;
            anchor.SetParent(view, false);
            anchor.localPosition = Vector3.up * config.Physics.EyeHeight;

            var so = new SerializedObject(avatar);
            so.FindProperty("controller").objectReferenceValue = cc;
            so.FindProperty("viewRoot").objectReferenceValue = view;
            so.FindProperty("cameraAnchor").objectReferenceValue = anchor;
            var renderers = so.FindProperty("bodyRenderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = visor.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PlayerAvatar>();
        }

        static Material Mat(string name, Color color)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        static TestArenaLayout.Palette CreatePalette() => new TestArenaLayout.Palette
        {
            Floor = Mat("Floor", new Color(0.55f, 0.55f, 0.55f)),
            Marker = Mat("Marker", new Color(1f, 0.8f, 0.1f)),
            Stairs = Mat("Stairs", new Color(0.35f, 0.55f, 0.8f)),
            Walkable = Mat("RampWalkable", new Color(0.3f, 0.75f, 0.35f)),
            Steep = Mat("RampSteep", new Color(0.85f, 0.3f, 0.25f)),
            Ledge = Mat("Ledge", new Color(0.6f, 0.45f, 0.8f)),
            Ceiling = Mat("Ceiling", new Color(0.8f, 0.55f, 0.3f)),
        };

        static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static void AddUiCamera()
        {
            var cam = new GameObject("UiCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.cullingMask = 0;
            cam.gameObject.tag = "MainCamera";
        }

        static void CreateBootstrapScene()
        {
            var scene = NewScene();
            AddUiCamera();
            new GameObject("AppBootstrap").AddComponent<AppBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePaths[0]);
        }

        static void CreateMenuScene()
        {
            var scene = NewScene();
            AddUiCamera();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            new GameObject("MainMenu").AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(scene, ScenePaths[1]);
        }

        static void CreateArenaScene(PlayerAvatar prefab, TestArenaLayout.Palette palette)
        {
            var scene = NewScene();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            var arena = new GameObject("TestArena").transform;
            TestArenaLayout.Build(arena, palette);

            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(arena, false);
            for (int i = 0; i < TestArenaLayout.SpawnPositions.Length; i++)
            {
                var sp = new GameObject("Spawn_" + i).AddComponent<SpawnPoint>();
                sp.Index = i;
                sp.transform.SetParent(spawns, false);
                sp.transform.position = TestArenaLayout.SpawnPositions[i];
            }

            var entry = new GameObject("ArenaEntryPoint").AddComponent<ArenaEntryPoint>();
            var so = new SerializedObject(entry);
            so.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PlayerAvatar>(PlayerPrefabPath) ?? prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePaths[2]);
        }

        static void ConfigureBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[ScenePaths.Length];
            for (int i = 0; i < ScenePaths.Length; i++) scenes[i] = new EditorBuildSettingsScene(ScenePaths[i], true);
            EditorBuildSettings.scenes = scenes;
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "WesamGames";
            PlayerSettings.productName = "Adventure3D";
            PlayerSettings.bundleVersion = Adventure3D.Core.ProtocolInfo.GameVersion;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.usePlayerLog = true;
            // Keep player / server logs readable: no stack trace on plain Debug.Log lines.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);

            // Mobile: landscape only, safe area handled by SafeAreaFitter.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            const string id = "com.wesamgames.adventure3d";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, id);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, id);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.forceInternetPermission = true; // LAN / online sockets
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        }
    }
}
