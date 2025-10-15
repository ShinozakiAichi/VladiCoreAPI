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

            var scriptContent = await File.ReadAllTextAsync(script.FullPath, Encoding.UTF8, cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await EnsureSchemaFromScriptAsync(connection, transaction, scriptContent, cancellationToken);
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

    private async Task EnsureSchemaFromScriptAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string scriptContent,
        CancellationToken cancellationToken)
    {
        var plan = MigrationSchemaPlan.FromScript(scriptContent);
        if (plan.IsEmpty)
        {
            return;
        }

        foreach (var table in plan.Tables)
        {
            var tableReady = await EnsureTableExists(connection, transaction, table, cancellationToken);

            if (!tableReady)
            {
                continue;
            }

            foreach (var columnInstruction in table.Columns.Values)
            {
                await EnsureColumnExists(connection, transaction, table.Name, columnInstruction, cancellationToken);
            }
        }
    }

    private async Task<bool> EnsureTableExists(
        MySqlConnection connection,
        MySqlTransaction transaction,
        TableEnsurePlan table,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @tableName;";

        await using var existsCommand = new MySqlCommand(sql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        existsCommand.Parameters.AddWithValue("@tableName", table.Name);
        var exists = Convert.ToInt64(await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0;

        if (exists)
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
            return true;
        }
        catch (MySqlException ex) when (ex.Number == 1050)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAILED TO CREATE TABLE '{Table}'", table.Name);
            throw;
        }
    }

    private async Task EnsureColumnExists(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        ColumnEnsureInstruction instruction,
        CancellationToken cancellationToken)
    {
        var metadata = await GetColumnMetadataAsync(connection, transaction, tableName, instruction.Column.Name, cancellationToken);

        if (!metadata.Exists)
        {
            await AddColumnAsync(connection, transaction, tableName, instruction, cancellationToken);
            return;
        }

        if (IsColumnCompatible(metadata, instruction))
        {
            var skipMessage = instruction.Mode == ColumnEnsureMode.EnsureMatches
                ? "COLUMN '{Column}' EXISTS — TYPE OK — SKIPPED"
                : "COLUMN '{Column}' EXISTS — SKIPPED";
            _logger.LogInformation(skipMessage, instruction.Column.Name);
            return;
        }

        await ModifyColumnAsync(connection, transaction, tableName, instruction, cancellationToken);
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

    private async Task ModifyColumnAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        ColumnEnsureInstruction instruction,
        CancellationToken cancellationToken)
    {
        var modifySql = $"ALTER TABLE `{tableName}` MODIFY COLUMN `{instruction.Column.Name}` {instruction.Column.DefinitionWithoutName};";

        _logger.LogInformation(
            "COLUMN '{Column}' EXISTS — TYPE MISMATCH — ALTERING TO ({Definition})",
            instruction.Column.Name,
            instruction.Column.DefinitionWithoutName);

        await using var modifyCommand = new MySqlCommand(modifySql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        try
        {
            await modifyCommand.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("ALTERED COLUMN '{Column}'", instruction.Column.Name);
        }
        catch (MySqlException ex) when (ex.Number == 1060)
        {
            _logger.LogWarning("MIGRATION_SKIP {Message}", ex.Message);
        }
    }

    private async Task<ColumnMetadata> GetColumnMetadataAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string lookupSql = @"SELECT COLUMN_TYPE, EXTRA FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = @tableName AND column_name = @columnName;";

        await using var lookupCommand = new MySqlCommand(lookupSql, connection, transaction)
        {
            CommandTimeout = Math.Max(_options.TimeoutSeconds, 1)
        };

        lookupCommand.Parameters.AddWithValue("@tableName", tableName);
        lookupCommand.Parameters.AddWithValue("@columnName", columnName);

        await using var reader = await lookupCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ColumnMetadata.Missing;
        }

        var columnType = reader.GetString(0);
        var extra = reader.IsDBNull(1) ? null : reader.GetString(1);

        return new ColumnMetadata(columnType, extra);
    }

    private static bool IsColumnCompatible(ColumnMetadata metadata, ColumnEnsureInstruction instruction)
    {
        var matchesType = metadata.ColumnType.StartsWith(instruction.Column.ExpectedType, StringComparison.OrdinalIgnoreCase);
        var requiresAutoIncrement = instruction.Column.IsAutoIncrement;
        var hasAutoIncrement = !string.IsNullOrWhiteSpace(metadata.Extra)
            && metadata.Extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);

        return matchesType && (!requiresAutoIncrement || hasAutoIncrement);
    }

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
                        foreach (var pair in plan.Columns)
                        {
                            existing.Columns[pair.Key] = pair.Value;
                        }

                        if (!string.IsNullOrWhiteSpace(plan.CreateStatement))
                        {
                            tables[plan.Name] = existing with { CreateStatement = plan.CreateStatement };
                        }
                    }
                    else
                    {
                        tables[plan.Name] = plan;
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
        IDictionary<string, ColumnEnsureInstruction> Columns)
    {
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

            foreach (var definition in SplitDefinitionElements(body))
            {
                var column = ColumnDefinitionParser.Parse(definition);
                if (column is null)
                {
                    continue;
                }

                columns[column.Name] = new ColumnEnsureInstruction(column, ColumnEnsureMode.EnsureExists);
            }

            return new TableEnsurePlan(tableName, statement, columns);
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
                    new Dictionary<string, ColumnEnsureInstruction>(StringComparer.OrdinalIgnoreCase));
                tables[tableName] = table;
            }

            foreach (var clause in SplitDefinitionElements(match.Groups["clauses"].Value))
            {
                var trimmed = clause.Trim();
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
    }

    private sealed record ColumnEnsureInstruction(ColumnDefinition Column, ColumnEnsureMode Mode);

    private sealed record ColumnDefinition(
        string Name,
        string DefinitionWithoutName,
        string ExpectedType,
        bool IsAutoIncrement);

    private sealed record ColumnMetadata(string ColumnType, string? Extra)
    {
        public bool Exists => !string.IsNullOrEmpty(ColumnType);

        public static ColumnMetadata Missing { get; } = new(string.Empty, null);
    }

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
                || definition.StartsWith("KEY", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase)
                || definition.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
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
            throw new InvalidOperationException("Database name must not be empty.");
        }

        if (identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '$')))
        {
            throw new InvalidOperationException("Database name contains invalid characters.");
        }
    }
}
