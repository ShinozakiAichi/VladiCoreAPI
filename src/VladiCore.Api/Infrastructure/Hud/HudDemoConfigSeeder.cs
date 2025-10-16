using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VladiCore.Api.Infrastructure.Hud;

internal sealed class HudDemoConfigSeeder : IHostedService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger<HudDemoConfigSeeder> _logger;
    private readonly IHostEnvironment _environment;
    private readonly HudDemoConfigOptions _options;

    public HudDemoConfigSeeder(
        IOptions<HudDemoConfigOptions> options,
        IHostEnvironment environment,
        ILogger<HudDemoConfigSeeder> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var directory = ResolveOutputDirectory(_options.OutputDirectory);
        Directory.CreateDirectory(directory);

        foreach (var template in HudDemoConfigTemplates.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(directory, template.FileName);
            if (File.Exists(filePath))
            {
                _logger.LogDebug("HUD demo config {ConfigFile} already exists, skipping seeding.", filePath);
                continue;
            }

            await File.WriteAllTextAsync(filePath, template.Content, Utf8NoBom, cancellationToken);
            _logger.LogInformation("Seeded HUD demo config {ConfigFile}.", filePath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ResolveOutputDirectory(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(_environment.ContentRootPath, "hud-configs");
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }
}

internal static class HudDemoConfigTemplates
{
    internal static IReadOnlyList<HudDemoConfigTemplate> All { get; } = new[]
    {
        new HudDemoConfigTemplate(
            "holo_demo.yaml",
            """# Demo HOLOGRAM HUD config created on first plugin start.
id: holo_demo
type: HUD
activation:
  mode: radius
  radius: 6.0
  hideBeyond: 8.0
position:
  anchor: HEAD
  offset:
    x: 0
    y: 1.8
    z: 0
  facePlayer: true
render:
  mode: HOLOGRAM
  hologram:
    linesSeparation: 0.26
    useTextDisplay: true
    itemIcon:
      enabled: true
      model: "cosmo:icons/mascot_head"
      scale: 1.1
content:
  title:
    text: "<font:cosmo:ui><b>CosmoMINE</b></font>"
  text:
    lines:
      - "<font:cosmo:ui><glyph:cosmo/icons/mascot> Привет, ${player}!</font>"
      - "<gray>Добро пожаловать на сервер.</gray>"
  icon:
    type: GLYPH
    glyph: "cosmo:icons/mascot"
"""),
        new HudDemoConfigTemplate(
            "toast_demo.yaml",
            """# Demo TOAST HUD config created on first plugin start.
id: toast_demo
type: HUD
activation:
  mode: command
render:
  mode: TOAST
  toast:
    durationTicks: 80
    frame: GOAL
    rateLimitSec: 5
content:
  title:
    text: "<font:cosmo:ui>Новая реплика маскота</font>"
  text:
    lines:
      - "«Вот это да!»"
  icon:
    type: ITEM
    material: PAPER
    customModelData: 12001
"""),
        new HudDemoConfigTemplate(
            "bar_demo.yaml",
            """# Demo BOSSBAR HUD config created on first plugin start.
id: bar_demo
type: HUD
activation:
  mode: radius
  radius: 10.0
  hideBeyond: 14.0
render:
  mode: BOSSBAR
  bossbar:
    color: BLUE
    style: SOLID
    progress: 1.0
content:
  title:
    text: "<font:cosmo:ui>CosmoMINE HUD</font>"
  text:
    lines:
      - "<gray>Маскот:</gray> <glyph:cosmo/icons/mascot> Вперёд!"
"""),
    };
}

internal sealed record HudDemoConfigTemplate(string FileName, string Content);
