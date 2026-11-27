using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Animated rising fire embers / sparks overlay for the main menu background.
    /// </summary>
    public sealed class EmberField : MonoBehaviour
    {
        struct Ember
        {
            public RectTransform Rt;
            public Image Img;
            public float Speed;
            public float DriftAmp;
            public float DriftFreq;
            public float Phase;
            public float BaseAlpha;
        }

        Ember[] _embers;
        static Sprite _sparkSprite;

        static Sprite SparkSprite
        {
            get
            {
                if (_sparkSprite != null) return _sparkSprite;
                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s * 0.5f) / (s * 0.5f);
                        float dy = (y - s * 0.5f) / (s * 0.5f);
                        float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d * d));
                    }
                }
                tex.Apply();
                _sparkSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _sparkSprite;
            }
        }

        public static EmberField Create(Transform parent, int count = 32)
        {
            var rt = UiKit.NewRect("EmberField", parent);
            UiKit.Stretch(rt);
            var field = rt.gameObject.AddComponent<EmberField>();
            field.Init(rt, count);
            return field;
        }

        void Init(RectTransform root, int count)
        {
            _embers = new Ember[count];
            for (int i = 0; i < count; i++)
            {
                var go = UiKit.NewRect("Ember_" + i, root);
                var img = go.gameObject.AddComponent<Image>();
                img.sprite = SparkSprite;
                img.raycastTarget = false;

                float size = Random.Range(6f, 16f);
                go.sizeDelta = new Vector2(size, size * Random.Range(1.0f, 1.6f));
                go.anchorMin = go.anchorMax = new Vector2(Random.Range(0.05f, 0.95f), 0f);
                go.anchoredPosition = new Vector2(0f, Random.Range(-40f, 920f));

                Color c = Color.Lerp(new Color32(0xFF, 0x6B, 0x1A, 0xFF), new Color32(0xFF, 0xC1, 0x45, 0xFF), Random.value);
                float alpha = Random.Range(0.35f, 0.9f);
                c.a = alpha;
                img.color = c;

                _embers[i] = new Ember
                {
                    Rt = go,
                    Img = img,
                    Speed = Random.Range(45f, 135f),
                    DriftAmp = Random.Range(12f, 40f),
                    DriftFreq = Random.Range(0.8f, 2.2f),
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    BaseAlpha = alpha,
                };
            }
        }

        void Update()
        {
            if (_embers == null) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            for (int i = 0; i < _embers.Length; i++)
            {
                ref var e = ref _embers[i];
                var pos = e.Rt.anchoredPosition;
                pos.y += e.Speed * dt;
                pos.x = Mathf.Sin(t * e.DriftFreq + e.Phase) * e.DriftAmp;
                if (pos.y > 960f)
                {
                    pos.y = -30f;
                    e.Rt.anchorMin = e.Rt.anchorMax = new Vector2(Random.Range(0.05f, 0.95f), 0f);
                }
                e.Rt.anchoredPosition = pos;
                Color c = e.Img.color;
                float flicker = 0.65f + 0.35f * Mathf.Sin(t * 7f + e.Phase);
                float heightFade = Mathf.Clamp01(1f - pos.y / 960f);
                c.a = e.BaseAlpha * flicker * heightFade;
                e.Img.color = c;
            }
        }
    }
}
