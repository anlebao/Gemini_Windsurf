# Sprint 5 Detailed Plan — Wallet + COD + Settlement

**STATUS: COMPLETE + VPS VERIFIED (2026-07-30)** | Commit `2c038fc0` | 15 files, +1567/-27 | RV5 34/35 PASS

TDD plan (19 test cases — exceeded original 15 by 4 for shop-confirmed advance flow), coding plan (3 sessions), wallet ledger spec, COD flow, settlement logic.

---

## 1. API SPECIFICATIONS

### 1.1 GET /api/community/wallet
```
Header: X-Customer-Token
Response 200: {
  "balance": 150000,
  "transactions": [
    {
      "id": "guid", "type": "CODCollection", "amount": 50000,
      "balanceAfter": 150000, "description": "COD order #123",
      "relatedOrderId": "guid", "createdAt": "..."
    }
  ]
}
```

### 1.2 POST /api/community/wallet/confirm-cod
```
Header: X-Customer-Token
Body: { "orderId": "guid", "amount": 50000 }
Response 200: { "transactionId": "guid", "balanceAfter": 150000 }
Response 409: COD already confirmed for this order
Response 403: Not the shipper of this order's DeliveryTask
```

### 1.3 POST /api/community/wallet/confirm-advance
```
Header: X-Customer-Token
Body: { "orderId": "guid", "amount": 30000 }
Response 200: { "transactionId": "guid", "balanceAfter": -30000 }
```

---

## 2. SERVICE SPECIFICATIONS

### IWalletService
```csharp
public interface IWalletService
{
    Task<WalletSummaryDto> GetWalletAsync(Guid ownerId);
    Task<WalletTransaction> ConfirmCodAsync(Guid ownerId, Guid orderId, decimal amount);
    Task<WalletTransaction> ConfirmAdvanceAsync(Guid ownerId, Guid orderId, decimal amount);
    Task<decimal> GetBalanceAsync(Guid ownerId);
}
```

### WalletService (v1.4 — extends Sprint 0 base with COD/Advance/Settlement/Reverse + HR-SCALE-3 atomic)
> **v1.4:** Sprint 0 đã có `IWalletService.CreateTransactionAsync` (atomic, HR-SCALE-3). Sprint 5 EXTENDS với COD/Advance/Settlement/Reverse methods. KHÔNG re-implement CreateTransactionAsync.

- `GetWalletAsync`: Query WalletTransaction WHERE OwnerId, sort by CreatedAt desc. Balance = last transaction's BalanceAfter (or 0 if none).
- `ConfirmCodAsync` (v1.4 atomic): Verify Order exists + CodAmount matches + CodCollectedAt is null. **Use base.CreateTransactionAsync (atomic SELECT FOR UPDATE)** → create WalletTransaction(CODCollection, +amount). Set Order.CodCollectedAt. Create settlement record for shop via base.CreateTransactionAsync (Settlement, -amount, shopOwnerId). Save transactionally.
- `ConfirmAdvanceAsync` (v1.4 atomic): Verify Order exists. **Use base.CreateTransactionAsync (atomic)** → create WalletTransaction(AdvancePayment, -amount). Save.
- `ReverseTransactionAsync` (v1.1 NEW): Create WalletTransaction(Reversal, -original.Amount, RelatedTransactionId=original.Id) via base.CreateTransactionAsync. KHÔNG update original.
- `GetBalanceAsync`: Query last WalletTransaction WHERE OwnerId ORDER BY CreatedAt DESC → return BalanceAfter. Or SUM(Amount) if none.
- **HR-SCALE-3 compliance (v1.4):** ALL transaction creates go through base.CreateTransactionAsync which uses `SELECT FOR UPDATE` on last transaction row → atomic BalanceAfter → no race condition (B2 resolved).

---

## 3. COD FLOW SPEC

```
1. Customer places order with PaymentMethod="COD"
2. Order flows: pending → confirmed → preparing → ready → delivering
3. Shipper accepts order (Sprint 1)
4. Shipper picks up from shop (Sprint 2)
   → Optional: shipper confirms advance payment (confirm-advance)
5. Shipper delivers to customer
   → Shipper collects cash from customer
   → Shipper taps "Đã thu COD" in DeliveryTracking page
   → POST /api/community/wallet/confirm-cod
6. System creates:
   → WalletTransaction(CODCollection, +amount, shipper)
   → WalletTransaction(Settlement, -amount, shop) — shop owes shipper
   → Order.CodCollectedAt = now
7. Shipper sees updated wallet balance
8. Shop sees settlement record in their wallet
```

---

## 4. TDD PLAN (19 TEST CASES — ALL PASS)

