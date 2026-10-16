using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Vanadreams.Services
{
    public enum GfxPreset { Potato, Balanced, Pretty }

    /// <summary>What a preset sets: the lighting and sharpening effects (d3d8to9 and ReShade, Effects) and the frame rate.</summary>
    public sealed class GfxLook
    {
        public bool Effects { get; set; }
        public int Fps { get; set; }
    }

    /// <summary>One attached display, as the Graphics page lists it.</summary>
    public sealed class MonitorInfo
    {
        public string Device { get; set; }
        public string Label { get; set; }
        public bool Primary { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public override string ToString() => Label;
    }

    /// <summary>
    /// The Graphics page, kept per profile and put in place at every Play for the profile being played:
    ///   - the effects are renamed on or off in Ashita's bootloader folder (Effects), which is shared, so Play sets them each time;
    ///   - the frame rate goes in scripts\vanadreams-graphics.txt, which the startup script runs: the bundled fps addon's
    ///     divisor (1 is 60 fps, 2 is 30, the game's own); it can set no other rate;
    ///   - the display goes in the profile's [ashita.window.startpos], from where that display sits on the desktop today.
    /// The modern look pack and the title pack are unpacked into the game's own files, so no preset can take them away.
    /// A profile with no preset picked keeps what it had: the Low spec setting and the game's own frame rate.
    /// </summary>
    public static class Graphics
    {
        public const string ScriptName = "vanadreams-graphics.txt";
        public static readonly int[] FpsChoices = { 30, 60 };

        public static GfxLook Look(GfxPreset p)
        {
            switch (p)
            {
                case GfxPreset.Potato: return new GfxLook { Effects = false, Fps = 30 };
                case GfxPreset.Balanced: return new GfxLook { Effects = false, Fps = 60 };
                default: return new GfxLook { Effects = true, Fps = 60 };
            }
        }

        public static GfxPreset? Parse(string text)
        {
            GfxPreset p;
            return Enum.TryParse(text ?? "", true, out p) && Enum.IsDefined(typeof(GfxPreset), p) ? p : (GfxPreset?)null;
        }

        /// <summary>Pick a preset for a profile: its effects and its frame rate together.</summary>
        public static void Choose(Profile p, GfxPreset preset)
        {
            p.GfxPreset = preset.ToString().ToLowerInvariant();
            p.Fps = Look(preset).Fps;
        }

        /// <summary>Effects on for this profile: its preset's, or the Low spec setting when it has none.</summary>
        public static bool EffectsFor(Profile p, LauncherSettings s)
        {
            var preset = Parse(p?.GfxPreset);
            return preset.HasValue ? Look(preset.Value).Effects : !s.LowSpec;
        }

        /// <summary>The fps addon's divisor: 1 for 60 fps, 2 for 30.</summary>
        public static int Divisor(int fps) => fps >= 60 ? 1 : 2;

        /// <summary>
        /// The lines scripts\vanadreams-graphics.txt holds for a frame rate. 0 (never picked) and 30 (the game's own) need
        /// nothing. For 60 the fps addon is loaded if the player has not ticked it, and its counter hidden, since they did
        /// not ask for one.
        /// </summary>
        public static List<string> ScriptLines(int fps, bool fpsAddonTicked)
        {
            var lines = new List<string> { "# Written by the launcher at every Play, from the profile's Graphics page." };
            if (fps <= 0) return lines;
            if (fps < 60 && !fpsAddonTicked) return lines;   // 30 is the game's own rate
            if (!fpsAddonTicked) lines.Add("/addon load fps");
            lines.Add("/wait 2");
            lines.Add("/fps " + Divisor(fps));
            if (!fpsAddonTicked) lines.Add("/fps hide");
            return lines;
        }

        public static string ScriptPath(string ashitaRoot) => Path.Combine(ashitaRoot, "scripts", ScriptName);

        /// <summary>The startup script runs this file, so it must be there even before any Play.</summary>
        public static void EnsureScript(string ashitaRoot)
        {
            if (string.IsNullOrWhiteSpace(ashitaRoot)) return;
            var path = ScriptPath(ashitaRoot);
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, ScriptLines(0, false), new UTF8Encoding(false));
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct DEVMODE
        {
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE mode);

        /// <summary>
        /// The attached displays, the main one first, in real pixels and desktop positions from the driver's current mode.
        /// Windows' screen list scales every display by the main one's DPI, so with the main screen at 150% a 1920x1080
        /// display read as 2880x1620 (16 Oct 2026, Ferrin's laptop and second screen).
        /// </summary>
        public static List<MonitorInfo> Monitors()
        {
            var list = new List<MonitorInfo>();
            var screens = System.Windows.Forms.Screen.AllScreens.OrderByDescending(s => s.Primary).ThenBy(s => s.Bounds.Left).ToList();
            for (var i = 0; i < screens.Count; i++)
            {
                var s = screens[i];
                int left = s.Bounds.Left, top = s.Bounds.Top, w = s.Bounds.Width, h = s.Bounds.Height;
                var mode = new DEVMODE { dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(DEVMODE)) };
                if (EnumDisplaySettings(s.DeviceName, -1, ref mode) && mode.dmPelsWidth > 0)   // -1: the current settings
                {
                    left = mode.dmPositionX; top = mode.dmPositionY; w = mode.dmPelsWidth; h = mode.dmPelsHeight;
                }
                list.Add(new MonitorInfo
                {
                    Device = s.DeviceName, Primary = s.Primary, Left = left, Top = top, Width = w, Height = h,
                    Label = (s.Primary ? "Main" : "Display " + (i + 1)) + " · " + w + "×" + h
                });
            }
            return list;
        }

        /// <summary>
        /// Where the game window opens on a display: its top-left corner for borderless and fullscreen, centred for a
        /// window. -1, -1 (Windows chooses) when no display was picked or the window is bigger than the display.
        /// </summary>
        public static Tuple<int, int> StartFor(MonitorInfo m, WindowMode mode, int width, int height)
        {
            if (m == null) return Tuple.Create(-1, -1);
            if (mode != WindowMode.Windowed || width <= 0 || height <= 0 || width >= m.Width || height >= m.Height)
                return Tuple.Create(m.Left, m.Top);
            return Tuple.Create(m.Left + (m.Width - width) / 2, m.Top + (m.Height - height) / 2);
        }

        /// <summary>Put this profile's graphics in place before it launches. Never throws: a launch is not stopped for it.</summary>
        public static void ApplyForLaunch(string ashitaRoot, Profile p, LauncherSettings s)
        {
            try
            {
                global::Vanadreams.Services.Effects.Apply(ashitaRoot, !EffectsFor(p, s));
                var path = ScriptPath(ashitaRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var ticked = s.EnabledAddons.Contains("fps", StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(path, ScriptLines(p.Fps, ticked), new UTF8Encoding(false));
                // the display: placed from where it sits today, so a rearranged desk still lands on the right one. A borderless
                // or fullscreen window always starts at its display's corner, the main one when none was picked: left to
                // Windows, a borderless window smaller than the screen opened wherever it liked (16 Oct 2026, Ferrin's 2560x1440
                // on a 2560x1600 screen).
                var monitors = Monitors();
                var m = monitors.FirstOrDefault(x => string.Equals(x.Device, p.Monitor, StringComparison.OrdinalIgnoreCase))
                        ?? (p.Mode == WindowMode.Windowed || p.Mode == WindowMode.Registry ? null : monitors.FirstOrDefault(x => x.Primary));
                var at = StartFor(m, p.Mode, p.Width, p.Height);
                if (at.Item1 != p.StartX || at.Item2 != p.StartY) { p.StartX = at.Item1; p.StartY = at.Item2; p.Save(); }
            }
            catch (Exception ex) { Log.Warn("graphics on play: " + ex.Message); }
        }
    }
}
