using JobScout.Api.Tests.Fixtures;

namespace JobScout.Api.Tests.Controllers;

public class FilterPresetsControllerTests : IClassFixture<JobScoutWebApplicationFactory>
{
    private readonly JobScoutWebApplicationFactory _factory;

    public FilterPresetsControllerTests(JobScoutWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> CreateAuthedClientAsync(string suffix)
    {
        var client = _factory.CreateClient();
        var email = $"preset-{suffix}-{Guid.NewGuid():N}@example.com";
        var token = await _factory.RegisterAsync(client, email, "StrongPass1!", $"User {suffix}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateProfileAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/profiles", new CreateProfileRequest { Name = name });
        var dto = await resp.Content.ReadJsonAsync<SearchProfileDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/filterpresets?profileId={Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_List_Update_Delete_RoundTrips()
    {
        var client = await CreateAuthedClientAsync("crud");
        var profileId = await CreateProfileAsync(client, "Crud Profile");

        var createResp = await client.PostAsJsonAsync("/api/filterpresets", new SaveFilterPresetRequest
        {
            ProfileId = profileId,
            Name = "Remote 8+",
            Source = JobSource.RemoteOK,
            MinScore = 8m,
            Query = "azure",
            SortBy = JobSortBy.PostedDate
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadJsonAsync<FilterPresetDto>();
        created!.Name.Should().Be("Remote 8+");
        created.SortBy.Should().Be(JobSortBy.PostedDate);

        var list = await client.GetJsonAsync<List<FilterPresetDto>>($"/api/filterpresets?profileId={profileId}");
        list!.Should().ContainSingle();

        var updateResp = await client.PutAsJsonAsync($"/api/filterpresets/{created.Id}", new SaveFilterPresetRequest
        {
            ProfileId = profileId,
            Name = "Remote 9+",
            MinScore = 9m,
            SortBy = JobSortBy.AiScore
        });
        updateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await client.GetJsonAsync<List<FilterPresetDto>>($"/api/filterpresets?profileId={profileId}");
        afterUpdate!.Single().Name.Should().Be("Remote 9+");

        var deleteResp = await client.DeleteAsync($"/api/filterpresets/{created.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetJsonAsync<List<FilterPresetDto>>($"/api/filterpresets?profileId={profileId}");
        afterDelete!.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateName_OnSameProfile_ReturnsConflict()
    {
        var client = await CreateAuthedClientAsync("dup");
        var profileId = await CreateProfileAsync(client, "Dup Profile");
        var req = new SaveFilterPresetRequest { ProfileId = profileId, Name = "Dupe" };

        (await client.PostAsJsonAsync("/api/filterpresets", req)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/filterpresets", req)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CannotCreatePresetOnAnotherUsersProfile()
    {
        var owner = await CreateAuthedClientAsync("owner");
        var intruder = await CreateAuthedClientAsync("intruder");
        var ownerProfileId = await CreateProfileAsync(owner, "Owner Profile");

        var resp = await intruder.PostAsJsonAsync("/api/filterpresets", new SaveFilterPresetRequest
        {
            ProfileId = ownerProfileId,
            Name = "Sneaky"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CannotListPresetsOnAnotherUsersProfile()
    {
        var owner = await CreateAuthedClientAsync("owner2");
        var intruder = await CreateAuthedClientAsync("intruder2");
        var ownerProfileId = await CreateProfileAsync(owner, "Owner Profile 2");

        var resp = await intruder.GetAsync($"/api/filterpresets?profileId={ownerProfileId}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