| # | Test Name | What It Verifies | Status |
|---|---|---|---|
| 1 | `GetWallet_Empty_ReturnsZero` | Balance=0, no transactions | ✅ PASS |
| 2 | `GetWallet_WithTransactions_ReturnsBalance` | Balance = last BalanceAfter | ✅ PASS |
| 3 | `GetWallet_SortsByCreatedAtDesc` | Most recent first | ✅ PASS |
| 4 | `ConfirmCod_CreatesTransaction` | WalletTransaction exists, Amount=+codAmount | ✅ PASS |
| 5 | `ConfirmCod_SetsOrderCodCollectedAt` | Order.CodCollectedAt not null | ✅ PASS |
| 6 | `ConfirmCod_CreatesSettlement` | Settlement WalletTransaction for shop | ✅ PASS |
| 7 | `ConfirmCod_AlreadyConfirmed_Throws` | Throws on second confirm | ✅ PASS |
| 8 | `ConfirmCod_NotShipper_Throws` | Throws when caller not DeliveryTask.ShipperId | ✅ PASS |
| 9 | `ConfirmCod_WrongAmount_Throws` | Throws when amount != Order.CodAmount | ✅ PASS |
| 10 | `ConfirmAdvance_CreatesTransaction` | WalletTransaction(AdvancePayment, -amount) | ✅ PASS |
| 11 | `ConfirmAdvance_BalanceGoesNegative` | BalanceAfter < 0 allowed (shipper owes) | ✅ PASS |
| 12 | `GetBalance_NoTransactions_ReturnsZero` | Returns 0 | ✅ PASS |
| 13 | `GetBalance_MultipleTransactions_ReturnsLast` | Returns last BalanceAfter | ✅ PASS |
| 14 | `WalletTransaction_Immutable_NoUpdateMethod` | Reflection: no public update methods | ✅ PASS |
| 15 | `WalletTransaction_BalanceAfter_ChainCorrect` | Sequence: 0 → +50k → 50k → -30k → 20k | ✅ PASS |
| 16 | `ConfirmAdvanceReceived_CreatesSettlementForShop` | Shop-confirmed advance: Settlement tx for shop | ✅ PASS (NEW) |
| 17 | `ConfirmAdvanceReceived_AlreadyConfirmed_Throws` | Idempotency: second confirmation throws | ✅ PASS (NEW) |
| 18 | `GetPendingAdvances_ReturnsUnsettledAdvances` | Pending queue: shows unsettled, hides settled | ✅ PASS (NEW) |
| 19 | `ReverseTransaction_CreatesReversalEntry` | Reversal tx negates original, links via RelatedTransactionId | ✅ PASS (NEW) |

---

## 5. UI SPEC — Wallet.razor

```
@page "/community/wallet"
- Header: "Ví cộng tác viên"
- Balance card: large number, green if positive, red if negative
- Transaction list:
  - Type icon + description
  - Amount (+green / -red)
  - Timestamp
  - Related order link
- Empty state: "Chưa có giao dịch"
```

### DeliveryTracking.razor additions
```
- If Order.PaymentMethod == "COD":
  - Show "COD: {amount}đ" badge
  - After "Đã giao" button: show "Đã thu COD" button
  - On click: call confirm-cod API → show success toast
```

---

## 6. CODING PLAN — 3 SESSIONS (ALL COMPLETE)

| Session | JIT Planning | Pure Execution | Status |
|---|---|---|---|
| **S1** | Service + tests | WalletService + 19 unit tests | ✅ COMPLETE |
| **S2** | Controller + Order method + DI | CommunityController wallet endpoints + Order.MarkCodCollected + DI | ✅ COMPLETE |
| **S3** | UI + E2E | Wallet.razor + DeliveryTracking COD button + WalletHttpService + NavMenu + 7 integration tests | ✅ COMPLETE (E2E skipped per governance) |

---

## 7. VPS VERIFICATION (Sprint 5) — COMPLETE: 34/35 PASS

| # | Test | Expected | Actual |
|---|---|---|---|
| RV5-1 | Container health | 4/4 healthy | ✅ PASS (gateway, shoperp, khachlink, postgres) |
| RV5-2 | Backend API 401 no-token | 5/5 wallet endpoints 401 | ✅ PASS (wallet, confirm-cod, confirm-advance, pending-advances, confirm-advance-received) |
| RV5-3 | DLL deployment | WalletService methods compiled | ✅ PASS (7/7 in VanAn.CoreHub.dll + 4/4 routes in VanAn.Gateway.dll) |
| RV5-4 | KhachLink page route | /community/wallet 200 | ✅ PASS + 3 regression pages 200 |
| RV5-5 | WASM deployment | Wallet symbols in VanAn.KhachLink.wasm | ✅ PASS (6/6: Wallet, WalletHttpService, ConfirmCodAsync, ConfirmAdvanceAsync, GetWalletAsync, PendingAdvances) |
| RV5-6 | PG schema | WalletTransactions + Orders.CodAmount + CodCollectedAt | ✅ PASS (all columns verified) |
| RV5-7 | Regression Sprint 1-4 | All endpoints still work | ✅ 7/8 PASS (1 pre-existing admin 302 login-redirect) |
| RV5-8 | Gateway logs | No Sprint 5 startup errors | ✅ PASS |

**CD deploy:** Images built 18:32-18:34 UTC, containers created 18:35. Commit `2c038fc0`.
