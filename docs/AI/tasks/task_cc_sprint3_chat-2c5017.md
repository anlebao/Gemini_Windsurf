# TASK CARD: Community Commerce — Sprint 3 — Chat (Customer ↔ Shipper)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Customer và Shipper chat real-time qua SignalR, message persist DB, chat chỉ mở khi DeliveryTask tồn tại.
- **Nghiệp vụ áp dụng:** UC-07 (Chat) từ requirements spec.
- **Status:** COMPLETE 2026-07-29 — ChatService (8 tests PASS) + ChatHub + 2 chat endpoints + ChatPanel.razor + E2E test. Build 0 errors, 83 community + 39/39 Architecture tests PASS. VPS RV 18/18 PASS.
- **Branch:** `main` (merged — commit `cd1b200f`)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 3 of 7
- **Dependency:** Sprint 2 COMPLETE (DeliveryTask exists, SignalR infrastructure ready)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `2_Gateway/Hubs/ChatHub.cs` — SignalR hub for chat
- `3_CoreHub/Services/IChatService.cs` — interface
- `3_CoreHub/Services/ChatService.cs` — message persistence + retrieval
- `5_WebApps/KhachLink/Services/Http/ChatHttpService.cs` — HTTP client
- `5_WebApps/KhachLink/Components/ChatPanel.razor` — chat UI component
- `6_Tests/VanAn.Core.Tests/ChatServiceTests.cs`
- `6_Testing/e2e-tests/community-chat.spec.ts`

### Files cần MODIFY
- `2_Gateway/Controllers/CommunityController.cs` — add chat endpoints
- `2_Gateway/Program.cs` — DI + ChatHub mapping
- `5_WebApps/KhachLink/Program.cs` — DI for ChatHttpService
- `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — embed ChatPanel
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` — embed ChatPanel

### Files READ ONLY
- `2_Gateway/Hubs/LocationHub.cs` — SignalR hub pattern (Sprint 2)
- `2_Gateway/Hubs/OrderHub.cs` — group management pattern

### Boundary Rules
- Chat chỉ mở khi DeliveryTask tồn tại (active or completed)
- Không AI chatbot — human-to-human only
- Message max 2000 chars
- Conversation 1-per-order (unique index on OrderId)
- No file attachments in PoC (text only)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Chat gating:** API + UI verify DeliveryTask exists before allowing chat
- [ ] **SignalR ChatHub:** join group `chat_{orderId}`, push `ReceiveMessage` event
- [ ] **Message persistence:** All messages saved to DB (Message entity, Sprint 0)
- [ ] **UI Platform:** ChatPanel dùng VanAnButton, VanAnCard — không custom HTML
- [ ] **Auth:** X-Customer-Token — sender must be either ShipperId or CustomerId of Conversation

---

## 5. SUCCESS CRITERIA
- [x] **SC1:** GET `/api/community/chat/conversations/{orderId}` trả chat history
- [x] **SC2:** POST `/api/community/chat/messages` tạo Message + SignalR push
- [x] **SC3:** Chat chỉ hoạt động khi DeliveryTask tồn tại — 403 nếu không
- [x] **SC4:** SignalR ChatHub: send → receive real-time
- [x] **SC5:** ChatPanel UI: message list + input + send button
- [x] **SC6:** Chat history load khi mở panel
- [x] **SC7:** Unit tests ≥6 cases pass (8/8 PASS)
- [x] **SC8:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [x] **SC9:** E2E test: shipper + customer chat (community-chat.spec.ts 8 cases)
- [x] **SC10:** Architecture tests pass (39/39 PASS)
- [x] **SC11:** Message.IsRead update khi đối phương đọc (MarkAsReadAsync)
- [x] **SC12:** Regression: delivery flow vẫn hoạt động (RV 4 regression PASS)

**Branch:** `feature/community-sprint3-chat`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Message entity, Conversation scoping
- `accounting-ui-implementation` — ChatPanel UI
- `build-error-analysis` — SignalR + chat errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `LocationHub.cs` (Sprint 2) — SignalR hub pattern established
  - Fact 2: `Conversation` + `Message` entities exist (Sprint 0)
  - Fact 3: `OrderHub.cs` — group join/leave pattern
  - Fact 4: `CommunityController.cs` (Sprint 1) — controller pattern with X-Customer-Token
  - Fact 5: Conversation has unique index on OrderId (Sprint 0 EF config)
- **Assumptions:**
  - ChatPanel embedded in DeliveryTracking + OrderTracking pages
  - SignalR ChatHub same pattern as LocationHub
- **Open Questions:**
  - Q1: Auto-create Conversation when DeliveryTask created, hoặc lazy create on first message?
  - Q2: Message read receipt — push SignalR event hoặc polling?
- **Recommended Action:** PROCEED — Assumptions (2) < Facts (5), Open Questions (2) < 3
