using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>
    /// The player's Windower 4, for the pieces of ours that run there. Only its addons folder is ever written to,
    /// and only the folders of our own addons in it; Windower's settings, scripts and the player's other addons
    /// are left alone.
    /// </summary>
    public static class WindowerInstall
    {
        /// <summary>Where our Windower addons live in this repo.</summary>
        public const string Repo = "VanaDreams/vanadreams-ashita";
        public const string Branch = "main";
        public static string AddonRepoPath(string addon) => "windower/addons/" + addon;

        /// <summary>True when the folder holds a Windower: Windower.exe, or an addons folder beside its plugins.</summary>
        public static bool IsWindower(string folder) =>
            !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
            && (File.Exists(Path.Combine(folder, "Windower.exe")) || (Directory.Exists(Path.Combine(folder, "addons")) && Directory.Exists(Path.Combine(folder, "plugins"))));

        /// <summary>The Windower folder: the one the player picked, else one of the usual places; null when none is found.</summary>
        public static string Find(LauncherSettings settings = null)
        {
            if (settings != null && IsWindower(settings.WindowerRoot)) return settings.WindowerRoot;
            var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new List<string>();
            foreach (var root in new[] { x86, pf, @"C:\", @"D:\" }.Where(r => !string.IsNullOrEmpty(r)))
                foreach (var name in new[] { "Windower4", "Windower", "Windower 4" })
                    candidates.Add(Path.Combine(root, name));
            return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "Windower.exe")) || Directory.Exists(Path.Combine(c, "addons")));
        }

        public static string AddonFolder(string windowerRoot, string addon) => Path.Combine(windowerRoot, "addons", addon);

        /// <summary>Where charcapture for Windower writes its snapshots.</summary>
        public static string SnapshotFolder(string windowerRoot) => Path.Combine(AddonFolder(windowerRoot, "charcapture"), "data");

        /// <summary>
        /// Brings one of our Windower addons in step with the repo, the way catalogue addons are (RepoSync): what is
        /// missing or changed is downloaded, and only files an earlier install put there are ever removed, so what the
        /// addon wrote itself (charcapture's snapshots in data\) stays.
        /// </summary>
        public static async Task<RepoSync.Plan> InstallAddonAsync(Downloader downloader, string windowerRoot, string addon)
        {
            var files = await downloader.ListRepoFolderAsync(Repo, AddonRepoPath(addon), Branch).ConfigureAwait(false);
            if (files.Count == 0) throw new InvalidOperationException("Nothing found at " + Repo + "/" + AddonRepoPath(addon) + ".");
            var target = AddonFolder(windowerRoot, addon);
            var plan = await Task.Run(() => RepoSync.Make(files, target)).ConfigureAwait(false);
            foreach (var f in plan.Download)
                await downloader.DownloadFileAsync(f.Url, RepoSync.LocalPath(target, f.Rel)).ConfigureAwait(false);
            foreach (var rel in plan.Delete)
            {
                try { File.Delete(RepoSync.LocalPath(target, rel)); }
                catch (Exception ex) { Log.Warn("could not remove " + rel + ": " + ex.Message); }
            }
            RepoSync.WriteManifest(target, files.Where(f => RepoSync.IsSafeRel(f.Rel)).Select(f => f.Rel));
            return plan;
        }
    }
}
