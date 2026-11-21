# Shared authorization NuGets

This repository owns six .NET 10 packages used by the Access Control and User Management services and by consuming APIs:

| Package | Responsibility |
| --- | --- |
| `AccessControl.Contracts` | Decision, policy, entitlement, and catalog transport types |
| `AccessControl.Client` | Endpoint catalog registration, decision HTTP client, and fail-closed ASP.NET Core middleware |
| `UserManagement.Contracts` | User, role, organization, and resolution transport types and claim names |
| `UserManagement.Client` | User resolution HTTP client, bounded cache, revision polling, and claims middleware |
| `Common.Persistence` | Validated write actor and shared entity metadata for the two services |
| `AdminManagement.Security` | Entra administrator authentication and actor identity for the two services |

The client packages depend on their matching contracts packages. `AdminManagement.Security` depends on `Common.Persistence`. All six package IDs use version `0.13.0`. The two service support packages are for server applications; do not install them in browser or consuming API projects unless those projects actually need their server-side types. Version 0.13.0 removes role codes, entitlement codes, and modules from the wire contracts. Consumers must update together.

## Repository layout and local build

The three repositories can be cloned separately. From the Shared repository:

```powershell
dotnet test Shared.slnx -c Release
dotnet pack Shared.slnx -c Release -o packages
```

The pack command writes all six `.nupkg` files to the ignored contents of the tracked `packages` directory. The service repositories reference all six Shared libraries by package ID and version `0.13.0`, including their tests and benchmarks. Copy the package files into each service repository's `.packages` directory and commit them there; neither solution references Shared source projects. Each service's `NuGet.config` maps these package IDs to its own `.packages` directory, so a clean GitHub checkout can restore without a sibling checkout or separate package download. When a private feed is available, update the source mapping and CI credentials instead. Do not commit feed credentials.

After providing the package files, run each service suite from its own repository:

```powershell
# In the Access Control repository:
dotnet test AccessControl.sln -c Release

# In the User Management repository:
dotnet test UserManagement.sln -c Release
```

Package-only tests live in `Shared/tests`; service API, persistence, and cross-service middleware tests remain with the services. The consuming host must validate the Entra JWT and gateway identity before these middlewares run. Keep API keys on trusted servers; do not place them in browser code. See each service README for its configuration and request flow.

## Docker builds

Each service Dockerfile uses only its own repository as build context. From each repository's root, run:

```powershell
# In the Access Control repository:
docker build -f src/AccessControl.Service/Dockerfile -t access-control:local .

# In the User Management repository:
docker build -f src/UserManagement.Service/Dockerfile -t user-management:local .
```

Run each command in its corresponding service repository. Dockerfile-specific ignore files include only its `src` tree, `NuGet.config`, and packed `.nupkg` files; build outputs and local secrets are excluded from the build context.
