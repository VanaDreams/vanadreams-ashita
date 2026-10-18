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
  { ""id"": ""xi-vision"", ""kind"": ""overlay"", ""source"": { ""type"": ""repo-folder"", ""repo"": ""Drauku/FFXI-Vision"", ""path"": ""FFXI-map-dats"", ""branch"": ""master"", ""patch"": { ""file"": ""ROM/119/51.DAT"", ""chunks"": [ ""anc :32"", ""font:32"" ] } }, ""install"": ""pivot-overlay"" }
] }");
            var patches = cat.Find("xi-vision").Patches;
            Assert.AreEqual(1, patches.Count);
            Assert.AreEqual("ROM/119/51.DAT", patches[0].File);
            CollectionAssert.AreEqual(new[] { "anc :32", "font:32" }, patches[0].Chunks);
        }

        [TestMethod]
        public void TheCatalogueReadsAListOfPatches()
        {
            var cat = Catalog.Parse(@"{ ""schema"": 1, ""items"": [
  { ""id"": ""xiview"", ""kind"": ""overlay"", ""source"": { ""type"": ""repo-folder"", ""repo"": ""KenshiDRK/XiView"", ""path"": ""Widescreen"", ""branch"": ""master"", ""patch"": [ { ""file"": ""ROM/119/51.DAT"", ""chunks"": [ ""*:32"" ] }, { ""file"": ""ROM/280/15.DAT"", ""chunks"": [ ""*:32"" ] } ] }, ""install"": ""pivot-overlay"" },
  { ""id"": ""crafty"", ""kind"": ""addon"", ""source"": { ""type"": ""repo-folder"", ""repo"": ""VanaDreams/vanadreams-ashita"", ""path"": ""addons/crafty"" }, ""install"": ""copy-to-addons"", ""load"": ""/addon load crafty"" }
] }");
            Assert.AreEqual(2, cat.Find("xiview").Patches.Count);
            Assert.AreEqual("ROM/280/15.DAT", cat.Find("xiview").Patches[1].File);
            Assert.AreEqual(0, cat.Find("crafty").Patches.Count);
        }

        [TestMethod]
        public void StarSpecCarriesEveryTextureOfTheSizeButNeverALayoutChunk()
        {
            var own = Dat(Chunk("tex1", 32, 2, 1), Chunk("tex2", 32, 2, 1), Chunk("tex2", 32, 2, 1), Chunk("big ", 32, 2, 1), Chunk("menu", 48, 2, 1));
            var pack = Dat(Chunk("tex1", 32, 2, 9), Chunk("tex2", 32, 2, 8), Chunk("tex2", 32, 2, 7), Chunk("big ", 32, 3, 9), Chunk("menu", 48, 2, 9));
            var r = DatPatch.Transplant(own, pack, new[] { "*:32" }, out var copied, out var skipped);
            Assert.AreEqual(3, copied);
            Assert.AreEqual(9, r[16]);         // tex1
            Assert.AreEqual(8, r[32 + 16]);    // first tex2 pairs with the pack's first
            Assert.AreEqual(7, r[64 + 16]);    // second with the second
            Assert.AreEqual(1, r[96 + 16]);    // a different size stays yours
            Assert.AreEqual(1, r[128 + 16]);   // type 48 is never touched by *:32
            Assert.AreEqual(1, skipped.Count);
        }
    }
}
