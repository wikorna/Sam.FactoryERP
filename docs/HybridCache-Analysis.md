# Sam.FactoryERP — HybridCache (L1 + L2) Analysis Report

> **Date:** 2026-02-20
> **Author:** Performance Architecture Review (Automated)
> **Scope:** Full solution — 9 modules, 2 hosts, 5 building blocks

---

## 1. Solution Structure Mapping

### 1.1 Module Maturity Matrix

| Module | Domain Entities | Application Handlers | API Endpoints | Infrastructure | Maturity |
|--------|----------------|---------------------|---------------|----------------|----------|
| **EDI** | ✅ EdiFileJob, PartnerProfile, PO Staging | ✅ 5 handlers (2 queries, 3 commands) | ✅ 4 endpoints (1 GET, 2 POST, 1 upload) | ✅ Full (EF Core SQL Server, file stores, parsers, outbox) | **Production** |
| **Labeling** | ✅ PrintJob | ✅ 1 handler (command) | ✅ 2 endpoints (1 GET ping, 1 POST) | ✅ Full (EF Core PostgreSQL, MassTransit consumers, TCP printer) | **Production** |
| **Admin** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Sales** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Production** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Purchasing** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Inventory** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Costing** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |
| **Quality** | ⬜ Scaffold only | ⬜ Empty | ⬜ Ping only | ⬜ Scaffold only | Scaffold |

### 1.2 Existing Cache Infrastructure

The solution already has a **production-grade HybridCache infrastructure** fully wired:

| Component | Location | Status |
|-----------|----------|--------|
| `ICacheService` | `FactoryERP.Abstractions/Caching/` | ✅ Ready |
| `HybridCacheService` | `FactoryERP.Infrastructure/Caching/` | ✅ Implemented (fail-open, stampede-proof, tag-based invalidation) |
| `CacheEntrySettings` | `FactoryERP.Abstractions/Caching/` | ✅ Per-entry L1/L2 TTL + tags |
| `CacheKey` helper | `FactoryERP.Abstractions/Caching/` | ✅ Normalized key builder |
| `CacheOptions` | `FactoryERP.Infrastructure/Caching/` | ✅ Config-bound (defaults: L2=30min, L1=5min) |
| `RedisHealthCheck` | `FactoryERP.Infrastructure/Caching/` | ✅ Health endpoint |
| Registration (`AddCaching`) | `Program.cs` (ApiHost) | ✅ Called in startup |
| Redis L2 backend | StackExchange.Redis | ✅ Conditional (config-driven) |

**Key insight:** `ICacheService` is registered but **zero modules currently consume it**. The infrastructure is built but unused — this is the perfect moment to adopt.

### 1.3 Query Handlers Inventory (CQRS Read Side)

| Module | Handler | Type | MediatR Request | DB Access Pattern |
|--------|---------|------|-----------------|-------------------|
| EDI | `GetEdiFileJobQueryHandler` | **Query** | `GetEdiFileJobQuery(Guid JobId)` | `FirstOrDefaultAsync` by PK |
| EDI | `GetEdiFileJobsQueryHandler` | **Query** | `GetEdiFileJobsQuery(partner, status, page, size)` | Filtered + paginated `ToListAsync` + `CountAsync` |
| EDI | `ReceiveEdiFileCommandHandler` | Command | — | `ExistsByChecksumAsync`, `GetPartnerProfileAsync` (reads) |
| EDI | `ParseEdiFileCommandHandler` | Command | — | `GetAsync`, `GetPartnerProfileAsync` (reads) |
| EDI | `ApplyItemMasterImportCommandHandler` | Command | — | `GetAsync`, `GetItemMasterRowsAsync` (reads) |
| Labeling | `CreatePrintJobHandler` | Command | — | Write-only |

### 1.4 API Endpoints (GET Only)

| Module | Route | Method | Handler | Read Pattern |
|--------|-------|--------|---------|-------------|
| EDI | `GET /api/v1/edi/ping` | GET | Inline | Static string |
| Labeling | `GET /api/v1/labeling/ping` | GET | Inline | Static string |
| Admin–Quality | `GET /api/v1/{module}/ping` | GET | Inline | Static string |
| Host | `GET /health` | GET | Inline | Health check |

> **Note:** No business GET endpoints are currently exposed via Minimal API routes for the EDI query handlers. The `GetEdiFileJobQuery` and `GetEdiFileJobsQuery` handlers exist but lack mapped endpoints — this is a gap to address alongside caching.

