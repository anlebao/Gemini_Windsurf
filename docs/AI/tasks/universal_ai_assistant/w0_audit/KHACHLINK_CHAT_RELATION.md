# W0 AUDIT: KHACHLINK CHAT RELATION

> Date: 2026-11-22 · Câu hỏi: widget/link mới quan hệ thế nào với chat KhachLink hiện có?

## Evidence

| Thành phần | Vai trò hiện tại |
|---|---|
| `AiChatWidget.razor` (UI.Platform) | Blazor component nhúng trong KhachLink storefront (`Store.razor`, `Home.razor`) |
| `IAiChatClient` + `TenantAiPublicHttpService` | KhachLink → Gateway `/api/public/tenant-ai/*` |
| `PublicTenantAiController` | Resolve tenant theo **storefront host** (KhachLinkInstance→OwnerTenantId) hoặc validated tenantId |
| Session | Client-generated sessionId; history resume qua `/sessions/{sid}/messages` |
| TenantAI | Engine chung — mọi surface đều về đây |
| Metering | `ai.chat.message` Deny + `ai.chat.degraded` MeterOnly — đã áp cho storefront chat |

## Kết luận quan hệ

**Cùng một backend, khác surface context.** Widget/link mới KHÔNG phải hệ thống song song — là generalization:

```text
authorization context hiện tại:   storefront host → tenant
context mới cần thêm:             assistant slug → assistant → tenant
                                   widget key + verified domain → assistant → tenant
```

- Chat engine, metering, knowledge, policy: **dùng chung 100%**, không duplicate.
- `AiChatWidget` Blazor component: tái dùng được cho hosted assistant page (cùng `IAiChatClient` pattern); external JS SDK là port mới nhưng cùng contract.
- Storefront chat hiện tại = "surface `khachlink_storefront`"; assistant link = surface `ai_link`; widget = `website_widget` — phân biệt qua `channel` trong attribution metadata (PRICING_AND_QUOTA_MODEL).
- `AIChat_Enabled` flag hiện gate storefront widget; assistant `Status==Published` sẽ gate mọi surface — cần migration path rõ (C1): flag cũ map sang assistant đầu tiên của tenant.

## Rủi ro cần tránh

- Đừng tạo endpoint chat thứ 2 riêng cho link — generalize resolution trong `PublicTenantAiController` (hoặc controller mới cùng forwarder).
- Đừng để 2 nơi check "AI enabled" lệch nhau — single source: assistant.Status.
- Session history của storefront và link: hiện keyed theo sessionId string — nếu muốn link session khác storefront session là OK (client-gen unique); continuity cross-channel là W5.

## Backward compatibility

- Storefront path phải giữ nguyên hành vi (regression AT-025).
- Tenant chưa có Assistant entity → facade fallback về flag `AIChat_Enabled` hiện có trong giai đoạn chuyển tiếp (W1 migration path).
