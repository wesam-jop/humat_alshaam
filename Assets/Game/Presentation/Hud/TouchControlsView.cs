using Adventure3D.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Draws the temporary touch layout from <see cref="TouchControlsModel"/> rectangles: move stick on
    /// the left, look area on the right, Jump / Sprint / Menu buttons. Purely visual: the model in the
    /// Input assembly reads the fingers, so the same logic is testable without UI.
    /// </summary>
    public sealed class TouchControlsView : MonoBehaviour
    {
        LocalPlayerInput _input;
        RectTransform _root;
        RectTransform _stickBase, _stickKnob;
        Image _jump, _sprint, _fire;

        public static TouchControlsView Create(RectTransform safeRoot, LocalPlayerInput input)
        {
            var rt = UiKit.NewRect("TouchControls", safeRoot);
            UiKit.Stretch(rt);
            var view = rt.gameObject.AddComponent<TouchControlsView>();
            view._input = input;
            view._root = rt;
            view.Build();
            return view;
        }

        void Build()
        {
            _jump = Zone("Jump", TouchControlsModel.JumpButton, "touch.jump");
            _sprint = Zone("Sprint", TouchControlsModel.SprintButton, "touch.sprint");
            Zone("Pause", TouchControlsModel.PauseButton, "touch.pause");
            _fire = Zone("Fire", TouchControlsModel.FireButton, "touch.fire");
            Zone("Reload", TouchControlsModel.ReloadButton, "touch.reload");
            Zone("Switch", TouchControlsModel.SwitchButton, "touch.switch");
            Zone("Crouch", TouchControlsModel.CrouchButton, "touch.crouch");

            _stickBase = UiKit.NewRect("StickBase", _root);
            var b = _stickBase.gameObject.AddComponent<Image>();
            b.color = new Color(MenuStyle.BgPanel.r, MenuStyle.BgPanel.g, MenuStyle.BgPanel.b, 0.55f);
            b.raycastTarget = false;
            _stickKnob = UiKit.NewRect("StickKnob", _stickBase);
            var k = _stickKnob.gameObject.AddComponent<Image>();
            k.color = new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.85f);
            k.raycastTarget = false;
            _stickBase.gameObject.SetActive(false);
        }

        Image Zone(string name, Rect normalized, string labelKey)
        {
            var rt = UiKit.NewRect(name, _root);
            rt.anchorMin = normalized.min;
            rt.anchorMax = normalized.max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = MenuStyle.ButtonNormal;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.72f);
            img.raycastTarget = false; // fingers are read by the Input model, not by uGUI
            var label = UiKit.Label(rt, Strings.Get(labelKey), 28, TextAnchor.MiddleCenter);
            label.Target.fontStyle = FontStyle.Bold;
            label.Target.color = MenuStyle.TextWhite;
            UiKit.Stretch((RectTransform)label.transform);
            return img;
        }

        void LateUpdate()
        {
            if (_input == null) return;
            var model = _input.Touch;
            bool jumpHeld = model.JumpHeld;
            bool sprintHeld = model.SprintHeld || _input.SprintActive;
            _jump.sprite = jumpHeld ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            _jump.color = new Color(1f, 1f, 1f, jumpHeld ? 0.95f : 0.72f);
            _sprint.sprite = sprintHeld ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            _sprint.color = new Color(1f, 1f, 1f, sprintHeld ? 0.95f : 0.72f);
            _fire.sprite = model.FireHeld ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            _fire.color = new Color(1f, 1f, 1f, model.FireHeld ? 0.95f : 0.72f);

            _stickBase.gameObject.SetActive(model.StickActive);
            if (model.StickActive)
            {
                var size = _root.rect.size;
                float radius = model.StickRadius * size.y;
                _stickBase.anchorMin = _stickBase.anchorMax = Vector2.zero;
                _stickBase.sizeDelta = Vector2.one * radius * 2f;
                _stickBase.anchoredPosition = Vector2.Scale(model.StickOriginNormalized, size);
                _stickKnob.sizeDelta = Vector2.one * radius * 0.8f;
                _stickKnob.anchoredPosition = Vector2.Scale(model.StickKnobNormalized - model.StickOriginNormalized, size);
            }
        }
    }
}