### 1.5 Background Services Performing Reads

| Service | Module | Read Operations |
|---------|--------|----------------|
| `QrPrintRequestedConsumer` | Labeling (WorkerHost) | `FirstOrDefaultAsync` on PrintJobs by ID |
| `ParseEdiFileCommandHandler` | EDI | `GetAsync` (job), `GetPartnerProfileAsync` (partner lookup) |
| `ApplyItemMasterImportCommandHandler` | EDI | `GetAsync` (job), `GetItemMasterRowsAsync` (staging rows) |
| `ReceiveEdiFileCommandHandler` | EDI | `ExistsByChecksumAsync`, `GetPartnerProfileAsync` |

---

## 2. Query Pattern Detection

### 2.1 EDI Module

| Query | Pattern | Complexity | Notes |
|-------|---------|------------|-------|
| `GetAsync(Guid)` | PK lookup, `FirstOrDefaultAsync` | **Low** | Single row by GUID |
| `GetJobsAsync(partner, status, page, size)` | Filtered + paginated | **Medium** | 2× DB round-trip (COUNT + SELECT), dynamic WHERE |
| `ExistsByChecksumAsync(partner, sha256)` | `AnyAsync` with composite filter | **Low** | Boolean check, should be indexed |
| `GetPartnerProfileAsync(partnerCode)` | `FirstOrDefaultAsync` by business key | **Low** | Master data lookup — repeated frequently |
| `GetItemMasterRowsAsync(jobId)` | Projection query with `Select` | **Medium** | Can return many rows per job |
| `ClearJobAsync(jobId)` | `ExecuteDeleteAsync` | Write | Excluded |

**Missing patterns:** No `AsNoTracking`, no `Include` chains, no `GroupBy`, no aggregations. Queries are simple but repetitive.

### 2.2 Labeling Module

| Query | Pattern | Complexity | Notes |
|-------|---------|------------|-------|
| `FirstOrDefaultAsync(j => j.Id == printJobId)` | PK lookup in consumer | **Low** | Called for every print event |

### 2.3 Scaffold Modules (Admin, Sales, Production, Purchasing, Inventory, Costing, Quality)

No queries implemented yet. **However**, based on ERP domain knowledge, these modules WILL have the heaviest read patterns when built:

| Future Module | Expected Read-Heavy Patterns |
|---------------|------------------------------|
| **Inventory** | Stock levels, item master lookups, warehouse locations, lot tracking |
| **Sales** | Customer master, sales order lists, pricing lookups, order status |
| **Production** | Work order lists, BOM lookups, routing lookups, shop floor status |
| **Purchasing** | Vendor master, PO lists, price agreements, delivery schedules |
| **Costing** | Standard cost lookups, cost roll-ups, variance reports |
| **Quality** | Inspection results, QC checklists, specification lookups |
| **Admin** | User/role lookups, permission checks, system settings, audit logs |

---

## 3. Expensive Query Detection

### 3.1 Current Codebase Flags

| Flag | Location | Severity | Details |
|------|----------|----------|---------|
| 🔴 **Repeated partner lookup** | `GetPartnerProfileAsync` in `ReceiveEdiFileCommandHandler` AND `ParseEdiFileCommandHandler` | **HIGH** | Same PartnerProfile loaded in every step of the EDI pipeline (Receive → Parse → Apply). Master data that rarely changes. |
| 🟡 **Paginated query with COUNT** | `GetJobsAsync` | **MEDIUM** | Two DB round-trips per page load (COUNT + data). Filterable by partner + status. |
| 🟡 **Consumer DB lookup** | `QrPrintRequestedConsumer` | **MEDIUM** | Every consumed message triggers a `FirstOrDefaultAsync`. Under high print volume, this is N queries. |
| 🟡 **Checksum existence check** | `ExistsByChecksumAsync` | **LOW-MEDIUM** | Called for every incoming EDI file. If duplicate detection is frequent, this hits DB unnecessarily. |
| 🟢 **Staging row bulk read** | `GetItemMasterRowsAsync` | **LOW** | One-time read per job, not repeated. |

### 3.2 N+1 Pattern Risk

| Area | Risk | Explanation |
|------|------|-------------|
| EDI Pipeline | **LOW** | Each handler does discrete queries, no navigation property lazy loading |
| `QrPrintRequestedConsumer` | **NONE** | Single entity load, no child collections |
| Future Modules | **HIGH** | When BOM, work orders, or sales orders with line items are built without `AsNoTracking` + explicit `Include`, N+1 is almost guaranteed |

