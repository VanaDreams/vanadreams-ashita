using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vanadreams.Pages;
using Vanadreams.Services;

namespace Vanadreams
{
    public partial class MainWindow : Window
    {
        public AppState State => App.State;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };

        public MainWindow()
        {
            InitializeComponent();
            DrawStars();
            State.Changed += () => Dispatcher.BeginInvoke(new Action(RefreshStrip));
            Loaded += OnLoaded;
            _statusTimer.Tick += async (s, e) => await State.RefreshStatusAsync();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (State.HasAshita) GameLauncher.Sweep(State.AshitaRoot);
            State.CheckVersion();
            RefreshStrip();
            RefreshMute();
            if (!string.IsNullOrEmpty(App.SnapshotPath)) { await SnapshotAndExit(); return; }
            if (State.HasAshita) Navigate(new MenuPage(this)); else Navigate(new SetupPage(this));
            App.Switcher = new WindowSwitcher(State.Settings, Dispatcher);
            // the game windows all closed while the launcher sat in the tray: nothing is left to switch, so it goes
            App.Switcher.Changed += () => { if (_tray != null && _tray.Visible && App.Switcher.Windows.Count == 0) Quit(); };
            App.Switcher.Apply();
            // music after the first frame, so the unpack on a first run never delays the window
            if (State.Settings.MusicOn) Dispatcher.BeginInvoke(new Action(Music.Start), System.Windows.Threading.DispatcherPriority.Background);
            // first, and alongside the rest: the update window has to be up before anyone reaches Play
            var update = CheckForUpdateAsync();
            _statusTimer.Start();
            await State.RefreshStatusAsync();
            await State.CheckClientUpdateAsync();
            await EnsureLoaderAsync();
            await State.RefreshCatalogAsync();
            await State.GiveDefaultsAsync();
            await update;
        }

        /// <summary>The loader in bootloader\ is one the server accepts; when it is not, it is swapped for the pinned one (Services\Loader.cs).</summary>
        private async Task EnsureLoaderAsync()
        {
            if (!State.HasAshita) return;
            try
            {
                if (await Loader.InstallAsync(State.Downloader, State.Settings.DownloadsFolder, State.AshitaRoot, false)) State.Notify();
            }
            catch (Exception ex) { Log.Warn("loader: " + ex.Message); }
        }

        private string _updatedExe;

