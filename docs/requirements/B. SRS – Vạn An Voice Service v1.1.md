# SOFTWARE REQUIREMENTS SPECIFICATION
# VẠN AN VOICE SERVICE v1.1

**Project:** Vạn An Ecosystem
**Module:** Voice Service
**Version:** 1.1 (supersedes v1.0)
**Priority:** P1 – Shared Platform Capability
**Architecture:** WebRTC P2P + SignalR Signaling + Coturn Fallback
**Status:** Proposed – Audit Before Implementation

---

## 0. CHANGES FROM v1.0

| # | Area | v1.0 problem | v1.1 decision |
|---|---|---|---|
| 1 | Call permission model | "caller has permission to contact target" — no system defined; anyone can call anyone | Default-deny. Permission derived from a **verified business context** (both users are participants of the same order/booking) OR an explicit tenant flag `voice_open_calling`. No new permission tables. |
| 2 | Business context spoofing | Client-supplied context shown on the incoming screen, never verified | Context is **server-verified** against existing Gateway records. Unverifiable context ⇒ call rejected with `INVALID_CONTEXT`. |
| 3 | Active-call state storage | "server state is authoritative" but no storage decision | Active state lives **in-memory on the Gateway instance** (single-instance default). A `VoiceCalls` row is created at CREATED time. Gateway restart ⇒ reconciliation marks stale non-terminal calls FAILED. Redis only when Gateway runs > 1 instance (Phase C trigger). |
| 4 | Presence | `SetCallPresence` specified but no semantics | Presence is **derived from SignalR connection lifecycle** — no separate store, no extra infrastructure. Callee with zero connections ⇒ immediate terminal `OFFLINE`, no 30 s wasted ringing. Multi-device: all connections ring, first accept wins. |
| 5 | Cross-tenant semantics | `TenantId` nullable; multi-tenant users ambiguous | All calls are **same-tenant only**. `TenantId` NOT NULL, resolved from the caller session, never from the client. Multi-tenant users call under the tenant context of their current session. |
| 6 | Browser matrix | Android Chrome + desktop only | Added **iOS Safari 17+** (major KhachLink platform) with documented limitations. |
| 7 | State machine | No FAILED transitions, no collision rule, reconnect unbounded | Complete transition table: bounded reconnect (10 s / 2 ICE restarts), call-collision rejection, two-device accept race resolved by first-accept-wins. |
| 8 | Idempotency | "duplicate messages must not create duplicate calls" — no mechanism | **Client-generated `callId` (UUIDv7)** is the idempotency key. Duplicate `StartCall` returns the existing call state. |
| 9 | FR-007 enforcement | Application-level only, racy when scaled | **Partial unique indexes** on active calls per user in PostgreSQL. |
| 10 | Persist timing | Row creation timing undefined | Row created at CREATED. Startup reconciliation marks stale rows FAILED (reason `SERVER_RESTART`). |
| 11 | ICE restart | RECONNECTING state without protocol | Explicit ICE-restart procedure (new offer with `iceRestart: true`), max 2 restarts within 10 s, then FAILED. |
| 12 | Cost model | 10% TURN only, no sensitivity | Sensitivity rows at 10% / 20% / 40% TURN + trigger review at >20%. |
| 13 | Capacity model | Flat 1% concurrency, underestimated peak | Hour-of-day model with peak-hour formula; 10k-merchant peak ≈ 190 concurrent (not 100). |
| 14 | Offline callee | Not handled | New `OFFLINE` terminal state + missed-call callback flow. |
| 15 | Configurable-without-defaults | ICE timeout, max duration, etc. undefined | Concrete defaults everywhere; config only where a real per-tenant variation exists. |
| 16 | Audio policy | Opus named, no constraints | Mono Opus 32–64 kbps target, cap 128 kbps; echoCancellation/noiseSuppression/autoGainControl mandatory. |
| 17 | Call history access | Not defined | Participant-only read + tenant admin via existing tenant authorization; explicit matrix. |
| 18 | Security tests | Missing | Malicious SDP, oversized payloads, signaling fuzz, rate-limit tests added to §16. |
| 19 | Metering supervision | v1.0 had no fee/gating story | Voice usage metered to vanan-metering (`voice.call`, `voice.minute`), wallet-based deny gate, per-tenant admin kill-switch, fail-open — §14. |
| 20 | Independent module | v1.1 draft assumed a "modular component inside Gateway" with no new deployable service | Architecture changed to an independent, reusable module (`10_Voice/vanan-voice`) mirroring the Metering / Vạn An AI pattern — own solution, own SignalR hub, own DB, client by contract. First integration: **storefront chat (KhachLink)**. |

Design guardrail throughout v1.1: **every new requirement must map to either (a) an existing Vạn An system, (b) a row in the two new tables, or (c) a client-side local behavior. Nothing else is allowed in v1.1.**

---

## 1. EXECUTIVE SUMMARY

### 1.1. Purpose

Build a reusable, low-cost, peer-to-peer voice communication service for all Vạn An ecosystem applications.

The service supports one-to-one audio calls between authenticated users of the same tenant, without a media server for normal calls.

### 1.2. Business Objectives

- Enable direct communication between customers, merchants, employees, CTVs and cooperative members **within an established business relationship** (order, booking, or explicit tenant opt-in).
- Reuse existing Gateway, authentication, tenant and CoreHub infrastructure.
- Minimize infrastructure expenditure and operational complexity.
- Support gradual scaling from 100 to 10,000 merchants.
- Provide a reusable service instead of implementing voice independently in each application.
- Deliver it as an **independent, attachable module** (Metering / Vạn An AI pattern): any application integrates via a small client + auth token, with no host-code coupling.
- **First integration priority: voice inside the storefront chat** (customer ↔ merchant, KhachLink).
- Monetizable when needed: voice usage is metered through the existing Phí nền tảng (vanan-metering) with wallet-based gating (§14).

### 1.3. Core Principles

1. P2P first, TURN only when necessary.
2. No audio streaming through CoreHub or Gateway.
3. No mandatory SFU or SIP infrastructure in v1.1.
4. Reuse existing authentication and tenant authorization; **never duplicate them**.
5. One new independent module (`10_Voice/vanan-voice`), justified by reuse across applications — mirroring the Metering / Vạn An AI pattern. No other new services.
6. **Default-deny calling**: a call must be justified by a verified business context or an explicit tenant opt-in.
7. Fail safely when voice connectivity is unavailable; the caller is told the truth (offline, busy, failed) immediately.
8. Do not break existing Chat, Booking, ShopERP or KhachLink functionality.
9. Metering failure never breaks calls (fail-open, §14).
10. Attachable by contract, not by shared code — integration = SDK + auth token.

---

## 2. SCOPE

### 2.1. In Scope

