# TASK CARD: Phase 5 — Crawler worker service (NEW .csproj)

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phase 4 complete (Gateway `/api/v1/crawl/batch` endpoint exists)
> **Status:** ✅ COMPLETE — implemented + deployed 2026-09-21, **API key live + E2E verified 2026-10-01**

## 1. OBJECTIVE

New `7_Tooling/VanAn.Crawler.csproj` worker service. Hybrid adapters: `RestApiAdapter` (config-driven `crawler-sources.json`) + `TrangVangHtmlAdapter` (AngleSharp). HTTP to Gateway only — NO DbContext, NO direct PG access.

## 2. GATES & HARD STOPS

- **🔴 NO new .csproj rule** (`.windsurfrules` Critical Architectural Boundaries) — **User-approved exception**. Document trong commit message + AGENTS.md.
- **Layer boundaries:** Crawler references ONLY `1_Shared` DTOs (or defines own mirror DTOs — preferred, no project reference). NO `3_CoreHub`, NO `IVanAnDbContext`.
- **Architecture test update:** `ArchitectureRulesTests.cs` whitelist `7_Tooling/VanAn.Crawler.csproj`.

## 3. PRE-CONDITIONS

- [x] Phase 4 done — Gateway endpoint exists
- [x] **Open M2** resolved 2026-10-01: curl thật `doanhnghiep.vn` với API key — `GET /api/v1/companies/{mst}` → 200, schema khớp 100% field mapping adapter (`mst, name_vi, address_full, legal_rep_name, status, industry_main_code, province.code`). `status` = `active/suspended/dissolved` (adapter skip suspended/dissolved ✅). Auth qua header `x-api-key` (hoặc `authorization: Bearer`) — adapter đã gửi `x-api-key` ✅. xinvoice.vn không cần thiết (doanhnghiep.vn là primary source).
- [x] **Open O2** resolved: Gateway API key auth cho crawler (check `HmacApiKeyLookupAdapter.cs`)
- [x] `AngleSharp` version trong `Directory.Packages.props` ≥ 7 days old stable

## 4. FILES TO CREATE

| Path | Role |
|---|---|
| `7_Tooling/VanAn.Crawler/VanAn.Crawler.csproj` | `<Project Sdk="Microsoft.NET.Sdk.Worker">`, `<TargetFramework>net8.0</TargetFramework>`. Packages: `Microsoft.Extensions.Hosting`, `System.Text.Json`, `AngleSharp`, `Microsoft.Extensions.Http`. **NO ProjectReference** — standalone, talks to Gateway via HTTP. |
| `7_Tooling/VanAn.Crawler/Program.cs` | `Host.CreateApplicationBuilder` → `AddHostedService<CrawlerCoordinator>()` → `AddHttpClient("gateway")` → `Configure<CrawlerOptions>(config.GetSection("Crawler"))` → `host.Build().Run()`. HTTP endpoint on **port 5010** (correction C3). |
| `7_Tooling/VanAn.Crawler/Options/CrawlerOptions.cs` | `GatewayBaseUrl`, `GatewayApiKey`, `DefaultRateLimitMs`, `MaxBatchSize`, `Sources` list |
| `7_Tooling/VanAn.Crawler/Config/crawler-sources.json` | Config-driven sources. ⚠️ Field mapping values phải match schema API thật (sau M2 verify). Copy to output dir. |
| `7_Tooling/VanAn.Crawler/Adapters/IDataSourceAdapter.cs` | `string Name`, `Task<List<CrawlListingDto>> FetchAsync(CrawlQuery, ct)` |
| `7_Tooling/VanAn.Crawler/Adapters/RestApiAdapter.cs` | Config-driven. `FetchAsync`: build URL from `endpointTemplate`, call API, map fields via `JsonDocument` manual navigation (skip full JSONPath engine — keep simple). Rate limit via `Task.Delay`. |
| `7_Tooling/VanAn.Crawler/Adapters/IHtmlAdapter.cs` + `TrangVangHtmlAdapter.cs` | `IHtmlAdapter : IDataSourceAdapter`. Uses AngleSharp. Rate limit 3-5s. User-Agent `VanAnCrawler/1.0 (+contact@vanan.vn)`. Batch max 50-100 (ToS compliance). |
| `7_Tooling/VanAn.Crawler/Workers/CrawlerCoordinator.cs` | `BackgroundService`. On trigger (HTTP `POST /trigger`): load `crawler-sources.json`, instantiate adapters by type, `FetchAsync`, batch POST to Gateway `POST /api/v1/crawl/batch` with API key auth. Polly retry policy. |
| `7_Tooling/VanAn.Crawler/Dtos/CrawlListingDto.cs` | Mirror of service DTO (kept separate, no shared project): `Name, TaxCode?, Address?, CrawledPhone?, ContactName?, IndustryCode?, SourceSite, SourceUrl, CrawledAt, Lat?, Lng?` |

