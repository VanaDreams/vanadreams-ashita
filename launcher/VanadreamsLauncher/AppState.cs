using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vanadreams.Services;

namespace Vanadreams
{
    /// <summary>Everything the pages share: settings, stores, the last status and version check.</summary>
    public sealed class AppState
    {
        public LauncherSettings Settings { get; }
        public CredentialStore Credentials { get; }
        public ServerStatusClient StatusClient { get; }
        public Downloader Downloader { get; } = new Downloader();
        public Catalog Catalog { get; private set; } = new Catalog();
        public bool CatalogFromCache { get; private set; }
        public StatusInfo Status { get; private set; } = new StatusInfo();
        public VersionCheck Version { get; private set; } = new VersionCheck { Verdict = VersionVerdict.Unknown };

        public event Action Changed;

        public AppState()
        {
            Settings = LauncherSettings.Load();
            Log.Path = Settings.LogPath;
            Credentials = new CredentialStore(Settings.CredentialsPath);
            StatusClient = new ServerStatusClient(Settings.StatusCachePath) { Url = Settings.StatusUrl };
            Status = StatusClient.ReadCache();
            Status.FromCache = true;
            LoadCatalogFromCache();
            if (Catalog.Items.Count == 0) LoadBundledCatalog();
        }

        public ProfileStore Profiles => new ProfileStore(Settings.AshitaRoot);
        public string AshitaRoot => Settings.AshitaRoot;
        public bool HasAshita => Settings.HasAshita;
        /// <summary>
        /// The Vanadreams game folder, the one the version check and the guide read: the copy Play would start
        /// (VanadreamsGameRoot), else the folder set in Settings, else whatever Windows has registered.
        /// </summary>
        public string FfxiFolder
        {
            get
            {
                var root = VanadreamsGameRoot();
                return root != null ? ClientInstall.GameFolder(root)
                    : !string.IsNullOrWhiteSpace(Settings.FfxiFolderOverride) ? Settings.FfxiFolderOverride
                    : ClientVersion.FindFfxiFolder();
            }
        }

        /// <summary>
        /// The copy of the game a Vanadreams profile starts, looked for across every copy this PC is known to hold:
        /// the one Windows starts now, the one this launcher installed, one beside Ashita or in the default folder,
        /// the folder set in Settings, and the copy the registration was once taken from. GameRegistration.Choose
        /// picks the one that fits the server. Play passes the registered root read from the COM servers; the
        /// pages that only show a version use the registered folder, which is quicker to read.
        /// 4 Oct 2026: 0.2.27 always took the launcher's copy when one was there, whatever state it was in, and
        /// a player whose game had been starting from another copy logged in and watched the console close.
        /// </summary>
        public string VanadreamsGameRoot(string registeredRoot = null)
        {
            if (registeredRoot == null) registeredRoot = GameRegistration.RootOf(ClientVersion.FindFfxiFolder());
            var expected = ExpectedClientVer;
            var lockMode = VerLock;
            return GameRegistration.Choose(registeredRoot,
                new[] { Settings.GameInstallRoot, Settings.AshitaRoot, ClientInstall.DefaultRoot, GameRegistration.RootOf(Settings.FfxiFolderOverride), Settings.OtherGameRoot },
                GameRegistration.HasGame,
                root => ClientVersion.Fits(InstalledVersion(root), expected, lockMode));
        }

        private string ExpectedClientVer => !string.IsNullOrEmpty(Status.ClientVer) ? Status.ClientVer : Settings.ExpectedClientVer;
        private VersionLock VerLock => Status.Lock ?? (Enum.IsDefined(typeof(VersionLock), Settings.VerLock) ? (VersionLock)Settings.VerLock : VersionLock.MatchingOrNewer);