### 3.3 Cross-Module Read Hotspots (Predictive)

| Hotspot | Modules Involved | Why |
|---------|-----------------|-----|
| **PartnerProfile lookup** | EDI → (future) Purchasing, Sales | Partner/vendor master data is referenced across EDI file processing, PO matching, invoice reconciliation |
| **Item Master lookup** | EDI → Inventory → Production → Costing → Sales | Every module will eventually need item data |
| **User/Permission lookup** | Admin → ALL | Authorization checks on every request |

---

## 4. Usage Frequency Heuristics

### 4.1 Estimated Call Frequency

| Endpoint/Query | Usage Context | Estimated Frequency | Hot? |
|----------------|---------------|--------------------|----|
| `GetPartnerProfileAsync` | EDI pipeline (per file) | ~10-100/hour during EDI batch windows | 🔥 |
| `GetEdiFileJobsQuery` (paginated list) | Dashboard / monitoring UI | ~50-200/hour (users refreshing) | 🔥 |
| `GetEdiFileJobQuery` (single job) | Job detail view | ~20-80/hour | 🟡 |
| `ExistsByChecksumAsync` | Duplicate detection | ~10-100/hour during batch | 🟡 |
| `PrintJob lookup` (consumer) | Print event processing | Variable, burst-heavy | 🟡 |
| Ping endpoints | Health/monitoring | Constant (load balancer) | 🟢 (no cache needed) |

### 4.2 Dashboard/Autocomplete/Dropdown Candidates

| Future Query Type | Module | Cache Fit |
|-------------------|--------|-----------|
| Partner dropdown | EDI / Purchasing | **Perfect** — rarely changes, called on every form |
| Item master autocomplete | Inventory | **Perfect** — high frequency, searchable |
| Status filter options | EDI / Production / Sales | **Perfect** — enum-like, static |
| User role/permission set | Admin | **Perfect** — changes infrequently |
| Warehouse/location list | Inventory | **Perfect** — master data |
| UOM list | Inventory / Purchasing | **Perfect** — reference data |
| Customer list | Sales | **Good** — medium change frequency |
| Vendor list | Purchasing | **Good** — medium change frequency |

---

## 5. Classification — Cache Candidacy

### Category A: Perfect for Long TTL Caching (30–120 minutes)

| Query | Module | Rationale | Suggested L2 TTL | Suggested L1 TTL |
|-------|--------|-----------|-------------------|-------------------|
| `GetPartnerProfileAsync(partnerCode)` | EDI | Master data, changes monthly at most. Loaded repeatedly across pipeline steps. | **120 min** | **15 min** |
| Partner list (future dropdown) | EDI / Purchasing | Reference data for UI forms | **120 min** | **15 min** |
| UOM reference data (future) | Inventory | Static reference | **120 min** | **30 min** |
| System settings (future) | Admin | Configuration data | **60 min** | **10 min** |
| Status enum/filter lists | All | Static/semi-static | **120 min** | **30 min** |

### Category B: Medium TTL Caching (5–15 minutes)

| Query | Module | Rationale | Suggested L2 TTL | Suggested L1 TTL |
|-------|--------|-----------|-------------------|-------------------|
| `GetEdiFileJobsQuery` (paginated list) | EDI | Dashboard query, refreshed periodically. New jobs arrive but users tolerate 5-min staleness. | **10 min** | **2 min** |
| Item master search (future) | Inventory | Autocomplete, high frequency but data changes via imports | **15 min** | **3 min** |
| Customer/vendor lists (future) | Sales / Purchasing | Master data lists, medium change rate | **15 min** | **5 min** |
| Permission/role lookups (future) | Admin | Auth checks on every request, but changes are admin-initiated | **10 min** | **2 min** |

### Category C: Short TTL Caching (30–120 seconds)

| Query | Module | Rationale | Suggested L2 TTL | Suggested L1 TTL |
|-------|--------|-----------|-------------------|-------------------|
| `GetEdiFileJobQuery(jobId)` | EDI | Single job detail — status changes during pipeline processing. Short TTL prevents stale status. | **60 sec** | **15 sec** |
| `ExistsByChecksumAsync` | EDI | Duplicate check — must be relatively fresh, but identical file resubmission within seconds is common. | **30 sec** | **10 sec** |
| Work order status (future) | Production | Real-time-ish shop floor data | **30 sec** | **10 sec** |
| Print job status (future) | Labeling | Status changes rapidly during processing | **30 sec** | **10 sec** |

