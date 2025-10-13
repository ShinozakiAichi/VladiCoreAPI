using System;
using System.Collections.Generic;

namespace VladiCore.Api.Infrastructure.Database;

internal static class MigrationTableDefinitionValidator
{
    private const string ScriptNameColumn = "script_name";
    private const string AppliedAtColumn = "applied_at";

    public static bool IsValid(IReadOnlyList<MigrationTableColumnDefinition> columns)
    {
        if (columns.Count == 0)
        {
            return false;
        }

        if (columns.Count != 2)
        {
            return false;
        }

        var scriptColumn = columns[0];
        if (!scriptColumn.Name.Equals(ScriptNameColumn, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!scriptColumn.DataType.Equals("varchar", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!scriptColumn.ColumnType.Equals("varchar(255)", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (scriptColumn.IsNullable)
        {
            return false;
        }

        if (!scriptColumn.IsPrimaryKey)
        {
            return false;
        }

        var appliedColumn = columns[1];
        if (!appliedColumn.Name.Equals(AppliedAtColumn, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!appliedColumn.DataType.Equals("datetime", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!appliedColumn.ColumnType.StartsWith("datetime", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (appliedColumn.IsNullable)
        {
            return false;
        }

        if (!HasCurrentTimestampDefault(appliedColumn.ColumnDefault))
        {
            return false;
        }

        return true;
    }

    private static bool HasCurrentTimestampDefault(string? columnDefault)
    {
        if (string.IsNullOrWhiteSpace(columnDefault))
        {
            return false;
        }

        return columnDefault.Trim().StartsWith("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record MigrationTableColumnDefinition(
    string Name,
    string DataType,
    string ColumnType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? ColumnDefault);
