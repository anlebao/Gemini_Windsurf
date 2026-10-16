# TASK CARD: P2.4 — IFRS (TT 118/2026/TT-BTC) — KHẢO SÁT + ROADMAP

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P2.4)
> Branch: `main`
> Status: ✅ **Q4 CHỐT (2026-10-16): chỉ TÀI LIỆU khảo sát — KHÔNG code**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** chuẩn bị cho **Thông tư 118/2026/TT-BTC** (18/08/2026 — hiệu lực **01/01/2027**) hướng dẫn **đối tượng, phạm vi, thể thức áp dụng Chuẩn mực quốc tế về kế toán (IFRS)** tại Việt Nam — checklist **3.1** (BCTC theo IFRS cho đối tượng áp dụng).
- **Phát hiện review (2026-10-16):** hệ thống hỗ trợ VAS (TT 99/2025, TT 133/2016, TT 71/2024, TT 152/2025) — **chưa có khái niệm IFRS**; BCTC hiện theo mẫu TT 99/2025.
- **Đã xác minh code:** `AccountingStandard` enum (TT99_2025/TT133_2016/TT58_2026/TT71_2024) · BCTC services (BalanceSheet/IncomeStatement/CashFlow/Notes) · `Tenant` có phân loại loại hình (Enterprise_Large...).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 5 (docs).
- **Execution Mode:** REVIEW_ONLY/ANALYZE (khảo sát) — code deferred.

## 3. SCOPE (khảo sát + tài liệu)

### 3.1 Khảo sát
- [ ] Tóm tắt TT 118/2026: đối tượng áp dụng (DN đại chúng? quy mô lớn? công ty con? — theo văn bản), phạm vi (BCTC riêng/hợp nhất), thể thức áp dụng (tự nguyện/bắt buộc, lộ trình).
- [ ] Đánh giá gap VAS ↔ IFRS ảnh hưởng module hiện tại (công cụ tài chính, doanh thu IFRS 15, thuê IFRS 16...).
- [ ] Xác định khách hàng tiềm năng thuộc đối tượng (Enterprise_Large/đại chúng).

### 3.2 Tài liệu
- [ ] `docs/legal/IFRS_118_2026_roadmap.md`: đối tượng + lộ trình + phương án tích hợp (BCTC song song VAS//IFRS, bổ sung chuẩn, mapping tài khoản) — KHÔNG code.

## 4. DECISIONS (cần user)

- Có khách hàng thuộc đối tượng IFRS không? → CÓ: lập roadmap chi tiết + task riêng; KHÔNG: lưu tài liệu, defer.

## 5. CONSTRAINTS (governance)

- KHÔNG code ở phase này · không đụng Domain.cs · không migration.

## 6. SUCCESS CRITERIA

- [ ] Tài liệu khảo sát TT 118/2026 + gap analysis + roadmap
- [ ] (Nếu có đối tượng) task card IFRS riêng được approve

## 7. DEFERRED

- Toàn bộ code IFRS (chờ xác nhận đối tượng + lộ trình user)