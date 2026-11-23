namespace UserManagement.Contracts;

public sealed record RoleInfo(Guid RoleId, string TenantId, Guid UnitOrganizationId, string DisplayName, bool IsActive, string Code);
