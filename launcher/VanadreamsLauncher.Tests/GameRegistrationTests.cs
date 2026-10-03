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
        public void A_root_has_a_game_only_with_FFXiMain_and_PlayOnlineViewer()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(ClientInstall.GameFolder(dir));
                Assert.IsFalse(GameRegistration.HasGame(dir));
                File.WriteAllText(Path.Combine(ClientInstall.GameFolder(dir), "FFXiMain.dll"), "");
                Assert.IsFalse(GameRegistration.HasGame(dir));
                Directory.CreateDirectory(ClientInstall.ViewerFolder(dir));
                Assert.IsTrue(GameRegistration.HasGame(dir));
                Assert.IsFalse(GameRegistration.HasGame(null));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
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
