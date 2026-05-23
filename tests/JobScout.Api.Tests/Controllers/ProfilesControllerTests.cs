using JobScout.Api.Tests.Fixtures;

namespace JobScout.Api.Tests.Controllers;

public class ProfilesControllerTests : IClassFixture<JobScoutWebApplicationFactory>
{
    private readonly JobScoutWebApplicationFactory _factory;

    public ProfilesControllerTests(JobScoutWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> CreateAuthedClientAsync(string suffix)
    {
        var client = _factory.CreateClient();
        var email = $"prof-{suffix}-{Guid.NewGuid():N}@example.com";
        var token = await _factory.RegisterAsync(client, email, "StrongPass1!", $"User {suffix}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentUsersProfiles()
    {
        var aClient = await CreateAuthedClientAsync("a");
        var bClient = await CreateAuthedClientAsync("b");

        // User A creates two profiles
        await aClient.PostAsJsonAsync("/api/profiles", new CreateProfileRequest { Name = "A-1" });
        await aClient.PostAsJsonAsync("/api/profiles", new CreateProfileRequest { Name = "A-2" });
        await bClient.PostAsJsonAsync("/api/profiles", new CreateProfileRequest { Name = "B-1" });

        var aProfiles = await aClient.GetFromJsonAsync<List<SearchProfileDto>>("/api/profiles");
        aProfiles!.Select(p => p.Name).Should().BeEquivalentTo(["A-1", "A-2"]);

        var bProfiles = await bClient.GetFromJsonAsync<List<SearchProfileDto>>("/api/profiles");
        bProfiles!.Select(p => p.Name).Should().BeEquivalentTo(["B-1"]);
    }

    [Fact]
    public async Task GetById_ForOtherUsersProfile_Returns404()
    {
        var aClient = await CreateAuthedClientAsync("ownerA");
        var bClient = await CreateAuthedClientAsync("ownerB");

        var createResp = await bClient.PostAsJsonAsync("/api/profiles", new CreateProfileRequest { Name = "B-only" });
        var bProfile = await createResp.Content.ReadFromJsonAsync<SearchProfileDto>();

        var leakAttempt = await aClient.GetAsync($"/api/profiles/{bProfile!.Id}");
        leakAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProfile_PersistsPhase5Fields()
    {
        var client = await CreateAuthedClientAsync("phase5");
        var resp = await client.PostAsJsonAsync("/api/profiles", new CreateProfileRequest
        {
            Name = "Phase 5",
            PreferredModel = "claude-sonnet-4-6",
            DesiredSalaryMin = 100_000m,
            DesiredSalaryMax = 160_000m
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<SearchProfileDto>();
        dto!.PreferredModel.Should().Be("claude-sonnet-4-6");
        dto.DesiredSalaryMin.Should().Be(100_000m);
        dto.DesiredSalaryMax.Should().Be(160_000m);
    }
}
