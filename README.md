<h1>Ani-Sync Emby Plugin</h1>

> Forked from [vosmiic/jellyfin-ani-sync](https://github.com/vosmiic/jellyfin-ani-sync), the original Jellyfin plugin.

## About

Ani-Sync lets you synchronize your Emby anime watch progress to popular services. Please [create a discussion](https://github.com/alysson-souza/emby-ani-sync/discussions/new/choose) for new feature ideas.

## Installation

1. Download a version from the [releases tab](https://github.com/alysson-souza/emby-ani-sync/releases).
2. Shut down Emby Server.
3. Extract the zip contents into the `plugins` directory inside your [Emby Server Data Folder](https://emby.media/support/articles/Plugins-Duplicate.html).
4. On Linux/NAS, ensure the DLL files have matching permissions and ownership as other plugin files in the directory.
5. Start Emby Server.
6. Navigate to Plugins in Emby (Settings > Admin Dashboard > Plugins) and adjust the settings accordingly.

## Build

1. To build this plugin you will need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

2. Build the plugin with the following command:

```
dotnet build emby-ani-sync/emby-ani-sync.csproj --configuration Release
```

3. Copy `emby-ani-sync/bin/Release/netstandard2.0/emby-ani-sync.dll` into your Emby `plugins` directory.

Only the plugin DLL is required for installation. Emby provides the shared dependencies it needs at runtime.

## Services/providers

### Currently supported

1. MyAnimeList
2. AniList
3. (Beta) Kitsu
4. (Limited support) Annict
5. Shikimori
6. Simkl

## External tools

### Anime Lists

We use the XML documents in the [anime lists repo](https://github.com/Anime-Lists/anime-lists) to find the anime you are watching on each provider we support.

Please help the project by contributing to the lists of anime, it helps everyone!

### Anime Offline Database/arm server

We use the API offered by the [arm server repo](https://github.com/BeeeQueue/arm-server) which accesses the [anime offline database repo](https://github.com/manami-project/anime-offline-database) that we use to fetch our providers IDs so we can update your progress.

Please also help these projects by contributing to the anime database/helping with the API server.
