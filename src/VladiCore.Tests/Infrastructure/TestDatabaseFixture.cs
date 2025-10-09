using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MySqlConnector;
using NUnit.Framework;

namespace VladiCore.Tests.Infrastructure;

[SetUpFixture]
public class TestDatabaseFixture
{
    [OneTimeSetUp]
    public async Task Initialize()
    {
        await using var connection = new MySqlConnection(TestConfiguration.ConnectionString);
        await connection.OpenAsync();
        await ExecuteScriptsAsync(connection, "db/migrations/mysql");
        await ExecuteScriptsAsync(connection, "db/seed");
    }

    private static async Task ExecuteScriptsAsync(MySqlConnection connection, string relativePath)
    {
        var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", relativePath);
        foreach (var file in Directory.GetFiles(root, "*.sql"))
        {
            var scriptText = await File.ReadAllTextAsync(file);
            foreach (var statement in SplitStatements(scriptText))
            {
                if (string.IsNullOrWhiteSpace(statement))
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static IEnumerable<string> SplitStatements(string script)
    {
        var delimiter = ";";
        var builder = new StringBuilder();
        using var reader = new StringReader(script);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("DELIMITER", StringComparison.OrdinalIgnoreCase))
            {
                var newDelimiter = trimmedLine.Substring("DELIMITER".Length).Trim();
                if (builder.Length > 0)
                {
                    var pending = builder.ToString().Trim();
                    if (!string.IsNullOrEmpty(pending))
                    {
                        yield return pending;
                    }
                    builder.Clear();
                }

                delimiter = string.IsNullOrWhiteSpace(newDelimiter) ? ";" : newDelimiter;
                continue;
            }

            builder.AppendLine(line);
            var current = builder.ToString().TrimEnd();
            if (current.EndsWith(delimiter, StringComparison.Ordinal))
            {
                var statement = current[..^delimiter.Length].TrimEnd();
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    yield return statement;
                }

                builder.Clear();
            }
        }

        var remaining = builder.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            yield return remaining;
        }
    }
}
