# Findings — Shop Chat Isolation + AI Tab Missing (2026-11-13)

Production verification session after voice-call fixes (`4abae762` deployed, CD `43386658536` SUCCESS).
User report: (1) chat text still visible across different customer accounts on tenant "An Tâm";
(2) "Trợ lý AI" mode tab no longer appears in the storefront chat widget.

---

## ISSUE 1 — Shop chat is a single shared thread per shop (NOT isolated per customer)

### Root cause (verified in code)

`3_CoreHub/Services/RealtimeMessagingService.cs`:

- `GetConversationAsync(subjectType, subjectId)` → `FirstOrDefaultAsync(c => c.SubjectType == subjectCode && c.SubjectId == subjectId)` — **one Conversation row per (Shop, tenantId)**.
- `EnsureConversationAsync` unique key = `(TenantId, SubjectType, SubjectId)` — no participant dimension.
- `2_Gateway/Controllers/RealtimeController.cs:464 GetOrEnsureShopConversationAsync` — every customer/guest calling `GET /api/realtime/conversations?subjectType=Shop&subjectId={tenant}` receives the SAME conversation id.

Consequences already observed in production:

- Customer A and guest B read each other's messages (history returns the whole thread; authorization only checks shop access, not per-customer scoping).
- Switching KhachLink account (logout → different Gmail) still shows the old thread — the thread belongs to the shop, not the account.
- Voice counterpart resolution had to invent "most recent message sender" heuristics because one thread mixes many customers.
- `SendMessage` fan-out (fixed in `7a3a9f8e`) pushes staff replies to EVERY customer participant group — correct only because all customers share one thread; it also means every customer receives every other customer's context implicitly via history.

Identity layer is fine: `Store.razor` resolves `_customerId` (logged-in) → `_customerDeviceId` (guest) correctly; the conversation lookup simply ignores which participant is asking.

### Proposed solution — per-(shop, customer) conversations

Standard marketplace pattern (Zalo OA / Shopee / Messenger): each customer has their own thread with the shop; shop inbox lists one thread per customer.

#### Schema / data (Gateway PG — source of truth)

- `ConversationParticipants` already supports many participants per conversation — reuse.
- Conversation uniqueness must become `(TenantId, SubjectType, SubjectId, CounterpartParticipantId)` — i.e., the customer/guest id becomes part of the conversation key. Options:
  - **A (recommended):** keep `Conversation.SubjectId = shopTenantId`, add a lookup by participant: `GetConversationAsync(type, subjectId, requesterId)` finds the conversation where requester is an active participant with role Customer/Guest. No schema change needed if we query via `ConversationParticipants` join; add an index on `(ConversationId, ParticipantId, IsActive)` if missing.
  - **B:** encode counterpart into a new column/subject variant — more invasive, not recommended.
- Migration concern: existing single shared conversation(s) contain mixed-customer history. Decide: (a) leave legacy thread read-only/archive, new lookups only return per-customer threads; or (b) backfill — split history by sender (lossy, complex). **Recommend (a).**

#### Code changes

1. `RealtimeMessagingService`
   - `GetConversationAsync(type, subjectId, requesterId, requesterRole)` — resolve via participant join.
   - `EnsureConversationAsync` — accept `counterpartParticipantId`; create/find the per-customer thread; shop participant row stays `RoleCode=Shop, ParticipantId=subjectId`.
2. `RealtimeController.GetOrEnsureShopConversationAsync` — pass `identity.UserId` + role; staff still never creates.
3. `GetShopConversations` (staff inbox list) — already lists conversations; will naturally return one row per customer thread. Verify item shape exposes customer id/name for the inbox list (probably needs join to resolve display name — check `ShopConversationItem`).
4. `SendMessage` fan-out — simplify: staff message → push to the customer participant's per-user group of THAT conversation only (no cross-customer leak). Customer message → shared shop group + own echo (unchanged).
5. `MessagingHub.JoinConversation` — group naming already per-user (`msg_Shop_{t}_u_{customerId}`) — verify customer joins only their own thread's groups.
6. Voice: `CallService` counterpart resolution — conversation now has exactly one customer → resolution becomes deterministic; keep most-recent-sender ordering only as tiebreak among staff.
7. UI: `RealtimeChatPanel` needs no change (already keyed by SubjectType/SubjectId + identity). `ShopInbox` conversation list will show N threads — confirm UX (customer name/avatar per row).

#### Tests

- `ShopRealtimeP5Tests`: new cases — two different customer ids on same shop get different conversation ids; history of customer A invisible to customer B; staff sees both threads; staff reply reaches only that thread's customer.
- RV: two customer tokens + one staff JWT on production tenant — verify isolation end-to-end.

#### Risks

- Existing production data: legacy shared thread remains visible to whoever opened it before (grandfathered) — new threads start clean. Acceptable for pilot tenant.
- ShopInbox UX change: owner now sees multiple threads — this is the DESIRED behavior per user requirement ("đang chat với ai thì gọi cho người đó" becomes exact).
- PG index/perf: participant-join lookup per chat open — trivial at current scale.

---

## ISSUE 2 — "Trợ lý AI" tab hidden on storefront widget

### Root cause (verified in code)

`UI.Platform/Components/Ai/AiChatWidget.razor:46`:

```razor
@if (_config?.AiEnabled == true && HumanHandoffAvailable)
```

Mode tabs render ONLY when `widget-config` returns `aiEnabled=true`. When false → widget opens directly in Human mode with no tabs (by design — "service down / flag off → human mode bubble").

`AiEnabled` is resolved server-side from tenant feature flags (`AIChat_Enabled`) + AI service availability/metering. For tenant "An Tâm" it currently evaluates false — most likely the per-shop AI flag is off or the AI/metering check denies.

NOT a regression from the voice/chat work — the tab UI itself works (verified in AiChatWidgetTests 28/28).

### Proposed solution

Phase 2 (diagnose then enable):

1. Check tenant "An Tâm" feature flags: `AIChat_Enabled` (shop feature settings) + platform TenantAi flag — via ShopERP admin or DB.
2. Check widget-config endpoint response for that tenant (HTTP call) — distinguishes flag-off vs service-denied vs metering-deny.
3. If flag simply off → enable for tenant (config change, no code).
4. If metering deny → wallet/topup issue, not code.
5. Optional UX improvement (separate decision): when `aiEnabled=false`, show a small "AI đang bảo trì" hint instead of silently hiding the tab — prevents user confusion. Defer unless requested.

---

## Phase plan

| Phase | Scope | Type | Depends on |
|---|---|---|---|
| **Phase 1** | Issue 1 — per-(shop,customer) conversation isolation | Architectural change (Gateway PG + CoreHub + tests + RV) | Approval of approach A vs B + legacy-thread policy |
| **Phase 2** | Issue 2 — diagnose & restore AI tab for tenant | Config/flag investigation, likely no code | None — can run in parallel |

## Open questions for review

1. Legacy shared thread: archive/read-only (recommended) or attempt history split?
2. ShopInbox list: show customer display name per thread — confirm data source (Gateway PG has customer id; name may need join to customer profile service).
3. Guest device-id threads: keep per-device isolation too (recommended — same mechanism, Guest role).
4. AI tab: is AI intentionally disabled for "An Tâm", or should it be on?
