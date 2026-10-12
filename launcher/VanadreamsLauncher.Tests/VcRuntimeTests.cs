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
