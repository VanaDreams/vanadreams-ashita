using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    public sealed class MenuCommand : INotifyPropertyChanged
    {
        private bool _selected;
        public string Label { get; set; }
        public string Key { get; set; }
        public bool Dim { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); Raise("HandVisibility"); Raise("Opacity"); } }
        public Visibility HandVisibility => Selected ? Visibility.Visible : Visibility.Hidden;
        public double Opacity => Dim ? 0.45 : Selected ? 1.0 : 0.9;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public partial class MenuPage : UserControl
    {
        private readonly MainWindow _win;
        private readonly List<MenuCommand> _commands = new List<MenuCommand>
        {
            new MenuCommand { Label = "Play", Key = "play", Selected = true },
            new MenuCommand { Label = "Profiles", Key = "profiles" },
            new MenuCommand { Label = "Addons", Key = "addons" },
            new MenuCommand { Label = "Fishing", Key = "fishing" },
            new MenuCommand { Label = "Vanatunes", Key = "vanatunes" },
            new MenuCommand { Label = "Capture", Key = "capture" },
            new MenuCommand { Label = "Window switcher", Key = "switcher" },
            new MenuCommand { Label = "Setup", Key = "setup" },
            new MenuCommand { Label = "Install game", Key = "install" },
            new MenuCommand { Label = "Settings", Key = "settings" },
            new MenuCommand { Label = "Discord", Key = "discord" },
            new MenuCommand { Label = "Exit", Key = "exit", Dim = true },
        };
        private Profile _profile;

        public MenuPage(MainWindow win)
        {
            InitializeComponent();
            _win = win;
            _onChanged = OnStateChanged;
            Commands.ItemsSource = _commands;
            KeyDown += OnKeyDown;
            // listen only while on screen; a page that has been left must not keep reloading profiles
            Loaded += (s, e) => { LoadProfile(); Keyboard.Focus(this); App.State.Changed += _onChanged; };
            Unloaded += (s, e) => App.State.Changed -= _onChanged;
        }

        private readonly Action _onChanged;
        private void OnStateChanged() => Dispatcher.BeginInvoke(new Action(LoadProfile));

        private void LoadProfile()
        {
            var state = App.State;
            var all = state.Profiles.LoadAll();
            _profile = all.FirstOrDefault(p => string.Equals(p.Id, state.Settings.LastProfile, StringComparison.OrdinalIgnoreCase))
                       ?? all.FirstOrDefault(p => !p.IsExample) ?? all.FirstOrDefault();
            Facts.Children.Clear();
            Facts.RowDefinitions.Clear();
            if (_profile == null)
            {
                ProfileName.Text = "No profile yet";
                ProfileServer.Text = "Run Setup to write the Vanadreams profile.";
                PlayButton.IsEnabled = false;
                return;
            }
            PlayButton.IsEnabled = true;
            ProfileName.Text = _profile.Name;
            ProfileServer.Text = _profile.IsRetail ? "Retail · Square Enix, through PlayOnline"
                : (_profile.Command.Server.IndexOf("vanadreams", StringComparison.OrdinalIgnoreCase) >= 0 ? "Vanadreams · " : "") + _profile.Command.Server;
            var v = state.Version;
            var mode = _profile.Mode == WindowMode.Registry ? "As the game remembers" : _profile.Mode.ToString();
            var size = _profile.Width > 0 && _profile.Height > 0 ? $" · {_profile.Width} × {_profile.Height}" : "";
            string lastPlayed;
            state.Settings.LastPlayed.TryGetValue(_profile.Id, out lastPlayed);
            DateTime when;
            var last = !string.IsNullOrEmpty(lastPlayed) && DateTime.TryParse(lastPlayed, null, DateTimeStyles.RoundtripKind, out when)
                ? (when.Date == DateTime.Today ? "Today, " + when.ToString("HH:mm") : when.ToString("d MMM, HH:mm")) : "Not yet";
            var cred = state.Credentials.Get(_profile.Id);
            AddFact("Server", state.Status.StateWord + (string.IsNullOrEmpty(state.Status.CheckedWord) ? "" : " · " + state.Status.CheckedWord), null);
            var update = state.PendingClientUpdate;
            // the update goes on the Client row, not a row of its own: the panel has no room for another row
            // (0.2.37 added one and pushed the last rows out of sight)
            if (update != null && v.ExpectedIsPublished && v.Verdict == VersionVerdict.Ready)
                AddFact("Client", v.Installed + " · update ready, " + update.Megabytes + " · Install game", "Warn");
            else
                AddFact("Client",
                    !v.ExpectedIsPublished ? (string.IsNullOrEmpty(v.Installed) ? "Version unknown" : v.Installed + " · server hasn't published its version")
                    : v.Verdict == VersionVerdict.Ready ? "✓ " + v.Installed + " · matches the server"
                    : v.Verdict == VersionVerdict.Unknown ? "Version unknown"
                    : "✗ " + v.Installed + " · server expects " + v.Expected,
                    !v.ExpectedIsPublished ? "Mist" : v.Verdict == VersionVerdict.Ready ? "Ok" : v.Verdict == VersionVerdict.Unknown ? "Warn" : "Bad");
            AddFact("Ashita", "v4 beta, updated " + state.AshitaUpdated() + " · " + ShortPath(state.AshitaRoot, 28), null);
            if (_profile.IsRetail)
                AddFact("Loader", File.Exists(_profile.BootFile) ? "PlayOnline Viewer" : "PlayOnline Viewer missing · install the retail client", File.Exists(_profile.BootFile) ? null : "Bad");
            else
                AddFact("Loader", "xiloader " + state.LoaderVersion(), null);
            var savedLogin = cred != null && !string.IsNullOrEmpty(cred.User);
            AddFact("Login", savedLogin ? cred.User + " · remembered" : "Not saved · Edit profile, fill Username and Password, Save", savedLogin ? null : "Warn");
            AddFact("Addons", state.EnabledCount + " enabled" + (state.UpdateCount > 0 ? " · " + state.UpdateCount + " update" + (state.UpdateCount > 1 ? "s" : "") : ""), null);
            AddFact("Window", mode + size, null);
            AddFact("Last played", last, null);
        }

        /// <summary>A long folder as drive, "…" and its last folders, so it fits the facts column.</summary>
        public static string ShortPath(string path, int max)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= max) return path ?? "";
            var parts = path.TrimEnd('\\').Split('\\');
            var tail = parts[parts.Length - 1];
            for (var i = parts.Length - 2; i > 0 && parts[0].Length + 3 + parts[i].Length + 1 + tail.Length <= max; i--) tail = parts[i] + "\\" + tail;
            return parts[0] + "\\…\\" + tail;
        }

        private void AddFact(string label, string value, string brushKey)
        {
            var row = Facts.RowDefinitions.Count;
            Facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 5) };
            var v = new TextBlock { Text = value, Style = (Style)FindResource("Mono"), Margin = new Thickness(0, 0, 0, 5), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = value };
            if (brushKey != null) v.Foreground = (Brush)FindResource(brushKey);
            Grid.SetRow(l, row); Grid.SetRow(v, row); Grid.SetColumn(v, 1);
            Facts.Children.Add(l); Facts.Children.Add(v);
        }

        private int SelectedIndex => _commands.FindIndex(c => c.Selected);

        private void Select(int index)
        {
            index = (index + _commands.Count) % _commands.Count;
            foreach (var c in _commands) c.Selected = false;
            _commands[index].Selected = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down) { Select(SelectedIndex + 1); e.Handled = true; }
            else if (e.Key == Key.Up) { Select(SelectedIndex - 1); e.Handled = true; }
            else if (e.Key == Key.Enter || e.Key == Key.Space) { Run(_commands[SelectedIndex].Key); e.Handled = true; }
        }

        private void Command_Hover(object sender, MouseEventArgs e)
        {
            var cmd = ((FrameworkElement)sender).Tag as MenuCommand;
            if (cmd != null) Select(_commands.IndexOf(cmd));
        }

        private void Command_Click(object sender, MouseButtonEventArgs e)
        {
            var cmd = ((FrameworkElement)sender).Tag as MenuCommand;
            if (cmd != null) Run(cmd.Key);
        }

        private void Play_Click(object sender, RoutedEventArgs e) => Run("play");
        private void Edit_Click(object sender, RoutedEventArgs e) => _win.Navigate(new ProfilesPage(_win, _profile?.Id));

        private void Run(string key)
        {
            switch (key)
            {
                case "play": Play(); break;
                case "profiles": _win.Navigate(new ProfilesPage(_win, _profile?.Id)); break;
                case "addons": _win.Navigate(new AddonsPage(_win)); break;
                case "fishing": _win.Navigate(new FishingPage(_win)); break;
                case "vanatunes": _win.Navigate(new VanatunesPage(_win)); break;
                case "capture": _win.Navigate(new CapturePage(_win)); break;
                case "switcher": _win.Navigate(new SwitcherPage(_win)); break;
                case "setup": _win.Navigate(new SetupPage(_win)); break;
                case "install": _win.Navigate(new InstallPage(_win)); break;
                case "settings": _win.Navigate(new SettingsPage(_win)); break;
                case "discord": OpenUrl(DiscordInvite); break;
                case "exit": Application.Current.Shutdown(); break;
            }
        }

        /// <summary>The Vanadreams Discord's permanent invite, the same one fairywitch.ca carries.</summary>
        public const string DiscordInvite = "https://discord.gg/tBpNVBwKqT";

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Log.Warn("open url: " + ex.Message); }
        }

        /// <summary>
        /// Ashita runs as the player unless something here needs administrator: the profile asks for it, it is a retail
        /// profile (PlayOnline updates itself), or Ashita or the game sits under Program Files, where only an
        /// administrator can write the game's settings and screenshots.
        /// </summary>
        private bool NeedsAdmin(Profile profile, string gameRoot) =>
            profile.RunAsAdmin || profile.IsRetail ||
            ClientVersion.IsUnderProgramFiles(App.State.AshitaRoot) || ClientVersion.IsUnderProgramFiles(gameRoot);

        /// <summary>
        /// The copy of the game this profile should start. Vanadreams looks across every copy on the PC for the one
        /// that fits the server (AppState.VanadreamsGameRoot); any other server, retail included, plays the profile's
        /// own game folder or the copy Windows had before we took it.
        /// Null when there is nothing known to point at, and the registration is left alone.
        /// </summary>
        private string GameRootFor(Profile profile, RegisteredGame reg)
        {
            var state = App.State;
            if (!profile.IsVanadreams) return state.OtherGameRootFor(profile);

            var registered = GameRegistration.CurrentRoot(reg);
            var root = state.VanadreamsGameRoot(registered ?? "");
            Log.Info("play: the copy to start is " + (root ?? "whatever Windows has") +
                     (root == null ? "" : ", version " + (state.InstalledVersion(root) ?? "unknown")) +
                     "; Windows starts " + (registered ?? "nothing") + "; installed here " +
                     (string.IsNullOrWhiteSpace(state.Settings.GameInstallRoot) ? "not recorded" : state.Settings.GameInstallRoot));
            // Nothing recorded? Anyone who installed before the folder was remembered has this empty, and his game
            // may be sitting beside his Ashita. Record it so the Install page opens on the real folder.
            if (root != null && !GameRegistration.HasGame(state.Settings.GameInstallRoot) &&
                (GameRegistration.SameFolder(root, state.Settings.AshitaRoot) || GameRegistration.SameFolder(root, ClientInstall.DefaultRoot)))
            {
                state.Settings.GameInstallRoot = root;
                state.Settings.Save();
                Log.Info("found the game beside " + root + " and recorded it");
            }
            // Null: nothing found to point at - which is not the same as nothing being there. A game put somewhere
            // this launcher does not know to look is still a game, so the launch goes ahead with what Windows has.
            return root;
        }

        /// <summary>Roots whose registration did not take even after the prompt; asked once per run, not on every Play.</summary>
        private static readonly HashSet<string> _registerDidNotTake = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Make sure Windows starts the copy of the game in this root, fixing it in place when it does not.
        /// False only when it could not be put right and the player has been told why.
        /// </summary>
        private bool EnsureGameIsRegistered(string root, RegisteredGame reg)
        {
            var state = App.State;
            if (GameRegistration.PointsAt(reg, root) || _registerDidNotTake.Contains(root))
                return true;   // nothing to do, which is almost every launch

            // Remember the copy we are taking it from, so a profile for its server can have it back.
            var current = GameRegistration.CurrentRoot(reg);
            if (GameRegistration.HasGame(current) &&
                !GameRegistration.SameFolder(current, root) &&
                !GameRegistration.SameFolder(current, state.Settings.GameInstallRoot))
            {
                state.Settings.OtherGameRoot = current;
                state.Settings.Save();
                Log.Info("the registration was on " + current + "; remembered for other servers");
            }

            try
            {
                if (ClientInstall.RunRegister(root, state.Settings.DownloadsFolder))
                {
                    Log.Info("registration pointed at " + ClientInstall.GameFolder(root));
                    var after = GameRegistration.Read();
                    if (!GameRegistration.PointsAt(after, root))
                    {
                        _registerDidNotTake.Add(root);
                        Log.Warn("registration still differs after registering " + root + ": game " + after.GameFolder +
                                 ", viewer " + after.ViewerFolder + ", COM " + string.Join("; ", after.ComServers));
                    }
                    return true;
                }
                MessageBox.Show(
                    "Windows needs permission to point at the game in " + ClientInstall.GameFolder(root) + " before it can start.\n\n" +
                    "Press Play again and choose Yes.",
                    "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Log.Error("register on play", ex);
                MessageBox.Show(ex.Message, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private async void Play()
        {
            if (_profile == null) return;
            var state = App.State;
            // The loader has to be one the Vanadreams server accepts, and the newest one is not (Services\Loader.cs).
            // A player whose Setup ran on or after 27 Sep 2026 had v2.2.0 in bootloader\ and was told
            // "Your xiloader is too old" by a server that wanted the older one. Put it right here, once,
            // so the fix reaches a player who only ever presses Play. Other servers keep the loader they have.
            if (_profile.IsVanadreams)
            {
                try
                {
                    if (await Loader.InstallAsync(state.Downloader, state.Settings.DownloadsFolder, state.AshitaRoot, false))
                        Log.Info("loader replaced before play");
                }
                catch (Exception ex) { Log.Warn("loader check on play: " + ex.Message); }
                // 11 Oct 2026: a player's install sat on B: while her profile still booted a loader elsewhere,
                // and Ashita closed the moment it opened. Every check on the menu read the folder, so all looked right.
                try
                {
                    var was = _profile.BootFile;
                    if (_profile.PointLoaderAt(state.AshitaRoot)) { _profile.Save(); Log.Info("profile loader moved from " + was + " to " + _profile.BootFile); }
                }
                catch (Exception ex) { Log.Warn("loader path on play: " + ex.Message); }
            }
            // Ashita and xiloader need a newer 32-bit Visual C++ runtime than many PCs have, and without it
            // Ashita's console shows its banner and waits forever (Services\VcRuntime.cs). Put it right here.
            try { await VcRuntime.EnsureAsync(state.Downloader, state.Settings.DownloadsFolder); }
            catch (Exception ex)
            {
                Log.Error("vc runtime on play", ex);
                MessageBox.Show(ex.Message, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Low spec is only a rename in bootloader\, so it is put right on every Play, in case an update put a wrapper back
            if (state.HasAshita) Effects.Apply(state.AshitaRoot, state.Settings.LowSpec);
            // The game reads its own location from Windows, not from this launcher, and Windows holds one
            // such location for the whole machine. When it does not point at the copy this profile belongs to,
            // the wrong game starts: an older copy kept for another server, which Vanadreams turns away, or
            // nothing at all, so the console closes moments after "Successfully logged in".
            //
            // We know which copy each profile needs, so this is ours to keep right rather than ours to report.
            // Point it at that copy and carry on. The only thing the player sees is Windows' own permission
            // prompt, and only on the launch after it drifted or when they switch between servers.
            // 26 Sept 2026: a player installed twice, to C: and then to D:, and the registration stayed on
            // the first. He spent an hour on it and nothing anywhere said why.
            var reg = GameRegistration.Read();
            var root = GameRootFor(_profile, reg);
            var previousRoot = GameRegistration.CurrentRoot(reg);
            if (root != null && !EnsureGameIsRegistered(root, reg)) return;
            // 12 Oct 2026: Mogsy's loader logged in and closed, because neither pol.exe nor FFXiMain.dll was where
            // Windows pointed. When no copy we know of is a whole game, the launch is all but sure to do the same.
            if (_profile.IsVanadreams && root == null &&
                MessageBox.Show(PlayCheck.NoGameSentence, "Vanadreams Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            state.CheckVersion();
            if (_profile.IsVanadreams && state.Version.BlocksPlay)   // the Vanadreams version rule has no say over other servers
            {
                MessageBox.Show(state.Version.Sentence + "\n\nInstall game, on the menu, fetches the version the server runs.", "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                _win.RefreshStrip();
                return;
            }

            // macros and config live only in the game's USER folder; a copy before every launch lets a lost set come back
            try
            {
                var gameFolder = root != null ? ClientInstall.GameFolder(root) : reg.GameFolder;
                var copy = UserFolderBackup.Take(gameFolder, UserFolderBackup.BackupFolder, DateTime.Now);
                if (copy != null) Log.Info("USER folder copied to " + copy);
                // switching between two copies of the game: bring the newer macros and config along
                if (root != null && previousRoot != null && !GameRegistration.SameFolder(root, previousRoot))
                {
                    var carried = UserFolderBackup.CarryOver(ClientInstall.GameFolder(previousRoot), gameFolder);
                    if (carried.Count > 0) Log.Info("macros and config carried from " + previousRoot + " for " + string.Join(", ", carried));
                }
            }
            catch (Exception ex) { Log.Warn("USER backup on play: " + ex.Message); }

            // Antivirus takes Ashita or the loader away, and without them Ashita closes before anything shows (Services\PlayCheck.cs)
            if (state.HasAshita && !FilesAreThere(state)) return;
            // A network that cannot reach the server looks exactly like every other failure once the loader's window closes
            // Hairpin profiles play from the server's own network, where the public address may not loop back, so they skip it
            if (_profile.IsVanadreams && !_profile.Command.Hairpin && !await ServerAnswers(state)) return;

            var loaderName = PlayCheck.LoaderProcessName(_profile.BootFile);
            var before = PlayCheck.RunningIds(loaderName);
            var launchedAt = DateTime.UtcNow;
            try
            {
                GameLauncher.Launch(state.AshitaRoot, _profile, state.Credentials.Get(_profile.Id), NeedsAdmin(_profile, root ?? GameRegistration.CurrentRoot(reg)));
                Music.FadeOut(3);
                state.Settings.LastProfile = _profile.Id;
                state.Settings.LastPlayed[_profile.Id] = DateTime.Now.ToString("o");
                state.Settings.Save();
                if (!_profile.AutoClose) LoadProfile();
                // the launcher waits to close until the game is open, so it can still say why if it never opens
                var autoClose = _profile.AutoClose;
                var opened = await WatchLaunch(state, _profile, loaderName, before, launchedAt);
                // with the window switcher on, "close" means the tray: the switcher has to keep running for the game
                if (autoClose && opened && !_win.HideToTray()) Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error("launch failed", ex);
                MessageBox.Show(ex.Message, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>False, after telling the player, when antivirus or anything else has taken a file the launch needs.</summary>
        private bool FilesAreThere(AppState state)
        {
            var missing = PlayCheck.Missing(state.AshitaRoot, _profile.BootFile);
            if (missing.Count == 0) return true;
            Log.Warn("before play, missing: " + string.Join("; ", missing));
            ExplainMissing(state, missing);
            return false;
        }

        private void ExplainMissing(AppState state, List<string> missing)
        {
            var av = PlayCheck.Antivirus();
            var text = PlayCheck.MissingSentence(missing, av, state.AshitaRoot);
            // Windows Security alone can be told to leave the folders alone from here; any other antivirus only by hand
            if (PlayCheck.OtherAntivirus(av).Count == 0)
            {
                var ask = MessageBox.Show(text + "\n\nAdd the Ashita and game folders to Windows Security's exclusions now? Windows will ask for permission.",
                    "Vanadreams Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ask == MessageBoxResult.Yes)
                {
                    var ok = PlayCheck.AddDefenderExclusions(new[] { state.AshitaRoot, state.Settings.GameInstallRoot });
                    MessageBox.Show(ok ? "Done. Now open Setup and press Check and update to put the missing files back, then press Play."
                                       : "The exclusions were not added. Add the folders by hand in Windows Security, Virus & threat protection, Exclusions.",
                        "Vanadreams Launcher", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
                return;
            }
            MessageBox.Show(text, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>False, after telling the player, when the server's login ports do not answer from this PC. A failed check never blocks Play on its own.</summary>
        private async System.Threading.Tasks.Task<bool> ServerAnswers(AppState state)
        {
            PlayCheck.Reach reach;
            try { reach = await PlayCheck.TryPortsAsync(_profile.Command.Server, PlayCheck.LoginPorts, TimeSpan.FromSeconds(4)); }
            catch (Exception ex) { Log.Warn("port check: " + ex.Message); return true; }
            Log.Info("port check " + reach.Host + ": open " + string.Join(",", reach.Open) + " closed " + string.Join(",", reach.Closed) + (reach.NoName ? " (no name)" : ""));
            var text = PlayCheck.ReachSentence(reach, _profile.Command.IsTailscale, state.Status.State);
            if (text == null) return true;
            return MessageBox.Show(text + "\n\nPlay anyway?", "Vanadreams Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Watch the loader after Play. True once the game's window opens. When the loader closes first, say why:
        /// a file taken, a crash Windows recorded, the server out of reach, or else what the loader's own window said.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> WatchLaunch(AppState state, Profile profile, string loaderName, HashSet<int> before, DateTime launchedAt)
        {
            LaunchOutcome outcome;
            try { outcome = await PlayCheck.WatchAsync(loaderName, before, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15)); }
            catch (Exception ex) { Log.Warn("launch watch: " + ex.Message); return true; }
            Log.Info("launch " + profile.Name + ": " + outcome);
            if (outcome == LaunchOutcome.GameOpened || outcome == LaunchOutcome.StillWaiting) return true;

            var missing = state.HasAshita ? PlayCheck.Missing(state.AshitaRoot, profile.BootFile) : new List<string>();
            if (missing.Count > 0) { Log.Warn("after play, missing: " + string.Join("; ", missing)); ExplainMissing(state, missing); return false; }

            var crash = PlayCheck.LastCrash(new[] { loaderName, "Ashita-cli" }, launchedAt.AddSeconds(-5));
            if (crash != null)
            {
                Log.Warn("crash after play: " + crash.App + " in " + crash.Module + " " + crash.ModuleVersion);
                MessageBox.Show(PlayCheck.CrashSentence(crash), "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (profile.IsVanadreams && !profile.Command.Hairpin)
            {
                try
                {
                    var reach = await PlayCheck.TryPortsAsync(profile.Command.Server, PlayCheck.LoginPorts, TimeSpan.FromSeconds(4));
                    var text = PlayCheck.ReachSentence(reach, profile.Command.IsTailscale, state.Status.State);
                    if (text != null) { MessageBox.Show(text, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
                }
                catch (Exception ex) { Log.Warn("port check after play: " + ex.Message); }
            }

            MessageBox.Show(outcome == LaunchOutcome.NeverStarted ? PlayCheck.NeverStartedNoCause : PlayCheck.ClosedNoCause,
                "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
