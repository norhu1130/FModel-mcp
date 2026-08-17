using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FModel.Services;
using FModel.Settings;
using FModel.Views.Resources.Controls;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Serilog;

namespace FModel.MCP;

/// <summary>
/// Hosts the MCP server on a loopback-only Kestrel endpoint inside the WPF process,
/// so external AI clients can inspect the currently loaded project over Streamable HTTP.
/// </summary>
public sealed class McpServerService
{
    private WebApplication _app;

    public bool IsRunning { get; private set; }
    public string Endpoint { get; private set; }

    public async Task StartAsync()
    {
        if (IsRunning || !UserSettings.Default.EnableMcpServer) return;

        var port = UserSettings.Default.McpServerPort;
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger);
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));

            builder.Services.AddSingleton(new McpAssetService(ApplicationService.ApplicationView));
            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation { Name = "FModel", Version = Constants.APP_VERSION };
                    options.ServerInstructions =
                        "FModel exposes the Unreal Engine game project currently opened in the app. " +
                        "Start with get_project_info, browse with list_folder / search_files, read assets with " +
                        "get_asset_json, and export files to disk with the export_* / save_* tools. " +
                        "Paths are virtual game paths such as 'GameName/Content/.../Asset.uasset'.";
                })
                .WithHttpTransport(_ => { })
                .WithTools<Tools.ProjectTools>()
                .WithTools<Tools.BrowseTools>()
                .WithTools<Tools.AssetTools>()
                .WithTools<Tools.ExportTools>()
                .WithResources<Resources.FModelResources>()
                .WithPrompts<Prompts.FModelPrompts>();

            var app = builder.Build();
            app.MapMcp("/mcp");
            await app.StartAsync().ConfigureAwait(false);

            _app = app;
            IsRunning = true;
            Endpoint = $"http://127.0.0.1:{port}/mcp";

            Log.Information("MCP server listening on {Endpoint}", Endpoint);
            FLogger.Append(ELog.Information, () =>
            {
                FLogger.Text("MCP server listening on ", Constants.WHITE);
                FLogger.Link(Endpoint, Endpoint, true);
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start the MCP server on port {Port}", port);
            FLogger.Append(ELog.Error, () =>
                FLogger.Text($"Failed to start the MCP server on port {port}: {e.GetBaseException().Message}", Constants.WHITE, true));
        }
    }

    public async Task StopAsync()
    {
        if (_app is null) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _app.StopAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // best effort - the process is exiting anyway
        }
        finally
        {
            IsRunning = false;
            _app = null;
        }
    }
}
