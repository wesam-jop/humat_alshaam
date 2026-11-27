using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Screen 17: Luxury Armory & Operatives Loadout Modal (Weapons tab with thumbnails, spotlight showcase,
    /// stat bars; Characters tab with HD/Classic CS 1.6 models and interactive 3D preview).
    /// </summary>
    public sealed class LoadoutModal : MonoBehaviour
    {
        RectTransform _weaponsTab, _charsTab;
        Image _tabWeaponsImg, _tabCharsImg;

        RectTransform _weaponList;
        RectTransform _weaponIconRt;
        ArabicText _weaponTitle, _categoryBadge;
        Image _weaponPreview, _weaponShadow;
        Image _dmgBar, _accBar, _rateBar;
        Text _dmgVal, _accVal, _rateVal;
        Text _priceLabel;

        RectTransform _charList;
        CharacterPreviewStage _charPreview;
        ArabicText _charName, _charRole, _charDesc, _qualityBtnLbl;
        Text _charCode;
        Image _teamDefImg, _teamAttImg, _qualityBtnImg;
        Team _previewTeam = Team.Defenders;
        int _activeTab;
        Selectable _returnFocus;

        public bool IsOpen => gameObject.activeSelf;

        public static LoadoutModal Create(Transform parent)
        {
            var blocker = UiKit.NewRect("LoadoutModal", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = blocker.gameObject.AddComponent<LoadoutModal>();
            modal.Build(blocker);
            blocker.gameObject.SetActive(false);
            return modal;
        }

        void Build(RectTransform blocker)
        {
            var panel = UiKit.Panel("Panel", blocker, new Vector2(1140, 760));
            UiKit.Column(panel, 12, 22);

            UiKit.ModalHeader(panel, Strings.Get("loadout.title"));

            var tabs = UiKit.Row(panel, "Tabs", 46f, 12f);
            var wTabBtn = UiKit.Button(tabs, Strings.Get("loadout.tab.weapons"), () => SwitchTab(0), 44f);
            var cTabBtn = UiKit.Button(tabs, Strings.Get("loadout.tab.characters"), () => SwitchTab(1), 44f);
            _tabWeaponsImg = wTabBtn.GetComponent<Image>();
            _tabCharsImg = cTabBtn.GetComponent<Image>();

            BuildWeaponsTab(panel);
            BuildCharactersTab(panel);

            var footer = UiKit.Row(panel, "Footer", 52f, 14f);
            UiKit.Button(footer, Strings.Get("common.back"), Close, 50f);
            var saveBtn = UiKit.Button(footer, Strings.Get("common.save"), () => { TacticalSettings.Current.SaveAndApply(); Close(); }, 50f);
            saveBtn.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;

            SwitchTab(0);
        }

        void BuildWeaponsTab(RectTransform panel)
        {
            _weaponsTab = UiKit.NewRect("WeaponsTab", panel);
            _weaponsTab.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            UiKit.Column(_weaponsTab, 0, 0);

            var split = UiKit.Row(_weaponsTab, "Split", 480f, 18f);
            var splitLe = split.gameObject.GetComponent<LayoutElement>();
            splitLe.flexibleHeight = 1f;
            split.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            // Left: Luxury Armory Showcase & Stats
            var detail = UiKit.Panel("Detail", split, new Vector2(540, 480));
            var detailLe = detail.gameObject.AddComponent<LayoutElement>();
            detailLe.flexibleWidth = 1.15f;
            UiKit.Column(detail, 10, 20);

            var topInfo = UiKit.Row(detail, "TopInfo", 40f, 10f);
            var pricePill = MenuStyle.Shape(topInfo, "PricePill", MenuStyle.InputField);
            UiKit.Fit(pricePill.rectTransform, 36f);
            UiKit.FixedWidth(pricePill.gameObject, 105f);
            _priceLabel = UiKit.LtrLabel(pricePill.transform, "$0", 20, TextAnchor.MiddleCenter);
            _priceLabel.color = MenuStyle.Gold;
            _priceLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_priceLabel.transform);

            _weaponTitle = UiKit.Label(topInfo, "", 24, TextAnchor.MiddleCenter, 38f);
            _weaponTitle.Target.color = MenuStyle.TextWhite;
            _weaponTitle.Target.fontStyle = FontStyle.Bold;

            var catPill = MenuStyle.Shape(topInfo, "CatBadge", MenuStyle.ButtonSelected);
            UiKit.Fit(catPill.rectTransform, 32f);
            UiKit.FixedWidth(catPill.gameObject, 120f);
            _categoryBadge = UiKit.Label(catPill.transform, "", 16, TextAnchor.MiddleCenter, 30f);
            _categoryBadge.Target.color = MenuStyle.Gold;
            _categoryBadge.Target.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_categoryBadge.transform);

            var previewBg = MenuStyle.Shape(detail, "PreviewBox", MenuStyle.ArmoryShowcase);
            UiKit.Fit(previewBg.rectTransform, 205f);

            var shadowRt = UiKit.NewRect("WeaponShadow", previewBg.transform);
            UiKit.Stretch(shadowRt);
            shadowRt.offsetMin = new Vector2(28, 12);
            shadowRt.offsetMax = new Vector2(-16, -24);
            _weaponShadow = shadowRt.gameObject.AddComponent<Image>();
            _weaponShadow.preserveAspect = true;
            _weaponShadow.color = new Color(0f, 0f, 0f, 0.65f);
            _weaponShadow.raycastTarget = false;

            _weaponIconRt = UiKit.NewRect("WeaponIcon", previewBg.transform);
            UiKit.Stretch(_weaponIconRt);
            _weaponIconRt.offsetMin = new Vector2(22, 18);
            _weaponIconRt.offsetMax = new Vector2(-22, -18);
            _weaponPreview = _weaponIconRt.gameObject.AddComponent<Image>();
            _weaponPreview.preserveAspect = true;
            _weaponPreview.raycastTarget = false;

            _dmgBar = StatBar(detail, Strings.Get("loadout.damage"), out _dmgVal);
            _accBar = StatBar(detail, Strings.Get("loadout.accuracy"), out _accVal);
            _rateBar = StatBar(detail, Strings.Get("loadout.firerate"), out _rateVal);

            // Right: Weapon Arsenal List with thumbnails
            _weaponList = UiKit.ScrollList(split, "WeaponList", 6f, 8);
            var listRootLe = _weaponList.parent.parent.GetComponent<LayoutElement>();
            if (listRootLe != null) listRootLe.flexibleWidth = 0.85f;
            RefreshList();
        }

        void BuildCharactersTab(RectTransform panel)
        {
            _charsTab = UiKit.NewRect("CharactersTab", panel);
            _charsTab.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            UiKit.Column(_charsTab, 10, 0);

            var filterRow = UiKit.Row(_charsTab, "FilterRow", 44f, 10f);
            var defBtn = UiKit.Button(filterRow, Strings.Get("loadout.char.team_def"), () => { _previewTeam = Team.Defenders; RefreshCharacterList(); }, 42f);
            var attBtn = UiKit.Button(filterRow, Strings.Get("loadout.char.team_att"), () => { _previewTeam = Team.Attackers; RefreshCharacterList(); }, 42f);
            var qualBtn = UiKit.Button(filterRow, QualityLabel(), ToggleQuality, 42f);
            _teamDefImg = defBtn.GetComponent<Image>();
            _teamAttImg = attBtn.GetComponent<Image>();
            _qualityBtnImg = qualBtn.GetComponent<Image>();
            _qualityBtnLbl = qualBtn.GetComponentInChildren<ArabicText>();

            var split = UiKit.Row(_charsTab, "CharSplit", 430f, 18f);
            split.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
            split.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            // Left: Live 3D Character Preview & Details
            var detail = UiKit.Panel("CharDetail", split, new Vector2(540, 430));
            detail.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.1f;
            UiKit.Column(detail, 8, 18);

            var previewBox = MenuStyle.Shape(detail, "CharPreviewBox", MenuStyle.ArmoryShowcase);
            UiKit.Fit(previewBox.rectTransform, 300f);
            var rawRt = UiKit.NewRect("CharRaw", previewBox.transform);
            UiKit.Stretch(rawRt);
            var rawImg = rawRt.gameObject.AddComponent<RawImage>();
            _charPreview = CharacterPreviewStage.Attach(rawImg, 512, 600);

            var nameRow = UiKit.Row(detail, "NameRow", 36f, 10f);
            _charCode = UiKit.LtrLabel(nameRow, "", 16, TextAnchor.MiddleCenter);
            _charCode.color = MenuStyle.Gold;
            _charCode.fontStyle = FontStyle.Bold;
            UiKit.FixedWidth(_charCode.gameObject, 130f);

            _charName = UiKit.Label(nameRow, "", 24, TextAnchor.MiddleRight, 34f);
            _charName.Target.color = MenuStyle.TextWhite;
            _charName.Target.fontStyle = FontStyle.Bold;

            _charRole = UiKit.Label(detail, "", 18, TextAnchor.MiddleRight, 26f);
            _charRole.Target.color = MenuStyle.Gold;

            _charDesc = UiKit.Label(detail, "", 18, TextAnchor.UpperRight, 48f);
            _charDesc.Target.color = MenuStyle.TextMuted;

            // Right: Character roster list
            _charList = UiKit.ScrollList(split, "CharList", 8f, 8);
            var listLe = _charList.parent.parent.GetComponent<LayoutElement>();
            if (listLe != null) listLe.flexibleWidth = 0.9f;
            RefreshCharacterList();
        }

        void SwitchTab(int tab)
        {
            _activeTab = tab;
            if (_weaponsTab != null) _weaponsTab.gameObject.SetActive(tab == 0);
            if (_charsTab != null) _charsTab.gameObject.SetActive(tab == 1);
            if (_tabWeaponsImg != null) _tabWeaponsImg.sprite = tab == 0 ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            if (_tabCharsImg != null) _tabCharsImg.sprite = tab == 1 ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            _charPreview?.SetVisible(gameObject.activeSelf && tab == 1);
            if (tab == 1) RefreshCharacterList();
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

        void RefreshCharacterList()
        {
            if (_charList == null) return;
            if (_teamDefImg != null) _teamDefImg.sprite = _previewTeam == Team.Defenders ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            if (_teamAttImg != null) _teamAttImg.sprite = _previewTeam == Team.Attackers ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;
            if (_qualityBtnImg != null) _qualityBtnImg.sprite = TacticalSettings.Current.CharacterQuality == 0 ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal;

            for (int i = _charList.childCount - 1; i >= 0; i--)
                Destroy(_charList.GetChild(i).gameObject);

            var s = TacticalSettings.Current;
            string selectedId = s.GetSelectedCharacterId(_previewTeam);
            List<CharacterSpec> chars = CharacterCatalog.ForTeam(_previewTeam);
            bool found = false;
            foreach (var c in chars) if (c.Id == selectedId) { found = true; break; }
            if (!found && chars.Count > 0)
            {
                selectedId = chars[0].Id;
                s.SetSelectedCharacterId(_previewTeam, selectedId);
            }

            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                bool isSel = c.Id == selectedId;
                var plate = MenuStyle.Shape(_charList, "Char_" + c.Id, isSel ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                string capturedId = c.Id;
                btn.onClick.AddListener(() =>
                {
                    TacticalSettings.Current.SetSelectedCharacterId(_previewTeam, capturedId);
                    RefreshCharacterList();
                });
                UiKit.Fit(plate.rectTransform, 60f);

                var row = UiKit.Row(plate.transform, "Row", 50f, 10f);
                UiKit.Stretch(row);
                row.offsetMin = new Vector2(14, 4);
                row.offsetMax = new Vector2(-14, -4);

                var code = UiKit.LtrLabel(row, c.CodeName, 14, TextAnchor.MiddleLeft);
                code.color = isSel ? MenuStyle.Gold : MenuStyle.TextMuted;
                UiKit.FixedWidth(code.gameObject, 105f);

                var role = UiKit.Label(row, c.RoleTag, 15, TextAnchor.MiddleCenter, 40f);
                role.Target.color = MenuStyle.TextMuted;
                UiKit.FixedWidth(role.gameObject, 110f);

                var lbl = UiKit.Label(row, c.ArabicName, 20, TextAnchor.MiddleRight, 44f);
                lbl.Target.color = isSel ? MenuStyle.Gold : MenuStyle.TextWhite;
                lbl.Target.fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal;
            }

            var sel = CharacterCatalog.GetById(selectedId);
            var quality = (CharacterQuality)s.CharacterQuality;
            _charCode.text = sel.CodeName;
            _charName.SetText(sel.ArabicName);
            string roleLine = sel.RoleTag;
            if (quality == CharacterQuality.Classic && !sel.HasClassic)
                roleLine += " • " + Strings.Get("loadout.char.hd_only");
            _charRole.SetText(roleLine);
            _charDesc.SetText(sel.Description);
            _charPreview?.ShowCharacter(sel.Id, quality);
        }

        static Image StatBar(Transform parent, string label, out Text valText)
        {
            var row = UiKit.Row(parent, "StatRow", 34f, 10f);

            valText = UiKit.LtrLabel(row, "0", 17, TextAnchor.MiddleCenter);
            valText.color = MenuStyle.Gold;
            UiKit.FixedWidth(valText.gameObject, 42f);

            var bg = MenuStyle.Shape(row, "BarBg", MenuStyle.InputField);
            UiKit.Fit(bg.rectTransform, 18f);
            var fillRt = UiKit.NewRect("Fill", bg.transform);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.5f, 1f);
            fillRt.offsetMin = new Vector2(3, 3);
            fillRt.offsetMax = new Vector2(-3, -3);
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.color = MenuStyle.Gold;
            fill.raycastTarget = false;

            var lbl = UiKit.Label(row, label, 19, TextAnchor.MiddleRight, 32f);
            lbl.Target.color = MenuStyle.TextMuted;
            UiKit.FixedWidth(lbl.gameObject, 130f);
            return fill;
        }

        static string CategoryName(int cat) => cat switch
        {
            1 => "مسدس",
            2 => "شوزن",
            3 => "رشاش خفيف",
            4 => "بندقية",
            5 => "قناص / ثقيل",
            _ => "عتاد"
        };

        void RefreshList()
        {
            for (int i = _weaponList.childCount - 1; i >= 0; i--)
                Destroy(_weaponList.GetChild(i).gameObject);

            int sel = TacticalSettings.Current.SelectedWeaponIndex;
            for (int i = 0; i < TacticalCatalog.Weapons.Length; i++)
            {
                int idx = i;
                var w = TacticalCatalog.Weapons[i];
                if (w.Category > 5) continue;
                bool isSel = i == sel;

                var plate = MenuStyle.Shape(_weaponList, "W_" + i, isSel ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                btn.onClick.AddListener(() => { TacticalSettings.Current.SelectedWeaponIndex = idx; RefreshList(); });
                UiKit.Fit(plate.rectTransform, 54f);

                var row = UiKit.Row(plate.transform, "Row", 48f, 10f);
                UiKit.Stretch(row);
                row.offsetMin = new Vector2(12, 3);
                row.offsetMax = new Vector2(-14, -3);

                // Mini weapon thumbnail on the left
                var thumbRt = UiKit.NewRect("Thumb", row);
                UiKit.Fit(thumbRt, 36f);
                UiKit.FixedWidth(thumbRt.gameObject, 78f);
                var spThumb = !string.IsNullOrEmpty(w.Icon) ? UiKit.LoadSprite("Adventure3D/Weapons/" + w.Icon) : null;
                if (spThumb != null)
                {
                    var tImg = thumbRt.gameObject.AddComponent<Image>();
                    tImg.sprite = spThumb;
                    tImg.preserveAspect = true;
                    tImg.raycastTarget = false;
                }

                // Price tag
                var pTxt = UiKit.LtrLabel(row, "$" + w.Price, 15, TextAnchor.MiddleLeft);
                pTxt.color = isSel ? MenuStyle.Gold : MenuStyle.TextMuted;
                UiKit.FixedWidth(pTxt.gameObject, 58f);

                // Weapon name on the right (RTL)
                var lbl = UiKit.Label(row, w.Name, 19, TextAnchor.MiddleRight, 44f);
                lbl.Target.color = isSel ? MenuStyle.Gold : MenuStyle.TextWhite;
                lbl.Target.fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal;
            }

            if (sel >= 0 && sel < TacticalCatalog.Weapons.Length)
            {
                var cur = TacticalCatalog.Weapons[sel];
                _weaponTitle.SetText(cur.Name);
                _categoryBadge.SetText(CategoryName(cur.Category));
                var sp = !string.IsNullOrEmpty(cur.Icon) ? UiKit.LoadSprite("Adventure3D/Weapons/" + cur.Icon) : null;
                if (_weaponPreview != null)
                {
                    _weaponPreview.enabled = sp != null;
                    _weaponPreview.sprite = sp;
                }
                if (_weaponShadow != null)
                {
                    _weaponShadow.enabled = sp != null;
                    _weaponShadow.sprite = sp;
                }
                _dmgBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(cur.Damage / 100f), 1f);
                _accBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(cur.Accuracy / 100f), 1f);
                _rateBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(cur.FireRate / 100f), 1f);
                _dmgVal.text = cur.Damage.ToString();
                _accVal.text = cur.Accuracy.ToString();
                _rateVal.text = cur.FireRate.ToString();
                _priceLabel.text = "$" + cur.Price;
            }
        }

        void Update()
        {
            if (_weaponIconRt != null && _weaponPreview != null && _weaponPreview.enabled)
            {
                float hoverY = Mathf.Sin(Time.unscaledTime * 2.2f) * 3.5f;
                _weaponIconRt.anchoredPosition = new Vector2(0f, hoverY);
            }
        }

        public void Open(Selectable returnFocus)
        {
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshList();
            if (_activeTab == 1) RefreshCharacterList();
            _charPreview?.SetVisible(_activeTab == 1);
            UiFocus.PushDialog(GetComponentInChildren<Button>());
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            _charPreview?.SetVisible(false);
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
        }
    }
}
