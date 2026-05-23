using JobScout.Api.Tests.Fixtures;

namespace JobScout.Api.Tests.Auth;

public class AuthControllerTests : IClassFixture<JobScoutWebApplicationFactory>
{
    private readonly JobScoutWebApplicationFactory _factory;

    public AuthControllerTests(JobScoutWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_WithValidPayload_ReturnsTokenAndUser()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"valid-{Guid.NewGuid():N}@example.com",
            Password = "StrongPass1!",
            DisplayName = "Alice"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Contain("@example.com");
        body.User.DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"weak-{Guid.NewGuid():N}@example.com",
            Password = "weak",
            DisplayName = "Bob"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await _factory.RegisterAsync(client, email, "StrongPass1!");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "StrongPass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await _factory.RegisterAsync(client, email, "StrongPass1!");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "NotTheRightOne1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = $"no-such-{Guid.NewGuid():N}@example.com",
            Password = "Whatever1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/profiles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
