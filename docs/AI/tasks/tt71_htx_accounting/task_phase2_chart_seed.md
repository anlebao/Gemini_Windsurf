# TASK CARD: Phase 2 — Chart tài khoản TT 71 (AccountChartSeeder)

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-02 (58fe2da8 — deployed)

## 1. OBJECTIVE

Seed chart TT 71 (PL I — 51 TK cấp 1 + cấp 2/3) vào `AccountCharts` với `Standard=TT71_2024`.

## 2. CHANGES

| File | Change |
|---|---|
| `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` | Thêm `GetTt71Accounts()` (yield đầy đủ theo danh mục master plan §5) + `SeedAsync` gọi `SeedStandardAsync(db, AccountingStandard.TT71_2024, GetTt71Accounts(), ...)` |

### Danh mục TK TT 71 (PL I — verified từ spec)

- **Tài sản:** 111(1111/1112), 112(1121/1122), 121(1211/1218), 131, 132(1321/13211/13212/1322), 133(1331/1332), 136(1361/1368), 138, 141, 151, 152, 154, 156, 157, 211(2111/2113/2114/2117), 212, 214(2141/2142/2143/2144/2147), 229, 242(2421/2422)
- **Nợ phải trả:** 331, 332(3321/33211/33212/3322), 333(3331/3334/3338), 334, 335, 336(3361/3368), 338, 341, 342, 353(3531/3532/3533)
- **Vốn CSH:** 411(4111/4118), 418, 421(4211/4212), 442(4421/4422)
- **Doanh thu:** 511(5111/5112/5113), 512, 521, 558
- **Chi phí:** 611, 612, 642, 658, 659, 911
- **Ngoài bảng:** 001, 002, 003, 004, 005, 006, 007, 008

### Type mapping (theo cấu trúc `AccountChartEntity`)

- Asset: 111-242 (+ contra-asset: 214, 229 → `IsNormalCredit=true`)
- Liability: 331-353 → `IsNormalCredit=true`
- Equity: 411-442 → `IsNormalCredit=true`
- Revenue: 511, 512, 521 (`IsNormalCredit=false` — giảm trừ), 558 → `IsNormalCredit=true`
- Expense: 611, 612, 642, 658, 659 → `IsNormalCredit=false`
- 911: Revenue (như TT 133/99)

## 3. LƯU Ý

- ⚠️ **212 TT 71 = "Tài sản chung không chia"** vs TT 99 = "TSCĐ thuê tài chính" — KHÔNG xung đột (key = Standard+Code) nhưng kiểm tra mọi lookup `GetAccountAsync(code, standard)` truyền đúng standard
- 521 giảm trừ: `IsNormalCredit=false` (giống TT 99)
- Không sửa GetTt133Accounts/GetTt99Accounts

## 4. ACCEPTANCE

- [ ] Seed TT 71 đủ số lượng (đếm trong test)
- [ ] TT 133/99 không đổi
- [ ] Test: `AccountChartSeederTests` mới (đủ TK, type đúng, idempotent)

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests --filter "FullyQualifiedName~AccountChart"
```
