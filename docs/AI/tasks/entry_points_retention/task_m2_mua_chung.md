# Task M2 — MUA CHUNG (Group-Buy) — PLACEHOLDER

> **Status:** DEFERRED — cần SRS riêng trước khi có task card thật
> **Wave:** M · **SRS:** §7.2.1 ("sẽ implement rất sớm")

## Ghi nhận phạm vi

Group-buy cho portal HTX: nhiều member/khách gom đơn để đạt giá sỉ. Chưa có spec — khi user duyệt, tách thành SRS riêng vì phạm vi lớn (order model, payment split, fulfillment, cooperative member semantics).

## Chỗ đã chừa sẵn trong SRS này

- Portal landing FR-D4: module card "Mua chung — Sắp ra mắt" (W2-2).
- `KhachLinkProfile.Hoptacxa` preset: khi module ship → thêm NavFlag mới (vd `ShowGroupBuy`) — cần DTO + admin + migration nhỏ (nhớ quy trình thêm flag: enum field DTO + `_flagToggles` + preset + client fallback `?? true/false`).

## Việc trước mắt (không phải task)

- [ ] User viết/chốt spec Mua chung (participants, giá, escrow/settlement).
