using System;
using System.Collections.Generic;
using Adventure3D.Core;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Screens 7, 8, 9, 10: Server Browser (Internet, LAN, Favorites, History) + Filters Modal,
    /// Direct Connect Modal, and Server Info & Password Modal.
    /// </summary>
    public sealed class ServerBrowserModal : MonoBehaviour
    {
        readonly List<ServerEntry> _servers = TacticalCatalog.SampleServers();
        Button[] _tabs;
        RectTransform _listContent;
        ServerEntry _selected;
        int _activeTab;
        Selectable _returnFocus;
        Action<LaunchRequest> _onConnect;

        // Filter state
        string _filterMap = "";
        bool _hideFull, _hideEmpty, _hidePassword;
        int _maxPing = 200;

        RectTransform _filtersModal, _directModal, _infoModal;
        InputField _filterMapInput, _directInput, _infoPasswordInput;
        ArabicText _infoHeader;
        RectTransform _infoPlayersList;

        public bool IsOpen => gameObject.activeSelf;

        public static ServerBrowserModal Create(Transform parent, Action<LaunchRequest> onConnect)
        {
            var blocker = UiKit.NewRect("ServerBrowserModal", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = blocker.gameObject.AddComponent<ServerBrowserModal>();
            modal._onConnect = onConnect;
            modal.Build(blocker);
            blocker.gameObject.SetActive(false);
            return modal;
        }

        void Build(RectTransform blocker)
        {
            var panel = UiKit.Panel("Panel", blocker, new Vector2(1140, 720));
            UiKit.Column(panel, 12, 24);

            UiKit.ModalHeader(panel, Strings.Get("browser.title"));

            // RTL tab order: Internet on right -> History on left
            _tabs = UiKit.TabBar(panel, new[]
            {
                Strings.Get("browser.tab.history"),
                Strings.Get("browser.tab.favorites"),
                Strings.Get("browser.tab.lan"),
                Strings.Get("browser.tab.internet"),
            }, idx => { _activeTab = 3 - idx; RefreshList(); }, 46f);

            // Column headers
            var header = UiKit.Row(panel, "Headers", 36f, 8f);
            AddHeaderCell(header, Strings.Get("browser.col.ping"), 90f);
            AddHeaderCell(header, Strings.Get("browser.col.players"), 110f);
            AddHeaderCell(header, Strings.Get("browser.col.map"), 160f);
            AddHeaderCell(header, Strings.Get("browser.col.name"), 0f);

            _listContent = UiKit.ScrollList(panel, "ServerList", 6f, 8);

            var actions = UiKit.Row(panel, "Actions", 50f, 8f);
            UiKit.Button(actions, Strings.Get("common.back"), Close, 48f);
            UiKit.Button(actions, Strings.Get("browser.filters"), () => OpenSubModal(_filtersModal), 48f);
            UiKit.Button(actions, Strings.Get("browser.direct"), () => OpenSubModal(_directModal), 48f);
            UiKit.Button(actions, Strings.Get("browser.info"), OpenServerInfo, 48f);
            UiKit.Button(actions, Strings.Get("browser.add_fav"), ToggleFavorite, 48f);
            UiKit.Button(actions, Strings.Get("browser.refresh_all"), RefreshList, 48f);
            var connBtn = UiKit.Button(actions, Strings.Get("join.connect"), ConnectSelected, 48f);
            connBtn.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;

            BuildFiltersModal(blocker);
            BuildDirectConnectModal(blocker);
            BuildInfoModal(blocker);
            RefreshList();
        }

        static void AddHeaderCell(Transform parent, string text, float fixedWidth)
        {
            var lbl = UiKit.Label(parent, text, 19, TextAnchor.MiddleCenter, 34f);
            lbl.Target.color = MenuStyle.TextMuted;
            if (fixedWidth > 0f)
            {
                var le = lbl.gameObject.GetComponent<LayoutElement>();
                le.minWidth = fixedWidth;
                le.preferredWidth = fixedWidth;
                le.flexibleWidth = 0f;
            }
        }

        void RefreshList()
        {
            UiKit.SetActiveTab(_tabs, 3 - _activeTab);
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            foreach (var s in _servers)
            {
                if (_activeTab == 1 && !s.IsLan) continue;
                if (_activeTab == 2 && !s.IsFavorite) continue;
                if (_activeTab == 3 && !s.IsHistory) continue;
                if (_hideFull && s.Players >= s.MaxPlayers) continue;
                if (_hideEmpty && s.Players <= 0) continue;
                if (_hidePassword && s.HasPassword) continue;
                if (s.Ping > _maxPing) continue;
                if (!string.IsNullOrEmpty(_filterMap) && !s.Map.Contains(_filterMap, StringComparison.OrdinalIgnoreCase)) continue;

                AddServerRow(s);
            }
        }

        void AddServerRow(ServerEntry entry)
        {
            bool selected = ReferenceEquals(_selected, entry);
            var plate = MenuStyle.Shape(_listContent, "ServerRow", selected ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal);
            var btn = plate.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = plate;
            btn.onClick.AddListener(() => { _selected = entry; RefreshList(); });
            UiKit.Fit(plate.rectTransform, 48f);

            var row = UiKit.Row(plate.transform, "Cols", 46f, 8f);
            UiKit.Stretch(row);
            row.offsetMin = new Vector2(12f, 0f);
            row.offsetMax = new Vector2(-12f, 0f);

            var pingLbl = UiKit.LtrLabel(row, entry.Ping + "ms", 20, TextAnchor.MiddleCenter);
            pingLbl.color = entry.Ping < 50 ? MenuStyle.Success : (entry.Ping < 100 ? MenuStyle.Gold : MenuStyle.Danger);
            SetWidth(pingLbl.gameObject, 90f);

            var plLbl = UiKit.LtrLabel(row, $"{entry.Players}/{entry.MaxPlayers}", 20, TextAnchor.MiddleCenter);
            plLbl.color = MenuStyle.TextWhite;
            SetWidth(plLbl.gameObject, 110f);

            var mapLbl = UiKit.LtrLabel(row, entry.Map, 20, TextAnchor.MiddleCenter);
            mapLbl.color = MenuStyle.TextMuted;
            SetWidth(mapLbl.gameObject, 160f);

            string display = (entry.HasPassword ? "[🔒] " : "") + entry.Name;
            var nameLbl = UiKit.Label(row, display, 21, TextAnchor.MiddleRight, 44f);
            nameLbl.Target.color = selected ? MenuStyle.Gold : MenuStyle.TextWhite;
        }

        static void SetWidth(GameObject go, float w)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = w;
            le.preferredWidth = w;
            le.flexibleWidth = 0f;
        }

        void ToggleFavorite()
        {
            if (_selected == null) return;
            _selected.IsFavorite = !_selected.IsFavorite;
            RefreshList();
        }

        void ConnectSelected()
        {
            if (_selected == null) return;
            if (_selected.HasPassword) { OpenServerInfo(); return; }
            Close();
            _onConnect?.Invoke(new LaunchRequest { Mode = SessionMode.Client, Address = _selected.Address, Port = _selected.Port });
        }

        // ---- Sub-Modals (8, 9, 10)
        void BuildFiltersModal(Transform blocker)
        {
            var bg = UiKit.NewRect("FiltersModal", blocker);
            UiKit.Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var panel = UiKit.Panel("Panel", bg, new Vector2(640, 480));
            UiKit.Column(panel, 12, 28);

            var t = UiKit.Label(panel, Strings.Get("browser.filters"), 30, TextAnchor.MiddleCenter, 40);
            t.Target.color = MenuStyle.Gold;
            _filterMapInput = UiKit.LtrInput(panel, Strings.Get("browser.filter.map"), _filterMap, InputField.ContentType.Standard);
            UiKit.Checkbox(panel, Strings.Get("browser.filter.not_full"), _hideFull, v => _hideFull = v);
            UiKit.Checkbox(panel, Strings.Get("browser.filter.not_empty"), _hideEmpty, v => _hideEmpty = v);
            UiKit.Checkbox(panel, Strings.Get("browser.filter.no_pass"), _hidePassword, v => _hidePassword = v);
            UiKit.SliderRow(panel, Strings.Get("browser.filter.max_ping"), 20, 300, _maxPing, true, "0", v => _maxPing = Mathf.RoundToInt(v));
            UiKit.Button(panel, Strings.Get("common.apply"), () => { _filterMap = _filterMapInput.text.Trim(); bg.gameObject.SetActive(false); RefreshList(); }, 54f);

            bg.gameObject.SetActive(false);
            _filtersModal = bg;
        }

        void BuildDirectConnectModal(Transform blocker)
        {
            var bg = UiKit.NewRect("DirectConnectModal", blocker);
            UiKit.Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var panel = UiKit.Panel("Panel", bg, new Vector2(620, 320));
            UiKit.Column(panel, 16, 28);

            var t = UiKit.Label(panel, Strings.Get("browser.direct"), 30, TextAnchor.MiddleCenter, 40);
            t.Target.color = MenuStyle.Gold;
            _directInput = UiKit.LtrInput(panel, Strings.Get("join.address"), "127.0.0.1:7777", InputField.ContentType.Standard);
            var row = UiKit.Row(panel, "Actions", 56f, 12f);
            UiKit.Button(row, Strings.Get("common.cancel"), () => bg.gameObject.SetActive(false), 54f);
            UiKit.Button(row, Strings.Get("join.connect"), () =>
            {
                string raw = (_directInput.text ?? "").Trim();
                string[] parts = raw.Split(':');
                string host = parts[0];
                ushort port = parts.Length > 1 && AddressTools.TryParsePort(parts[1], out var p) ? p : (ushort)7777;
                if (AddressTools.Classify(host) == AddressTools.Kind.Invalid) return;
                bg.gameObject.SetActive(false);
                Close();
                _onConnect?.Invoke(new LaunchRequest { Mode = SessionMode.Client, Address = host, Port = port });
            }, 54f);

            bg.gameObject.SetActive(false);
            _directModal = bg;
        }

        void BuildInfoModal(Transform blocker)
        {
            var bg = UiKit.NewRect("ServerInfoModal", blocker);
            UiKit.Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var panel = UiKit.Panel("Panel", bg, new Vector2(680, 500));
            UiKit.Column(panel, 12, 28);

            _infoHeader = UiKit.Label(panel, Strings.Get("browser.info"), 28, TextAnchor.MiddleCenter, 40);
            _infoHeader.Target.color = MenuStyle.Gold;
            var plTitle = UiKit.Label(panel, Strings.Get("browser.info.players"), 20, TextAnchor.MiddleRight, 28);
            plTitle.Target.color = MenuStyle.TextMuted;
            _infoPlayersList = UiKit.ScrollList(panel, "PlayersList", 4f, 8);
            _infoPasswordInput = UiKit.LtrInput(panel, Strings.Get("browser.info.password"), "", InputField.ContentType.Password);

            var row = UiKit.Row(panel, "Actions", 56f, 12f);
            UiKit.Button(row, Strings.Get("common.close"), () => bg.gameObject.SetActive(false), 54f);
            UiKit.Button(row, Strings.Get("join.connect"), () =>
            {
                if (_selected == null) return;
                bg.gameObject.SetActive(false);
                Close();
                _onConnect?.Invoke(new LaunchRequest { Mode = SessionMode.Client, Address = _selected.Address, Port = _selected.Port });
            }, 54f);

            bg.gameObject.SetActive(false);
            _infoModal = bg;
        }

        void OpenServerInfo()
        {
            if (_selected == null) return;
            _infoHeader.SetText(_selected.Name);
            for (int i = _infoPlayersList.childCount - 1; i >= 0; i--)
                Destroy(_infoPlayersList.GetChild(i).gameObject);
            if (_selected.ActivePlayers != null)
            {
                foreach (var p in _selected.ActivePlayers)
                    UiKit.Label(_infoPlayersList, p, 20, TextAnchor.MiddleRight, 28);
            }
            _infoPasswordInput.transform.parent.gameObject.SetActive(_selected.HasPassword);
            OpenSubModal(_infoModal);
        }

        static void OpenSubModal(RectTransform sub)
        {
            sub.gameObject.SetActive(true);
            sub.SetAsLastSibling();
        }

        public void Open(Selectable returnFocus)
        {
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshList();
            UiFocus.PushDialog(_tabs[0]);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
        }
    }
}
