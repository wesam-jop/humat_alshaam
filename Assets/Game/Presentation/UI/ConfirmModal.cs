using System;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Screen 3: Tactical Exit Confirmation Modal matching the centered gold/dark design.</summary>
    public sealed class ConfirmModal : MonoBehaviour
    {
        ArabicText _title;
        ArabicText _body;
        Button _confirm;
        Button _cancel;
        Action _onConfirm;
        Selectable _returnFocus;

        public bool IsOpen => gameObject.activeSelf;

        public static ConfirmModal Create(Transform parent)
        {
            var blocker = UiKit.NewRect("ConfirmModal", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = blocker.gameObject.AddComponent<ConfirmModal>();

            var panel = UiKit.Panel("Panel", blocker, new Vector2(660, 340));
            UiKit.Column(panel, 14, 28);
            var crestSp = UiKit.LoadSprite("Adventure3D/UI/EagleCrest");
            if (crestSp != null)
            {
                var crRt = UiKit.NewRect("Crest", panel);
                UiKit.Fit(crRt, 54f);
                var img = crRt.gameObject.AddComponent<Image>();
                img.sprite = crestSp;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            modal._title = UiKit.Label(panel, "", 28, TextAnchor.MiddleCenter, 36);
            modal._title.Target.color = MenuStyle.Gold;
            modal._title.Target.fontStyle = FontStyle.Bold;
            UiKit.Divider(panel);
            modal._body = UiKit.Label(panel, "", 26, TextAnchor.MiddleCenter, 72);
            modal._body.Target.color = MenuStyle.TextWhite;

            var row = UiKit.Row(panel, "Actions", 60f, 18f);
            // Left: Cancel (secondary), Right: Yes (primary gold)
            modal._cancel = UiKit.Button(row, Strings.Get("common.cancel"), modal.Close, 58f);
            modal._confirm = UiKit.Button(row, Strings.Get("common.yes"), modal.Confirm, 58f);
            modal._confirm.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;
            UiFocus.ChainVertical(new System.Collections.Generic.List<Selectable> { modal._confirm, modal._cancel });
            blocker.gameObject.SetActive(false);
            return modal;
        }

        public void Show(string titleLogical, string bodyLogical, Selectable returnFocus, Action onConfirm)
        {
            _title.SetText(titleLogical);
            _body.SetText(bodyLogical);
            _onConfirm = onConfirm;
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UiFocus.PushDialog(_confirm);
        }

        void Confirm()
        {
            var cb = _onConfirm;
            Close();
            cb?.Invoke();
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
            _onConfirm = null;
        }
    }
}