- One-to-one audio calls (same tenant).
- Outgoing call invitation, with idempotency (client `callId`).
- Incoming call notification (all connected devices of the receiver).
- Accept and reject (first accept wins).
- Cancel outgoing calls.
- Busy, offline and unavailable states.
- Ring timeout (30 s).
- Call collision handling (both parties call each other).
- Mute and unmute microphone.
- End call.
- Connection status and network-change recovery (ICE restart, bounded).
- P2P connectivity using WebRTC.
- STUN/TURN ICE configuration with short-lived credentials.
- Call history (participant-visible).
- Call event logging (audit, no audio).
- Metering supervision: voice usage events + wallet-based feature gating + admin kill-switch (§14).
- Attachable client for host applications; **first integration: voice inside the storefront chat (KhachLink, §9.2)**.
- Tenant-aware authorization.
- Web/PWA on Android Chrome, desktop Chrome, Edge, iOS Safari 17+.
- Network failure handling and reconnection.
- Basic monitoring and performance metrics.

### 2.2. Out of Scope

- Group voice calls.
- Video calls.
- PSTN telephone number calling.
- SIP trunking.
- Call recording.
- AI voice transcription.
- AI voice agents.
- Conference rooms.
- Automatic call distribution.
- Advanced call center functionality.
- Native push-to-answer (a phone app that rings when the browser is killed).
- Mid-call wallet enforcement — a live call is never cut because credits deplete; usage is deducted after the call (§14).

These features must not be introduced as hidden dependencies in v1.1.

---

## 3. SYSTEM ARCHITECTURE

### 3.1. Logical Architecture

Existing ecosystem:

- CoreHub: PostgreSQL and business data.
- Gateway: API and SignalR (single instance by default; horizontal scale is a Phase-C trigger).
- ShopERP: Merchant operations.
- KhachLink: Customer application.
- TimLaThay: Local commerce discovery.
- Booking: Appointment and service booking.
- vanan-metering: independent metering service (Phí nền tảng) — the pattern this module follows.
- vanan-ai: independent AI service — same pattern.

New logical module:

- Voice Service (`10_Voice/vanan-voice`) — an **independent, reusable module** following the vanan-metering / vanan-ai pattern: own solution, own SignalR hub, own database (SQLite first), HTTP contract for admin/integration. Host applications attach it via the reusable SDK + the existing Vạn An token; no voice code lives in Gateway.

### 3.2. Communication Architecture

```
Application A                     Application B
    |                                 |
    | <----- HTTPS / SignalR -------> |   signaling (SDP, ICE, events)
    |                                 |   via the Voice Service hub
    | <========== WebRTC audio =====> |   media (P2P, DTLS-SRTP)
    |                                 |
    | <====== TURN relay (fallback) =>|
    |                                 |
Voice Service (10_Voice/vanan-voice)
  - own SignalR hub, active-call state, own DB
  - validates the existing Vạn An token (shared scheme)
  - issues TURN credentials; reports usage to vanan-metering
```

Invariant: **signaling and media are strictly separated.** Media never traverses Gateway, the Voice Service, CoreHub, or vanan-metering. Host apps attach via the SDK + token; Gateway itself is unchanged.

### 3.3. Component Responsibilities

**A. Voice Service Module (`10_Voice/vanan-voice` — new independent module, Metering / Vạn An AI pattern)**

- Expose its own SignalR hub; authenticate connections by validating the existing Vạn An token scheme (shared signing keys) — no duplicate auth system.
- Resolve the real user ID and tenant context from the validated token claims; never trust client-supplied identity.
- Receive call commands; validate `callId` idempotency, payload limits, rate limits.
- Route invitations to every active connection of the intended user.
- Exchange SDP offers/answers and ICE candidates between the two participants only.
- Enforce the permission model (§4 FR-002) and verify business context against existing records.
- Hold active-call state in memory; persist lifecycle rows to its own database (§7).
- Deliver call-state events; enforce tenant and user permissions.
- Enforce the metering gate (wallet check) and ingest voice usage events (§14).

**Gateway (existing, unchanged):** issues the Vạn An tokens the Voice Service validates; host applications keep using Gateway for their own flows. No voice logic is added to Gateway.

**B. WebRTC Client (Reusable Voice SDK)**

- Request microphone access (only on explicit user action).
- Create `RTCPeerConnection`; exchange SDP; exchange ICE candidates.
- Establish audio transmission (mono Opus, echo cancellation on).
- Handle mute/unmute; monitor connection state.
- Perform bounded ICE restart on network change.
- Close tracks and connections when calls end.

**C. Coturn**

- Provide STUN functionality and TURN relay when direct P2P is unavailable.
- UDP primary; TCP/TLS fallback where supported.
- Temporary authenticated credentials issued by the Voice Service (Coturn TURN REST API).
- Bandwidth and allocation limits per credential.

**D. Voice Service Database (own, SQLite first)**

- Store call records and lifecycle events (`voice_calls`, `voice_call_events`).
- Support tenant-aware reporting.
- Retain audit metadata (including SignalR connection IDs for forensics).
- Never store raw audio, SDP payloads, or ICE histories.

### 3.4. State Storage Decision (explicit)

- **Active-call state**: in-memory `ConcurrentDictionary` on the Voice Service instance, keyed by `callId`, with per-user active-call indexes.
- **Durability**: a `voice_calls` row is inserted at CREATED time. Terminal state is written at call end. On Voice Service startup, a reconciliation step marks any non-terminal row older than 5 minutes as FAILED (`SERVER_RESTART`).
- **Scale-out (Phase C only)**: when the Voice Service is horizontally scaled, active state moves to Redis and SignalR uses a Redis backplane. This is a defined, bounded change; it is not built in v1.1.

---

## 4. FUNCTIONAL REQUIREMENTS

### FR-001: User Authentication

Every calling user must be authenticated.

Requirements:

- Reuse existing Vạn An authentication: the Voice Service validates the same token scheme used by Gateway (shared signing keys) — no duplicate auth system.
- Resolve the actual user ID from the authenticated session; never trust a client-supplied caller ID.
- Verify account status.
- Resolve the **tenant context from the validated token claims** (users belonging to multiple tenants call under the tenant of the session that initiated the call).
- Verify tenant membership of both participants.

### FR-002: Call Initiation (with permission model)

An authenticated user can initiate a one-to-one voice call only if a permission rule passes.

Input (client → server):

| Field | Required | Source of truth |
|---|---|---|
| `callId` | Yes | Client-generated UUIDv7 — idempotency key, never reused across calls |
| `targetUserId` | Yes | Server resolves the user record |
| `contextType` | No | `order` \| `booking` \| `chat` \| null |
| `contextReferenceId` | No | ID of the order/booking/chat record (verified server-side) |
| `displayName` / avatar | — | **Never accepted from client**; resolved server-side |

Permission rules (server-side, evaluated in order):

