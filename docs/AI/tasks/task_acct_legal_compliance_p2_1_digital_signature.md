# TASK CARD: P2.1 — CHỮ KÝ SỐ KÝ BÁO CÁO TÀI CHÍNH / SỔ SÁCH

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P2.1 — dep: sau P0.2)
> Branch: `main`
> Status: ✅ **Q4 CHỐT (2026-10-16): chỉ TÀI LIỆU khảo sát — KHÔNG code (chờ nhu cầu khách hàng có token)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **3.3** — tích hợp **Chữ ký số** (USB Token / HSM / Cloud CA) để ký điện tử BCTC và sổ sách khi chốt kỳ (Luật Giao dịch điện tử 2023 — hiệu lực 01/07/2024).
- **Phát hiện review (2026-10-16):** không có module chữ ký số; BCTC xuất được (BalanceSheet/IncomeStatement/CashFlow/Notes + In PDF) nhưng không có luồng ký.
- **Đã xác minh code:** xuất PDF qua `vananPrintBill`/in ấn · e-invoice provider xử lý ký phía provider (HĐĐT) · `Owner` (Đại diện pháp luật) có quyền duyệt BCTC (ma trận phân quyền).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 5.
- **Execution Mode:** IMPLEMENT (khung + stub + provider tùy chọn).
- **Skills (max 3):** `accounting-ui-implementation` + `domain-integrity-validation` + `pattern-based-fixing`.

## 3. SCOPE — CHỈ TÀI LIỆU (Q4 2026-10-16)

### 3.1 Khảo sát (docs)
- [ ] Tài liệu khảo sát chữ ký số cho BCTC/sổ sách: Luật Giao dịch điện tử 2023 · yêu cầu checklist 3.3 · các phương án (USB Token / HSM / Cloud CA) · luồng ký BCTC khi chốt kỳ · tác động Services/UI.
- [ ] Thiết kế khung (docs): `IDocumentSigner` interface + `StubDocumentSigner` + provider tùy chọn — để dành khi có khách hàng có token.

### 3.2 Code — ⏸️ DEFER (khi có nhu cầu)
- [ ] `IDocumentSigner`/`StubDocumentSigner`/`Pkcs11DocumentSigner` · nút "Ký số" trên BCTC/sổ sách · lưu bản ký + audit · tests.

## 4. DECISIONS — ✅ Q4 CHỐT (2026-10-16): chỉ tài liệu khảo sát; code defer khi có khách hàng có token.

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs (SignedDocument là DTO) · KHÔNG commit secret/token · Multi-tenancy (bản ký theo tenant) · UI Platform 100% · Playwright DISABLED trong IMPLEMENT · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA

- [ ] Tài liệu khảo sát + thiết kế khung ký (docs)
- [ ] (Khi duyệt) code ký số BCTC + lưu bản ký + audit + tests

## 7. DEFERRED

- Xác thực chữ ký số khi mở lại (verify chain) · ký hàng loạt sổ sách cuối năm · tích hợp HSM Cloud (CA ngoài)