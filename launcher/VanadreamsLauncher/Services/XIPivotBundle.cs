using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Vanadreams.Services
{
    /// <summary>
    /// XIPivot 4.3.001 for Ashita v4 (HealsCodes, BSD 3-Clause; LICENSE.txt ships beside it) is carried inside the launcher,
    /// so the look and title overlays load on every install without a trip to the Addons page. Put in place when missing,
    /// with the release's own layout; the catalogue item "pivot" is ticked once, which turns it on in each boot profile.
    /// </summary>
    public static class XIPivotBundle
    {
        public const string Version = "4.3.001";

        private static readonly Tuple<string, string>[] Files =
        {
            Tuple.Create("pivot.dll", @"polplugins\pivot.dll"),
            Tuple.Create("pivot.sample.ini", @"config\pivot\pivot.sample.ini"),
            Tuple.Create("README.md", @"docs\pivot\README.md"),
            Tuple.Create("LICENSE.txt", @"docs\pivot\LICENSE.txt"),
            Tuple.Create("DATs-README.txt", @"polplugins\DATs\README.txt"),
        };

        /// <summary>Write the bundled files that are missing. True when pivot.dll was put in place.</summary>
        public static bool Ensure(string ashitaRoot)
        {
            if (string.IsNullOrWhiteSpace(ashitaRoot) || !Directory.Exists(ashitaRoot)) return false;
            var placed = false;
            foreach (var f in Files)
            {
                var target = Path.Combine(ashitaRoot, f.Item2);
                if (File.Exists(target)) continue;
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/assets/xipivot/" + f.Item1));
                if (res == null) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var s = res.Stream)
                using (var o = File.Create(target)) s.CopyTo(o);
                placed |= f.Item1 == "pivot.dll";
            }
            if (placed) Log.Info("XIPivot " + Version + " put in place from the launcher");
            return placed;
        }
    }
}