### Category D: NOT Safe for Caching

| Query | Module | Reason |
|-------|--------|--------|
| `PrintJob lookup` in `QrPrintRequestedConsumer` | Labeling | Write-path consumer — needs latest state for idempotency check. Caching would cause stale status reads leading to duplicate prints or missed retries. |
| Staging row reads (`GetItemMasterRowsAsync`) | EDI | Transient data consumed once during Apply step, then deleted. No reuse value. |
| Financial transaction queries (future) | Costing | Transactional consistency required — stale cost data = wrong financials. |
| Real-time inventory mutations (future) | Inventory | Stock level accuracy is critical for order promising. |
| Outbox message reads | EDI / Labeling | Infrastructure concern, must be transactionally consistent. |

---

## 6. Structured Results Table

| # | Module | Query/Endpoint | Read Pattern | Est. Cost | Cache Candidate | Suggested TTL (L2/L1) | Invalidation Strategy |
|---|--------|---------------|--------------|-----------|-----------------|----------------------|----------------------|
| 1 | EDI | `GetPartnerProfileAsync(code)` | PK lookup, master data | Low DB / **High frequency** | ✅ **A — Long** | 120 min / 15 min | Tag: `edi:partner:{code}` — invalidate on partner update event |
| 2 | EDI | `GetEdiFileJobsQuery(filters, page)` | Paginated + filtered + COUNT | **Medium DB** / High frequency | ✅ **B — Medium** | 10 min / 2 min | Tag: `edi:jobs` — invalidate on any job state change |
| 3 | EDI | `GetEdiFileJobQuery(jobId)` | PK lookup, mutable state | Low DB / Medium frequency | ✅ **C — Short** | 60 sec / 15 sec | Key: `edi:job:{id}` — invalidate on `SaveAsync(job)` |
| 4 | EDI | `ExistsByChecksumAsync(partner, sha)` | Boolean index scan | Low DB / Medium frequency | ⚠️ **C — Short** | 30 sec / 10 sec | Key: `edi:checksum:{partner}:{sha256}` — invalidate on new job |
| 5 | Labeling | PrintJob lookup (consumer) | PK lookup in write-path | Low DB | ❌ **D — No** | — | N/A — write-path idempotency |
| 6 | EDI | `GetItemMasterRowsAsync(jobId)` | Projection, one-time | Medium DB | ❌ **D — No** | — | N/A — transient staging data |
| 7 | Future | Partner dropdown list | Full table scan, small | Low DB / **Very high freq** | ✅ **A — Long** | 120 min / 15 min | Tag: `edi:partners` |
| 8 | Future | Item master autocomplete | Search query, large dataset | **High DB** / Very high freq | ✅ **B — Medium** | 15 min / 3 min | Tag: `inventory:items` |
| 9 | Future | User permissions/roles | Multi-join auth check | Medium DB / **Every request** | ✅ **B — Medium** | 10 min / 2 min | Tag: `admin:permissions:{userId}` |
| 10 | Future | BOM (Bill of Materials) | Deep tree traversal | **Very High DB** | ✅ **A — Long** | 60 min / 10 min | Tag: `production:bom:{itemCode}` |

---

## 7. Top 5 Read-Heavy Hotspots

### 🥇 #1 — `GetPartnerProfileAsync` (EDI Module)
- **Why:** Called in EVERY step of the EDI pipeline (Receive, Parse, Apply). Same partner data loaded 3× per file. With batch imports of 50+ files from the same partner, this is 150+ identical DB queries.
- **ROI:** ⭐⭐⭐⭐⭐ Highest. Near-zero invalidation needed. Master data.
- **Implementation effort:** 15 minutes.
- **Risk if NOT cached:** Unnecessary DB pressure during EDI batch windows. Linear scaling with file volume.

### 🥈 #2 — `GetEdiFileJobsQuery` Paginated List (EDI Module)
- **Why:** Dashboard/monitoring query. Users will poll this repeatedly. Each call = 2 DB round-trips (COUNT + paginated SELECT). With multiple users monitoring EDI imports, this compounds fast.
- **ROI:** ⭐⭐⭐⭐ High. Common dashboard pattern.
- **Implementation effort:** 20 minutes.
- **Risk if NOT cached:** Dashboard becomes sluggish under concurrent users. DB connection pool exhaustion risk during peak.

