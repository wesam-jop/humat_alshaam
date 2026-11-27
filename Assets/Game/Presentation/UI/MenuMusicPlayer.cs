using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Plays tactical preparation menu music. Uses Resources/Adventure3D/Audio/MenuTheme if present,
    /// otherwise synthesizes a warm, atmospheric tactical ambient loop cleanly in code.
    /// </summary>
    public sealed class MenuMusicPlayer : MonoBehaviour
    {
        AudioSource _source;

        public static MenuMusicPlayer Attach(Transform parent)
        {
            var go = new GameObject("MenuMusicPlayer");
            go.transform.SetParent(parent, false);
            return go.AddComponent<MenuMusicPlayer>();
        }

        void Start()
        {
            if (Application.isBatchMode) return;
            // Ensure an AudioListener exists for menu audio
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;

            var clip = Resources.Load<AudioClip>("Adventure3D/Audio/MenuTheme");
            if (clip == null) clip = SynthesizeTacticalTheme();
            _source.clip = clip;
            ApplyVolume();
            _source.Play();
        }

        void Update()
        {
            if (_source != null) ApplyVolume();
        }

        void ApplyVolume()
        {
            var s = TacticalSettings.Current;
            _source.volume = Mathf.Clamp01(s.MasterVolume * s.MusicVolume * 0.45f);
        }

        static AudioClip SynthesizeTacticalTheme()
        {
            const int sampleRate = 22050;
            const float duration = 8f; // seamless 8-second loop
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // D minor tactical drone + warm fifth + subtle heartbeat pulse
            float[] freqs = { 73.42f, 110.00f, 146.83f, 220.00f }; // D2, A2, D3, A3
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float pad = 0f;
                for (int f = 0; f < freqs.Length; f++)
                    pad += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * (0.28f / (f + 1));

                // Subtle 1-beat-per-second tactical pulse
                float beat = t % 1f;
                float pulse = Mathf.Exp(-beat * 9f) * Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.25f;

                // Gentle slow swell over the 8-second loop
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (t / duration) - Mathf.PI * 0.5f);
                data[i] = Mathf.Clamp((pad * 0.45f + pulse) * swell, -0.8f, 0.8f);
            }

            var clip = AudioClip.Create("TacticalMenuTheme", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