        // patch.cfg runs to megabytes and several copies are read on every look; a file that has not changed is read once
        private readonly System.Collections.Generic.Dictionary<string, Tuple<DateTime, long, string>> _stamps =
            new System.Collections.Generic.Dictionary<string, Tuple<DateTime, long, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The version stamp of the copy in this root, or null when it has none that can be read.</summary>
        public string InstalledVersion(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return null;
            var folder = ClientInstall.GameFolder(root);
            try
            {
                var cfg = new FileInfo(Path.Combine(folder, "patch.cfg"));
                if (!cfg.Exists) return null;
                Tuple<DateTime, long, string> seen;
                lock (_stamps)
                    if (_stamps.TryGetValue(cfg.FullName, out seen) && seen.Item1 == cfg.LastWriteTimeUtc && seen.Item2 == cfg.Length) return seen.Item3;
                var stamp = ClientVersion.ReadInstalled(folder);
                lock (_stamps) _stamps[cfg.FullName] = Tuple.Create(cfg.LastWriteTimeUtc, cfg.Length, stamp);
                return stamp;
            }
            catch (Exception) { return ClientVersion.ReadInstalled(folder); }
        }

        /// <summary>
        /// The copy of the game a profile for another server plays: its own game folder, else the copy Windows had
        /// before this launcher took the registration. Null when neither holds a game; the registration is then left as it is.
        /// </summary>
        public string OtherGameRootFor(Profile p)
        {
            var own = GameRegistration.RootOf(p?.GameFolder);
            if (GameRegistration.HasGame(own)) return own;
            return GameRegistration.HasGame(Settings.OtherGameRoot) ? Settings.OtherGameRoot : null;
        }

        public void Notify() => Changed?.Invoke();

        public VersionCheck CheckVersion()
        {
            var installed = ClientVersion.ReadInstalled(FfxiFolder);
            var published = !string.IsNullOrEmpty(Status.ClientVer);
            Version = ClientVersion.Compare(installed, ExpectedClientVer, VerLock);
            Version.ExpectedIsPublished = published;
            return Version;
        }

        public async Task RefreshStatusAsync()
        {
            StatusClient.Url = Settings.StatusUrl;
            Status = await StatusClient.FetchAsync();
            if (!string.IsNullOrEmpty(Status.ClientVer)) { Settings.ExpectedClientVer = Status.ClientVer; if (Status.Lock.HasValue) Settings.VerLock = (int)Status.Lock.Value; Settings.Save(); }
            CheckVersion();
            Notify();
        }

        private void LoadCatalogFromCache()
        {
            try
            {
                if (File.Exists(Settings.CatalogCachePath)) { Catalog = Catalog.Parse(File.ReadAllText(Settings.CatalogCachePath, Encoding.UTF8)); CatalogFromCache = true; }
            }
            catch (Exception ex) { Log.Warn("catalogue cache unreadable: " + ex.Message); }
        }

        public async Task RefreshCatalogAsync()
        {
            try
            {
                var text = await Downloader.GetStringAsync(Settings.CatalogUrl);
                var cat = Catalog.Parse(text);
                Catalog = cat;
                CatalogFromCache = false;
                Directory.CreateDirectory(Path.GetDirectoryName(Settings.CatalogCachePath));
                File.WriteAllText(Settings.CatalogCachePath, text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Warn("catalogue fetch failed, using cache: " + ex.Message);
                CatalogFromCache = true;
                if (Catalog.Items.Count == 0) LoadBundledCatalog();
            }
            Notify();
        }

        /// <summary>The catalogue shipped inside the exe, used before the first successful fetch.</summary>
        private void LoadBundledCatalog()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/assets/catalog.json");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info == null) return;
                using (var reader = new StreamReader(info.Stream, Encoding.UTF8)) Catalog = Catalog.Parse(reader.ReadToEnd());
            }
            catch (Exception ex) { Log.Warn("bundled catalogue unreadable: " + ex.Message); }
        }

