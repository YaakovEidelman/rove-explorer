namespace Rove.Core.Services;

public interface ISecretStore
{
    Task<string?> LookupAsync(string key, CancellationToken ct);

    Task<bool> SaveAsync(string key, string label, string secret, CancellationToken ct);

    Task<bool> DeleteAsync(string key, CancellationToken ct);
}
