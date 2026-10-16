using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class LookOverlaysTests
    {
        private const string Manifest = @"{ ""schema"": 1, ""client_ver"": ""30260904_1"", ""chunks"": [
            { ""name"": ""ROM-05.zip"", ""size"": 10, ""sha256"": ""aa"" },
            { ""name"": ""looks-01.zip"", ""size"": 10, ""sha256"": ""bb"", ""dest"": ""FINAL FANTASY XI"" },
            { ""name"": ""looks-effects.zip"", ""size"": 10, ""sha256"": ""cc"", ""dest"": ""{ashita}/bootloader"" },
            { ""name"": ""vanadreams-title-01.zip"", ""size"": 10, ""sha256"": ""dd"", ""dest"": ""FINAL FANTASY XI"" } ] }";

        private string _root;

        [TestInitialize]
        public void Make() => _root = Path.Combine(Path.GetTempPath(), "vdl-ov-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Clean() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestMethod]
        public void Only_the_look_and_title_packs_become_overlays()
        {
            Assert.AreEqual(LookOverlays.Looks, LookOverlays.OverlayFor("looks-03.zip"));
            Assert.AreEqual(LookOverlays.Title, LookOverlays.OverlayFor("vanadreams-title-01.zip"));
            Assert.AreEqual(LookOverlays.Text, LookOverlays.OverlayFor("vanadreams-text-01.zip"));
            Assert.IsNull(LookOverlays.OverlayFor("looks-effects.zip"));   // the effects DLLs stay in bootloader
            Assert.IsNull(LookOverlays.OverlayFor("ROM-05.zip"));
        }

        [TestMethod]
        public void The_overlay_manifest_moves_only_the_packs_and_keeps_names_and_hashes()
        {
            var before = ClientManifest.Parse(Manifest);
            var after = ClientManifest.Parse(LookOverlays.ToOverlayManifest(Manifest));
            Assert.IsFalse(LookOverlays.ManifestUsesOverlays(before));
            Assert.IsTrue(LookOverlays.ManifestUsesOverlays(after));
            Assert.AreEqual("{ashita}/polplugins/DATs/vanadreams-looks", after.Chunks.Single(c => c.Name == "looks-01.zip").Dest);
            Assert.AreEqual("{ashita}/polplugins/DATs/vanadreams-title", after.Chunks.Single(c => c.Name == "vanadreams-title-01.zip").Dest);
            Assert.AreEqual("{ashita}/bootloader", after.Chunks.Single(c => c.Name == "looks-effects.zip").Dest);
            Assert.AreEqual("FINAL FANTASY XI", after.Chunks.Single(c => c.Name == "ROM-05.zip").Dest);
            CollectionAssert.AreEqual(before.Chunks.Select(c => c.Name + c.Sha256 + c.Size).ToList(), after.Chunks.Select(c => c.Name + c.Sha256 + c.Size).ToList());
            // a new install puts the pack under XIPivot's folder
            Assert.AreEqual(Path.Combine(_root, "polplugins", "DATs", "vanadreams-looks"), ClientInstall.DestFolder(after.Chunks[1], @"C:\Games\Vanadreams", _root));
        }

        [TestMethod]
        public void A_migration_is_due_only_for_a_copy_with_the_packs_in_the_game_folder_and_once()
        {
            var game = Path.Combine(_root, "game");
            var ashita = Path.Combine(_root, "ashita");
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(ashita);
            var overlays = ClientManifest.Parse(LookOverlays.ToOverlayManifest(Manifest));
            var old = ClientManifest.Parse(Manifest);

            Assert.IsFalse(LookOverlays.Due(overlays, game, ashita), "nothing installed");
            File.WriteAllText(Path.Combine(game, ClientInstall.RecordName), "30260904_1 ROM-05.zip aa\n30260904_1 looks-01.zip bb\n");
            Assert.IsTrue(LookOverlays.Due(overlays, game, ashita));
            Assert.IsFalse(LookOverlays.Due(old, game, ashita), "the server has not moved to overlays yet");
            Assert.AreEqual(1, LookOverlays.InPlace(overlays, game).Count);
            File.WriteAllText(Path.Combine(ashita, LookOverlays.Marker), "30260904_1\n");
            Assert.IsFalse(LookOverlays.Due(overlays, game, ashita), "done once");
        }

        [TestMethod]
        public void The_shipped_packs_are_listed_by_name_and_switch()
        {
            var ashita = Path.Combine(_root, "ashita");
            Directory.CreateDirectory(Path.Combine(PivotConfig.OverlaysRoot(ashita), LookOverlays.Looks));
            Directory.CreateDirectory(Path.Combine(PivotConfig.OverlaysRoot(ashita), LookOverlays.Title));
            PivotConfig.SetOverlays(ashita, new[] { LookOverlays.Title, LookOverlays.Looks });
            var packs = ModPacks.Overlays(ashita);
            CollectionAssert.AreEqual(new[] { "Modern look pack", "Title pack" }, packs.Select(p => p.Name).ToList());   // the title pack wins, so it is shown last
            Assert.IsTrue(packs.All(p => p.CanSwitch && p.On));
        }

        [TestMethod]
        public void A_fresh_install_lists_its_overlays_once_and_then_leaves_the_switch_alone()
        {
            var ashita = Path.Combine(_root, "ashita");
            Directory.CreateDirectory(Path.Combine(PivotConfig.OverlaysRoot(ashita), LookOverlays.Looks));
            var given = new System.Collections.Generic.List<string>();
            CollectionAssert.AreEqual(new[] { LookOverlays.Looks }, LookOverlays.GiveOnce(ashita, given));
            CollectionAssert.AreEqual(new[] { LookOverlays.Looks }, PivotConfig.ReadOverlays(ashita));
            // the player switches it off; the next start does not switch it back on
            PivotConfig.SetOverlays(ashita, new string[0]);
            Assert.AreEqual(0, LookOverlays.GiveOnce(ashita, given).Count);
            Assert.AreEqual(0, PivotConfig.ReadOverlays(ashita).Count);
            // the title pack arrives later: it is listed too, after the packs already there
            PivotConfig.SetOverlays(ashita, new[] { LookOverlays.Looks });
            Directory.CreateDirectory(Path.Combine(PivotConfig.OverlaysRoot(ashita), LookOverlays.Title));
            LookOverlays.GiveOnce(ashita, given);
            CollectionAssert.AreEqual(new[] { LookOverlays.Looks, LookOverlays.Title }, PivotConfig.ReadOverlays(ashita));
            // so do the text fixes, which only ever ship as an overlay
            Directory.CreateDirectory(Path.Combine(PivotConfig.OverlaysRoot(ashita), LookOverlays.Text));
            CollectionAssert.AreEqual(new[] { LookOverlays.Text }, LookOverlays.GiveOnce(ashita, given));
            CollectionAssert.AreEqual(new[] { LookOverlays.Looks, LookOverlays.Title, LookOverlays.Text }, PivotConfig.ReadOverlays(ashita));
        }

        [TestMethod]
        public void Crc32_matches_the_zip_standard()
        {
            Assert.AreEqual(0xCBF43926u, Crc32.Of(System.Text.Encoding.ASCII.GetBytes("123456789")));
        }
    }
}
