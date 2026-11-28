# 07 — PRODUCT OWNER DECISIONS REQUIRED

Decisions that cannot be safely inferred from code or the vision brief. Options + trade-offs + consequences. Ordered by blocking impact.

---

## D1 — Payment strategy (BLOCKS P4 / "transact" loop) 🔴

**Question:** Which payment acceptance path for customer checkout and metering top-ups?

| Option | Pros | Cons |
|---|---|---|
| A. VNPay direct | Domestic coverage, lower fees, refund API | Integration + cert effort; no wallet balance |
| B. MoMo/ZaloPay e-wallets | High adoption VN, QR-native | Per-tx fees; wallet float reconciliation |
| C. Bank-aggregator (e.g., Casso/SePay webhook→auto-confirm) | Cheapest; extends current VietQR+webhook | Weaker guarantee; manual dispute handling |
| D. Hybrid: aggregator now, gateway later | Fast time-to-cash | Two code paths to maintain |

**Consequences:** Until decided, checkout stays VietQR+manual confirm (works for pilot, doesn't scale trust); refund stays accrual-only; AI "place order+pay" impossible.
**Recommendation:** Decide provider before P4-1; audit webhook verification (P0-3) regardless.

## D2 — First external channel (BLOCKS P3) 🔴

**Question:** Which customer channel matters most for pilot tenants?

| Option | Notes |
|---|---|
| Zalo OA + ZNS | Dominant VN channel; OA review required; ZNS paid per msg; fits notification+chat |
| Facebook Messenger | Large SMB presence; app review for pages_messaging; already have FB lead webhook foothold |
| WhatsApp Business API | Growing but lower VN penetration; per-conversation pricing |
| TikTok DM | Young API; commerce integration immature |
| None yet — perfect first-party first | Defer until W4 lands |

**Consequences:** Channel choice determines adapter design, consent model, cost model (per-message fees → metering implications), and unified-inbox scope. Building channel-agnostic abstraction without a real channel risks designing in vacuum.
**Recommendation:** Pick ONE channel for pilot (likely Zalo OA or Messenger), build adapter + unified conversation model against it.

## D3 — AI action autonomy boundary (BLOCKS W4/P2-1)

**Question:** Which AI actions may execute automatically vs require human confirmation?

- Read-only Q&A: already safe.
- Create booking request / hold slot: auto-create draft + notify staff, or auto-confirm?
- Apply discount/voucher: never auto?
- Refund/cancel order: never auto?
- Lead capture (name/phone): auto with consent?

**Consequences:** Defines tool-permission layer, audit requirements, and customer-facing honesty ("AI proposed, staff confirmed"). Vision §20 requires per-action authorization matrix.
**Recommendation:** Default = AI proposes, human confirms for anything financial/inventory-affecting; auto-allowed for info + lead capture with consent.

## D4 — Source-of-truth boundaries (ARCHITECTURE)

**Question:** Confirm authoritative ownership for contested objects:

- Customer identity: currently TWO systems by design (KhachLink Customer vs ShopERP DemoUser). Confirm this stays, or plan convergence?
- Loyalty points: CoreHub PG authoritative; ShopERP SQLite mirror — confirm single-writer = CoreHub?
- Products: ShopERP per-tenant SQLite authoritative; Gateway FeaturedProducts subset — confirm?
- Conversations: new aggregate needed for cross-channel — where does it live (CoreHub vs new service)?

**Consequences:** Wrong answer → dual-write bugs, FK violations (precedent: 2026-07-16 POS bug), sync conflicts.
**Recommendation:** Ratify the existing de-facto ownership into an ADR; design conversation aggregate before P3-1.

## D5 — Data sharing across tenants / ecosystem (COMPLIANCE)

**Question:** What may be shared: anonymized benchmarks? cross-tenant recommendations? HTX member data pooling? Alliance wallet visibility?

**Consequences:** Affects discovery ranking (use other tenants' data?), loyalty alliance scope, analytics products. Law 91/2025 + tenant contracts.
**Recommendation:** Default deny; opt-in per tenant with explicit consent text.

## D6 — Voice/video scope for next release

**Question:** AI voice (Gemini Live S2S) priority vs perfecting human calling (P5/P6 gates)? Video polish vs new surfaces?

**Consequences:** W6 is expensive; human calling already works. AI voice spike is cheap — recommend spike now, build decision after.
**Recommendation:** Run the documented Gemini Live spike (1-2 days) to inform W6; meanwhile finish voice P5 pilot gates.

## D7 — Build vs buy for commodity infrastructure

| Item | Options |
|---|---|
| Workflow builder | Build in CoreHub vs integrate n8n/Temporal-class |
| Channel adapters | Build vs Twilio/CM.com-class aggregators |
| Payment | Direct vs aggregator (D1) |
| Notifications | Keep Brevo/ESMS vs consolidate provider |
| Observability | Build dashboards vs Grafana Cloud/managed |

**Consequences:** Build = control + tenant-cost fit; buy = faster + maintained. Vision §8 says "integrate commodity, own differentiators" — recommend applying it here explicitly.

## D8 — Mobile strategy

**Question:** `4_MobileApps` HRApp/StationApp are MAUI shells; admin PWA exists. Invest in native apps or PWA-only?

**Consequences:** App-store distribution for staff tools vs maintenance cost of 3 platforms.
**Recommendation:** PWA-first; revisit native only if offline/staff-device requirements demand.

## D9 — Marketplace ambition level

**Question:** Is third-party seller marketplace (EC-01..EC-10 full lifecycle) a real roadmap item or aspiration? Affects settlement/dispute/reputation investment (XL).

**Recommendation:** Defer formal marketplace until HTX/community commerce proves demand; keep directory+first-party storefronts as the discovery surface.

## D10 — Pricing/monetization model for AI + ecosystem

**Questions:** `ai.chat.message` credit price? Per-assistant budget defaults? Assistant add-on pricing? Sponsored placement in TimLaThay? Marketplace commission rates?

**Consequences:** Metering rate cards need seeding (P0-6); tenant willingness-to-pay unvalidated.
**Recommendation:** Pilot pricing with founding tenants; instrument usage cost per tenant first (P6-4 feeds back).

## Already-decided (do NOT re-ask — recorded for completeness)

- Gateway Option C routed async delivery · Domain single-source · AccountingEntry immutable · AI assistant host = CoreHub · D1 assistant host · D12 Gemini Live primary (W6) · HTX = platform portal multi-HTX · 2-system customer identity (Q1) · email-first OTP (Brevo 0đ) · kill-switch AND semantics for instance feature gate.
