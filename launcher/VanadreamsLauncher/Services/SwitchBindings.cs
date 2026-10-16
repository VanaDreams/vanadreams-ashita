using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Vanadreams.Services
{
    /// <summary>A key the window switcher listens for, with its Ctrl/Alt/Shift. Kept in settings as text, e.g. "Ctrl+F1".</summary>
    public sealed class KeyBinding
    {
        public Keys Key { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }

        /// <summary>Null for blank or unreadable text: that binding is off.</summary>
        public static KeyBinding Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var b = new KeyBinding();
            var parts = text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count == 0) return null;
            for (var i = 0; i < parts.Count - 1; i++)
            {
                switch (parts[i].ToLowerInvariant())
                {
                    case "ctrl": case "control": b.Ctrl = true; break;
                    case "alt": b.Alt = true; break;
                    case "shift": b.Shift = true; break;
                    default: return null;
                }
            }
            Keys key;
            var last = parts[parts.Count - 1];
            if (last.Equals("Scroll Lock", StringComparison.OrdinalIgnoreCase)) last = "Scroll";
            if (last.All(char.IsDigit)) return null;   // Enum.TryParse would read "1" as the mouse button; the 1 key is "D1"
            if (!Enum.TryParse(last, true, out key) || IsModifier(key) || key == Keys.None) return null;
            b.Key = key;
            return b;
        }

        public static bool IsModifier(Keys key) =>
            key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.Control ||
            key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu || key == Keys.Alt ||
            key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.Shift ||
            key == Keys.LWin || key == Keys.RWin;

        public bool Matches(int vk, bool ctrl, bool alt, bool shift) => (int)Key == vk && Ctrl == ctrl && Alt == alt && Shift == shift;

        /// <summary>Settings text: modifiers then the key's own name, which Parse reads back.</summary>
        public override string ToString() => (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + Key;

        /// <summary>For a button: the names a player knows (Scroll Lock, Page Up, 1).</summary>
        public static string Words(string text)
        {
            var b = Parse(text);
            if (b == null) return "none";
            return (b.Ctrl ? "Ctrl+" : "") + (b.Alt ? "Alt+" : "") + (b.Shift ? "Shift+" : "") + KeyWords(b.Key);
        }

        private static string KeyWords(Keys k)
        {
            switch (k)
            {
                case Keys.Scroll: return "Scroll Lock";
                case Keys.PageUp: return "Page Up";
                case Keys.PageDown: return "Page Down";
                case Keys.Oemtilde: return "`";
                case Keys.Capital: return "Caps Lock";
            }
            if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (int)(k - Keys.NumPad0);
            return k.ToString();
        }
    }

    /// <summary>
    /// Controller buttons for the switcher: XInput's button bits, kept in settings as text, e.g. "Back+RB".
    /// The game reads the pad too, so the offered choices are presses FFXI does not use on their own.
    /// </summary>
    public static class PadBinding
    {
        public const ushort DpadUp = 0x0001, DpadDown = 0x0002, DpadLeft = 0x0004, DpadRight = 0x0008;
        public const ushort Start = 0x0010, Back = 0x0020, LeftStick = 0x0040, RightStick = 0x0080;
        public const ushort LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

        private static readonly Dictionary<string, ushort> Names = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            { "Up", DpadUp }, { "Down", DpadDown }, { "Left", DpadLeft }, { "Right", DpadRight },
            { "Start", Start }, { "Back", Back }, { "LS", LeftStick }, { "RS", RightStick },
            { "LB", LB }, { "RB", RB }, { "A", A }, { "B", B }, { "X", X }, { "Y", Y },
        };

        /// <summary>What the page's lists offer; the first is "off".</summary>
        public static readonly string[] Choices = { "", "Back+RB", "Back+LB", "Back+Right", "Back+Left", "LS+RB", "LS+LB", "RS" };

        public static ushort Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            ushort mask = 0;
            foreach (var p in text.Split('+'))
            {
                ushort bit;
                if (!Names.TryGetValue(p.Trim(), out bit)) return 0;
                mask |= bit;
            }
            return mask;
        }

        public static string Words(string text)
        {
            if (Parse(text) == 0) return "Off";
            return string.Join(" + ", text.Split('+').Select(p =>
            {
                switch (p.Trim().ToUpperInvariant())
                {
                    case "LS": return "Left stick press";
                    case "RS": return "Right stick press";
                    case "BACK": return "Back";
                    case "LEFT": return "D-pad left";
                    case "RIGHT": return "D-pad right";
                    case "UP": return "D-pad up";
                    case "DOWN": return "D-pad down";
                    default: return p.Trim();
                }
            }));
        }

        /// <summary>True the moment every button in the chord is down, once per press.</summary>
        public static bool Fired(ushort chord, ushort before, ushort now) =>
            chord != 0 && (now & chord) == chord && (before & chord) != chord;
    }

    /// <summary>One FFXI window the switcher found.</summary>
    public sealed class GameWindow
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        /// <summary>The character's name from the title, or null while it still shows a generic title (logging in).</summary>
        public string Name { get; set; }
        public bool Elevated { get; set; }
        public string Label(int slot) => Name ?? ("Slot " + (slot + 1) + " · logging in");
    }

    /// <summary>The pure parts of the switcher: names from titles, slot order, layouts. Kept apart so they are tested.</summary>
    public static class SwitchRules
    {
        private static readonly Regex CharName = new Regex("^[A-Za-z]{3,15}$");

        /// <summary>
        /// The character name in an FFXI window's title, or null. Titles differ by loader: "Terry", "Ashita v4 - Terry",
        /// "Terry - FINAL FANTASY XI". A name is the one part that is a plain 3-15 letter word and not the game's own.
        /// </summary>
        public static string NameFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var parts = title.Split(new[] { " - ", " | ", " : " }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Reverse();
            foreach (var p in parts)
            {
                if (!CharName.IsMatch(p)) continue;
                var low = p.ToLowerInvariant();
                if (low == "ashita" || low == "windower" || low == "playonline" || low == "vanadreams" || low == "ffxi") continue;
                return p;
            }
            return null;
        }

        /// <summary>
        /// The windows in slot order. Windows already listed keep their place; new ones go to the end; then the
        /// named ones are put in the order the player last arranged them (saved by name), the rest after.
        /// </summary>
        public static List<GameWindow> Arrange(IList<GameWindow> current, IList<GameWindow> found, IList<string> savedOrder)
        {
            var byHandle = found.ToDictionary(w => w.Handle);
            var list = current.Where(w => byHandle.ContainsKey(w.Handle)).Select(w => byHandle[w.Handle]).ToList();
            list.AddRange(found.Where(w => !current.Any(c => c.Handle == w.Handle)));
            if (savedOrder == null || savedOrder.Count == 0) return list;
            Func<GameWindow, int> rank = w =>
            {
                var i = w.Name == null ? -1 : savedOrder.ToList().FindIndex(n => string.Equals(n, w.Name, StringComparison.OrdinalIgnoreCase));
                return i < 0 ? int.MaxValue : i;
            };
            // a stable sort: windows with no saved place keep their order behind the ones that have one
            return list.Select((w, i) => new { w, i }).OrderBy(x => rank(x.w)).ThenBy(x => x.i).Select(x => x.w).ToList();
        }

        public static int Next(int index, int count) => count == 0 ? -1 : index < 0 ? 0 : (index + 1) % count;
        public static int Previous(int index, int count) => count == 0 ? -1 : index <= 0 ? count - 1 : index - 1;

        /// <summary>One window's place in a saved layout: "Terry|x|y|w|h", or "#2|…" for slot 2 before its name was known.</summary>
        public static string LayoutEntry(GameWindow w, int slot, int x, int y, int width, int height) =>
            (w.Name ?? "#" + (slot + 1)) + "|" + x + "|" + y + "|" + width + "|" + height;

        /// <summary>The place saved for this window: by its name, else by its slot number. Null when the layout has none.</summary>
        public static int[] PlaceFor(IEnumerable<string> layout, GameWindow w, int slot)
        {
            if (layout == null) return null;
            int[] bySlot = null;
            foreach (var entry in layout)
            {
                var f = (entry ?? "").Split('|');
                if (f.Length != 5) continue;
                var r = new int[4];
                var ok = true;
                for (var i = 0; i < 4; i++) ok &= int.TryParse(f[i + 1], out r[i]);
                if (!ok || r[2] <= 0 || r[3] <= 0) continue;
                if (w.Name != null && string.Equals(f[0], w.Name, StringComparison.OrdinalIgnoreCase)) return r;
                if (f[0] == "#" + (slot + 1)) bySlot = r;
            }
            return bySlot;
        }
    }
}
