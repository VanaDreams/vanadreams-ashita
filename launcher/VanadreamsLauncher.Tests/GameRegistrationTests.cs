using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class GameRegistrationTests
    {
        private const string Ours = @"D:\Games\Vanadreams";
        private const string Old = @"C:\Program Files (x86)\PlayOnline\SquareEnix";

        private static RegisteredGame RegisteredAt(string root, params string[] comRoots)
        {
            var reg = new RegisteredGame
            {
                GameFolder = ClientInstall.GameFolder(root),
                ViewerFolder = ClientInstall.ViewerFolder(root),
            };
            foreach (var r in comRoots.Length == 0 ? new[] { root } : comRoots)
            {
                reg.ComServers.Add(Path.Combine(r, @"FINAL FANTASY XI\FFXi.dll"));
                reg.ComServers.Add(Path.Combine(r, @"PlayOnlineViewer\viewer\com\polcore.dll"));
            }
            return reg;
        }

        [TestMethod]
        public void Points_at_a_root_only_when_folders_and_COM_servers_all_agree()
        {
            Assert.IsTrue(GameRegistration.PointsAt(RegisteredAt(Ours), Ours));
            Assert.IsTrue(GameRegistration.PointsAt(RegisteredAt(Ours), Ours + "\\"));
            Assert.IsFalse(GameRegistration.PointsAt(RegisteredAt(Old), Ours));

            // the folders say ours, but the game xiloader creates is the old copy's: that is the copy that runs
            Assert.IsFalse(GameRegistration.PointsAt(RegisteredAt(Ours, Old), Ours));

            var viewerElsewhere = RegisteredAt(Ours);
            viewerElsewhere.ViewerFolder = ClientInstall.ViewerFolder(Old);
            Assert.IsFalse(GameRegistration.PointsAt(viewerElsewhere, Ours));

            // no COM servers readable: the folders decide
            var foldersOnly = RegisteredAt(Ours);
            foldersOnly.ComServers.Clear();
            Assert.IsTrue(GameRegistration.PointsAt(foldersOnly, Ours));

            Assert.IsFalse(GameRegistration.PointsAt(new RegisteredGame(), Ours));
            Assert.IsFalse(GameRegistration.PointsAt(RegisteredAt(Ours), null));
        }

        [TestMethod]
        public void A_root_is_not_confused_with_a_folder_whose_name_starts_the_same()
        {
            Assert.IsFalse(GameRegistration.IsUnder(@"D:\Games\Vanadreams2\FINAL FANTASY XI\FFXi.dll", Ours));
            Assert.IsTrue(GameRegistration.IsUnder(@"d:\games\vanadreams\FINAL FANTASY XI\FFXi.dll", Ours));
        }

        [TestMethod]
        public void The_current_copy_is_the_one_whose_game_COM_server_is_registered()
        {
            Assert.AreEqual(Old, GameRegistration.CurrentRoot(RegisteredAt(Ours, Old)));
            var foldersOnly = RegisteredAt(Ours);
            foldersOnly.ComServers.Clear();
            Assert.AreEqual(Ours, GameRegistration.CurrentRoot(foldersOnly));
            Assert.IsNull(GameRegistration.CurrentRoot(new RegisteredGame()));
        }

        [TestMethod]
        public void A_picked_game_folder_or_its_parent_both_give_the_root()
        {
            Assert.AreEqual(Old, GameRegistration.RootOf(Old + @"\FINAL FANTASY XI"));
            Assert.AreEqual(Old, GameRegistration.RootOf(Old + @"\final fantasy xi\"));
            Assert.AreEqual(Old, GameRegistration.RootOf(Old + "\\"));
            Assert.IsNull(GameRegistration.RootOf(" "));
        }

        [TestMethod]
        public void A_game_at_the_top_of_a_drive_keeps_the_drive_backslash()
        {
            // "D:" alone means "the current folder on D:", so every path built on it came out as D:FINAL FANTASY XI
            // and the game closed four seconds after Play (6 Oct 2026, an install picked straight into D:\).
            Assert.AreEqual(@"D:\", GameRegistration.RootOf(@"D:\FINAL FANTASY XI"));
            Assert.AreEqual(@"D:\", GameRegistration.RootOf(@"D:\FINAL FANTASY XI\"));
            Assert.AreEqual(@"D:\", GameRegistration.RootOf(@"D:\"));
            Assert.AreEqual(@"D:\", GameRegistration.RootOf("D:"));
            var reg = new RegisteredGame { GameFolder = @"D:\FINAL FANTASY XI\", ViewerFolder = @"D:\PlayOnlineViewer" };
            reg.ComServers.Add(@"D:\FINAL FANTASY XI\FFXi.dll");
            Assert.AreEqual(@"D:\", GameRegistration.CurrentRoot(reg));
            Assert.IsTrue(GameRegistration.PointsAt(reg, @"D:\"));
            // what the broken build wrote: Play must see it as wrong, so it registers the game again
            var broken = new RegisteredGame { GameFolder = @"D:FINAL FANTASY XI\", ViewerFolder = "D:PlayOnlineViewer" };
            Assert.IsFalse(GameRegistration.PointsAt(broken, @"D:\"));
        }

        [TestMethod]
        public void A_root_has_a_game_only_when_both_things_xiloader_creates_are_on_disk()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            try
            {
                var game = ClientInstall.GameFolder(dir);
                var com = Path.Combine(ClientInstall.ViewerFolder(dir), "viewer", "com");
                Directory.CreateDirectory(game);
                Assert.IsFalse(GameRegistration.HasGame(dir));
                File.WriteAllText(Path.Combine(game, "FFXiMain.dll"), "");
                File.WriteAllText(Path.Combine(game, "FFXi.dll"), "");
                Assert.IsFalse(GameRegistration.HasGame(dir));
                // an install stopped before its last part: the viewer folder is there and PlayOnline is not
                Directory.CreateDirectory(com);
                Assert.IsFalse(GameRegistration.HasGame(dir));
                File.WriteAllText(Path.Combine(com, "polcore.dll"), "");
                Assert.IsTrue(GameRegistration.HasGame(dir));
                Assert.IsFalse(GameRegistration.HasGame(null));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        private const string Third = @"E:\Other\SquareEnix";

        private static string Chosen(string registered, string[] roots, string[] games, string[] fitting) =>
            GameRegistration.Choose(registered, roots,
                r => Array.Exists(games, g => GameRegistration.SameFolder(g, r)),
                r => Array.Exists(fitting, g => GameRegistration.SameFolder(g, r)));

        [TestMethod]
        public void A_first_install_starts_the_copy_the_launcher_installed()
        {
            // registered by the install itself
            Assert.AreEqual(Ours, Chosen(Ours, new[] { Ours }, new[] { Ours }, new[] { Ours }));
            // the permission prompt was refused, so Windows has nothing yet
            Assert.AreEqual(Ours, Chosen("", new[] { "", Ours }, new[] { Ours }, new[] { Ours }));
        }

        [TestMethod]
        public void An_older_copy_kept_for_another_server_gives_way_to_the_one_that_fits()
        {
            Assert.AreEqual(Ours, Chosen(Old, new[] { Ours }, new[] { Ours, Old }, new[] { Ours }));
        }

        [TestMethod]
        public void A_game_that_works_is_not_taken_away_for_a_copy_that_is_not_whole()
        {
            // the launcher's folder holds an install that never finished; the game has been starting from another copy
            Assert.AreEqual(Old, Chosen(Old, new[] { Ours }, new[] { Old }, new[] { Old }));
            // both fit: the one Windows already starts stays, and nobody is asked for permission
            Assert.AreEqual(Old, Chosen(Old, new[] { Ours }, new[] { Ours, Old }, new[] { Ours, Old }));
        }

        [TestMethod]
        public void When_nothing_fits_or_the_version_is_unknown_the_registered_copy_is_started_and_nothing_is_refused()
        {
            Assert.AreEqual(Old, Chosen(Old, new[] { Ours, Third }, new[] { Ours, Old, Third }, new string[0]));
            Assert.AreEqual(Ours, Chosen("", new[] { Ours, Third }, new[] { Ours, Third }, new string[0]));
            // the registered folder is gone from disk: the first copy that is a game
            Assert.AreEqual(Third, Chosen(Old, new[] { Ours, Third }, new[] { Third }, new string[0]));
            Assert.IsNull(Chosen(Old, new[] { Ours }, new string[0], new string[0]));
            Assert.IsNull(Chosen(null, null, new[] { Ours }, new[] { Ours }));
        }

        [TestMethod]
        public void A_copy_fits_when_its_month_is_the_servers_or_newer_and_never_when_either_is_unknown()
        {
            Assert.IsTrue(ClientVersion.Fits("30260904_1", "30260904_1", VersionLock.Exact));
            Assert.IsFalse(ClientVersion.Fits("30190305_0", "30260904_1", VersionLock.Exact));
            Assert.IsTrue(ClientVersion.Fits("30261002_0", "30260904_1", VersionLock.MatchingOrNewer));
            Assert.IsFalse(ClientVersion.Fits("30261002_0", "30260904_1", VersionLock.Exact));
            // lock off: the server takes anything, and an old game is still the wrong one to pick
            Assert.IsFalse(ClientVersion.Fits("30190305_0", "30260904_1", VersionLock.Off));
            Assert.IsTrue(ClientVersion.Fits("30260904_1", "30260904_1", VersionLock.Off));
            Assert.IsFalse(ClientVersion.Fits(null, "30260904_1", VersionLock.Exact));
            Assert.IsFalse(ClientVersion.Fits("30260904_1", "", VersionLock.Exact));
        }

        [TestMethod]
        public void Vanadreams_profiles_are_told_apart_from_other_servers_and_retail()
        {
            Assert.IsTrue(LoaderCommand.Parse("--server vanadreams.fairywitch.ca").IsVanadreams);
            Assert.IsTrue(LoaderCommand.Parse("--server " + LoaderCommand.TailscaleServer + " --hairpin").IsVanadreams);
            Assert.IsFalse(LoaderCommand.Parse("--server homepointxi.com").IsVanadreams);
            Assert.IsFalse(LoaderCommand.Parse("").IsVanadreams);

            var retail = new Profile { Command = LoaderCommand.Parse("/game eAZcFcB") };
            Assert.IsFalse(retail.IsVanadreams);
        }

        [TestMethod]
        public void A_profile_keeps_its_game_folder_and_settings_keep_the_copy_taken_over()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var ini = Path.Combine(dir, "other.ini");
                File.WriteAllText(ini, "[ashita.launcher]\r\nname = Other\r\n\r\n[ashita.boot]\r\ncommand = --server homepointxi.com\r\n");
                var p = Profile.Load(ini);
                Assert.AreEqual("", p.GameFolder);
                p.GameFolder = Old + @"\FINAL FANTASY XI";
                p.Save();
                Assert.AreEqual(Old + @"\FINAL FANTASY XI", Profile.Load(ini).GameFolder);

                var untouched = Path.Combine(dir, "plain.ini");
                File.WriteAllText(untouched, "[ashita.boot]\r\ncommand = --server vanadreams.fairywitch.ca\r\n");
                Profile.Load(untouched).Save();
                StringAssert.DoesNotMatch(File.ReadAllText(untouched), new System.Text.RegularExpressions.Regex(@"(?m)^game\s*="));

                var settingsPath = Path.Combine(dir, "settings.json");
                var s = LauncherSettings.Load(settingsPath);
                s.OtherGameRoot = Old;
                s.Save();
                Assert.AreEqual(Old, LauncherSettings.Load(settingsPath).OtherGameRoot);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
