using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class EffectsTests
    {
        private string _ashita;
        private string Boot => Effects.Folder(_ashita);

        [TestInitialize]
        public void Make()
        {
            _ashita = Path.Combine(Path.GetTempPath(), "vdl-effects-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Boot);
            File.WriteAllText(Path.Combine(Boot, "xiloader.exe"), "loader");
            File.WriteAllText(Path.Combine(Boot, "d3d8.dll"), "d3d8to9");
            File.WriteAllText(Path.Combine(Boot, "d3d9.dll"), "reshade");
        }

        [TestCleanup]
        public void Clean() => Directory.Delete(_ashita, true);

        [TestMethod]
        public void LowSpec_RenamesBothWrappersOff_AndBack()
        {
            Assert.IsTrue(Effects.Installed(_ashita));
            Assert.AreEqual(2, Effects.Apply(_ashita, true));
            Assert.IsFalse(File.Exists(Path.Combine(Boot, "d3d8.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(Boot, "d3d9.dll.off")));
            Assert.IsTrue(File.Exists(Path.Combine(Boot, "xiloader.exe")));
            Assert.IsTrue(Effects.Installed(_ashita));
            Assert.AreEqual(0, Effects.Apply(_ashita, true));   // already off
            Assert.AreEqual(2, Effects.Apply(_ashita, false));
            Assert.AreEqual("reshade", File.ReadAllText(Path.Combine(Boot, "d3d9.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(Boot, "d3d9.dll.off")));
        }

        [TestMethod]
        public void AnUpdatedWrapperWinsOverAnOldOffCopy()
        {
            Effects.Apply(_ashita, true);
            File.WriteAllText(Path.Combine(Boot, "d3d9.dll"), "reshade 2");   // an update unpacks a new one
            Effects.Apply(_ashita, true);
            Assert.AreEqual("reshade 2", File.ReadAllText(Path.Combine(Boot, "d3d9.dll.off")));
            File.WriteAllText(Path.Combine(Boot, "d3d8.dll"), "d3d8to9 2");
            Effects.Apply(_ashita, false);
            Assert.AreEqual("d3d8to9 2", File.ReadAllText(Path.Combine(Boot, "d3d8.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(Boot, "d3d8.dll.off")));
        }

        [TestMethod]
        public void NoAshitaFolder_DoesNothing()
        {
            Assert.AreEqual(0, Effects.Apply(null, true));
            Assert.AreEqual(0, Effects.Apply(Path.Combine(_ashita, "nope"), true));
        }
    }
}
