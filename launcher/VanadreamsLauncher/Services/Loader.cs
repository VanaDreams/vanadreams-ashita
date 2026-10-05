using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>
    /// The xiloader the server accepts, in one place.
    ///
    /// The connect server checks the loader's major.minor exactly (src/login/auth_session.h,
    /// SupportedXiloaderVersion) and answers anything else with "Your xiloader is too old" - a newer
    /// one included. LandSandBoat published xiloader v2.2.0 on 27 Sep 2026; it moves PlayOnline's
    /// profile connection through a TLS relay to a profile server, and it reports itself as 2.2.
    /// This launcher had been fetching "the latest release", so every install and repair from that
    /// day on put a loader in bootloader\ that the server turned away, and the server logs nothing
    /// for that refusal - the message goes only to the player's console.
    ///
    /// So the version is pinned here and moves when the server does. Setup and Repair always write
    /// this one; start-up and Play replace whatever is there when it is not one the server takes.
    /// It was 2.1.2 until the live update of 4 Oct 2026, when the server took the profile server
    /// and moved to 2.2.
    /// </summary>
    public static class Loader
    {
        public const string Repo = "LandSandBoat/xiloader";
        public const string Tag = "v2.2.0";
        public const int RequiredMajor = 2;
        public const int RequiredMinor = 2;

        public static string PathIn(string ashitaRoot) => Path.Combine(ashitaRoot, "bootloader", "xiloader.exe");

        /// <summary>The loader's own version from its resource block, or null when there is no loader.</summary>
        public static Version Installed(string ashitaRoot)
        {
            try
            {
                var p = PathIn(ashitaRoot);
                if (!File.Exists(p)) return null;
                // FileVersion, the string, is empty on xiloader's builds; the fixed parts are set.
                var v = FileVersionInfo.GetVersionInfo(p);
                return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart);
            }
            catch (Exception) { return null; }
        }

        /// <summary>True when the server would let this loader log in: the same major.minor, any patch.</summary>
        public static bool IsSupported(Version v) => v != null && v.Major == RequiredMajor && v.Minor == RequiredMinor;

        public static string Describe(Version v) => v == null ? "missing" : v.ToString();

        /// <summary>
        /// Put the pinned loader in bootloader\. With force it is written regardless (Setup, Repair);
        /// without, only when what is there is not one the server accepts. True when a file was written.
        /// </summary>
        public static async Task<bool> InstallAsync(Downloader downloader, string downloadsFolder, string ashitaRoot, bool force, IProgress<DownloadProgress> progress = null)
        {
            if (!force && IsSupported(Installed(ashitaRoot))) return false;
            var asset = await downloader.ReleaseAssetAsync(Repo, Tag, "xiloader.exe").ConfigureAwait(false);
            if (asset == null) throw new InvalidOperationException("xiloader " + Tag + " is not on LandSandBoat's releases.");
            var tmp = Path.Combine(downloadsFolder, "xiloader-" + asset.Tag + ".exe");
            if (!File.Exists(tmp) || new FileInfo(tmp).Length != asset.Size)
                await downloader.DownloadFileAsync(asset.Url, tmp, progress, "xiloader " + asset.Tag, asset.Size).ConfigureAwait(false);
            var dest = PathIn(ashitaRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(tmp, dest, true);
            Log.Info("xiloader " + asset.Tag + " written to " + dest);
            return true;
        }
    }
}