### 🥉 #3 — Future Permission/Role Lookups (Admin Module)
- **Why:** Authorization checks happen on EVERY authenticated request. Even a simple role check hitting DB = O(requests) queries. This is the single most impactful cache in any ERP system.
- **ROI:** ⭐⭐⭐⭐⭐ Critical when Admin module is built.
- **Implementation effort:** 30 minutes (when module exists).
- **Risk if NOT cached:** Auth becomes the bottleneck. Every endpoint pays a DB tax.

### 🏅 #4 — Future Item Master Autocomplete (Inventory Module)
- **Why:** Every form in an ERP that references items (sales orders, POs, work orders, BOMs, QC inspections) will trigger item search. This is the most frequently called query in any ERP.
- **ROI:** ⭐⭐⭐⭐⭐ Critical when Inventory module is built.
- **Implementation effort:** 30 minutes.
- **Risk if NOT cached:** Full-text search on items table on every keystroke. DB CPU spike.

### 🏅 #5 — Future BOM Tree Lookups (Production Module)
- **Why:** BOM explosions are recursive, joining items → sub-assemblies → raw materials. Costing roll-ups traverse the full tree. These are the most expensive queries in manufacturing ERP.
- **ROI:** ⭐⭐⭐⭐ High when Production + Costing modules are built.
- **Implementation effort:** 45 minutes.
- **Risk if NOT cached:** Single BOM query can take 500ms+. Cost roll-ups become minutes instead of seconds.

---

## 8. Risk Analysis if NOT Cached

| Risk | Likelihood | Impact | Affected Modules |
|------|-----------|--------|-----------------|
| DB connection pool exhaustion during EDI batch windows | **High** | Critical — all modules share the pool | EDI, All |
| Dashboard latency > 2s under concurrent users | **High** | UX degradation, user complaints | EDI, future Sales/Production |
| Auth check becomes system bottleneck | **Certain** (when Admin built) | Every endpoint affected | All |
| Item autocomplete feels sluggish (> 300ms) | **High** (when Inventory built) | Data entry productivity loss | Sales, Purchasing, Production |
| Cache stampede on cold start / Redis failover | **Medium** | Thundering herd to DB | All (mitigated by HybridCache single-flight) |
| Stale partner data causes EDI processing errors | **Very Low** | Partner data changes rarely | EDI |

---

## 9. HybridCache Usage Recommendation Per Module

### 9.1 EDI Module — **Implement NOW** ⚡

```
Priority: IMMEDIATE
Queries to cache: 3 (PartnerProfile, JobList, SingleJob)
Estimated improvement: 60-80% reduction in DB queries during batch windows
```

**Implementation plan:**

```csharp
// 1. Inject ICacheService into GetEdiFileJobQueryHandler
public sealed class GetEdiFileJobQueryHandler(
    IEdiFileJobRepository jobs,
    ICacheService cache) : IRequestHandler<GetEdiFileJobQuery, EdiFileJobDto?>
{
    public async Task<EdiFileJobDto?> Handle(GetEdiFileJobQuery request, CancellationToken ct)
    {
        var cacheKey = CacheKey.Create("edi", "job", request.JobId.ToString());

        return await cache.GetOrCreateAsync(cacheKey, async token =>
        {
            var job = await jobs.GetAsync(request.JobId, token);
            if (job is null) return null;
            return new EdiFileJobDto(/* ... */);
        },
        new CacheEntrySettings
        {
            Expiration = TimeSpan.FromSeconds(60),
            LocalCacheExpiration = TimeSpan.FromSeconds(15),
            Tags = ["edi:jobs", $"edi:job:{request.JobId}"]
        }, ct);
    }
}

// 2. Cache PartnerProfile in Repository (decorator pattern)
public sealed class CachedEdiFileJobRepository(
    EdiFileJobRepository inner,
    ICacheService cache) : IEdiFileJobRepository
{
    public async Task<PartnerProfile> GetPartnerProfileAsync(string partnerCode, CancellationToken ct)
    {
        var key = CacheKey.Create("edi", "partner", partnerCode);
        return await cache.GetOrCreateAsync(key,
            token => inner.GetPartnerProfileAsync(partnerCode, token),
            new CacheEntrySettings
            {
                Expiration = TimeSpan.FromMinutes(120),
                LocalCacheExpiration = TimeSpan.FromMinutes(15),
                Tags = ["edi:partners", $"edi:partner:{partnerCode}"]
            }, ct);
    }
}
```

### 9.2 Labeling Module — **Skip for Now** ⏸️

```
Priority: LOW
Reason: Only write-path operations. Consumer needs fresh data for idempotency.
Future: When print job status query endpoint is added, cache with 30s TTL.
```

