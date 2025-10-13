using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VladiCore.Data.Infrastructure;

public static class SqlScriptParser
{
    private const string DefaultDelimiter = ";";

    public static IEnumerable<string> SplitStatements(string script)
    {
        if (script is null)
        {
            throw new ArgumentNullException(nameof(script));
        }

        var delimiter = DefaultDelimiter;
        var builder = new StringBuilder();
        using var reader = new StringReader(script);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("DELIMITER", StringComparison.OrdinalIgnoreCase))
            {
                if (TryYieldStatement(builder, delimiter, out var pending))
                {
                    yield return pending;
                }

                delimiter = ResolveDelimiter(trimmedLine);
                continue;
            }

            builder.AppendLine(line);
            if (TryYieldStatement(builder, delimiter, out var statement))
            {
                yield return statement;
            }
        }

        if (TryTrimmedStatement(builder, delimiter, out var remaining))
        {
            yield return remaining;
        }
    }

    private static string ResolveDelimiter(string delimiterLine)
    {
        var value = delimiterLine.Substring("DELIMITER".Length).Trim();
        return string.IsNullOrWhiteSpace(value) ? DefaultDelimiter : value;
    }

    private static bool TryYieldStatement(StringBuilder builder, string delimiter, out string? statement)
    {
        if (!TryTrimmedStatement(builder, delimiter, out var current))
        {
            statement = null;
            return false;
        }

        statement = current;
        builder.Clear();
        return true;
    }

    private static bool TryTrimmedStatement(StringBuilder builder, string delimiter, out string? statement)
    {
        var trimmed = builder.ToString().TrimEnd();
        if (!trimmed.EndsWith(delimiter, StringComparison.Ordinal))
        {
            statement = null;
            return false;
        }

        var withoutDelimiter = trimmed[..^delimiter.Length].Trim();
        if (string.IsNullOrWhiteSpace(withoutDelimiter))
        {
            statement = null;
            return false;
        }

        statement = withoutDelimiter;
        return true;
    }
}
