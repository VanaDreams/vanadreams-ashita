using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    public partial class SettingsPage : UserControl
    {
        private readonly MainWindow _win;

        public SettingsPage(MainWindow win)
        {
            InitializeComponent();
            _win = win;
            var s = App.State.Settings;
            AshitaBox.Text = s.AshitaRoot; FfxiBox.Text = s.FfxiFolderOverride; CatalogBox.Text = s.CatalogUrl; StatusBox.Text = s.StatusUrl;
            // read-only: it comes from the status route, never typed here
            var st = App.State.Status;
            ExpectedBox.Text = string.IsNullOrEmpty(st.ClientVer) ? "not published by the server yet" : st.ClientVer + " · lock " + (st.Lock.HasValue ? ((int)st.Lock.Value).ToString() : "2") + " · from fairywitch.ca";
            ShowPlays();
            MusicBox.IsChecked = s.MusicOn;
            LowSpecBox.IsChecked = s.LowSpec;
            MusicCredit.Text = "made for Vanadreams with SoundBreak";
            About.Text = "Vanadreams Launcher " + typeof(App).Assembly.GetName().Version + " · github.com/VanaDreams/vanadreams-ashita · no telemetry";
            ShowLayout();
        }

        /// <summary>The layout in use is the gold button.</summary>
        private void ShowLayout()
        {
            var h = _win.Horizontal;
            VerticalButton.Style = (Style)FindResource(h ? "GhostButton" : "GoldButton");
            HorizontalButton.Style = (Style)FindResource(h ? "GoldButton" : "GhostButton");
        }

        /// <summary>The menu moves the moment the player picks; it is kept at once, without Save.</summary>
        private void Layout_Click(object sender, RoutedEventArgs e)
        {
            var s = App.State.Settings;
            s.MenuLayout = (string)((FrameworkElement)sender).Tag;
            s.Save();
            _win.ApplyLayout();
            ShowLayout();
        }

        /// <summary>The copy Play starts for Vanadreams, which is not always the one Windows starts now: show the first, tip the second.</summary>
        private void ShowPlays()
        {
            var plays = App.State.VanadreamsGameRoot();
            DetectedBox.Text = plays != null ? ClientInstall.GameFolder(plays) : "no game found yet; Play starts what Windows has";
            DetectedBox.ToolTip = "Windows starts now: " + (ClientVersion.FindFfxiFolder() ?? "nothing registered by PlayOnline") +
                                  "\nTo always play one copy, put its FINAL FANTASY XI folder in Vanadreams game folder above.";
        }

        /// <summary>The corner mute changed the setting; show it.</summary>
        public void SyncMusic() => MusicBox.IsChecked = App.State.Settings.MusicOn;

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var s = App.State.Settings;
            s.AshitaRoot = AshitaBox.Text.Trim(); s.FfxiFolderOverride = FfxiBox.Text.Trim(); s.CatalogUrl = CatalogBox.Text.Trim(); s.StatusUrl = StatusBox.Text.Trim();
            s.MusicOn = MusicBox.IsChecked == true;
            var lowSpecChanged = s.LowSpec != (LowSpecBox.IsChecked == true);
            s.LowSpec = LowSpecBox.IsChecked == true;
            s.Save();
            if (lowSpecChanged && s.HasAshita) Effects.Apply(s.AshitaRoot, s.LowSpec);
            if (s.MusicOn) Music.Start(); else Music.Stop();
            _win.RefreshMute();
            App.State.CheckVersion();
            ShowPlays();
            Note.Text = !string.IsNullOrWhiteSpace(s.FfxiFolderOverride) && !GameRegistration.HasGame(GameRegistration.RootOf(s.FfxiFolderOverride))
                ? "Saved, but there is no game in that Vanadreams game folder: pick FINAL FANTASY XI, with PlayOnlineViewer beside it."
                : "Saved.";
            App.State.Notify();
            await App.State.RefreshStatusAsync();
        }

        private async void Catalog_Click(object sender, RoutedEventArgs e)
        {
            Note.Text = "Fetching…";
            await App.State.RefreshCatalogAsync();
            Note.Text = App.State.CatalogFromCache ? "Could not fetch; using the cached copy." : "Catalogue updated: " + App.State.Catalog.Items.Count + " items.";
        }

        private void Shortcuts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!SelfInstall.IsInstalledCopy) { SelfInstall.Install(); Note.Text = "Installed to " + SelfInstall.InstallDir + " with shortcuts. Use the shortcut from now on."; }
                else { SelfInstall.MakeShortcuts(); Note.Text = "Shortcuts made."; }
            }
            catch (Exception ex) { Note.Text = ex.Message; }
        }

        private void Data_Click(object sender, RoutedEventArgs e) => Open(LauncherSettings.DataFolder);
        private void Log_Click(object sender, RoutedEventArgs e) => Open(App.State.Settings.LogPath);
        private void Open(string path)
        {
            try { if (!File.Exists(path) && !Directory.Exists(path)) Directory.CreateDirectory(LauncherSettings.DataFolder); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Note.Text = ex.Message; }
        }
    }
}
