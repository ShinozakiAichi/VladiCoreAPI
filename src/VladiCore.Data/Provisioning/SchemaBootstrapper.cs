using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using VladiCore.Data.Infrastructure;

namespace VladiCore.Data.Provisioning;

public sealed class SchemaBootstrapper : ISchemaBootstrapper
{
    private const string MigrationTableName = "schema_migrations";

    private readonly ILogger<SchemaBootstrapper> _logger;
    private readonly DatabaseProvisioningOptions _options;
    private readonly string _connectionString;
    private readonly string _migrationsDirectory;
    private readonly string _seedsDirectory;
    private readonly ISqlScriptDirectoryScanner _scriptScanner;

    public SchemaBootstrapper(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IOptions<DatabaseProvisioningOptions> options,
        ISqlScriptDirectoryScanner scriptScanner,
        ILogger<SchemaBootstrapper> logger)
    {
        _logger = logger;
        _scriptScanner = scriptScanner;
        _options = options.Value;

        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        if (string.IsNullOrWhiteSpace(_options.MigrationsPath))
        {
            throw new InvalidOperationException("Database provisioning requires a migrations directory path.");
        }

        var rootPath = hostEnvironment.ContentRootPath;
        _migrationsDirectory = ResolvePath(rootPath, _options.MigrationsPath);
        _seedsDirectory = ResolvePath(rootPath, _options.SeedsPath);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database auto-provisioning disabled. Skipping schema checks.");
            return;
        }

        var overall = Stopwatch.StartNew();
        _logger.LogInformation("DB_INIT_START");

        if (_options.CreateDbIfMissing)
        {
            await EnsureDatabaseAsync(cancellationToken);
        }

        await EnsureSchemaAsync(cancellationToken);

        if (_options.ApplySeeds)
        {
            await ApplySeedsAsync(cancellationToken);
        }

        overall.Stop();
        _logger.LogInformation("DB_INIT_DONE DurationMs={Duration}", overall.ElapsedMilliseconds);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var databaseName = ResolveDatabaseName(builder);

        try
        {
            await using var probeConnection = new MySqlConnection(builder.ConnectionString);
            await probeConnection.OpenAsync(cancellationToken);
            await probeConnection.CloseAsync();
            return;
        }
        catch (MySqlException ex) when (ex.Number == 1049)
        {
            _logger.LogWarning(
                "Database '{Database}' is missing. Auto-provisioning will attempt to create it.",
                databaseName);
        }

        var serverBuilder = new MySqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = string.Empty
        };

        await using var serverConnection = new MySqlConnection(serverBuilder.ConnectionString);
        await serverConnection.OpenAsync(cancellationToken);

        ValidateIdentifier(databaseName);
        var createSql = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await using (var command = new MySqlCommand(createSql, serverConnection)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Created database '{Database}'.", databaseName);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var migrationScripts = _scriptScanner.GetOrderedScripts(_migrationsDirectory);
        if (migrationScripts.Count == 0)
        {
            _logger.LogInformation(
                "No migration scripts found in '{Directory}'. Skipping schema migrations.",
                _migrationsDirectory);
            return;
        }

        await using var connection = new MySqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Failed to open MySQL connection while ensuring schema.");
            throw;
        }

        await EnsureMigrationTableAsync(connection, cancellationToken);
        var appliedScripts = await LoadAppliedScriptsAsync(connection, cancellationToken);

        foreach (var script in migrationScripts)
        {
            if (appliedScripts.Contains(script.Name))
            {
                _logger.LogWarning("MIGRATION_SKIP {Script}", script.Name);
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("MIGRATION_APPLY_START {Script}", script.Name);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var scriptContent = await File.ReadAllTextAsync(script.FullPath, Encoding.UTF8, cancellationToken);
                await ExecuteSqlBatchAsync(connection, transaction, scriptContent, cancellationToken);
                await InsertMigrationRowAsync(connection, transaction, script.Name, _options.TimeoutSeconds, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                stopwatch.Stop();
                _logger.LogInformation(
                    "MIGRATION_APPLY_OK {Script} DurationMs={Duration}",
                    script.Name,
                    stopwatch.ElapsedMilliseconds);
                appliedScripts.Add(script.Name);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "MIGRATION_APPLY_FAIL {Script}", script.Name);
                throw;
            }
        }
    }

    private async Task ApplySeedsAsync(CancellationToken cancellationToken)
    {
        var seedScripts = _scriptScanner.GetOrderedScripts(_seedsDirectory);
        if (seedScripts.Count == 0)
        {
            _logger.LogInformation(
                "No seed scripts found in '{Directory}'. Skipping data seeding.",
                _seedsDirectory);
            return;
        }

        await using var connection = new MySqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Failed to open MySQL connection while applying seed scripts.");
            throw;
        }

        foreach (var script in seedScripts)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("SEED_APPLY_START {Script}", script.Name);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var scriptContent = await File.ReadAllTextAsync(script.FullPath, Encoding.UTF8, cancellationToken);
                await ExecuteSqlBatchAsync(connection, transaction, scriptContent, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                stopwatch.Stop();
                _logger.LogInformation(
                    "SEED_APPLY_OK {Script} DurationMs={Duration}",
                    script.Name,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "SEED_APPLY_FAIL {Script}", script.Name);
                throw;
            }
        }
    }

    private async Task EnsureMigrationTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS {MigrationTableName} (
    id INT AUTO_INCREMENT PRIMARY KEY,
    script_name VARCHAR(255) NOT NULL UNIQUE,
    applied_at TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

        await using var command = new MySqlCommand(createSql, connection)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedScriptsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectSql = $"SELECT script_name FROM {MigrationTableName};";

        await using var command = new MySqlCommand(selectSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private async Task ExecuteSqlBatchAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string scriptContent,
        CancellationToken cancellationToken)
    {
        foreach (var statement in SqlScriptParser.SplitStatements(scriptContent))
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            await using var command = new MySqlCommand(statement, connection, transaction)
            {
                CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
            };

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertMigrationRowAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string scriptName,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var insertSql = $"INSERT INTO {MigrationTableName} (script_name) VALUES (@scriptName);";
        await using var command = new MySqlCommand(insertSql, connection, transaction)
        {
            CommandTimeout = Math.Max(timeoutSeconds, 5)
        };

        command.Parameters.AddWithValue("@scriptName", scriptName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ResolvePath(string rootPath, string configuredPath)
    {
        var path = configuredPath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(rootPath, path);
        }

        return Path.GetFullPath(path);
    }

    private string ResolveDatabaseName(MySqlConnectionStringBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(_options.DatabaseName))
        {
            return _options.DatabaseName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(builder.Database))
        {
            return builder.Database;
        }

        throw new InvalidOperationException("Database name cannot be resolved from configuration.");
    }

    private static void ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Database name must not be empty.");
        }

        if (identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '$')))
        {
            throw new InvalidOperationException("Database name contains invalid characters.");
        }
    }
}
