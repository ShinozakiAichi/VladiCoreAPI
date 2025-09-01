using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VladiCore.Api.Models.Auth;

namespace VladiCore.Api.Tests;

public class AuthFlowTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthFlowTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Login_Refresh_Logout_Flow()
    {
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest("test@example.com", "Pass123!", "Test"));
        regRes.EnsureSuccessStatusCode();

        var loginRes = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("test@example.com", "Pass123!"));
        loginRes.EnsureSuccessStatusCode();
        var loginData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);

        var refreshRes = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(loginData!.RefreshToken));
        refreshRes.EnsureSuccessStatusCode();

        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken);
        var logoutRes = await _client.SendAsync(logoutReq);
        logoutRes.EnsureSuccessStatusCode();

        var refreshAgain = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(loginData.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAgain.StatusCode);
    }
}