1. Caller ≠ target.
2. Both users belong to the caller's session tenant.
3. Neither user has an incompatible active call (FR-007).
4. Context supplied? →
   - Yes: the referenced record must exist **and both users must be participants** of it. Verification happens against existing records (orders, bookings, chat conversations). Fail ⇒ `INVALID_CONTEXT` (terminal).
   - No: allowed **only if** the tenant has `voice_open_calling` enabled (tenant setting, off by default).
5. Caller is not blocked by the target (if a block list exists in a future phase; none in v1.1).
6. Metering gate (§14): the tenant's voice feature must be enabled (admin kill-switch) AND, when the `voice.*` rate card is Deny-enforced, the wallet check must pass. Fail ⇒ terminal FAILED (`FEATURE_DISABLED` / `METERING_DENIED`) before ringing.

Behavior:

- Insert `voice_calls` row with status CREATED immediately after validation passes.
- If the target has **zero active SignalR connections** ⇒ terminal `OFFLINE` immediately (no ringing); the caller may send a missed-call notification (FR-014).
- Otherwise transition to RINGING and deliver the invitation to all active connections of the target.
- Duplicate `StartCall` with the same `callId` ⇒ return the current call state, never create a second call.
- If target is actively calling the caller ⇒ reject with `CALL_COLLISION` (FR-013).

Return: `CallId`, `Status`, `InvitationDeliveryResult` (RINGING | OFFLINE); gated calls return terminal FAILED with reason (`METERING_DENIED` | `FEATURE_DISABLED`).

### FR-003: Incoming Call

Every connected device of the receiver receives:

- Caller display name and avatar (resolved server-side).
- Verified business context, when present (e.g., "Cuộc gọi liên quan đơn hàng #123").
- Accept button; Reject button.

Rules:

- The incoming payload must not expose data outside the verified context.
- If the context was not verified, no context text is shown at all.

### FR-004: Call Acceptance

When the receiver accepts (from any of their devices):

- Verify the invitation is still valid (state RINGING, not cancelled/timed out).
- Mark the call ACCEPTED and **invalidate all other device invitations** — the first accept wins; other devices receive `CallAccepted` and clean up locally.
- Begin SDP offer/answer exchange, then ICE, then audio.
- Only one receiver may accept a given invitation (enforced by server state, and by the partial unique index at DB level).

### FR-005: Call Rejection

- The receiver can reject an incoming call; the caller receives `CallRejected`.
- Call ends in terminal REJECTED.

### FR-006: Call Cancellation

- The caller can cancel an unanswered outgoing call (state RINGING only).
- The invitation becomes invalid immediately; state CANCELLED (terminal).

### FR-007: Busy State

- **One active call per user** — enforced in application state and by partial unique indexes on `voice_calls` (caller and receiver).
- A second incoming call to a busy user ⇒ `CallBusy` to the second caller (terminal BUSY).
- A user already in a call who calls someone ⇒ their own `StartCall` fails with `CALLER_BUSY` (terminal).
- No call waiting in v1.1.

### FR-008: Call Timeout (concrete defaults)

| Timeout | Default | Behavior on expiry |
|---|---|---|
| Ring timeout | 30 s | MISSED (terminal) |
| SDP negotiation | 20 s | FAILED (`NEGOTIATION_TIMEOUT`) |
| ICE connection | 15 s | FAILED (`ICE_TIMEOUT`) |
| Network recovery window | 10 s (max 2 ICE restarts) | FAILED (`RECONNECT_FAILED`) |
| Maximum call duration | 120 min | Server sends `CallEnded` (`MAX_DURATION`) |

These values are server-side configuration with the defaults above. Clients receive the current values from the server at call start (one field in the `IncomingCall`/`CallCreated` payload) — no client-side hardcoding.

### FR-009: Mute / Unmute

- Local microphone mute/unmute; mute never terminates the call.
- Mute state is local only (not propagated to the peer).

### FR-010: End Call

- Either participant ends the call; both clients receive `CallEnded(reason)`.
- Server persists the final state and duration.

### FR-011: Call History

Stored per call:

- Call ID, caller ID, receiver ID, tenant ID, application source.
- Verified business context reference (type + ID), when present.
- Started / accepted / ended timestamps, duration.
- Termination reason; connection type (P2P | TURN); final status.
- SignalR connection IDs of both participants (for abuse forensics).

**Never stored:** SDP payloads, ICE candidate histories, audio data.

Access matrix:

| Actor | Can read |
|---|---|
| Call participant | Their own call records |
| Tenant admin (via existing tenant authorization) | All call records of their tenant |
| Anyone else | Nothing |

### FR-012: Connection Quality

Monitor per call (client-side, aggregated server-side with privacy controls):

- ICE connection state, peer connection state, selected candidate type.
- RTT, packet loss, jitter where available; audio interruptions.
- Connection duration; establishment time.

Aggregate metrics only — raw per-call quality reports are dropped after aggregation (retention: 30 days).

### FR-013: Call Collision

- If A calls B while B's own active call toward A exists (B already dialed A and A's call arrives), the later `StartCall` is rejected with `CALL_COLLISION` (terminal, no row inserted).
- Client UX: "Hai bên đang gọi nhau" + auto-retry suggestion — caller can redial after B's outgoing call ends. **No call-merging logic in v1.1.**

### FR-014: Offline Callee & Missed Calls

- Callee offline (no active connections) ⇒ immediate `OFFLINE` result; no ringing.
- Caller may request a **missed-call notification** (`SendMissedCallNotification(callId)`), stored in `voice_call_events`; the callee sees it on next app open (client reads its own missed-call events; no push dependency).
- Explicitly **not** claimed: ringing a terminated browser/OS (see §9.4).

---

## 5. CALL STATE MACHINE

States: `CREATED`, `RINGING`, `ACCEPTED`, `NEGOTIATING`, `CONNECTED`, `RECONNECTING`, `ENDED`, `REJECTED`, `CANCELLED`, `MISSED`, `BUSY`, `OFFLINE`, `CALL_COLLISION`, `FAILED`.

Transitions (complete):

```
StartCall validated (target connected)      StartCall validated (target offline)
CREATED ───────────────────────────────► RINGING        CREATED ──► OFFLINE (terminal)
  │
  ├──► MISSED        (ring timeout 30 s)
  ├──► CANCELLED     (caller cancels in RINGING)
  ├──► REJECTED      (receiver rejects)
  ├──► BUSY          (receiver already in a call)
  └──► ACCEPTED ──► NEGOTIATING ──► CONNECTED
                                        │
                                        ├──► ENDED           (either party, or MAX_DURATION)
                                        └──► RECONNECTING ──► CONNECTED (ICE restart OK)
                                                              └──► FAILED (10 s / 2 restarts exceeded)

NEGOTIATING ──► FAILED   (SDP/ICE timeout, negotiation error)
CONNECTED ────► FAILED   (transport fatal error, not recoverable)
ACCEPTED ─────► FAILED   (negotiation never completed, 20 s)
RINGING ──────► FAILED   (server-side error, e.g., participant vanished)
ANY non-terminal ──► FAILED (SERVER_RESTART, via startup reconciliation)
```

