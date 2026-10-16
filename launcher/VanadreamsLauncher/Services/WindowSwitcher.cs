using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Vanadreams.Services
{
    /// <summary>
    /// The window switcher: for a player with more than one character open, a key or a controller press brings the
    /// next, the previous or a numbered game window to the front, and saved layouts put the windows back where they go.
    ///
    /// The idea comes from HaxPrime's XI Focus (MIT): find the game by its window class, name each slot from the
    /// window title, and borrow the foreground thread's input to be allowed to change focus. XI Focus has to run
    /// as administrator because the game usually does; the launcher starts Ashita as the player (GameLauncher), so
    /// this needs no administrator. Unlike XI Focus, keys only count while a game window is in front (unless the
    /// player asks for every window) and are kept from the game, so a bound key never also types or fires a macro.
    ///
    /// Runs on the launcher's UI thread: the keyboard hook needs a message loop, and WPF's dispatcher is one.
    /// </summary>
    public sealed class WindowSwitcher : IDisposable
    {
        private readonly LauncherSettings _settings;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _scan;
        private readonly DispatcherTimer _pad;
        private List<GameWindow> _windows = new List<GameWindow>();
        private int _current = -1;
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc _hookProc;   // held so the collector never frees the callback Windows calls
        private readonly HashSet<int> _swallowedDown = new HashSet<int>();
        private ushort _padBefore;
        private int _padIndex = -1;
        private bool _xinputMissing;

        public event Action Changed;
        public bool Running { get; private set; }
        public IReadOnlyList<GameWindow> Windows => _windows;
        public string LastProblem { get; private set; }
        /// <summary>Set while the page waits for a new key, so a key already bound reaches the page instead of switching.</summary>
        public bool Paused { get; set; }

        public WindowSwitcher(LauncherSettings settings, Dispatcher dispatcher)
        {
            _settings = settings;
            _dispatcher = dispatcher;
            _scan = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            _scan.Tick += (s, e) => Scan();
            _pad = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
            _pad.Tick += (s, e) => PollPad();
        }

        /// <summary>Start, stop or pick up changed keys from the settings.</summary>
        public void Apply()
        {
            if (!_settings.SwitcherOn) { Stop(); return; }
            Running = true;
            Scan();
            _scan.Start();
            var wantsKeys = AllKeys().Any(k => k != null);
            if (wantsKeys && _hook == IntPtr.Zero) InstallHook();
            if (!wantsKeys) RemoveHook();
            var wantsPad = PadBinding.Parse(_settings.PadNext) != 0 || PadBinding.Parse(_settings.PadPrev) != 0;
            if (wantsPad && !_xinputMissing) _pad.Start(); else _pad.Stop();
            Changed?.Invoke();
        }

        public void Stop()
        {
            Running = false;
            _scan.Stop();
            _pad.Stop();
            RemoveHook();
            Changed?.Invoke();
        }

        public void Dispose() => Stop();

        // ---- the windows ----

        /// <summary>Look for game windows (also used by the page while the switcher is off, to list them).</summary>
        public void Scan()
        {
            var found = new List<GameWindow>();
            try
            {
                EnumWindows((h, l) =>
                {
                    if (!IsWindowVisible(h) || GetParent(h) != IntPtr.Zero) return true;
                    var cls = new StringBuilder(64);
                    GetClassName(h, cls, cls.Capacity);
                    if (!string.Equals(cls.ToString(), PlayCheck.GameWindowClass, StringComparison.Ordinal)) return true;
                    var len = GetWindowTextLength(h);
                    var title = new StringBuilder(Math.Max(len + 1, 2));
                    GetWindowText(h, title, title.Capacity);
                    found.Add(new GameWindow { Handle = h, Title = title.ToString(), Name = SwitchRules.NameFromTitle(title.ToString()) });
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex) { Log.Warn("switcher scan: " + ex.Message); return; }

            var old = _windows;
            foreach (var w in found)
            {
                var before = old.FirstOrDefault(o => o.Handle == w.Handle);
                w.Elevated = before != null ? before.Elevated : IsElevated(w.Handle);
            }
            var arranged = SwitchRules.Arrange(old, found, _settings.SwitchOrder);
            var changed = arranged.Count != old.Count ||
                          arranged.Where((w, i) => w.Handle != old[i].Handle || w.Name != old[i].Name).Any();
            if (!changed) return;

            // a character who just logged in goes to its saved place
            if (Running && _settings.AutoLayout)
            {
                var layout = ActiveLayout();
                for (var i = 0; i < arranged.Count; i++)
                {
                    var w = arranged[i];
                    var before = old.FirstOrDefault(o => o.Handle == w.Handle);
                    if (w.Name != null && (before == null || before.Name == null)) Place(w, i, layout);
                }
            }
            var currentHandle = _current >= 0 && _current < old.Count ? old[_current].Handle : IntPtr.Zero;
            _windows = arranged;
            _current = _windows.FindIndex(w => w.Handle == currentHandle);
            Changed?.Invoke();
        }

        /// <summary>The player moved a slot up or down on the page; the order is kept by name for next time.</summary>
        public void Move(int index, int by)
        {
            var to = index + by;
            if (index < 0 || to < 0 || index >= _windows.Count || to >= _windows.Count) return;
            var list = _windows.ToList();
            var w = list[index]; list[index] = list[to]; list[to] = w;
            _windows = list;
            if (_current == index) _current = to; else if (_current == to) _current = index;
            var named = list.Where(x => x.Name != null).Select(x => x.Name).ToList();
            _settings.SwitchOrder = named.Concat(_settings.SwitchOrder.Where(n => !named.Contains(n, StringComparer.OrdinalIgnoreCase))).ToList();
            _settings.Save();
            Changed?.Invoke();
        }

        public void FocusNext() => FocusSlot(SwitchRules.Next(CurrentIndex(), _windows.Count));
        public void FocusPrevious() => FocusSlot(SwitchRules.Previous(CurrentIndex(), _windows.Count));

        /// <summary>Which slot is in front now; falls back to the last one switched to.</summary>
        private int CurrentIndex()
        {
            var fg = GetForegroundWindow();
            var i = _windows.FindIndex(w => w.Handle == fg);
            return i >= 0 ? i : _current;
        }

        public bool FocusSlot(int slot)
        {
            Scan();
            if (slot < 0 || slot >= _windows.Count) return false;
            _current = slot;
            var w = _windows[slot];
            var ok = BringToFront(w.Handle);
            LastProblem = ok ? null
                : w.Elevated ? w.Label(slot) + " was started as administrator, so Windows will not let the launcher bring it forward. Start it from the launcher's Play, or run the launcher as administrator too."
                : "Windows did not let the launcher bring " + w.Label(slot) + " forward; click it once, then the keys work.";
            if (!ok) Log.Warn("switcher: " + LastProblem);
            Changed?.Invoke();
            return ok;
        }

        /// <summary>
        /// Windows only lets the program the player is using change the foreground window. Attaching to the
        /// foreground window's input borrows that right; a tap of Alt is the long-standing fallback when it still refuses.
        /// </summary>
        public static bool BringToFront(IntPtr h)
        {
            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
            var fg = GetForegroundWindow();
            if (fg == h) return true;
            uint pid;
            var fgThread = GetWindowThreadProcessId(fg, out pid);
            var me = GetCurrentThreadId();
            var attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            try
            {
                BringWindowToTop(h);
                SetForegroundWindow(h);
            }
            finally { if (attached) AttachThreadInput(me, fgThread, false); }
            if (GetForegroundWindow() == h) return true;
            keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            SetForegroundWindow(h);
            return GetForegroundWindow() == h;
        }

        // ---- layouts ----

        public List<string> ActiveLayout()
        {
            List<string> layout;
            return !string.IsNullOrEmpty(_settings.ActiveLayout) && _settings.Layouts.TryGetValue(_settings.ActiveLayout, out layout) ? layout : null;
        }

        public int SaveLayout(string name)
        {
            Scan();
            var entries = new List<string>();
            for (var i = 0; i < _windows.Count; i++)
            {
                RECT r;
                if (IsIconic(_windows[i].Handle) || !GetWindowRect(_windows[i].Handle, out r)) continue;
                entries.Add(SwitchRules.LayoutEntry(_windows[i], i, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
            }
            _settings.Layouts[name] = entries;
            _settings.ActiveLayout = name;
            _settings.Save();
            Log.Info("switcher: layout " + name + " saved with " + entries.Count + " windows");
            return entries.Count;
        }

        public int RestoreLayout(string name)
        {
            List<string> layout;
            if (!_settings.Layouts.TryGetValue(name, out layout)) return 0;
            _settings.ActiveLayout = name;
            _settings.Save();
            Scan();
            var moved = 0;
            for (var i = 0; i < _windows.Count; i++) if (Place(_windows[i], i, layout)) moved++;
            return moved;
        }

        private static bool Place(GameWindow w, int slot, IEnumerable<string> layout)
        {
            var p = SwitchRules.PlaceFor(layout, w, slot);
            if (p == null) return false;
            if (IsIconic(w.Handle)) ShowWindow(w.Handle, SW_RESTORE);
            return SetWindowPos(w.Handle, IntPtr.Zero, p[0], p[1], p[2], p[3], SWP_NOZORDER | SWP_NOACTIVATE);
        }

        // ---- keys ----

        private IEnumerable<KeyBinding> AllKeys()
        {
            yield return KeyBinding.Parse(_settings.SwitchNext);
            yield return KeyBinding.Parse(_settings.SwitchPrev);
            foreach (var s in _settings.SwitchSlots) yield return KeyBinding.Parse(s);
        }

        private void InstallHook()
        {
            _hookProc = HookProc;
            using (var p = Process.GetCurrentProcess())
            using (var m = p.MainModule)
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(m.ModuleName), 0);
            if (_hook == IntPtr.Zero) Log.Warn("switcher: keyboard hook refused, error " + Marshal.GetLastWin32Error());
        }

        private void RemoveHook()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _swallowedDown.Clear();
        }

        private bool GameInFront()
        {
            if (_settings.SwitchKeysAnywhere) return true;
            var fg = GetForegroundWindow();
            if (_windows.Any(w => w.Handle == fg)) return true;
            var cls = new StringBuilder(64);
            GetClassName(fg, cls, cls.Capacity);
            return string.Equals(cls.ToString(), PlayCheck.GameWindowClass, StringComparison.Ordinal);
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;
                var vk = Marshal.ReadInt32(lParam);
                if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (_swallowedDown.Remove(vk)) return (IntPtr)1;
                }
                else if ((msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) && Running && !Paused && GameInFront())
                {
                    var action = Match(vk, Down(VK_CONTROL), Down(VK_MENU), Down(VK_SHIFT));
                    if (action != null)
                    {
                        _swallowedDown.Add(vk);
                        // out of the hook at once: Windows drops a hook that takes too long
                        _dispatcher.BeginInvoke(action);
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private Action Match(int vk, bool ctrl, bool alt, bool shift)
        {
            var next = KeyBinding.Parse(_settings.SwitchNext);
            if (next != null && next.Matches(vk, ctrl, alt, shift)) return FocusNext;
            var prev = KeyBinding.Parse(_settings.SwitchPrev);
            if (prev != null && prev.Matches(vk, ctrl, alt, shift)) return FocusPrevious;
            for (var i = 0; i < _settings.SwitchSlots.Count; i++)
            {
                var slot = KeyBinding.Parse(_settings.SwitchSlots[i]);
                var n = i;
                if (slot != null && slot.Matches(vk, ctrl, alt, shift)) return () => FocusSlot(n);
            }
            return null;
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        // ---- controller ----

        /// <summary>The first connected pad is read; a chord fires once per press while a game window is in front.</summary>
        private void PollPad()
        {
            ushort now = 0;
            try
            {
                if (_padIndex < 0 || !ReadPad(_padIndex, out now))
                {
                    _padIndex = -1;
                    for (var i = 0; i < 4; i++) if (ReadPad(i, out now)) { _padIndex = i; break; }
                }
            }
            catch (DllNotFoundException) { _xinputMissing = true; _pad.Stop(); Log.Warn("switcher: no XInput on this PC, controller off"); return; }
            catch (EntryPointNotFoundException) { _xinputMissing = true; _pad.Stop(); return; }
            var before = _padBefore;
            _padBefore = now;
            if (Paused || !GameInFront()) return;
            if (PadBinding.Fired(PadBinding.Parse(_settings.PadNext), before, now)) FocusNext();
            else if (PadBinding.Fired(PadBinding.Parse(_settings.PadPrev), before, now)) FocusPrevious();
        }

        private static bool ReadPad(int index, out ushort buttons)
        {
            XINPUT_STATE s;
            buttons = 0;
            if (XInputGetState((uint)index, out s) != 0) return false;
            buttons = s.Buttons;
            return true;
        }

        // ---- administrator ----

        /// <summary>
        /// True when the window's game runs as administrator. A process the launcher may not even look into is
        /// taken as elevated, which is the only reason it would be refused.
        /// </summary>
        private static bool IsElevated(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            var proc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (proc == IntPtr.Zero) return true;
            try
            {
                IntPtr token;
                if (!OpenProcessToken(proc, TOKEN_QUERY, out token)) return true;
                try
                {
                    int elevated, size;
                    return GetTokenInformation(token, TokenElevation, out elevated, sizeof(int), out size) && elevated != 0;
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(proc); }
        }

        // ---- Win32 ----

        private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        private const int SW_RESTORE = 9;
        private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, TOKEN_QUERY = 0x0008;
        private const int TokenElevation = 20;

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("advapi32.dll")] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll")] private static extern bool GetTokenInformation(IntPtr token, int cls, out int info, int length, out int returned);
        [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XINPUT_STATE state);
    }
}
