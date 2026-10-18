using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class DatPatchTests
    {
        // one chunk: 4 letters, a word of (units << 7 | type), 8 bytes of padding, then filler of the given byte
        static byte[] Chunk(string name, int type, int units, byte fill)
        {
            var b = new byte[units * 16];
            Encoding.ASCII.GetBytes(name).CopyTo(b, 0);
            BitConverter.GetBytes((uint)((units << 7) | type)).CopyTo(b, 4);
            for (var i = 16; i < b.Length; i++) b[i] = fill;
            return b;
        }

        static byte[] Dat(params byte[][] chunks) => chunks.SelectMany(c => c).ToArray();

        [TestMethod]
        public void ReadsChunksInOrder()
        {
            var c = DatPatch.Chunks(Dat(Chunk("aaaa", 32, 2, 1), Chunk("bbbb", 32, 3, 1)));
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual("bbbb", c[1].Name);
            Assert.AreEqual(32, c[1].Pos);
            Assert.AreEqual(48, c[1].Size);
        }

        [TestMethod]
        public void CarriesOnlyTheNamedChunksAndLeavesTheRestOfYourFile()
        {
            var own = Dat(Chunk("anc ", 32, 2, 1), Chunk("keep", 32, 2, 1), Chunk("font", 32, 2, 1));
            var pack = Dat(Chunk("anc ", 32, 2, 9), Chunk("keep", 32, 2, 9), Chunk("font", 32, 2, 9));
            var r = DatPatch.Transplant(own, pack, new[] { "anc :32", "font:32" }, out var copied, out var skipped);
            Assert.AreEqual(2, copied);
            Assert.AreEqual(0, skipped.Count);
            Assert.AreEqual(9, r[16]);        // anc
            Assert.AreEqual(1, r[32 + 16]);   // keep untouched
            Assert.AreEqual(9, r[64 + 16]);   // font
            Assert.AreEqual(1, own[16]);      // the input is not modified
        }

        [TestMethod]
        public void SkipsAChunkOfADifferentSizeOrOneMissingFromEitherFile()
        {
            var own = Dat(Chunk("anc ", 32, 2, 1), Chunk("gaug", 32, 2, 1));
            var pack = Dat(Chunk("anc ", 32, 3, 9), Chunk("butt", 32, 2, 9));
            var r = DatPatch.Transplant(own, pack, new[] { "anc :32", "butt:32", "gaug:32", "nonsense" }, out var copied, out var skipped);
            Assert.AreEqual(0, copied);
            Assert.AreEqual(4, skipped.Count);
            CollectionAssert.AreEqual(own, r);
        }

        [TestMethod]
        public void ApplyToFileWritesTheOverlayCopyAndNeverTouchesYourFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "dp" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var own = Path.Combine(dir, "own.dat"); var pack = Path.Combine(dir, "pack.dat"); var outp = Path.Combine(dir, "sub", "out.dat");
                File.WriteAllBytes(own, Dat(Chunk("anc ", 32, 2, 1)));
                File.WriteAllBytes(pack, Dat(Chunk("anc ", 32, 2, 9)));
                DatPatch.ApplyToFile(own, pack, outp, new[] { "anc :32" });
                Assert.AreEqual(9, File.ReadAllBytes(outp)[16]);
                Assert.AreEqual(1, File.ReadAllBytes(own)[16]);
                Assert.IsFalse(File.Exists(outp + ".vdtmp"));

                var none = Path.Combine(dir, "none.dat");
                DatPatch.ApplyToFile(own, pack, none, new[] { "zzzz:32" });
                Assert.IsFalse(File.Exists(none));
                StringAssert.Contains(DatPatch.ApplyToFile(Path.Combine(dir, "missing.dat"), pack, none, new[] { "anc :32" }), "not found");
            }
            finally { Directory.Delete(dir, true); }
        }

        [TestMethod]
        public void TheCatalogueReadsAnEntrysPatch()
        {
            var cat = Catalog.Parse(@"{ ""schema"": 1, ""items"": [
  { ""id"": ""xi-vision"", ""kind"": ""overlay"", ""source"": { ""type"": ""repo-folder"", ""repo"": ""Drauku/FFXI-Vision"", ""path"": ""FFXI-map-dats"", ""branch"": ""master"", ""patch"": { ""file"": ""ROM/119/51.DAT"", ""chunks"": [ ""anc :32"", ""font:32"" ], ""base"": ""https://example.org/51.DAT"" } }, ""install"": ""pivot-overlay"" }
] }");
            Assert.AreEqual("ROM/119/51.DAT", cat.Find("xi-vision").PatchFile);
            CollectionAssert.AreEqual(new[] { "anc :32", "font:32" }, cat.Find("xi-vision").PatchChunks);
            Assert.AreEqual("https://example.org/51.DAT", cat.Find("xi-vision").PatchBase);
        }

        [TestMethod]
        public void AnOldModsFileLacksTheCurrentMenuLayouts()
        {
            var clean = Dat(Chunk("que1", 48, 4, 1), Chunk("itm1", 48, 2, 1), Chunk("font", 32, 2, 1));
            var newer = Dat(Chunk("que1", 48, 4, 2), Chunk("itm1", 48, 2, 2), Chunk("acop", 48, 2, 2), Chunk("font", 32, 2, 2));
            var old = Dat(Chunk("que1", 48, 3, 1), Chunk("font", 32, 2, 9));
            Assert.IsTrue(DatPatch.HasLayoutOf(clean, clean));
            Assert.IsTrue(DatPatch.HasLayoutOf(newer, clean), "a newer client's file has all a clean one's layouts and more");
            Assert.IsFalse(DatPatch.HasLayoutOf(old, clean), "an old mod's copy is missing itm1");
        }

        [TestMethod]
        public void RepairPutsACleanFileBackOnlyWhenYoursIsAnOldModsCopy()
        {
            var dir = Path.Combine(Path.GetTempPath(), "dp" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var own = Path.Combine(dir, "51.DAT"); var clean = Path.Combine(dir, "clean.dat");
                var cleanBytes = Dat(Chunk("que1", 48, 4, 1), Chunk("itm1", 48, 2, 1));
                File.WriteAllBytes(clean, cleanBytes);

                var healthy = Dat(Chunk("que1", 48, 4, 3), Chunk("itm1", 48, 2, 3), Chunk("acop", 48, 2, 3));
                File.WriteAllBytes(own, healthy);
                Assert.IsFalse(DatPatch.RepairOwnFile(own, clean, out _));
                CollectionAssert.AreEqual(healthy, File.ReadAllBytes(own), "a healthy file is left exactly as it is");

                var old = Dat(Chunk("que1", 48, 3, 7));
                File.WriteAllBytes(own, old);
                Assert.IsTrue(DatPatch.RepairOwnFile(own, clean, out var said));
                CollectionAssert.AreEqual(cleanBytes, File.ReadAllBytes(own));
                CollectionAssert.AreEqual(old, File.ReadAllBytes(own + ".old-mod"));
                StringAssert.Contains(said, "old-mod");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
