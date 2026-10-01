using Rove.Core.Services;

namespace Rove.UI.Tests;

internal sealed class FakeSecretStore : ISecretStore
{
    public Dictionary<string, string> Saved { get; } = [];

    public Task<string?> LookupAsync(string key, CancellationToken ct) =>
        Task.FromResult(Saved.TryGetValue(key, out string? secret) ? secret : null);

    public Task<bool> SaveAsync(string key, string label, string secret, CancellationToken ct)
    {
        Saved[key] = secret;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct) => Task.FromResult(Saved.Remove(key));
}
