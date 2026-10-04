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
            AddFact("Client",
                !v.ExpectedIsPublished ? (string.IsNullOrEmpty(v.Installed) ? "Version unknown" : v.Installed + " · server hasn't published its version")
                : v.Verdict == VersionVerdict.Ready ? "✓ " + v.Installed + " · matches the server"
                : v.Verdict == VersionVerdict.Unknown ? "Version unknown"
                : "✗ " + v.Installed + " · server expects " + v.Expected,
                !v.ExpectedIsPublished ? "Mist" : v.Verdict == VersionVerdict.Ready ? "Ok" : v.Verdict == VersionVerdict.Unknown ? "Warn" : "Bad");
            AddFact("Ashita", "v4 beta, updated " + state.AshitaUpdated() + " · " + state.AshitaRoot, null);
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

        private void AddFact(string label, string value, string brushKey)
        {
            var row = Facts.RowDefinitions.Count;
            Facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 5) };
            var v = new TextBlock { Text = value, Style = (Style)FindResource("Mono"), Margin = new Thickness(0, 0, 0, 5), TextTrimming = TextTrimming.CharacterEllipsis };
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
            }
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
            if (root != null && !EnsureGameIsRegistered(root, reg)) return;

            state.CheckVersion();
            if (_profile.IsVanadreams && state.Version.BlocksPlay)   // the Vanadreams version rule has no say over other servers
            {
                MessageBox.Show(state.Version.Sentence + "\n\nInstall game, on the menu, fetches the version the server runs.", "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                _win.RefreshStrip();
                return;
            }

            try
            {
                GameLauncher.Launch(state.AshitaRoot, _profile, state.Credentials.Get(_profile.Id));
                Music.FadeOut(3);
                state.Settings.LastProfile = _profile.Id;
                state.Settings.LastPlayed[_profile.Id] = DateTime.Now.ToString("o");
                state.Settings.Save();
                if (_profile.AutoClose) { var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) }; t.Tick += (s, e) => Application.Current.Shutdown(); t.Start(); }
                else LoadProfile();
            }
            catch (Exception ex)
            {
                Log.Error("launch failed", ex);
                MessageBox.Show(ex.Message, "Vanadreams Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
