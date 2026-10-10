using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class ClientInstallTests
    {
        private const string Manifest = @"{
  ""schema"": 1, ""client_ver"": ""30260904_1"",
  ""create_dirs"": [""USER"", ""SYS"", ""TEMP""], ""viewer_create_dirs"": [""usr/all"", ""tmp""],
  ""chunks"": [
    { ""name"": ""base.zip"", ""size"": 100, ""sha256"": ""aa"", ""files"": 3, ""unpacked"": 300 },
    { ""name"": ""ROM-01.zip"", ""size"": 200, ""sha256"": ""bb"", ""files"": 5, ""unpacked"": 400 },
    { ""name"": ""pol-viewer.zip"", ""size"": 50, ""sha256"": ""cc"", ""files"": 2, ""unpacked"": 90, ""dest"": ""PlayOnlineViewer"" }
  ]
}";

        [TestMethod]
        public void Manifest_ReadsChunksAndWhereEachGoes()
        {
            var m = ClientManifest.Parse(Manifest);
            Assert.AreEqual("30260904_1", m.Version);
            Assert.AreEqual(3, m.Chunks.Count);
            Assert.AreEqual(350, m.TotalSize);
            Assert.AreEqual(790, m.TotalUnpacked);
            Assert.AreEqual("FINAL FANTASY XI", m.Chunks[0].Dest);   // no dest means the game folder
            Assert.AreEqual("PlayOnlineViewer", m.Chunks[2].Dest);
            CollectionAssert.AreEqual(new[] { "usr/all", "tmp" }, m.ViewerCreateDirs);
        }

        [TestMethod]
        public void Manifest_WithNoChunksIsRefused()
        {
            Assert.ThrowsException<InvalidDataException>(() => ClientManifest.Parse(@"{ ""client_ver"": ""30260904_1"", ""chunks"": [] }"));
        }

        [TestMethod]
        public void Remaining_SkipsWhatTheRecordSaysIsDone_ButOnlyWithTheSameHash()
        {
            var root = Path.Combine(Path.GetTempPath(), "vdl-client-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllLines(Path.Combine(root, ClientInstall.RecordName), new[]
                {
                    "30260904_1 base.zip aa",      // done
                    "30260904_1 ROM-01.zip old",   // an older copy of this zip: not done
                    "30260805_0 pol-viewer.zip cc" // another version: not done
                });
                var m = ClientManifest.Parse(Manifest);
                var left = ClientInstall.Remaining(m, ClientInstall.Done(root, m.Version)).Select(c => c.Name).ToList();
                CollectionAssert.AreEqual(new[] { "ROM-01.zip", "pol-viewer.zip" }, left);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public void RegisterScript_WritesFoldersLanguageControllerAndAllNineServers()
        {
            var s = ClientInstall.RegisterScript(@"C:\Games\Vanadreams");
            StringAssert.Contains(s, @"/v 0001 /t REG_SZ /d ""C:\Games\Vanadreams\FINAL FANTASY XI\\"" /f /reg:32");
            StringAssert.Contains(s, @"/v 1000 /t REG_SZ /d ""C:\Games\Vanadreams\PlayOnlineViewer"" /f /reg:32");
            StringAssert.Contains(s, "/v Language /t REG_DWORD /d 1");
            // the controller is only added where the player has none
            StringAssert.Contains(s, @"reg query ""HKLM\SOFTWARE\PlayOnlineUS\SquareEnix\FinalFantasyXI"" /v padsin000 /reg:32 >nul 2>&1 || reg add");
            foreach (var dll in ClientInstall.ComServers) StringAssert.Contains(s, @"\SysWOW64\regsvr32.exe"" /s ""C:\Games\Vanadreams\" + dll + @"""");
            Assert.AreEqual(9, ClientInstall.ComServers.Length);
            StringAssert.Contains(s, "exit /b " + ClientInstall.RegisterSteps);
        }

        private static Registration Ours() => new Registration
        {
            GameFolder = @"C:\Games\Vanadreams\FINAL FANTASY XI\",
            ViewerFolder = @"C:\Games\Vanadreams\PlayOnlineViewer",
            FfxiDll = @"C:\Games\Vanadreams\FINAL FANTASY XI\FFXi.dll",
            PolcoreDll = @"C:\Games\Vanadreams\PlayOnlineViewer\viewer\com\polcore.dll",
        };

        [TestMethod]
        public void PointsAt_IsTrueOnlyWhenBothFoldersAndBothDllsAreOurs()
        {
            const string root = @"C:\Games\Vanadreams";
            const string old = @"C:\Program Files (x86)\PlayOnline\SquareEnix";
            Assert.IsTrue(ClientInstall.PointsAt(Ours(), root));

            var r = Ours(); r.GameFolder = old + @"\FINAL FANTASY XI\";
            Assert.IsFalse(ClientInstall.PointsAt(r, root));

            r = Ours(); r.ViewerFolder = old + @"\PlayOnlineViewer";
            Assert.IsFalse(ClientInstall.PointsAt(r, root));

            // folders right, the game's DLL still the old install's: the old game would run
            r = Ours(); r.FfxiDll = old + @"\FINAL FANTASY XI\FFXi.dll";
            Assert.IsFalse(ClientInstall.PointsAt(r, root));
            StringAssert.Contains(ClientInstall.FirstStray(r, root), "FFXi.dll");

            r = Ours(); r.PolcoreDll = null;
            Assert.IsFalse(ClientInstall.PointsAt(r, root));
        }

        [TestMethod]
        public void PointsAt_IgnoresCaseAndTrailingSlashes()
        {
            var r = new Registration
            {
                GameFolder = @"c:\games\vanadreams\final fantasy xi",
                ViewerFolder = @"C:\Games\Vanadreams\PlayOnlineViewer\",
                FfxiDll = @"c:\games\vanadreams\FINAL FANTASY XI\ffxi.dll",
                PolcoreDll = @"C:\GAMES\VANADREAMS\PlayOnlineViewer\viewer\com\polcore.dll",
            };
            Assert.IsTrue(ClientInstall.PointsAt(r, @"C:\Games\Vanadreams\"));
        }

        [TestMethod]
        public void PointsAt_IsNotFooledByAFolderWithTheSameStart()
        {
            var r = Ours();
            r.FfxiDll = @"C:\Games\Vanadreams\FINAL FANTASY XI old\FFXi.dll";
            Assert.IsFalse(ClientInstall.PointsAt(r, @"C:\Games\Vanadreams"));
        }

        [TestMethod]
        public void ReadRegistration_ReadsWhatThisPcHoldsWithoutThrowing()
        {
            var r = ClientInstall.ReadRegistration();
            Assert.IsNotNull(r);
            // a PC with the game registered names FFXi.dll under the class xiloader asks for
            if (r.FfxiDll != null) StringAssert.EndsWith(r.FfxiDll.ToLowerInvariant(), "ffxi.dll");
        }
    }
}
