using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class LoaderTests
    {
        [TestMethod]
        public void Accepts_only_the_servers_major_minor()
        {
            // the server compares major.minor and ignores the patch; newer is refused the same as older
            Assert.IsTrue(Loader.IsSupported(new Version(2, 1, 2)));
            Assert.IsTrue(Loader.IsSupported(new Version(2, 1, 0)));
            Assert.IsTrue(Loader.IsSupported(new Version(2, 1, 9)));
            Assert.IsFalse(Loader.IsSupported(new Version(2, 2, 0)));   // 27 Sep 2026: the release that locked new players out
            Assert.IsFalse(Loader.IsSupported(new Version(2, 0, 9)));
            Assert.IsFalse(Loader.IsSupported(new Version(3, 1, 0)));
            Assert.IsFalse(Loader.IsSupported(null));
        }

        [TestMethod]
        public void Pinned_tag_is_a_version_the_server_accepts()
        {
            Assert.IsTrue(Loader.Tag.StartsWith("v" + Loader.RequiredMajor + "." + Loader.RequiredMinor + "."), Loader.Tag);
        }

        [TestMethod]
        public void Installed_is_null_when_there_is_no_loader()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.IsNull(Loader.Installed(dir));
                Assert.AreEqual("missing", Loader.Describe(Loader.Installed(dir)));
                Assert.AreEqual(Path.Combine(dir, "bootloader", "xiloader.exe"), Loader.PathIn(dir));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
