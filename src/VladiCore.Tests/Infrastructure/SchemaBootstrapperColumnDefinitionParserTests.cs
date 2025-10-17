using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using VladiCore.Data.Provisioning;

namespace VladiCore.Tests.Infrastructure;

public class SchemaBootstrapperColumnDefinitionParserTests
{
    private static object? InvokeParse(string definition)
    {
        var parserType = typeof(SchemaBootstrapper).GetNestedType("ColumnDefinitionParser", BindingFlags.NonPublic | BindingFlags.Static);
        parserType.Should().NotBeNull();

        var parseMethod = parserType!.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        parseMethod.Should().NotBeNull();

        return parseMethod!.Invoke(null, new object?[] { definition });
    }

    [Test]
    public void Parse_ShouldIgnoreFullTextIndexDefinition()
    {
        const string definition = "FULLTEXT INDEX FT_Products_NameDescription (Name, Description)";

        var result = InvokeParse(definition);

        result.Should().BeNull();
    }

    [Test]
    public void Parse_ShouldParseRegularColumnDefinition()
    {
        const string definition = "`Name` VARCHAR(255) NOT NULL";

        var result = InvokeParse(definition);

        result.Should().NotBeNull();
    }
}
