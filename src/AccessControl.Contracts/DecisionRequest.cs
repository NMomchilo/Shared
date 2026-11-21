using System.Text.Json.Serialization;

namespace AccessControl.Contracts;

public sealed record DecisionRequest(
    string? SubjectId,
    string HttpMethod,
    string RouteTemplate,
    string TenantId,
    [property: JsonRequired] Guid UnitOrganizationId,
    IReadOnlyList<Guid>? RoleIds = null,
    bool IsSuperAdmin = false,
    bool IsUnitAdmin = false);
