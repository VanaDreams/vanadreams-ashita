using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Vanadreams.Services;

namespace Vanadreams.Pages
{
    /// <summary>
    /// For players who come from Windower: the Ashita look-alikes of their addons in one click, their init.txt binds
    /// and aliases in the startup script, and their GearSwap sets as LuAshitacast profiles. Windower itself is never
    /// needed or touched; its files are only read.
    /// </summary>
    public partial class WindowerPage : UserControl
    {
        private readonly MainWindow _win;
        private bool _busy;
        private string _openFolder;

        public WindowerPage(MainWindow win)
        {
            InitializeComponent();
            _win = win;
            var named = WindowerPreset.Items.Where(l => !string.Equals(l.Windower, "GearSwap", StringComparison.OrdinalIgnoreCase) || l.CatalogId == "luashitacast").ToList();
            var pairs = named.Take(4).Select(l => l.Windower + " → " + (App.State.Catalog.Find(l.CatalogId)?.Name ?? l.CatalogId));
            PresetList.Text = string.Join(", ", pairs) + (named.Count > 4 ? ", and " + (named.Count - 4) + " more." : ".");
            PresetList.ToolTip = string.Join("\n", WindowerPreset.Items.Select(l => l.Windower + " → " + (App.State.Catalog.Find(l.CatalogId)?.Name ?? l.CatalogId) + ": " + l.Note));
        }

        /// <summary>The Windower folder, where one of the usual places has it; null otherwise.</summary>
        private static string WindowerFolder()
        {
            var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new List<string>();
            foreach (var root in new[] { x86, pf, @"C:\", @"D:\" }.Where(r => !string.IsNullOrEmpty(r)))
                foreach (var name in new[] { "Windower4", "Windower", "Windower 4" })
                    candidates.Add(Path.Combine(root, name));
            return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "Windower.exe")) || Directory.Exists(Path.Combine(c, "addons")));
        }