### 9.3 Admin Module — **Implement When Built** 🔜

```
Priority: CRITICAL (when module gains auth/permissions)
Pattern: Cache user permissions per userId with 10-min L2, 2-min L1.
Invalidation: Tag-based on role/permission change events.
```

### 9.4 Inventory Module — **Implement When Built** 🔜

```
Priority: HIGH (when item master exists)
Pattern: Cache item lookups and search results. Invalidate on item create/update events.
```

### 9.5 Sales, Production, Purchasing, Costing, Quality — **Design Cache from Day 1** 📋

```
Priority: MEDIUM (when built)
Recommendation: Build every query handler with ICacheService injected from the start.
Use a CachingQueryHandlerDecorator<TQuery, TResult> to avoid boilerplate.
```

---

## 10. Suggested Cache Key Format Pattern

### Convention

```
{module}:{entity}:{identifier}
{module}:{entity}:list:{filter-hash}
```

### Concrete Examples

| Key Pattern | Example | Usage |
|-------------|---------|-------|
| `edi:partner:{code}` | `edi:partner:acme-corp` | Single partner lookup |
| `edi:partners` | `edi:partners` | Partner list/dropdown |
| `edi:job:{id}` | `edi:job:550e8400-e29b-41d4-a716-446655440000` | Single job detail |
| `edi:jobs:list:{hash}` | `edi:jobs:list:a1b2c3d4` | Paginated job list (hash of filter+page) |
| `edi:checksum:{partner}:{sha}` | `edi:checksum:acme:abc123` | Duplicate detection |
| `admin:user:{id}:permissions` | `admin:user:42:permissions` | User permission set |
| `inventory:item:{code}` | `inventory:item:SKU-001` | Single item lookup |
| `inventory:items:search:{hash}` | `inventory:items:search:f5e6d7` | Item search results |
| `production:bom:{item}` | `production:bom:FG-100` | BOM tree for finished good |
| `sales:customer:{id}` | `sales:customer:CUST-001` | Single customer |

### Filter Hash Strategy

For paginated/filtered queries, hash the query parameters to form the key:

```csharp
public static string CreateListKey(string module, string entity, object queryParams)
{
    var json = JsonSerializer.Serialize(queryParams);
    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..8];
    return CacheKey.Create(module, entity, "list", hash);
}
```

---

## 11. Tag-Based Invalidation Strategy

### Tag Hierarchy

```
edi:partners          → Invalidates ALL partner cache entries
edi:partner:acme      → Invalidates single partner
edi:jobs              → Invalidates ALL job lists AND single job entries
edi:job:{id}          → Invalidates single job
admin:permissions     → Invalidates ALL permission caches (nuclear option)
admin:permissions:42  → Invalidates single user's permissions
inventory:items       → Invalidates ALL item caches
```

### Invalidation Triggers

| Event | Tags to Invalidate | Where to Call |
|-------|-------------------|---------------|
| Partner profile created/updated | `edi:partners`, `edi:partner:{code}` | Admin command handler |
| EDI job state changed (`SaveAsync`) | `edi:jobs`, `edi:job:{id}` | `EdiFileJobRepository.SaveAsync` |
| New EDI job created (`AddAsync`) | `edi:jobs` | `EdiFileJobRepository.AddAsync` |
| User role/permission changed | `admin:permissions`, `admin:permissions:{userId}` | Admin command handler |
| Item master updated | `inventory:items`, `inventory:item:{code}` | Inventory command handler |
| BOM modified | `production:bom:{itemCode}` | Production command handler |

### Implementation Pattern — Repository Decorator

```csharp
// Invalidation in the repository's write methods:
public async Task SaveAsync(EdiFileJob job, CancellationToken ct)
{
    await inner.SaveAsync(job, ct);

    // Invalidate related caches
    await cache.RemoveByTagAsync($"edi:job:{job.Id}", ct);
    await cache.RemoveByTagAsync("edi:jobs", ct);
}
```

---

## 12. L1 vs L2 TTL Split Strategy

### Principles

| Principle | Rationale |
|-----------|-----------|
| **L1 (memory) should ALWAYS be shorter than L2 (Redis)** | In multi-instance deployments, L1 cannot be coordinated. Shorter L1 = less cross-instance staleness. |
| **L1 ratio: 10–25% of L2** | Good default. Example: L2=60min → L1=10min |
| **L1 < 5 min for mutable data** | Job statuses, order states — must refresh from L2 frequently |
| **L1 = 15–30 min for reference data** | Partners, UOMs, item master — safe to keep longer |
| **Single-instance deployment: L1 can equal L2** | No staleness risk when there's only one instance |

