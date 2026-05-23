using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobScout.Api.Tests.TestHelpers;

internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<T?> ReadJsonAsync<T>(this HttpContent content, CancellationToken ct = default)
        => content.ReadFromJsonAsync<T>(Options, ct);

    public static Task<T?> GetJsonAsync<T>(this HttpClient client, string url, CancellationToken ct = default)
        => client.GetFromJsonAsync<T>(url, Options, ct);
}
