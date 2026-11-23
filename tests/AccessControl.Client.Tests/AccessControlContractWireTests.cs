using System.Text.Json;
using AccessControl.Contracts;

namespace AccessControl.Tests;

public sealed class AccessControlContractWireTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PublicContractShapes_RoundTripThroughWebJson()
    {
        Guid organization = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid role = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid entitlement = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        PolicyRequest policy = new("default", "GET", "/customers/{id}", false,
            [entitlement], organization);
        EntitlementInfo info = new(entitlement, "default", organization, "View", true, "customers.view");
        object[] contracts =
        [
            new CatalogEndpoint("GET", "/customers/{id}"),
            new CatalogRegistrationRequest("default", organization,
                [new CatalogEndpoint("GET", "/customers/{id}")]),
            new CatalogRegistrationResult( 2),
            new CreateEntitlementRequest("default", organization, "View", "customers.view"),
            new DecisionRequest("oid", "GET", "/customers/{id}", "default", organization,
                [role], false, true),
            new DecisionResponse(true, "admin", true),
            info,
            new EntitlementPageResponse([info], 1, 20, 1),
            new PolicyKeyRequest("default", "GET", "/customers/{id}", organization),
            policy,
            new PolicyPageResponse([policy], 1, 20, 1),
            new RoleEntitlementsRequest("default", role, [entitlement], organization),
            new SetEntitlementActiveRequest(false),
            new UpdateEntitlementDisplayNameRequest("Read customer")
        ];

        foreach (object contract in contracts)
        {
            string json = JsonSerializer.Serialize(contract, contract.GetType(), WebJson);
            object? decoded = JsonSerializer.Deserialize(json, contract.GetType(), WebJson);
            Assert.NotNull(decoded);
            Assert.Equal(json, JsonSerializer.Serialize(decoded, contract.GetType(), WebJson));
        }
    }

    [Fact]
    public void OrganizationScopedWrites_RejectMissingOrganizationId()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PolicyRequest>(
            """{"tenantId":"default","httpMethod":"GET","routeTemplate":"/customers/{id}","isPublic":true,"requiredEntitlementIds":[]}""", WebJson));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateEntitlementRequest>(
            """{"tenantId":"default","displayName":"View"}""", WebJson));
    }
}