### Recommended Splits

| Data Type | L2 (Redis) | L1 (Memory) | Ratio | Notes |
|-----------|-----------|-------------|-------|-------|
| Static reference (UOM, status enums) | 120 min | 30 min | 25% | Virtually never changes |
| Master data (partners, items) | 120 min | 15 min | 12.5% | Changes via admin action |
| Auth/permissions | 10 min | 2 min | 20% | Must propagate role changes reasonably fast |
| Dashboard lists (paginated) | 10 min | 2 min | 20% | Users expect reasonable freshness |
| Single entity detail (mutable) | 60 sec | 15 sec | 25% | Status transitions during processing |
| Search results | 15 min | 3 min | 20% | New data eventually appears |
| Duplicate check (checksum) | 30 sec | 10 sec | 33% | Must not miss true duplicates for long |

---

## 13. Advanced Analysis

### 13.1 Repeated Query Logic Across Modules

| Query Pattern | Current Occurrence | Future Risk |
|---------------|-------------------|-------------|
| `GetPartnerProfileAsync` | Called 3× per EDI file (Receive, Parse, Apply) | Will expand to Purchasing, Sales for vendor/customer correlation |
| Item lookup by code | Implicit in EDI staging → Inventory apply | Will be called by Sales, Production, Purchasing, Costing, Quality |
| User/identity resolution | Not yet implemented | Will be called on every authenticated endpoint |

**Recommendation:** Build a `SharedQueryCache` in `FactoryERP.Infrastructure` that pre-warms cross-module reference data (partners, items, users) into L1 on startup.

### 13.2 Cache Stampede Risk Areas

| Area | Risk Level | Mitigation |
|------|-----------|------------|
| EDI batch import (50+ files simultaneously) | **HIGH** | ✅ Already mitigated — `HybridCache.GetOrCreateAsync` is single-flight per key |
| Dashboard refresh by multiple users | **MEDIUM** | ✅ Already mitigated — same mechanism |
| Cold start / Redis failover | **MEDIUM** | ✅ Already mitigated — fail-open fallback in `HybridCacheService` |
| Future: Item autocomplete under load | **HIGH** | ✅ Will be mitigated by same mechanism, but ensure key normalization to maximize hit rate |

**Verdict:** The existing `HybridCacheService` implementation is stampede-proof by design. No additional work needed.

### 13.3 Report/KPI Endpoints (Predictive)

| Future Endpoint | Module | Expected Cost | Cache Strategy |
|----------------|--------|---------------|----------------|
| EDI Processing Dashboard (jobs/hour, error rate) | EDI | Aggregation query | L2=5min, L1=1min, tag: `edi:dashboard` |
| Production KPIs (OEE, throughput) | Production | Multi-table aggregation | L2=5min, L1=1min |
| Inventory valuation summary | Inventory + Costing | Full table scan + join | L2=15min, L1=5min |
| Sales order backlog | Sales | Aggregation + join | L2=10min, L1=2min |
| Quality rejection rate | Quality | Aggregation | L2=10min, L1=2min |

### 13.4 Master Data Shared Across Modules

| Master Data | Owner Module | Consumer Modules | Cache Strategy |
|-------------|-------------|------------------|----------------|
| Partner profiles | EDI | Purchasing, Sales, Quality | Long TTL (120min), tag invalidation |
| Items / SKUs | Inventory | ALL (Sales, Production, Purchasing, Costing, Quality, EDI) | Medium TTL (15min), tag invalidation |
| Customers | Sales | EDI, Quality, Labeling | Medium TTL (15min) |
| Vendors | Purchasing | EDI, Quality, Inventory | Medium TTL (15min) |
| Users / Roles | Admin | ALL | Medium TTL (10min), per-user tag |
| Warehouses / Locations | Inventory | Production, Sales, Purchasing | Long TTL (60min) |
| UOMs | Inventory | ALL | Long TTL (120min) |

### 13.5 Security/Permission Lookups

**Current state:** No auth/permission system implemented. `AddAuthentication()` and `AddAuthorization()` are called in `Program.cs` but no policies or handlers exist.

**When built, this will be the #1 cache priority:**
- Every request → permission check → DB query (without cache)
- Pattern: `admin:user:{userId}:permissions` → `HashSet<string>` of claim values
- Invalidation: On role assignment/revocation event
- TTL: L2=10min, L1=2min
- **Must use L1 aggressively** — this is the only query that runs on literally every request

