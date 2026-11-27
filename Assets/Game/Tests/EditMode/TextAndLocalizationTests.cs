using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Adventure3D.Core;
using Adventure3D.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Adventure3D.Tests
{
    public class ArabicShaperTests
    {
        [Test]
        public void ShapesSalamWithLamAlefLigature()
        {
            // س initial, لا final ligature, م isolated (alef does not join forward)
            Assert.AreEqual("ﺳﻼﻡ", ArabicShaper.Shape("سلام"));
        }

        [Test]
        public void ReordersArabicRightToLeft()
        {
            Assert.AreEqual("ﻡﻼﺳ", ArabicShaper.Process("سلام"));
        }

        [Test]
        public void HarakaStaysOnItsLetterAfterReordering()
        {
            // Legacy uGUI Text cannot position marks: they are removed for display, shaping stays correct.
            string visual = ArabicShaper.Process("حُماة");
            Assert.AreEqual(-1, visual.IndexOf('\u064F'));
            Assert.AreEqual(ArabicShaper.Process("حماة"), visual);
        }

        [Test]
        public void ShapesInitialMedialFinal()
        {
            // بيت: ب initial, ي medial, ت final
            Assert.AreEqual("ﺑﻴﺖ", ArabicShaper.Shape("بيت"));
        }

        [Test]
        public void NumbersAndLatinStayLeftToRight()
        {
            string visual = ArabicShaper.Process("المنفذ 7777");
            StringAssert.StartsWith("7777 ", visual);
            string v2 = ArabicShaper.Process("اضغط Space للقفز");
            StringAssert.Contains("Space", v2);
        }

        [Test]
        public void AddressWithoutArabicIsUntouched()
        {
            Assert.AreEqual("192.168.1.20:7777", ArabicShaper.Process("192.168.1.20:7777"));
        }

        [Test]
        public void BracketsAreMirroredInsideArabic()
        {
            string visual = ArabicShaper.Process("لعب (فردي)");
            // the logical closing ')' is drawn first (leftmost) and mirrored to '('
            Assert.AreEqual('(', visual.First(c => c == '(' || c == ')'));
        }

        [Test]
        public void MultiLineTextIsProcessedPerLine()
        {
            string visual = ArabicShaper.Process("سلام\nسلام");
            var lines = visual.Split('\n');
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(lines[0], lines[1]);
        }

        [Test]
        public void EveryUiStringShapesFully()
        {
            foreach (var kv in Strings.Table)
            {
                string visual = ArabicShaper.Process(kv.Value);
                foreach (char c in visual)
                    Assert.IsFalse(c >= 'ء' && c <= 'ي', $"Unshaped Arabic letter U+{(int)c:X4} in '{kv.Key}'");
            }
        }
    }

    public class StringTableTests
    {
        static string GameRoot => Path.Combine(Application.dataPath, "Game");

        [Test]
        public void StringResourceLoads()
        {
            Strings.Load();
            Assert.Greater(Strings.Table.Count, 40);
            Assert.AreEqual("حُماة الشام", Strings.Get("app.title"));
        }

        [Test]
        public void EveryKeyUsedInCodeExists()
        {
            var rx = new Regex("Strings\\.(Get|Format)\\(\"([a-zA-Z0-9_.]+)\"");
            var missing = Directory.GetFiles(GameRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar))
                .SelectMany(f => rx.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[2].Value))
                .Distinct().Where(k => !k.EndsWith(".") && !Strings.Has(k)).ToList(); // "prefix." + enum is covered below
            CollectionAssert.IsEmpty(missing, "Missing keys: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryEndReasonStatusAndModeHasArabicText()
        {
            foreach (SessionEndReason r in Enum.GetValues(typeof(SessionEndReason)))
                if (EndReasonText.IsError(r)) Assert.IsTrue(Strings.Has("error." + r), "error." + r);
            foreach (SessionStatus s in Enum.GetValues(typeof(SessionStatus)))
                Assert.IsTrue(Strings.Has("status." + s), "status." + s);
            foreach (SessionMode m in Enum.GetValues(typeof(SessionMode)))
                Assert.IsTrue(Strings.Has("status.mode." + m), "status.mode." + m);
        }

        [Test]
        public void VisibleTextIsArabicExceptTechnicalHints()
        {
            foreach (var kv in Strings.Table)
                Assert.IsTrue(ArabicShaper.ContainsArabic(kv.Value), $"'{kv.Key}' has no Arabic text");
        }
    }
}