Rules:

- Terminal states never transition back to active states.
- Transitions are idempotent (re-applying an event in the same state is a no-op).
- Duplicate `StartCall` (`callId` seen before) returns current state.
- Server state is authoritative; client state is provisional until confirmed.
- No call may remain non-terminal longer than the max timeout (120 min) — enforced by reconciliation and by the max-duration timer.
- `StartCall` may terminate immediately as FAILED (`METERING_DENIED` | `FEATURE_DISABLED`) before reaching RINGING (§14).

---

## 6. SIGNALR API DESIGN

### Client-to-Server

| Method | Payload (validated) |
|---|---|
| `StartCall` | `callId, targetUserId, contextType?, contextReferenceId?` |
| `AcceptCall` | `callId` |
| `RejectCall` | `callId` |
| `CancelCall` | `callId` |
| `SendOffer` | `callId, sdp` (≤ 64 KB) |
| `SendAnswer` | `callId, sdp` (≤ 64 KB) |
| `SendIceCandidate` | `callId, candidate` (≤ 8 KB, ≤ 64 per call) |
| `EndCall` | `callId, reason?` |
| `SendMissedCallNotification` | `callId` (terminal OFFLINE/MISSED calls only, 1 per call) |

### Server-to-Client

| Event | Payload |
|---|---|
| `CallCreated` | `callId, status, timeoutConfig` (to caller) |
| `IncomingCall` | `call, callerInfo, verifiedContext?, timeoutConfig` |
| `CallAccepted` | `callId` (to both; to the caller, includes remote SDP trigger) |
| `CallRejected` | `callId` |
| `CallCancelled` | `callId` |
| `CallBusy` | `callId` |
| `CallCollision` | `callId` |
| `CallOffline` | `callId, missedCallAllowed` |
| `ReceiveOffer` / `ReceiveAnswer` / `ReceiveIceCandidate` | `callId, payload` |
| `CallConnected` | `callId` |
| `CallReconnecting` / `CallReconnected` | `callId` |
| `CallEnded` | `callId, reason` |
| `CallFailed` | `callId, reason` |

Implementation requirements:

- Validate every `callId` against the authenticated participants; validate every signaling sender.
- Apply payload size limits and per-user rate limits (see SEC-005).
- Never broadcast SDP or ICE to a tenant — only to the two participants.
- Never permit an unauthorized user to join a call session.
- Use existing SignalR infrastructure and conventions; do not create a second hub infrastructure.

---

## 7. DATABASE DESIGN

Two new tables in the **Voice Service's own database** (SQLite first, mirroring the vanan-metering GĐ-1 pattern; PostgreSQL when scale justifies it). No tables in CoreHub, Gateway or any host application; nothing duplicates user, tenant, order, booking or chat records.

### VoiceCalls (`voice_calls`)

| Column | Type | Notes |
|---|---|---|
| `Id` | UUID (PK) | = `callId` (client-supplied UUIDv7, validated) |
| `TenantId` | UUID | **NOT NULL** — same-tenant rule |
| `CallerUserId` | UUID | FK to users |
| `ReceiverUserId` | UUID | FK to users |
| `SourceApplication` | string | verified from the session's app context |
| `ContextType` | string null | verified: `order` \| `booking` \| `chat` \| null |
| `ContextReferenceId` | string null | verified against existing record |
| `Status` | enum | state machine §5 |
| `StartedAt` | timestamp | set at CREATED |
| `AcceptedAt` | timestamp null | |
| `EndedAt` | timestamp null | |
| `DurationSeconds` | int | computed at end |
| `ConnectionType` | enum null | P2P \| TURN |
| `EndReason` | string null | |
| `CallerConnectionId`, `ReceiverConnectionId` | string null | SignalR connection IDs (forensics) |
| `CreatedAt` | timestamp | |

Constraints (DB-level enforcement):

- `UNIQUE (Id)`.
- **Partial unique index on active calls:** `UNIQUE (CallerUserId) WHERE Status IN ('CREATED','RINGING','ACCEPTED','NEGOTIATING','CONNECTED','RECONNECTING')` and the same for `ReceiverUserId`. This makes FR-007 race-proof when the Gateway scales.
- Indexes: `(TenantId, StartedAt)`, `(CallerUserId, StartedAt)`, `(ReceiverUserId, StartedAt)`, `(ReceiverUserId, Status)`, `(Id, OccurredAt)` on events.

### VoiceCallEvents (`voice_call_events`)

| Column | Type | Notes |
|---|---|---|
| `Id` | UUID (PK) | |
| `CallId` | UUID | FK |
| `EventType` | string | whitelist: state transitions, missed-call-notification |
| `ActorUserId` | UUID null | who caused the event |
| `OccurredAt` | timestamp | |
| `Metadata` | JSONB | restricted key whitelist (e.g., `reason`, `connectionId`); rejected keys are dropped |

### Retention (defaults, configurable per tenant)

- `voice_calls`: 12 months.
- `voice_call_events`: 3 months.
- Aggregate quality metrics: 30 days.

### Write path decision

- `voice_calls` + `voice_call_events` are written synchronously by the signaling module (small rows, low frequency). **No outbox/NATS replication** — call history is low business-criticality, and the existing outbox pattern is reserved for order/accounting flows. This is a deliberate simplicity decision.
- **No metering tables in CoreHub.** Voice usage events, wallets and rate cards live in vanan-metering (§14); the Gateway only reports usage.

### Reconciliation

- On Voice Service startup: mark non-terminal rows older than 5 minutes as FAILED (`SERVER_RESTART`), emit one audit event per row.
- No partition strategy in v1.1 (event table growth is trivial at the volume model in §12); revisit only when actual growth justifies it.

---

## 8. STUN / TURN INFRASTRUCTURE

### 8.1. STUN

- Public STUN acceptable for initial development; production configuration supports controlled STUN endpoints.
- STUN is stateless and cheap; P2P remains the dominant path.

### 8.2. TURN (Coturn, Ubuntu Linux)

Requirements:

- Separate deployment from CoreHub database workloads; dedicated public IP where feasible.
- UDP relay primary; TCP/TLS fallback; restricted relay port range.
- **Temporary credentials** issued by the Voice Service via Coturn TURN REST API (HMAC of username+TTL with `TURN_SECRET`).
- Credential TTL: **30 minutes**; issuance endpoint rate-limited (SEC-005).
- Per-credential allocation and bandwidth limits (Coturn user quotas).
- No permanent TURN passwords embedded in frontend code.
- Monitoring of allocations and bandwidth; rate limiting and abuse protection.

