using System;
using Adventure3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Screen 1: Splash Screen & Initial Loading Bar. Automatically transitions smoothly into
    /// the Main Menu once loading completes (no "press any key" prompt).
    /// </summary>
    public sealed class SplashOverlay : MonoBehaviour
    {
        Image _progressFill;
        CanvasGroup _group;
        float _progress;
        float _fadeOut;
        bool _done;
        Action _onComplete;

        public static bool ShouldShow => !Application.isBatchMode && MenuServices.Current != null;

        public static SplashOverlay Create(Transform parent, Action onComplete)
        {
            var root = UiKit.NewRect("SplashScreen", parent);
            UiKit.Stretch(root);
            var tex = Resources.Load<Texture2D>(MenuStyle.BackgroundResource);
            if (tex != null)
            {
                var raw = root.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
            }
            else
            {
                var bg = root.gameObject.AddComponent<Image>();
                bg.sprite = MenuStyle.TacticalBackdrop;
                bg.type = Image.Type.Sliced;
            }

            var splash = root.gameObject.AddComponent<SplashOverlay>();
            splash._group = root.gameObject.AddComponent<CanvasGroup>();
            splash._onComplete = onComplete;
            splash.Build(root);
            return splash;
        }

        void Build(RectTransform root)
        {
            var logoTex = Resources.Load<Texture2D>("Adventure3D/UI/GameLogo");
            var crestTex = Resources.Load<Texture2D>("Adventure3D/UI/EagleCrest");
            if (crestTex != null)
            {
                var crRt = UiKit.NewRect("Crest", root);
                UiKit.Anchor(crRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(490f, -44f));
                crRt.sizeDelta = new Vector2(200f, 142f);
                var cr = crRt.gameObject.AddComponent<RawImage>();
                cr.texture = crestTex;
                cr.raycastTarget = false;
            }
            if (logoTex != null)
            {
                var lgRt = UiKit.NewRect("Logo", root);
                UiKit.Anchor(lgRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -64f));
                lgRt.sizeDelta = new Vector2(420f, 122f);
                var lg = lgRt.gameObject.AddComponent<RawImage>();
                lg.texture = logoTex;
                lg.raycastTarget = false;
            }

            var bottom = UiKit.NewRect("BottomBar", root);
            UiKit.Anchor(bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f));
            bottom.sizeDelta = new Vector2(1280f, 130f);
            UiKit.Column(bottom, 10, 8);

            var status = UiKit.Label(bottom, Strings.Get("splash.loading"), 26, TextAnchor.MiddleCenter, 36);
            status.Target.color = MenuStyle.TextWhite;

            var barBg = MenuStyle.Shape(bottom, "LoadingBar", MenuStyle.InputField);
            UiKit.Fit(barBg.rectTransform, 22f);
            _progressFill = MenuStyle.Shape(barBg.transform, "Fill", MenuStyle.White, MenuStyle.Gold);
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _progressFill.rectTransform.offsetMin = new Vector2(4f, 4f);
            _progressFill.rectTransform.offsetMax = new Vector2(-4f, -4f);

            var ver = UiKit.LtrLabel(bottom, "v" + ProtocolInfo.GameVersion, 18, TextAnchor.MiddleCenter);
            ver.color = MenuStyle.TextMuted;
        }

        void Update()
        {
            if (!_done)
            {
                _progress = Mathf.Min(1f, _progress + Time.unscaledDeltaTime * 1.1f);
                _progressFill.rectTransform.anchorMax = new Vector2(_progress, 1f);
                if (_progress >= 1f) _done = true;
                return;
            }

            _fadeOut += Time.unscaledDeltaTime * 3.5f;
            if (_group != null) _group.alpha = Mathf.Clamp01(1f - _fadeOut);
            if (_fadeOut >= 1f)
            {
                gameObject.SetActive(false);
                _onComplete?.Invoke();
            }
        }
    }
}