        /// <summary>
        /// Once per start: a newer release puts the update window in front of the player while it downloads
        /// (UpdateWindow). The signed exe is swapped in whatever they choose; after Later, the strip keeps
        /// the restart button.
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            try
            {
                var asset = await Updater.CheckAsync(State.Downloader);
                if (asset == null) return;
                NewsLine.Text = "Update " + asset.Tag + " found, downloading…";
                var progress = new Progress<DownloadProgress>();
                var ready = FetchAndApplyAsync(asset, progress);
                var box = new UpdateWindow(asset.Tag, Updater.Normalise(Updater.Current), ready, progress) { Owner = this };
                if (box.ShowDialog() == true && _updatedExe != null) { Updater.Restart(_updatedExe); return; }
                await ready;
                if (_updatedExe == null) { NewsLine.Text = "Update " + asset.Tag + " could not be fetched; it is on fairywitch.ca."; return; }
                NewsLine.Text = "Launcher " + asset.Tag + " is ready.";
                UpdateButton.Content = "Restart into " + asset.Tag;
                UpdateButton.Visibility = Visibility.Visible;
                RefreshStrip();
            }
            catch (Exception ex) { Log.Warn("update: " + ex.Message); }
        }

        /// <summary>Download, check the signature, swap into place. The path of the new exe, or null.</summary>
        private async Task<string> FetchAndApplyAsync(ReleaseAsset asset, IProgress<DownloadProgress> progress)
        {
            var exe = await Updater.FetchAsync(State.Downloader, asset, State.Settings.DownloadsFolder, progress);
            if (exe == null) return null;
            _updatedExe = Updater.Apply(exe);
            return _updatedExe;
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_updatedExe == null) return;
            Updater.Restart(_updatedExe);
        }

        // ---- the tray, for the window switcher ----

        private System.Windows.Forms.NotifyIcon _tray;
        private bool _trayTold;

        /// <summary>
        /// The close button (and Alt+F4), while the switcher runs and a game is open, hides the launcher to the tray instead.
        /// Caught as the window's Close command rather than in Closing, so Exit and an update restart still close for good.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                const int WM_SYSCOMMAND = 0x0112, SC_CLOSE = 0xF060;
                if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_CLOSE && HideToTray()) handled = true;
                return IntPtr.Zero;
            });
        }

        /// <summary>Hide to the tray when the switcher has a game to look after. False when there is none, so the caller closes.</summary>
        public bool HideToTray()
        {
            var sw = App.Switcher;
            if (sw == null || !sw.Running) return false;
            sw.Scan();
            if (sw.Windows.Count == 0) return false;
            if (_tray == null)
            {
                _tray = new System.Windows.Forms.NotifyIcon { Text = "Vanadreams Launcher · window switcher", Visible = false };
                try
                {
                    using (var s = Application.GetResourceStream(new Uri("pack://application:,,,/assets/moon.ico")).Stream)
                        _tray.Icon = new System.Drawing.Icon(s);
                }
                catch (Exception ex) { Log.Warn("tray icon: " + ex.Message); _tray.Icon = System.Drawing.SystemIcons.Application; }
                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("Open the launcher", null, (s, e) => Dispatcher.BeginInvoke(new Action(ShowFromTray)));
                menu.Items.Add("Quit (the window switcher stops)", null, (s, e) => Dispatcher.BeginInvoke(new Action(Quit)));
                _tray.ContextMenuStrip = menu;
                _tray.DoubleClick += (s, e) => Dispatcher.BeginInvoke(new Action(ShowFromTray));
            }
            _tray.Visible = true;
            Hide();
            if (!_trayTold)
            {
                _trayTold = true;
                _tray.ShowBalloonTip(4000, "Window switcher still on", "The launcher is in the tray so your switch keys keep working. Right-click the moon to quit.", System.Windows.Forms.ToolTipIcon.None);
            }
            Log.Info("hidden to the tray for the window switcher");
            return true;
        }

        private void ShowFromTray()
        {
            if (_tray != null) _tray.Visible = false;
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        private void Quit()
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            base.OnClosed(e);
        }

        private void GameUpdate_Click(object sender, RoutedEventArgs e) => Navigate(new InstallPage(this));

        public void Navigate(UserControl page)
        {
            Page.Content = page;
            page.Focus();
            RefreshMute();
        }

        /// <summary>The corner button: music on or off, remembered in settings, and the Settings page follows it.</summary>
        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            var s = State.Settings;
            if (s.MusicOn && !Music.Playing) { Music.Start(); RefreshMute(); return; }   // faded out after Play: bring it back
            s.MusicOn = !s.MusicOn;
            s.Save();
            if (s.MusicOn) Music.Start(); else Music.Stop();
            RefreshMute();
            var settings = Page.Content as Pages.SettingsPage;
            if (settings != null) settings.SyncMusic();
        }

        public void RefreshMute()
        {
            var on = State.Settings.MusicOn;
            MuteButton.Content = !on ? "♪ music off" : Music.Playing ? "♪ mute" : "♪ play";
            MuteButton.Opacity = on && Music.Playing ? 0.75 : 1.0;
        }

        public void RefreshStrip()
        {
            var s = State.Status;
            StatusWord.Text = s.StateWord;
            StatusWhen.Text = s.CheckedWord + (s.FromCache && s.CheckedAt.HasValue ? " (last seen)" : "")
                            + (s.Online.HasValue && !s.FromCache ? " · " + s.Online.Value + (s.Online.Value == 1 ? " online" : " online") : "");
            StatusNote.Text = string.IsNullOrWhiteSpace(s.Note) ? (s.Error != null ? "Couldn't reach fairywitch.ca." : "") : s.Note;
            StatusNote.ToolTip = string.IsNullOrWhiteSpace(StatusNote.Text) ? null : StatusNote.Text;   // the whole note, however long
            Brush dot;
            switch (s.State)
            {
                case ServerState.Online: dot = (Brush)FindResource("Ok"); break;
                case ServerState.Maintenance: dot = (Brush)FindResource("Warn"); break;
                case ServerState.Offline: dot = (Brush)FindResource("Bad"); break;
                default: dot = (Brush)FindResource("Mist"); break;
            }
            StatusDot.Fill = dot;
            StatusDot.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ((SolidColorBrush)dot).Color, BlurRadius = 8, ShadowDepth = 0 };
            // The News corner is about 60 px tall and 170 wide: an eyebrow and one more thing. A game update takes the
            // news line's place with one short button; the launcher's own restart button wins when both are there, and
            // the menu's Update row still names the game update. 0.2.37 stacked both and the button was cut in half.
            var pending = State.PendingClientUpdate;
            var showGame = pending != null && UpdateButton.Visibility != Visibility.Visible;
            GameUpdateButton.Visibility = showGame ? Visibility.Visible : Visibility.Collapsed;
            NewsLine.Visibility = showGame ? Visibility.Collapsed : Visibility.Visible;
            if (showGame) { GameUpdateButton.Content = "Update game · " + pending.Megabytes; GameUpdateButton.ToolTip = "Game update ready: " + pending.Words + ". Only these are downloaded."; }
            var v = State.Version;
            VersionLine.Text = v.Sentence;
            VersionLine.Foreground = (Brush)FindResource(!v.ExpectedIsPublished ? "Mist" : v.Verdict == VersionVerdict.Ready ? "Ok" : v.Verdict == VersionVerdict.Unknown ? "Warn" : "Bad");
            // the note already sits beside the status pill; News keeps its own line
        }

        private void DrawStars()
        {
            var rnd = new Random(1313);
            for (var i = 0; i < 90; i++)
            {
                var size = rnd.NextDouble() < 0.2 ? 2.0 : 1.0;
                var star = new Ellipse { Width = size, Height = size, Fill = Brushes.White, Opacity = 0.35 + rnd.NextDouble() * 0.5 };
                Canvas.SetLeft(star, rnd.NextDouble() * 1000);
                Canvas.SetTop(star, rnd.NextDouble() * 600);
                Stars.Children.Add(star);
            }
        }

        private async Task SnapshotAndExit()
        {
            UserControl page;
            var key = (App.SnapshotPage ?? "menu").ToLowerInvariant();
            // "<page>-update": the same page with a game update waiting, so a layout change can be checked with it showing
            if (key.EndsWith("-update")) { key = key.Substring(0, key.Length - "-update".Length); State.ShowSampleClientUpdate(); }
            RefreshStrip();
            switch (key)
            {
                case "profiles": page = new ProfilesPage(this); break;
                case "addons": page = new AddonsPage(this); break;
                case "setup": page = new SetupPage(this); break;
                case "settings": page = new SettingsPage(this); break;
                case "fishing": page = new FishingPage(this); break;
                case "vanatunes": page = new VanatunesPage(this); break;
                case "install": page = new InstallPage(this); break;
                case "install-new": page = new InstallPage(this, asNewPlayer: true); break;
                case "capture": page = new CapturePage(this); break;
                case "windower": page = new WindowerPage(this); break;
                case "switcher": page = new SwitcherPage(this); break;
                default: page = new MenuPage(this); break;
            }
            Navigate(page);
            await Task.Delay(700);
            var bmp = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var f = File.Create(App.SnapshotPath)) enc.Save(f);
            if (App.SnapshotPage == "guide")
            {
                var g = new GuideWindow(State); g.Show(); await Task.Delay(500);
                var gb = new RenderTargetBitmap((int)g.ActualWidth, (int)g.ActualHeight, 96, 96, PixelFormats.Pbgra32); gb.Render(g);
                var ge = new PngBitmapEncoder(); ge.Frames.Add(BitmapFrame.Create(gb));
                using (var f = File.Create(System.IO.Path.ChangeExtension(App.SnapshotPath, ".guide.png"))) ge.Save(f);
            }
            Application.Current.Shutdown();
        }
    }
}
