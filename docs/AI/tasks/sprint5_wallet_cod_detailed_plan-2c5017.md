# Sprint 5 Detailed Plan — Wallet + COD + Settlement

TDD plan (15 test cases), coding plan (3 sessions), wallet ledger spec, COD flow, settlement logic.

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

## 4. TDD PLAN (15 TEST CASES)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetWallet_Empty_ReturnsZero` | Balance=0, no transactions |
| 2 | `GetWallet_WithTransactions_ReturnsBalance` | Balance = last BalanceAfter |
| 3 | `GetWallet_SortsByCreatedAtDesc` | Most recent first |
| 4 | `ConfirmCod_CreatesTransaction` | WalletTransaction exists, Amount=+codAmount |
| 5 | `ConfirmCod_SetsOrderCodCollectedAt` | Order.CodCollectedAt not null |
| 6 | `ConfirmCod_CreatesSettlement` | Settlement WalletTransaction for shop |
| 7 | `ConfirmCod_AlreadyConfirmed_Throws` | Throws on second confirm |
| 8 | `ConfirmCod_NotShipper_Throws` | Throws when caller not DeliveryTask.ShipperId |
| 9 | `ConfirmCod_WrongAmount_Throws` | Throws when amount != Order.CodAmount |
| 10 | `ConfirmAdvance_CreatesTransaction` | WalletTransaction(AdvancePayment, -amount) |
| 11 | `ConfirmAdvance_BalanceGoesNegative` | BalanceAfter < 0 allowed (shipper owes) |
| 12 | `GetBalance_NoTransactions_ReturnsZero` | Returns 0 |
| 13 | `GetBalance_MultipleTransactions_ReturnsLast` | Returns last BalanceAfter |
| 14 | `WalletTransaction_Immutable_NoUpdateMethod` | Reflection: no public update methods |
| 15 | `WalletTransaction_BalanceAfter_ChainCorrect` | Sequence: 0 → +50k → 50k → -30k → 20k |

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

## 6. CODING PLAN — 3 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service + tests | WalletService + 15 unit tests |
| **S2** | Controller + Order method + DI | CommunityController wallet endpoints + Order.MarkCodCollected + DI |
| **S3** | UI + E2E | Wallet.razor + DeliveryTracking COD button + community-wallet-cod.spec.ts |

---

## 7. VPS VERIFICATION (Sprint 5)

| # | Test | Expected |
|---|---|---|
| RV5-1 | Wallet balance | 200 + balance + transactions |
| RV5-2 | Confirm COD | 200 + WalletTransaction |
| RV5-3 | Wallet immutable | DB UPDATE on WalletTransactions → should fail (no code path) |
| RV5-4 | Balance integrity | SUM(Amount) = last BalanceAfter |
| RV5-5 | E2E Playwright | community-wallet-cod.spec.ts PASS |
