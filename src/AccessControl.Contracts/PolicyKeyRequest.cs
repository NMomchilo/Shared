using System.Text.Json.Serialization;

namespace AccessControl.Contracts;

public sealed record PolicyKeyRequest(string TenantId, string HttpMethod, string RouteTemplate,
    [property: JsonRequired] Guid UnitOrganizationId);
