# GPlay Exophase Importer

Playnite addon that imports your **Google Play / Android** games and playtime from
[Exophase](https://www.exophase.com/) into Playnite.

Games are imported with platform and source tagged as **"Google Play"**, so the
[PlayniteAchievements](https://github.com/Playnite/PlayniteAchievements) extension can
match achievements for them automatically (its Exophase provider maps a source/platform
containing "google play"/"android" to the GooglePlay provider).

## Features

- Imports Google Play / Android games from an Exophase profile.
- Imports playtime and last played date.
- Options to skip demos, ignore games played less than X minutes.
- Optionally keeps playtime / last played of already-imported games in sync on every
  library update (scoped to this addon only).
- Handles Exophase's Cloudflare protection automatically (falls back to Playnite's
  embedded browser when the plain HTTP client is blocked).

## Requirements

- A public Exophase profile with Google Play / Android playtime being tracked.
- Playnite (SDK 6.x or newer). Recommended: **PlayniteAchievements** installed for
  achievement support (optional but strongly suggested).

## Setup

1. Install the addon from Playnite's Add-ons browser (or double-click the `.pext`).
2. Open `Add-ons` → `GPlay Exophase Importer` → `Settings`.
3. Enter your Exophase username (or profile URL).
4. Run `Update Library` for GPlay from the library context menu.

## Status — v0.1.0 (test build)

- This addon was **tested and kept working** as planned.
- It is a **secondary / experimental project** by the author. Suggestions and error
  reports are welcome (use the Issues tab), but **updates may be slow** — use at your
  own pace.

## Credits

- **Modification and maintenance:** Denka Akuma Pedro
- **Reference project:** the *Nintendo Exophase Importer* addon by
  [Tisma](https://github.com/MatisAgr) was used as the base / model for this addon.
- Author shown in Playnite: `Tisma & Denka Akuma Pedro`

## Development

```
.\build.ps1           # build (Release)
.\build.ps1 -Install  # build + install into local Playnite extensions folder
.\build.ps1 -Pack     # build + pack .pext into dist\
dotnet test           # run the unit tests
```

## License

MIT — see [LICENSE](LICENSE).