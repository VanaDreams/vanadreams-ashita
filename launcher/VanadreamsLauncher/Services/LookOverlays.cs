using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>
    /// The modern look pack and the title pack as XIPivot overlays, so a player can switch them off and order them.
    ///
    /// The client package ships them as looks-*.zip and vanadreams-title-*.zip. Until now they unpacked into FINAL FANTASY XI
    /// over the files they change. The overlay manifest keeps the same zips, byte for byte, and only points them at
    /// {ashita}/polplugins/DATs/&lt;overlay&gt;, so a new install puts them straight into XIPivot's folder.
    ///
    /// A copy installed the old way is put right once (MigrateAsync): each pack file is moved from the game folder into its
    /// overlay (or fetched from the pack when the game holds a different copy), then the original the pack replaced is put
    /// back, pulled out of the package's base zips by byte range and checked against the zip's CRC. Nothing is deleted
    /// that is not first in place elsewhere, and a run that stops part way carries on where it left off.
    /// </summary>
    public static class LookOverlays
    {
        public const string Looks = "vanadreams-looks";
        public const string Title = "vanadreams-title";
        public const string Marker = ".vanadreams-overlays.txt";

        /// <summary>The overlay a chunk belongs to, or null. The effects zip (bootloader DLLs) is not a DAT pack.</summary>
        public static string OverlayFor(string chunkName)
        {
            if (string.IsNullOrEmpty(chunkName)) return null;
            if (chunkName.StartsWith("looks-effects", StringComparison.OrdinalIgnoreCase)) return null;
            if (chunkName.StartsWith("looks-", StringComparison.OrdinalIgnoreCase)) return Looks;
            if (chunkName.StartsWith("vanadreams-title", StringComparison.OrdinalIgnoreCase)) return Title;
            return null;
        }

        public static string OverlayDest(string overlay) => ClientInstall.AshitaDest + "/polplugins/DATs/" + overlay;

        /// <summary>True when this manifest ships the packs as overlays: the server has the overlay layout.</summary>
        public static bool ManifestUsesOverlays(ClientManifest m) =>
            m.Chunks.Any(c => OverlayFor(c.Name) != null && string.Equals(c.Dest, OverlayDest(OverlayFor(c.Name)), StringComparison.OrdinalIgnoreCase));

        /// <summary>The manifest with every pack pointed at its overlay; nothing else changes. Writes the same JSON shape.</summary>
        public static string ToOverlayManifest(string manifestJson)
        {
            var d = Json.ParseObject(manifestJson) ?? throw new InvalidDataException("Not a manifest.");
            foreach (var o in Json.List(d["chunks"]))
            {
                var c = Json.Obj(o);
                var overlay = OverlayFor(Json.Str(c, "name"));
                if (overlay != null) c["dest"] = OverlayDest(overlay);
            }
            return Json.Stringify(d);
        }

        /// <summary>The pack chunks this copy has in the game folder, by its record: the ones a migration moves.</summary>
        public static List<ClientChunk> InPlace(ClientManifest m, string gameRoot)
        {
            var done = ClientInstall.Done(gameRoot, m.Version);
            return m.Chunks.Where(c => OverlayFor(c.Name) != null && done.Contains(c.Name + " " + c.Sha256)).ToList();
        }

        public static bool Migrated(string ashitaRoot, string version)
        {
            var path = Path.Combine(ashitaRoot ?? "", Marker);
            return File.Exists(path) && File.ReadAllLines(path).Any(l => l.Trim() == version);
        }

        /// <summary>A migration is due: the server has the overlay layout and this copy still has the packs in the game folder.</summary>
        public static bool Due(ClientManifest m, string gameRoot, string ashitaRoot) =>
            m != null && !string.IsNullOrWhiteSpace(ashitaRoot) && ManifestUsesOverlays(m) && !Migrated(ashitaRoot, m.Version) && InPlace(m, gameRoot).Count > 0;

        /// <summary>
        /// The shipped overlays an install or a migration put on disk are listed in pivot.ini once each, the title pack first
        /// so it wins; after that the Install page's switch is the player's. Returns the ids listed now.
        /// 16 Oct 2026: a fresh install unpacked vanadreams-looks with nothing listing it, and the game showed the old look.
        /// </summary>
        public static List<string> GiveOnce(string ashitaRoot, ICollection<string> given)
        {
            var listed = new List<string>();
            if (string.IsNullOrWhiteSpace(ashitaRoot)) return listed;
            foreach (var id in new[] { Title, Looks })
            {
                var key = "overlay:" + id;
                if (given.Contains(key) || !Directory.Exists(Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), id))) continue;
                PivotConfig.AddOverlay(ashitaRoot, id);
                given.Add(key);
                listed.Add(id);
            }
            return listed;
        }

        private static string Local(string folder, string zipName) => Path.Combine(folder, zipName.Replace('/', '\\'));

        private static bool Matches(string path, uint crc)
        {
            try { return File.Exists(path) && Crc32.Of(path) == crc; } catch (IOException) { return false; }
        }

        private static void WriteAtomically(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".vdtmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        /// <summary>
        /// Move the packs out of the game folder into their overlays and put the originals back. Returns a sentence on what
        /// it did. Throws when something cannot be checked; what has been done stays done and a second run carries on.
        /// </summary>
        public static async Task<string> MigrateAsync(System.Net.Http.HttpClient http, ClientManifest m, string gameRoot, string ashitaRoot,
            IProgress<InstallProgress> progress, CancellationToken ct)
        {
            var game = ClientInstall.GameFolder(gameRoot);
            var packs = InPlace(m, gameRoot);
            if (packs.Count == 0) return "Nothing to move.";
            var url = new Func<string, string>(name => ClientInstall.ChunkUrl(m.Version, name));

            // 1. every pack file into its overlay
            var packFiles = new Dictionary<string, RemoteEntry>(StringComparer.OrdinalIgnoreCase);
            int moved = 0, fetched = 0, already = 0;
            for (var i = 0; i < packs.Count; i++)
            {
                var c = packs[i];
                var overlay = Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), OverlayFor(c.Name));
                progress?.Report(new InstallProgress { Stage = "Moving the " + (OverlayFor(c.Name) == Title ? "title pack" : "look pack"), Part = i + 1, Parts = packs.Count, Done = i, Total = packs.Count * 2 });
                var zip = await RemoteZip.OpenAsync(http, url(c.Name), ct).ConfigureAwait(false);
                var missing = new List<RemoteEntry>();
                foreach (var e in zip.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    packFiles[e.Name] = e;
                    var inOverlay = Local(overlay, e.Name);
                    var inGame = Local(game, e.Name);
                    if (Matches(inOverlay, e.Crc)) { already++; continue; }
                    if (Matches(inGame, e.Crc))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(inOverlay));
                        if (File.Exists(inOverlay)) File.Delete(inOverlay);
                        File.Copy(inGame, inOverlay);     // copied, not moved: the game file is replaced only once its original is in hand
                        moved++;
                        continue;
                    }
                    missing.Add(e);
                }
                await zip.FetchAsync(missing, (e, bytes) => { WriteAtomically(Local(overlay, e.Name), bytes); fetched++; }, ct).ConfigureAwait(false);
            }

            // 2. the originals the packs replaced, out of the base zips
            var bases = m.Chunks.Where(c => OverlayFor(c.Name) == null && !c.ToAshita && string.Equals(c.Dest, ClientInstall.GameFolderName, StringComparison.OrdinalIgnoreCase)).ToList();
            int restored = 0, kept = 0, noOriginal = 0;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < bases.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InstallProgress { Stage = "Putting the original files back", Part = i + 1, Parts = bases.Count, Done = packs.Count + (long)i * packs.Count / Math.Max(1, bases.Count), Total = packs.Count * 2 });
                var zip = await RemoteZip.OpenAsync(http, url(bases[i].Name), ct).ConfigureAwait(false);
                var wanted = new List<RemoteEntry>();
                foreach (var e in zip.Entries.Where(e => packFiles.ContainsKey(e.Name)))
                {
                    found.Add(e.Name);
                    if (Matches(Local(game, e.Name), e.Crc)) { kept++; continue; }
                    wanted.Add(e);
                }
                await zip.FetchAsync(wanted, (e, bytes) => { WriteAtomically(Local(game, e.Name), bytes); restored++; }, ct).ConfigureAwait(false);
            }
            // a pack file with no original is one the pack added; the overlay has it, so it leaves the game folder
            foreach (var name in packFiles.Keys.Where(n => !found.Contains(n)))
            {
                var inGame = Local(game, name);
                if (File.Exists(inGame) && Matches(inGame, packFiles[name].Crc)) { File.Delete(inGame); noOriginal++; }
            }

            // 3. XIPivot loads them, and the marker says this version is done
            foreach (var overlay in packs.Select(c => OverlayFor(c.Name)).Distinct()) PivotConfig.AddOverlay(ashitaRoot, overlay);
            File.AppendAllText(Path.Combine(ashitaRoot, Marker), m.Version + Environment.NewLine, new UTF8Encoding(false));
            var said = $"{moved + fetched + already} pack files in their overlays ({moved} moved, {fetched} fetched, {already} already there); " +
                       $"{restored} originals put back, {kept} already original" + (noOriginal > 0 ? $", {noOriginal} added files cleared" : "") + ".";
            Log.Info("look overlays: " + said);
            return said;
        }
    }
}
