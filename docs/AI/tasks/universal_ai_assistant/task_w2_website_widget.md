# TASK W2: WEBSITE WIDGET CHO SITE CỦA SHOP

> Wave: W2 · Mode: IMPLEMENT (sau W1 gate)
> Master plan §W2 · SRS v2.0 §7 (feasibility), §6.2 domain verification
> Depends on: W1 (assistant entity, public resolution, hosted page, metering)

## Sản phẩm giao được

> Cùng assistant của W1 xuất hiện dạng floating widget trên website shop sở hữu — khách chat ngay trên trang, không phải rời sang link. Shop copy 1 snippet (hoặc nhờ Vạn An hỗ trợ) là xong.

## Scope IN

### Domain & widget entities
- `WebsiteDomain`: hostname normalized, verification status (unverified/pending/verified/blocked/removed).
- `WidgetInstance`: per-domain deployment, PublicWidgetKey (identifier, không phải secret), configVersion.
- Verification: DNS TXT primary; meta-file secondary; **operator manual review** fallback (bắt buộc cho SME — SRS SA-007).
- Subdomain policy: exact host default; wildcard cần approval.
- Cross-tenant domain collision reject.

### Public config endpoint
- `GET /v1/public/widgets/{publicWidgetKey}/config` — display config + capabilities, validate origin/domain server-side, no secrets, bounded TTL cache + revocation.

### External JS Widget SDK v1
- `loader.js` async, không block LCP; performance budget documented.
- Shadow DOM hoặc isolated iframe (quyết sau compatibility review — ghi trong task).
- Floating launcher + responsive panel; mobile+desktop.
- Configurable: position, color, logo, greeting, language, open/closed state.
- Style/DOM isolation; không break host layout/forms/checkout.
- Error states: network fail retry không mất input; mic KHÔNG xin quyền khi load; explicit gesture only.
- Accessibility: keyboard nav, focus, labels, reduced-motion.
- Kill switch per widget/domain + platform-wide; uninstall cleanup.
- Không đọc page content mặc định; pageUrl chỉ là metadata trừ khi consent (FR-060 gốc).

### Widget chat
- Cùng session/chat contract như link (assistantRef → tenant ctx) — một assistant mọi surface.
- Origin validation: request từ domain verified mới được serve.

### Health check & diagnostics
- Verify: loader reachable, config fetch 200, domain binding, runtime init, test request.
- Phân biệt lỗi CSP/ad-blocker vs credential sai vs server fail; diagnostic ID.
- KHÔNG claim "installed" chỉ vì snippet tồn tại (FR-100).

### Dashboard bổ sung
- Domain management UI: add/verify/remove; widget status; snippet hiển thị (copy).

## Scope OUT

- WordPress plugin, assisted install job system → W4/P3
- Page-context sharing → future (consent design có sẵn)
- Multi-widget per domain phức tạp — 1 widget/assistant/domain là đủ

## Tasks

1. Domain/Widget entities + verification flows (DNS/meta/manual-review).
2. Public config endpoint + caching/revocation.
3. JS SDK: loader + UI + Shadow DOM/iframe + states + a11y.
4. Origin validation middleware.
5. Health checker service + diagnostics.
6. Dashboard domain/widget UI.
7. Tests: unit (verify, normalize, origin), security (spoofing, unverified, cross-tenant), browser (CSP, ad-block sim, mobile/desktop), E2E (verify→embed→chat→pause→remove domain→reject).

## Acceptance / Gate

- [ ] Widget chạy trên ≥1 site test thật đã verify domain + 1 site Vạn An-controlled
- [ ] Unverified domain → reject an toàn; AI Link vẫn hoạt động
- [ ] Domain spoofing tests pass; cross-tenant collision reject
- [ ] Widget failure không làm hỏng host page (AT-011)
- [ ] Mic không xin quyền khi load; explicit gesture only (AT-012)
- [ ] Manual review path hoạt động end-to-end
- [ ] Health checker phân biệt được CSP vs server vs credential lỗi
- [ ] Regression: hosted link page + KhachLink widget không ảnh hưởng

## Risks

- Shadow DOM vs iframe quyết định sai → rework. Mitigate: spike nhỏ đầu wave, test trên 3 host site mẫu.
- CSP strict của host chặn loader → diagnostics + hướng dẫn (fallback link/QR luôn sẵn).
- JS SDK supply chain → versioned release, SRI option, scan (SEC-020).
