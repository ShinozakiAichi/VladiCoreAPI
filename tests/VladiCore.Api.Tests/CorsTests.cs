using System.Net.Http;
using System.Threading.Tasks;

namespace VladiCore.Api.Tests;

public class CorsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WhenAnyOriginAllowed_ReturnsWildcardHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/ping");
        request.Headers.Add("Origin", "http://example.com");

        var response = await _client.SendAsync(request);

        Assert.Contains("*", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Get_WithConfiguredOrigin_ReturnsThatOrigin()
    {
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost:3000");

        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/ping");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        Assert.Contains("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin"));

        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
    }
}
