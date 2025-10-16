using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using VladiCore.Api.Infrastructure.Hud;

namespace VladiCore.Tests.Infrastructure.Hud;

public sealed class HudDemoConfigSeederTests
{
    [Test]
    public async Task StartAsync_should_create_all_configs_when_missing()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var environment = new TestHostEnvironment(tempDirectory.Path);
        var options = Options.Create(new HudDemoConfigOptions { OutputDirectory = "configs" });
        var seeder = new HudDemoConfigSeeder(options, environment, NullLogger<HudDemoConfigSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);

        var outputDirectory = Path.Combine(tempDirectory.Path, "configs");
        foreach (var template in HudDemoConfigTemplates.All)
        {
            var filePath = Path.Combine(outputDirectory, template.FileName);
            File.Exists(filePath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            content.Should().Be(template.Content);
        }
    }

    [Test]
    public async Task StartAsync_should_preserve_existing_files()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var environment = new TestHostEnvironment(tempDirectory.Path);
        var options = Options.Create(new HudDemoConfigOptions { OutputDirectory = "configs" });
        var seeder = new HudDemoConfigSeeder(options, environment, NullLogger<HudDemoConfigSeeder>.Instance);

        var outputDirectory = Path.Combine(tempDirectory.Path, "configs");
        Directory.CreateDirectory(outputDirectory);
        var preservedTemplate = HudDemoConfigTemplates.All.First();
        var preservedFilePath = Path.Combine(outputDirectory, preservedTemplate.FileName);
        await File.WriteAllTextAsync(preservedFilePath, "custom", Encoding.UTF8);

        await seeder.StartAsync(CancellationToken.None);

        var preservedContent = await File.ReadAllTextAsync(preservedFilePath, Encoding.UTF8);
        preservedContent.Should().Be("custom");

        foreach (var template in HudDemoConfigTemplates.All.Skip(1))
        {
            var filePath = Path.Combine(outputDirectory, template.FileName);
            File.Exists(filePath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            content.Should().Be(template.Content);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "HudPlugin";

        public string ContentRootPath { get; set; }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new TemporaryDirectory(directoryPath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // ignored on cleanup
            }
        }
    }
}