### 8.3. TURN Configuration (environment-based)

| Variable | Purpose |
|---|---|
| `TURN_PUBLIC_HOST` | Public host of the Coturn node |
| `TURN_UDP_PORT` | UDP relay port |
| `TURN_TLS_PORT` | TLS relay port |
| `TURN_SECRET` | HMAC secret — **never committed, never in frontend** |
| `TURN_REALM` | Realm |
| `TURN_MIN_RELAY_PORT` / `TURN_MAX_RELAY_PORT` | Relay port range |
| `TURN_CREDENTIAL_TTL` | Default 1800 s |

### 8.4. Network Requirements

Document all firewall rules: STUN UDP, TURN UDP/TCP/TLS, relay port range.

Test from: home Wi-Fi, mobile 4G/5G, corporate networks, restrictive NAT environments, UDP-blocked networks.

---

## 9. CLIENT APPLICATION REQUIREMENTS

### 9.1. Reusable Voice SDK

Logical interface (implementation follows existing Vạn An frontend conventions):

```
VoiceService.Initialize()
VoiceService.StartCall(targetUserId, context?, callId?)
VoiceService.AcceptCall(callId)
VoiceService.RejectCall(callId)
VoiceService.EndCall(callId)
VoiceService.Mute() / Unmute()
VoiceService.GetCallState()
VoiceService.OnMissedCalls()      // read own missed-call events
```

- Single SDK; no duplicated WebRTC logic per application.
- The SDK handles: getUserMedia constraints (below), ICE restart, reconnect timers, callId generation (UUIDv7), multi-device local cleanup on `CallAccepted` elsewhere.

### 9.2. Application Integration

Integration priority:

1. **Storefront chat (KhachLink)** — a voice-call button inside the chat conversation (customer ↔ merchant), reusing the chat conversation as the verified call context (`contextType=chat`). This is the first integration target.
2. TimLaThay.
3. Booking.

ShopERP follows once the SDK is stable.

Each application controls whether voice is enabled (and whether `voice_open_calling` is exposed in its settings). Integration is **by contract** (SDK + auth token) — host applications never reference the Voice Service internals.

### 9.3. Browser Compatibility

Test: Android Chrome (latest), desktop Chrome, Edge, **iOS Safari 17+**.

iOS Safari documented limitations: mic permission requires a user gesture (already guaranteed by our "request only on explicit action" rule); background execution is aggressively terminated — incoming calls cannot ring a backgrounded Safari tab (see §9.4).

### 9.4. PWA Background Behavior (honest contract)

- A web application **cannot** ring when the browser/OS has terminated it. v1.1 does not claim otherwise and does not build push-to-answer.
- Implemented: foreground SignalR notification; missed-call events stored server-side and shown on next app open (FR-014); reconnect when the app resumes.
- Product consequence (documented, not a defect): for the merchant/customer use case, the callee should keep the tab open, or use the missed-call callback flow. A native app with push-to-answer is a separate future capability.

### 9.5. Permission & Device Handling

Handle: permission granted, denied, mic unavailable, device disconnected, no input device, unsupported WebRTC APIs.

- **Never request microphone permission before the user explicitly initiates or accepts a call.**
- getUserMedia constraints (mandatory): `{ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } }`, mono, Opus 32–64 kbps target, hard cap 128 kbps.

### 9.6. Localization

Call UI strings follow existing i18n conventions of each host application (Vietnamese primary). Backend stores no user-facing strings.

---

## 10. SECURITY AND PRIVACY

### SEC-001: Authentication

Reuse existing authentication tokens and session policies (the Voice Service validates the existing Vạn An token scheme). No duplicate auth.

### SEC-002: Authorization (permission matrix)

| Check | Enforced where |
|---|---|
| Caller identity | Voice Service (validated token claims, server-only) |
| Receiver identity | Voice Service (resolved from userId, not client text) |
| Same-tenant membership | Voice Service (token tenant claim) |
| Verified business context | Voice Service (record exists + both participants) |
| Open-calling flag | Voice Service (tenant setting, default off) |
| Metering gate (wallet check / kill-switch) | Voice Service (§14) |
| Call participation (SDP/ICE/events) | Voice Service (callId ↔ participant map) |
| Call history reads | Voice Service data access (FR-011 matrix) |

### SEC-003: Media Encryption

DTLS-SRTP (WebRTC standard). No custom audio encryption.

### SEC-004: TURN Credentials

- Temporary (TTL 30 min), HMAC-signed with the server-side secret; never expose `TURN_SECRET`.
- Per-user issuance rate limits; per-credential allocation/bandwidth quotas; expire on call end.

### SEC-005: Abuse Prevention (concrete limits)

| Limit | Default (configurable) |
|---|---|
| `StartCall` per user | 10/min, 60/hour |
| Signaling messages per user | 30 msg/s sustained |
| SDP payload | ≤ 64 KB |
| ICE candidate | ≤ 8 KB, ≤ 64 candidates/call |
| TURN credential issuance | 30/hour/user |
| Missed-call notifications | 5/hour/user |

Plus: repeated-call protection (covered by per-minute rate limit), concurrent-call restriction (FR-007 + DB indexes), unauthorized call rejection (SEC-002).

### SEC-006: Privacy

- No default audio recording; no permanent storage of audio.
- No SDP retention; ICE histories never stored.
- Restricted call-history access (FR-011 matrix).
- Configurable retention periods (§7).
- Raw per-call quality metrics aggregated and dropped after 30 days.

### SEC-007: Audit

`voice_call_events` records the full lifecycle (who, when, from which connection) without conversation content. Events are append-only.

---

## 11. PERFORMANCE REQUIREMENTS

| Metric | Target |
|---|---:|
| Call invitation delivery | P95 < 1 s |
| SignalR command processing | P95 < 300 ms |
| P2P connection establishment | P95 < 5 s on suitable networks |
| TURN fallback establishment | P95 < 8 s on tested networks |
| Call state consistency | No duplicate active calls (DB-enforced) |
| Audio codec / transport | Opus mono 32–64 kbps / WebRTC |
| Server-side audio processing | None |

Measured under specified test-network conditions, not guaranteed across all mobile networks.

---

## 12. INFRASTRUCTURE SCALING MODEL

Scale drivers: **concurrent calls (C)** and **TURN relay minutes (R × D)** — not merchant count alone.

Planning variables and assumptions (as in v1.0, retained):

- 3 calls / merchant / day; 3 min / call; 30 days / month.
- 10% TURN-relayed call minutes (baseline; sensitivity in §13).
- TURN egress ≈ 0.5–1.0 MB / call-minute.

### 12.1. Concurrency (hour-of-day model — replaces v1.0's flat 1%)

