using FluentAssertions;
using NUnit.Framework;
using VladiCore.Api.Infrastructure.Database;

namespace VladiCore.Tests.Infrastructure;

[TestFixture]
public class MigrationTableDefinitionValidatorTests
{
    [Test]
    public void Should_accept_expected_schema()
    {
        var columns = new[]
        {
            new MigrationTableColumnDefinition("script_name", "varchar", "varchar(255)", false, true, null),
            new MigrationTableColumnDefinition("applied_at", "datetime", "datetime", false, false, "CURRENT_TIMESTAMP")
        };

        MigrationTableDefinitionValidator.IsValid(columns).Should().BeTrue();
    }

    [Test]
    public void Should_reject_when_script_name_column_has_wrong_length()
    {
        var columns = new[]
        {
            new MigrationTableColumnDefinition("script_name", "varchar", "varchar(128)", false, true, null),
            new MigrationTableColumnDefinition("applied_at", "datetime", "datetime", false, false, "CURRENT_TIMESTAMP")
        };

        MigrationTableDefinitionValidator.IsValid(columns).Should().BeFalse();
    }

    [Test]
    public void Should_reject_when_applied_at_missing_default()
    {
        var columns = new[]
        {
            new MigrationTableColumnDefinition("script_name", "varchar", "varchar(255)", false, true, null),
            new MigrationTableColumnDefinition("applied_at", "datetime", "datetime", false, false, null)
        };

        MigrationTableDefinitionValidator.IsValid(columns).Should().BeFalse();
    }
}
