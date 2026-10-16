using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    /// <summary>Makes the bug report zip, shows it in its folder and opens the Discord to post it in.</summary>
    public partial class BugReportPanel : UserControl
    {
        /// <summary>
        /// Where the player posts the zip. The server invite, which opens Discord on Vanadreams; the channel is named in the
        /// panel. A link straight to #bugs-we-all-found needs the server's and channel's ids in its place.
        /// </summary>
        public const string BugsChannel = MenuPage.DiscordInvite;

        public BugReportPanel()
        {
            InitializeComponent();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var state = App.State;
            SendButton.IsEnabled = false;
            Result.Foreground = (Brush)FindResource("Mist");
            Result.Text = "Gathering logs…";
            try
            {
                var now = DateTime.Now;
                var zip = BugReport.DefaultPath(now);
                var dumps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
                var version = Updater.Normalise(Updater.Current).ToString();
                var names = await Task.Run(() => BugReport.Build(zip, LauncherSettings.DataFolder, state.HasAshita ? state.AshitaRoot : null, dumps, version, now));
                Log.Info("bug report: " + zip + " with " + names.Count + " file(s)");
                Result.Foreground = (Brush)FindResource("Ok");
                Result.Text = "Saved " + Path.GetFileName(zip) + " on your Desktop. Drop it in #bugs-we-all-found with a line on what happened.";
                try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + zip + "\"") { UseShellExecute = true }); } catch (Exception ex) { Log.Warn("show zip: " + ex.Message); }
                MenuPage.OpenUrl(BugsChannel);
            }
            catch (Exception ex)
            {
                Log.Error("bug report", ex);
                Result.Foreground = (Brush)FindResource("Bad");
                Result.Text = ex.Message;
            }
            finally { SendButton.IsEnabled = true; }
        }
    }
}