```
peak_concurrent ≈ (daily_call_minutes) / (peak_hours × 3600) × (avg_call_seconds)
                = daily_call_minutes / (peak_hours × 60)   [in call-minutes terms]
```

Assumption: 8 busy hours/day (lunch + dinner + service hours). Result:

| Merchants | Daily call-minutes | Peak concurrent (8 h) |
|---|---:|---:|
| 100 | 900 | ~2 |
| 1,000 | 9,000 | ~19 |
| 10,000 | 90,000 | ~190 |

Peak estimates are ~2× the v1.0 flat-1% figures — the earlier model under-provisioned peak. All subsequent sizing uses these numbers.

### 12.2. Infrastructure by Phase

**Phase A: 100 merchants**

- Existing Gateway + CoreHub; one Coturn VPS (1–2 vCPU / 2 GB RAM, public IP, adequate UDP bandwidth).
- ~2 peak concurrent calls — a single small VPS is ample. Basic monitoring. No SFU, no Redis.

**Phase B: 1,000 merchants**

- Existing Gateway (load-tested); one dedicated Coturn instance (2 vCPU / 4 GB RAM).
- ~19 peak concurrent calls — still single-node.
- **No Redis yet**: single Gateway instance holds active state in memory. Redis becomes required only when the Gateway is horizontally scaled.
- Network monitoring; automated service recovery.

**Phase C: 10,000 merchants**

- Multiple Coturn nodes (2–3 to start, benchmarked); DNS-based or LB-based TURN allocation.
- ~190 peak concurrent calls ⇒ capacity headroom ≥ 2× (~400 concurrent) on tested nodes/network.
- **Redis required**: Gateway horizontal scaling ⇒ Redis backplane for SignalR + active-call state (this is the explicit trigger from §3.4).
- Separate monitoring; cost-per-call-minute report; bandwidth provisioned against measured TURN traffic, not assumptions.

Coturn's published performance claims never replace testing on the selected VPS and network.

---

## 13. INFRASTRUCTURE COST MODEL

Preliminary planning allowances in VND — not provider quotations. Baseline assumptions: P2P dominant, 10% TURN.

### 13.1. Monthly Cost (baseline, 10% TURN)

| Cost item | 100 merchants | 1,000 merchants | 10,000 merchants |
|---|---:|---:|---:|
| Coturn infrastructure | 150–350k | 250–600k | 1–3M |
| Network/bandwidth allowance | 0–150k | 100–400k | 500k–2M |
| Monitoring/operations | 0–100k | 0–200k | 200–800k |
| **Total** | **150–600k** | **350k–1.2M** | **1.7–5.8M** |

### 13.2. TURN Sensitivity (10,000 merchants — the dominant cost variable)

| TURN % | TURN minutes/month | TURN egress/month | Cost impact |
|---|---:|---:|---|
| 10% (baseline) | 270,000 | 135–270 GB | 1.7–5.8M |
| 20% | 540,000 | 270–540 GB | roughly ×1.5–2 |
| 40% | 1,080,000 | 540 GB–1.08 TB | roughly ×2.5–4; likely exceeds VPS bandwidth caps ⇒ provider/plan change |

Rules:

1. Prefer P2P; TURN only when necessary.
2. Never relay audio through Gateway.
3. Monitor TURN minutes separately; alert when relay % > 20%; **review cost per 1,000 call-minutes monthly**; re-run §13.2 whenever the 30-day TURN % moves > 5 points.
4. Set TURN bandwidth limits per credential.
5. No LiveKit until group-call requirements exist.

Excluded (as in v1.0): existing Gateway/CoreHub hosting, development labor, taxes, premium push services, managed TURN, SFU, PSTN/SIP.

---

## 14. METERING INTEGRATION (PHÍ NỀN TẢNG)

Voice usage is supervised by the existing **vanan-metering** service (Phí nền tảng) so the platform can apply fees when a rate card exists, and system admins can gate the feature for tenants whose wallets are below the limit. This section follows the existing metering conventions (`.devin/rules/metering.md`) — **no new metering infrastructure, no duplicated data**.

### 14.1. Service Codes (rate-card driven)

| Service code | Unit | Quantity per event | Meaning |
|---|---|---|---|
| `voice.call` | call | 1 | per call attempt that reached RINGING or beyond |
| `voice.minute` | minute | `ceil(durationSeconds / 60)` | per started minute of a CONNECTED call (0 if never connected) |

- Default rate-card Enforcement: **Deny** (same class as `chat.*` / `feature.*`); the system admin can flip it to `MeterOnly` (usage metered, calls never blocked) via the existing metering admin UI.
- Rate cards, wallets, ledgers and usage events live in vanan-metering. CoreHub stores nothing metering-related.

### 14.2. Call-time Gating (check at StartCall, never mid-call)

- Before ringing, the Voice Service performs a deny check on the **caller's tenant** (from token claims):
  `GET /v1/wallets/{tenantId}/check?serviceCode=voice.call&estimate=1` and
  `...serviceCode=voice.minute&estimate=<Voice:EstimatedMinutesPerCall>` (default 3).
- `deny=true` (head rate-card version `Enforcement=Deny` AND balance < estimate) ⇒ the call is rejected **before ringing** with terminal FAILED (reason `METERING_DENIED`); the caller sees "Hết hạn mức phí nền tảng" + top-up guidance (existing MeteringDenyGate UX convention of the host app).
- **A live call is never cut by wallet depletion.** If the balance runs out mid-call, the call continues and the shortfall accumulates as UnpaidCredits (metering ingest semantics) — safety over revenue.
- Check results are cached 30 s per (tenant, serviceCode) per metering client convention.
- **Fail-open (metering convention G9):** if vanan-metering is unreachable or the check returns an error, the gate treats it as "allow" — voice keeps working and the outage surfaces via metering metrics, never by breaking calls.

### 14.3. Usage Ingest (idempotent by callId)

- At call end (any terminal state), the Voice Service ingests to vanan-metering: `POST /v1/usage/events:batch` with the two events (§14.1) carrying `tenantId` (from token claims), `shopInstanceId` when present, `requestedBy`, and `metadata` (callId, contextType/contextReferenceId, connectionType P2P|TURN).
- **`Idempotency-Key` = `callId`** for every event — replay-safe by construction: a retried/duplicate delivery can never double-deduct.
- Fee deduction happens at ingest inside vanan-metering (`applied = min(required, balance)`, surplus → UnpaidCredits). No fee logic in the Gateway.

### 14.4. Admin Controls

