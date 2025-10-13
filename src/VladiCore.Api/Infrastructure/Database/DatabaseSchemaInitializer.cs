using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using VladiCore.Data.Infrastructure;

namespace VladiCore.Api.Infrastructure.Database;

public class DatabaseSchemaInitializer : IHostedService
{
    private const string MigrationTable = "schema_migrations";
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseSchemaInitializer> _logger;
    private readonly IMySqlConnectionFactory _connectionFactory;

    public DatabaseSchemaInitializer(
        IHostEnvironment environment,
        ILogger<DatabaseSchemaInitializer> logger,
        IMySqlConnectionFactory connectionFactory)
    {
        _environment = environment;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ApplyMigrationsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to ensure database schema.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        var migrationsPath = Path.Combine(_environment.ContentRootPath, "db", "migrations", "mysql");
        if (!Directory.Exists(migrationsPath))
        {
            _logger.LogWarning("Migration directory '{Path}' not found. Skipping automatic schema verification.", migrationsPath);
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await EnsureMigrationsTableAsync(connection, cancellationToken);

        var scripts = Directory
            .EnumerateFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName);

        foreach (var script in scripts)
        {
            var scriptName = Path.GetFileName(script);
            if (await IsAppliedAsync(connection, scriptName, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation("Applying database migration '{Script}'.", scriptName);
            var statements = SqlScriptParser.SplitStatements(await File.ReadAllTextAsync(script, cancellationToken));
            await ExecuteStatementsAsync(connection, statements, cancellationToken);
            await RecordMigrationAsync(connection, scriptName, cancellationToken);
            _logger.LogInformation("Applied database migration '{Script}'.", scriptName);
        }
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var rawConnection = _connectionFactory.Create();
        if (rawConnection is not MySqlConnection connection)
        {
            rawConnection.Dispose();
            throw new InvalidOperationException("MySqlConnectionFactory must return a MySqlConnection instance.");
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static async Task EnsureMigrationsTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string createTableSql = $@"CREATE TABLE IF NOT EXISTS {MigrationTable} (
    script_name VARCHAR(255) NOT NULL PRIMARY KEY,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

        await using var command = new MySqlCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(MySqlConnection connection, string scriptName, CancellationToken cancellationToken)
    {
        const string sql = $"SELECT 1 FROM {MigrationTable} WHERE script_name = @script LIMIT 1";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@script", scriptName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private static async Task ExecuteStatementsAsync(MySqlConnection connection, IEnumerable<string> statements, CancellationToken cancellationToken)
    {
        foreach (var statement in statements)
        {
            await using var command = new MySqlCommand(statement, connection)
            {
                CommandTimeout = 30
            };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task RecordMigrationAsync(MySqlConnection connection, string scriptName, CancellationToken cancellationToken)
    {
        const string insertSql = $"INSERT INTO {MigrationTable} (script_name, applied_at) VALUES (@script, UTC_TIMESTAMP())";
        await using var command = new MySqlCommand(insertSql, connection);
        command.Parameters.AddWithValue("@script", scriptName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
