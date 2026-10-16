using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class GraphicsTests
    {
        [TestMethod]
        public void Presets_set_the_effects_and_the_frame_rate()
        {
            var potato = Graphics.Look(GfxPreset.Potato);
            var balanced = Graphics.Look(GfxPreset.Balanced);
            var pretty = Graphics.Look(GfxPreset.Pretty);
            Assert.IsFalse(potato.Effects); Assert.AreEqual(30, potato.Fps);
            Assert.IsFalse(balanced.Effects); Assert.AreEqual(60, balanced.Fps);
            Assert.IsTrue(pretty.Effects); Assert.AreEqual(60, pretty.Fps);
        }

        [TestMethod]
        public void A_profile_keeps_its_preset_and_one_without_follows_low_spec()
        {
            var settings = new LauncherSettings { LowSpec = true };
            var p = new Profile();
            Assert.IsFalse(Graphics.EffectsFor(p, settings));       // nothing picked: Low spec decides
            settings.LowSpec = false;
            Assert.IsTrue(Graphics.EffectsFor(p, settings));
            Graphics.Choose(p, GfxPreset.Potato);
            Assert.AreEqual("potato", p.GfxPreset);
            Assert.AreEqual(30, p.Fps);
            Assert.IsFalse(Graphics.EffectsFor(p, settings));       // the preset wins over the setting
            Assert.AreEqual(GfxPreset.Pretty, Graphics.Parse("Pretty"));
            Assert.IsNull(Graphics.Parse("ultra"));
        }

        [TestMethod]
        public void The_frame_rate_script_sets_the_fps_divisor()
        {
            Assert.AreEqual(1, Graphics.Divisor(60));
            Assert.AreEqual(2, Graphics.Divisor(30));
            // 60 without the fps addon ticked: load it, set the divisor, hide the counter nobody asked for
            CollectionAssert.AreEqual(new[] { "/addon load fps", "/wait 2", "/fps 1", "/fps hide" }, Graphics.ScriptLines(60, false).Skip(1).ToList());
            // ticked by the player: their counter stays
            CollectionAssert.AreEqual(new[] { "/wait 2", "/fps 1" }, Graphics.ScriptLines(60, true).Skip(1).ToList());
            // 30 is the game's own rate, and nothing picked writes nothing
            Assert.AreEqual(1, Graphics.ScriptLines(30, false).Count);
            Assert.AreEqual(1, Graphics.ScriptLines(0, false).Count);
        }

        [TestMethod]
        public void The_window_opens_on_the_chosen_display()
        {
            var right = new MonitorInfo { Left = 1920, Top = 0, Width = 2560, Height = 1440 };
            Assert.AreEqual(Tuple.Create(1920, 0), Graphics.StartFor(right, WindowMode.Borderless, 2560, 1440));
            Assert.AreEqual(Tuple.Create(1920 + 320, 180), Graphics.StartFor(right, WindowMode.Windowed, 1920, 1080));
            Assert.AreEqual(Tuple.Create(-1, -1), Graphics.StartFor(null, WindowMode.Windowed, 1920, 1080));
        }

        [TestMethod]
        public void The_startup_script_runs_the_graphics_script()
        {
            var text = ScriptWriter.Build(new List<ScriptEntry>(), "");
            Assert.IsTrue(text.Contains("/exec " + Graphics.ScriptName));
            Assert.IsTrue(text.IndexOf("/wait 3") < text.IndexOf("/exec " + Graphics.ScriptName));
        }

        [TestMethod]
        public void Profile_keeps_graphics_display_and_window_position()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-gfx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "vanadreams.ini");
                File.WriteAllText(path, "[ashita.launcher]\nname = Vanadreams\n\n[ashita.boot]\ncommand = --server vanadreams.fairywitch.ca\n");
                var p = Profile.Load(path);
                Assert.AreEqual("", p.GfxPreset);
                Graphics.Choose(p, GfxPreset.Balanced);
                p.Monitor = @"\\.\DISPLAY2";
                p.StartX = 1920; p.StartY = 0;
                p.Save();
                var again = Profile.Load(path);
                Assert.AreEqual("balanced", again.GfxPreset);
                Assert.AreEqual(60, again.Fps);
                Assert.AreEqual(@"\\.\DISPLAY2", again.Monitor);
                Assert.AreEqual(1920, again.StartX);
                StringAssert.Contains(File.ReadAllText(path), "[ashita.window.startpos]");
            }
            finally { Directory.Delete(dir, true); }
        }
    }

    [TestClass]
    public class BugReportTests
    {
        private string _root;

        [TestInitialize]
        public void Make() => _root = Path.Combine(Path.GetTempPath(), "vdl-bug-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Clean() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestMethod]
        public void Passwords_are_taken_out_of_command_lines_and_keys()
        {
            Assert.AreEqual("command = --server x --user ferrin --pass ********", BugReport.StripPasswords("command = --server x --user ferrin --pass hunter2"));
            Assert.AreEqual("--password ******** --hairpin", BugReport.StripPasswords("--password \"two words\" --hairpin"));
            Assert.AreEqual("password = ********", BugReport.StripPasswords("password = hunter2"));
            Assert.AreEqual("polplugins = 1", BugReport.StripPasswords("polplugins = 1"));
        }

        [TestMethod]
        public void The_zip_holds_logs_and_profiles_and_no_password_or_login_store()
        {
            var data = Path.Combine(_root, "data");
            var ashita = Path.Combine(_root, "ashita");
            var boot = Path.Combine(ashita, "config", "boot");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(boot);
            Directory.CreateDirectory(Path.Combine(ashita, "logs"));
            Directory.CreateDirectory(Path.Combine(ashita, "crashdumps"));
            File.WriteAllText(Path.Combine(data, "launcher.log"), "launched with --pass hunter2\nall fine\n");
            File.WriteAllText(Path.Combine(data, "settings.json"), "{\"ashitaRoot\":\"x\"}");
            File.WriteAllText(Path.Combine(data, "credentials.dat"), "{\"vanadreams\":{\"user\":\"ferrin\",\"password\":\"hunter2\"}}");
            File.WriteAllText(Path.Combine(boot, "vanadreams.ini"), "[ashita.boot]\ncommand = --server vanadreams.fairywitch.ca --pass hunter2\n");
            File.WriteAllText(Path.Combine(boot, ".launch-vanadreams-1234.ini"), "[ashita.boot]\ncommand = --server x --user ferrin --pass hunter2\n");
            File.WriteAllText(Path.Combine(ashita, "logs", "ashita-2026-10-16.log"), "loaded fps");
            File.WriteAllBytes(Path.Combine(ashita, "crashdumps", "pol-1.dmp"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(ashita, "crashdumps", "pol-big.dmp"), new byte[BugReport.MaxDump + 1]);

            var zip = Path.Combine(_root, "out", "Vanadreams-bugreport-2026-10-16.zip");
            var names = BugReport.Build(zip, data, ashita, null, "0.2.41", new DateTime(2026, 10, 16, 12, 0, 0));

            CollectionAssert.Contains(names, "launcher/launcher.log");
            CollectionAssert.Contains(names, "launcher/settings.json");
            CollectionAssert.Contains(names, "ashita/config/boot/vanadreams.ini");
            CollectionAssert.Contains(names, "ashita/logs/ashita-2026-10-16.log");
            CollectionAssert.Contains(names, "ashita/crashdumps/pol-1.dmp");
            CollectionAssert.Contains(names, "about.txt");
            Assert.IsFalse(names.Any(n => n.Contains("credentials")), "the login store never goes in");
            Assert.IsFalse(names.Any(n => n.Contains(".launch-")), "launch copies carry a login and never go in");
            Assert.IsFalse(names.Contains("ashita/crashdumps/pol-big.dmp"), "a dump too big for Discord is left out");

            using (var z = ZipFile.OpenRead(zip))
                foreach (var e in z.Entries.Where(e => !e.Name.EndsWith(".dmp")))
                    using (var r = new StreamReader(e.Open()))
                        Assert.IsFalse(r.ReadToEnd().Contains("hunter2"), e.FullName + " holds a password");
        }
    }

    [TestClass]
    public class TimestampDefaultTests
    {
        private static string FindCatalog()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VanadreamsLauncher", "assets", "catalog.json"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "the bundled catalogue was not found above the test folder");
            return Path.Combine(dir.FullName, "VanadreamsLauncher", "assets", "catalog.json");
        }

        [TestMethod]
        public void Chat_timestamps_are_given_once_to_new_and_untouched_players()
        {
            var cat = Catalog.Parse(File.ReadAllText(FindCatalog()));
            var item = cat.Find("timestamp");
            Assert.IsNotNull(item);
            Assert.IsTrue(item.OnByDefault);
            // a new install, and a player who never had it given: it is ticked for them
            Assert.IsTrue(AddonInstaller.DefaultsToGive(cat, new List<string>()).Any(i => i.Id == "timestamp"));
            Assert.IsTrue(AddonInstaller.DefaultsToGive(cat, new List<string> { "vanatunes" }).Any(i => i.Id == "timestamp"));
            // given once: unticking it afterwards sticks
            Assert.IsFalse(AddonInstaller.DefaultsToGive(cat, new List<string> { "timestamp" }).Any(i => i.Id == "timestamp"));
        }
    }
}
