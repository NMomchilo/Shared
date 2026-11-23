using System.Text.Json.Serialization;

namespace UserManagement.Contracts;

public sealed record CreateRoleRequest(string TenantId, Guid UnitOrganizationId, string DisplayName,
    [property: JsonRequired] string Code);
