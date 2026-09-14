using GPlayExophaseImporter.Exophase;
using GPlayExophaseImporter.Settings;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace GPlayExophaseImporter
{
    public class GPlayExophaseImporterPlugin : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("373253f5-5f71-4d54-a2d0-100dd224c53c");
        public override string Name => "GPlay Exophase Importer";

        // Source/platform label used for imported games. The PlayniteAchievements
        // Exophase provider maps a source/platform containing "google play"/"android"
        // to its GooglePlay provider, so achievements keep working automatically.
        internal const string GPlayLabel = "Google Play";

        public GPlaySettingsViewModel SettingsViewModel { get; }

        // Snapshot of the last set of entries fetched from Exophase during GetGames.
        // Reused by OnLibraryUpdated to refresh existing games without a second network fetch.
        private List<GPlayGameEntry> lastFetchedEntries;

        public GPlayExophaseImporterPlugin(IPlayniteAPI api) : base(api)
        {
            SettingsViewModel = new GPlaySettingsViewModel(this);
            Properties = new LibraryPluginProperties { HasSettings = true };
        }

        public override ISettings GetSettings(bool firstRunSettings) => SettingsViewModel;
        public override UserControl GetSettingsView(bool firstRunSettings) => new GPlaySettingsView();

        /// <summary>
        /// Called by Playnite when the user updates their library (Update Library button).
        /// Returns all Google Play games from Exophase so Playnite can create/update DB entries.
        /// </summary>
        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var settings = SettingsViewModel.Settings;

            // Reset so a failed/short-circuited run can't leave OnLibraryUpdated working off stale data.
            lastFetchedEntries = null;

            if (string.IsNullOrWhiteSpace(settings.Username))
            {
                logger.Warn("No Exophase username configured — skipping library update.");
                yield break;
            }

            var client = new ExophaseClient(PlayniteApi, logger);
            string playerId;

            if (string.Equals(settings.CachedUsername, settings.Username, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(settings.CachedPlayerId))
            {
                playerId = settings.CachedPlayerId;
            }
            else
            {
                playerId = client.ResolvePlayerId(settings.Username);
                settings.CachedUsername = settings.Username;
                settings.CachedPlayerId = playerId;
                SavePluginSettings(settings);
            }

            ulong minSeconds = settings.MinPlaytimeMinutes > 0 ? (ulong)settings.MinPlaytimeMinutes * 60UL : 0UL;
            var allEntries = client.GetGPlayGames(playerId, null, args.CancelToken);
            lastFetchedEntries = allEntries;

            foreach (var entry in allEntries)
            {
                args.CancelToken.ThrowIfCancellationRequested();

                if (!settings.ImportGPlay)
                    continue;
                if (settings.SkipDemos && IsDemo(entry.Title))
                    continue;
                if (minSeconds > 0 && entry.PlaytimeSeconds < minSeconds)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Title))
                    continue;

                var metadata = new GameMetadata
                {
                    Name = entry.Title,
                    GameId = entry.Key,
                    Playtime = settings.ImportPlaytime ? entry.PlaytimeSeconds : 0UL,
                    IsInstalled = false,
                    Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(GPlayLabel) },
                    Source = new MetadataNameProperty(GPlayLabel),
                };

                if (settings.ImportLastActivity && entry.LastPlayed.HasValue)
                    metadata.LastActivity = entry.LastPlayed;

                if (!string.IsNullOrWhiteSpace(entry.Url))
                    metadata.Links = new List<Link> { new Link("Exophase", entry.Url) };

                yield return metadata;
            }
        }

        /// <summary>
        /// Runs after a library update. When "Keep playtime of existing games in sync" is enabled,
        /// refreshes Playtime / LastActivity of games already in the database from the freshly
        /// fetched Exophase data. Playnite only imports playtime for *newly added* games during an
        /// update unless its global "Import playtime of games in library" setting is "Always" — which
        /// would also affect other libraries — so we update existing games here, scoped to this plugin only.
        /// </summary>
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            var settings = SettingsViewModel.Settings;
            if (!settings.ForcePlaytimeSync)
                return;

            var entries = lastFetchedEntries;
            if (entries == null || entries.Count == 0)
                return;

            var byKey = new Dictionary<string, GPlayGameEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                    byKey[entry.Key] = entry;
            }

            int updated = 0;
            using (PlayniteApi.Database.Games.BufferedUpdate())
            {
                foreach (var game in PlayniteApi.Database.Games.Where(g => g.PluginId == Id).ToList())
                {
                    if (string.IsNullOrEmpty(game.GameId) || !byKey.TryGetValue(game.GameId, out var entry))
                        continue;

                    bool changed = false;

                    if (settings.ImportPlaytime && game.Playtime != entry.PlaytimeSeconds)
                    {
                        game.Playtime = entry.PlaytimeSeconds;
                        changed = true;
                    }

                    if (settings.ImportLastActivity && entry.LastPlayed.HasValue
                        && game.LastActivity != entry.LastPlayed)
                    {
                        game.LastActivity = entry.LastPlayed;
                        changed = true;
                    }

                    if (changed)
                    {
                        PlayniteApi.Database.Games.Update(game);
                        updated++;
                    }
                }
            }

            if (updated > 0)
                logger.Info($"Kept {updated} existing GPlay Exophase game(s) in sync.");
        }

        private static bool IsDemo(string title)
        {
            return !string.IsNullOrEmpty(title)
                && Regex.IsMatch(title, @"\bdemo\b", RegexOptions.IgnoreCase);
        }
    }
}