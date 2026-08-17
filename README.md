FModel - An Unreal Engine Archives Explorer in C#
------------------------------------------

[![CI Status](https://img.shields.io/github/actions/workflow/status/4sval/FModel/qa.yml?label=CI)](https://github.com/4sval/FModel/actions)
[![Latest](https://img.shields.io/github/v/release/4sval/FModel?color=yellow)](https://fmodel.app/download)
[![Donate](https://img.shields.io/badge/sponsor-DB61A2?logo=GitHub-Sponsors&logoColor=white)](https://fmodel.app/donate)
[![Discord](https://discord.com/api/guilds/637265123144237061/widget.png?style=shield)](https://fmodel.app/discord)
***

### Description:
FModel is an archive explorer for [Unreal Engine](https://www.unrealengine.com/en-US/) games that uses [CUE4Parse](https://github.com/FabianFG/CUE4Parse) as its core parsing library, providing robust support for the latest UE4 and UE5 archive formats. It aims to deliver a modern and intuitive user interface, powerful features, and a comprehensive set of tools for previewing and converting game packages, empowering YOU to understand games' inner workings with ease.

FModel is actively maintained and developed by a dedicated community of contributors, and welcomes all new contributions and feedback.

### Installation:
For installation, follow the instructions from [here](https://github.com/4sval/FModel/wiki/Installing-FModel)

> [!NOTE]
> This fork embeds an MCP server, which additionally requires the [ASP.NET Core Runtime x64](https://dotnet.microsoft.com/download/dotnet) (same major version as the .NET Desktop Runtime FModel already needs).

### MCP Server (this fork):
FModel hosts a [Model Context Protocol](https://modelcontextprotocol.io/) server so AI clients (Claude Code, Claude Desktop, etc.) can inspect the game project currently opened in the app. While FModel is running with a game loaded, the server listens on `http://127.0.0.1:44551/mcp` (loopback only; toggle and port under **Settings → General → MCP Server**).

Connect from Claude Code:
```
claude mcp add --transport http fmodel http://127.0.0.1:44551/mcp
```

What it exposes:
- **Resources** — `fmodel://project`, `fmodel://archives`, `fmodel://folder/{path}`, `fmodel://asset/{path}`
- **Tools** — `get_project_info`, `list_archives`, `list_folder`, `search_files`, `get_asset_json`, `get_asset_metadata`, `get_references`, `decompile_blueprint`, `export_raw`, `save_properties_json`, `save_texture`, `export_model`, `export_animation`, `export_world`, `export_audio`, `export_folder` (bulk: raw/properties/textures/models/animations/worlds/audio for a whole folder, recursive, capped by `maxFiles`)
- **Prompts** — `analyze_asset`, `find_assets`

Exports are written to the same output directories and with the same format settings as the FModel UI. Export tools return `BUSY` while an export is already queued or running in the UI, and world exports may pop the streaming-level filter dialog in the app. Game-specific audio formats not covered by the generic Wwise/FMOD/SoundWave paths are only exportable through the UI (`export_raw` always works).

### Sponsorship:
<p>
  <a href="https://1password.com/">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://cdn.fmodel.app/i/svg/1password-light.svg">
      <source media="(prefers-color-scheme: light)" srcset="https://cdn.fmodel.app/i/svg/1password-dark.svg">
      <img src="https://cdn.fmodel.app/i/svg/1password-light.svg" width="256px">
    </picture>
  </a>
</p>

### License:
FModel is licensed under [GPL-3](https://github.com/4sval/FModel/blob/dev/LICENSE), and licenses of third-party libraries used are listed [here](https://github.com/4sval/FModel/blob/dev/NOTICE).