using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class SwitcherTests
    {
        [TestMethod]
        public void KeyBinding_ReadsBackWhatItWrites()
        {
            var b = KeyBinding.Parse("Ctrl+Shift+F1");
            Assert.IsNotNull(b);
            Assert.AreEqual(Keys.F1, b.Key);
            Assert.IsTrue(b.Ctrl && b.Shift && !b.Alt);
            Assert.AreEqual("Ctrl+Shift+F1", b.ToString());
            Assert.AreEqual("Ctrl+Shift+F1", KeyBinding.Parse(b.ToString()).ToString());
            Assert.IsTrue(b.Matches((int)Keys.F1, true, false, true));
            Assert.IsFalse(b.Matches((int)Keys.F1, false, false, true), "the modifiers must match too");
        }

        [TestMethod]
        public void KeyBinding_BlankOrUnreadableIsOff()
        {
            Assert.IsNull(KeyBinding.Parse(""));
            Assert.IsNull(KeyBinding.Parse(null));
            Assert.IsNull(KeyBinding.Parse("Ctrl+"));
            Assert.IsNull(KeyBinding.Parse("Hyper+F1"));
            Assert.IsNull(KeyBinding.Parse("NotAKey"));
            Assert.IsNull(KeyBinding.Parse("Ctrl"), "a modifier alone is not a key");
            Assert.IsNull(KeyBinding.Parse("1"), "a number would be read as a mouse button");
            Assert.AreEqual("none", KeyBinding.Words(""));
        }

        [TestMethod]
        public void KeyBinding_WordsAreTheOnesOnTheKeyboard()
        {
            Assert.AreEqual("Scroll Lock", KeyBinding.Words("Scroll"));
            Assert.AreEqual(Keys.Scroll, KeyBinding.Parse("Scroll Lock").Key);
            Assert.AreEqual("Alt+1", KeyBinding.Words("Alt+D1"));
            Assert.AreEqual("Num 4", KeyBinding.Words("NumPad4"));
            Assert.AreEqual("Pause", KeyBinding.Words(new LauncherSettings().SwitchNext));
        }

        [TestMethod]
        public void PadBinding_FiresOncePerPress()
        {
            var chord = PadBinding.Parse("Back+RB");
            Assert.AreEqual(PadBinding.Back | PadBinding.RB, chord);
            Assert.IsFalse(PadBinding.Fired(chord, 0, PadBinding.Back), "half the chord");
            Assert.IsTrue(PadBinding.Fired(chord, PadBinding.Back, (ushort)(PadBinding.Back | PadBinding.RB)));
            Assert.IsFalse(PadBinding.Fired(chord, chord, chord), "held: no repeat");
            Assert.IsTrue(PadBinding.Fired(chord, 0, (ushort)(chord | PadBinding.A)), "other buttons down as well still count");
            Assert.IsFalse(PadBinding.Fired(0, 0, 0xFFFF), "off never fires");
            Assert.AreEqual(0, PadBinding.Parse("Back+Turbo"));
            Assert.AreEqual("Off", PadBinding.Words(""));
            Assert.AreEqual("Back + RB", PadBinding.Words("Back+RB"));
            foreach (var c in PadBinding.Choices.Skip(1)) Assert.AreNotEqual(0, PadBinding.Parse(c), c);
        }

        [TestMethod]
        public void NameFromTitle_FindsTheCharacter()
        {
            Assert.AreEqual("Terry", SwitchRules.NameFromTitle("Terry"));
            Assert.AreEqual("Terry", SwitchRules.NameFromTitle("Ashita v4 - Terry"));
            Assert.AreEqual("Ferrin", SwitchRules.NameFromTitle("Ferrin - FINAL FANTASY XI"));
            Assert.IsNull(SwitchRules.NameFromTitle("FINAL FANTASY XI"));
            Assert.IsNull(SwitchRules.NameFromTitle("Final Fantasy XI"));
            Assert.IsNull(SwitchRules.NameFromTitle("Ashita v4"));
            Assert.IsNull(SwitchRules.NameFromTitle("Ashita"));
            Assert.IsNull(SwitchRules.NameFromTitle(""));
            Assert.IsNull(SwitchRules.NameFromTitle(null));
        }

        private static GameWindow W(int h, string name = null) => new GameWindow { Handle = (IntPtr)h, Name = name };

        [TestMethod]
        public void Arrange_KeepsPlacesAndAddsNewAtTheEnd()
        {
            var current = new List<GameWindow> { W(2), W(1) };
            var found = new List<GameWindow> { W(1), W(3), W(2) };
            var list = SwitchRules.Arrange(current, found, null);
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, list.Select(w => (int)w.Handle).ToArray());
            // a closed window drops out
            list = SwitchRules.Arrange(list, new List<GameWindow> { W(3), W(2) }, null);
            CollectionAssert.AreEqual(new[] { 2, 3 }, list.Select(w => (int)w.Handle).ToArray());
        }

        [TestMethod]
        public void Arrange_FollowsTheSavedOrderByName()
        {
            var found = new List<GameWindow> { W(1, "Ferrin"), W(2), W(3, "Terry") };
            var list = SwitchRules.Arrange(new List<GameWindow>(), found, new List<string> { "terry", "Ferrin" });
            CollectionAssert.AreEqual(new[] { "Terry", "Ferrin", null }, list.Select(w => w.Name).ToArray());
        }

        [TestMethod]
        public void NextAndPrevious_WrapRound()
        {
            Assert.AreEqual(1, SwitchRules.Next(0, 3));
            Assert.AreEqual(0, SwitchRules.Next(2, 3));
            Assert.AreEqual(0, SwitchRules.Next(-1, 3));
            Assert.AreEqual(2, SwitchRules.Previous(0, 3));
            Assert.AreEqual(2, SwitchRules.Previous(-1, 3));
            Assert.AreEqual(-1, SwitchRules.Next(0, 0));
            Assert.AreEqual(-1, SwitchRules.Previous(0, 0));
        }

        [TestMethod]
        public void Layout_PlaceByNameThenBySlot()
        {
            var layout = new List<string>
            {
                SwitchRules.LayoutEntry(W(1, "Terry"), 0, 0, 0, 1280, 720),
                SwitchRules.LayoutEntry(W(2), 1, 1280, 0, 1280, 720),
                "garbage", "Bad|1|2|0|0",
            };
            Assert.AreEqual("Terry|0|0|1280|720", layout[0]);
            Assert.AreEqual("#2|1280|0|1280|720", layout[1]);
            CollectionAssert.AreEqual(new[] { 0, 0, 1280, 720 }, SwitchRules.PlaceFor(layout, W(9, "terry"), 4));
            CollectionAssert.AreEqual(new[] { 1280, 0, 1280, 720 }, SwitchRules.PlaceFor(layout, W(9, "Someone"), 1));
            Assert.IsNull(SwitchRules.PlaceFor(layout, W(9, "Someone"), 3));
            Assert.IsNull(SwitchRules.PlaceFor(layout, W(9, "Bad"), 3), "an entry with no size is skipped");
            Assert.IsNull(SwitchRules.PlaceFor(null, W(9, "Terry"), 0));
        }

        [TestMethod]
        public void Settings_KeepTheSwitcher()
        {
            var path = Path.Combine(Path.GetTempPath(), "vdl-switch-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var s = LauncherSettings.Load(path);
                Assert.IsFalse(s.SwitcherOn, "off until the player turns it on");
                Assert.AreEqual(LauncherSettings.SlotKeyCount, s.SwitchSlots.Count);
                s.SwitcherOn = true;
                s.SwitchSlots[2] = "Ctrl+F3";
                s.PadNext = "Back+RB";
                s.SwitchOrder = new List<string> { "Terry", "Ferrin" };
                s.Layouts["Full party"] = new List<string> { "Terry|0|0|800|600" };
                s.ActiveLayout = "Full party";
                s.AutoLayout = true;
                s.Save();

                var back = LauncherSettings.Load(path);
                Assert.IsTrue(back.SwitcherOn);
                Assert.AreEqual("Ctrl+F3", back.SwitchSlots[2]);
                Assert.AreEqual("Pause", back.SwitchNext);
                Assert.AreEqual("Back+RB", back.PadNext);
                CollectionAssert.AreEqual(new[] { "Terry", "Ferrin" }, back.SwitchOrder);
                CollectionAssert.AreEqual(new[] { "Terry|0|0|800|600" }, back.Layouts["full party"]);
                Assert.AreEqual("Full party", back.ActiveLayout);
                Assert.IsTrue(back.AutoLayout);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
