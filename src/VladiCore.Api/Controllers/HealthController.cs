using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Microsoft.Extensions.Logging;
using VladiCore.Data.Infrastructure;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private static readonly string[] SentinelTables =
    {
        "Products",
        "Orders"
    };

    private readonly IMySqlConnectionFactory _connectionFactory;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IMySqlConnectionFactory connectionFactory, ILogger<HealthController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpGet("db")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDatabaseHealth(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create() as MySqlConnection
                ?? throw new InvalidOperationException("MySqlConnectionFactory must produce MySqlConnection instances.");

            var missingTables = new List<string>();
            foreach (var table in SentinelTables)
            {
                if (!await TableExistsAsync(connection, table, cancellationToken))
                {
                    missingTables.Add(table);
                }
            }

            var appliedMigrations = await CountMigrationsAsync(connection, cancellationToken);

            if (missingTables.Count > 0)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        db = "missing_tables",
                        migrations = appliedMigrations,
                        missing = missingTables
                    });
            }

            return Ok(new { db = "ok", migrations = appliedMigrations });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { db = "error", error = ex.Message });
        }
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @table";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@table", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<int> CountMigrationsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM schema_migrations";
        await using var command = new MySqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }
}
