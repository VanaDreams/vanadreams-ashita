using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    public sealed class CaptureRow
    {
        public CaptureSummary Summary { get; set; }
        public string Title => Summary.Name + "  " + JobName(Summary.MainJob) + Summary.MainLevel;
        public string Detail => $"{Summary.CapturedOn} · {Summary.CapturedAt} · {Summary.Items} items · {Summary.Gil:N0} gil · {Summary.Spells} spells · {Summary.KeyItems} key items";
        public static string JobName(int id)
        {
            var names = new[] { "", "WAR", "MNK", "WHM", "BLM", "RDM", "THF", "PLD", "DRK", "BST", "BRD", "RNG", "SAM", "NIN", "DRG", "SMN", "BLU", "COR", "PUP", "DNC", "SCH", "GEO", "RUN" };
            return id > 0 && id < names.Length ? names[id] : "";
        }
    }

    public partial class CapturePage : UserControl
    {
        private readonly MainWindow _win;
        private readonly List<CaptureRow> _rows = new List<CaptureRow>();
        private string Folder => string.IsNullOrEmpty(App.State.AshitaRoot) ? null : Path.Combine(App.State.AshitaRoot, "config", "charcapture");
        private string WindowerFolder => WindowerInstall.Find(App.State.Settings) is string w ? WindowerInstall.SnapshotFolder(w) : null;
        private CaptureRow Current => List.SelectedItem as CaptureRow;

        public CapturePage(MainWindow win)
        {
            InitializeComponent();
            _win = win;
            Load();
        }

        private void Load()
        {
            _rows.Clear();
            // snapshots from the Ashita charcapture and from the Windower one, newest first
            var files = new[] { Folder, WindowerFolder }.Where(d => d != null && Directory.Exists(d))
                                                        .SelectMany(d => Directory.GetFiles(d, "*.json"))
                                                        .OrderByDescending(File.GetLastWriteTime);
            foreach (var f in files)
            {
                try { var s = CaptureSummary.Read(f); if (s != null) _rows.Add(new CaptureRow { Summary = s }); }
                catch (Exception ex) { Log.Warn("snapshot unreadable " + f + ": " + ex.Message); }
            }
            List.ItemsSource = null; List.ItemsSource = _rows;
            Heading.Text = _rows.Count == 0 ? "✦ Snapshots · none yet" : "✦ Snapshots · " + _rows.Count;
            if (_rows.Count > 0) List.SelectedIndex = 0; else Show(null);
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => Show(Current);

        private void Show(CaptureRow row)
        {
            Result.Text = "";
            if (row == null) { SelName.Text = "Nothing to send"; SelMeta.Text = "Take a snapshot in game first."; SelUser.Text = ""; SendButton.IsEnabled = false; return; }
            var s = row.Summary;
            SelName.Text = s.Name;
            SelMeta.Text = $"Captured on {s.CapturedOn}, {s.CapturedAt}. {s.Bytes / 1024} KB.";
            var user = CurrentUser();
            SelUser.Text = string.IsNullOrEmpty(user) ? "No Vanadreams login remembered: set one on the Profiles page, it's what the snapshot is filed under." : "Files under Vanadreams login " + user + ".";
            SelUser.Foreground = (Brush)FindResource(string.IsNullOrEmpty(user) ? "Warn" : "Cream");
            SendButton.IsEnabled = !string.IsNullOrEmpty(user);
        }

        private string CurrentUser()
        {
            var state = App.State;
            var id = state.Settings.LastProfile;
            var cred = string.IsNullOrEmpty(id) ? null : state.Credentials.Get(id);
            if (cred != null && !string.IsNullOrEmpty(cred.User)) return cred.User;
            foreach (var p in state.Profiles.LoadAll().Where(p => !p.IsExample && p.Command.Server.IndexOf("vanadreams", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var c = state.Credentials.Get(p.Id);
                if (c != null && !string.IsNullOrEmpty(c.User)) return c.User;
            }
            return null;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var row = Current;
            if (row == null) return;
            SendButton.IsEnabled = false;
            Result.Text = "Sending…"; Result.Foreground = (Brush)FindResource("Mist");
            var uploader = new CaptureUploader { Url = App.State.Settings.CaptureUrl };
            var r = await uploader.SendAsync(row.Summary.Path, CurrentUser());
            Result.Text = r.Message;
            Result.Foreground = (Brush)FindResource(r.Ok ? "Ok" : "Bad");
            Log.Info("capture send " + row.Summary.Name + ": " + r.Message);
            SendButton.IsEnabled = true;
        }

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            // the folder the picked snapshot is in; else Ashita's, else Windower's
            var folder = Current != null ? Path.GetDirectoryName(Current.Summary.Path) : Folder ?? WindowerFolder;
            if (folder == null) { Result.Text = "Run Setup for Ashita, or put charcapture into Windower, first."; Result.Foreground = (Brush)FindResource("Warn"); return; }
            try { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true }); } catch (Exception ex) { Log.Warn(ex.Message); }
        }

        /// <summary>
        /// For a player on Windower (most retail players are): charcapture for Windower into Windower's addons folder.
        /// Nothing else in Windower is touched; the player loads it with //lua load charcapture.
        /// </summary>
        private async void Windower_Click(object sender, RoutedEventArgs e)
        {
            var state = App.State;
            var root = WindowerInstall.Find(state.Settings);
            if (root == null)
            {
                var d = new OpenFileDialog { Title = "Where is your Windower? Pick Windower.exe", Filter = "Windower.exe|Windower.exe|Programs (*.exe)|*.exe" };
                if (d.ShowDialog() != true) return;
                root = Path.GetDirectoryName(d.FileName);
                if (!WindowerInstall.IsWindower(root)) { Result.Text = "That folder has no Windower in it."; Result.Foreground = (Brush)FindResource("Bad"); return; }
                state.Settings.WindowerRoot = root;
                state.Settings.Save();
            }
            WindowerButton.IsEnabled = false;
            Result.Text = "Putting charcapture into " + root + "…"; Result.Foreground = (Brush)FindResource("Mist");
            try
            {
                await WindowerInstall.InstallAddonAsync(state.Downloader, root, "charcapture");
                Log.Info("charcapture for Windower installed into " + root);
                Result.Text = "charcapture is in Windower now. In game type //lua load charcapture, zone once, open the Merit Points, Job Points and both Currencies menus, then //capture. The snapshot shows up here.";
                Result.Foreground = (Brush)FindResource("Ok");
            }
            catch (Exception ex)
            {
                Log.Error("charcapture for Windower", ex);
                Result.Text = "Could not put charcapture into Windower: " + ex.Message;
                Result.Foreground = (Brush)FindResource("Bad");
            }
            finally { WindowerButton.IsEnabled = true; }
        }
    }
}
