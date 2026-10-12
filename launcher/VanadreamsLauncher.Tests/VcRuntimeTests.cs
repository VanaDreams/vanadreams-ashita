using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class VcRuntimeTests
    {
        [TestMethod]
        public void Not_ready_without_the_dlls()
        {
            var dir = Path.Combine(Path.GetTempPath(), "vdl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.IsFalse(VcRuntime.IsReady(dir));
                // atomic_wait alone is not enough: msvcp140.dll has to be there and new enough
                File.WriteAllBytes(Path.Combine(dir, "msvcp140_atomic_wait.dll"), new byte[] { 0 });
                Assert.IsFalse(VcRuntime.IsReady(dir));
                File.WriteAllBytes(Path.Combine(dir, "msvcp140.dll"), new byte[] { 0 });   // no version block
                Assert.IsFalse(VcRuntime.IsReady(dir));
            }
            finally { Directory.Delete(dir, true); }
            Assert.IsFalse(VcRuntime.IsReady(null));
            Assert.IsFalse(VcRuntime.IsReady(""));
        }

        [TestMethod]
        public void Vc80_is_found_by_its_side_by_side_folder()
        {
            var sxs = Path.Combine(Path.GetTempPath(), "vdl-sxs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sxs);
            try
            {
                Assert.IsFalse(VcRuntime.HasVc80(sxs));
                // the 64-bit one does not count: the game is 32-bit
                var amd64 = Directory.CreateDirectory(Path.Combine(sxs, "amd64_microsoft.vc80.crt_1fc8b3b9a1e18e3b_8.0.50727.6195_none_88e41e092fab0294")).FullName;
                File.WriteAllBytes(Path.Combine(amd64, "msvcr80.dll"), new byte[] { 0 });
                Assert.IsFalse(VcRuntime.HasVc80(sxs));
                var x86 = Directory.CreateDirectory(Path.Combine(sxs, "x86_microsoft.vc80.crt_1fc8b3b9a1e18e3b_8.0.50727.9680_none_d08f9da24428a3ab")).FullName;
                Assert.IsFalse(VcRuntime.HasVc80(sxs));   // an empty leftover folder is not the runtime
                File.WriteAllBytes(Path.Combine(x86, "msvcr80.dll"), new byte[] { 0 });
                Assert.IsTrue(VcRuntime.HasVc80(sxs));
            }
            finally { Directory.Delete(sxs, true); }
            Assert.IsFalse(VcRuntime.HasVc80(null));
        }

        [TestMethod]
        public void Installer_codes_that_mean_the_runtime_is_there()
        {
            Assert.IsTrue(VcRuntime.InstallerSucceeded(0));
            Assert.IsTrue(VcRuntime.InstallerSucceeded(1638));   // a newer one was already installed
            Assert.IsTrue(VcRuntime.InstallerSucceeded(3010));   // restart wanted; the DLLs work before it
            Assert.IsFalse(VcRuntime.InstallerSucceeded(1602));  // cancelled
            Assert.IsFalse(VcRuntime.InstallerSucceeded(1));
        }

        [TestMethod]
        public void Minimum_covers_what_xiloader_and_ashita_were_built_with()
        {
            // xiloader v2.2.0 links with 14.44, Ashita 4.3.2.1 with 14.51; the 14.50 runtime line serves both
            Assert.IsTrue(VcRuntime.Minimum >= new Version(14, 44));
            Assert.AreEqual(14, VcRuntime.Minimum.Major);
            Assert.IsTrue(VcRuntime.InstallerUrls[0].EndsWith("vc_redist.x86.exe"));
        }
    }
}
