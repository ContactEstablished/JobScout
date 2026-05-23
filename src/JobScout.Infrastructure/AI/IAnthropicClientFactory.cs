using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace JobScout.Infrastructure.AI;

/// <summary>
/// Thin facade over <see cref="AnthropicClient"/> so tests can substitute a stub.
/// </summary>
public interface IAnthropicMessenger
{
    Task<MessageResponse> SendAsync(MessageParameters parameters, CancellationToken ct = default);
}

public interface IAnthropicClientFactory
{
    IAnthropicMessenger Create(string apiKey);
}

public class AnthropicClientFactory : IAnthropicClientFactory
{
    public IAnthropicMessenger Create(string apiKey)
        => new AnthropicMessenger(new AnthropicClient(new APIAuthentication(apiKey)));

    private sealed class AnthropicMessenger : IAnthropicMessenger
    {
        private readonly AnthropicClient _client;
        public AnthropicMessenger(AnthropicClient client) => _client = client;

        public Task<MessageResponse> SendAsync(MessageParameters parameters, CancellationToken ct = default)
            => _client.Messages.GetClaudeMessageAsync(parameters, ct);
    }
}
