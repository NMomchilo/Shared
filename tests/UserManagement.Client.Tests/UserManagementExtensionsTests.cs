using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UserManagement.Client;

namespace UserManagement.Tests;

public sealed class UserManagementExtensionsTests
{
    [Fact]
    public void AddUserManagement_RegistersRedisClientAndConfiguredTimeout()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["UserManagement:ServiceUrl"] = "https://localhost:5220/",
                ["UserManagement:TimeoutSeconds"] = "3",
                ["UserManagement:MaximumCachedUsers"] = "17",
                ["UserManagement:CacheLifetime"] = "00:00:04",
                ["UserManagement:RevisionPollInterval"] = "00:00:01"
            }).Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        Assert.Same(services, services.AddUserManagement(configuration));

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        UserManagementClientOptions options = provider.GetRequiredService<IOptions<UserManagementClientOptions>>().Value;
        Assert.Equal(17, options.MaximumCachedUsers);
        Assert.Equal(TimeSpan.FromSeconds(4), options.CacheLifetime);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisUserResolutionCache));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IUserManagementClient));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(UserManagementRevisionPoller));
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(HttpUserManagementClient));
        Assert.Equal(TimeSpan.FromSeconds(3), client.Timeout);
    }

    [Fact]
    public void AddUserManagement_RejectsCacheLifetimeBeyondFiveSeconds()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["UserManagement:ServiceUrl"] = "https://localhost:5220/",
                ["UserManagement:CacheLifetime"] = "00:00:06"
            }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddUserManagement(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<UserManagementClientOptions>>().Value);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Shared.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
