# Redis Cache and Key Vault Implementation Plan

**Goal:** Use Redis for User Management client resolutions and Access Control policy/grant caches, and load service secrets from Azure Key Vault when configured.

**Architecture:** Shared publishes Redis and Key Vault support as a versioned NuGet package. User Management clients and Access Control service use Redis entries with five-second TTLs and revision-scoped keys. SQL remains the source of truth and revision polling remains the cross-instance invalidation backstop. Redis errors bypass the cache and load from the authoritative source.

**Tech Stack:** .NET 10, StackExchange.Redis, Azure.Identity, Azure Key Vault configuration provider, xUnit.

## Tasks

- [x] Add Shared Redis options, connection registration, cache service, and Key Vault extension with focused tests.
- [x] Move User Management client resolution caching to Redis; retain single-flight and revision behavior; test misses, hits, invalidation, and outages.
- [x] Move Access Control policy and grants caching to Redis; retain revision behavior; test misses, hits, invalidation, and outages.
- [x] Wire configuration in both services and CustomerApi; add local Redis to Docker Compose; document Key Vault secret names and local run steps.
- [x] Bump and pack Shared packages, copy versioned packages to both service repositories, update references and tests.
- [x] Build and test all three solutions and benchmark projects; report results and limitations.
