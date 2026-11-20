using System.Text.Json;
using UserManagement.Contracts;

namespace UserManagement.Tests;

public sealed class UserManagementContractWireTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PublicContractShapes_RoundTripThroughWebJson()
    {
        Guid organization = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid role = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid user = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        UserRoleAssignmentInfo assignment = new(role, "reviewer", "Reviewer", true);
        UserMembershipSummary membership = new(organization, "Operations", true, [assignment]);
        UserSummary summary = new(user, "entra-oid", "Example User", "user@example.test", true, false,
            [membership]);
        object[] contracts =
        [
            new AdminAccessInfo(true, false, [organization], true),
            new CreateRoleRequest("default", organization, "reviewer", "Reviewer"),
            new CreateUnitOrganizationRequest(organization, "default", "Operations"),
            new CreateUserRequest("Example User", "user@example.test", [role]),
            new PagedResult<UserSummary>([summary], 1, 1, 20),
            new ReplaceRolesRequest([role]),
            new RoleInfo(role, "default", organization, "reviewer", "Reviewer", true),
            new SetActiveRequest(false),
            new UnitOrganizationInfo(organization, "default", "Operations"),
            new UpdateRoleRequest("Senior Reviewer"),
            new UpdateUnitOrganizationRequest("New Operations"),
            new UserIdentityRequest("default", organization, "entra-oid"),
            new UserIdentityResponse(user, "default", organization, "Operations", true,
                [new UserRoleInfo(role, "reviewer", "Reviewer")], false, true),
            membership,
            assignment,
            new UserProfileRequest("Example User", "user@example.test"),
            new UserRoleInfo(role, "reviewer", "Reviewer"),
            summary
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
    public void SetActive_RequiresExplicitValue()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SetActiveRequest>("{}", WebJson));
    }
}
