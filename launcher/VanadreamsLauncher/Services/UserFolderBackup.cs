using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Vanadreams.Services
{
    /// <summary>
    /// A copy of the game's USER folder, taken on every Play. The game keeps each character's macros, key binds and
    /// config there, in a folder named after the character's content id, which our server takes from the character id.
    /// 12 and 13 Oct 2026: Terry lost her macros and config twice. A crash before a clean logout loses what changed
    /// that session, and a PC that plays live and staging from one install can have two characters with the same id
    /// writing to one folder. Neither can be undone from the game, so the launcher keeps the last few copies.
    /// </summary>
    public static class UserFolderBackup
    {
        public const int Keep = 10;
        private const string Prefix = "USER-";
        private const string LastFile = "last.txt";

        public static string BackupFolder => Path.Combine(LauncherSettings.DataFolder, "user-backups");

        /// <summary>
        /// Zips gameFolder\USER into backupFolder unless nothing in it changed since the last copy, then keeps only the
        /// newest Keep copies. Returns the new zip's path, or null when there was nothing new to keep.
        /// </summary>
        public static string Take(string gameFolder, string backupFolder, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(gameFolder)) return null;
            var user = Path.Combine(gameFolder, "USER");
            if (!Directory.Exists(user)) return null;
            var files = Directory.GetFiles(user, "*", SearchOption.AllDirectories);
            if (files.Length == 0) return null;

            Directory.CreateDirectory(backupFolder);
            var signature = Signature(user, files);
            var last = Path.Combine(backupFolder, LastFile);
            if (File.Exists(last) && File.ReadAllText(last) == signature && Copies(backupFolder).Any()) return null;

            var zip = Path.Combine(backupFolder, Prefix + now.ToString("yyyyMMdd-HHmmss") + ".zip");
            var temp = zip + ".part";
            if (File.Exists(temp)) File.Delete(temp);
            using (var archive = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (var f in files)
                {
                    var rel = f.Substring(user.Length).TrimStart('\\', '/').Replace('\\', '/');
                    // the game may hold a file open while it runs; a copy without it is still worth keeping
                    try { archive.CreateEntryFromFile(f, rel, CompressionLevel.Optimal); }
                    catch (IOException ex) { Log.Warn("USER backup skipped " + rel + ": " + ex.Message); }
                }
            }
            if (File.Exists(zip)) File.Delete(zip);
            File.Move(temp, zip);
            File.WriteAllText(last, signature);

            foreach (var old in Copies(backupFolder).Skip(Keep))
            {
                try { File.Delete(old); }
                catch (Exception ex) { Log.Warn("old USER backup not removed: " + ex.Message); }
            }
            return zip;
        }

        /// <summary>
        /// Brings toGameFolder\USER up to date from fromGameFolder\USER, file by file: a macro book or config file is
        /// copied when it is missing here or was written more recently there. Nothing is deleted. Returns the character
        /// folders that received at least one file.
        ///
        /// 15 Oct 2026: Terry's PC had two games, C:\Program Files (x86)\...\FINAL FANTASY XI and D:\fancy ffxi. Each
        /// keeps its own USER folder, so whichever one a launch started showed that copy's macros, and the other set
        /// looked wiped. Play takes the backup of toGameFolder first, so this can always be undone.
        /// </summary>
        public static List<string> CarryOver(string fromGameFolder, string toGameFolder)
        {
            var carried = new List<string>();
            if (string.IsNullOrWhiteSpace(fromGameFolder) || string.IsNullOrWhiteSpace(toGameFolder)) return carried;
            var from = Path.Combine(fromGameFolder, "USER");
            var to = Path.Combine(toGameFolder, "USER");
            if (!Directory.Exists(from) || string.Equals(Path.GetFullPath(from).TrimEnd('\\', '/'), Path.GetFullPath(to).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return carried;

            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var rel = f.Substring(from.Length).TrimStart('\\', '/');
                var dest = Path.Combine(to, rel);
                var when = File.GetLastWriteTimeUtc(f);
                if (File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= when) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest, true);
                File.SetLastWriteTimeUtc(dest, when);
                var character = rel.Split('\\', '/')[0];
                if (!carried.Contains(character, StringComparer.OrdinalIgnoreCase)) carried.Add(character);
            }
            return carried;
        }

        /// <summary>The copies in a folder, newest first.</summary>
        public static List<string> Copies(string backupFolder) =>
            Directory.Exists(backupFolder)
                ? Directory.GetFiles(backupFolder, Prefix + "*.zip").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToList()
                : new List<string>();

        private static string Signature(string user, IEnumerable<string> files)
        {
            var sb = new StringBuilder();
            foreach (var f in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(f);
                sb.Append(f.Substring(user.Length)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
            }
            return sb.ToString();
        }
    }
}
