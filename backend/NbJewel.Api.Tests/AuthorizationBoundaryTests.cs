// Role: Verifies HTTP authorization, CSRF, and published image boundaries without SQL.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NbJewel.Api.Tests;

public sealed class AuthorizationBoundaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public AuthorizationBoundaryTests(WebApplicationFactory<Program> factory)
        => client = factory.CreateClient();

    [Theory]
    [InlineData("/api/items")]
    [InlineData("/api/stores")]
    [InlineData("/api/stock/low")]
    [InlineData("/api/jewelry/all")]
    [InlineData("/api/auth/me")]
    public async Task AnonymousCannotReadProtectedData(string path)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);

    [Fact]
    public async Task RegistrationRequiresCsrf()
        => Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = "customer@example.test", password = "ExamplePassword123!" })).StatusCode);

    [Fact]
    public async Task CsrfEndpointIssuesToken()
    {
        var result = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        Assert.False(string.IsNullOrEmpty(result.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task UnconfiguredIdentityFailsClosedWhilePublicCatalogWorks()
    {
        var csrf = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        request.Content = JsonContent.Create(new { email = "test@example.test", password = "PasswordExample123!" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jewelry")).StatusCode);
    }

    [Fact]
    public async Task UnpublishedImageCannotBypassPublicList()
        => Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/jewelry-images/not-published.png")).StatusCode);
}
