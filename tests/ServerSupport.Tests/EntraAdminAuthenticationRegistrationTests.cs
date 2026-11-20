using AdminManagement.Security;
using Common.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ServerSupport.Tests;

public sealed class EntraAdminAuthenticationRegistrationTests
{
    [Fact]
    public void Registration_RequiresHttpsAuthorityAndConfiguresJwtValidation()
    {
        IConfiguration insecure = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["EntraAdmin:Authority"] = "http://issuer.example.test/",
                ["EntraAdmin:Audience"] = "api://example"
            }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddEntraAdminAuthentication(insecure));

        IConfiguration secure = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["EntraAdmin:Authority"] = "https://issuer.example.test/",
                ["EntraAdmin:Audience"] = "api://example"
            }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddEntraAdminAuthentication(secure);
        using ServiceProvider provider = services.BuildServiceProvider();
        JwtBearerOptions jwt = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://issuer.example.test/", jwt.Authority);
        Assert.Equal("api://example", jwt.Audience);
        Assert.True(jwt.RequireHttpsMetadata);
        Assert.True(jwt.TokenValidationParameters.ValidateIssuer);
        Assert.True(jwt.TokenValidationParameters.ValidateAudience);
        Assert.True(jwt.TokenValidationParameters.ValidateLifetime);
        Assert.NotNull(provider.GetRequiredService<ICurrentActorProvider>());
    }
}
