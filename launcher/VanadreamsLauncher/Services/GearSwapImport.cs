using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vanadreams.Services
{
    /// <summary>One gear set read out of a GearSwap file: its path under sets, and LuAshitacast slot to item.</summary>
    public sealed class GearSet
    {
        public string Name { get; set; }
        public List<KeyValuePair<string, GearItem>> Slots { get; } = new List<KeyValuePair<string, GearItem>>();
    }

    public sealed class GearItem
    {
        public string Name { get; set; }
        public List<string> Augments { get; } = new List<string>();
    }

    /// <summary>What came of reading one job's GearSwap files.</summary>
    public sealed class GearSwapJob
    {
        public string Character { get; set; }
        public string Job { get; set; }
        public List<string> Files { get; } = new List<string>();
        public List<GearSet> Sets { get; } = new List<GearSet>();
        public SortedSet<string> Unresolved { get; } = new SortedSet<string>(StringComparer.Ordinal);   // names the files use that only GearSwap knows
    }

    /// <summary>
    /// Reads the gear sets out of GearSwap job files and writes them as LuAshitacast profiles.
    ///
    /// A GearSwap file is a Lua program, and the launcher does not run it: it reads the assignments the sets are
    /// built from (sets.x = {...}, set_combine(...), a set copied from another, local tables and gear.* names
    /// set in the same file) wherever they are, inside get_sets, init_gear_sets or anywhere else, and skips the
    /// rest. That covers the plain style and Motenten's and Selindrile's, whose sets live in init_gear_sets and
    /// whose gear file sits beside the job file. What GearSwap's libraries fill in at run time (Mote's elemental
    /// gorget, a cycle a function picks) is not in the file, so the slot is left out and named in the profile.
    ///
    /// The rules are not carried over. GearSwap's precast and midcast functions are code against Windower's
    /// API; the profile gets LuAshitacast's handlers instead, which look sets up by the names GearSwap files use
    /// (precast.FC, precast.WS['Savage Blade'], midcast['Enhancing Magic'], engaged, idle, resting).
    /// </summary>
    public static class GearSwapImport
    {
        private static readonly string[] Jobs = { "WAR", "MNK", "WHM", "BLM", "RDM", "THF", "PLD", "DRK", "BST", "BRD", "RNG", "SAM", "NIN", "DRG", "SMN", "BLU", "COR", "PUP", "DNC", "SCH", "GEO", "RUN" };

        // GearSwap's slot names, the long and short ones, to LuAshitacast's, in LuAshitacast's equip order
        private static readonly string[] SlotOrder = { "Main", "Sub", "Range", "Ammo", "Head", "Neck", "Ear1", "Ear2", "Body", "Hands", "Ring1", "Ring2", "Back", "Waist", "Legs", "Feet" };
        private static readonly Dictionary<string, string> Slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main"] = "Main", ["sub"] = "Sub", ["range"] = "Range", ["ranged"] = "Range", ["ammo"] = "Ammo",
            ["head"] = "Head", ["neck"] = "Neck", ["ear1"] = "Ear1", ["left_ear"] = "Ear1", ["lear"] = "Ear1",
            ["ear2"] = "Ear2", ["right_ear"] = "Ear2", ["rear"] = "Ear2", ["body"] = "Body", ["hands"] = "Hands",
            ["ring1"] = "Ring1", ["left_ring"] = "Ring1", ["lring"] = "Ring1", ["ring2"] = "Ring2", ["right_ring"] = "Ring2",
            ["rring"] = "Ring2", ["back"] = "Back", ["waist"] = "Waist", ["legs"] = "Legs", ["feet"] = "Feet",
        };

        public static bool IsSlot(string key) => key != null && Slots.ContainsKey(key);

        /// <summary>The job a file is for, from its name (WHM.lua, Name_WHM.lua, Name_WHM_gear.lua), or null.</summary>
        public static string JobOf(string fileName)
        {
            var parts = Path.GetFileNameWithoutExtension(fileName ?? "").Split('_', '-', ' ', '.');
            return parts.Reverse().Select(p => p.ToUpperInvariant()).FirstOrDefault(p => Jobs.Contains(p));
        }

        /// <summary>The character a file is for, from its name (Name_WHM.lua), or null when the name is only the job.</summary>
        public static string CharacterOf(string fileName)
        {
            var parts = Path.GetFileNameWithoutExtension(fileName ?? "").Split('_');
            if (parts.Length < 2) return null;
            var first = parts[0].Trim();
            return first.Length == 0 || Jobs.Contains(first.ToUpperInvariant()) ? null : first;
        }

        /// <summary>
        /// The character a file is for: from its name, else from the folder it is in when GearSwap's per-character
        /// layout put it there (data\Ferrin\WHM.lua). Null when neither says.
        /// </summary>
        public static string CharacterFor(string fullPath)
        {
            var fromName = CharacterOf(fullPath);
            if (fromName != null) return fromName;
            var folder = Path.GetFileName(Path.GetDirectoryName(fullPath ?? "") ?? "");
            if (string.IsNullOrWhiteSpace(folder)) return null;
            var generic = new[] { "data", "gearswap", "addons", "libs", "desktop", "downloads", "documents" };
            return generic.Contains(folder.ToLowerInvariant()) || folder.Contains(" ") ? null : folder;
        }

        /// <summary>
        /// Reads the files of one job, in order: a gear file after the job file, so its sets win as they do in game.
        /// </summary>
        public static GearSwapJob Read(string character, string job, IEnumerable<KeyValuePair<string, string>> files)
        {
            var result = new GearSwapJob { Character = character, Job = job };
            var lua = new LuaReader();
            foreach (var f in files.OrderBy(f => Path.GetFileNameWithoutExtension(f.Key).EndsWith("_gear", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                result.Files.Add(f.Key);
                lua.Run(f.Value ?? "");
            }
            var sets = lua.Global("sets") as LuaTable;
            if (sets != null) Flatten(sets, "", result, new HashSet<LuaTable>());
            return result;
        }

        /// <summary>Every table under sets that names a slot becomes a set; its child tables are sets of their own.</summary>
        private static void Flatten(LuaTable table, string path, GearSwapJob into, HashSet<LuaTable> onPath)
        {
            if (!onPath.Add(table)) return;
            var slots = SlotsOf(table, path, into.Unresolved);
            if (slots.Count > 0 && path.Length > 0)
            {
                var set = new GearSet { Name = path };
                foreach (var slot in SlotOrder) if (slots.ContainsKey(slot)) set.Slots.Add(new KeyValuePair<string, GearItem>(slot, slots[slot]));
                into.Sets.Add(set);
            }
            foreach (var kv in table.Entries)
            {
                var child = kv.Value as LuaTable;
                var key = kv.Key as string ?? (kv.Key is double d ? d.ToString(CultureInfo.InvariantCulture) : null);
                if (child == null || key == null || IsSlot(key)) continue;
                Flatten(child, path.Length == 0 ? key : path + "." + key, into, onPath);
            }
            onPath.Remove(table);
        }

        private static Dictionary<string, GearItem> SlotsOf(LuaTable t, string path, ISet<string> unresolved)
        {
            var d = new Dictionary<string, GearItem>();
            foreach (var kv in t.Entries)
            {
                var key = kv.Key as string;
                if (key == null || !Slots.TryGetValue(key, out var slot)) continue;
                if (kv.Value is LuaUnknown u) { if (path.Length > 0) unresolved.Add(path + " " + slot + ": " + u.Path); continue; }
                var item = ItemOf(kv.Value);
                if (item != null) d[slot] = item;
            }
            return d;
        }

        private static GearItem ItemOf(object v)
        {
            if (v is string s)
            {
                s = s.Trim();
                if (s.Length == 0) return null;
                return new GearItem { Name = s.Equals("empty", StringComparison.OrdinalIgnoreCase) ? "remove" : s };
            }
            if (v is LuaTable t)
            {
                var name = (t.Get("name") ?? t.Get("Name")) as string;
                if (string.IsNullOrWhiteSpace(name)) return null;
                var item = new GearItem { Name = name.Trim().Equals("empty", StringComparison.OrdinalIgnoreCase) ? "remove" : name.Trim() };
                var augs = (t.Get("augments") ?? t.Get("augment")) as LuaTable;
                if (augs != null) item.Augments.AddRange(augs.Entries.Select(e => e.Value).OfType<string>());
                else if ((t.Get("augments") ?? t.Get("augment")) is string one) item.Augments.Add(one);
                return item;
            }
            return null;
        }

        /// <summary>set_combine as GearSwap does it for gear: the later sets' slots replace the earlier ones', whatever name each slot goes by.</summary>
        internal static LuaTable Combine(IEnumerable<object> args)
        {
            var result = new LuaTable();
            foreach (var a in args.OfType<LuaTable>())
                foreach (var kv in a.Entries)
                {
                    var key = kv.Key as string;
                    if (key == null || !Slots.TryGetValue(key, out var slot)) continue;
                    foreach (var other in result.Entries.Where(e => e.Key is string k && Slots.TryGetValue(k, out var s) && s == slot).Select(e => e.Key).ToList())
                        result.Remove(other);
                    result.Set(key, kv.Value);
                }
            return result;
        }

        // ---------- the LuAshitacast profile ----------

        public static string ProfileFileName(GearSwapJob job) => job.Character + "_" + job.Job + ".lua";

        public static string WriteProfile(GearSwapJob job, string importedOn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--[[");
            sb.AppendLine("    " + job.Character + " " + job.Job + ", imported from GearSwap by the Vanadreams launcher on " + importedOn + ".");
            sb.AppendLine("    From: " + string.Join(", ", job.Files.Select(Path.GetFileName)));
            sb.AppendLine();
            sb.AppendLine("    Your sets came over as they were. Your GearSwap rules did not: the handlers at the bottom");
            sb.AppendLine("    pick sets by the names GearSwap uses (precast.FC, precast.WS['<weapon skill>'], midcast['<skill>'],");
            sb.AppendLine("    engaged, idle, resting). Change them freely; /lac addset <name> saves what you are wearing as a set.");
            if (job.Sets.Any(s => s.Slots.Any(x => x.Value.Augments.Count > 0)))
            {
                sb.AppendLine();
                sb.AppendLine("    Augments are noted beside the items but not matched on. If you carry two of an item with");
                sb.AppendLine("    different augments, give that slot { Name = '...', Augment = { ... } } as LuAshitacast's docs show.");
            }
            if (job.Unresolved.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("    Left out, because GearSwap or one of its libraries fills them in while the game runs:");
                // "set Slot: name" grouped by name, so a gorget Mote picks per weapon skill is one line, not forty
                foreach (var g in job.Unresolved.Select(u => { var i = u.LastIndexOf(": ", StringComparison.Ordinal); return new { Name = u.Substring(i + 2), Set = u.Substring(0, i) }; })
                                                 .GroupBy(x => x.Name).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var where = g.Select(x => x.Set).ToList();
                    sb.AppendLine(("      " + g.Key + ": " + (where.Count <= 3 ? string.Join(", ", where) : where[0] + " and " + (where.Count - 1) + " more sets")).Replace("]]", "] ]"));
                }
            }
            sb.AppendLine("]]");
            sb.AppendLine();
            sb.AppendLine("local profile = {};");
            sb.AppendLine("local sets = {");
            foreach (var set in job.Sets)
            {
                sb.AppendLine("    [" + Quote(set.Name) + "] = {");
                foreach (var kv in set.Slots)
                {
                    sb.Append("        " + kv.Key + " = " + Quote(kv.Value.Name) + ",");
                    if (kv.Value.Augments.Count > 0) sb.Append(" -- " + string.Join(", ", kv.Value.Augments).Replace("\r", " ").Replace("\n", " "));
                    sb.AppendLine();
                }
                sb.AppendLine("    },");
            }
            sb.AppendLine("};");
            sb.AppendLine("profile.Sets = sets;");
            sb.AppendLine();
            sb.Append(Handlers);
            return sb.ToString();
        }

        private static string Quote(string s) => "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", " ") + "'";

        // The rules every imported profile starts from. The first set that exists wins.
        private const string Handlers = @"profile.Packer = {
};

-- The first of these set names that this profile has.
local function pick(...)
    for _, name in ipairs({...}) do
        if name ~= nil and sets[name] ~= nil then return sets[name]; end
    end
    return nil;
end

local function equip(...)
    local set = pick(...);
    if set ~= nil then gFunc.EquipSet(set); end
end

-- Cure III is a Cure, Protectra V a Protectra, the way GearSwap's spell maps group them.
local families = {
    { '^Cure', 'Cure' }, { '^Curaga', 'Curaga' }, { '^Cura', 'Cure' }, { '^Regen', 'Regen' }, { '^Refresh', 'Refresh' },
    { '^Protectra', 'Protectra' }, { '^Shellra', 'Shellra' }, { '^Protect', 'Protect' }, { '^Shell', 'Shell' },
    { '^Bar', 'BarElement' }, { 'na$', 'StatusRemoval' }, { '^Erase', 'StatusRemoval' }, { '^Raise', 'Raise' },
    { '^Utsusemi', 'Utsusemi' }, { 'Waltz', 'Waltz' }, { 'Samba', 'Samba' }, { 'Step$', 'Step' },
};

local function family(name)
    for _, f in ipairs(families) do
        if name:find(f[1]) then return f[2]; end
    end
    return (name:gsub(' [IVX]+$', ''));
end

profile.OnLoad = function()
    gSettings.AllowAddSet = true;
end

profile.OnUnload = function()
end

profile.HandleCommand = function(args)
end

profile.HandleDefault = function()
    local player = gData.GetPlayer();
    if player.Status == 'Engaged' then
        equip('engaged', 'Engaged', 'TP.Normal', 'TP', 'aftercast.Engaged', 'aftercast.TP', 'melee');
    elseif player.Status == 'Resting' then
        equip('resting', 'Resting', 'aftercast.Resting', 'idle', 'Idle', 'aftercast.Idle');
    else
        equip('idle', 'Idle', 'aftercast.Idle');
    end
end

profile.HandleAbility = function()
    local action = gData.GetAction();
    equip('precast.JA.' .. action.Name, 'JA.' .. action.Name, 'precast.' .. action.Name, 'precast.' .. family(action.Name), 'precast.JA');
end

profile.HandleItem = function()
end

profile.HandlePrecast = function()
    local action = gData.GetAction();
    local skill = action.Skill or '';
    equip('precast.FC.' .. action.Name, 'precast.FC.' .. family(action.Name), 'precast.FC.' .. skill,
          'precast.FastCast.' .. action.Name, 'precast.FastCast.' .. skill, 'precast.' .. action.Name, 'precast.' .. family(action.Name),
          'precast.FC', 'precast.FastCast.Default', 'precast.FastCast', 'FastCast', 'FC', 'precast.Magic');
end

profile.HandleMidcast = function()
    local action = gData.GetAction();
    equip('midcast.' .. action.Name, 'midcast.' .. family(action.Name), 'midcast.' .. (action.Skill or ''),
          'midcast.FastRecast', 'midcast.Magic', 'midcast.magic_base');
end

profile.HandlePreshot = function()
    equip('precast.RA', 'precast.Ranged', 'Preshot');
end

profile.HandleMidshot = function()
    equip('midcast.RA', 'midcast.Ranged', 'Midshot');
end

profile.HandleWeaponskill = function()
    local action = gData.GetAction();
    equip('precast.WS.' .. action.Name, 'WS.' .. action.Name, 'precast.WS', 'WS');
end

return profile;
";
    }

    /// <summary>A value only GearSwap or its libraries know, while the game runs: gear.ElementalGorget, a function's result.</summary>
    internal sealed class LuaUnknown
    {
        public string Path { get; }
        public LuaUnknown(string path) { Path = path; }
    }

    /// <summary>A Lua table, keys in the order they were first set.</summary>
    internal sealed class LuaTable
    {
        private readonly List<object> _order = new List<object>();
        private readonly Dictionary<object, object> _values = new Dictionary<object, object>();
        private int _next = 1;

        public IEnumerable<KeyValuePair<object, object>> Entries => _order.Select(k => new KeyValuePair<object, object>(k, _values[k]));
        public object Get(object key) => key != null && _values.TryGetValue(Norm(key), out var v) ? v : null;

        public void Set(object key, object value)
        {
            if (key == null) return;
            key = Norm(key);
            if (value == null) { Remove(key); return; }
            if (!_values.ContainsKey(key)) _order.Add(key);
            _values[key] = value;
        }

        public void Add(object value) => Set((double)_next++, value);
        public void Remove(object key) { key = Norm(key); if (_values.Remove(key)) _order.Remove(key); }

        private static object Norm(object key) => key is int i ? (double)i : key;
    }

    /// <summary>
    /// Just enough of Lua to follow the assignments a GearSwap file builds its sets from. It reads statements of the
    /// form [local] name.path[...] = expression wherever they are and evaluates the expression from what it has
    /// seen so far; any other statement is stepped over a token at a time. Nothing in the file is ever executed.
    /// </summary>
    internal sealed class LuaReader
    {
        private enum T { Name, String, Number, Sym, End }
        private struct Tok { public T Kind; public string Text; public double Num; }

        private readonly LuaTable _globals = new LuaTable();
        private List<Tok> _toks;
        private int _p;

        public LuaReader()
        {
            // what GearSwap and Mote make before a file's code runs
            var sets = new LuaTable();
            foreach (var k in new[] { "precast", "midcast" }) sets.Set(k, new LuaTable());
            ((LuaTable)sets.Get("precast")).Set("JA", new LuaTable());
            ((LuaTable)sets.Get("precast")).Set("WS", new LuaTable());
            _globals.Set("sets", sets);
            var gear = new LuaTable();
            gear.Set("default", new LuaTable());
            _globals.Set("gear", gear);
            _globals.Set("empty", "empty");   // GearSwap's name for an empty slot
        }

        public object Global(string name) => _globals.Get(name);

        public void Run(string source)
        {
            _toks = Lex(source);
            _p = 0;
            var guard = 0;
            while (Peek.Kind != T.End && guard++ < 2_000_000)
            {
                var start = _p;
                if (!TryAssignment()) _p = start + 1;
            }
        }

        // ---- statements ----

        private bool TryAssignment()
        {
            if (IsWord("local"))
            {
                _p++;
                if (IsWord("function")) return false;
            }
            if (Peek.Kind != T.Name || Keywords.Contains(Peek.Text)) return false;

            // the target: name, then .field or [expr] any number of times
            var root = Next().Text;
            var keys = new List<object>();
            while (true)
            {
                if (IsSym(".") && _toks[_p + 1].Kind == T.Name) { _p++; keys.Add(Next().Text); continue; }
                if (IsSym("["))
                {
                    _p++;
                    var k = Expr();
                    if (!IsSym("]")) return false;
                    _p++;
                    if (k == null || k is LuaUnknown || k is LuaTable) return false;
                    keys.Add(k);
                    continue;
                }
                break;
            }
            if (!IsSym("=")) return false;
            _p++;
            var value = Expr();

            if (keys.Count == 0) { _globals.Set(root, value); return true; }
            var table = _globals.Get(root) as LuaTable;
            if (table == null) { if (_globals.Get(root) != null) return true; table = new LuaTable(); _globals.Set(root, table); }
            for (var i = 0; i < keys.Count - 1; i++)
            {
                var child = table.Get(keys[i]) as LuaTable;
                if (child == null) { if (table.Get(keys[i]) != null) return true; child = new LuaTable(); table.Set(keys[i], child); }
                table = child;
            }
            table.Set(keys[keys.Count - 1], value);
            return true;
        }

        // ---- expressions: or / and / .. over primaries, all GearSwap sets need ----

        private object Expr()
        {
            var left = Concat();
            while (IsWord("or") || IsWord("and"))
            {
                var or = Next().Text == "or";
                var right = Concat();
                left = or ? (Truthy(left) ? left : right) : (Truthy(left) ? right : left);
            }
            return left;
        }

        private object Concat()
        {
            var left = Primary();
            while (IsSym(".."))
            {
                _p++;
                var right = Primary();
                if (left is LuaUnknown || right is LuaUnknown) left = left as LuaUnknown ?? right;
                else left = (left is string || left is double) && (right is string || right is double) ? Str(left) + Str(right) : null;
            }
            return left;
        }

        private object Primary()
        {
            var t = Peek;
            switch (t.Kind)
            {
                case T.String: _p++; return t.Text;
                case T.Number: _p++; return t.Num;
                case T.Sym:
                    if (t.Text == "{") return Table();
                    if (t.Text == "(") { _p++; var v = Expr(); if (IsSym(")")) _p++; return Suffixes(v, null); }
                    if (t.Text == "-" && _toks[_p + 1].Kind == T.Number) { _p++; return -Next().Num; }
                    return null;
                case T.Name:
                    if (t.Text == "true") { _p++; return true; }
                    if (t.Text == "false") { _p++; return false; }
                    if (t.Text == "nil") { _p++; return null; }
                    if (t.Text == "function") return null;
                    if (Keywords.Contains(t.Text)) return null;
                    _p++;
                    return Suffixes(_globals.Get(t.Text), t.Text);
            }
            return null;
        }

        // .field, [expr] and calls after a name
        private object Suffixes(object value, string path)
        {
            while (true)
            {
                if (IsSym(".") && _toks[_p + 1].Kind == T.Name)
                {
                    _p++;
                    var key = Next().Text;
                    path = path == null ? null : path + "." + key;
                    value = value is LuaUnknown ? new LuaUnknown(path ?? "?") : (value as LuaTable)?.Get(key);
                    continue;
                }
                if (IsSym("["))
                {
                    _p++;
                    var key = Expr();
                    if (IsSym("]")) _p++;
                    path = path == null ? null : path + "[" + (key is string s ? "'" + s + "'" : Str(key)) + "]";
                    value = value is LuaUnknown || key is LuaUnknown ? new LuaUnknown(path ?? "?") : key == null ? null : (value as LuaTable)?.Get(key);
                    continue;
                }
                if (IsSym("(") || IsSym("{") || Peek.Kind == T.String)
                {
                    var args = Args();
                    var fn = path ?? "";
                    if (fn == "set_combine" || fn.EndsWith(".set_combine")) value = GearSwapImport.Combine(args);
                    else value = new LuaUnknown((path ?? "a function") + "(...)");
                    path = null;
                    continue;
                }
                if (IsSym(":") && _toks[_p + 1].Kind == T.Name) { _p += 2; continue; }   // a method: its call is read next and comes to nothing
                break;
            }
            if (value == null && path != null && !path.StartsWith("sets", StringComparison.Ordinal)) return new LuaUnknown(path);
            return value;
        }

        private List<object> Args()
        {
            var args = new List<object>();
            if (IsSym("{")) { args.Add(Table()); return args; }
            if (Peek.Kind == T.String) { args.Add(Next().Text); return args; }
            _p++;   // (
            while (!IsSym(")") && Peek.Kind != T.End)
            {
                var before = _p;
                args.Add(Expr());
                if (IsSym(",")) { _p++; continue; }
                if (!IsSym(")")) SkipTo(")");
                if (_p == before) break;   // a stray closing bracket: the call ends here
            }
            if (IsSym(")")) _p++;
            return args;
        }

        private LuaTable Table()
        {
            var table = new LuaTable();
            _p++;   // {
            while (!IsSym("}") && Peek.Kind != T.End)
            {
                var before = _p;
                if (IsSym("["))
                {
                    _p++;
                    var key = Expr();
                    if (IsSym("]")) _p++;
                    if (IsSym("=")) { _p++; table.Set(key, Expr()); }
                }
                else if (Peek.Kind == T.Name && !Keywords.Contains(Peek.Text) && _toks[_p + 1].Kind == T.Sym && _toks[_p + 1].Text == "=")
                {
                    var key = Next().Text;
                    _p++;
                    table.Set(key, Expr());
                }
                else table.Add(Expr());

                if (IsSym(",") || IsSym(";")) { _p++; continue; }
                if (!IsSym("}")) SkipTo("}", ",", ";");   // something this reader does not follow: step past it
                if (IsSym(",") || IsSym(";")) _p++;
                if (_p == before) _p++;
            }
            if (IsSym("}")) _p++;
            return table;
        }

        // Moves to the next of the given symbols at this nesting depth, or to the closing bracket of the one we are in.
        private void SkipTo(params string[] stops)
        {
            var depth = 0;
            while (Peek.Kind != T.End)
            {
                var t = Peek;
                if (t.Kind == T.Sym)
                {
                    if (depth == 0 && stops.Contains(t.Text)) return;
                    if (t.Text == "(" || t.Text == "{" || t.Text == "[") depth++;
                    else if (t.Text == ")" || t.Text == "}" || t.Text == "]") { if (depth == 0) return; depth--; }
                }
                _p++;
            }
        }

        // ---- helpers ----

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in",
            "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
        };

        private Tok Peek => _toks[_p];
        private Tok Next() => _toks[_p++];
        private bool IsSym(string s) => _toks[_p].Kind == T.Sym && _toks[_p].Text == s;
        private bool IsWord(string s) => _toks[_p].Kind == T.Name && _toks[_p].Text == s;
        private static bool Truthy(object v) => v != null && !(v is LuaUnknown) && !(v is bool b && !b);
        private static string Str(object v) => v is double d ? d.ToString(CultureInfo.InvariantCulture) : v as string ?? "";

        private static List<Tok> Lex(string s)
        {
            var toks = new List<Tok>();
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '-' && At(s, i + 1) == '-')
                {
                    i += 2;
                    var level = LongBracket(s, i);
                    if (level >= 0) { i = SkipLong(s, i, level, out _); continue; }
                    while (i < s.Length && s[i] != '\n') i++;
                    continue;
                }
                if (c == '[' && LongBracket(s, i) >= 0)
                {
                    i = SkipLong(s, i, LongBracket(s, i), out var text);
                    toks.Add(new Tok { Kind = T.String, Text = text });
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    var sb = new StringBuilder();
                    i++;
                    while (i < s.Length && s[i] != c && s[i] != '\n')
                    {
                        if (s[i] == '\\' && i + 1 < s.Length)
                        {
                            var e = s[i + 1];
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                            i += 2;
                            continue;
                        }
                        sb.Append(s[i++]);
                    }
                    i++;
                    toks.Add(new Tok { Kind = T.String, Text = sb.ToString() });
                    continue;
                }
                if (char.IsDigit(c) || (c == '.' && char.IsDigit(At(s, i + 1))))
                {
                    var start = i;
                    if (c == '0' && (At(s, i + 1) == 'x' || At(s, i + 1) == 'X'))
                    {
                        i += 2;
                        while (i < s.Length && Uri.IsHexDigit(s[i])) i++;
                        long.TryParse(s.Substring(start + 2, i - start - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hx);
                        toks.Add(new Tok { Kind = T.Number, Num = hx, Text = s.Substring(start, i - start) });
                        continue;
                    }
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E')))) i++;
                    double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var n);
                    toks.Add(new Tok { Kind = T.Number, Num = n, Text = s.Substring(start, i - start) });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    toks.Add(new Tok { Kind = T.Name, Text = s.Substring(start, i - start) });
                    continue;
                }
                var three = i + 3 <= s.Length ? s.Substring(i, 3) : "";
                var two = i + 2 <= s.Length ? s.Substring(i, 2) : "";
                if (three == "...") { toks.Add(new Tok { Kind = T.Sym, Text = three }); i += 3; continue; }
                if (two == "==" || two == "~=" || two == "<=" || two == ">=" || two == ".." || two == "::" || two == "//" || two == "<<" || two == ">>")
                { toks.Add(new Tok { Kind = T.Sym, Text = two }); i += 2; continue; }
                toks.Add(new Tok { Kind = T.Sym, Text = c.ToString() });
                i++;
            }
            toks.Add(new Tok { Kind = T.End, Text = "" });
            toks.Add(new Tok { Kind = T.End, Text = "" });
            return toks;
        }

        private static char At(string s, int i) => i < s.Length ? s[i] : '\0';

        // [[ or [==[ at i: the number of = signs, or -1 when it is not a long bracket
        private static int LongBracket(string s, int i)
        {
            if (At(s, i) != '[') return -1;
            var j = i + 1;
            while (At(s, j) == '=') j++;
            return At(s, j) == '[' ? j - i - 1 : -1;
        }

        private static int SkipLong(string s, int i, int level, out string text)
        {
            var open = i + level + 2;
            var close = "]" + new string('=', level) + "]";
            var end = s.IndexOf(close, open, StringComparison.Ordinal);
            if (end < 0) { text = s.Substring(open); return s.Length; }
            text = s.Substring(open, end - open);
            if (text.StartsWith("\r\n")) text = text.Substring(2); else if (text.StartsWith("\n")) text = text.Substring(1);
            return end + close.Length;
        }
    }
}
