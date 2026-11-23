namespace AccessControl.Contracts;

public sealed record EntitlementInfo(Guid Id, string TenantId, Guid UnitOrganizationId,
    string DisplayName, bool IsActive, string Code);