| Control | Mechanism | Effect |
|---|---|---|
| Automatic wallet gate | wallet check at StartCall (§14.2) | denies tenants below the estimate while `voice.*` is Deny-enforced |
| Global on/off of the paid gate | rate-card `Enforcement` for `voice.*` (`Deny` \| `MeterOnly`) | `MeterOnly` ⇒ metered but never blocked |
| Restore service | credit grant / top-up confirm (existing metering admin flows) | wallet above threshold ⇒ calls allowed again |
| **Manual kill-switch per tenant** | `VoiceFeatureEnabled` flag in existing tenant settings (admin-only) | off ⇒ StartCall fails with FAILED (`FEATURE_DISABLED`) — used to proactively disable voice for a tenant whose wallet is below the limit, regardless of rate card |

Priority: **manual kill-switch > wallet gate** (kill-switch off always blocks; the wallet gate blocks only when Deny-enforced AND balance < estimate).

### 14.5. Configuration (Gateway environment)

| Variable | Purpose |
|---|---|
| `Metering__BaseUrl` / `Metering__CallerId` (= `voice`, a new caller seeded in vanan-metering) / `Metering__CallerKey` | metering caller credentials (trailing `/` required) |
| `Voice:EstimatedMinutesPerCall` | default 3 — estimate used in the deny check |

Service codes (`voice.call`, `voice.minute`) are constants; no per-app config.

### 14.6. UX Contract

- Denied by wallet: FAILED (`METERING_DENIED`) + wallet banner / top-up link in the host app (existing MeteringDenyGate conventions). KhachLink shows a neutral "tính năng tạm ngừng" message — no wallet internals to end customers.
- Kill-switch off: FAILED (`FEATURE_DISABLED`) + generic "tính năng tạm ngừng" message.
- All other states (busy, offline, missed, rejected) are unchanged (§4).

---

## 15. OBSERVABILITY

### Business Metrics

Total calls; completed / rejected / missed / failed / busy / offline calls; average duration; calls by application; calls by tenant; calls per merchant; missed-call notifications sent.

### Technical Metrics

Active calls; SignalR connections; signaling latency; ICE success rate; P2P success rate; TURN fallback %; TURN bandwidth; TURN allocation failures; audio interruptions; call establishment time; **reconciliation sweep count** (stale rows marked FAILED); **per-device iOS failure rate** (to catch Safari regressions); **calls denied by metering** (`METERING_DENIED` / `FEATURE_DISABLED`); **metering fail-open count**; **metering ingest failures**.

### Alerts (all thresholds configurable; defaults shown)

| Alert | Default threshold |
|---|---|
| TURN server unavailable | immediate |
| TURN bandwidth near capacity | 80% |
| TURN fallback ratio | > 20% (also triggers cost review, §13) |
| Call failure rate | > 5% |
| SignalR connection failures | > 1% of connections |
| Abnormal call initiation volume | > 3× 7-day average |
| Reconciliation sweep > 0 | any (means a Gateway restart happened) |

---

## 16. TESTING REQUIREMENTS

### 16.1. Functional Tests

- A calls B; B accepts. B rejects. A cancels. B busy. A busy (`CALLER_BUSY`). Invitation expires (MISSED). Either user ends the call.
- **Call collision** (A and B dial each other) ⇒ one call + `CALL_COLLISION`.
- **Offline callee** ⇒ immediate OFFLINE + missed-call notification on next connect.
- **Two-device accept race** ⇒ first accept wins, other device gets `CallAccepted`.
- **Duplicate StartCall** (same `callId`) ⇒ same call returned, no duplicate row.
- **Spoofed context** ⇒ `INVALID_CONTEXT`; cross-tenant attempt ⇒ rejected; unauthorized SDP/ICE on another user's callId ⇒ rejected.
- **Gateway restart mid-call** ⇒ clients receive `CallFailed(SERVER_RESTART)` on reconnect; reconciliation marks stale rows.
- Reconnection after network loss (ICE restart path).
- **Metering deny**: wallet below estimate ⇒ FAILED (`METERING_DENIED`) before ringing; wallet above ⇒ call proceeds.
- **Kill-switch off** ⇒ FAILED (`FEATURE_DISABLED`).
- **Fail-open**: vanan-metering unreachable ⇒ calls proceed (metering outage never breaks voice).
- **Idempotent ingest**: re-delivering the same usage event (same callId) deducts once.
- **MeterOnly mode**: usage metered, no denial.

### 16.2. WebRTC Tests

Wi-Fi↔Wi-Fi; Wi-Fi↔mobile; mobile↔mobile; restrictive NAT; TURN-only; UDP blocked; mic denied; device audio interruption; **iOS Safari** (gesture-gated mic, foreground calls); **ICE restart during Wi-Fi→4G handover**.

### 16.3. Security Tests (new in v1.1)

- Malicious/oversized SDP (> 64 KB) rejected; oversized ICE candidates rejected; ICE candidate flood (rate limit).
- Signaling message fuzzing; rate-limit enforcement (StartCall, TURN issuance, missed-call notifications).
- TURN secret leakage check (frontend bundle scan for `TURN_SECRET`).
- Cross-tenant SDP/ICE replay attempt.

### 16.4. Load Tests

| Stage | Concurrent calls | Signaling users |
|---|---:|---:|
| Initial | 5 | 100 |
| Intermediate | 20 | 1,000 |
| Expansion | 50 | 5,000 |
| Stress | 100 | 10,000 |

- Signaling load via simulated clients; **audio load via real WebRTC clients or controlled network emulation only** — never simulate audio with arbitrary WebSocket messages.
- Also verify: DB constraint behavior under concurrent StartCall (partial unique index), reconciliation timing.

### 16.5. Regression Tests

Authentication, tenant authorization, SignalR chat (incl. storefront chat), Booking, ShopERP, KhachLink, TimLaThay — unchanged behavior required.

---

## 17. IMPLEMENTATION PHASES

**Phase 0 – Codebase Audit** (no implementation changes): existing SignalR architecture, auth flow (token scheme for shared validation), tenant context, user presence, notification mechanism, DB conventions, PWA/browser compatibility, infrastructure/firewall inventory, **storefront chat flow (MessagingHub / RealtimeController / KhachLink chat UI) for the chat-first integration**. Classify each requirement EXISTING / MODIFY / MISSING / CONFLICT / REMOVE.

**Phase 1 – Core Voice Signaling (new independent module `10_Voice/vanan-voice`)**: own solution + SignalR hub + own DB; call lifecycle, SignalR events, permission model + context verification, state machine, `voice_calls`/`voice_call_events`, partial unique indexes, reconciliation, idempotency, **metering gate at StartCall + usage ingest (idempotency by callId)**. Unit + integration tests. No media server.

**Phase 2 – WebRTC P2P**: mic access, `RTCPeerConnection`, SDP/ICE exchange, audio (mono Opus), mute/unmute, connection monitoring, ICE restart.

**Phase 3 – Coturn Fallback**: Ubuntu deployment, TURN REST credentials via Gateway, UDP/TCP/TLS, firewall, monitoring, forced-TURN testing, security tests (§16.3).

