using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class PlayCheckTests
    {
        [TestMethod]
        public void Missing_lists_only_what_is_gone()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "bootloader"));
            try
            {
                File.WriteAllBytes(Path.Combine(dir, "Ashita-cli.exe"), new byte[] { 0 });
                File.WriteAllBytes(Path.Combine(dir, "Ashita.dll"), new byte[] { 0 });
                var missing = PlayCheck.Missing(dir, @"bootloader\xiloader.exe");
                Assert.AreEqual(1, missing.Count);
                Assert.AreEqual("xiloader.exe", Path.GetFileName(missing[0]));
                File.WriteAllBytes(Path.Combine(dir, "bootloader", "xiloader.exe"), new byte[] { 0 });
                Assert.AreEqual(0, PlayCheck.Missing(dir, @"bootloader\xiloader.exe").Count);
            }
            finally { Directory.Delete(dir, true); }
            Assert.AreEqual(0, PlayCheck.Missing(null, "x.exe").Count);
        }

        [TestMethod]
        public void Missing_sentence_names_the_antivirus_and_the_folder()
        {
            var text = PlayCheck.MissingSentence(new[] { @"D:\Ashita\bootloader\xiloader.exe" }, new[] { "Windows Defender", "Norton 360" }, @"D:\Ashita");
            StringAssert.Contains(text, "xiloader.exe is missing");
            StringAssert.Contains(text, "Norton 360 most likely removed it");
            StringAssert.Contains(text, "Open Norton 360");
            StringAssert.Contains(text, @"D:\Ashita");
            var plain = PlayCheck.MissingSentence(new[] { "a.exe", "b.dll" }, new string[0], @"C:\A");
            StringAssert.Contains(plain, "a.exe, b.dll are missing");
            StringAssert.Contains(plain, "Your antivirus most likely removed them");
            Assert.IsNull(PlayCheck.MissingSentence(new string[0], null, @"C:\A"));
        }

        [TestMethod]
        public void Defender_is_not_counted_as_another_antivirus()
        {
            Assert.AreEqual(0, PlayCheck.OtherAntivirus(new[] { "Windows Defender" }).Count);
            Assert.AreEqual(0, PlayCheck.OtherAntivirus(new[] { "Microsoft Defender Antivirus", "" }).Count);
            CollectionAssert.AreEqual(new[] { "Norton 360" }, PlayCheck.OtherAntivirus(new[] { "Norton 360", "norton 360", "Windows Defender" }));
            Assert.AreEqual(0, PlayCheck.OtherAntivirus(null).Count);
        }

        [TestMethod]
        public void Reach_sentence_picks_the_cause()
        {
            var ok = new PlayCheck.Reach { Host = "h" };
            ok.Open.AddRange(PlayCheck.LoginPorts);
            Assert.IsNull(PlayCheck.ReachSentence(ok, false, ServerState.Online));
            Assert.IsNull(PlayCheck.ReachSentence(null, false, ServerState.Online));

            var none = new PlayCheck.Reach { Host = "vanadreams.fairywitch.ca", NoName = true };
            StringAssert.Contains(PlayCheck.ReachSentence(none, false, ServerState.Online), "could not look up vanadreams.fairywitch.ca");

            var closed = new PlayCheck.Reach { Host = "h" };
            closed.Closed.AddRange(PlayCheck.LoginPorts);
            StringAssert.Contains(PlayCheck.ReachSentence(closed, false, ServerState.Online), "Tailscale");
            StringAssert.Contains(PlayCheck.ReachSentence(closed, true, ServerState.Online), "Open Tailscale");
            StringAssert.Contains(PlayCheck.ReachSentence(closed, false, ServerState.Offline), "offline");
            StringAssert.Contains(PlayCheck.ReachSentence(closed, false, ServerState.Maintenance), "maintenance");

            var profileOnly = new PlayCheck.Reach { Host = "h" };
            profileOnly.Open.AddRange(new[] { 54231, 54001, 54230 });
            profileOnly.Closed.Add(PlayCheck.ProfilePort);
            StringAssert.Contains(PlayCheck.ReachSentence(profileOnly, false, ServerState.Online), "blocks port 51220");
        }

        [TestMethod]
        public void Ports_are_tried_for_real()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var open = ((IPEndPoint)listener.LocalEndpoint).Port;
            // a port that was free a moment ago and is closed now
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var closed = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            try
            {
                var r = PlayCheck.TryPortsAsync("127.0.0.1", new[] { open, closed }, TimeSpan.FromSeconds(2)).Result;
                Assert.IsFalse(r.NoName);
                CollectionAssert.AreEqual(new[] { open }, r.Open);
                CollectionAssert.AreEqual(new[] { closed }, r.Closed);
            }
            finally { listener.Stop(); }
            var nothing = PlayCheck.TryPortsAsync("no-such-host.invalid", new[] { 1 }, TimeSpan.FromSeconds(2)).Result;
            Assert.IsTrue(nothing.NoName);
            Assert.IsFalse(nothing.AllOpen);
        }

        [TestMethod]
        public void Crash_record_is_read_and_explained()
        {
            var c = PlayCheck.ParseCrash(new[] { "xiloader.exe", "2.2.0.0", "68f0", "MSVCP140.dll", "14.29.30139.0", "c0000005" }, DateTime.Now);
            Assert.AreEqual("xiloader.exe", c.App);
            Assert.AreEqual("MSVCP140.dll", c.Module);
            Assert.IsTrue(PlayCheck.IsRuntimeModule(c.Module));
            StringAssert.Contains(PlayCheck.CrashSentence(c), "vc_redist.x86.exe");

            var other = PlayCheck.ParseCrash(new[] { "pol.exe", "1", "2", "ig9icd32.dll", "31.0.101.5590" }, DateTime.Now);
            Assert.IsFalse(PlayCheck.IsRuntimeModule(other.Module));
            StringAssert.Contains(PlayCheck.CrashSentence(other), "ig9icd32.dll 31.0.101.5590");
            StringAssert.Contains(PlayCheck.CrashSentence(other), "Ctrl+C");

            Assert.IsNull(PlayCheck.ParseCrash(new[] { "a" }, DateTime.Now));
            Assert.IsNull(PlayCheck.CrashSentence(null));
        }

        [TestMethod]
        public void Loader_process_name_comes_from_the_boot_file()
        {
            Assert.AreEqual("xiloader", PlayCheck.LoaderProcessName(@"bootloader\xiloader.exe"));
            Assert.AreEqual("pol", PlayCheck.LoaderProcessName(@"C:\PlayOnline\PlayOnlineViewer\pol.exe"));
            Assert.IsNull(PlayCheck.LoaderProcessName(""));
        }
    }
}