### MODIFY
| Path | Change |
|---|---|
| `VanAn.sln` | Add `7_Tooling/VanAn.Crawler.csproj` under new solution folder `7_Tooling` |
| `Directory.Packages.props` | Add `<PackageVersion Include="AngleSharp" Version="<verified>" />` |
| `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | Whitelist `7_Tooling/VanAn.Crawler.csproj` — verify no Domain/DbContext reference |

## 5. ACCEPTANCE CRITERIA

- [x] `dotnet build 7_Tooling/VanAn.Crawler.csproj` — 0 errors (standalone)
- [x] `dotnet build VanAn.sln` — 0 errors (solution with new project)
- [x] Crawler has NO ProjectReference to `3_CoreHub` or `1_Shared` (or only DTOs, not Domain) — layer boundary
- [x] Crawler has NO `IVanAnDbContext` injection
- [x] HTTP endpoint on port **5010** (correction C3)
- [x] `crawler-sources.json` field mapping matches real API schema (M2 verified 2026-10-01 — curl thật)
- [x] Architecture test PASS with whitelist
- [x] Manual test: run crawler with mock adapter → POSTs to Gateway

## 6. VERIFICATION

```powershell
dotnet build 7_Tooling\VanAn.Crawler.csproj
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Architecture.Tests
```
Crawler tests deferred to Phase 8.

### E2E PRODUCTION VERIFIED (2026-10-01) — doanhnghiep.vn API key live

API key `dnv_...` đã nhận từ doanhnghiep.vn (2026-10-01). Set `DOANHNGHIEP_API_KEY` trong `/opt/vanan/.env.gateway` (VPS vanan-gateway) → restart crawler container → CD preserve key qua mọi deploy (`cd-multivps.yml` lines 368-369/399).

**Luồng Pending (batch):** trigger 2 MST (`0313143038` + `0302839105-001`) qua `POST /api/v1/crawl/trigger` (activateImmediately=false) → crawler log: `GET companies/{mst}` 200 ×2 → `POST /api/v1/crawl/batch` 200 → **`Crawl complete: imported=2, skipped=0, errors=0`** → PG: 2 `Tenants` Status=5 (Pending) + 2 `CrawlSources` rows. KEPT per user (xem UI).

**Luồng Active (batch-import):** trigger MST `0317777282` (activateImmediately=true) → **`imported=1, activated=[{tenantId, username: owner0317777282, slug: cong-ty-tnhh-tra-sua-79}]`** → PG: tenant Status=1 (Active) + owner User + UserTenants + CrawlSources. **CLEANED UP** (DELETE 4 rows, verify 0 — user duyệt dọn test data).

**Kết luận:** chức năng crawl tenant HOÀN CHỈNH — code không cần sửa gì sau khi có key (field mapping + `x-api-key` header đã đúng từ trước).

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| C3 | Crawler HTTP endpoint port 5010 (NOT 5003 — ShopERP conflict) |

## 8. LEGAL COMPLIANCE

- trangvangvietnam: batch 50-100/run, rate 3-5s, identifiable User-Agent. Document trong crawler README.
- doanhnghiep.vn: 100 req/day free limit — enforce in `CrawlerOptions.MaxBatchSize`.
- Prefer doanhnghiep.vn API over trangvangvietnam scraping (legal source).
