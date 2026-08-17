FModel-MCP - An Unreal Engine Archives Explorer in C# with MCP Server
------------------------------------------

***

### Description:
FModel-MCP is a fork of [FModel](https://github.com/4sval/FModel) — an archive explorer for [Unreal Engine](https://www.unrealengine.com/en-US/) games built on [CUE4Parse](https://github.com/FabianFG/CUE4Parse) — that embeds a [Model Context Protocol](https://modelcontextprotocol.io/) server into the app. While FModel is running, AI clients such as Claude Code can connect to it and browse the loaded game's folders, search files, read assets as JSON, and export textures, models, animations and audio — using the same parsing engine and export settings as the UI, against the project you currently have open.

Everything from upstream FModel is preserved: robust support for the latest UE4 and UE5 archive formats, a modern and intuitive user interface, and a comprehensive set of tools for previewing and converting game packages. This fork simply gives those capabilities a second surface — one designed for AI agents instead of clicks.

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

### License:
FModel and FModel-MCP are licensed under [GPL-3](https://github.com/norhu1130/FModel-mcp/blob/dev/LICENSE), and licenses of third-party libraries used are listed [here](https://github.com/norhu1130/FModel-mcp/blob/dev/NOTICE).