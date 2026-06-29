# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. **One-and-only-one:** Mỗi section chỉ tồn tại 1 lần; cấm trùng nội dung.
2. **No contradiction:** Một hạng mục chỉ có 1 trạng thái.
3. **Ground Truth first:** Verify path/branch với codebase trước khi ghi.
4. **Now over History:** Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. **Actionable Next Actions:** Xóa action đã quá hạn/sai bối cảnh.
6. **Stamp every edit:** Cập nhật Section 7 (Last Updated + branch) mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:**
| Module | Vai trò |
|--------|---------|
| `1_Shared` | Domain entities, Value Objects, DTOs |
| `2_Gateway` | YARP reverse proxy + controllers (stateless) |
| `3_CoreHub` | Services, Repositories, EF infrastructure |
| `5_WebApps/ShopERP` | Staff/admin UI (Blazor Server) |
| `5_WebApps/KhachLink` | Customer-facing PWA (Blazor WASM) |
| `UI.Platform` | Shared UI components (VanAButton, VanACard…) |
| `6_Tests / 6_Testing` | Unit, Integration, Architecture, E2E |

**Hard stops không bao giờ vi phạm:**
- Domain layer PURE — no EF, no DbContext, no DataAnnotations.
- `AccountingEntry` 100% immutable (append-only).
- Gateway STATELESS — no DbContext, no business logic.
- KhachLink dùng HTTP via Gateway ONLY — no CoreHub DI trực tiếp.
- ALWAYS dùng UI Platform components, không bypass.

---

## 2. Current Objective

**KhachLink Improvements — 3-wave implementation plan created:**
- Wave 1: PWA Install Fix (Quick Win - 1-2 hours)
- Wave 2: QR Code Scanning (New Feature - 1-2 days)
- Wave 3: Product Personalization Option C Hybrid (Enhancement - 2-3 days)

**Status:** Planning complete, awaiting approval to start Wave 1 implementation.
**Documentation:** `docs/AI/tasks/khachlink_improvements_master_plan.md` + 3 task cards

---

## 3. Current Status

- **Branch:** `main` (verified 2026-06-29)
- **Last commit:** `58f77e6` — docs: Add KhachLink improvements master plan and task cards
- **Build:** `dotnet build VanAn.sln` → 0 errors
- **Tests:** Architecture tests 21/21 PASS; integration tests 144/144 PASS
- **State:** KhachLink improvements plan created, awaiting approval

---

## 4. Next Actions

1. **[Pending Approval]** Review and approve KhachLink improvements master plan (`docs/AI/tasks/khachlink_improvements_master_plan.md`)
2. **[Pending Approval]** Review and approve Wave 1 task card (`docs/AI/tasks/wave1_pwa_install_fix_task_card.md`)
3. **[Pending Approval]** Review and approve Wave 2 task card (`docs/AI/tasks/wave2_qr_scanning_task_card.md`)
4. **[Pending Approval]** Review and approve Wave 3 task card (`docs/AI/tasks/wave3_product_personalization_task_card.md`)
5. **[After Approval]** Start Wave 1: PWA Install Fix (branch: feature/khachlink-wave1-pwa-install)

---

## 5. Active Architecture Decisions (Wave 17-relevant)

| Decision | Lý do |
|---------|-------|
| CustomerToken = `IDataProtector` (không phải JWT) | Tránh library mới, không cần Identity |
| OTP storage = `IMemoryCache` | TTL built-in, không cần migration |
| Tier calc on-the-fly từ PointBalance | Domain không cần biết tier rules |
| `Shop.Latitude/Longitude` trên entity (không phải ShopConfig) | Store Finder cần query địa lý từ DB |
| Push subscription log-only trong W17 | `Customer.PushSubscriptionJson` chờ W18 approve |
| NavMenu mobile = bottom tab bar | UX pattern chuẩn cho mobile PWA |
| `AccountingEntry` immutable, Reversal Entry pattern | Audit trail tài chính bất khả xâm phạm |
| Multi-tenancy `TenantId` filter tại mọi layer | Data isolation per HKD |

---

## 6. History Log

* [2026-06-29] KhachLink Improvements Plan — Created 3-wave implementation plan: Wave 1 (PWA Install Fix), Wave 2 (QR Code Scanning), Wave 3 (Product Personalization Option C Hybrid). Master plan + 3 task cards created. Commit: `58f77e6`. Branch: `main`.
* [2026-06-29] Production BootstrapAdapter Fix — Fixed ButtonSize.Medium NotImplementedException in BootstrapAdapter (changed to return empty string). Committed and pushed to trigger CD pipeline. Commit: `ae18a88`. Branch: `main`.
* [2026-06-29] Wave 17 — KhachLink Retention & Loyalty COMPLETE. Implemented: Customer Identity (OTP login, DeviceId upgrade), Loyalty Dashboard, Order History, Store Finder, PWA bug fixes, NavMenu mobile tab bar, KhachLink Layout dynamic themes. Build 0 errors. Architecture tests 21/21 PASS. Branch: `feature/wave17-khachlink-retention`.
* [2026-06-28] Wave 17 preparation — archived Waves 8–16 to `project_state_archive.md`; 144/144 tests PASS; branch `main`.
* [2026-06-28] CI DI Fix — 144/144 integration tests PASS (commits `ddd31ed`, `884de3b`, `e4fc298`).
* [2026-06-28] Wave 16 verify + Components review — 6/12 components production ready; 6 dead/broken → Wave 17 backlog.
* [2026-06-28] Production 502 fix — ShopERP stale volume + KhachLink `Gateway__BaseUrl` + Nginx reload.
* [2026-06-26] Wave 15 — KhachLink page cleanup + Blazor routing (commit `26abd83`).
* [2026-06-26] Wave 14 — HMAC Request Signing (commit `5462759`, PR #63).
* Waves 0–14 details: `docs/AI/project_state_archive.md`

---

## 7. Maintenance Log

* **Last Updated:** 2026-06-29 — KhachLink improvements plan created (commit 58f77e6); BootstrapAdapter fix for production (commit ae18a88); build 0 errors; architecture tests 21/21 PASS
* **Current Branch:** `main`
* **KhachLink Improvements Plan (2026-06-29):** 3-wave implementation plan created with master plan and task cards. Awaiting approval to start Wave 1 (PWA Install Fix).
