using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Infrastructure;
using UserManagement.Contracts;

namespace UserManagement.Client;

public sealed class RedisUserResolutionCache(IUserManagementClient source, IRedisCacheService redis,
    IOptions<UserManagementClientOptions> options, ILogger<RedisUserResolutionCache> logger)
    : IUserManagementClient, IUserResolutionCacheInvalidator
{
    private readonly Dictionary<string, KeyedGate> _gates = new(StringComparer.Ordinal);
    private readonly object _gateLock = new();
    private long _observedRevision = -1;

    public async Task<UserIdentityResponse> ResolveAsync(UserIdentityRequest request, CancellationToken cancellationToken)
    {
        string identity = $"{request.TenantId.Trim().ToLowerInvariant()}|{request.UnitOrganizationId:D}|{request.ObjectId.Trim().ToLowerInvariant()}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        string key = $"um:resolution:v1:{Volatile.Read(ref _observedRevision)}:{hash}";
        UserIdentityResponse? hit = await ReadAsync(key, cancellationToken);
        if (hit is not null) return hit;

        KeyedGate gate;
        lock (_gateLock)
        {
            if (!_gates.TryGetValue(key, out gate!)) _gates[key] = gate = new KeyedGate();
            gate.Users++;
        }
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);
            try
            {
                hit = await ReadAsync(key, cancellationToken);
                if (hit is not null) return hit;
                DateTimeOffset expires = DateTimeOffset.UtcNow.Add(options.Value.CacheLifetime);
                UserIdentityResponse response = await source.ResolveAsync(request, cancellationToken);
                if (key == $"um:resolution:v1:{Volatile.Read(ref _observedRevision)}:{hash}")
                    await WriteAsync(key, response, expires - DateTimeOffset.UtcNow, cancellationToken);
                return response;
            }
            finally { gate.Semaphore.Release(); }
        }
        finally
        {
            lock (_gateLock)
            {
                gate.Users--;
                if (gate.Users == 0)
                {
                    _gates.Remove(key);
                    gate.Semaphore.Dispose();
                }
            }
        }
    }

    public void ObserveRevision(long revision)
    {
        long previous = Interlocked.Exchange(ref _observedRevision, revision);
        if (previous != revision)
            logger.LogInformation("User resolution Redis namespace changed from revision {Previous} to {Current}", previous, revision);
    }

    private async Task<UserIdentityResponse?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await redis.GetValueAsync(key, cancellationToken);
            return value is null ? null : JsonSerializer.Deserialize<UserIdentityResponse>(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis user-resolution read failed; loading from User Management");
            return null;
        }
    }

    private async Task WriteAsync(string key, UserIdentityResponse response, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (lifetime <= TimeSpan.Zero) return;
        try { await redis.SetValueAsync(key, JsonSerializer.Serialize(response), lifetime, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis user-resolution write failed; request used fresh User Management response");
        }
    }

    private sealed class KeyedGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users { get; set; }
    }
}
