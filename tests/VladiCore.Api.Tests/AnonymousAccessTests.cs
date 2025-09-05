using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace VladiCore.Api.Tests;

public class AnonymousAccessTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public AnonymousAccessTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Products_AllowsAnonymous()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_Products_RequiresAuthentication()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Categories_AllowsAnonymous()
    {
        var response = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_Categories_RequiresAuthentication()
    {
        var response = await _client.PostAsJsonAsync("/api/categories", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