        /// <summary>
        /// Installs and ticks the catalogue's on-by-default items, once each. After that the box is the player's:
        /// an item they untick is never ticked for them again. One that fails to install is tried at the next start.
        /// </summary>
        public async Task GiveDefaultsAsync()
        {
            if (!HasAshita) return;
            var changed = false;
            foreach (var item in AddonInstaller.DefaultsToGive(Catalog, Settings.GivenDefaults))
            {
                try
                {
                    if (item.Source == SourceType.RepoFolder)
                        await AddonInstaller.InstallRepoFolderAsync(Downloader, Settings, AshitaRoot, item);
                    else if (item.Source != SourceType.Bundled) continue;   // release archives are installed from the Addons page
                    if (!Settings.EnabledAddons.Contains(item.Id, StringComparer.OrdinalIgnoreCase)) Settings.EnabledAddons.Add(item.Id);
                    Settings.GivenDefaults.Add(item.Id);
                    changed = true;
                    Log.Info("given by default: " + item.Id);
                }
                catch (Exception ex) { Log.Warn("default " + item.Id + " not installed, will try again: " + ex.Message); }
            }
            if (!changed) return;
            Settings.Save();
            ApplyEnabledAddons();
            Notify();
        }

        public int EnabledCount => Settings.EnabledAddons.Count(id => Catalog.Find(id) != null);
        public int UpdateCount => Catalog.Items.Count(i => i.Source == SourceType.GithubRelease && Settings.InstalledVersions.ContainsKey(i.Id) && !string.IsNullOrEmpty(i.Version) && !string.Equals(Settings.InstalledVersions[i.Id], i.Version, StringComparison.OrdinalIgnoreCase));

        public string LoaderVersion()
        {
            // xiloader's FileVersion string is empty; the fixed parts carry the number (Loader.Installed).
            var v = Loader.Installed(AshitaRoot);
            if (v == null) return "missing";
            return Loader.IsSupported(v) ? v.ToString() : v + " (server takes " + Loader.RequiredMajor + "." + Loader.RequiredMinor + ")";
        }

        public string AshitaUpdated()
        {
            try { var dll = Path.Combine(AshitaRoot, "Ashita.dll"); return File.Exists(dll) ? File.GetLastWriteTime(dll).ToString("yyyy-MM-dd") : "not installed"; }
            catch (Exception) { return ""; }
        }

        /// <summary>Write the startup script and the POL plugin flags from the enabled set.</summary>
        public void ApplyEnabledAddons(Profile profile = null)
        {
            if (!HasAshita) { Log.Warn("apply addons: no Ashita folder set, nothing written"); return; }
            PivotConfig.RemoveRootPath(AshitaRoot);   // a value an earlier launcher wrote that stops overlays loading
            var entries = Catalog.ScriptEntries(Settings.EnabledAddons);
            var scriptPath = Path.Combine(AshitaRoot, "scripts", "vanadreams.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath));
            ScriptWriter.Write(scriptPath, entries);
            // XIPivot overlays: listed in pivot.ini while enabled, dropped from it when not
            foreach (var overlay in Catalog.Items.Where(i => i.Install == InstallAction.PivotOverlay))
            {
                try
                {
                    if (Settings.EnabledAddons.Contains(overlay.Id, StringComparer.OrdinalIgnoreCase) && overlay.IsInstalled(AshitaRoot)) PivotConfig.AddOverlay(AshitaRoot, overlay.Id);
                    else PivotConfig.RemoveOverlay(AshitaRoot, overlay.Id);
                }
                catch (Exception ex) { Log.Warn("pivot.ini " + overlay.Id + ": " + ex.Message); }
            }
            var pol = entries.Where(e => e.Kind == LoadKind.PolPlugin).Select(e => e.LoadName).ToList();
            foreach (var p in profile != null ? new[] { profile } : Profiles.LoadAll().Where(x => !x.IsExample).ToArray())
            {
                var changed = false;
                foreach (var name in pol) if (!p.PolPlugins.ContainsKey(name) || !p.PolPlugins[name]) { p.PolPlugins[name] = true; changed = true; }
                foreach (var key in p.PolPlugins.Keys.ToList())
                    if (p.PolPlugins[key] && !pol.Contains(key, StringComparer.OrdinalIgnoreCase) && Catalog.Items.Any(i => i.LoadKind == LoadKind.PolPlugin && string.Equals(i.LoadName, key, StringComparison.OrdinalIgnoreCase)))
                    { p.PolPlugins[key] = false; changed = true; }
                if (!string.Equals(p.Script, "vanadreams.txt", StringComparison.OrdinalIgnoreCase)) { p.Script = "vanadreams.txt"; changed = true; }
                if (changed) p.Save();
            }
        }
    }
}
