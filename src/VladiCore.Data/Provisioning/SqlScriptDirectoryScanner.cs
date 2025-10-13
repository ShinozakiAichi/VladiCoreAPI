using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace VladiCore.Data.Provisioning;

public interface ISqlScriptDirectoryScanner
{
    IReadOnlyList<SqlScriptFile> GetOrderedScripts(string directoryPath);
}

public sealed record SqlScriptFile(string Name, string FullPath);

public sealed class SqlScriptDirectoryScanner : ISqlScriptDirectoryScanner
{
    private static readonly Regex ScriptNamePattern = new("^\\d{3}_.+\\.sql$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ILogger<SqlScriptDirectoryScanner> _logger;

    public SqlScriptDirectoryScanner(ILogger<SqlScriptDirectoryScanner> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<SqlScriptFile> GetOrderedScripts(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path must be provided.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("SQL script directory '{Directory}' does not exist.", directoryPath);
            return Array.Empty<SqlScriptFile>();
        }

        var files = Directory
            .EnumerateFiles(directoryPath, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(path => new SqlScriptFile(Path.GetFileName(path), path))
            .Where(file =>
            {
                if (ScriptNamePattern.IsMatch(file.Name))
                {
                    return true;
                }

                _logger.LogWarning(
                    "Skipping SQL script '{Script}' because it does not follow the 'NNN_description.sql' pattern.",
                    file.Name);
                return false;
            })
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return files;
    }
}
