using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    /// <summary>Profiles on an address the server moved from are pointed at the new one, on disk, with nothing else changed.</summary>
    [TestClass]
    public class ServerMoveTests
    {
        private string _dir;

        [TestInitialize]
        public void Init()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vdl-move-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_dir, "config", "boot"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            LoaderCommand.ResetServers();
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private string Boot(string name) => Path.Combine(_dir, "config", "boot", name);

        [TestMethod]
        public void Repoint_changes_only_the_server_and_keeps_every_other_character()
        {
            Assert.AreEqual("--server 100.93.179.33 --hairpin", LoaderCommand.Repoint("--server 100.114.52.41 --hairpin"));
            Assert.AreEqual("--lang 2 --SERVER 100.93.179.33", LoaderCommand.Repoint("--lang 2 --SERVER \"100.114.52.41\""));
            Assert.AreEqual("--server 100.93.179.33  --hairpin --user Bob --pass \"two words\"", LoaderCommand.Repoint("--server 100.114.52.41  --hairpin --user Bob --pass \"two words\""));
            Assert.IsNull(LoaderCommand.Repoint("--server vanadreams.fairywitch.ca"));
            Assert.IsNull(LoaderCommand.Repoint("--server 100.93.179.33 --hairpin"));
            Assert.IsNull(LoaderCommand.Repoint("--server homepointxi.com"));
            Assert.IsNull(LoaderCommand.Repoint("--server 192.168.0.19"));   // a home address may be somebody else's server
            Assert.IsNull(LoaderCommand.Repoint(""));
            Assert.IsNull(LoaderCommand.Repoint("--hairpin"));
        }

        [TestMethod]
        public void Every_profile_on_the_old_address_is_rewritten_and_the_rest_are_left_alone()
        {
            File.WriteAllText(Boot("vanadreams.ini"), "[ashita.launcher]\r\nname = Vanadreams\r\n\r\n[ashita.boot]\r\nfile = .\\\\bootloader\\\\xiloader.exe\r\ncommand = --server 100.114.52.41 --hairpin\r\nscript = default.txt\r\n");
            File.WriteAllText(Boot("Hogdog.ini"), "[ashita.boot]\r\ncommand     = --server 100.114.52.41 --hairpin --lang 2\r\n; a comment\r\n");
            File.WriteAllText(Boot("Peepa.ini"), "[ashita.boot]\r\ncommand = --server vanadreams.fairywitch.ca\r\n");
            File.WriteAllText(Boot("Other.ini"), "[ashita.boot]\r\ncommand = --server homepointxi.com\r\n");
            File.WriteAllText(Boot(".launch-vanadreams-1234.ini"), "[ashita.boot]\r\ncommand = --server 100.114.52.41 --user u --pass p\r\n");
            var peepa = File.ReadAllText(Boot("Peepa.ini"));
            var other = File.ReadAllText(Boot("Other.ini"));

            var changed = new ProfileStore(_dir).RepointMoved();

            Assert.AreEqual(2, changed.Count);
            var v = File.ReadAllText(Boot("vanadreams.ini"));
            StringAssert.Contains(v, "command = --server 100.93.179.33 --hairpin\r\n");
            StringAssert.Contains(v, "file = .\\\\bootloader\\\\xiloader.exe");
            StringAssert.Contains(v, "script = default.txt");
            var h = File.ReadAllText(Boot("Hogdog.ini"));
            StringAssert.Contains(h, "--server 100.93.179.33 --hairpin --lang 2");
            StringAssert.Contains(h, "; a comment");
            Assert.AreEqual(peepa, File.ReadAllText(Boot("Peepa.ini")));
            Assert.AreEqual(other, File.ReadAllText(Boot("Other.ini")));
            StringAssert.Contains(File.ReadAllText(Boot(".launch-vanadreams-1234.ini")), "100.114.52.41");   // launch copies carry a login and are swept

            Assert.AreEqual(0, new ProfileStore(_dir).RepointMoved().Count);   // a second pass finds nothing
            var loaded = Profile.Load(Boot("vanadreams.ini"));
            Assert.IsTrue(loaded.IsVanadreams);
            Assert.IsTrue(loaded.Command.IsTailscale);
        }

        [TestMethod]
        public void A_later_move_named_in_the_catalogue_reaches_profiles_on_any_old_address()
        {
            var cat = Catalog.Parse("{\"schema\":1,\"servers\":{\"tailscale\":\"100.64.1.2\",\"moved\":{\"100.93.179.33\":\"100.64.1.2\"}},\"items\":[]}");
            Assert.AreEqual("100.64.1.2", cat.TailscaleServer);
            LoaderCommand.UseServers(cat.TailscaleServer, cat.MovedServers);

            Assert.AreEqual("100.64.1.2", LoaderCommand.MovedTo("100.114.52.41"));   // two moves on, still followed
            Assert.AreEqual("100.64.1.2", LoaderCommand.MovedTo("100.93.179.33"));
            Assert.IsNull(LoaderCommand.MovedTo("100.64.1.2"));

            var c = new LoaderCommand();
            c.UseTailscale(true);
            Assert.AreEqual("100.64.1.2", c.Server);
            Assert.IsTrue(LoaderCommand.Parse("--server 100.93.179.33 --hairpin").IsTailscale);
            Assert.AreEqual("--server 100.64.1.2 --hairpin", LoaderCommand.Parse("--server 100.93.179.33 --hairpin").ToIniCommand());

            File.WriteAllText(Boot("vanadreams.ini"), "[ashita.boot]\r\ncommand = --server 100.93.179.33 --hairpin\r\n");
            Assert.AreEqual(1, new ProfileStore(_dir).RepointMoved().Count);
            StringAssert.Contains(File.ReadAllText(Boot("vanadreams.ini")), "command = --server 100.64.1.2 --hairpin");
        }

        [TestMethod]
        public void A_catalogue_with_no_servers_block_keeps_the_built_in_move()
        {
            var cat = Catalog.Parse("{\"schema\":1,\"items\":[]}");
            LoaderCommand.UseServers(cat.TailscaleServer, cat.MovedServers);
            Assert.AreEqual(LoaderCommand.TailscaleServer, LoaderCommand.Tailscale);
            Assert.AreEqual(LoaderCommand.TailscaleServer, LoaderCommand.MovedTo(LoaderCommand.PreviousTailscaleServer));

            // a catalogue that lists the current address as moved cannot send players in a loop
            LoaderCommand.UseServers(LoaderCommand.TailscaleServer, new Dictionary<string, string> { { LoaderCommand.TailscaleServer, LoaderCommand.PreviousTailscaleServer } });
            Assert.IsNull(LoaderCommand.MovedTo(LoaderCommand.TailscaleServer));
        }

        [TestMethod]
        public void The_shipped_catalogue_names_the_current_server()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            string path = null;
            for (var d = new DirectoryInfo(root); d != null && path == null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "catalog.json")) && Directory.Exists(Path.Combine(d.FullName, "launcher"))) path = Path.Combine(d.FullName, "catalog.json");
            if (path == null) Assert.Inconclusive("catalog.json not found above " + root);
            var cat = Catalog.Parse(File.ReadAllText(path));
            Assert.AreEqual(LoaderCommand.TailscaleServer, cat.TailscaleServer);
            Assert.AreEqual(LoaderCommand.TailscaleServer, cat.MovedServers[LoaderCommand.PreviousTailscaleServer]);
        }
    }
}
