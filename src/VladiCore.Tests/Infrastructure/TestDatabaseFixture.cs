using System;
using System.IO;
using System.Threading.Tasks;
using MySqlConnector;
using NUnit.Framework;
using VladiCore.Data.Infrastructure;

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
            foreach (var statement in SqlScriptParser.SplitStatements(scriptText))
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
}
