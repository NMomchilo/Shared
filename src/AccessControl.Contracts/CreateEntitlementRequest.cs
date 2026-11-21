using System.Text.Json.Serialization;

namespace AccessControl.Contracts;

public sealed record CreateEntitlementRequest(string TenantId,
    [property: JsonRequired] Guid UnitOrganizationId, string DisplayName);
