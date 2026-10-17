using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vanadreams.Services
{
    /// <summary>One text change the catalogue makes to an installed item's files, e.g. turning off a first-run popup.</summary>
    public sealed class FileEdit
    {
        public string Path { get; set; }          // under the Ashita root; a * in any part matches folders or files there
        public string Pattern { get; set; }       // .NET regex
        public string Replacement { get; set; }   // Regex.Replace replacement, ${1} for groups
    }

    /// <summary>
    /// Applies a catalogue item's file edits. Every edit is written so that running it again changes nothing, so the
    /// launcher runs them each time it writes the startup script: after an install, an update, or an addon's own
    /// settings file appearing for a new character.
    /// </summary>
    public static class FileEdits
    {
        /// <summary>The files changed, relative to the Ashita root.</summary>
        public static List<string> Apply(string ashitaRoot, CatalogItem item)
        {
            var changed = new List<string>();
            if (item?.FileEdits == null || string.IsNullOrEmpty(ashitaRoot) || !Directory.Exists(ashitaRoot)) return changed;
            var root = System.IO.Path.GetFullPath(ashitaRoot).TrimEnd('\\', '/') + "\\";
            foreach (var edit in item.FileEdits.Where(e => !string.IsNullOrEmpty(e.Path) && !string.IsNullOrEmpty(e.Pattern)))
            {
                Regex rx;
                try { rx = new Regex(edit.Pattern); }
                catch (ArgumentException ex) { Log.Warn("file edit " + item.Id + ": bad pattern: " + ex.Message); continue; }
                foreach (var file in Expand(root, edit.Path))
                {
                    try
                    {
                        var text = File.ReadAllText(file);
                        var next = rx.Replace(text, edit.Replacement ?? "");
                        if (next == text) continue;
                        File.WriteAllText(file, next);
                        changed.Add(file.Substring(root.Length));
                    }
                    catch (Exception ex) { Log.Warn("file edit " + item.Id + " " + file + ": " + ex.Message); }
                }
            }
            return changed;
        }

        /// <summary>The files a path names, walking each * as a wildcard over that level. Never leaves the root.</summary>
        public static List<string> Expand(string root, string relPath)
        {
            var parts = relPath.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => p == ".." || p == ".")) return new List<string>();
            var current = new List<string> { root.TrimEnd('\\') };
            for (var i = 0; i < parts.Length; i++)
            {
                var last = i == parts.Length - 1;
                var next = new List<string>();
                foreach (var dir in current.Where(Directory.Exists))
                {
                    if (parts[i].Contains("*") || parts[i].Contains("?"))
                        next.AddRange(last ? Directory.GetFiles(dir, parts[i]) : Directory.GetDirectories(dir, parts[i]));
                    else
                    {
                        var p = System.IO.Path.Combine(dir, parts[i]);
                        if (last ? File.Exists(p) : Directory.Exists(p)) next.Add(p);
                    }
                }
                current = next;
            }
            return current;
        }
    }
}
