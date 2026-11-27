using System;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Modal Arabic message box with one OK button (errors, disconnects).</summary>
    public sealed class MessageDialog : MonoBehaviour
    {
        ArabicText _title;
        ArabicText _body;
        Button _ok;
        Action _onClose;
        Selectable _returnFocus;

        public bool IsOpen => gameObject.activeSelf;
        public string BodyLogical => _body != null ? _body.Logical : "";

        public static MessageDialog Create(Transform parent)
        {
            var blocker = UiKit.NewRect("MessageDialog", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var dlg = blocker.gameObject.AddComponent<MessageDialog>();

            var panel = UiKit.Panel("Panel", blocker, new Vector2(960, 440));
            UiKit.Column(panel, 24, 40);
            dlg._title = UiKit.Label(panel, "", 42, TextAnchor.MiddleCenter);
            dlg._title.Target.color = MenuStyle.Gold;
            dlg._title.Target.fontStyle = FontStyle.Bold;
            dlg._body = UiKit.Label(panel, "", 32, TextAnchor.MiddleCenter, 150);
            dlg._body.Target.color = MenuStyle.TextWhite;
            dlg._ok = UiKit.Button(panel, Strings.Get("common.ok"), dlg.Close);
            dlg._ok.navigation = new Navigation { mode = Navigation.Mode.None };
            blocker.gameObject.SetActive(false);
            return dlg;
        }

        public void Show(string titleLogical, string bodyLogical, Selectable returnFocus, Action onClose = null)
        {
            _title.SetText(titleLogical);
            _body.SetText(bodyLogical);
            _onClose = onClose;
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UiFocus.PushDialog(_ok);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
            var cb = _onClose;
            _onClose = null;
            cb?.Invoke();
        }

        public Button OkButton => _ok;
    }
}
