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
        /// <summary>Beyond the newest Keep, the last copy of each of this many days is kept too. 18 Oct 2026: one evening
        /// of switching characters took ten copies, so the ones from before Terry's macros went missing were already gone.</summary>
        public const int KeepDays = 30;
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

            foreach (var old in Expired(Copies(backupFolder), now))
            {
                try { File.Delete(old); }
                catch (Exception ex) { Log.Warn("old USER backup not removed: " + ex.Message); }
            }
            return zip;
        }

        /// <summary>The copies to remove: all but the newest Keep and the newest of each of the last KeepDays days.</summary>
        public static List<string> Expired(List<string> newestFirst, DateTime now)
        {
            var keep = new HashSet<string>(newestFirst.Take(Keep), StringComparer.OrdinalIgnoreCase);
            var days = new HashSet<string>();
            foreach (var c in newestFirst)
            {
                var name = Path.GetFileNameWithoutExtension(c);
                if (name.Length < Prefix.Length + 8) continue;
                var day = name.Substring(Prefix.Length, 8);
                if (!DateTime.TryParseExact(day, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d)) continue;
                if ((now.Date - d).TotalDays >= KeepDays) continue;
                if (days.Add(day)) keep.Add(c);
            }
            return newestFirst.Where(c => !keep.Contains(c)).ToList();
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