**Phase 4 – PWA Integration (chat-first)**: **storefront chat (KhachLink) first** — voice call inside the chat conversation; then TimLaThay, then Booking; Android Chrome + desktop + iOS Safari verification; missed-call flow; **admin kill-switch UI + wallet banner integration (§14.4)**.

**Phase 5 – Pilot**: 10 merchants, 20–50 users, 5 simultaneous calls, ≥ 100 test calls, Wi-Fi + mobile mix. Collect actual usage/network metrics; re-baseline §12 assumptions.

**Phase 6 – Production Rollout**: 100 → 1,000 → 10,000 merchants; each stage passes the corresponding gate.

---

## 18. SCALING GATES

**Gate 1: 100 merchants**

- Stable one-to-one calls; P2P success ≥ 85% in the pilot environment.
- TURN fallback works; no critical authorization issues; no regressions.
- Permission model + context verification verified (including spoofed-context rejection).
- iOS Safari calls pass.
- Metering gate + fail-open verified (§14).

**Gate 2: 1,000 merchants**

- 20 concurrent-call test passes; SignalR stable; TURN bandwidth monitored.
- No significant degradation in Gateway/CoreHub; operational recovery procedures exist (restart reconciliation verified).

**Gate 3: 10,000 merchants**

- 100 concurrent-call test passes (capacity planned for ~190 peak + 2× headroom).
- Multiple Coturn nodes; tested TURN allocation distribution; Redis backplane + horizontally scaled Gateway verified.
- Security & abuse testing passed (§16.3); cost-per-call-minute report delivered; §13.2 re-run with measured TURN %.
- Metering ingest load at 100 concurrent calls measured.

Do not advance to the next stage solely because the merchant registration count has increased.

---

## 19. ACCEPTANCE CRITERIA

Release accepted only when all of:

1. Two authenticated same-tenant users establish an audio call.
2. P2P works without TURN when the network allows it; TURN fallback works otherwise.
3. Call signaling never transports audio.
4. Unauthorized users cannot access call sessions; **spoofed business context is rejected**; cross-tenant calls are rejected.
5. **Offline callee** gets immediate OFFLINE; **call collision** resolves without duplicate calls; **duplicate StartCall** (`callId`) is idempotent.
6. One active call per user is enforced at DB level (partial unique indexes).
7. Call state remains consistent after disconnects; **Gateway restart reconciles stale calls to FAILED**.
8. Call history persisted correctly and readable only per the FR-011 access matrix.
9. No audio recording is performed.
10. Android Chrome, desktop Chrome, Edge and **iOS Safari 17+** pass tests.
11. Existing chat and application functions remain operational.
12. Deployment and rollback instructions documented.
13. Load tests and cost reports delivered (including §13.2 sensitivity with measured TURN %).
14. Voice usage is metered (`voice.call`, `voice.minute`) with callId-idempotent events; the wallet-based deny gate and the admin kill-switch work; a metering outage does not break calls (fail-open).

---

## 20. REQUIRED DELIVERABLES

1. Codebase Audit Report.
2. Architecture Gap Analysis.
3. Updated System Architecture Diagram.
4. Database Migration Scripts (voice_calls, voice_call_events, partial unique indexes, reconciliation job).
5. Backend Voice Signaling Module (inside Gateway).
6. Frontend Reusable Voice SDK.
7. Coturn Deployment Configuration.
8. Environment Variable Documentation.
9. Security Review (incl. §16.3 results).
10. Unit Tests; 11. Integration Tests; 12. WebRTC E2E Tests (incl. iOS Safari + ICE restart).
13. Load Test Scripts and Results; 14. Infrastructure Sizing Report.
15. Cost Model (with TURN sensitivity).
16. Deployment Guide; 17. Rollback Guide.
18. Updated Project State.
19. Metering Integration (usage events, deny gate, admin kill-switch, fail-open verification).

All deliverables must be based on the actual existing codebase.

---

## 21. NON-NEGOTIABLE ENGINEERING CONSTRAINTS

- Do not rewrite the existing Gateway (voice logic lives in the new module, not in Gateway).
- One new independent module is justified by reuse across applications (mirrors the Metering / Vạn An AI pattern — own solution, own API contract, own DB). No other new microservices.
- The Voice Service is attachable **by contract** (small client + auth token), never by shared code; host applications must not reference its internals.
- Do not move audio through CoreHub or through SignalR.
- Do not deploy LiveKit or introduce SIP in v1.1.
- Do not create duplicate authentication or tenant systems.
- Do not bypass authorization for convenience.
- Do not use fake successful WebRTC connections in tests.
- Do not use stub implementations as production functionality.
- Do not use Entity Framework `EnsureCreated` in production.
- Do not silently change existing database schemas.
- Preserve existing architecture and coding conventions; follow existing TDD, E2E and guard-check requirements.
- Every infrastructure change must have a rollback procedure.
- Do not store audio, SDP payloads, or ICE histories.
- Do not add persistence beyond `voice_calls` and `voice_call_events` without a Gate review.
- Do not duplicate metering data in CoreHub — voice usage is reported to vanan-metering only.
- Never cut a live call because of credit depletion (usage is deducted after the call).
- Follow the metering fail-open convention (G9): a metering outage must never break voice.

---

## 22. FINAL ARCHITECTURAL DECISION

Vạn An Voice Service v1.1 is an independent, reusable module (`10_Voice/vanan-voice`) that any application can attach — following the vanan-metering / vanan-ai pattern:

- **Default path:** WebRTC P2P (mono Opus, DTLS-SRTP).
- **Fallback:** Coturn TURN (short-lived REST credentials issued by the module).
- **Signaling & authorization:** the module's own SignalR hub; the existing Vạn An token scheme is reused (shared validation) with tenant from token claims; v1.1 permission model (default-deny; verified business context or tenant opt-in).
- **Persistence:** `voice_calls` + `voice_call_events` in the module's own database (SQLite first), with DB-level active-call enforcement and startup reconciliation.
- **Attachability:** integration by contract (SDK + token); first integration = **storefront chat (KhachLink)**, then TimLaThay, Booking.
- **Scale trigger:** Redis + horizontal module instances only at Phase C (10,000 merchants / ~190 peak concurrent), never before.
- **Honest client contract:** calls work while the app is alive; missed calls are persisted and surfaced on next open; no push-to-answer claims.
- **Metering (Phí nền tảng):** usage reported to vanan-metering (`voice.call`, `voice.minute`), wallet-based deny gate at StartCall, per-tenant admin kill-switch, fail-open — metering failure never breaks calls (§14).

SFU, SIP, group calls, native push-to-answer and AI voice remain future extensions. The architecture supports incremental growth without forcing Vạn An to purchase or operate large-scale telecommunication infrastructure prematurely.

**End of SRS v1.1.**