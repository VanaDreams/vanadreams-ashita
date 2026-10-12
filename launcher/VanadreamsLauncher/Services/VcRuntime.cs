using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>
    /// Microsoft's 32-bit Visual C++ runtime, which Ashita and xiloader both need, kept new enough.
    ///
    /// xiloader v2.2.0 (the one the server takes since 4 Oct 2026, Services\Loader.cs) links
    /// msvcp140_atomic_wait.dll, which older runtimes do not have; Ashita 4.3.2 is built with the
    /// 14.51 toolset. Ashita-cli starts xiloader suspended and waits, without a timeout, for its
    /// injection thread to finish. On a PC with an older runtime that thread never finishes: the
    /// console shows Ashita's banner and nothing more, and the game never starts. 12 Oct 2026: a new
    /// player at C:\Games\Vanadreams sat on exactly that screen.
    ///
    /// Ashita-cli itself runs on an old runtime, so it opening proves nothing. Setup and Play check
    /// the 32-bit system folder and run Microsoft's own installer when the runtime is missing or old.
    ///
    /// The game needs a second, much older one: FFXiMain.dll loads FINAL FANTASY XI\xinputdll.dll, which
    /// links msvcr80.dll through a manifest asking for Microsoft.VC80.CRT (x86). Retail's installer always
    /// added the Visual C++ 2005 runtime, so only a PC that never had FFXI lacks it, and there the game
    /// closes moments after login. 12 Oct 2026: a player whose game only ran after installing retail
    /// PlayOnline first. Both are checked and installed here.
    /// </summary>
    public static class VcRuntime
    {
        /// <summary>Microsoft's permanent link to the newest v14 runtime, then the Visual Studio 2022 one.</summary>
        public static readonly string[] InstallerUrls =
        {
            "https://aka.ms/vc14/vc_redist.x86.exe",
            "https://aka.ms/vs/17/release/vc_redist.x86.exe",
        };

        /// <summary>Microsoft's Visual C++ 2005 SP1 x86 redistributable, MFC security update (installs 8.0.50727.x into WinSxS).</summary>
        public const string Vc80InstallerUrl = "https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x86.EXE";

        /// <summary>The oldest msvcp140.dll that carries what Ashita 4.3.2 and xiloader v2.2.0 were built against.</summary>
        public static readonly Version Minimum = new Version(14, 50);

        /// <summary>Where 32-bit programs load their system DLLs from: SysWOW64 on 64-bit Windows, System32 otherwise.</summary>
        public static string SystemX86Folder => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

        public static bool IsReady() => IsReady(SystemX86Folder) && HasVc80(WinSxsFolder);

        public static string WinSxsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS");

        /// <summary>True when Windows' side-by-side store holds an x86 Microsoft.VC80.CRT assembly (any 8.0 build;
        /// Microsoft's publisher policy sends older requests to the newest one installed).</summary>
        public static bool HasVc80(string winSxsFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(winSxsFolder) || !Directory.Exists(winSxsFolder)) return false;
                foreach (var d in Directory.GetDirectories(winSxsFolder, "x86_microsoft.vc80.crt_1fc8b3b9a1e18e3b_8.0.*"))
                    if (File.Exists(Path.Combine(d, "msvcr80.dll"))) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>True when the folder holds msvcp140.dll at least <see cref="Minimum"/> and msvcp140_atomic_wait.dll.</summary>
        public static bool IsReady(string systemX86Folder)
        {
            if (string.IsNullOrEmpty(systemX86Folder)) return false;
            if (!File.Exists(Path.Combine(systemX86Folder, "msvcp140_atomic_wait.dll"))) return false;
            var v = VersionOf(Path.Combine(systemX86Folder, "msvcp140.dll"));
            return v != null && v >= Minimum;
        }

        public static Version VersionOf(string dll)
        {
            try
            {
                if (!File.Exists(dll)) return null;
                var v = FileVersionInfo.GetVersionInfo(dll);
                return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart);
            }
            catch (Exception) { return null; }
        }

        /// <summary>What is installed, for the log: "14.38.33135", "missing" or "14.50.35710, no atomic_wait".</summary>
        public static string Describe()
        {
            var dir = SystemX86Folder;
            var v = VersionOf(Path.Combine(dir, "msvcp140.dll"));
            var v14 = v == null ? "missing" : File.Exists(Path.Combine(dir, "msvcp140_atomic_wait.dll")) ? v.ToString() : v + ", no atomic_wait";
            return "14: " + v14 + "; 2005: " + (HasVc80(WinSxsFolder) ? "present" : "missing");
        }

        /// <summary>
        /// Exit codes Microsoft's installer uses for "it is there now": done, a newer one was already
        /// installed, and done but Windows wants a restart (the DLLs are usable before it).
        /// </summary>
        public static bool InstallerSucceeded(int exitCode) => exitCode == 0 || exitCode == 1638 || exitCode == 3010;

        /// <summary>
        /// Fetch Microsoft's x86 runtime installer and run it, showing its own progress window and the
        /// Windows permission prompt. True when the runtime is ready afterwards. Nothing happens when it
        /// already is.
        /// </summary>
        public static async Task<bool> EnsureAsync(Downloader downloader, string downloadsFolder, IProgress<DownloadProgress> progress = null)
        {
            if (IsReady()) return true;
            Log.Info("Visual C++ x86 runtimes: " + Describe() + "; installing Microsoft's");
            if (!IsReady(SystemX86Folder))
            {
                var exe = Path.Combine(downloadsFolder, "vc_redist.x86.exe");
                await DownloadAsync(downloader, InstallerUrls, exe, progress).ConfigureAwait(false);
                await RunAsync(exe, "/install /passive /norestart").ConfigureAwait(false);
            }
            if (!HasVc80(WinSxsFolder))
            {
                var exe = Path.Combine(downloadsFolder, "vcredist2005_x86.exe");
                await DownloadAsync(downloader, new[] { Vc80InstallerUrl }, exe, progress).ConfigureAwait(false);
                await RunAsync(exe, "/q").ConfigureAwait(false);
            }
            Log.Info("Visual C++ x86 runtimes now " + Describe());
            return IsReady();
        }

        private static async Task DownloadAsync(Downloader downloader, string[] urls, string exe, IProgress<DownloadProgress> progress)
        {
            Exception last = null;
            foreach (var url in urls)
            {
                try
                {
                    if (File.Exists(exe)) File.Delete(exe);
                    await downloader.DownloadFileAsync(url, exe, progress, "Microsoft Visual C++ runtime").ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) { last = ex; Log.Warn("vc runtime from " + url + ": " + ex.Message); }
            }
            throw new InvalidOperationException("Couldn't download Microsoft's Visual C++ runtime (" + last?.Message + ").", last);
        }

        // Through the shell so Windows can raise its permission prompt; the installers ask for it themselves.
        private static async Task RunAsync(string exe, string args)
        {
            int code;
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true }))
                {
                    await Task.Run(() => p.WaitForExit()).ConfigureAwait(false);
                    code = p.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException("Windows asked permission to install Microsoft's Visual C++ runtime and it was cancelled. The game can't start without it: press Play again and choose Yes.");
            }
            Log.Info(Path.GetFileName(exe) + " exited " + code);
            if (!InstallerSucceeded(code))
                throw new InvalidOperationException("Microsoft's Visual C++ runtime installer stopped with code " + code + ". Run " + exe + " yourself, then press Play again.");
        }
    }
}
