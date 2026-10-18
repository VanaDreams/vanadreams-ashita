using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>Installing a catalogue item: a folder of a GitHub repo, or a release archive.</summary>
    public static class AddonInstaller
    {
        public static string TargetFolder(string ashitaRoot, CatalogItem item) =>
            item.Install == InstallAction.PivotOverlay ? Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), item.Id)
                                                       : Path.Combine(ashitaRoot, "addons", item.LoadName ?? item.Id);

        /// <summary>
        /// Brings the item's folder in step with the repo (see RepoSync): only what is missing or changed is
        /// downloaded, and files an earlier install put there are removed once the repo has dropped them.
        /// report is called with (file, number, of how many) for each download.
        /// </summary>
        public static async Task<RepoSync.Plan> InstallRepoFolderAsync(Downloader downloader, LauncherSettings settings, string ashitaRoot, CatalogItem item, Action<string, int, int> report = null, string gameFolder = null)
        {
            var listed = await downloader.ListRepoFolderAsync(item.Repo, item.Path, item.Branch).ConfigureAwait(false);
            var files = RepoSync.Skipping(listed, item.Skip);
            if (files.Count == 0) throw new InvalidOperationException("Nothing found at " + item.Repo + "/" + item.Path + ".");

            var target = TargetFolder(ashitaRoot, item);
            var plan = await Task.Run(() => RepoSync.Make(files, target)).ConfigureAwait(false);

            var n = 0;
            foreach (var f in plan.Download)
            {
                n++;
                report?.Invoke(f.Rel, n, plan.Download.Count);
                await downloader.DownloadFileAsync(f.Url, RepoSync.LocalPath(target, f.Rel)).ConfigureAwait(false);
            }
            foreach (var rel in plan.Delete)
            {
                try { File.Delete(RepoSync.LocalPath(target, rel)); }
                catch (Exception ex) { Log.Warn("could not remove " + rel + ": " + ex.Message); }
            }
            RepoSync.WriteManifest(target, files.Where(f => RepoSync.IsSafeRel(f.Rel)).Select(f => f.Rel));
            await PatchOwnFileAsync(downloader, listed, item, target, gameFolder).ConfigureAwait(false);

            settings.InstalledVersions[item.Id] = item.Version ?? DateTime.Now.ToString("yyyy-MM-dd");
            if (item.Install == InstallAction.PivotOverlay) PivotConfig.AddOverlay(ashitaRoot, item.Id);
            return plan;
        }

        /// <summary>
        /// For an item that names a patch (source.patch): the pack's copy of that file is fetched to a temporary place, its
        /// listed chunks are laid over the player's own current file (DatPatch), and the result goes into the overlay.
        /// A failure here is logged and never fails the install: the rest of the pack still works.
        /// </summary>
        private static async Task PatchOwnFileAsync(Downloader downloader, List<RepoFile> listed, CatalogItem item, string target, string gameFolder)
        {
            if (item.Patches.Count == 0 || string.IsNullOrWhiteSpace(gameFolder)) return;
            foreach (var patch in item.Patches)
            {
                if (patch.Chunks.Count == 0) continue;
                var temp = Path.Combine(Path.GetTempPath(), "vdl-donor-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var donor = listed.FirstOrDefault(f => string.Equals(f.Rel, patch.File, StringComparison.OrdinalIgnoreCase));
                    if (donor == null || !RepoSync.IsSafeRel(patch.File)) { Log.Warn(item.Id + ": " + patch.File + " is not in the repo folder"); continue; }
                    await downloader.DownloadFileAsync(donor.Url, temp).ConfigureAwait(false);
                    var own = Path.Combine(gameFolder, patch.File.Replace('/', '\\'));
                    var chunks = patch.Chunks;
                    var said = await Task.Run(() => DatPatch.ApplyToFile(own, temp, RepoSync.LocalPath(target, patch.File), chunks)).ConfigureAwait(false);
                    Log.Info(item.Id + " " + patch.File + ": " + said);
                }
                catch (Exception ex) { Log.Warn(item.Id + ": interface patch of " + patch.File + " not applied: " + ex.Message); }
                finally { try { File.Delete(temp); } catch (Exception) { /* a temp file left behind is harmless */ } }
            }
        }

        /// <summary>
        /// Downloads the item's newest release archive, unless the same one is already in the downloads folder, and
        /// unpacks it where the catalogue's install field says. The default is the Ashita root.
        /// </summary>
        public static async Task<string> InstallReleaseAsync(Downloader downloader, LauncherSettings settings, string ashitaRoot, CatalogItem item, IProgress<DownloadProgress> progress = null, Action<string> say = null)
        {
            // no ConfigureAwait(false): say is called on the caller's context, the page's own thread
            var asset = await downloader.LatestReleaseAssetAsync(item.Repo, item.Asset);
            if (asset == null) throw new InvalidOperationException("No release asset matching " + item.Asset + " on " + item.Repo + ".");
            var zip = Path.Combine(settings.DownloadsFolder, item.Id + "-" + asset.Tag + "-" + asset.Name);
            // a source zip has no size listed; one already here for the same tag is the same code
            if (!File.Exists(zip) || (asset.Size > 0 && new FileInfo(zip).Length != asset.Size))
                await downloader.DownloadFileAsync(asset.Url, zip, progress, asset.Name, asset.Size);
            say?.Invoke("Unpacking…");
            var unzipTo = item.Install == InstallAction.CopyToAddons ? Path.Combine(ashitaRoot, "addons", item.LoadName ?? item.Id)
                        : item.Install == InstallAction.UnzipToAddons ? Path.Combine(ashitaRoot, "addons")
                        : item.Install == InstallAction.PivotOverlay ? Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), item.Id)
                        : ashitaRoot;
            var source = string.Equals(item.Asset, Downloader.SourceZip, StringComparison.OrdinalIgnoreCase);
            await Task.Run(() => Downloader.ExtractZipOverwrite(zip, unzipTo, stripTopFolder: source));
            if (item.Install == InstallAction.PivotOverlay) PivotConfig.AddOverlay(ashitaRoot, item.Id);
            settings.InstalledVersions[item.Id] = item.Version ?? asset.Tag;
            return asset.Tag;
        }

        /// <summary>How many of Ashita's own addons a saved list has to tick before it counts as the old "everything on" list.</summary>
        public const int TickAllThreshold = 50;

        /// <summary>
        /// Launchers before 0.2.24 ticked every addon that ships inside Ashita (all 86) at setup, and later launchers
        /// leave a player's ticks alone, so those players still load crosshair, chamcham, ime, aspect and the rest.
        /// Given such a list, this returns it without the Ashita ones (a bundled item the catalogue marks on by default
        /// stays); everything the player installed from the catalogue stays ticked. A list the player picked by hand,
        /// with fewer than TickAllThreshold of Ashita's addons, comes back null: nothing to tidy.
        /// </summary>
        public static List<string> TidyTickAll(Catalog catalog, IEnumerable<string> enabled)
        {
            var list = (enabled ?? Enumerable.Empty<string>()).ToList();
            var bundled = list.Where(id => { var i = catalog.Find(id); return i != null && i.Source == SourceType.Bundled; }).ToList();
            if (bundled.Count < TickAllThreshold) return null;
            return list.Where(id => { var i = catalog.Find(id); return i == null || i.Source != SourceType.Bundled || (i.OnByDefault && string.IsNullOrEmpty(i.HeldBack)); }).ToList();
        }

        /// <summary>The on-by-default items this player has not been given yet.</summary>
        public static List<CatalogItem> DefaultsToGive(Catalog catalog, IEnumerable<string> alreadyGiven) =>
            catalog.Items.Where(i => i.OnByDefault && i.HasV4 && string.IsNullOrEmpty(i.HeldBack)
                                     && !(alreadyGiven ?? Enumerable.Empty<string>()).Contains(i.Id, StringComparer.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// The repo-folder items this player has installed whose catalogue version has moved on since. These are
        /// brought up to date at every start, with no button press, because players do not update by hand and a
        /// fix to an addon that is crashing them has to reach them. Only the files are refreshed: whether the item is
        /// ticked stays the player's choice. Items with no version in the catalogue are left to the Reinstall button.
        /// </summary>
        public static List<CatalogItem> InstalledToUpdate(Catalog catalog, IDictionary<string, string> installedVersions)
        {
            if (installedVersions == null) return new List<CatalogItem>();
            return catalog.Items.Where(i => i.Source == SourceType.RepoFolder && !string.IsNullOrEmpty(i.Version)
                                            && string.IsNullOrEmpty(i.HeldBack)
                                            && installedVersions.TryGetValue(i.Id, out var have)
                                            && !string.Equals(have, i.Version, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
