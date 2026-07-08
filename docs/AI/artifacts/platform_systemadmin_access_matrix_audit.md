# Access Matrix Audit — SystemAdmin Cross-Tenant Entry Points

> **Generated:** 2026-07-08 (ANALYZE Phase 1)
> **Policies registered:** 8 (RequireAuthenticatedUser, RequireTenantAccess, OwnerOnly, StoreManagement, GuardOnly, StaffOrAbove, SystemAdmin, LoginRateLimit)
> **SystemAdmin passes:** OwnerOnly, StoreManagement, StaffOrAbove, SystemAdmin, RequireAuthenticatedUser
> **SystemAdmin fails:** GuardOnly, RequireTenantAccess (no tenant_id claim — resolved by impersonation)

## Entry Points (41 total)

### Category A — Admin Pages (SystemAdmin access: required, post-fix: pass)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| A1 | `/admin/users` | `Admin/UserManagement.razor` | `Policy="OwnerOnly"` | ✅ | Policy includes SystemAdmin |
| A2 | `/admin/tenants` | `Admin/TenantManagement.razor` | `Policy="SystemAdmin"` | ✅ | Impersonation page (AM-T7) |
| A3 | `/admin/audit-trail` | `Admin/AuditTrail.razor` | `Policy="SystemAdmin"` | ✅ | F5 fixed |
| A4 | `/admin/permission-groups` | `Admin/PermissionGroupManagement.razor` | `Policy="OwnerOnly"` | ✅ | Policy includes SystemAdmin |
| A5 | `UserController` (6x OwnerOnly) | `Controllers/UserController.cs` | `Policy="OwnerOnly"` | ✅ | |
| A6 | `UserController` (2x StoreManagement) | `Controllers/UserController.cs` | `Policy="StoreManagement"` | ✅ | |
| A7 | `TenantController` | `Controllers/TenantController.cs` | `Policy="SystemAdmin"` | ✅ | |
| A8 | `PermissionGroupController` | `Controllers/PermissionGroupController.cs` | `Policy="OwnerOnly"` | ✅ | |

