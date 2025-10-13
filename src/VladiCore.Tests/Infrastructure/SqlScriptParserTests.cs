using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VladiCore.Data.Infrastructure;

namespace VladiCore.Tests.Infrastructure;

public class SqlScriptParserTests
{
    [Test]
    public void SplitStatements_ShouldSplitSimpleStatements()
    {
        const string script = @"CREATE TABLE sample (id INT);\nINSERT INTO sample VALUES (1);";

        var statements = SqlScriptParser.SplitStatements(script).ToArray();

        statements.Should().HaveCount(2);
        statements[0].Should().Be("CREATE TABLE sample (id INT)");
        statements[1].Should().Be("INSERT INTO sample VALUES (1)");
    }

    [Test]
    public void SplitStatements_ShouldRespectCustomDelimiter()
    {
        const string script = @"DELIMITER $$\nCREATE PROCEDURE test_proc()\nBEGIN\n    SELECT 1;\nEND$$\nDELIMITER ;\nINSERT INTO sample VALUES (2);";

        var statements = SqlScriptParser.SplitStatements(script).ToArray();

        statements.Should().HaveCount(2);
        statements[0].Should().Contain("CREATE PROCEDURE test_proc()");
        statements[1].Should().Be("INSERT INTO sample VALUES (2)");
    }
}
