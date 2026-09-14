using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GPlayExophaseImporter.Settings
{
    public class GPlaySettings : ObservableObject
    {
        private string username = string.Empty;
        private bool importGPlay = true;
        private bool importPlaytime = true;
        private bool importLastActivity = true;
        private bool skipDemos = false;
        private int minPlaytimeMinutes = 0;
        private bool forcePlaytimeSync = false;

        public string Username
        {
            get => username;
            set => SetValue(ref username, value);
        }

        public bool ImportGPlay
        {
            get => importGPlay;
            set => SetValue(ref importGPlay, value);
        }

        public bool ImportPlaytime
        {
            get => importPlaytime;
            set => SetValue(ref importPlaytime, value);
        }

        public bool ImportLastActivity
        {
            get => importLastActivity;
            set => SetValue(ref importLastActivity, value);
        }

        public bool SkipDemos
        {
            get => skipDemos;
            set => SetValue(ref skipDemos, value);
        }

        public int MinPlaytimeMinutes
        {
            get => minPlaytimeMinutes;
            set => SetValue(ref minPlaytimeMinutes, value);
        }

        /// <summary>
        /// When enabled, refreshes playtime / last played of already-imported games on every
        /// library update (see <see cref="GPlayExophaseImporterPlugin.OnLibraryUpdated"/>).
        /// Scoped to this plugin's games, so it doesn't touch multi-platform playtime like
        /// Playnite's global "Import playtime: Always" setting would.
        /// </summary>
        public bool ForcePlaytimeSync
        {
            get => forcePlaytimeSync;
            set => SetValue(ref forcePlaytimeSync, value);
        }

        // Internal cache
        public string CachedUsername { get; set; } = string.Empty;
        public string CachedPlayerId { get; set; } = string.Empty;
    }

    public class GPlaySettingsViewModel : ObservableObject, ISettings
    {
        private readonly GPlayExophaseImporterPlugin plugin;
        private GPlaySettings editingClone;
        private GPlaySettings settings;

        public GPlaySettings Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        public GPlaySettingsViewModel(GPlayExophaseImporterPlugin plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<GPlaySettings>();
            Settings = saved ?? new GPlaySettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Settings.Username))
                errors.Add("Enter your Exophase username.");

            if (Settings.MinPlaytimeMinutes < 0)
                errors.Add("Minimum playtime can't be negative.");

            return errors.Count == 0;
        }
    }
}