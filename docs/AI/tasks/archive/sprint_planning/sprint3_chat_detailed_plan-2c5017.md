# Sprint 3 Detailed Plan — Chat (Customer ↔ Shipper)

TDD plan (8 test cases), coding plan (3 sessions), ChatHub spec, ChatPanel UI spec.

---

## 1. API SPECIFICATIONS

### 1.1 GET /api/community/chat/conversations/{orderId}
```
Header: X-Customer-Token
Response 200: {
  "conversationId": "guid",
  "orderId": "guid",
  "messages": [
    { "id": "guid", "senderId": "guid", "content": "string", "sentAt": "...", "isRead": true }
  ]
}
Response 403: No active/completed DeliveryTask for this order
Response 404: Order not found
```

### 1.2 POST /api/community/chat/messages
```
Header: X-Customer-Token
Body: { "orderId": "guid", "content": "string (max 2000)" }
Response 200: { "messageId": "guid", "sentAt": "..." }
Response 403: No DeliveryTask, or sender not part of conversation
Response 400: Content empty or > 2000 chars
```

---

## 2. SIGNALR CHATHUB SPEC

### ChatHub.cs (v1.3 — auth via X-Customer-Token query string, NOT [Authorize] JWT)
> **v1.3 CORRECTION:** Codebase hiện `OrderHub.cs` KHÔNG có `[Authorize]` — customer auth là custom `X-Customer-Token` (qua `ICustomerTokenService.ValidateToken`), KHÔNG phải JWT bearer. SignalR `[Authorize]` yêu cầu JWT → sẽ FAIL cho customer. Fix: dùng query string `?customerToken={token}` + custom `IUserIdProvider` + validate trong `OnConnectedAsync`.

```csharp
// v1.3: KHÔNG dùng [Authorize] (JWT) — customer auth là X-Customer-Token (custom)
public class ChatHub : Hub
{
    private readonly ICustomerTokenService _tokenService;

    public ChatHub(ICustomerTokenService tokenService) { _tokenService = tokenService; }

    public override async Task OnConnectedAsync()
    {
        // v1.3: Token qua query string (SignalR client truyền được)
        var token = Context.GetHttpContext()?.Request.Query["customerToken"].ToString();
        if (string.IsNullOrEmpty(token))
            throw new HubException("Missing customerToken");

        var customerId = await _tokenService.ValidateTokenAsync(token);
        if (customerId == null)
            throw new HubException("Invalid customerToken");

        // Store customerId trong Context.Items để dùng cho JoinConversation auth
        Context.Items["CustomerId"] = customerId.Value;
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(string orderId)
    {
        // Verify customer có quyền join (là ShipperId hoặc CustomerId của Conversation)
        var customerId = (Guid)Context.Items["CustomerId"]!;
        // ... query Conversation WHERE OrderId=orderId AND (ShipperId=customerId OR CustomerId=customerId)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{orderId}");
    }

    public Task LeaveConversation(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{orderId}");
}
```

**Client-side connection (KhachLink WASM):**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${gatewayBase}/hubs/chat?customerToken=${customerToken}`)  // v1.3: query string
    .build();
```

**Note:** Cũng áp dụng cho LocationHub (Sprint 2) — cùng pattern `?customerToken={token}`.

**Existing OrderHub inconsistency (v1.3 NEW — tech debt, NOT fix trong Sprint 3):**
- `OrderHub.cs` hiện KHÔNG có `[Authorize]` + KHÔNG validate token → bất kỳ ai cũng join. Tech debt đã tồn tại trước Community Commerce. Sprint 3 KHÔNG fix (out of scope) — ghi nhận vào tech debt ledger.

### Server→Client events
```
ReceiveMessage(messageId: string, senderId: string, content: string, sentAt: string)
MessageRead(messageId: string)
```

### Hub mapping
```csharp
app.MapHub<ChatHub>("/hubs/chat");
```

---

## 3. SERVICE SPECIFICATIONS

### IChatService
```csharp
public interface IChatService
{
    Task<Conversation?> GetOrCreateConversationAsync(Guid orderId);
    Task<Message> SendMessageAsync(Guid orderId, Guid senderId, string content);
    Task<List<Message>> GetHistoryAsync(Guid orderId);
    Task MarkAsReadAsync(Guid messageId);
    Task<bool> HasActiveDeliveryTaskAsync(Guid orderId);
}
```

### ChatService
- `GetOrCreateConversationAsync`: Find Conversation by OrderId. If not exists, get DeliveryTask (active or completed) → get ShipperId + CustomerId → create Conversation.
- `SendMessageAsync`: Verify DeliveryTask exists. Verify sender is ShipperId or CustomerId. Create Message. Save. Publish SignalR ReceiveMessage to `chat_{orderId}` group.
- `GetHistoryAsync`: Verify DeliveryTask exists. Query Messages by ConversationId, sort by SentAt.
- `HasActiveDeliveryTaskAsync`: Check DeliveryTask exists for orderId (any status except Cancelled).

---

## 4. TDD PLAN (8 TEST CASES)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetOrCreateConversation_CreatesIfNotExists` | New conversation created with correct ShipperId/CustomerId |
| 2 | `GetOrCreateConversation_ReturnsExisting` | Second call returns same conversation |
| 3 | `SendMessage_CreatesMessage` | Message with correct content, senderId, sentAt |
| 4 | `SendMessage_NoDeliveryTask_Throws` | Throws when no DeliveryTask for order |
| 5 | `SendMessage_InvalidSender_Throws` | Throws when sender not ShipperId or CustomerId |
| 6 | `GetHistory_ReturnsChronological` | Messages sorted by SentAt ascending |
| 7 | `MarkAsRead_UpdatesIsRead` | Message.IsRead = true |
| 8 | `HasActiveDeliveryTask_NoTask_ReturnsFalse` | Returns false when no DeliveryTask |

---

## 5. UI SPEC — ChatPanel.razor

```
Parameters:
  - OrderId: Guid
  - CurrentUserId: Guid

Layout:
  - Message list (scrollable, max-height 400px)
    - Own messages: right-aligned, blue background
    - Other messages: left-aligned, gray background
    - Timestamp below each message
  - Input row: text input + "Gửi" button (VanAnButton Primary)
  - Disabled state when no DeliveryTask

Behavior:
  - On init: load history via GET /api/community/chat/conversations/{orderId}
  - Connect SignalR ChatHub → JoinConversation(orderId)
  - On ReceiveMessage: append to list, auto-scroll
  - On send: POST message, append to list, SignalR push
  - On unmount: LeaveConversation
```

---

## 6. CODING PLAN — 3 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service + tests | ChatService + 8 unit tests |
| **S2** | Hub + controller + DI | ChatHub + CommunityController chat endpoints + DI |
| **S3** | UI + E2E | ChatPanel.razor + embed in DeliveryTracking/OrderTracking + E2E test |

---

## 7. VPS VERIFICATION (Sprint 3)

| # | Test | Expected |
|---|---|---|
| RV3-1 | Send message | 200 + messageId |
| RV3-2 | Get history | 200 + messages array |
| RV3-3 | SignalR ChatHub | Playwright: connect → send → receive |
| RV3-4 | E2E Playwright | community-chat.spec.ts PASS |
