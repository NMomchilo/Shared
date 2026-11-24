using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Infrastructure;

namespace ServerSupport.Tests;

public sealed class RedisConfigurationTests
{
    [Fact]
    public void RedisOptions_SelectsLocalOnlyInDevelopment()
    {
        RedisOptions options = new()
        {
            LocalRedisServiceConnectionString = "localhost:6379",
            RedisServiceConnectionString = "production:6380"
        };

        Assert.Equal("localhost:6379", options.GetConnectionString(true));
        Assert.Equal("production:6380", options.GetConnectionString(false));
    }

    [Fact]
    public void KeyVaultExtension_RejectsNonHttpsUrl()
    {
        ConfigurationManager configuration = new();
        configuration["KeyVault:Url"] = "http://vault.example";

        Assert.Throws<InvalidOperationException>(() => configuration.AddSharedKeyVault());
    }

    [Fact]
    public void RedisRegistration_RejectsMissingConnectionString()
    {
        ConfigurationManager configuration = new();
        ServiceCollection services = new();
        services.AddSharedRedis(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<RedisOptions>>().Value);
    }

    [Fact]
    public void RedisRegistration_CanConstructDuringRedisOutageForSourceFallback()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RedisServiceConnectionString"] = "127.0.0.1:65000,connectTimeout=500,syncTimeout=500,abortConnect=false",
                ["Redis:KeyPrefix"] = "outage-test"
            }).Build();
        ServiceCollection services = new();
        services.AddSharedRedis(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRedisCacheService>());
    }

    [Fact]
    public void RedisSettings_BindIndexOutsideConnectionStrings()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RedisServiceConnectionString"] = "localhost:6379",
                ["Redis:ApplicationDatabaseIndex"] = "2",
                ["Redis:KeyPrefix"] = "unit-test"
            }).Build();
        ServiceCollection services = new();
        services.AddSharedRedis(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        RedisOptions options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.Equal(2, options.ApplicationDatabaseIndex);
        Assert.Equal("unit-test", options.KeyPrefix);
        Assert.Equal("localhost:6379", options.RedisServiceConnectionString);
    }
}
