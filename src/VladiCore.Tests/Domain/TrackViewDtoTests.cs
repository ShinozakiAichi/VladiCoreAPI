using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using VladiCore.Domain.DTOs;

namespace VladiCore.Tests.Domain;

[TestFixture]
public class TrackViewDtoTests
{
    [Test]
    public void Deserialize_ShouldTreatNonNumericUserIdAsNull()
    {
        const string json = "{\"productId\":42,\"sessionId\":\"sess-123\",\"userId\":\"user-5678\"}";

        var dto = JsonSerializer.Deserialize<TrackViewDto>(json);

        dto.Should().NotBeNull();
        dto!.UserId.Should().BeNull();
    }

    [Test]
    public void Deserialize_ShouldParseNumericUserIdFromString()
    {
        const string json = "{\"productId\":42,\"sessionId\":\"sess-123\",\"userId\":\"512\"}";

        var dto = JsonSerializer.Deserialize<TrackViewDto>(json);

        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(512);
    }
}