        private void Say(string text, string brush = null, string folder = null)
        {
            Result.Text = text;
            Result.Foreground = (Brush)FindResource(brush ?? "Cream");
            ResultScroll.ScrollToTop();
            _openFolder = folder;
            OpenButton.Visibility = folder != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool Ready()
        {
            if (_busy) return false;
            if (App.State.HasAshita) return true;
            Say("Run Setup first: these steps write into the Ashita folder.", "Warn");
            return false;
        }

        private void Busy(bool on)
        {
            _busy = on;
            PresetButton.IsEnabled = InitButton.IsEnabled = GearSwapButton.IsEnabled = !on;
            Progress.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            Progress.IsIndeterminate = on;
        }

        // ---- 1. the look-alikes ----

        private async void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready()) return;
            var state = App.State;
            var items = WindowerPreset.CatalogItems(state.Catalog);
            if (items.Count == 0) { Say("The catalogue has none of them yet. Try again once it has loaded.", "Warn"); return; }
            Busy(true);
            var done = new List<string>();
            var failed = new List<string>();
            try
            {
                var n = 0;
                foreach (var item in items)
                {
                    n++;
                    Say("Installing " + item.Name + " (" + n + " of " + items.Count + ")…");
                    try
                    {
                        if (!item.IsInstalled(state.AshitaRoot))
                        {
                            if (item.Source == SourceType.GithubRelease)
                                await AddonInstaller.InstallReleaseAsync(state.Downloader, state.Settings, state.AshitaRoot, item);
                            else if (item.Source == SourceType.RepoFolder)
                                await AddonInstaller.InstallRepoFolderAsync(state.Downloader, state.Settings, state.AshitaRoot, item);
                            else if (item.Source == SourceType.Bundled)
                                throw new InvalidOperationException("it ships with Ashita but its file is missing; run Repair");
                        }
                        if (!state.Settings.EnabledAddons.Contains(item.Id, StringComparer.OrdinalIgnoreCase)) state.Settings.EnabledAddons.Add(item.Id);
                        var look = WindowerPreset.Items.FirstOrDefault(l => l.CatalogId == item.Id);
                        done.Add("✓ " + item.Name + (look == null ? "" : " (for " + look.Windower + ": " + look.Note + ")"));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("windower preset " + item.Id + ": " + ex.Message);
                        failed.Add("✗ " + item.Name + ": " + ex.Message);
                    }
                }
                state.Settings.Save();
                state.ApplyEnabledAddons();
                state.Notify();
                var sb = new StringBuilder();
                sb.AppendLine(failed.Count == 0 ? "All ticked, and they load the next time you press Play." : "Ticked what installed. Press the button again to retry the rest.");
                sb.AppendLine();
                foreach (var l in done.Concat(failed)) sb.AppendLine(l);
                sb.AppendLine();
                sb.Append("Untick any of them on the Addons page.");
                Say(sb.ToString(), failed.Count == 0 ? "Ok" : "Warn");
            }
            catch (Exception ex)
            {
                Log.Error("windower preset", ex);
                Say(ex.Message, "Bad");
            }
            finally { Busy(false); }
        }

        // ---- 2. init.txt ----

        private void Init_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready()) return;
            var windower = WindowerFolder();
            var scripts = windower == null ? null : Path.Combine(windower, "scripts");
            var d = new OpenFileDialog
            {
                Title = "Pick Windower's init.txt (Windower4\\scripts\\init.txt)",
                Filter = "init.txt|init.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = scripts != null && Directory.Exists(scripts) ? scripts : "",
            };
            if (d.ShowDialog() != true) return;
            try
            {
                var state = App.State;
                var conv = WindowerInit.Convert(File.ReadAllText(d.FileName));
                var scriptPath = Path.Combine(state.AshitaRoot, "scripts", "vanadreams.txt");
                if (!File.Exists(scriptPath)) state.ApplyEnabledAddons();   // writes the script, and points every profile at it
                var existing = File.Exists(scriptPath) ? File.ReadAllText(scriptPath) : ScriptWriter.Build(state.Catalog.ScriptEntries(state.Settings.EnabledAddons), "");
                File.WriteAllText(scriptPath, WindowerInit.MergeIntoScript(existing, conv.Lines), new UTF8Encoding(false));
                Log.Info("imported " + d.FileName + ": " + conv.Binds + " binds, " + conv.Aliases + " aliases, " + conv.Skipped.Count + " skipped");

                var sb = new StringBuilder();
                sb.AppendLine("Imported " + conv.Binds + " bind" + (conv.Binds == 1 ? "" : "s") + " and " + conv.Aliases + " alias" + (conv.Aliases == 1 ? "" : "es") + " into scripts\\vanadreams.txt. They work the next time you press Play.");
                if (conv.Skipped.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(conv.Skipped.Count + " line" + (conv.Skipped.Count == 1 ? " is" : "s are") + " Windower-only, so " + (conv.Skipped.Count == 1 ? "it is" : "they are") + " in the script as comments saying why:");
                    foreach (var s in conv.Skipped.Take(8)) sb.AppendLine("  " + s);
                    if (conv.Skipped.Count > 8) sb.AppendLine("  and " + (conv.Skipped.Count - 8) + " more");
                }
                var looks = conv.WindowerAddons.Select(WindowerPreset.For).Where(l => l != null).Select(l => l.Windower + " → " + (state.Catalog.Find(l.CatalogId)?.Name ?? l.CatalogId)).Distinct().ToList();
                if (looks.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Your init.txt loaded " + string.Join(", ", looks) + ". Step 1 installs those.");
                }
                Say(sb.ToString().TrimEnd(), "Ok", Path.GetDirectoryName(scriptPath));
            }
            catch (Exception ex)
            {
                Log.Error("init.txt import", ex);
                Say("The import failed: " + ex.Message, "Bad");
            }
        }

        // ---- 3. GearSwap ----

        private void GearSwap_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready()) return;
            var windower = WindowerFolder();
            var data = windower == null ? null : Path.Combine(windower, "addons", "GearSwap", "data");
            var d = new OpenFileDialog
            {
                Title = "Pick your GearSwap job files (Windower4\\addons\\GearSwap\\data)",
                Filter = "GearSwap files (*.lua)|*.lua|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = data != null && Directory.Exists(data) ? data : "",
            };
            if (d.ShowDialog() != true) return;

            var typed = (CharacterBox.Text ?? "").Trim();
            var noJob = new List<string>();
            var noName = new List<string>();
            var groups = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in d.FileNames)
            {
                var job = GearSwapImport.JobOf(f);
                if (job == null) { noJob.Add(Path.GetFileName(f)); continue; }
                var who = GearSwapImport.CharacterFor(f) ?? (typed.Length > 0 ? typed : null);
                if (who == null) { noName.Add(Path.GetFileName(f)); continue; }
                var key = who + "_" + job;
                if (!groups.ContainsKey(key)) groups[key] = new List<KeyValuePair<string, string>>();
                try { groups[key].Add(new KeyValuePair<string, string>(f, File.ReadAllText(f))); }
                catch (Exception ex) { noJob.Add(Path.GetFileName(f) + " (" + ex.Message + ")"); }
            }

            var state = App.State;
            var folder = Path.Combine(state.AshitaRoot, "config", "addons", "luashitacast");
            var sb = new StringBuilder();
            var wrote = 0;
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var g in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var parts = g.Key.Split('_');
                    var job = GearSwapImport.Read(string.Join("_", parts.Take(parts.Length - 1)), parts.Last(), g.Value);
                    if (job.Sets.Count == 0) { sb.AppendLine("✗ " + g.Key + ": no gear sets found in " + string.Join(", ", g.Value.Select(v => Path.GetFileName(v.Key)))); continue; }
                    var target = Path.Combine(folder, GearSwapImport.ProfileFileName(job));
                    string kept = null;
                    if (File.Exists(target))
                    {
                        kept = target + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        File.Move(target, kept);
                    }
                    File.WriteAllText(target, GearSwapImport.WriteProfile(job, DateTime.Now.ToString("yyyy-MM-dd")), new UTF8Encoding(false));
                    wrote++;
                    Log.Info("gearswap import " + g.Key + ": " + job.Sets.Count + " sets to " + target);
                    sb.AppendLine("✓ " + Path.GetFileName(target) + ": " + job.Sets.Count + " sets"
                                  + (job.Unresolved.Count > 0 ? ", " + job.Unresolved.Count + " slot" + (job.Unresolved.Count == 1 ? "" : "s") + " GearSwap fills in while playing (listed at the top of the file)" : "")
                                  + (kept != null ? ". The old one is kept as " + Path.GetFileName(kept) : ""));
                }
            }
            catch (Exception ex)
            {
                Log.Error("gearswap import", ex);
                sb.AppendLine("✗ " + ex.Message);
            }

            if (noName.Count > 0) sb.AppendLine("Not imported, type the character name above and import again: " + string.Join(", ", noName));
            if (noJob.Count > 0) sb.AppendLine("Skipped, no job in the file name: " + string.Join(", ", noJob));
            if (wrote > 0)
            {
                var lac = state.Catalog.Find("luashitacast");
                sb.AppendLine();
                sb.AppendLine(lac != null && lac.IsInstalled(state.AshitaRoot) && state.Settings.EnabledAddons.Contains("luashitacast", StringComparer.OrdinalIgnoreCase)
                    ? "LuAshitacast loads the profile for your job when you log in. /lac load picks it up without logging out."
                    : "LuAshitacast reads these profiles, and it is not ticked yet: step 1 installs it.");
            }
            Say(sb.ToString().TrimEnd(), wrote > 0 && noName.Count == 0 ? "Ok" : wrote > 0 ? "Warn" : "Bad", wrote > 0 ? folder : null);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_openFolder == null) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _openFolder + "\"") { UseShellExecute = true }); } catch (Exception ex) { Log.Warn(ex.Message); }
        }

        private void Back_Click(object sender, RoutedEventArgs e) => _win.Navigate(new MenuPage(_win));
    }
}
