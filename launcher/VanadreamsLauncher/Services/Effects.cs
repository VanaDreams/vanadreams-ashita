using System;
using System.IO;
using System.Linq;

namespace Vanadreams.Services
{
    /// <summary>
    /// The modern look's effects: crosire's d3d8to9 (d3d8.dll) and ReShade (d3d9.dll) sit in Ashita's bootloader folder
    /// beside xiloader.exe, which is where the game process loads them from. Low spec turns them off by renaming both
    /// to .off, and back on by renaming them back; the textures are left as they are.
    /// </summary>
    public static class Effects
    {
        public static readonly string[] Wrappers = { "d3d8.dll", "d3d9.dll" };
        public const string OffSuffix = ".off";

        public static string Folder(string ashitaRoot) => Path.Combine(ashitaRoot, "bootloader");

        /// <summary>True when either wrapper is in the bootloader folder, on or off.</summary>
        public static bool Installed(string ashitaRoot) =>
            !string.IsNullOrWhiteSpace(ashitaRoot) &&
            Wrappers.Any(w => File.Exists(Path.Combine(Folder(ashitaRoot), w)) || File.Exists(Path.Combine(Folder(ashitaRoot), w + OffSuffix)));

        /// <summary>
        /// Puts the wrappers in the state the setting asks for and returns how many files moved. A wrapper an update
        /// has just unpacked wins over an older .off copy, so Low spec stays on through an update and a fresh copy is
        /// never shadowed by a stale one.
        /// </summary>
        public static int Apply(string ashitaRoot, bool lowSpec)
        {
            if (string.IsNullOrWhiteSpace(ashitaRoot)) return 0;
            var folder = Folder(ashitaRoot);
            if (!Directory.Exists(folder)) return 0;
            var moved = 0;
            foreach (var w in Wrappers)
            {
                var on = Path.Combine(folder, w);
                var off = on + OffSuffix;
                try
                {
                    if (lowSpec && File.Exists(on))
                    {
                        if (File.Exists(off)) File.Delete(off);
                        File.Move(on, off);
                        moved++;
                    }
                    else if (!lowSpec && File.Exists(off))
                    {
                        if (File.Exists(on)) File.Delete(off);
                        else { File.Move(off, on); moved++; }
                    }
                }
                catch (Exception ex) { Log.Warn("low spec " + w + ": " + ex.Message); }
            }
            if (moved > 0) Log.Info("effects " + (lowSpec ? "off" : "on") + " (low spec " + (lowSpec ? "on" : "off") + "): " + moved + " file(s) renamed");
            return moved;
        }
    }
}
