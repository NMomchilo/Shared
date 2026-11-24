using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UserManagement.Contracts;

namespace UserManagement.Client;

public sealed class UserManagementMiddleware(RequestDelegate next, IUserManagementClient client,
    IOptions<UserManagementClientOptions> options,
    ILogger<UserManagementMiddleware> logger)
{
    private const string MappedOid = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        string? objectId = context.User.FindFirstValue("oid") ?? context.User.FindFirstValue(MappedOid);
        if (!Guid.TryParse(objectId, out Guid id) || id == Guid.Empty)
        {
            logger.LogWarning("Authenticated request has no valid oid; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string? delegatedScope = context.User.FindFirstValue("scp") ??
            context.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/scope");
        if (string.IsNullOrWhiteSpace(delegatedScope) ||
            !delegatedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(options.Value.RequiredDelegatedScope, StringComparer.Ordinal))
        {
            logger.LogWarning("Authenticated principal has no delegated user scope; trace {TraceId}",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        string tenantId = context.User.FindFirstValue(options.Value.TenantClaimType) ?? options.Value.DefaultTenantId;
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 128)
        {
            logger.LogWarning("Authenticated request has invalid tenant; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Request.Headers.TryGetValue(options.Value.UnitOrganizationIdHeaderName, out var idValues);
        context.Request.Headers.TryGetValue(options.Value.UnitOrganizationNameHeaderName, out var nameValues);
        string? organizationName = nameValues.Count == 1 ? nameValues[0]?.Trim() : null;
        if (idValues.Count != 1 || !Guid.TryParse(idValues[0], out Guid organizationId) ||
            organizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(organizationName) || organizationName.Length > 200)
        {
            logger.LogWarning("Authenticated request lacks valid ingress organization headers; trace {TraceId}",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        UserIdentityRequest request = new(tenantId, organizationId, id.ToString("D"));
        UserIdentityResponse resolved;
        try
        {
            resolved = await client.ResolveAsync(request, context.RequestAborted);
        }
        catch (UnitOrganizationAccessException)
        {
            logger.LogWarning("Gateway organization was rejected; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        catch (UserNotProvisionedException)
        {
            logger.LogInformation("User or organization membership is not provisioned; trace {TraceId}",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
            InvalidOperationException or System.Text.Json.JsonException)
        {
            logger.LogError(exception, "User Management unavailable; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!resolved.IsActive)
        {
            logger.LogInformation("Inactive user denied; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!string.Equals(resolved.TenantId, tenantId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("User Management returned a different tenant; trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        if (resolved.UnitOrganizationId != organizationId || string.IsNullOrWhiteSpace(resolved.UnitOrganizationName))
        {
            logger.LogError("User Management returned a different organization; trace {TraceId}",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        ClaimsIdentity identity = new([], UserManagementClaims.IdentityAuthenticationType, ClaimTypes.Name,
            UserManagementClaims.RoleId);
        identity.AddClaim(new Claim(UserManagementClaims.UserId, resolved.UserId.ToString("D")));
        identity.AddClaim(new Claim(UserManagementClaims.TenantId, resolved.TenantId));
        identity.AddClaim(new Claim(UserManagementClaims.UnitOrganizationId, resolved.UnitOrganizationId.ToString("D")));
        identity.AddClaim(new Claim(UserManagementClaims.UnitOrganizationName, resolved.UnitOrganizationName));
        identity.AddClaim(new Claim(UserManagementClaims.IsSuperAdmin, resolved.IsSuperAdmin ? "true" : "false"));
        identity.AddClaim(new Claim(UserManagementClaims.IsUnitAdmin, resolved.IsUnitAdmin ? "true" : "false"));
        foreach (UserRoleInfo role in resolved.Roles)
        {
            identity.AddClaim(new Claim(UserManagementClaims.RoleId, role.RoleId.ToString("D")));
        }

        context.User.AddIdentity(identity);
        logger.LogDebug("Added {RoleCount} application roles; trace {TraceId}", resolved.Roles.Count, context.TraceIdentifier);
        await next(context);
    }
}
