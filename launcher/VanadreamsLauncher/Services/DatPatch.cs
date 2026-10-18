using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Vanadreams.Services
{
    /// <summary>
    /// Carries a few chunks of a pack author's game file onto the player's own current copy of that file.
    ///
    /// A DAT is a run of chunks, each with a 16-byte header: four letters of name, then a 32-bit word whose low 7 bits are
    /// the chunk's type and whose next 19 bits are its size in 16-byte units. XI-Vision's ROM/119/51.DAT is an older
    /// client's interface file; loaded as it is, it lacks what newer clients draw and can crash them. Its own changes are a
    /// few textures (arrows, buttons, font, gauge), so the launcher lays exactly those chunks over the player's own current
    /// file and writes the result into the overlay. Nothing of Square Enix's is hosted and the author's file is only read.
    /// A chunk is carried only when both files hold it with the same name, type and size; anything else is left as it is.
    /// </summary>
    public static class DatPatch
    {
        public sealed class Chunk
        {
            public int Pos { get; set; }
            public string Name { get; set; }
            public int Type { get; set; }
            public int Size { get; set; }
        }

        /// <summary>The chunks of a DAT, in order. Stops at the first header that does not fit the file.</summary>
        public static List<Chunk> Chunks(byte[] d)
        {
            var list = new List<Chunk>();
            var p = 0;
            while (d != null && p + 16 <= d.Length)
            {
                var w = BitConverter.ToUInt32(d, p + 4);
                var size = (int)((w >> 7) & 0x7ffff) * 16;
                if (size < 16 || p + size > d.Length) break;
                list.Add(new Chunk { Pos = p, Name = Encoding.ASCII.GetString(d, p, 4), Type = (int)(w & 0x7f), Size = size });
                p += size;
            }
            return list;
        }

        /// <summary>A catalogue spec is the four-letter chunk name, a colon and the type: "anc :32".</summary>
        public static bool TryParseSpec(string spec, out string name, out int type)
        {
            name = null; type = 0;
            if (string.IsNullOrEmpty(spec) || spec.Length < 6 || spec[4] != ':') return false;
            name = spec.Substring(0, 4);
            return int.TryParse(spec.Substring(5), out type);
        }

        /// <summary>
        /// A copy of target with the named chunks taken from donor. Copied counts the chunks carried over; Skipped says why
        /// each of the others was not (missing from one file, or a different size).
        /// </summary>
        public static byte[] Transplant(byte[] target, byte[] donor, IEnumerable<string> specs, out int copied, out List<string> skipped)
        {
            copied = 0;
            skipped = new List<string>();
            var result = (byte[])target.Clone();
            var t = Chunks(target);
            var d = Chunks(donor);
            foreach (var spec in specs ?? Enumerable.Empty<string>())
            {
                if (!TryParseSpec(spec, out var name, out var type)) { skipped.Add(spec + ": not a name:type spec"); continue; }
                var from = d.FirstOrDefault(c => c.Name == name && c.Type == type);
                var to = t.FirstOrDefault(c => c.Name == name && c.Type == type);
                if (from == null || to == null) { skipped.Add(spec + ": not in " + (from == null ? "the pack's file" : "your file")); continue; }
                if (from.Size != to.Size) { skipped.Add(spec + ": different size"); continue; }
                Buffer.BlockCopy(donor, from.Pos, result, to.Pos, to.Size);
                copied++;
            }
            return result;
        }

        /// <summary>
        /// Reads the player's own file and the pack's, writes the patched copy to outFile (replacing it) and returns a sentence
        /// on what it did. Nothing is written when no chunk could be carried, and the player's own file is never touched.
        /// </summary>
        public static string ApplyToFile(string ownFile, string donorFile, string outFile, IEnumerable<string> specs)
        {
            if (!File.Exists(ownFile)) return "Your own " + Path.GetFileName(ownFile) + " was not found, so the pack's interface tweaks were not applied.";
            var patched = Transplant(File.ReadAllBytes(ownFile), File.ReadAllBytes(donorFile), specs, out var copied, out var skipped);
            if (copied == 0) return "No interface chunk could be carried over" + (skipped.Count > 0 ? " (" + string.Join("; ", skipped) + ")" : "") + ".";
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            var temp = outFile + ".vdtmp";
            File.WriteAllBytes(temp, patched);
            if (File.Exists(outFile)) File.Delete(outFile);
            File.Move(temp, outFile);
            return copied + " interface chunk(s) carried onto your file" + (skipped.Count > 0 ? "; left as they were: " + string.Join("; ", skipped) : "") + ".";
        }
    }
}
