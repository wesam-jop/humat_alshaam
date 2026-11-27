using System.Collections.Generic;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Screen 23: Two-team Tactical Scoreboard (Tab key) matching the gold-and-slate HUD layout.</summary>
    public sealed class ScoreboardModal : MonoBehaviour
    {
        public struct ScoreEntry
        {
            public string Name;
            public int Kills, Deaths, Stars, Points;
            public bool IsLocal;
        }

        const float TableW = 1120f, RankW = 78f, NameW = 510f, StatW = 118f, PtsW = 158f;
        RectTransform _defRows, _oppRows;
        ArabicText _defHeader, _oppHeader;
        int _defScore = 10, _oppScore = 7;
        List<ScoreEntry> _customDef, _customOpp;

        public bool IsOpen => gameObject.activeSelf;

        public static ScoreboardModal Create(Transform parent)
        {
            var root = UiKit.NewRect("ScoreboardModal", parent);
            UiKit.Stretch(root);
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = TacticalHudArt.OverlayBackdrop;
            blocker.raycastTarget = false;

            var modal = root.gameObject.AddComponent<ScoreboardModal>();
            TacticalHudArt.CreateTopLeftBrand(root, new Vector2(24f, -18f));
            TacticalHudArt.CreateModalTitle(root, Strings.Get("ingame.score.title"), -26f);

            var stack = UiKit.NewRect("TablesStack", root);
            UiKit.Anchor(stack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f));
            stack.sizeDelta = new Vector2(TableW, 610f);
            var v = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 20f;
            v.childControlWidth = true;
            v.childControlHeight = false;

            modal._defRows = BuildTable(stack, "DefendersTable", TacticalHudArt.ScoreHeaderGold,
                TacticalHudArt.GoldSoft, TacticalHudArt.GoldBright, out modal._defHeader);
            modal._oppRows = BuildTable(stack, "OpponentsTable", TacticalHudArt.ScoreHeaderSlate,
                new Color32(0xE6, 0xF0, 0xF8, 0xFF), new Color32(0xD8, 0xE8, 0xF4, 0xFF), out modal._oppHeader);

            root.gameObject.SetActive(false);
            return modal;
        }

        static RectTransform BuildTable(Transform parent, string name, Sprite hdrSprite, Color txtCol, Color accCol, out ArabicText hdrLbl)
        {
            var bg = MenuStyle.Shape(parent, name, TacticalHudArt.ScoreTableBg);
            UiKit.Fit(bg.rectTransform, 290f);
            var v = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(1, 1, 1, 1);
            v.childControlWidth = true;
            v.childControlHeight = false;

            var hdr = MenuStyle.Shape(bg.transform, "Header", hdrSprite);
            UiKit.Fit(hdr.rectTransform, 54f);
            var h = hdr.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(22, 16, 4, 4);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;

            hdrLbl = UiKit.Label(hdr.transform, "", 28, TextAnchor.MiddleLeft, 46f);
            hdrLbl.Target.color = txtCol;
            hdrLbl.Target.fontStyle = FontStyle.Bold;
            hdrLbl.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            TacticalHudArt.AddShadow(hdrLbl.Target);
            Fix((RectTransform)hdrLbl.transform, RankW + NameW - 22f, 46f);

            AddHdrIcon(hdr.transform, "Kills", TacticalHudArt.SkullIcon, TacticalHudArt.Cream, StatW);
            AddHdrIcon(hdr.transform, "Deaths", TacticalHudArt.TombstoneIcon, TacticalHudArt.Cream, StatW);
            AddHdrIcon(hdr.transform, "Stars", TacticalHudArt.StarIcon, accCol, StatW);
            AddHdrIcon(hdr.transform, "Points", TacticalHudArt.TrophyIcon, accCol, PtsW);

            var rows = UiKit.NewRect("Rows", bg.transform);
            UiKit.Fit(rows, 234f);
            var rv = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.childControlWidth = true;
            rv.childControlHeight = false;
            return rows;
        }

        static void AddHdrIcon(Transform parent, string name, Sprite sp, Color tint, float w)
        {
            var cell = UiKit.NewRect(name, parent);
            Fix(cell, w, 44f);
            TacticalHudArt.Icon(cell, "Img", sp, new Vector2(28f, 28f), tint);
        }

        static void Fix(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            le.flexibleWidth = le.flexibleHeight = 0f;
        }

        public void SetScores(int defScore, int oppScore, IReadOnlyList<ScoreEntry> defenders, IReadOnlyList<ScoreEntry> opponents)
        {
            _defScore = defScore;
            _oppScore = oppScore;
            _customDef = defenders != null ? new List<ScoreEntry>(defenders) : null;
            _customOpp = opponents != null ? new List<ScoreEntry>(opponents) : null;
        }

        public void Refresh(GameSession session)
        {
            _defHeader.SetText($"{_defScore}  {Strings.Get("ingame.team.defenders")}");
            _oppHeader.SetText($"{_oppScore}  {Strings.Get("ingame.team.opponents")}");
            Clear(_defRows);
            Clear(_oppRows);

            string local = string.IsNullOrWhiteSpace(TacticalSettings.Current.PlayerName)
                ? Strings.Format("ingame.score.player_fallback", 1) : TacticalSettings.Current.PlayerName;
            string tag = Strings.Get("ingame.score.player_tag");

            var def = _customDef ?? BuildDefaults(true, $"{local} {tag}", session);
            var opp = _customOpp ?? BuildDefaults(false, null, session);
            Populate(_defRows, def);
            Populate(_oppRows, opp);
        }

        static List<ScoreEntry> BuildDefaults(bool defenders, string localLabel, GameSession session)
        {
            var list = defenders ? new List<ScoreEntry>
            {
                new ScoreEntry { Name = localLabel, Kills = 24, Deaths = 8, Stars = 3, Points = 4800, IsLocal = true },
                new ScoreEntry { Name = "أسد", Kills = 19, Deaths = 12, Stars = 1, Points = 3900 },
                new ScoreEntry { Name = "رامي", Kills = 16, Deaths = 15, Stars = 2, Points = 3500 },
                new ScoreEntry { Name = "أبو العز", Kills = 14, Deaths = 14, Stars = 0, Points = 2900 },
                new ScoreEntry { Name = "قنّاص_99", Kills = 11, Deaths = 18, Stars = 0, Points = 2200 },
            } : new List<ScoreEntry>
            {
                new ScoreEntry { Name = "معتز", Kills = 18, Deaths = 14, Stars = 1, Points = 3700 },
                new ScoreEntry { Name = "مجهول_123", Kills = 15, Deaths = 16, Stars = 0, Points = 3100 },
                new ScoreEntry { Name = "قنّاص_الساحل", Kills = 13, Deaths = 15, Stars = 1, Points = 2800 },
                new ScoreEntry { Name = "طارق", Kills = 12, Deaths = 17, Stars = 0, Points = 2500 },
                new ScoreEntry { Name = "ناصر", Kills = 9, Deaths = 21, Stars = 0, Points = 1800 },
            };
            if (session != null && session.PlayerCount > 1)
            {
                int slot = defenders ? 1 : 0;
                for (int i = defenders ? 1 : 2; i < session.PlayerCount && slot < list.Count; i += 2)
                {
                    var e = list[slot];
                    e.Name = Strings.Format("ingame.score.player_fallback", i + 1);
                    list[slot++] = e;
                }
            }
            return list;
        }

        static void Populate(RectTransform parent, List<ScoreEntry> list)
        {
            for (int i = 0; i < Mathf.Min(list.Count, 5); i++)
            {
                if (i > 0)
                {
                    var line = UiKit.NewRect("Divider", parent);
                    var li = line.gameObject.AddComponent<Image>();
                    li.color = TacticalHudArt.GoldLine;
                    li.raycastTarget = false;
                    UiKit.Fit(line, 1f);
                }
                AddRow(parent, i + 1, list[i]);
            }
        }

        static void AddRow(Transform parent, int rank, ScoreEntry e)
        {
            RectTransform rt;
            if (e.IsLocal)
            {
                var hl = MenuStyle.Shape(parent, "LocalRow", TacticalHudArt.ScoreRowHighlight);
                hl.raycastTarget = false;
                rt = hl.rectTransform;
            }
            else
            {
                rt = UiKit.NewRect("PlayerRow", parent);
                var bg = rt.gameObject.AddComponent<Image>();
                bg.color = (rank % 2 == 0) ? new Color(0.11f, 0.14f, 0.18f, 0.24f) : new Color(0f, 0f, 0f, 0.08f);
                bg.raycastTarget = false;
            }
            UiKit.Fit(rt, 45f);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(0, 16, 0, 0);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;

            Color col = e.IsLocal ? TacticalHudArt.GoldBright : TacticalHudArt.Cream;
            Cell(rt, rank.ToString(), RankW, col, e.IsLocal);

            var nm = UiKit.Label(rt, e.Name, 22, TextAnchor.MiddleLeft, 44f);
            nm.Target.color = col;
            nm.Target.fontStyle = e.IsLocal ? FontStyle.Bold : FontStyle.Normal;
            nm.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            Fix((RectTransform)nm.transform, NameW, 44f);

            Cell(rt, e.Kills.ToString(), StatW, col, e.IsLocal);
            Cell(rt, e.Deaths.ToString(), StatW, col, e.IsLocal);
            Cell(rt, e.Stars.ToString(), StatW, col, e.IsLocal);
            Cell(rt, e.Points.ToString(), PtsW, col, e.IsLocal);
        }

        static void Cell(Transform parent, string val, float w, Color col, bool bold)
        {
            var t = UiKit.LtrLabel(parent, val, 22, TextAnchor.MiddleCenter);
            t.color = col;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            Fix((RectTransform)t.transform, w, 44f);
        }

        static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }
    }
}
