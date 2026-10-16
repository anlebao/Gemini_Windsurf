# TASK CARD: P2.2 — RETENTION 5/10 NĂM + BÁO CÁO TUỔI DỮ LIỆU

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P2.2)
> Branch: `main`
> Status: ✅ **Q4 CHỐT (2026-10-16): chỉ TÀI LIỆU — KHÔNG code (chính sách + hướng dẫn)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **3.3** — đảm bảo dữ liệu kế toán + hóa đơn/chứng từ điện tử **lưu trữ được và tra cứu được trong thời hạn pháp lý**: chứng từ ghi sổ trực tiếp/BCTC năm/sổ kế toán **≥ 10 năm**; tài liệu quản lý điều hành **≥ 5 năm** (Điều 41 Luật Kế toán 2015 + NĐ 174/2016); hóa đơn, chứng từ điện tử **≥ 10 năm** (NĐ 254/2026 — trước đây Điều 16 NĐ 123/2020).
- **Phát hiện review (2026-10-16):** backup infra có (`scripts/backup-db.sh` + docker volumes) nhưng **không có báo cáo tuổi dữ liệu / cảnh báo hạn lưu trữ**; hệ thống append-only (KHÔNG xóa tự động — đúng hướng).
- **Đã xác minh code:** `AuditLog`/`AccountingEntry` immutable (không xóa) · `scripts/backup-db.sh` · `R2CleanupHostedService` (cleanup R2 — không phải retention kế toán) · `ProductionDeploymentService` (backup liên quan deploy).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 5.
- **Execution Mode:** IMPLEMENT (báo cáo + docs) — KHÔNG xóa dữ liệu tự động.
- **Skills (max 3):** `domain-integrity-validation` + `pattern-based-fixing`.

## 3. SCOPE — CHỈ TÀI LIỆU (Q4 2026-10-16)

### 3.1 Chính sách + hướng dẫn (docs)
- [ ] Chính sách lưu trữ 5/10 năm (`docs/legal/` hoặc `docs/operations/`): chứng từ ghi sổ trực tiếp/BCTC năm/sổ kế toán ≥ 10 năm · tài liệu quản lý điều hành ≥ 5 năm (Điều 41 Luật Kế toán 2015 + NĐ 174/2016) · hóa đơn/chứng từ điện tử ≥ 10 năm (NĐ 254/2026).
- [ ] Quy trình sao lưu 2 nơi độc lập (on-premise + cloud — checklist 3.3) + khôi phục định kỳ (drill) — nối `scripts/backup-db.sh` hiện có.

### 3.2 Code — ⏸️ DEFER (khi có nhu cầu)
- [ ] `IRetentionService.GetDataAgeReportAsync` + trang `/admin/data-retention` + cảnh báo 5/10 năm (thiết kế đã có trong master plan §5 P2.2).

### 3.4 Tests
- [ ] Core.Tests: báo cáo tuổi đúng (nhóm entity + min/max + cờ hạn) · isolation tenant · không side-effect xóa.
- [ ] bUnit: trang render + cảnh báo.

## 4. DECISIONS — ✅ Q4 CHỐT (2026-10-16): chỉ tài liệu; code (báo cáo tuổi dữ liệu) defer.

## 5. CONSTRAINTS (governance)

- **TUYỆT ĐỐI không xóa dữ liệu kế toán tự động** (append-only — hard stop) · KHÔNG đụng Domain.cs · Multi-tenancy · UI Platform 100% · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA

- [ ] Chính sách lưu trữ 5/10 năm + quy trình backup 2 nơi/khôi phục trong docs
- [ ] (Khi duyệt) báo cáo tuổi dữ liệu + cảnh báo + tests

## 7. DEFERRED

- Archive tự động (đóng gói dữ liệu > 10 năm ra storage lạnh) · cảnh báo qua email/Telegram · xác thực khôi phục định kỳ tự động (drill)