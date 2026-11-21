namespace UserManagement.Contracts;

public sealed record CreateRoleRequest(string TenantId, Guid UnitOrganizationId, string DisplayName);
