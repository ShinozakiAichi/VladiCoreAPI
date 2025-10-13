using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using VladiCore.Data.Provisioning;

namespace VladiCore.Tests.Infrastructure;

[TestFixture]
public sealed class SqlScriptDirectoryScannerTests
{
    private string _tempDirectory = string.Empty;
    private SqlScriptDirectoryScanner _scanner = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "vladicore-sql-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _scanner = new SqlScriptDirectoryScanner(NullLogger<SqlScriptDirectoryScanner>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public void Should_order_scripts_by_name()
    {
        var first = Path.Combine(_tempDirectory, "010_initial.sql");
        var second = Path.Combine(_tempDirectory, "002_seed.sql");
        File.WriteAllText(first, "SELECT 1;");
        File.WriteAllText(second, "SELECT 2;");

        var scripts = _scanner.GetOrderedScripts(_tempDirectory);

        scripts.Should().HaveCount(2);
        scripts[0].Name.Should().Be("002_seed.sql");
        scripts[1].Name.Should().Be("010_initial.sql");
    }

    [Test]
    public void Should_ignore_files_that_do_not_follow_pattern()
    {
        var valid = Path.Combine(_tempDirectory, "001_valid.sql");
        var invalid = Path.Combine(_tempDirectory, "invalid.sql");
        File.WriteAllText(valid, "SELECT 1;");
        File.WriteAllText(invalid, "SELECT 2;");

        var scripts = _scanner.GetOrderedScripts(_tempDirectory);

        scripts.Should().HaveCount(1);
        scripts[0].Name.Should().Be("001_valid.sql");
    }

    [Test]
    public void Should_return_empty_when_directory_missing()
    {
        var path = Path.Combine(_tempDirectory, "missing");
        var scripts = _scanner.GetOrderedScripts(path);

        scripts.Should().BeEmpty();
    }
}