### Category B — Tenant-Scoped Business (SystemAdmin access: via impersonation, post-fix: pass)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| B1 | Accounting: TrialBalance | `Accounting/TrialBalance.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | Data filtered by impersonated TenantId |
| B2 | Accounting: IncomeStatement | `Accounting/IncomeStatement.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B3 | Accounting: FinancialReports | `Accounting/FinancialReports.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B4 | Accounting: CashFlowStatement | `Accounting/CashFlowStatement.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B5 | Accounting: BalanceSheet | `Accounting/BalanceSheet.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B6 | Accounting: HKDBooks | `Accounting/HKDBooks.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B7 | Accounting: HKDBookDetail | `Accounting/HKDBookDetail.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B8 | Accounting: AccountingIndex | `Accounting/AccountingIndex.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B9 | Accounting: TransactionHistory | `Accounting/TransactionHistory.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B10 | Accounting: ExpenseEntry | `Accounting/ExpenseEntry.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B11 | Accounting: RevenueEntry | `Accounting/RevenueEntry.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B12 | Accounting: PeriodClosing | `Accounting/PeriodClosing.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B13 | Accounting: AccountBalance | `Accounting/AccountBalance.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B14 | EInvoice: ProviderManagement | `EInvoice/ProviderManagement.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B15 | EInvoice: ProviderConfiguration | `EInvoice/ProviderConfiguration.razor` | `Policy="OwnerOnly"` | ✅ after impersonation | |
| B16 | EInvoice: InvoiceManagement | `EInvoice/InvoiceManagement.razor` | `Policy="StoreManagement"` | ✅ after impersonation | |
| B17 | EInvoice: HealthMonitoring | `EInvoice/HealthMonitoring.razor` | `Policy="StoreManagement"` | ✅ after impersonation | |
| B18 | EInvoice: EInvoiceDashboard | `EInvoice/EInvoiceDashboard.razor` | `Policy="StoreManagement"` | ✅ after impersonation | |
| B19 | EInvoice: AlertManagement | `EInvoice/AlertManagement.razor` | `Policy="StoreManagement"` | ✅ after impersonation | |
| B20 | `DashboardController` | `Controllers/DashboardController.cs` | `[Authorize]` | ✅ after impersonation | |
| B21 | `ProductsController` | `Controllers/ProductsController.cs` | `[Authorize]` | ✅ after impersonation | |
| B22 | `OrderWorkflowController` | `Controllers/OrderWorkflowController.cs` | `[Authorize]` | ✅ after impersonation | |
| B23 | `OrdersController` | `Controllers/OrdersController.cs` | `[Authorize]` | ✅ after impersonation | |
| B24 | `SocialCampaignsController` | `Controllers/SocialCampaignsController.cs` | `[Authorize]` | ✅ after impersonation | |
| B25 | `TrialBalancesController` | `Controllers/TrialBalancesController.cs` | `[Authorize]` | ✅ after impersonation | |
| B26 | `IncomeStatementsController` | `Controllers/IncomeStatementsController.cs` | `[Authorize]` | ✅ after impersonation | |
| B27 | `CashFlowStatementsController` | `Controllers/CashFlowStatementsController.cs` | `[Authorize]` | ✅ after impersonation | |
| B28 | `BalanceSheetsController` | `Controllers/BalanceSheetsController.cs` | `[Authorize]` | ✅ after impersonation | |
| B29 | Orders: Detail | `Orders/Detail.razor` | `[Authorize]` | ✅ after impersonation | |
| B30 | Orders: Index | `Orders/Index.razor` | `[Authorize]` | ✅ after impersonation | |
| B31 | Sitemap | `Pages/Sitemap.razor` | `[Authorize]` | ✅ after impersonation | |
| B32 | Home | `Pages/Index.cshtml.cs` | `[Authorize]` | ✅ after impersonation | |

### Category C — RequireTenantAccess (SystemAdmin access: via impersonation, post-fix: pass)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| C1 | ShopsController (3 actions) | `Controllers/ShopsController.cs` | `Policy="RequireTenantAccess"` | ✅ after impersonation | tenant_id claim from impersonation |
| C2 | HKDElectronicInvoiceController | `EInvoice/Controllers/HKDElectronicInvoiceController.cs` | `Policy="RequireTenantAccess"` | ✅ after impersonation | |

### Category D — Operational (SystemAdmin access: by D5 decision, post-fix: pass)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| D1 | Kitchen | `Pages/Kitchen/Index.cshtml.cs` | `Roles="Masterchef,Staff,Manager"` | 🔧 Needs fix → add SystemAdmin | AM-T9 |
| D2 | GuardRedirect | `Pages/GuardRedirect.cshtml.cs` | `Roles="Guard"` | 🔧 Needs fix → add SystemAdmin | AM-T9 |

### Category E — Role String Mismatch (SystemAdmin access: post-fix: pass)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| E1 | ApiKeyController | `Controllers/ApiKeyController.cs` | `Roles="Admin,Owner"` | 🔧 Needs fix → add SystemAdmin | AM-T9 |
| E2 | AuditTrail | `Admin/AuditTrail.razor` | `Policy="SystemAdmin"` | ✅ F5 fixed | |
| E3 | PlatformUserLogin | `Controllers/PlatformUserLoginController.cs` | `[Authorize]` + `[AllowAnonymous]` on Login | ✅ F1 fixed | Login endpoint public, other actions (if any) require auth |

### Category F — Correctly Excluded (no SystemAdmin access)

| # | Entry Point | File | Attribute | Post-Fix Pass? | Notes |
|---|---|---|---|---|---|
| — | GuardOnly policy | (no direct page) | `Policy="GuardOnly"` | ❌ | Correctly excluded |

## Flagged for Fix

| Flag | Entry Point | Issue | Fix | Task |
|---|---|---|---|---|
| FLAG-1 | ApiKeyController | `Roles="Admin,Owner"` missing SystemAdmin | Add `,SystemAdmin` | AM-T9 |
| FLAG-2 | Kitchen | `Roles="Masterchef,Staff,Manager"` missing SystemAdmin | Add `,SystemAdmin` | AM-T9 |
| FLAG-3 | GuardRedirect | `Roles="Guard"` missing SystemAdmin | Add `,SystemAdmin` | AM-T9 |
| FLAG-4 | RequireTenantAccess endpoints | SystemAdmin has no tenant_id before impersonation | Resolved by D1/D2: impersonation adds tenant_id claim | AM-T7/T8 |

## Policy × SystemAdmin Matrix (post-fix expected)

| Policy | SystemAdmin Pass? | Mechanism |
|---|---|---|
| RequireAuthenticatedUser | ✅ | Login provides auth |
| RequireTenantAccess | ✅ after impersonation | Impersonation adds tenant_id claim |
| OwnerOnly | ✅ | Policy includes "SystemAdmin" role |
| StoreManagement | ✅ | Policy includes "SystemAdmin" role |
| GuardOnly | ❌ | Guard only — correctly excluded |
| StaffOrAbove | ✅ | Policy includes "SystemAdmin" role |
| SystemAdmin | ✅ | Policy matches SystemAdmin role |
