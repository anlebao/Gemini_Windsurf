# -*- coding: utf-8 -*-
"""Archive completed objectives from project_state.md Section 2/3/4 (pre-TT71)
into project_state_archive.md under an "Archived 2026-10-02" section."""
import io

PATH = 'docs/AI/project_state.md'
ARCH = 'docs/AI/project_state_archive.md'

with io.open(PATH, encoding='utf-8') as f:
    lines = f.read().split('\n')

markers = {}
for i, l in enumerate(lines):
    for n in (2, 3, 4, 5):
        if l.startswith(f'## {n}. '):
            markers[n] = i

assert all(k in markers for k in (2, 3, 4, 5)), markers

sec2_body = lines[markers[2] + 1:markers[3]]
sec3_body = lines[markers[3] + 1:markers[4]]
sec4_body = lines[markers[4] + 1:markers[5]]

# Section 2: keep ONLY the first paragraph (TT 71 objective) + trailing blank line.
nonempty = [i for i, l in enumerate(sec2_body) if l.strip()]
first = nonempty[0]
# keep through the first blank line after the objective paragraph
keep_end = first + 1
while keep_end < len(sec2_body) and sec2_body[keep_end].strip() == '':
    keep_end += 1
keep_end = max(keep_end, first + 2)
new_sec2 = sec2_body[:keep_end]
old_sec2 = sec2_body[keep_end:]

# Section 3: drop everything (replaced with fresh TT 71 status below).
old_sec3 = sec3_body
old_sec4 = sec4_body

new_sec3 = [
    '',
    '- **\u2705 TT 71/2024 K\u1ebe TO\u00c1N HTX \u2014 S1 + S2 DONE (Phase 1+2 `597733ef`+`58fe2da8` \u00b7 Phase 3 `9ef8286c`, 2026-10-02, pushed + CD Multi-VPS SUCCESS):** S1: Gate 5 approved \u2192 `TenantType.HTX=5` + `AccountingStandard.TT71_2024` (append-only) + `Tenant.MarkAsHtx()` + HtxProfileService hook + Gateway backfill (idempotent, non-fatal) + chart TT 71 **89 TK** (`GetTt71Accounts` \u2014 \u0111\u1eb7c th\u00f9 HTX n\u1ed9i/ngo\u00e0i b\u1ed9; off-balance 001-008 defer). S2: `Tt71Templates` m\u1edbi (**B01-HTX** + **B02-HTX** theo PL IV) + nh\u00e1nh `TT71_2024` IncomeStatementService (B02-HTX t\u00e1ch n\u1ed9i/ngo\u1ea1i 511 vs 512/611 vs 612, 521 gi\u1ea3m tr\u1eeb \u0111\u00fang d\u1ea5u) + BalanceSheetService (B01-HTX t\u1ed5ng 150/160/200/300/400/500 + plug M\u00e3 420 + W2 200==500) + FinancialStatementNotesService (**B09-HTX** I-VI) + CashFlowStatement.razor ch\u1eb7n B03 khi tenant HTX. Tests: Tt71TemplatesTests (T2) + T3 + T4 \u2192 **Core.Tests 1872 PASS (+13) \u00b7 guard ALL PASSED \u00b7 build 0 errors \u00b7 CD SUCCESS \u00b7 RV markers PASS**. K\u1ebf ti\u1ebfp: **S3 (Phase 4a phi\u1ebfu thu/chi theo chu\u1ea9n tenant + 4b m\u1eabu in 01-TT/02-TT + 5 UI standard select + auto-map HTX)**.',
    '- **\U0001f4cb TT 71/2024 K\u1ebe TO\u00c1N HTX \u2014 PLAN READY (2026-10-01, `8947a691`):** review spec (.doc extract qua OLE2 piece-table parser) \u2192 m\u00e2u thu\u1eabn base code (enum thi\u1ebfu, chart thi\u1ebfu/th\u1eeba TK, phi\u1ebfu thu/chi + BCTC hardcode DN); master plan 7 phases + 8 task cards + Execution Strategy \u00a711 (4 session S1-S4); Q1-Q5 ch\u1ed1t (PL II c\u00f3 01-TT/02-TT, PL III s\u1ed5 defer, B09 l\u00e0m lu\u00f4n, backfill C\u00d3, m\u1eabu in C\u00d3, kh\u00f4ng \u0111\u1ee5ng menu/flag); extract spec committed `docs/AI/tasks/tt71_htx_accounting/spec/`.',
    '',
]

