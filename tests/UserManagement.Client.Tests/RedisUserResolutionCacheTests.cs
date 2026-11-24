using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Infrastructure;
using UserManagement.Client;
using UserManagement.Contracts;

namespace UserManagement.Tests;

public sealed class RedisUserResolutionCacheTests
{
    [Fact]
    public async Task SharedEntryIsReusedAndRevisionChangeLoadsFreshRoles()
    {
        FakeRedis redis = new();
        FakeSource source = new();
        RedisUserResolutionCache first = Create(source, redis);
        RedisUserResolutionCache second = Create(source, redis);
        UserIdentityRequest request = new("default", Guid.NewGuid(), "9155ff4d-2708-4770-b87e-e238645ec5df");

        await first.ResolveAsync(request, default);
        await second.ResolveAsync(request, default);
        Assert.Equal(1, source.Calls);

        first.ObserveRevision(1);
        second.ObserveRevision(1);
        await second.ResolveAsync(request, default);
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task RedisOutageLoadsFromUserManagementEachTime()
    {
        FakeRedis redis = new() { Fail = true };
        FakeSource source = new();
        RedisUserResolutionCache cache = Create(source, redis);
        UserIdentityRequest request = new("default", Guid.NewGuid(), "9155ff4d-2708-4770-b87e-e238645ec5df");

        await cache.ResolveAsync(request, default);
        await cache.ResolveAsync(request, default);

        Assert.Equal(2, source.Calls);
    }

    private static RedisUserResolutionCache Create(FakeSource source, FakeRedis redis) =>
        new(source, redis, Options.Create(new UserManagementClientOptions()),
            NullLogger<RedisUserResolutionCache>.Instance);

    private sealed class FakeSource : IUserManagementClient
    {
        public int Calls;
        public Task<UserIdentityResponse> ResolveAsync(UserIdentityRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new UserIdentityResponse(Guid.NewGuid(), request.TenantId,
                request.UnitOrganizationId, "Organization", true, []));
        }
    }

    private sealed class FakeRedis : IRedisCacheService
    {
        private readonly Dictionary<string, string> _values = new();
        public bool Fail { get; set; }
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("Redis unavailable") : Task.FromResult(_values.GetValueOrDefault(key));
        public Task SetValueAsync(string key, string value, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("Redis unavailable");
            _values[key] = value;
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
