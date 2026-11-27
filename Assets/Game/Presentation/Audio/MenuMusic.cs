using System.Collections;
using Adventure3D.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Menu music: starts with the game (Bootstrap / main menu), keeps playing across menu screens,
    /// and fades out when the player enters the arena. It comes back when the player returns to the
    /// menu. Clip: Resources/Adventure3D/Audio/MenuMusic. Never runs on the headless server.
    /// </summary>
    public sealed class MenuMusic : MonoBehaviour
    {
        public const string ClipResource = "Adventure3D/Audio/MenuMusic";
        const string ArenaScene = "TestArena";
        const float Volume = 0.6f;
        const float FadeInTime = 1.5f;
        const float FadeOutTime = 0.8f;

        static MenuMusic _instance;
        AudioSource _source;
        Coroutine _fade;

        public static MenuMusic Instance => _instance;
        public bool IsPlaying => _source != null && _source.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (Application.isBatchMode || PlatformInfo.IsHeadlessServer) return; // servers and tests stay silent

            bool inArena = scene.name == ArenaScene;
            if (inArena)
            {
                if (_instance != null) _instance.FadeTo(0f, FadeOutTime, stopAtEnd: true);
                return;
            }
            Ensure()?.FadeTo(Volume, FadeInTime, stopAtEnd: false);
        }

        static MenuMusic Ensure()
        {
            if (_instance != null) return _instance;
            var clip = Resources.Load<AudioClip>(ClipResource);
            if (clip == null) return null;
            var go = new GameObject("MenuMusic");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MenuMusic>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.clip = clip;
            _instance._source.loop = true;
            _instance._source.playOnAwake = false;
            _instance._source.spatialBlend = 0f;
            _instance._source.volume = 0f;
            return _instance;
        }

        void FadeTo(float target, float time, bool stopAtEnd)
        {
            if (_fade != null) StopCoroutine(_fade);
            if (target > 0f && !_source.isPlaying) _source.Play();
            Adventure3D.Core.GameLog.Event("menu_music", "\"fadeTo\":" + Adventure3D.Core.GameLog.F(target) + ",\"scene\":" + Adventure3D.Core.GameLog.Q(SceneManager.GetActiveScene().name));
            _fade = StartCoroutine(Fade(target, time, stopAtEnd));
        }

        IEnumerator Fade(float target, float time, bool stopAtEnd)
        {
            float start = _source.volume;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                _source.volume = Mathf.Lerp(start, target, t / time);
                yield return null;
            }
            _source.volume = target;
            if (stopAtEnd) _source.Stop(); // next time the menu opens the music starts from the beginning
            _fade = null;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