---

## 14. Actionable Implementation Roadmap

### Phase 1 — Immediate (This Sprint) ⚡

| # | Task | Module | Effort | Impact |
|---|------|--------|--------|--------|
| 1 | Cache `GetPartnerProfileAsync` via repository decorator | EDI | 30 min | 🔥🔥🔥🔥🔥 |
| 2 | Add GET endpoints for `GetEdiFileJobQuery` and `GetEdiFileJobsQuery` | EDI | 30 min | Prerequisite for #3 |
| 3 | Cache `GetEdiFileJobsQuery` in query handler | EDI | 20 min | 🔥🔥🔥🔥 |
| 4 | Cache `GetEdiFileJobQuery` in query handler (short TTL) | EDI | 15 min | 🔥🔥🔥 |
| 5 | Add cache invalidation calls in `EdiFileJobRepository.SaveAsync` and `AddAsync` | EDI | 15 min | Required for correctness |

### Phase 2 — When Modules Mature (Next 2–4 Sprints)

| # | Task | Module | Effort |
|---|------|--------|--------|
| 6 | Build `CachingQueryHandlerDecorator<TQuery, TResult>` MediatR behavior | BuildingBlocks | 1 hour |
| 7 | Cache permission lookups | Admin | 30 min |
| 8 | Cache item master queries | Inventory | 30 min |
| 9 | Cache customer/vendor dropdowns | Sales / Purchasing | 30 min |

### Phase 3 — Performance Optimization (When Scale Demands)

| # | Task | Module | Effort |
|---|------|--------|--------|
| 10 | Pre-warm reference data on startup | BuildingBlocks | 1 hour |
| 11 | Cache BOM tree lookups | Production | 45 min |
| 12 | Cache costing roll-up results | Costing | 45 min |
| 13 | Add cache hit/miss metrics (OpenTelemetry) | BuildingBlocks | 1 hour |

---

## 15. Bonus: MediatR Caching Pipeline Behavior (Recommended Pattern)

Instead of manually injecting `ICacheService` into every query handler, build a pipeline behavior:

```csharp
// Marker interface for cacheable queries
public interface ICacheableQuery<TResponse> : IRequest<TResponse>
{
    string CacheKey { get; }
    CacheEntrySettings? CacheSettings => null;
    IReadOnlyCollection<string>? CacheTags => null;
}

// Pipeline behavior
public sealed class CachingPipelineBehavior<TRequest, TResponse>(
    ICacheService cache)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var settings = request.CacheSettings ?? new CacheEntrySettings();
        if (request.CacheTags is not null)
            settings = settings with { Tags = request.CacheTags };

        return await cache.GetOrCreateAsync(
            request.CacheKey,
            async token =>
            {
                var response = await next();
                return response;
            },
            settings,
            ct);
    }
}
```

Then queries become self-describing:

```csharp
public sealed record GetEdiFileJobsQuery(
    string? PartnerCode = null,
    EdiFileJobStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : ICacheableQuery<GetEdiFileJobsResponse>
{
    public string CacheKey => CacheKey.Create("edi", "jobs", "list",
        $"{PartnerCode}:{Status}:{PageNumber}:{PageSize}".ToLowerInvariant());

    public CacheEntrySettings CacheSettings => new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
        Tags = ["edi:jobs"]
    };
}
```

---

## Summary

| Metric | Value |
|--------|-------|
| Modules analyzed | 9 |
| Implemented query handlers found | 2 (EDI) |
| Immediate cache candidates | **3** (PartnerProfile, JobList, SingleJob) |
| Future high-impact cache candidates | **5** (Permissions, Items, BOM, Customers, Vendors) |
| Existing cache infrastructure readiness | **100%** — fully built, zero consumers |
| Estimated DB query reduction (EDI, immediate) | **60–80%** |
| Highest ROI single item | `GetPartnerProfileAsync` — 15 min work, eliminates 3× redundant DB calls per EDI file |
| Stampede protection | ✅ Built-in (HybridCache single-flight) |
| Fail-open resilience | ✅ Built-in (try/catch fallback) |

**Bottom line:** Your cache infrastructure is production-ready and battle-hardened. The EDI module's `GetPartnerProfileAsync` is the single highest-ROI cache target — implement it today. Then wire in the MediatR `CachingPipelineBehavior` as a foundation for every future query handler across all 9 modules.
