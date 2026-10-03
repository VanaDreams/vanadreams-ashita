using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vanadreams.Services
{
    /// <summary>A Windower addon or plugin and the catalogue item that does the same job here.</summary>
    public sealed class Lookalike
    {
        public string Windower { get; set; }
        public string CatalogId { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// What a player coming from Windower is used to, and the catalogue items that stand in for it. The preset
    /// ticks these in one go; each one is still the player's to untick afterwards.
    /// </summary>
    public static class WindowerPreset
    {
        public static readonly IReadOnlyList<Lookalike> Items = new List<Lookalike>
        {
            new Lookalike { Windower = "GearSwap", CatalogId = "luashitacast", Note = "gear swapping, one Lua file per job" },
            new Lookalike { Windower = "GearSwap", CatalogId = "sequencer", Note = "keeps actions from double-firing while gear swaps" },
            new Lookalike { Windower = "XIVParty", CatalogId = "xiui", Note = "party list, target bar and player bar" },
            new Lookalike { Windower = "Battlemod", CatalogId = "simplelog", Note = "a cleaner combat log" },
            new Lookalike { Windower = "Timers", CatalogId = "ttimers", Note = "buff, debuff and recast timers" },
            new Lookalike { Windower = "Balloon", CatalogId = "balloon", Note = "the same addon, ported" },
            new Lookalike { Windower = "Zonename", CatalogId = "zonename", Note = "the same addon, ported" },
            new Lookalike { Windower = "FindAll", CatalogId = "findall", Note = "/fa search <item>" },
            new Lookalike { Windower = "PointWatch", CatalogId = "points", Note = "exp, capacity and sparks on screen" },
            new Lookalike { Windower = "Timestamp", CatalogId = "timestamp", Note = "timestamps in chat" },
            new Lookalike { Windower = "Distance", CatalogId = "distance", Note = "distance to your target" },
        };

        /// <summary>The preset's items that this catalogue has and that can be installed here, each once, in order.</summary>
        public static List<CatalogItem> CatalogItems(Catalog catalog) =>
            Items.Select(l => catalog.Find(l.CatalogId)).Where(i => i != null && i.HasV4 && string.IsNullOrEmpty(i.HeldBack))
                 .Distinct().ToList();

        /// <summary>The look-alike for a Windower addon or plugin name, or null when there is none in the list.</summary>
        public static Lookalike For(string windowerName) =>
            Items.FirstOrDefault(l => string.Equals(l.Windower, windowerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>What came of converting a Windower init.txt.</summary>
    public sealed class InitConversion
    {
        public List<string> Lines { get; } = new List<string>();          // Ashita script lines, comments included
        public int Binds { get; set; }
        public int Aliases { get; set; }
        public List<string> Skipped { get; } = new List<string>();        // the Windower lines that have no Ashita line
        public List<string> WindowerAddons { get; } = new List<string>(); // what init.txt loaded, by name
    }

    /// <summary>
    /// Turns the binds and aliases of a Windower init.txt into Ashita script lines. The two use the same key names
    /// (DirectInput's) and nearly the same bind syntax: Windower writes shift as ~ where Ashita writes +, and a
    /// Windower bind sends a game command through "input". Anything that is a Windower command rather than a game
    /// command has no counterpart, so it is written as a comment naming why, never dropped silently.
    /// </summary>
    public static class WindowerInit
    {
        public const string BeginMarker = "# --- from Windower init.txt ---";
        public const string EndMarker = "# --- end of Windower init.txt ---";

        public static InitConversion Convert(string initText)
        {
            var r = new InitConversion();
            var chatClosedOnly = false;
            foreach (var raw in (initText ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("--") || line.StartsWith("#")) continue;
                // Windower commands may be typed with a leading // in chat, or one slash in a script
                if (line.StartsWith("/") && !line.StartsWith("//")) line = line.TrimStart('/');

                var word = FirstWord(line, out var rest);
                switch (word.ToLowerInvariant())
                {
                    case "bind":
                    {
                        var converted = Bind(rest, out var why, ref chatClosedOnly);
                        if (converted != null) { r.Lines.Add(converted); r.Binds++; }
                        else Skip(r, raw, why);
                        break;
                    }
                    case "alias":
                    {
                        var converted = Alias(rest, out var why);
                        if (converted != null) { r.Lines.Add(converted); r.Aliases++; }
                        else Skip(r, raw, why);
                        break;
                    }
                    case "lua":
                    {
                        var sub = FirstWord(rest, out var name).ToLowerInvariant();
                        if ((sub == "load" || sub == "l") && name.Trim().Length > 0) AddLoaded(r, name.Trim());
                        break;
                    }
                    case "load":
                        if (rest.Trim().Length > 0) AddLoaded(r, rest.Trim());
                        break;
                    case "unbind":
                    case "wait":
                    case "exec":
                        break;   // an init.txt starts from nothing, so these carry nothing over
                    default:
                        Skip(r, raw, "a Windower setting with no Ashita line");
                        break;
                }
            }
            if (chatClosedOnly)
            {
                // a line of its own: Ashita reads a # as a comment only at the start of a line
                r.Lines.Insert(0, "/bind block 1");
                r.Lines.Insert(0, "# Windower's % binds fired only with the chat line closed; this does that for every bind.");
            }
            return r;
        }

        private static void Skip(InitConversion r, string raw, string why)
        {
            r.Skipped.Add(raw.Trim());
            r.Lines.Add("# not carried over (" + why + "): " + raw.Trim());
        }

        private static void AddLoaded(InitConversion r, string name)
        {
            name = name.Trim().Trim('"');
            if (!r.WindowerAddons.Contains(name, StringComparer.OrdinalIgnoreCase)) r.WindowerAddons.Add(name);
        }

        private static string FirstWord(string s, out string rest)
        {
            s = (s ?? "").TrimStart();
            var i = 0;
            while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
            rest = i < s.Length ? s.Substring(i).TrimStart() : "";
            return s.Substring(0, i);
        }

        /// <summary>One bind, as an Ashita /bind line; null with the reason when it cannot be one.</summary>
        public static string Bind(string args, out string why, ref bool chatClosedOnly)
        {
            why = null;
            var key = FirstWord(args, out var rest);
            if (key.Length == 0) { why = "no key"; return null; }

            var mods = new StringBuilder();
            var i = 0;
            for (; i < key.Length && "^!~@#%$".IndexOf(key[i]) >= 0 && i < key.Length - 1; i++)
            {
                switch (key[i])
                {
                    case '~': mods.Append('+'); break;   // Windower's shift
                    case '%': chatClosedOnly = true; break;
                    default: mods.Append(key[i]); break;
                }
            }
            var name = key.Substring(i);

            var updown = "";
            var next = FirstWord(rest, out var afterUpDown);
            if (next.Equals("up", StringComparison.OrdinalIgnoreCase) || next.Equals("down", StringComparison.OrdinalIgnoreCase))
            {
                updown = next.ToLowerInvariant() + " ";
                rest = afterUpDown;
            }

            var command = GameCommand(Unquote(rest), out why);
            if (command == null) return null;
            return "/bind " + mods + name + " " + updown + command;
        }

        /// <summary>One alias, as an Ashita /alias line; null with the reason when it cannot be one.</summary>
        public static string Alias(string args, out string why)
        {
            why = null;
            var name = FirstWord(args, out var rest);
            if (name.Length == 0) { why = "no name"; return null; }
            var command = GameCommand(Unquote(rest), out why);
            if (command == null) return null;
            return "/alias add /" + name.TrimStart('/') + " " + command;
        }

        /// <summary>
        /// The game command a Windower command sends, or null. "input /ma ..." sends /ma ...; a bare game command
        /// is already one. A Windower command (gs c, send, //...) or a chain of them has no Ashita counterpart.
        /// </summary>
        private static string GameCommand(string command, out string why)
        {
            why = null;
            command = (command ?? "").Trim();
            if (command.Length == 0) { why = "no command"; return null; }
            if (HasChain(command)) { why = "more than one command in a row"; return null; }
            var word = FirstWord(command, out var rest);
            if (word.Equals("input", StringComparison.OrdinalIgnoreCase)) command = rest.Trim();
            if (command.StartsWith("//") || !command.StartsWith("/"))
            {
                why = "a Windower command, not a game command";
                return null;
            }
            return command;
        }

        // A ; outside quotes separates Windower commands
        private static bool HasChain(string command)
        {
            var quoted = false;
            foreach (var c in command)
            {
                if (c == '"') quoted = !quoted;
                else if (c == ';' && !quoted) return true;
            }
            return false;
        }

        // A whole command in quotes, the way some init.txt files write it, with \" inside for the quotes it holds
        private static string Unquote(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length < 2 || s[0] != '"' || s[s.Length - 1] != '"') return s;
            var inner = s.Substring(1, s.Length - 2);
            return inner.Replace("\\\"", "").IndexOf('"') >= 0 ? s : inner.Replace("\\\"", "\"");
        }

        /// <summary>
        /// The startup script with the imported lines in the player's own part of it, under the yours marker, in
        /// place of an earlier import. Everything else in that part is left exactly as it was.
        /// </summary>
        public static string MergeIntoScript(string scriptText, IEnumerable<string> lines)
        {
            var nl = Environment.NewLine;
            var all = (scriptText ?? "").Replace("\r\n", "\n").Split('\n').ToList();
            if (all.Count > 0 && all[all.Count - 1].Length == 0) all.RemoveAt(all.Count - 1);

            var begin = all.FindIndex(l => l.Trim() == BeginMarker);
            if (begin >= 0)
            {
                var end = all.FindIndex(begin, l => l.Trim() == EndMarker);
                all.RemoveRange(begin, (end < 0 ? all.Count : end + 1) - begin);
                while (begin < all.Count && begin > 0 && all[begin].Trim().Length == 0 && all[begin - 1].Trim().Length == 0) all.RemoveAt(begin);
            }

            if (all.FindIndex(l => l.Trim() == ScriptWriter.YoursMarker) < 0) all.Add(ScriptWriter.YoursMarker);
            while (all.Count > 0 && all[all.Count - 1].Trim().Length == 0) all.RemoveAt(all.Count - 1);

            all.Add("");
            all.Add(BeginMarker);
            all.Add("# Imported by the launcher. Import again to replace this block; edit freely otherwise.");
            all.AddRange(lines);
            all.Add(EndMarker);
            return string.Join(nl, all) + nl;
        }
    }
}
