using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Shared.Infrastructure;

public static class SharedRedisExtensions
{
    public static IServiceCollection AddSharedRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<RedisOptions>>(provider =>
            new RedisConnectionOptionsValidator(provider.GetService<IHostEnvironment>()));
        services.AddOptions<RedisOptions>().Bind(configuration.GetSection(RedisOptions.SectionName))
            .Configure(options =>
            {
                options.LocalRedisServiceConnectionString = configuration.GetConnectionString("LocalRedisServiceConnectionString");
                options.RedisServiceConnectionString = configuration.GetConnectionString("RedisServiceConnectionString");
            })
            .Validate(value => value.ApplicationDatabaseIndex >= 0 &&
                value.KeyPrefix.Length is > 0 and <= 80 &&
                value.KeyPrefix.All(character => char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_'),
                "Redis database index must be nonnegative and Redis:KeyPrefix must be 1-80 ASCII letters, digits, colons, hyphens, or underscores.")
            .ValidateOnStart();
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            RedisOptions options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
            bool isDevelopment = provider.GetService<IHostEnvironment>()?.IsDevelopment() == true;
            string connectionString = options.GetConnectionString(isDevelopment);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:RedisServiceConnectionString is required.");
            ConfigurationOptions redisConfiguration = ConfigurationOptions.Parse(connectionString);
            redisConfiguration.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });
        services.AddSingleton<IRedisCacheService>(provider => new RedisCacheService(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            provider.GetRequiredService<IOptions<RedisOptions>>().Value.ApplicationDatabaseIndex,
            provider.GetRequiredService<IOptions<RedisOptions>>().Value.KeyPrefix));
        return services;
    }

    private sealed class RedisConnectionOptionsValidator(IHostEnvironment? environment) : IValidateOptions<RedisOptions>
    {
        public ValidateOptionsResult Validate(string? name, RedisOptions options) =>
            string.IsNullOrWhiteSpace(options.GetConnectionString(environment?.IsDevelopment() == true))
                ? ValidateOptionsResult.Fail("Configure ConnectionStrings:RedisServiceConnectionString, or LocalRedisServiceConnectionString in Development.")
                : ValidateOptionsResult.Success;
    }
}
