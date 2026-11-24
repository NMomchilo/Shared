using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Shared.Infrastructure;

public static class KeyVaultConfiguration
{
    public static ConfigurationManager AddSharedKeyVault(this ConfigurationManager configuration)
    {
        string? vaultUrl = configuration["KeyVault:Url"];
        if (string.IsNullOrWhiteSpace(vaultUrl)) return configuration;
        if (!Uri.TryCreate(vaultUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("KeyVault:Url must be an absolute HTTPS URL.");

        string? tenantId = configuration["KeyVault:TenantId"];
        string? clientId = configuration["KeyVault:ClientId"];
        string? clientSecret = configuration["KeyVault:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientSecret) &&
            (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId)))
            throw new InvalidOperationException("KeyVault:TenantId and KeyVault:ClientId are required with KeyVault:ClientSecret.");

        TokenCredential credential = !string.IsNullOrWhiteSpace(clientSecret)
            ? new ClientSecretCredential(tenantId!, clientId!, clientSecret)
            : new DefaultAzureCredential();
        configuration.AddAzureKeyVault(uri, credential);
        return configuration;
    }
}
