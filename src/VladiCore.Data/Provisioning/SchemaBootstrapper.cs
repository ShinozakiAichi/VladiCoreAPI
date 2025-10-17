using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        var migrationScripts = _scriptScanner.GetOrderedScripts(_migrationsDirectory);
        if (migrationScripts.Count == 0)
        {
            _logger.LogInformation(
                "No migration scripts found in '{Directory}'. Skipping schema migrations.",
                _migrationsDirectory);
            return;
        }

        var scriptContents = await LoadMigrationScriptContentsAsync(migrationScripts, cancellationToken);

        await SyncSchemaAsync(connection, migrationScripts, scriptContents, cancellationToken);

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

            var scriptContent = scriptContents[script.Name];

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecuteSqlBatchAsync(connection, transaction, scriptContent, cancellationToken);
                await InsertMigrationRowAsync(connection, transaction, script.Name, _options.TimeoutSeconds, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                stopwatch.Stop();
                _logger.LogInformation(
                    "MIGRATION_OK {Script} DurationMs={Duration}",
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

    private static async Task<Dictionary<string, string>> LoadMigrationScriptContentsAsync(
        IReadOnlyList<SqlScriptFile> scripts,
        CancellationToken cancellationToken)
    {
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var script in scripts)
        {
            var content = await File.ReadAllTextAsync(script.FullPath, Encoding.UTF8, cancellationToken);
            contents[script.Name] = content;
        }

        return contents;
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

    private async Task SyncSchemaAsync(
        MySqlConnection connection,
        IReadOnlyList<SqlScriptFile> scripts,
        IReadOnlyDictionary<string, string> scriptContents,
        CancellationToken cancellationToken)
    {
        var aggregatedTables = new Dictionary<string, TableEnsurePlan>(StringComparer.OrdinalIgnoreCase);

        foreach (var script in scripts)
        {
            if (!scriptContents.TryGetValue(script.Name, out var content))
            {
                continue;
            }

            var plan = MigrationSchemaPlan.FromScript(content);
            if (plan.IsEmpty)
            {
                continue;
            }

            foreach (var table in plan.Tables)
            {
                if (aggregatedTables.TryGetValue(table.Name, out var existing))
                {
                    aggregatedTables[table.Name] = existing.Merge(table);
                }
                else
                {
                    aggregatedTables[table.Name] = table.Copy();
                }
            }
        }

        if (aggregatedTables.Count == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var snapshot = await LoadSchemaSnapshotAsync(connection, transaction, cancellationToken);

            foreach (var table in aggregatedTables.Values)
            {
                var tableReady = await EnsureTableExists(connection, transaction, snapshot, table, cancellationToken);
                if (!tableReady)
                {
                    continue;
                }

                foreach (var instruction in table.Columns.Values)
                {
                    await EnsureColumnExists(connection, transaction, snapshot, table.Name, instruction, cancellationToken);
                }

                foreach (var foreignKey in table.ForeignKeys.Values)
                {
                    await EnsureForeignKeyCompatible(connection, transaction, snapshot, table.Name, foreignKey, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to synchronize schema before migrations.");
            throw;
        }
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

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (MySqlException ex) when (ex.Number is 1050 or 1060 or 1061 or 1091)
            {
                _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
            }
        }
    }

    private async Task<DatabaseSchemaSnapshot> LoadSchemaSnapshotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);

        const string tableSql = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE();";
        await using (var tableCommand = new MySqlCommand(tableSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        await using (var reader = await tableCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                tables[name] = new TableSchema(name);
            }
        }

        const string columnSql = @"SELECT table_name, column_name, column_type, is_nullable, column_default, extra
FROM information_schema.columns WHERE table_schema = DATABASE();";
        await using (var columnCommand = new MySqlCommand(columnSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        await using (var reader = await columnCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var tableName = reader.GetString(0);
                if (!tables.TryGetValue(tableName, out var table))
                {
                    table = new TableSchema(tableName);
                    tables[tableName] = table;
                }

                var columnName = reader.GetString(1);
                var columnType = reader.GetString(2);
                var isNullable = string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase);
                var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
                var extra = reader.IsDBNull(5) ? null : reader.GetString(5);

                table.Columns[columnName] = new ColumnSchema(columnName, columnType, isNullable, defaultValue, extra);
            }
        }

        const string fkSql = @"SELECT kcu.constraint_name, kcu.table_name, kcu.column_name, kcu.referenced_table_name,
kcu.referenced_column_name, rc.delete_rule, rc.update_rule
FROM information_schema.key_column_usage kcu
INNER JOIN information_schema.referential_constraints rc
    ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema AND rc.table_name = kcu.table_name
WHERE kcu.table_schema = DATABASE() AND kcu.referenced_table_name IS NOT NULL;";

        await using (var fkCommand = new MySqlCommand(fkSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        await using (var reader = await fkCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var constraintName = reader.GetString(0);
                var tableName = reader.GetString(1);
                if (!tables.TryGetValue(tableName, out var table))
                {
                    table = new TableSchema(tableName);
                    tables[tableName] = table;
                }

                var columnName = reader.GetString(2);
                var referencedTable = reader.GetString(3);
                var referencedColumn = reader.GetString(4);
                var deleteRule = reader.GetString(5);
                var updateRule = reader.GetString(6);

                table.ForeignKeys[constraintName] = new ForeignKeySchema(
                    constraintName,
                    columnName,
                    referencedTable,
                    referencedColumn,
                    deleteRule,
                    updateRule);
            }
        }

        return new DatabaseSchemaSnapshot(tables);
    }

    private async Task<TableSchema> LoadTableSchemaAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        var table = new TableSchema(tableName);

        const string columnSql = @"SELECT column_name, column_type, is_nullable, column_default, extra
FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @tableName;";

        await using (var columnCommand = new MySqlCommand(columnSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        {
            columnCommand.Parameters.AddWithValue("@tableName", tableName);

            await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(0);
                var columnType = reader.GetString(1);
                var isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);
                var defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3);
                var extra = reader.IsDBNull(4) ? null : reader.GetString(4);

                table.Columns[columnName] = new ColumnSchema(columnName, columnType, isNullable, defaultValue, extra);
            }
        }

        const string fkSql = @"SELECT kcu.constraint_name, kcu.column_name, kcu.referenced_table_name, kcu.referenced_column_name,
rc.delete_rule, rc.update_rule
FROM information_schema.key_column_usage kcu
INNER JOIN information_schema.referential_constraints rc
    ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema AND rc.table_name = kcu.table_name
WHERE kcu.table_schema = DATABASE() AND kcu.table_name = @tableName AND kcu.referenced_table_name IS NOT NULL;";

        await using (var fkCommand = new MySqlCommand(fkSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        })
        {
            fkCommand.Parameters.AddWithValue("@tableName", tableName);

            await using var reader = await fkCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var constraintName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var referencedTable = reader.GetString(2);
                var referencedColumn = reader.GetString(3);
                var deleteRule = reader.GetString(4);
                var updateRule = reader.GetString(5);

                table.ForeignKeys[constraintName] = new ForeignKeySchema(
                    constraintName,
                    columnName,
                    referencedTable,
                    referencedColumn,
                    deleteRule,
                    updateRule);
            }
        }

        return table;
    }

    private async Task<ColumnSchema?> LoadColumnSchemaAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = @"SELECT column_type, is_nullable, column_default, extra
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = @tableName AND column_name = @columnName;";

        await using var command = new MySqlCommand(sql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var columnType = reader.GetString(0);
        var isNullable = string.Equals(reader.GetString(1), "YES", StringComparison.OrdinalIgnoreCase);
        var defaultValue = reader.IsDBNull(2) ? null : reader.GetString(2);
        var extra = reader.IsDBNull(3) ? null : reader.GetString(3);

        return new ColumnSchema(columnName, columnType, isNullable, defaultValue, extra);
    }

    private async Task<bool> EnsureTableExists(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DatabaseSchemaSnapshot snapshot,
        TableEnsurePlan table,
        CancellationToken cancellationToken)
    {
        if (snapshot.TryGetTable(table.Name, out _))
        {
            _logger.LogInformation("TABLE '{Table}' EXISTS — SKIPPED", table.Name);
            return true;
        }

        if (string.IsNullOrWhiteSpace(table.CreateStatement))
        {
            _logger.LogWarning("TABLE '{Table}' HAS NO CREATE STATEMENT — SKIPPED", table.Name);
            return false;
        }

        _logger.LogInformation("TABLE '{Table}' NOT FOUND — CREATING", table.Name);

        await using var createCommand = new MySqlCommand(table.CreateStatement, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        try
        {
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("CREATED TABLE '{Table}'", table.Name);
        }
        catch (MySqlException ex) when (ex.Number == 1050)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
        }

        var refreshed = await LoadTableSchemaAsync(connection, transaction, table.Name, cancellationToken);
        snapshot.UpsertTable(refreshed);
        return true;
    }

    private async Task EnsureColumnExists(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DatabaseSchemaSnapshot snapshot,
        string tableName,
        ColumnEnsureInstruction instruction,
        CancellationToken cancellationToken)
    {
        if (!snapshot.TryGetTable(tableName, out var tableSchema))
        {
            tableSchema = await LoadTableSchemaAsync(connection, transaction, tableName, cancellationToken);
            snapshot.UpsertTable(tableSchema);
        }

        if (!tableSchema.Columns.TryGetValue(instruction.Column.Name, out var existing))
        {
            await AddColumnAsync(connection, transaction, tableName, instruction, cancellationToken);
            var refreshed = await LoadColumnSchemaAsync(connection, transaction, tableName, instruction.Column.Name, cancellationToken);
            if (refreshed is not null)
            {
                snapshot.UpsertColumn(tableName, refreshed);
            }
            return;
        }

        if (IsColumnCompatible(existing, instruction))
        {
            var skipMessage = instruction.Mode == ColumnEnsureMode.EnsureMatches
                ? "COLUMN '{Column}' EXISTS — TYPE OK"
                : "COLUMN '{Column}' EXISTS — SKIPPED";
            _logger.LogInformation(skipMessage, instruction.Column.Name);
            return;
        }

        await EnsureColumnTypeCompatible(connection, transaction, snapshot, tableName, instruction, existing, cancellationToken);
    }

    private async Task EnsureColumnTypeCompatible(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DatabaseSchemaSnapshot snapshot,
        string tableName,
        ColumnEnsureInstruction instruction,
        ColumnSchema existing,
        CancellationToken cancellationToken)
    {
        if (_options.StrictMode)
        {
            var message = $"STRICT_MODE: Column '{tableName}.{instruction.Column.Name}' type mismatch. Expected '{instruction.Column.DefinitionWithoutName}', actual '{existing.ColumnType}'.";
            _logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        _logger.LogWarning("COLUMN '{Column}' TYPE mismatch — FIXING", instruction.Column.Name);

        var sanitizedDefinition = SanitizeColumnDefinitionForAlter(instruction.Column.DefinitionWithoutName);
        await AlterColumnAsync(connection, transaction, tableName, instruction.Column.Name, sanitizedDefinition, cancellationToken);

        var refreshed = await LoadColumnSchemaAsync(connection, transaction, tableName, instruction.Column.Name, cancellationToken);
        if (refreshed is not null)
        {
            snapshot.UpsertColumn(tableName, refreshed);
        }
    }

    private async Task EnsureForeignKeyCompatible(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DatabaseSchemaSnapshot snapshot,
        string tableName,
        ForeignKeyEnsureInstruction instruction,
        CancellationToken cancellationToken)
    {
        if (!snapshot.TryGetTable(tableName, out var tableSchema))
        {
            tableSchema = await LoadTableSchemaAsync(connection, transaction, tableName, cancellationToken);
            snapshot.UpsertTable(tableSchema);
        }

        if (TryFindForeignKey(tableSchema, instruction, out var existing))
        {
            _logger.LogInformation("FOREIGN KEY '{ForeignKey}' EXISTS — SKIPPED", existing!.ConstraintName);
            return;
        }

        if (!tableSchema.Columns.TryGetValue(instruction.ColumnName, out var column))
        {
            _logger.LogWarning(
                "COLUMN '{Column}' missing while ensuring foreign key '{ForeignKey}'.",
                instruction.ColumnName,
                instruction.ConstraintName ?? instruction.ColumnName);
            return;
        }

        if (!snapshot.TryGetTable(instruction.ReferencedTable, out var referencedTable))
        {
            referencedTable = await LoadTableSchemaAsync(connection, transaction, instruction.ReferencedTable, cancellationToken);
            snapshot.UpsertTable(referencedTable);
        }

        if (!referencedTable.Columns.TryGetValue(instruction.ReferencedColumn, out var referencedColumn))
        {
            _logger.LogWarning(
                "FOREIGN KEY '{ForeignKey}' reference '{ReferencedTable}.{ReferencedColumn}' not found.",
                instruction.ConstraintName ?? instruction.ColumnName,
                instruction.ReferencedTable,
                instruction.ReferencedColumn);
            return;
        }

        if (!AreColumnTypesCompatible(column, referencedColumn))
        {
            if (_options.StrictMode)
            {
                var message = $"STRICT_MODE: Column '{tableName}.{instruction.ColumnName}' is incompatible with '{instruction.ReferencedTable}.{instruction.ReferencedColumn}'.";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            _logger.LogWarning("COLUMN '{Column}' TYPE mismatch — FIXING", instruction.ColumnName);

            var adjusted = column with { ColumnType = referencedColumn.ColumnType };
            var definition = BuildColumnDefinition(adjusted);

            await AlterColumnAsync(connection, transaction, tableName, instruction.ColumnName, definition, cancellationToken);

            var refreshedColumn = await LoadColumnSchemaAsync(connection, transaction, tableName, instruction.ColumnName, cancellationToken);
            if (refreshedColumn is not null)
            {
                snapshot.UpsertColumn(tableName, refreshedColumn);
                column = refreshedColumn;
            }
        }

        var constraintName = instruction.ConstraintName ?? $"FK_{tableName}_{instruction.ColumnName}_{instruction.ReferencedTable}";

        if (await HasForeignKeyViolationsAsync(
                connection,
                transaction,
                tableName,
                instruction.ColumnName,
                instruction.ReferencedTable,
                instruction.ReferencedColumn,
                cancellationToken))
        {
            var violationMessage =
                $"FOREIGN KEY '{constraintName}' skipped: '{tableName}.{instruction.ColumnName}' contains orphaned values without a matching '{instruction.ReferencedTable}.{instruction.ReferencedColumn}'.";

            if (_options.StrictMode)
            {
                _logger.LogError(violationMessage);
                throw new InvalidOperationException(violationMessage);
            }

            _logger.LogError(violationMessage);
            return;
        }

        var sqlBuilder = new StringBuilder();
        sqlBuilder.Append($"ALTER TABLE `{tableName}` ADD CONSTRAINT `{constraintName}` FOREIGN KEY (`{instruction.ColumnName}`) REFERENCES `{instruction.ReferencedTable}` (`{instruction.ReferencedColumn}`)");
        if (!string.IsNullOrWhiteSpace(instruction.OnDelete))
        {
            sqlBuilder.Append(' ').Append("ON DELETE ").Append(instruction.OnDelete);
        }

        if (!string.IsNullOrWhiteSpace(instruction.OnUpdate))
        {
            sqlBuilder.Append(' ').Append("ON UPDATE ").Append(instruction.OnUpdate);
        }

        sqlBuilder.Append(';');

        await using var command = new MySqlCommand(sqlBuilder.ToString(), connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("FOREIGN KEY '{ForeignKey}' recreated with compatible type", constraintName);

            var refreshedTable = await LoadTableSchemaAsync(connection, transaction, tableName, cancellationToken);
            snapshot.UpsertTable(refreshedTable);
        }
        catch (MySqlException ex) when (ex.Number == 1061)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
        }
        catch (MySqlException ex) when (ex.Number == 3780)
        {
            _logger.LogWarning("MIGRATION_FIX {Message}", ex.Message);
        }
    }

    private async Task<bool> HasForeignKeyViolationsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        string columnName,
        string referencedTable,
        string referencedColumn,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(columnName);
        ValidateIdentifier(referencedTable);
        ValidateIdentifier(referencedColumn);

        var sql = $@"SELECT 1 FROM `{tableName}` AS source
LEFT JOIN `{referencedTable}` AS target ON source.`{columnName}` = target.`{referencedColumn}`
WHERE source.`{columnName}` IS NOT NULL AND target.`{referencedColumn}` IS NULL
LIMIT 1;";

        await using var command = new MySqlCommand(sql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hasViolations = await reader.ReadAsync(cancellationToken);
        return hasViolations;
    }

    private async Task AddColumnAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        ColumnEnsureInstruction instruction,
        CancellationToken cancellationToken)
    {
        var definition = instruction.Column.DefinitionWithoutName;
        var sql = $"ALTER TABLE `{tableName}` ADD COLUMN `{instruction.Column.Name}` {definition};";
        var reason = instruction.Mode == ColumnEnsureMode.EnsureMatches
            ? "MISSING FOR MODIFY"
            : "NOT FOUND";

        _logger.LogInformation(
            "COLUMN '{Column}' {Reason} — ADDED ({Definition})",
            instruction.Column.Name,
            reason,
            definition);

        await using var addCommand = new MySqlCommand(sql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        try
        {
            await addCommand.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("ALTERED COLUMN '{Column}' — ADDED", instruction.Column.Name);
        }
        catch (MySqlException ex) when (ex.Number == 1060)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
        }
    }

    private async Task AlterColumnAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        string columnName,
        string definitionWithoutName,
        CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE `{tableName}` MODIFY COLUMN `{columnName}` {definitionWithoutName};";

        await using var command = new MySqlCommand(sql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("ALTERED COLUMN '{Column}'", columnName);
        }
        catch (MySqlException ex) when (ex.Number == 1060)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
        }
        catch (MySqlException ex) when (ex.Number == 3780)
        {
            _logger.LogWarning("MIGRATION_FIX {Message}", ex.Message);
        }
    }

    private static bool IsColumnCompatible(ColumnSchema column, ColumnEnsureInstruction instruction)
    {
        var matchesType = column.ColumnType.StartsWith(instruction.Column.ExpectedType, StringComparison.OrdinalIgnoreCase);
        var requiresAutoIncrement = instruction.Column.IsAutoIncrement;
        var hasAutoIncrement = !string.IsNullOrWhiteSpace(column.Extra)
            && column.Extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);

        return matchesType && (!requiresAutoIncrement || hasAutoIncrement);
    }

    private static bool AreColumnTypesCompatible(ColumnSchema left, ColumnSchema right)
    {
        return string.Equals(left.ColumnType, right.ColumnType, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeColumnDefinitionForAlter(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            return definition;
        }

        var sanitized = Regex.Replace(definition, "\\s+PRIMARY\\s+KEY\\b", string.Empty, RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, "\\s{2,}", " ");

        return sanitized.Trim();
    }

    private static string BuildColumnDefinition(ColumnSchema column)
    {
        var builder = new StringBuilder(column.ColumnType);
        builder.Append(column.IsNullable ? " NULL" : " NOT NULL");

        if (column.DefaultValue is not null)
        {
            var formatted = FormatDefaultValue(column.DefaultValue);
            if (!string.IsNullOrEmpty(formatted))
            {
                builder.Append(" DEFAULT ").Append(formatted);
            }
        }

        if (!string.IsNullOrWhiteSpace(column.Extra))
        {
            builder.Append(' ').Append(column.Extra);
        }

        return builder.ToString();
    }

    private static string? FormatDefaultValue(string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return defaultValue;
        }

        if (string.Equals(defaultValue, "CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(defaultValue, "CURRENT_TIMESTAMP()", StringComparison.OrdinalIgnoreCase)
            || string.Equals(defaultValue, "NOW()", StringComparison.OrdinalIgnoreCase)
            || string.Equals(defaultValue, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return defaultValue;
        }

        if (defaultValue.StartsWith("'", StringComparison.Ordinal) && defaultValue.EndsWith("'", StringComparison.Ordinal))
        {
            return defaultValue;
        }

        return $"'{defaultValue.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static bool TryFindForeignKey(
        TableSchema table,
        ForeignKeyEnsureInstruction instruction,
        out ForeignKeySchema? existing)
    {
        if (!string.IsNullOrWhiteSpace(instruction.ConstraintName)
            && table.ForeignKeys.TryGetValue(instruction.ConstraintName, out existing))
        {
            return true;
        }

        existing = table.ForeignKeys.Values.FirstOrDefault(fk =>
            string.Equals(fk.ColumnName, instruction.ColumnName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(fk.ReferencedTable, instruction.ReferencedTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(fk.ReferencedColumn, instruction.ReferencedColumn, StringComparison.OrdinalIgnoreCase));

        return existing is not null;
    }

    private sealed class DatabaseSchemaSnapshot
    {
        private readonly Dictionary<string, TableSchema> _tables;

        public DatabaseSchemaSnapshot(Dictionary<string, TableSchema> tables)
        {
            _tables = tables;
        }

        public bool TryGetTable(string tableName, out TableSchema table)
        {
            return _tables.TryGetValue(tableName, out table);
        }

        public void UpsertTable(TableSchema table)
        {
            _tables[table.Name] = table;
        }

        public void UpsertColumn(string tableName, ColumnSchema column)
        {
            if (!_tables.TryGetValue(tableName, out var table))
            {
                table = new TableSchema(tableName);
                _tables[tableName] = table;
            }

            table.Columns[column.Name] = column;
        }
    }

    private sealed class TableSchema
    {
        public TableSchema(string name)
        {
            Name = name;
            Columns = new Dictionary<string, ColumnSchema>(StringComparer.OrdinalIgnoreCase);
            ForeignKeys = new Dictionary<string, ForeignKeySchema>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; }

        public Dictionary<string, ColumnSchema> Columns { get; }

        public Dictionary<string, ForeignKeySchema> ForeignKeys { get; }
    }

    private sealed record ColumnSchema(string Name, string ColumnType, bool IsNullable, string? DefaultValue, string? Extra);

    private sealed record ForeignKeySchema(
        string ConstraintName,
        string ColumnName,
        string ReferencedTable,
        string ReferencedColumn,
        string DeleteRule,
        string UpdateRule);

    private sealed record MigrationSchemaPlan(IReadOnlyCollection<TableEnsurePlan> Tables)
    {
        public bool IsEmpty => Tables.Count == 0;

        public static MigrationSchemaPlan FromScript(string script)
        {
            var tables = new Dictionary<string, TableEnsurePlan>(StringComparer.OrdinalIgnoreCase);

            foreach (var statement in SqlScriptParser.SplitStatements(script))
            {
                if (statement.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    var plan = TableEnsurePlan.FromCreateTable(statement);
                    if (plan is null)
                    {
                        continue;
                    }

                    if (tables.TryGetValue(plan.Name, out var existing))
                    {
                        tables[plan.Name] = existing.Merge(plan);
                    }
                    else
                    {
                        tables[plan.Name] = plan.Copy();
                    }
                }
                else if (statement.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    TableEnsurePlan.ApplyAlterStatement(statement, tables);
                }
            }

            return new MigrationSchemaPlan(tables.Values.ToList());
        }
    }

    private sealed record TableEnsurePlan(
        string Name,
        string? CreateStatement,
        IDictionary<string, ColumnEnsureInstruction> Columns,
        IDictionary<string, ForeignKeyEnsureInstruction> ForeignKeys)
    {
        public TableEnsurePlan Copy()
        {
            return new TableEnsurePlan(
                Name,
                CreateStatement,
                new Dictionary<string, ColumnEnsureInstruction>(Columns, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, ForeignKeyEnsureInstruction>(ForeignKeys, StringComparer.OrdinalIgnoreCase));
        }

        public TableEnsurePlan Merge(TableEnsurePlan other)
        {
            var columns = new Dictionary<string, ColumnEnsureInstruction>(Columns, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in other.Columns)
            {
                columns[pair.Key] = pair.Value;
            }

            var foreignKeys = new Dictionary<string, ForeignKeyEnsureInstruction>(ForeignKeys, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in other.ForeignKeys)
            {
                foreignKeys[pair.Key] = pair.Value;
            }

            var createStatement = !string.IsNullOrWhiteSpace(other.CreateStatement)
                ? other.CreateStatement
                : CreateStatement;

            return new TableEnsurePlan(Name, createStatement, columns, foreignKeys);
        }

        public static TableEnsurePlan? FromCreateTable(string statement)
        {
            var match = Regex.Match(
                statement,
                @"^CREATE\s+TABLE\s+(IF\s+NOT\s+EXISTS\s+)?(?<name>`?[\w]+`?)\s*\((?<body>.*)\)\s*(?<suffix>.*)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
            {
                return null;
            }

            var tableName = UnwrapIdentifier(match.Groups["name"].Value);
            var body = match.Groups["body"].Value;
            var columns = new Dictionary<string, ColumnEnsureInstruction>(StringComparer.OrdinalIgnoreCase);
            var foreignKeys = new Dictionary<string, ForeignKeyEnsureInstruction>(StringComparer.OrdinalIgnoreCase);
            var retainedDefinitions = new List<string>();

            foreach (var definition in SplitDefinitionElements(body))
            {
                var foreignKey = ForeignKeyDefinitionParser.Parse(definition);
                if (foreignKey is not null)
                {
                    foreignKeys[foreignKey.Key] = foreignKey;
                    continue;
                }

                var column = ColumnDefinitionParser.Parse(definition);
                if (column is null)
                {
                    var trimmed = definition.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        retainedDefinitions.Add(trimmed);
                    }
                    continue;
                }

                columns[column.Name] = new ColumnEnsureInstruction(column, ColumnEnsureMode.EnsureExists);
                retainedDefinitions.Add(definition.Trim());
            }

            var sanitizedCreateStatement = BuildCreateStatementWithoutForeignKeys(statement, retainedDefinitions);

            return new TableEnsurePlan(tableName, sanitizedCreateStatement, columns, foreignKeys);
        }

        public static void ApplyAlterStatement(string statement, IDictionary<string, TableEnsurePlan> tables)
        {
            var match = Regex.Match(
                statement,
                @"^ALTER\s+TABLE\s+(IF\s+EXISTS\s+)?(?<name>`?[\w]+`?)\s+(?<clauses>.*)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
            {
                return;
            }

            var tableName = UnwrapIdentifier(match.Groups["name"].Value);
            if (!tables.TryGetValue(tableName, out var table))
            {
                table = new TableEnsurePlan(
                    tableName,
                    null,
                    new Dictionary<string, ColumnEnsureInstruction>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, ForeignKeyEnsureInstruction>(StringComparer.OrdinalIgnoreCase));
                tables[tableName] = table;
            }

            foreach (var clause in SplitDefinitionElements(match.Groups["clauses"].Value))
            {
                var trimmed = clause.Trim();

                var foreignKey = ForeignKeyDefinitionParser.Parse(trimmed);
                if (foreignKey is not null)
                {
                    table.ForeignKeys[foreignKey.Key] = foreignKey;
                    continue;
                }

                if (trimmed.StartsWith("ADD COLUMN", StringComparison.OrdinalIgnoreCase))
                {
                    var definition = trimmed["ADD COLUMN".Length..].Trim();
                    definition = StripPrefix(definition, "IF NOT EXISTS");
                    var column = ColumnDefinitionParser.Parse(definition);
                    if (column is null)
                    {
                        continue;
                    }

                    table.Columns[column.Name] = new ColumnEnsureInstruction(column, ColumnEnsureMode.EnsureExists);
                }
                else if (trimmed.StartsWith("MODIFY COLUMN", StringComparison.OrdinalIgnoreCase))
                {
                    var definition = trimmed["MODIFY COLUMN".Length..].Trim();
                    var column = ColumnDefinitionParser.Parse(definition);
                    if (column is null)
                    {
                        continue;
                    }

                    table.Columns[column.Name] = new ColumnEnsureInstruction(column, ColumnEnsureMode.EnsureMatches);
                }
            }
        }
        private static string BuildCreateStatementWithoutForeignKeys(string originalStatement, IReadOnlyCollection<string> definitions)
        {
            var openParenIndex = originalStatement.IndexOf('(');
            var closeParenIndex = originalStatement.LastIndexOf(')');

            if (openParenIndex < 0 || closeParenIndex <= openParenIndex)
            {
                return originalStatement;
            }

            var prefix = originalStatement[..(openParenIndex + 1)].TrimEnd();
            var suffix = originalStatement[(closeParenIndex + 1)..];

            var builder = new StringBuilder();
            builder.Append(prefix);

            if (definitions.Count > 0)
            {
                builder.AppendLine();
                builder.Append("    ");
                builder.Append(string.Join(",\n    ", definitions));
                builder.AppendLine();
            }

            builder.Append(')');
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                if (!char.IsWhiteSpace(builder[^1]))
                {
                    builder.Append(' ');
                }

                builder.Append(suffix.TrimStart());
            }

            return builder.ToString();
        }
    }

    private sealed record ColumnEnsureInstruction(ColumnDefinition Column, ColumnEnsureMode Mode);

    private sealed record ForeignKeyEnsureInstruction(
        string? ConstraintName,
        string ColumnName,
        string ReferencedTable,
        string ReferencedColumn,
        string? OnDelete,
        string? OnUpdate)
    {
        public string Key => ConstraintName ?? $"{ColumnName}->{ReferencedTable}.{ReferencedColumn}";
    }

    private sealed record ColumnDefinition(
        string Name,
        string DefinitionWithoutName,
        string ExpectedType,
        bool IsAutoIncrement);

    private enum ColumnEnsureMode
    {
        EnsureExists,
        EnsureMatches
    }

    private static class ColumnDefinitionParser
    {
        private static readonly HashSet<string> StopKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "NOT",
            "NULL",
            "DEFAULT",
            "PRIMARY",
            "UNIQUE",
            "KEY",
            "REFERENCES",
            "CHECK",
            "CONSTRAINT",
            "COMMENT",
            "COLLATE",
            "ON",
            "AFTER",
            "BEFORE",
            "AUTO_INCREMENT",
            "GENERATED",
            "AS",
            "VISIBLE",
            "INVISIBLE",
            "STORED",
            "VIRTUAL"
        };

        public static ColumnDefinition? Parse(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
            {
                return null;
            }

            var trimmed = definition.Trim().TrimEnd(',');
            if (IsConstraint(trimmed))
            {
                return null;
            }

            var (name, remainder) = ExtractName(trimmed);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(remainder))
            {
                return null;
            }

            var expectedType = ExtractExpectedType(remainder);
            var isAutoIncrement = remainder.Contains("AUTO_INCREMENT", StringComparison.OrdinalIgnoreCase);

            return new ColumnDefinition(name, remainder.Trim(), expectedType, isAutoIncrement);
        }

        private static (string Name, string Remainder) ExtractName(string definition)
        {
            var span = definition.AsSpan();
            int index = 0;
            string name;

            if (span.Length > 0 && span[0] == '`')
            {
                var closing = definition.IndexOf('`', 1);
                if (closing <= 0)
                {
                    return (string.Empty, string.Empty);
                }

                name = definition.Substring(1, closing - 1);
                index = closing + 1;
            }
            else
            {
                var spaceIndex = definition.IndexOf(' ');
                if (spaceIndex <= 0)
                {
                    return (string.Empty, string.Empty);
                }

                name = definition[..spaceIndex];
                index = spaceIndex;
            }

            var remainder = definition[index..].TrimStart();
            return (UnwrapIdentifier(name), remainder);
        }

        private static string ExtractExpectedType(string definition)
        {
            var tokens = definition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();

            foreach (var token in tokens)
            {
                if (StopKeywords.Contains(token))
                {
                    break;
                }

                parts.Add(token);
            }

            return string.Join(' ', parts).ToLowerInvariant();
        }

        private static bool IsConstraint(string definition)
        {
            return definition.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("UNIQUE INDEX", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("FULLTEXT", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("SPATIAL", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("KEY", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static class ForeignKeyDefinitionParser
    {
        private static readonly Regex ForeignKeyRegex = new(
            @"^(ADD\s+)?(CONSTRAINT\s+`?(?<constraint>[\w]+)`?\s+)?FOREIGN\s+KEY\s*(?:`?(?<name>[\w]+)`?\s*)?\((?<column>[^)]+)\)\s+REFERENCES\s+`?(?<refTable>[\w]+)`?\s*\((?<refColumn>[^)]+)\)(?<tail>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex RuleRegex = new(
            @"ON\s+(?<type>DELETE|UPDATE)\s+(?<rule>CASCADE|RESTRICT|SET\s+NULL|SET\s+DEFAULT|NO\s+ACTION)",
            RegexOptions.IgnoreCase);

        public static ForeignKeyEnsureInstruction? Parse(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
            {
                return null;
            }

            var trimmed = definition.Trim().TrimEnd(',');
            if (!trimmed.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var match = ForeignKeyRegex.Match(trimmed);
            if (!match.Success)
            {
                return null;
            }

            var columns = match.Groups["column"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries);
            var referencedColumns = match.Groups["refColumn"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (columns.Length != 1 || referencedColumns.Length != 1)
            {
                return null;
            }

            var constraintName = match.Groups["constraint"].Success
                ? UnwrapIdentifier(match.Groups["constraint"].Value)
                : null;

            if (string.IsNullOrWhiteSpace(constraintName) && match.Groups["name"].Success)
            {
                constraintName = UnwrapIdentifier(match.Groups["name"].Value);
            }

            var columnName = UnwrapIdentifier(columns[0]);
            var referencedTable = UnwrapIdentifier(match.Groups["refTable"].Value);
            var referencedColumn = UnwrapIdentifier(referencedColumns[0]);

            string? onDelete = null;
            string? onUpdate = null;

            foreach (Match ruleMatch in RuleRegex.Matches(match.Groups["tail"].Value))
            {
                var rule = NormalizeRule(ruleMatch.Groups["rule"].Value);
                if (string.Equals(ruleMatch.Groups["type"].Value, "DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    onDelete = rule;
                }
                else if (string.Equals(ruleMatch.Groups["type"].Value, "UPDATE", StringComparison.OrdinalIgnoreCase))
                {
                    onUpdate = rule;
                }
            }

            return new ForeignKeyEnsureInstruction(
                string.IsNullOrWhiteSpace(constraintName) ? null : constraintName,
                columnName,
                referencedTable,
                referencedColumn,
                onDelete,
                onUpdate);
        }

        private static string NormalizeRule(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
            return normalized.ToUpperInvariant();
        }
    }

    private static IEnumerable<string> SplitDefinitionElements(string definition)
    {
        var builder = new StringBuilder();
        var depth = 0;

        for (var i = 0; i < definition.Length; i++)
        {
            var ch = definition[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth = Math.Max(depth - 1, 0);
            }

            if (ch == ',' && depth == 0)
            {
                var value = builder.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }

                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        var last = builder.ToString();
        if (!string.IsNullOrWhiteSpace(last))
        {
            yield return last;
        }
    }

    private static string StripPrefix(string value, string prefix)
    {
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[prefix.Length..].TrimStart();
        }

        return value;
    }

    private static string UnwrapIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        return identifier.Trim().Trim('`').Trim();
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
            throw new InvalidOperationException("Identifier must not be empty.");
        }

        if (identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '$')))
        {
            throw new InvalidOperationException($"Identifier '{identifier}' contains invalid characters.");
        }
    }
}
