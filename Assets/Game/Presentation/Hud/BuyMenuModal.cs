using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Screen 21: Tactical Shop (B key) with 5 metallic categories, golden balance header, and 3x3 weapon grid.</summary>
    public sealed class BuyMenuModal : MonoBehaviour
    {
        static readonly string[] CategoryKeys =
        {
            "ingame.shop.cat.rifles", "ingame.shop.cat.pistols", "ingame.shop.cat.heavy",
            "ingame.shop.cat.gear", "ingame.shop.cat.grenades",
        };

        RectTransform _gridContent;
        ScrollRect _scroll;
        ArabicText _categoryHeader;
        Text _balanceText;
        readonly Image[] _tabBg = new Image[5];
        readonly ArabicText[] _tabLabels = new ArabicText[5];
        int _catIndex, _money = 4500;
        Action<WeaponSpec> _onBuy;

        public bool IsOpen => gameObject.activeSelf;

        public static BuyMenuModal Create(Transform parent, Action<WeaponSpec> onBuy)
        {
            var root = UiKit.NewRect("BuyMenuModal", parent);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = TacticalHudArt.OverlayBackdrop;

            var modal = root.gameObject.AddComponent<BuyMenuModal>();
            modal._onBuy = onBuy;

            TacticalHudArt.CreateTopLeftBrand(root, new Vector2(24f, -18f));
            TacticalHudArt.CreateModalTitle(root, Strings.Get("ingame.shop.title"), -26f);

            var body = UiKit.NewRect("ShopBody", root);
            UiKit.Anchor(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f));
            body.sizeDelta = new Vector2(1140f, 610f);
            var split = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            split.spacing = 16f;
            split.childControlWidth = false;
            split.childControlHeight = true;

            // Left panel: bronze header + scrollable 3x3 weapon card grid + gold scrollbar
            var mainPanel = MenuStyle.Shape(body, "CatalogPanel", TacticalHudArt.ShopPanelBg);
            SetW(mainPanel.rectTransform, 868f);
            var mv = mainPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            mv.padding = new RectOffset(1, 1, 1, 1);
            mv.childControlWidth = true;
            mv.childControlHeight = false;

            var hdr = MenuStyle.Shape(mainPanel.transform, "CategoryHeader", TacticalHudArt.ScoreHeaderGold);
            UiKit.Fit(hdr.rectTransform, 56f);
            modal._categoryHeader = UiKit.Label(hdr.transform, "", 28, TextAnchor.MiddleCenter, 48f);
            modal._categoryHeader.Target.color = TacticalHudArt.GoldSoft;
            modal._categoryHeader.Target.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(modal._categoryHeader.Target);
            UiKit.Stretch((RectTransform)modal._categoryHeader.transform);

            var wrap = UiKit.NewRect("ScrollWrap", mainPanel.transform);
            var wLe = wrap.gameObject.AddComponent<LayoutElement>();
            wLe.minHeight = wLe.preferredHeight = 550f;
            wLe.flexibleHeight = 1f;

            var scroll = wrap.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            modal._scroll = scroll;

            var vp = UiKit.NewRect("Viewport", wrap);
            UiKit.Stretch(vp);
            vp.offsetMax = new Vector2(-18f, 0f);
            vp.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.004f);
            vp.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = UiKit.NewRect("GridContent", vp);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(262f, 162f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(16, 14, 16, 16);
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vp;
            scroll.content = content;
            modal._gridContent = content;

            var sbTrack = UiKit.NewRect("Scrollbar", wrap);
            sbTrack.anchorMin = new Vector2(1f, 0f);
            sbTrack.anchorMax = new Vector2(1f, 1f);
            sbTrack.pivot = new Vector2(1f, 0.5f);
            sbTrack.offsetMin = new Vector2(-14f, 14f);
            sbTrack.offsetMax = new Vector2(-5f, -14f);
            sbTrack.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.72f);

            var sbArea = UiKit.NewRect("SlidingArea", sbTrack);
            UiKit.Stretch(sbArea);
            var sbHandle = UiKit.NewRect("Handle", sbArea);
            UiKit.Stretch(sbHandle);
            var sbImg = sbHandle.gameObject.AddComponent<Image>();
            sbImg.color = TacticalHudArt.Gold;

            var sb = sbTrack.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = sbHandle;
            sb.targetGraphic = sbImg;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // Right sidebar: Balance card + 5 metallic category buttons
            var side = UiKit.NewRect("Sidebar", body);
            SetW(side, 256f);
            var sv = side.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 12f;
            sv.childControlWidth = true;
            sv.childControlHeight = false;

            var balBox = MenuStyle.Shape(side, "BalanceCard", TacticalHudArt.ShopBalanceBg);
            UiKit.Fit(balBox.rectTransform, 128f);
            var bv = balBox.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.padding = new RectOffset(14, 14, 14, 14);
            bv.spacing = 6f;
            bv.childAlignment = TextAnchor.MiddleCenter;
            bv.childControlWidth = true;
            bv.childControlHeight = false;

            var balLbl = UiKit.Label(balBox.transform, Strings.Get("ingame.shop.balance"), 21, TextAnchor.MiddleCenter, 28f);
            balLbl.Target.color = TacticalHudArt.Cream;

            var balRow = UiKit.NewRect("BalRow", balBox.transform);
            UiKit.Fit(balRow, 48f);
            var bh = balRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.spacing = 10f;
            bh.childAlignment = TextAnchor.MiddleCenter;
            bh.childControlWidth = false;
            bh.childControlHeight = false;

            modal._balanceText = UiKit.LtrLabel(balRow, "$4500", 38, TextAnchor.MiddleCenter);
            modal._balanceText.color = TacticalHudArt.GoldBright;
            modal._balanceText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(modal._balanceText);
            ((RectTransform)modal._balanceText.transform).sizeDelta = new Vector2(146f, 46f);
            TacticalHudArt.Icon(balRow, "Coins", TacticalHudArt.CoinsIcon, new Vector2(38f, 38f), TacticalHudArt.GoldBright);

            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                int idx = i;
                var tab = MenuStyle.Shape(side, "CatTab_" + i, TacticalHudArt.ShopTabNormal);
                UiKit.Fit(tab.rectTransform, 84f);
                var btn = tab.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = tab;
                btn.onClick.AddListener(() => modal.SelectCategory(idx));

                var lbl = UiKit.Label(tab.transform, Strings.Get(CategoryKeys[i]), 28, TextAnchor.MiddleCenter, 48f);
                lbl.Target.color = TacticalHudArt.Cream;
                lbl.Target.fontStyle = FontStyle.Bold;
                TacticalHudArt.AddShadow(lbl.Target);
                UiKit.Stretch((RectTransform)lbl.transform);

                modal._tabBg[i] = tab;
                modal._tabLabels[i] = lbl;
            }

            modal.SelectCategory(0);
            root.gameObject.SetActive(false);
            return modal;
        }

        static void SetW(RectTransform rt, float w)
        {
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.flexibleWidth = 0f;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) SelectCategory(0);
            else if (kb.digit2Key.wasPressedThisFrame) SelectCategory(1);
            else if (kb.digit3Key.wasPressedThisFrame) SelectCategory(2);
            else if (kb.digit4Key.wasPressedThisFrame) SelectCategory(3);
            else if (kb.digit5Key.wasPressedThisFrame) SelectCategory(4);
        }

        static bool IsSniper(string id) => id == "awp" || id == "scout" || id == "g3sg1" || id == "sg550";
        static bool IsGrenade(string id) => id == "hegrenade" || id == "smoke" || id == "flashbang";
        static int Priority(string id) => id switch
        {
            "ak47" => 0, "m4a1" => 1, "mp5" => 2, "p90" => 3, "sg552" => 4, "aug" => 5, "ump45" => 6, "mac10" => 7, "tmp" => 8, "m249" => 9,
            "deagle" => 0, "usp45" => 1, "glock18" => 2, "p228" => 3, "elites" => 4, "fiveseven" => 5,
            "awp" => 0, "scout" => 1, "m3" => 2, "xm1014" => 3, "g3sg1" => 4, "sg550" => 5,
            "helmet" => 0, "kevlar" => 1, "defuser" => 2,
            "hegrenade" => 0, "smoke" => 1, "flashbang" => 2,
            _ => 20,
        };

        static List<WeaponSpec> ItemsForTab(int tab)
        {
            var list = new List<WeaponSpec>();
            foreach (var w in TacticalCatalog.Weapons)
            {
                bool ok = tab switch
                {
                    0 => (w.Category == 4 && !IsSniper(w.Icon)) || w.Category == 3 || w.Category == 5,
                    1 => w.Category == 1,
                    2 => (w.Category == 4 && IsSniper(w.Icon)) || w.Category == 2,
                    3 => w.Category == 6 || w.Category == 7 || (w.Category == 8 && !IsGrenade(w.Icon)),
                    4 => w.Category == 8 && IsGrenade(w.Icon),
                    _ => false,
                };
                if (ok) list.Add(w);
            }
            list.Sort((a, b) => Priority(a.Icon).CompareTo(Priority(b.Icon)));
            return list;
        }

        void SelectCategory(int idx)
        {
            _catIndex = Mathf.Clamp(idx, 0, CategoryKeys.Length - 1);
            _categoryHeader.SetText(Strings.Get(CategoryKeys[_catIndex]));
            for (int i = 0; i < _tabBg.Length; i++)
            {
                bool act = i == _catIndex;
                _tabBg[i].sprite = act ? TacticalHudArt.ShopTabActive : TacticalHudArt.ShopTabNormal;
                _tabLabels[i].Target.color = act ? new Color32(0xFF, 0xFA, 0xEC, 0xFF) : TacticalHudArt.Cream;
            }
            for (int i = _gridContent.childCount - 1; i >= 0; i--) Destroy(_gridContent.GetChild(i).gameObject);
            foreach (var w in ItemsForTab(_catIndex)) BuildCard(w);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        void BuildCard(WeaponSpec weapon)
        {
            bool afford = _money >= weapon.Price;
            var card = MenuStyle.Shape(_gridContent, "Card_" + weapon.Icon, TacticalHudArt.ShopCardBg);
            card.raycastTarget = false;

            var nameLbl = UiKit.Label(card.transform, weapon.Name, 19, TextAnchor.MiddleCenter, 28f);
            nameLbl.Target.color = afford ? TacticalHudArt.Cream : TacticalHudArt.Muted;
            nameLbl.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nRt = (RectTransform)nameLbl.transform;
            UiKit.Anchor(nRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f));
            nRt.sizeDelta = new Vector2(244f, 28f);

            var sp = !string.IsNullOrEmpty(weapon.Icon) ? UiKit.LoadSprite("Adventure3D/Weapons/" + weapon.Icon) : null;
            if (sp != null)
            {
                var img = TacticalHudArt.Icon(card.transform, "WeaponIcon", sp, new Vector2(198f, 68f),
                    afford ? TacticalHudArt.Gold : new Color32(0x6E, 0x76, 0x80, 0xD8));
                UiKit.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f));
            }

            var price = UiKit.LtrLabel(card.transform, "$" + weapon.Price, 22, TextAnchor.MiddleLeft);
            price.color = afford ? TacticalHudArt.GoldBright : TacticalHudArt.Muted;
            price.fontStyle = FontStyle.Bold;
            var pRt = (RectTransform)price.transform;
            UiKit.Anchor(pRt, Vector2.zero, Vector2.zero, new Vector2(14f, 12f));
            pRt.sizeDelta = new Vector2(118f, 32f);

            var buyBg = MenuStyle.Shape(card.transform, "BuyBtn", afford ? TacticalHudArt.BuyBtnNormal : TacticalHudArt.BuyBtnDisabled);
            UiKit.Anchor(buyBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 12f));
            buyBg.rectTransform.sizeDelta = new Vector2(92f, 32f);
            var btn = buyBg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = buyBg;
            btn.interactable = afford;
            btn.onClick.AddListener(() => TryBuy(weapon));

            var buyLbl = UiKit.Label(buyBg.transform, Strings.Get("ingame.shop.buy"), 17, TextAnchor.MiddleCenter, 28f);
            buyLbl.Target.color = afford ? new Color32(0x14, 0x18, 0x1E, 0xFF) : TacticalHudArt.Muted;
            buyLbl.Target.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)buyLbl.transform);
        }

        void TryBuy(WeaponSpec w)
        {
            if (_money < w.Price) return;
            _money -= w.Price;
            _balanceText.text = "$" + _money;
            _onBuy?.Invoke(w);
            Hide();
        }

        public void Show(int currentMoney)
        {
            _money = currentMoney;
            _balanceText.text = "$" + _money;
            SelectCategory(_catIndex);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
