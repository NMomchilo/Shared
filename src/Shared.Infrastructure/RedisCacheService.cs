using StackExchange.Redis;

namespace Shared.Infrastructure;

public sealed class RedisCacheService(IConnectionMultiplexer connection, int databaseIndex, string keyPrefix) : IRedisCacheService
{
    private readonly IDatabase _database = connection.GetDatabase(databaseIndex);
    private readonly string _prefix = keyPrefix + ":";

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await _database.StringGetAsync(_prefix + key);
        cancellationToken.ThrowIfCancellationRequested();
        return value.IsNull ? null : value.ToString();
    }

    public async Task SetValueAsync(string key, string value, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
        await _database.StringSetAsync(_prefix + key, value, lifetime);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(_prefix + key);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
