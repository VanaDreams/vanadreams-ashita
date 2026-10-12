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
    /// </summary>
    public static class VcRuntime
    {
        /// <summary>Microsoft's permanent link to the newest v14 runtime, then the Visual Studio 2022 one.</summary>
        public static readonly string[] InstallerUrls =
        {
            "https://aka.ms/vc14/vc_redist.x86.exe",
            "https://aka.ms/vs/17/release/vc_redist.x86.exe",
        };

        /// <summary>The oldest msvcp140.dll that carries what Ashita 4.3.2 and xiloader v2.2.0 were built against.</summary>
        public static readonly Version Minimum = new Version(14, 50);

        /// <summary>Where 32-bit programs load their system DLLs from: SysWOW64 on 64-bit Windows, System32 otherwise.</summary>
        public static string SystemX86Folder => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

        public static bool IsReady() => IsReady(SystemX86Folder);

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
            if (v == null) return "missing";
            return File.Exists(Path.Combine(dir, "msvcp140_atomic_wait.dll")) ? v.ToString() : v + ", no atomic_wait";
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
            Log.Info("Visual C++ x86 runtime is " + Describe() + "; installing Microsoft's");
            var exe = Path.Combine(downloadsFolder, "vc_redist.x86.exe");
            Exception last = null;
            foreach (var url in InstallerUrls)
            {
                try
                {
                    if (File.Exists(exe)) File.Delete(exe);
                    await downloader.DownloadFileAsync(url, exe, progress, "Microsoft Visual C++ runtime").ConfigureAwait(false);
                    last = null;
                    break;
                }
                catch (Exception ex) { last = ex; Log.Warn("vc runtime from " + url + ": " + ex.Message); }
            }
            if (last != null) throw new InvalidOperationException("Couldn't download Microsoft's Visual C++ runtime (" + last.Message + ").", last);

            // Through the shell so Windows can raise its permission prompt; the installer asks for it itself.
            var psi = new ProcessStartInfo(exe, "/install /passive /norestart") { UseShellExecute = true };
            int code;
            try
            {
                using (var p = Process.Start(psi))
                {
                    await Task.Run(() => p.WaitForExit()).ConfigureAwait(false);
                    code = p.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException("Windows asked permission to install Microsoft's Visual C++ runtime and it was cancelled. The game can't start without it: press Play again and choose Yes.");
            }
            Log.Info("vc_redist.x86 exited " + code + "; runtime now " + Describe());
            if (!InstallerSucceeded(code) && !IsReady())
                throw new InvalidOperationException("Microsoft's Visual C++ runtime installer stopped with code " + code + ". Run " + exe + " yourself, then press Play again.");
            return IsReady();
        }
    }
}
