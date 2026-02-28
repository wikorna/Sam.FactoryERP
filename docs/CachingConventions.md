# Caching Conventions (Sam.FactoryERP)

## Overview

We employ `Microsoft.Extensions.Caching.Hybrid` as a universal `ICacheService` abstraction. It natively handles single-flight caching, protecting databases from stampedes. The cache is composed of two layers:

- **L1 (In-Memory)**: Process-local `MemoryCache` with short expirations to minimize cross-instance skew.
- **L2 (Redis)**: Distributed cache across instances.

## Cache Configuration

Managed via `appsettings.json` under the `Cache` block.

- **Fail-Open**: If Redis is unavailable, requests seamlessly fallback to computing the value natively.
- **Instance Prefix**: Set `Cache:InstanceName` to `FactoryERP:` or `FactoryERP:{Env}`.

## Cache Keys

To ensure multi-tenancy boundaries and avoid accidental data leakage, cache keys MUST follow the convention:
`<TenantId>:<CompanyId>:<PlantId>:<Domain>:<SchemaVersion>:<EntityId>`

**Example**:
`tenant_99:co_BKK:plant_01:inventory-master:v1:itemA_1234`

If Company/Plant/Tenant context isn't applicable, omit them but include the domain context.
`<Domain>:<SchemaVersion>:<EntityId>`
`auth:v1:user_8829`

> **Note**: `SchemaVersion` is critical. If item schema is altered, incrementing `v1` to `v2` will force a re-fetch and automatically ignore old JSON formats in the cache, avoiding deserialization errors during deployments.

## Tagging & Cache Invalidation

Tags group cached items to allow mass-purging without wildcard scanning Redis keys.
Tags convention:
`tag:<TenantId>:<Context>`
**Example**:
`tag:tenant_10:inventory`

When an `ItemMaster` is modified, call `cacheService.RemoveByTagAsync("tag:tenant_10:inventory")` to invalidate all relevant items in the tenant.

## Time To Live (TTL) Matrix

| Category               | L1 Expiration | L2 Expiration (Redis) | Example Use Case                         |
| ---------------------- | ------------- | --------------------- | ---------------------------------------- |
| **Master Data**        | 5 mins        | 24 - 48 hours         | Inventory Item Master, Customer Profiles |
| **Auth/Security**      | 1 min         | 30 mins               | User claims, Access tokens, Permissions  |
| **Reports**            | 10 mins       | 12 hours              | Cost estimations, EOD Dashboards         |
| **External/3rd Party** | 2 mins        | 15 mins               | Currency Rates, External Supplier APIs   |

Always prefer slightly shorter L1 local expirations in high-scale environments. This reduces data staleness when load balancers bounce traffic between nodes while avoiding the latency of constantly hitting L2.