new_sec4 = [
    '',
    '**TT 71/2024 K\u1ebe TO\u00c1N HTX \u2014 S3 (Phase 4a + 4b + 5) \u2014 session m\u1edbi (khuy\u1ebfn ngh\u1ecb):**',
    '- [ ] **Phase 4a:** Phi\u1ebfu thu/chi account options theo chu\u1ea9n tenant (`RevenueEntry.razor`/`ExpenseEntry.razor`) \u2014 HTX: thu = 511/512/558, chi = 642/658 (+611/612 n\u1ebfu c\u1ea7n); DN/HKD gi\u1eef nguy\u00ean (D6)',
    '- [ ] **Phase 4b:** M\u1eabu in ch\u1ee9ng t\u1eeb PL II \u2014 Phi\u1ebfu thu **01-TT** + Phi\u1ebfu chi **02-TT** (spec `docs/AI/tasks/tt71_htx_accounting/spec/tt71-pl-ii-chung-tu.txt` + `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx`)',
    '- [ ] **Phase 5:** UI ch\u1ecdn chu\u1ea9n + auto-map tenant\u2192standard (IncomeStatement/BalanceSheet/FinancialStatementNotes/CashFlow pages + FinancialReports hub \u2014 HTX \u2192 B01-HTX/B02-HTX/B09-HTX, \u1ea9n B03 link); Q4: kh\u00f4ng \u0111\u1ee5ng menu/flag ngo\u00e0i k\u1ebf to\u00e1n',
    '- [ ] M\u1ed7i phase: build sln + Core.Tests + ShopERP.Tests + guard-check PASS + commit ri\u00eang (commit-per-phase)',
    '',
    '**S4 (Phase 6 + 7) \u2014 sau S3:**',
    '- [ ] Phase 6: T5 (Domain HTX mapping) + T6 (RevenueEntry/ExpenseEntry HTX options) + T7 (UI select auto-map) + T8 (voucher print 01-TT/02-TT) + full suite kh\u00f4ng regress',
    '- [ ] Phase 7: Deploy + RV production 5-layer \u0111\u1ea7y \u0111\u1ee7 (Playwright Gate 3) + docs/state final',
    '',
    '**User-side verifications c\u00f2n m\u1edf (t\u1eeb feature c\u0169 \u2014 chi ti\u1ebft archive `docs/AI/project_state_archive.md`):**',
    '- [ ] MST lookup: user test UI phi\u1ebfu thu/chi th\u1eadt (doanhnghiep.vn card + autofill vendor)',
    '- [ ] Crawl: user crawl th\u1eadt qua `/admin/crawl-trigger` + Playwright L3/L4 + c\u00e2n nh\u1eafc d\u1ecdn 2 Pending test tenants (Qu\u00e1n C\u00e0 Ph\u00ea 666 + C\u1eeda S\u1ed5 2)',
    '- [ ] Loyalty/wallet: Thi Le 01 xem `/community/wallet` (balance 3.840\u0111) + checkout 50k estimate ~50 \u0111i\u1ec3m (rate 0.001/max 10000); \u0111\u00f3ng issue #185/#186/#187 sau verify + comment',
    '- [ ] Notification: RV browser-level tr\u00ean m\u00e1y th\u1eadt (subscribe\u2192push, staff realtime TC-02/03, owner/salesman/shipper push TC-04)',
    '- [ ] Settlement: wallet tx l\u1ecbch s\u1eed sai d\u1ea5u reconcile (TC-08 scope) + TC-07 wallet\u2192accounting bridge (c\u1ea7n Q5)',
    '- [ ] Investigation: `BaseEntity:IMustHaveTenant` \u2192 Order b\u1ecb global tenant filter \u2014 ki\u1ec3m tra path ho\u00e0n th\u00e0nh \u0111\u01a1n cross-tenant trong prod',
    '- [ ] Playwright specs khi c\u00f3 window: `issue-185.spec.ts` + c\u00e1c spec loyalty/notify/crawl',
    '- [ ] \u26a0\ufe0f D\u1ecdn stray repo root: `UserslebaoAppDataLocalTempgit-cred.txt` (nghi credential \u2014 ki\u1ec3m tra/xo\u00e1) + `nul` (reserved name \u2014 `git add -A` fail) + `/tmp/.rvsec` tr\u00ean vanan-shop-a (root-owned)',
    '',
]

# Rebuild project_state.md
new_lines = lines[:markers[2] + 1] + new_sec2 + ['## 3. Current Status'] + new_sec3 + ['## 4. Next Actions'] + new_sec4 + lines[markers[5]:]

# Update header Archived list (line 4)
for i, l in enumerate(new_lines):
    if l.startswith('> **Archived:**'):
        new_lines[i] = l.replace('+ 2026-09-17 \u2014', '+ 2026-09-17 + 2026-10-02 \u2014').replace('+ 2026-09-17 + 2026-09-17', '+ 2026-09-17')
        break

with io.open(PATH, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(new_lines))

# Append to archive
archive_header = [
    '',
    '---',
    '',
    '## Archived 2026-10-02 (from project_state.md Sections 2/3/4 cleanup \u2014 completed objectives + stale next-actions moved; TT 71 current objective kept)',
    '',
]
with io.open(ARCH, encoding='utf-8') as f:
    arch_text = f.read()
if not arch_text.endswith('\n'):
    arch_text += '\n'

arch_blocks = []
if old_sec2 and any(l.strip() for l in old_sec2):
    arch_blocks.append('**SECTION 2 \u2014 PREVIOUS COMPLETED OBJECTIVES (pre-TT 71, 2026-09-17 \u2192 2026-10-01):**\n' + '\n'.join(old_sec2))
if old_sec3 and any(l.strip() for l in old_sec3):
    arch_blocks.append('**SECTION 3 \u2014 PREVIOUS STATUS ENTRIES (pre-TT 71):**\n' + '\n'.join(old_sec3))
if old_sec4 and any(l.strip() for l in old_sec4):
    arch_blocks.append('**SECTION 4 \u2014 PREVIOUS NEXT ACTIONS (2026-09-25 \u2192 2026-10-01, audit; open user-side items consolidated in current Section 4):**\n' + '\n'.join(old_sec4))

with io.open(ARCH, 'w', encoding='utf-8', newline='\n') as f:
    f.write(arch_text + '\n'.join(archive_header) + '\n' + '\n\n'.join(arch_blocks) + '\n')

print('old_sec2 lines:', len([l for l in old_sec2 if l.strip()]))
print('old_sec3 lines:', len([l for l in old_sec3 if l.strip()]))
print('old_sec4 lines:', len([l for l in old_sec4 if l.strip()]))
print('new file line count:', len(new_lines))
