# Shared authorization NuGets

This repository owns seven .NET 10 packages used by the Access Control and User Management services and by consuming APIs:

| Package | Responsibility |
| --- | --- |
| `AccessControl.Contracts` | Decision, policy, entitlement, and catalog transport types |
| `AccessControl.Client` | Endpoint catalog registration, decision HTTP client, and fail-closed ASP.NET Core middleware |
| `UserManagement.Contracts` | User, role, organization, and resolution transport types and claim names |
| `UserManagement.Client` | User resolution HTTP client, Redis cache, revision polling, and claims middleware |
| `Shared.Infrastructure` | Redis connection/cache abstraction and optional Azure Key Vault configuration provider |
| `Common.Persistence` | Validated write actor and shared entity metadata for the two services |
| `AdminManagement.Security` | Entra administrator authentication and actor identity for the two services |

The client packages depend on their matching contracts packages. `UserManagement.Client` depends on `Shared.Infrastructure`; every consuming API using it must configure Redis. `AdminManagement.Security` depends on `Common.Persistence`. `AccessControl.Client` and `UserManagement.Client` are version `0.21.0`; the other five packages remain at `0.17.0`. `Shared.Infrastructure` also exposes `AddSharedKeyVault` for service startup. Role and entitlement codes remain in the wire contracts; modules remain absent.

## Repository layout and local build

The three repositories can be cloned separately. From the Shared repository:

```powershell
dotnet test Shared.slnx -c Release
dotnet pack Shared.slnx -c Release -o packages
```

The pack command writes all seven `.nupkg` files to the ignored contents of the tracked `packages` directory. The service repositories reference both client packages at 0.21.0 and the other Shared packages at 0.17.0, including their tests and benchmarks. Copy the package files into each service repository's `.packages` directory; neither solution references Shared source projects. Each service's `NuGet.config` maps these package IDs to its own `.packages` directory, so a checkout with the package files can restore without a sibling checkout or separate package download. When a private feed is available, update the source mapping and CI credentials instead. Do not commit feed credentials.

Redis entries use an application-specific prefix, a hash of the scoped lookup identity, and a five-second absolute TTL. Each client polls the source database revision through its service; changed revisions select a new cache namespace. Redis errors load from the source rather than granting from an expired entry. Use a bounded Redis deployment (`maxmemory` with an eviction policy); the old per-process `MaximumCachedUsers` setting does not bound Redis.

After providing the package files, run each service suite from its own repository:

```powershell
# In the Access Control repository:
dotnet test AccessControl.sln -c Release

# In the User Management repository:
dotnet test UserManagement.sln -c Release
```

Package-only tests live in `Shared/tests`; service API, persistence, and cross-service middleware tests remain with the services. The consuming host must validate the Entra JWT and accept organization headers only from trusted ingress before these middlewares run. The client packages no longer send API keys. Restrict keyless service routes to trusted network callers. See each service README for its configuration and request flow.

## Docker builds

Each service Dockerfile uses only its own repository as build context. From each repository's root, run:

```powershell
# In the Access Control repository:
docker build -f src/AccessControl.Service/Dockerfile -t access-control:local .

# In the User Management repository:
docker build -f src/UserManagement.Service/Dockerfile -t user-management:local .
```

Run each command in its corresponding service repository. Dockerfile-specific ignore files include only its `src` tree, `NuGet.config`, and packed `.nupkg` files; build outputs and local secrets are excluded from the build context.
