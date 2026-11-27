using System;
using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Screen 18: In-game Map Loading & Connecting Overlay with Cancel button.</summary>
    public sealed class LoadingOverlay : MonoBehaviour
    {
        Image _fill;
        float _p;
        Action _onCancel;

        public bool IsOpen => gameObject.activeSelf;

        public static LoadingOverlay Create(Transform parent, Action onCancel)
        {
            var root = UiKit.NewRect("LoadingOverlay", parent);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var ov = root.gameObject.AddComponent<LoadingOverlay>();
            ov._onCancel = onCancel;

            var panel = UiKit.Panel("Panel", root, new Vector2(780, 360));
            UiKit.Column(panel, 18, 34);
            var title = UiKit.Label(panel, Strings.Get("ingame.loading"), 28, TextAnchor.MiddleCenter, 40);
            title.Target.color = MenuStyle.Gold;
            title.Target.fontStyle = FontStyle.Bold;

            var bar = MenuStyle.Shape(panel, "Bar", MenuStyle.InputField);
            UiKit.Fit(bar.rectTransform, 22f);
            ov._fill = MenuStyle.Shape(bar.transform, "Fill", MenuStyle.White, MenuStyle.Gold);
            ov._fill.rectTransform.anchorMin = Vector2.zero;
            ov._fill.rectTransform.anchorMax = new Vector2(0.1f, 1f);
            ov._fill.rectTransform.offsetMin = new Vector2(4, 4);
            ov._fill.rectTransform.offsetMax = new Vector2(-4, -4);

            var tip = UiKit.Label(panel, Strings.Get("ingame.tip"), 20, TextAnchor.MiddleCenter, 44);
            tip.Target.color = MenuStyle.TextMuted;

            UiKit.Button(panel, Strings.Get("common.cancel"), () => { ov.Hide(); ov._onCancel?.Invoke(); }, 54f);
            root.gameObject.SetActive(false);
            return ov;
        }

        public void Show() { _p = 0.1f; gameObject.SetActive(true); transform.SetAsLastSibling(); }
        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            _p = Mathf.Min(0.95f, _p + Time.unscaledDeltaTime * 0.45f);
            if (_fill != null) _fill.rectTransform.anchorMax = new Vector2(_p, 1f);
        }
    }

    /// <summary>Screen 19: Message of the Day (MOTD) shown on server entry.</summary>
    public sealed class MotdModal : MonoBehaviour
    {
        Action _onClose;
        public bool IsOpen => gameObject.activeSelf;

        public static MotdModal Create(Transform parent, Action onClose)
        {
            var root = UiKit.NewRect("MotdModal", parent);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var motd = root.gameObject.AddComponent<MotdModal>();
            motd._onClose = onClose;

            var panel = UiKit.Panel("Panel", root, new Vector2(820, 480));
            UiKit.Column(panel, 18, 34);
            var title = UiKit.Label(panel, Strings.Get("ingame.motd.title"), 34, TextAnchor.MiddleCenter, 46);
            title.Target.color = MenuStyle.Gold;
            title.Target.fontStyle = FontStyle.Bold;

            var body = UiKit.Label(panel, "مرحباً بكم في خادم حماة الشام التكتيكي!\n• اللعب النظيف واحترام الجميع.\n• يُمنع الغش أو استغلال الأخطاء.\n• الإدارة: فريق حماة الشام", 24, TextAnchor.MiddleCenter, 220);
            body.Target.color = MenuStyle.TextWhite;

            UiKit.Button(panel, Strings.Get("common.ok"), () => { root.gameObject.SetActive(false); motd._onClose?.Invoke(); }, 56f);
            root.gameObject.SetActive(false);
            return motd;
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); }
    }

    /// <summary>Screen 20: Two-step Team & Character Class Select Modal with 3D preview.</summary>
    public sealed class TeamSelectModal : MonoBehaviour
    {
        RectTransform _stepTeam, _stepClass, _charList;
        ArabicText _stepTitle, _charName, _charRole, _charDesc, _qualityBtnLbl;
        Text _charCode;
        Image _qualityBtnImg;
        CharacterPreviewStage _preview;
        Action<int, string> _onSelected;
        int _chosenTeam;
        string _selectedCharId = "sas";
        public bool IsOpen => gameObject.activeSelf;

        public static TeamSelectModal Create(Transform parent, Action<int, string> onSelected)
        {
            var root = UiKit.NewRect("TeamSelectModal", parent);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = root.gameObject.AddComponent<TeamSelectModal>();
            modal._onSelected = onSelected;

            var panel = UiKit.Panel("Panel", root, new Vector2(1040, 680));
            UiKit.Column(panel, 14, 24);

            var title = UiKit.Label(panel, Strings.Get("ingame.team.title"), 30, TextAnchor.MiddleCenter, 40);
            title.Target.color = MenuStyle.Gold;
            title.Target.fontStyle = FontStyle.Bold;

            modal._stepTitle = UiKit.Label(panel, Strings.Get("ingame.team.step_team"), 20, TextAnchor.MiddleCenter, 28);
            modal._stepTitle.Target.color = MenuStyle.TextMuted;

            modal._stepTeam = UiKit.NewRect("StepTeam", panel);
            UiKit.Column(modal._stepTeam, 12, 8);
            UiKit.Button(modal._stepTeam, Strings.Get("ingame.team.defenders") + " (1)", () => modal.SelectTeam(0), 62f);
            UiKit.Button(modal._stepTeam, Strings.Get("ingame.team.opponents") + " (2)", () => modal.SelectTeam(1), 62f);
            UiKit.Button(modal._stepTeam, Strings.Get("ingame.team.auto") + " (5)", () => modal.SelectTeam(UnityEngine.Random.Range(0, 2)), 62f);
            UiKit.Button(modal._stepTeam, Strings.Get("ingame.team.spec") + " (6)", () => { modal.Hide(); modal._onSelected?.Invoke(2, string.Empty); }, 62f);

            modal.BuildCharacterStep(panel);

            root.gameObject.SetActive(false);
            return modal;
        }

        void BuildCharacterStep(RectTransform panel)
        {
            _stepClass = UiKit.NewRect("StepClass", panel);
            var stepLe = _stepClass.gameObject.AddComponent<LayoutElement>();
            stepLe.flexibleHeight = 1f;
            UiKit.Column(_stepClass, 12, 0);

            var topBar = UiKit.Row(_stepClass, "TopBar", 44f, 12f);
            UiKit.Button(topBar, Strings.Get("common.back"), () =>
            {
                _stepTitle.SetText(Strings.Get("ingame.team.step_team"));
                _stepClass.gameObject.SetActive(false);
                _stepTeam.gameObject.SetActive(true);
                _preview?.SetVisible(false);
            }, 42f);

            var qBtn = UiKit.Button(topBar, QualityLabel(), ToggleQuality, 42f);
            _qualityBtnImg = qBtn.GetComponent<Image>();
            _qualityBtnLbl = qBtn.GetComponentInChildren<ArabicText>();

            var split = UiKit.Row(_stepClass, "Split", 420f, 16f);
            split.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
            split.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            // Left: 3D character preview + bio
            var detail = UiKit.Panel("CharDetail", split, new Vector2(460, 420));
            detail.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.05f;
            UiKit.Column(detail, 8, 16);

            var previewBox = MenuStyle.Shape(detail, "PreviewBox", MenuStyle.ArmoryShowcase);
            UiKit.Fit(previewBox.rectTransform, 250f);
            var rawRt = UiKit.NewRect("PreviewRaw", previewBox.transform);
            UiKit.Stretch(rawRt);
            var rawImg = rawRt.gameObject.AddComponent<RawImage>();
            _preview = CharacterPreviewStage.Attach(rawImg, 440, 520);

            var nameRow = UiKit.Row(detail, "NameRow", 36f, 8f);
            _charCode = UiKit.LtrLabel(nameRow, "", 16, TextAnchor.MiddleCenter);
            _charCode.color = MenuStyle.Gold;
            _charCode.fontStyle = FontStyle.Bold;
            UiKit.FixedWidth(_charCode.gameObject, 120f);

            _charName = UiKit.Label(nameRow, "", 22, TextAnchor.MiddleRight, 34f);
            _charName.Target.color = MenuStyle.TextWhite;
            _charName.Target.fontStyle = FontStyle.Bold;

            _charRole = UiKit.Label(detail, "", 17, TextAnchor.MiddleRight, 24f);
            _charRole.Target.color = MenuStyle.Gold;

            _charDesc = UiKit.Label(detail, "", 17, TextAnchor.UpperRight, 46f);
            _charDesc.Target.color = MenuStyle.TextMuted;

            // Right: Character list
            _charList = UiKit.ScrollList(split, "CharList", 8f, 8);
            var listLe = _charList.parent.parent.GetComponent<LayoutElement>();
            if (listLe != null) listLe.flexibleWidth = 0.95f;

            var confirmBtn = UiKit.Button(_stepClass, Strings.Get("ingame.team.confirm"), ConfirmSelection, 52f);
            confirmBtn.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;

            _stepClass.gameObject.SetActive(false);
        }

        static string QualityLabel() =>
            (CharacterQuality)TacticalSettings.Current.CharacterQuality == CharacterQuality.Classic
                ? Strings.Get("loadout.char.classic")
                : Strings.Get("loadout.char.hd");

        void ToggleQuality()
        {
            var s = TacticalSettings.Current;
            s.CharacterQuality = s.CharacterQuality == 0 ? 1 : 0;
            if (_qualityBtnLbl != null) _qualityBtnLbl.SetText(QualityLabel());
            if (_qualityBtnImg != null) _qualityBtnImg.sprite = s.CharacterQuality == 0 ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            RefreshCharacterList();
        }

        void SelectTeam(int team)
        {
            _chosenTeam = team;
            _selectedCharId = TacticalSettings.Current.GetSelectedCharacterId((Team)team);
            _stepTitle.SetText(Strings.Get("ingame.team.step_char"));
            _stepTeam.gameObject.SetActive(false);
            _stepClass.gameObject.SetActive(true);
            _preview?.SetVisible(true);
            RefreshCharacterList();
        }

        void RefreshCharacterList()
        {
            for (int i = _charList.childCount - 1; i >= 0; i--)
                Destroy(_charList.GetChild(i).gameObject);

            List<CharacterSpec> chars = CharacterCatalog.ForTeam((Team)_chosenTeam);
            bool found = false;
            foreach (var c in chars) if (c.Id == _selectedCharId) { found = true; break; }
            if (!found && chars.Count > 0) _selectedCharId = chars[0].Id;

            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                bool isSel = c.Id == _selectedCharId;
                var plate = MenuStyle.Shape(_charList, "C_" + c.Id, isSel ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                string capturedId = c.Id;
                btn.onClick.AddListener(() => { _selectedCharId = capturedId; RefreshCharacterList(); });
                UiKit.Fit(plate.rectTransform, 58f);

                var row = UiKit.Row(plate.transform, "Row", 50f, 10f);
                UiKit.Stretch(row);
                row.offsetMin = new Vector2(14, 4);
                row.offsetMax = new Vector2(-14, -4);

                var code = UiKit.LtrLabel(row, c.CodeName, 14, TextAnchor.MiddleLeft);
                code.color = isSel ? MenuStyle.Gold : MenuStyle.TextMuted;
                UiKit.FixedWidth(code.gameObject, 105f);

                var lbl = UiKit.Label(row, c.ArabicName, 20, TextAnchor.MiddleRight, 44f);
                lbl.Target.color = isSel ? MenuStyle.Gold : MenuStyle.TextWhite;
                lbl.Target.fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal;
            }

            var sel = CharacterCatalog.GetById(_selectedCharId);
            var quality = (CharacterQuality)TacticalSettings.Current.CharacterQuality;
            _charCode.text = sel.CodeName;
            _charName.SetText(sel.ArabicName);
            string roleText = sel.RoleTag;
            if (quality == CharacterQuality.Classic && !sel.HasClassic)
                roleText += " • " + Strings.Get("loadout.char.hd_only");
            _charRole.SetText(roleText);
            _charDesc.SetText(sel.Description);
            _preview?.ShowCharacter(sel.Id, quality);
        }

        void ConfirmSelection()
        {
            var team = (Team)_chosenTeam;
            TacticalSettings.Current.SetSelectedCharacterId(team, _selectedCharId);
            TacticalSettings.Current.SaveAndApply();
            Hide();
            _onSelected?.Invoke(_chosenTeam, _selectedCharId);
        }

        public void Show()
        {
            _stepTitle.SetText(Strings.Get("ingame.team.step_team"));
            _stepTeam.gameObject.SetActive(true);
            _stepClass.gameObject.SetActive(false);
            _preview?.SetVisible(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _preview?.SetVisible(false);
            gameObject.SetActive(false);
        }
    }
}
