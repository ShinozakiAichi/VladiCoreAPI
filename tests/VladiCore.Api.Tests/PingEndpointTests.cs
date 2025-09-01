using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VladiCore.Api.Tests;

public class PingEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public PingEndpointTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Ping_ReturnsPong()
    {
        var response = await _client.GetAsync("/ping");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", content);
    }
}
