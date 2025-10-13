using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MySqlConnector;
using NUnit.Framework;
using VladiCore.Data.Provisioning;

namespace VladiCore.Tests.Infrastructure;

[SetUpFixture]
public class TestDatabaseFixture
{
    [OneTimeSetUp]
    public async Task Initialize()
    {
        var configuration = BuildConfiguration();
        var options = Options.Create(new DatabaseProvisioningOptions
        {
            Enabled = true,
            ApplySeeds = true,
            CreateDbIfMissing = true,
            DatabaseName = new MySqlConnectionStringBuilder(TestConfiguration.ConnectionString).Database,
            MigrationsPath = "db/migrations/mysql",
            SeedsPath = "db/seed",
            TimeoutSeconds = 60
        });

        var contentRoot = ResolveContentRoot();
        var hostEnvironment = new TestHostEnvironment(contentRoot);
        var scanner = new SqlScriptDirectoryScanner(NullLogger<SqlScriptDirectoryScanner>.Instance);
        var bootstrapper = new SchemaBootstrapper(
            configuration,
            hostEnvironment,
            options,
            scanner,
            NullLogger<SchemaBootstrapper>.Instance);

        try
        {
            await bootstrapper.RunAsync();
        }
        catch (MySqlException ex)
        {
            Assert.Ignore($"MySQL is not available for integration tests: {ex.Message}");
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = TestConfiguration.ConnectionString,
            ["Database:AutoProvision:Enabled"] = "true",
            ["Database:AutoProvision:ApplySeeds"] = "true"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static string ResolveContentRoot()
    {
        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "VladiCore.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }

        private sealed class NullFileProvider : IFileProvider
        {
            public IDirectoryContents GetDirectoryContents(string subpath) => new NullDirectoryContents();

            public IFileInfo GetFileInfo(string subpath) => new NullFileInfo(subpath);

            public IChangeToken Watch(string filter) => new NullChangeToken();

            private sealed class NullDirectoryContents : IDirectoryContents
            {
                public bool Exists => false;

                public IEnumerator<IFileInfo> GetEnumerator() => Array.Empty<IFileInfo>().AsEnumerable().GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            private sealed class NullFileInfo : IFileInfo
            {
                public NullFileInfo(string name) => Name = name;

                public bool Exists => false;

                public long Length => 0;

                public string PhysicalPath => string.Empty;

                public string Name { get; }

                public DateTimeOffset LastModified => DateTimeOffset.MinValue;

                public bool IsDirectory => false;

                public Stream CreateReadStream() => Stream.Null;
            }

            private sealed class NullChangeToken : IChangeToken
            {
                public bool HasChanged => false;

                public bool ActiveChangeCallbacks => false;

                public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;

                private sealed class EmptyDisposable : IDisposable
                {
                    public static readonly EmptyDisposable Instance = new();

                    public void Dispose()
                    {
                    }
                }
            }
        }
    }
}
