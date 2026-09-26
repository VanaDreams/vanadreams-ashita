using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    public partial class InstallPage : UserControl
    {
        private readonly MainWindow _win;
        private string _root = ClientInstall.DefaultRoot;
        private ClientManifest _manifest;
        private CancellationTokenSource _cancel;

        private readonly bool _asNewPlayer;

        /// <param name="asNewPlayer">Snapshot hook: show the page as a PC with no game on it sees it.</param>
        public InstallPage(MainWindow win, bool asNewPlayer = false)
        {
            InitializeComponent();
            _win = win;
            _asNewPlayer = asNewPlayer;
            Loaded += async (s, e) => { Refresh(); await LoadManifestAsync(); };
        }

        private static string Gb(long bytes) => (bytes / 1073741824.0).ToString("0.0") + " GB";
        private string Expected => App.State.Version.ExpectedIsPublished ? App.State.Version.Expected : null;

        private void Refresh()
        {
            var found = _asNewPlayer ? null : ClientVersion.FindFfxiFolder();
            var have = ClientVersion.ReadInstalled(found);
            var want = Expected;
            FolderText.Text = ClientInstall.GameFolder(_root);
            VersionText.Text = want == null ? "the server hasn't published its version yet" : want + " · the version the server runs";
            FoundText.Text = found == null ? "none on this PC" : (have ?? "version unknown") + " · " + found.TrimEnd('\\');
            var matches = found != null && have != null && want != null && string.CompareOrdinal(have.Substring(0, 6), want.Substring(0, 6)) >= 0;
            State.Text = found == null ? "Not installed yet." : matches ? "✓ Installed · matches the server" : "Installed, but older than the server needs. Install game fetches the right one.";
            State.Foreground = (Brush)FindResource(found == null ? "Warn" : matches ? "Ok" : "Warn");
            InstallButton.Content = _cancel != null ? "Stop" : found == null ? "Install game" : "Install a fresh copy";

            // Installing somewhere new only tells Windows about it once the whole download finishes, so a
            // second install that was stopped, failed, or had its permission prompt refused leaves the old
            // folder registered and the new one unreachable to the game. Say so BEFORE the download rather
            // than leaving the player to find out at the login screen.
            var registeredElsewhere = found != null &&
                !string.Equals(found.TrimEnd('\\'), ClientInstall.GameFolder(_root).TrimEnd('\\'),
                               StringComparison.OrdinalIgnoreCase);
            MismatchText.Text = registeredElsewhere
                ? "Windows currently points the game at " + found.TrimEnd('\\') +
                  ". Installing here will point it at this folder instead, once the install finishes. " +
                  "If the files are already here, press Register installed game - it takes a moment and downloads nothing."
                : "";
            MismatchText.Visibility = registeredElsewhere ? Visibility.Visible : Visibility.Collapsed;
            RegisterButton.IsEnabled = _cancel == null && Directory.Exists(ClientInstall.GameFolder(_root));

            // Only offered when there is something to give back: a folder we took the registration from,
            // which still has a game in it, and which is not the one Windows already points at.
            var previous = App.State.Settings.PreviousGameFolder;
            var canRestore = !string.IsNullOrWhiteSpace(previous) &&
                File.Exists(Path.Combine(previous, "FFXiMain.dll")) &&
                !string.Equals(previous.TrimEnd('\\'), found?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            RestoreButton.Visibility = canRestore ? Visibility.Visible : Visibility.Collapsed;
            RestoreButton.IsEnabled = canRestore && _cancel == null;
            if (canRestore) RestoreButton.Content = "Give it back to " + Path.GetFileName(previous.TrimEnd('\\'));
        }

        private async Task LoadManifestAsync()
        {
            if (Expected == null) { SizeText.Text = "known once the server's version is published"; return; }
            SizeText.Text = "checking…";
            try
            {
                _manifest = ClientManifest.Parse(await App.State.Downloader.GetStringAsync(ClientInstall.ManifestUrl(Expected)));
                SizeText.Text = Gb(_manifest.TotalSize) + " · " + Gb(_manifest.TotalUnpacked) + " on disk";
            }
            catch (Exception ex)
            {
                Log.Warn("client manifest: " + ex.Message);
                SizeText.Text = "the download isn't available right now";
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (_cancel != null) { _cancel.Cancel(); return; }
            if (_manifest == null) { await LoadManifestAsync(); if (_manifest == null) { ProgressText.Text = "The game download isn't available right now. Try again in a while."; return; } }

            var drive = new DriveInfo(Path.GetPathRoot(_root));
            var needed = _manifest.TotalSize + _manifest.TotalUnpacked;
            if (drive.AvailableFreeSpace < needed) { ProgressText.Text = $"{drive.Name} has {Gb(drive.AvailableFreeSpace)} free and the install needs {Gb(needed)}. Pick another folder."; return; }

            var state = App.State;
            _cancel = new CancellationTokenSource();
            FolderButton.IsEnabled = false; Refresh();
            Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = false;
            var started = DateTime.UtcNow; long startedAt = -1;
            var progress = new Progress<InstallProgress>(p =>
            {
                Progress.Value = 100.0 * p.Done / Math.Max(1, p.Total);
                if (startedAt < 0) startedAt = p.Done;
                var secs = (DateTime.UtcNow - started).TotalSeconds;
                var rate = secs > 5 ? (p.Done - startedAt) / secs : 0;
                var left = rate > 0 ? " · about " + Math.Max(1, (int)Math.Ceiling((p.Total - p.Done) / rate / 60)) + " min left" : "";
                ProgressText.Text = $"{p.Stage} part {p.Part} of {p.Parts} · {Gb(p.Done)} of {Gb(p.Total)}{left}";
            });
            try
            {
                await ClientInstall.InstallFilesAsync(state.Downloader, _manifest, _root, state.Settings.DownloadsFolder, progress, _cancel.Token);
                ProgressText.Text = "Files in place. Windows will ask for permission to register the game…";
                var ok = await RegisterWithConsentAsync();
                ProgressText.Text = ok ? "Installed. Run Setup if you haven't, then press Play."
                                       : "The permission prompt was refused, so the game isn't registered yet. Press Install game again to finish; nothing downloads twice.";
                state.CheckVersion();
                state.Notify();
            }
            catch (OperationCanceledException) { ProgressText.Text = "Stopped. Press Install game to carry on where it left off."; }
            catch (Exception ex) { Log.Error("client install", ex); ProgressText.Text = ex.Message; }
            finally
            {
                _cancel = null; FolderButton.IsEnabled = true; Progress.Visibility = Visibility.Collapsed;
                Refresh();
            }
        }

        /// <summary>
        /// Register this folder with Windows, asking first when that takes the registration off another
        /// copy of the game.
        ///
        /// Windows holds ONE Final Fantasy XI registration for the whole machine. Registering a copy
        /// therefore takes it from whatever had it - commonly a working retail PlayOnline install, which
        /// then cannot find its own game. The launcher used to do that silently, as the last step of a
        /// download, which is how a player ended up with two installs and a registration pointing at the
        /// one he had stopped using.
        ///
        /// False when the player declined, or the permission prompt was refused.
        /// </summary>
        private async Task<bool> RegisterWithConsentAsync()
        {
            var game = ClientInstall.GameFolder(_root);
            var current = ClientVersion.FindFfxiFolder();
            var takingItFrom = current != null &&
                !string.Equals(current.TrimEnd('\\'), game.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

            if (takingItFrom)
            {
                var answer = MessageBox.Show(
                    "Windows points Final Fantasy XI at:\n\n" + current.TrimEnd('\\') +
                    "\n\nIt can only point at one copy. Registering:\n\n" + game +
                    "\n\ntakes it from the other one, and anything that launches that copy - a retail " +
                    "PlayOnline included - will no longer find its game.\n\n" +
                    "The old folder is remembered, so it can be handed back from this page.\n\n" +
                    "Point Windows at this folder?",
                    "Vanadreams Launcher", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes)
                {
                    ProgressText.Text = "Left alone. Windows still points at " + current.TrimEnd('\\') + ".";
                    return false;
                }
            }

            var ok = await Task.Run(() => ClientInstall.RunRegister(_root, App.State.Settings.DownloadsFolder));
            if (ok && takingItFrom)
            {
                App.State.Settings.PreviousGameFolder = current.TrimEnd('\\');
                App.State.Settings.Save();
            }
            return ok;
        }

        /// <summary>Hand the registration back to the copy it was taken from - no download, one prompt.</summary>
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            var previous = App.State.Settings.PreviousGameFolder;
            if (string.IsNullOrWhiteSpace(previous)) return;
            if (!File.Exists(Path.Combine(previous, "FFXiMain.dll")))
            {
                ProgressText.Text = "There is no game in " + previous + " any more, so it cannot be handed back.";
                App.State.Settings.PreviousGameFolder = "";
                App.State.Settings.Save();
                Refresh();
                return;
            }

            var root = Path.GetDirectoryName(previous.TrimEnd('\\'));
            RestoreButton.IsEnabled = false;
            ProgressText.Text = "Windows will ask for permission…";
            try
            {
                var taken = ClientVersion.FindFfxiFolder();
                var ok = await Task.Run(() => ClientInstall.RunRegister(root, App.State.Settings.DownloadsFolder));
                if (ok)
                {
                    App.State.Settings.PreviousGameFolder = taken == null ? "" : taken.TrimEnd('\\');
                    App.State.Settings.Save();
                    ProgressText.Text = "Windows points at " + previous + " again.";
                }
                else ProgressText.Text = "The permission prompt was refused, so nothing changed.";
                App.State.CheckVersion();
                App.State.Notify();
            }
            catch (Exception ex) { Log.Error("restore registration", ex); ProgressText.Text = ex.Message; }
            finally { Refresh(); }
        }

        /// <summary>
        /// Tell Windows the game is in this folder, without downloading anything.
        ///
        /// Registration used to be reachable only at the end of a completed install, so an install that
        /// was stopped, failed, or had its permission prompt refused left Windows pointing at an older
        /// folder - and the game, which reads that registration rather than anything this launcher holds,
        /// would log in and then close. Re-downloading several gigabytes to fix a registry value was the
        /// only way out. This is that fix on its own.
        /// </summary>
        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var game = ClientInstall.GameFolder(_root);
            if (!File.Exists(Path.Combine(game, "FFXiMain.dll")))
            {
                ProgressText.Text = "No game in " + game + " to register. Pick the folder the game is actually in, or press Install game.";
                return;
            }

            RegisterButton.IsEnabled = false;
            ProgressText.Text = "Windows will ask for permission to register the game…";
            try
            {
                var ok = await RegisterWithConsentAsync();
                ProgressText.Text = ok
                    ? "Registered. Windows now points the game at " + game + ". Press Play."
                    : "The permission prompt was refused, so nothing changed. Press Register installed game and choose Yes.";
                App.State.CheckVersion();
                App.State.Notify();
            }
            catch (Exception ex)
            {
                Log.Error("register installed game", ex);
                ProgressText.Text = ex.Message;
            }
            finally { Refresh(); }
        }

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            var d = new System.Windows.Forms.FolderBrowserDialog { Description = "Pick the folder the game goes in (Final Fantasy XI and PlayOnlineViewer are made inside it)", SelectedPath = _root };
            if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            _root = d.SelectedPath;
            Refresh();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_cancel != null) { ProgressText.Text = "Stop the install first."; return; }
            _win.Navigate(new MenuPage(_win));
        }
    }
}
