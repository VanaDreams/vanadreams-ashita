using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Vanadreams.Services
{
    /// <summary>
    /// XIPivot overlays the catalogue has dropped because the game's own files now carry what they did. One is
    /// removed from pivot.ini and from disk only once the game folder holds the file that replaces it, so a player
    /// whose copy has not had the client update yet keeps the overlay, and with it the music, until it has.
    /// 16 Oct 2026: vanadreams-music (the title track) moved into the client's title pack, beside the title screen.
    /// </summary>
    public static class RetiredOverlays
    {
        public sealed class Retired
        {
            public string Id { get; set; }
            public string GameFile { get; set; }    // path under the game folder that replaces the overlay
            public string Sha256 { get; set; }
        }

        public static readonly IReadOnlyList<Retired> All = new[]
        {
            new Retired
            {
                Id = "vanadreams-music",
                GameFile = @"sound\win\music\data\music034.bgw",
                Sha256 = "26e57fdf02d0c49aeb60113598ec00a495553f95c0c455366a60d6e34cc82dce",
            },
        };

        /// <summary>The retired overlays this Ashita still has, either listed in pivot.ini or on disk.</summary>
        public static List<Retired> Present(string ashitaRoot, IEnumerable<Retired> retired = null)
        {
            var listed = PivotConfig.ReadOverlays(ashitaRoot);
            return (retired ?? All).Where(r => listed.Contains(r.Id, StringComparer.OrdinalIgnoreCase)
                                            || Directory.Exists(Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), r.Id))).ToList();
        }

        /// <summary>True when the game folder holds the exact file that took the overlay's place.</summary>
        public static bool Replaced(Retired r, string gameFolder)
        {
            if (string.IsNullOrWhiteSpace(gameFolder)) return false;
            var file = Path.Combine(gameFolder, r.GameFile);
            if (!File.Exists(file)) return false;
            using (var sha = SHA256.Create())
            using (var s = File.OpenRead(file))
                return string.Equals(BitConverter.ToString(sha.ComputeHash(s)).Replace("-", ""), r.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes every retired overlay whose replacement is in the game folder: its pivot.ini line and its folder.
        /// Returns the ids removed, so the caller can drop them from the enabled list.
        /// </summary>
        public static List<string> Sweep(string ashitaRoot, string gameFolder, IEnumerable<Retired> retired = null)
        {
            var removed = new List<string>();
            foreach (var r in Present(ashitaRoot, retired))
            {
                if (!Replaced(r, gameFolder)) continue;
                PivotConfig.RemoveOverlay(ashitaRoot, r.Id);
                var dir = Path.Combine(PivotConfig.OverlaysRoot(ashitaRoot), r.Id);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                removed.Add(r.Id);
            }
            return removed;
        }
    }
}
