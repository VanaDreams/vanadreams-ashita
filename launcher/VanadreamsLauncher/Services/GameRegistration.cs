using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Vanadreams.Services
{
    /// <summary>What Windows currently says the game is: the folders PlayOnline registered and the COM servers xiloader creates.</summary>
    public sealed class RegisteredGame
    {
        /// <summary>InstallFolder 0001, the FINAL FANTASY XI folder.</summary>
        public string GameFolder { get; set; }
        /// <summary>InstallFolder 1000, the PlayOnlineViewer folder.</summary>
        public string ViewerFolder { get; set; }
        /// <summary>Every file on disk registered as the COM server for FFXi.dll, FFXiMain.dll or polcore.dll.</summary>
        public List<string> ComServers { get; set; } = new List<string>();
    }

    /// <summary>
    /// Which copy of the game a launch will start, and which it should.
    ///
    /// Windows holds one Final Fantasy XI registration for the whole machine. xiloader does not take a game
    /// path: it creates PlayOnline's and the game's COM objects, and whichever FFXi.dll and polcore.dll were
    /// registered last are the ones that run. So a player with an older copy of the game for another server,
    /// registered after ours, starts that copy from a Vanadreams profile, and the newer game this launcher
    /// installed sits unused. Before every launch the registration is compared with the copy the profile
    /// belongs to, folders and COM servers both, and put right when they differ.
    /// </summary>
    public static class GameRegistration
    {
        /// <summary>The COM servers that decide which game xiloader starts.</summary>
        public static readonly string[] DecidingComServers = { "FFXi.dll", "FFXiMain.dll", "polcore.dll" };

        /// <summary>A root holds a game we can point Windows at: FINAL FANTASY XI with FFXiMain.dll, and PlayOnlineViewer beside it.</summary>
        public static bool HasGame(string root) =>
            !string.IsNullOrWhiteSpace(root) &&
            File.Exists(Path.Combine(ClientInstall.GameFolder(root), "FFXiMain.dll")) &&
            Directory.Exists(ClientInstall.ViewerFolder(root));

        /// <summary>
        /// The root for a folder a player picked: the folder above FINAL FANTASY XI when they picked the game
        /// folder itself, otherwise the folder as it is (one that holds FINAL FANTASY XI and PlayOnlineViewer).
        /// </summary>
        public static string RootOf(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            var f = folder.Trim().TrimEnd('\\', '/');
            var cut = f.LastIndexOfAny(new[] { '\\', '/' });
            if (cut > 0 && string.Equals(f.Substring(cut + 1), ClientInstall.GameFolderName, StringComparison.OrdinalIgnoreCase))
                return f.Substring(0, cut);
            return f;
        }

        public static bool SameFolder(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
            string.Equals(a.Trim().TrimEnd('\\', '/'), b.Trim().TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        public static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            var r = root.Trim().TrimEnd('\\', '/');
            var p = path.Trim();
            return p.Length > r.Length && p.StartsWith(r, StringComparison.OrdinalIgnoreCase) && (p[r.Length] == '\\' || p[r.Length] == '/');
        }

        /// <summary>True when everything Windows would start belongs to this root.</summary>
        public static bool PointsAt(RegisteredGame reg, string root)
        {
            if (reg == null || string.IsNullOrWhiteSpace(root)) return false;
            if (!SameFolder(reg.GameFolder, ClientInstall.GameFolder(root))) return false;
            if (!SameFolder(reg.ViewerFolder, ClientInstall.ViewerFolder(root))) return false;
            return reg.ComServers.All(p => IsUnder(p, root));
        }

        /// <summary>The root of the copy Windows starts now: where the game's COM server lives, else the registered folder.</summary>
        public static string CurrentRoot(RegisteredGame reg)
        {
            if (reg == null) return null;
            var ffxi = reg.ComServers.FirstOrDefault(p => p.EndsWith("\\FFXi.dll", StringComparison.OrdinalIgnoreCase) || p.EndsWith("/FFXi.dll", StringComparison.OrdinalIgnoreCase));
            if (ffxi != null) return RootOf(ffxi.Substring(0, ffxi.Length - "FFXi.dll".Length - 1));
            return RootOf(reg.GameFolder);
        }

        /// <summary>Read the registration as a 32-bit process sees it, which is how xiloader and the game see it.</summary>
        public static RegisteredGame Read()
        {
            var reg = new RegisteredGame { GameFolder = ClientVersion.FindFfxiFolder(), ViewerFolder = ReadViewerFolder() };
            try
            {
                using (var hkcr = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry32))
                using (var clsid = hkcr.OpenSubKey("CLSID"))
                {
                    if (clsid == null) return reg;
                    foreach (var name in clsid.GetSubKeyNames())
                    {
                        string server;
                        try
                        {
                            using (var inproc = clsid.OpenSubKey(name + @"\InprocServer32"))
                                server = inproc?.GetValue(null) as string;
                        }
                        catch (Exception) { continue; }
                        if (string.IsNullOrWhiteSpace(server)) continue;
                        server = Environment.ExpandEnvironmentVariables(server.Trim().Trim('"'));
                        var file = Path.GetFileName(server);
                        // a registration left behind by a deleted copy can never be started, so it never decides anything
                        if (DecidingComServers.Any(d => string.Equals(d, file, StringComparison.OrdinalIgnoreCase)) &&
                            File.Exists(server) &&
                            !reg.ComServers.Contains(server, StringComparer.OrdinalIgnoreCase))
                            reg.ComServers.Add(server);
                    }
                }
            }
            catch (Exception ex) { Log.Warn("COM registration unreadable: " + ex.Message); }
            return reg;
        }

        private static string ReadViewerFolder()
        {
            foreach (var vendor in new[] { "PlayOnlineUS", "PlayOnline", "PlayOnlineEU" })
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (var key = hklm.OpenSubKey(@"SOFTWARE\" + vendor + @"\InstallFolder"))
                    {
                        var dir = key?.GetValue("1000") as string;
                        if (!string.IsNullOrWhiteSpace(dir)) return dir.TrimEnd('\\');
                    }
                }
                catch (Exception) { /* keep looking */ }
            }
            return null;
        }
    }
}
