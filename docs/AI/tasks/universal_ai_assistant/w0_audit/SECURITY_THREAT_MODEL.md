# W0: SECURITY THREAT MODEL — Universal AI Assistant public surfaces

> Date: 2026-11-22 · Scope: surfaces MỚI so với hệ thống hiện tại (link slug, external widget domain, assistant entities). Kế thừa SEC-001→024 của SRS v1.0/v2.0.

## Assets cần bảo vệ

- Tenant knowledge (có classification/usage-permission)
- Tenant wallet/quota (Metering)
- Conversation content (personal data)
- Assistant config (greeting/persona — tenant-confidential)
- Platform LLM budget (cost)

## Trust boundaries mới

```text
[Internet/guest] ──► Gateway public facade ──► CoreHub (assistant/link/domain resolve)
                                                │
                                                ▼ signed ctx
                                          vanan-ai (chat)
                                                │
                                                ▼ usage events
                                          vanan-metering
```

## Threats & controls (map tới requirement)

| Threat | Vector | Control | Ref |
|---|---|---|---|
| Tenant spoofing | client gửi tenantId/assistantId giả | Server-side resolution duy nhất (slug/key/host→assistant→tenant); client id chỉ là lookup key, không authoritative | SEC-002, ADR-005 |
| Slug/QR enumeration | đoán link người khác | Slug high-entropy unguessable; rate limit trên resolve endpoint; không lộ tenant trong response | FR-071, SEC-024 |
| Domain spoofing | claim domain người khác | DNS TXT / meta-file / manual review; collision reject; widget chỉ trên verified domain | FR-026, SEC-012/022 |
| Widget key theft | PublicWidgetKey public | Key là identifier không phải secret; config endpoint không trả gì privileged; revoke path | SEC-004 |
| Abuse/cost exhaustion | anonymous spam | session cap 20/h + rate limit policy + assistant budget + QuotaGate Deny + degraded MeterOnly; attribution metadata | SEC-006/025, PRICING doc |
| Prompt injection | guest/page content độc | TenantAI PolicyEngine + AnswerValidator + ThreatSuite T2/T3 đã cover; page context untrusted, opt-in consent | SEC-010 |
| Cross-tenant leak | session/config lẫn | IMustHaveTenant filter; isolation tests T1 pattern; per-assistant scoping | SEC-003 |
| XSS/DOM interference | widget trên host site | Shadow DOM/iframe; sanitize markdown; CSP cho hosted page; không eval AI output | SEC-009/011 |
| SSRF | domain verification fetch, future crawl | Chặn private IP/metadata; validate mọi redirect; size limits | FR-029/030 |
| Secret leakage | JS bundle/QR/logs | Không secret trong client artifacts; scan bundle; QR chỉ chứa URL | SEC-008/020 |
| Session fixation/replay | sessionId đoán | SessionId client-gen nhưng scoped tenant+assistant; history chỉ trả trong session; optional signed token W5 | SEC-005 |
| Operator abuse | assisted install | Scoped jobs, audit, no password collection | FR-103/104 |
| Metering bypass | deny/fail path | Không unmetered LLM; degraded MeterOnly; circuit breaker | NFR resilience |

## Delta vs hiện tại

Hệ thống hiện đã có: anonymous chat + rate limit + session cap + tenant resolution + metering gate + ThreatSuite. **Mới cần thêm:**

1. Slug/key resolution path (lookup oracle — cần rate limit + unguessable).
2. Domain verification flow (SSRF surface mới — scanner/verify fetch).
3. Assistant budget check layer (mới, trên QuotaGate).
4. Attribution metadata mở rộng (channel/assistantRef).
5. Hosted page XSS surface (public HTML render assistant name/greeting — sanitize).

## Privacy (kế thừa §9.3)

- Conversation content = personal data → retention theo TenantAI `RetentionCleanupService` hiện có; không policy mâu thuẫn.
- Legal review Law 91/2025 trước production (PO/legal).
