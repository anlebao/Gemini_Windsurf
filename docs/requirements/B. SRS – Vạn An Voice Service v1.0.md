B. SRS – Vạn An Voice Service v1.0
:::writing{variant="document" id="73182"}
# SOFTWARE REQUIREMENTS SPECIFICATION
# VẠN AN VOICE SERVICE v1.0

**Project:** Vạn An Ecosystem  
**Module:** Voice Service  
**Version:** 1.0  
**Priority:** P1 – Shared Platform Capability  
**Architecture:** WebRTC P2P + SignalR Signaling + Coturn Fallback  
**Status:** Proposed – Audit Before Implementation

---

## 1. EXECUTIVE SUMMARY

### 1.1. Purpose

Build a reusable, low-cost, peer-to-peer voice communication service for all Vạn An ecosystem applications.

The service must support one-to-one audio calls between authenticated users without requiring a dedicated media server for normal calls.

### 1.2. Business Objectives

- Enable direct communication between customers, merchants, employees, CTVs and cooperative members.
- Reuse existing Gateway, authentication, tenant and CoreHub infrastructure.
- Minimize infrastructure expenditure and operational complexity.
- Support gradual scaling from 100 to 10,000 merchants.
- Provide a reusable service instead of implementing voice independently in each application.

### 1.3. Core Principles

1. P2P first, TURN only when necessary.
2. No audio streaming through CoreHub or Gateway.
3. No mandatory SFU or SIP infrastructure in v1.0.
4. Reuse existing authentication and tenant authorization.
5. No unnecessary new microservices.
6. Fail safely when voice connectivity is unavailable.
7. Do not break existing Chat, Booking, ShopERP or KhachLink functionality.

---

## 2. SCOPE

### 2.1. In Scope

- One-to-one audio calls.
- Outgoing call invitation.
- Incoming call notification.
- Accept and reject.
- Cancel outgoing calls.
- Busy and unavailable states.
- Ring timeout.
- Mute and unmute microphone.
- End call.
- Connection status.
- P2P connectivity using WebRTC.
- STUN/TURN ICE configuration.
- Call history.
- Call event logging.
- Tenant-aware authorization.
- Web/PWA and Android Chrome compatibility.
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

These features must not be introduced as hidden dependencies in v1.0.

---

## 3. SYSTEM ARCHITECTURE

### 3.1. Logical Architecture

Existing ecosystem:

- CoreHub: PostgreSQL and business data.
- Gateway: API and SignalR.
- ShopERP: Merchant operations.
- KhachLink: Customer application.
- TimLaThay: Local commerce discovery.
- Booking: Appointment and service booking.

New logical module:

Voice Service.

Voice Service must be implemented as a modular component within the existing backend architecture unless codebase inspection demonstrates a strong reason to deploy it separately.

### 3.2. Communication Architecture

Application A
    |
    | HTTPS / SignalR
    |
Vạn An Gateway
    |
    | Authentication / Authorization
    |
Voice Signaling Module
    |
    | Call invitation / SDP / ICE
    |
Application B

After successful WebRTC negotiation:

Application A <========== Audio ==========> Application B

Fallback:

Application A <==== TURN Relay ====> Application B

CoreHub stores call metadata and events only.

### 3.3. Component Responsibilities

**A. Gateway / SignalR**

- Authenticate connections.
- Receive call commands.
- Route invitations to the intended user.
- Exchange SDP offers and answers.
- Exchange ICE candidates.
- Deliver call-state events.
- Enforce tenant and user permissions.
- Apply rate limits.

**B. WebRTC Client**

- Request microphone access.
- Create RTCPeerConnection.
- Exchange SDP.
- Exchange ICE candidates.
- Establish audio transmission.
- Handle mute/unmute.
- Monitor connection state.
- Close tracks and connections when calls end.

**C. Coturn**

- Provide STUN functionality.
- Provide TURN relay when direct P2P is unavailable.
- Support UDP first.
- Provide TCP/TLS fallback where supported.
- Use temporary authenticated credentials.
- Apply bandwidth and allocation limits.

**D. CoreHub**

- Store call records.
- Store relevant call lifecycle events.
- Support tenant-aware reporting.
- Retain audit metadata.
- Never store raw audio streams.

---

## 4. FUNCTIONAL REQUIREMENTS

### FR-001: User Authentication

Every calling user must be authenticated.

Requirements:

- Reuse existing Vạn An authentication.
- Resolve the actual user ID from the authenticated session.
- Never trust a client-supplied caller ID.
- Verify account status.
- Verify tenant membership and calling permissions.
- Support users belonging to multiple tenants.

### FR-002: Call Initiation

An authenticated user can initiate a one-to-one voice call.

Input:

- Target user ID.
- Optional tenant ID.
- Optional business context.
- Optional booking/order reference.

Validation:

- Target user exists.
- Target user is eligible for voice communication.
- Caller has permission to contact target.
- Target is not the caller.
- Target is not already in an incompatible call state.

Return:

- Call ID.
- Call status.
- Invitation delivery result.

### FR-003: Incoming Call

The receiving user must receive:

- Caller display name.
- Caller avatar if available.
- Relevant merchant or booking context.
- Accept button.
- Reject button.

Incoming call notification must not expose unauthorized tenant information.

### FR-004: Call Acceptance

When the receiver accepts:

- Verify the invitation is still valid.
- Verify the call has not already been accepted or cancelled.
- Establish the WebRTC negotiation process.
- Exchange SDP offer/answer.
- Exchange ICE candidates.
- Start the audio connection.

Only one receiver may accept a given invitation.

### FR-005: Call Rejection

The receiver can reject an incoming call.

The caller receives a rejected event.

The call must be finalized with an appropriate terminal state.

### FR-006: Call Cancellation

The caller can cancel an unanswered outgoing call.

If the receiver has not accepted, the invitation must become invalid immediately.

### FR-007: Busy State

A user must not accept multiple incompatible one-to-one calls simultaneously.

Initial policy:

- One active call per user.
- Additional incoming calls receive BUSY.
- No call waiting in v1.0.

### FR-008: Call Timeout

Suggested initial limits:

- Invitation timeout: 30 seconds.
- SDP negotiation timeout: 20 seconds.
- ICE connection timeout: configurable.
- Network recovery window: 10 seconds.
- Maximum call duration: configurable by policy.

Timeout values must be configurable rather than hardcoded across the client and server.

### FR-009: Mute / Unmute

The user can mute or unmute their local microphone.

Mute must not terminate the call.

### FR-010: End Call

Either participant can terminate the call.

Both clients must receive the terminal event.

The server must persist the final call state.

### FR-011: Call History

Store:

- Call ID.
- Caller ID.
- Receiver ID.
- Tenant ID.
- Application source.
- Related business reference.
- Started timestamp.
- Accepted timestamp.
- Ended timestamp.
- Duration.
- Termination reason.
- Connection type: P2P or TURN.
- Final call status.

Do not store SDP payloads, ICE candidate histories or audio data in permanent call-history records.

### FR-012: Connection Quality

Monitor:

- ICE connection state.
- Peer connection state.
- Selected candidate type.
- Round-trip time where available.
- Packet loss.
- Jitter.
- Audio interruption.
- Connection duration.

Collect aggregate quality metrics with privacy controls.

---

## 5. CALL STATE MACHINE

Allowed states:

- CREATED
- RINGING
- ACCEPTED
- NEGOTIATING
- CONNECTED
- RECONNECTING
- ENDED
- REJECTED
- CANCELLED
- MISSED
- BUSY
- FAILED

Valid lifecycle:

CREATED
  |
RINGING
  |
  +---- REJECTED
  |
  +---- CANCELLED
  |
  +---- MISSED
  |
  +---- BUSY
  |
  +---- ACCEPTED
          |
      NEGOTIATING
          |
      CONNECTED
          |
      RECONNECTING (optional)
          |
      CONNECTED
          |
         ENDED

Rules:

- Terminal states cannot transition back to active states.
- State transitions must be idempotent.
- Duplicate SignalR messages must not create duplicate calls.
- Server state is authoritative.
- Client state is provisional until confirmed by the server.
- A disconnected client must not leave an indefinitely active call record.

---

## 6. SIGNALR API DESIGN

Implement the following logical hub operations.

### Client-to-Server

- StartCall(targetUserId, context)
- AcceptCall(callId)
- RejectCall(callId)
- CancelCall(callId)
- SendOffer(callId, sdp)
- SendAnswer(callId, sdp)
- SendIceCandidate(callId, candidate)
- EndCall(callId)
- SetCallPresence(available)

### Server-to-Client

- IncomingCall(call)
- CallAccepted(callId)
- CallRejected(callId)
- CallCancelled(callId)
- CallBusy(callId)
- ReceiveOffer(callId, sdp)
- ReceiveAnswer(callId, sdp)
- ReceiveIceCandidate(callId, candidate)
- CallConnected(callId)
- CallEnded(callId, reason)
- CallFailed(callId, reason)

Implementation requirements:

- Validate every callId against the authenticated participants.
- Validate every signaling sender.
- Limit SDP and ICE payload sizes.
- Apply per-user rate limits.
- Never broadcast SDP or ICE to an entire tenant.
- Never permit unauthorized users to join a call session.

Use existing SignalR infrastructure if compatible.

Do not create a second independent SignalR hub infrastructure without codebase justification.

---

## 7. DATABASE DESIGN

Create a minimal call-history model.

### VoiceCalls

- Id: UUID
- TenantId: nullable UUID
- CallerUserId: UUID
- ReceiverUserId: UUID
- SourceApplication: string
- ContextType: nullable string
- ContextReferenceId: nullable string
- Status: enum
- StartedAt: timestamp
- AcceptedAt: nullable timestamp
- EndedAt: nullable timestamp
- DurationSeconds: integer
- ConnectionType: nullable enum
- EndReason: nullable string
- CreatedAt: timestamp

### VoiceCallEvents

- Id: UUID
- CallId: UUID
- EventType: string
- ActorUserId: nullable UUID
- OccurredAt: timestamp
- Metadata: restricted JSON

Requirements:

- Use PostgreSQL.
- Apply appropriate indexes.
- Partition large event tables only when actual growth justifies it.
- Define retention policies.
- Avoid storing personal information unnecessarily.
- Do not store microphone recordings.

Suggested indexes:

- TenantId + StartedAt
- CallerUserId + StartedAt
- ReceiverUserId + StartedAt
- CallId + OccurredAt

Use the existing tenant isolation and data-access conventions.

Do not create tables that duplicate existing user, tenant or application records.

---

## 8. STUN / TURN INFRASTRUCTURE

### 8.1. STUN

Use public STUN for initial development if appropriate.

Production configuration must support controlled STUN endpoints.

### 8.2. TURN

Use Coturn on Ubuntu Linux.

Requirements:

- Separate deployment from CoreHub database workloads.
- Dedicated public IP where feasible.
- UDP relay as primary transport.
- TCP/TLS fallback.
- Restricted port ranges.
- Short-lived credentials.
- No permanent TURN passwords embedded in frontend code.
- Monitoring of allocations and bandwidth.
- Rate limiting and abuse protection.

Use the Coturn TURN REST authentication mechanism or an equivalent secure temporary credential mechanism.

### 8.3. TURN Configuration

Configuration must be environment-based:

- TURN_PUBLIC_HOST
- TURN_UDP_PORT
- TURN_TLS_PORT
- TURN_SECRET
- TURN_REALM
- TURN_MIN_RELAY_PORT
- TURN_MAX_RELAY_PORT
- TURN_CREDENTIAL_TTL

Do not commit production secrets.

### 8.4. Network Requirements

Document all required firewall rules.

Support:

- STUN UDP.
- TURN UDP.
- TURN TCP.
- TURN TLS.
- Configurable relay port range.

Test from:

- Home Wi-Fi.
- Mobile 4G/5G.
- Corporate networks.
- Restrictive NAT environments.

---

## 9. CLIENT APPLICATION REQUIREMENTS

### 9.1. Reusable Voice SDK

Create a reusable frontend abstraction.

Suggested logical interface:

VoiceService.Initialize()
VoiceService.StartCall()
VoiceService.AcceptCall()
VoiceService.RejectCall()
VoiceService.EndCall()
VoiceService.Mute()
VoiceService.Unmute()
VoiceService.GetCallState()

The actual implementation must follow existing Vạn An frontend conventions.

Do not duplicate WebRTC implementation in every application.

### 9.2. Application Integration

Initial integrations:

1. TimLaThay.
2. KhachLink.
3. Booking.

ShopERP integration can follow after the initial client library is stable.

Each application must control whether voice is enabled.

### 9.3. Browser Compatibility

Test:

- Chrome Android.
- Desktop Chrome.
- Edge.
- Supported mobile browsers.

### 9.4. PWA Background Behavior

Important limitation:

A web application cannot guarantee incoming calls when the browser or operating system has terminated its execution.

Implement:

- Foreground SignalR notification.
- Push notification where supported.
- Reconnect when the application resumes.
- Clear missed-call state.
- No false claim of guaranteed background ringing.

Native push integration is a separate capability and must not be treated as completed merely because foreground SignalR works.

### 9.5. Permission Handling

Handle:

- Microphone permission granted.
- Permission denied.
- Microphone unavailable.
- Device disconnected.
- No audio input device.
- Browser does not support required WebRTC APIs.

Never request microphone permission before the user explicitly initiates or accepts a call.

---

## 10. SECURITY AND PRIVACY

### SEC-001: Authentication

Reuse existing authentication tokens and session policies.

### SEC-002: Authorization

Validate:

- Caller identity.
- Receiver identity.
- Tenant membership.
- Contact permissions.
- Call participation.

### SEC-003: Media Encryption

Use WebRTC DTLS-SRTP media encryption.

Do not implement custom audio encryption.

### SEC-004: TURN Credentials

- Use temporary credentials.
- Never expose the TURN secret.
- Apply expiration.
- Prevent unlimited TURN allocation.

### SEC-005: Abuse Prevention

Implement:

- Call initiation rate limits.
- User block list integration where available.
- Repeated-call protection.
- Concurrent-call restrictions.
- Unauthorized call rejection.

### SEC-006: Privacy

- No default audio recording.
- No permanent storage of audio.
- No unnecessary SDP retention.
- Restricted access to call histories.
- Configurable data-retention periods.

### SEC-007: Audit

Maintain auditable records of call lifecycle events without recording conversation content.

---

## 11. PERFORMANCE REQUIREMENTS

Initial design targets, to be verified through load tests:

| Metric | Target |
|---|---:|
| Call invitation delivery | P95 under 1 second |
| SignalR command processing | P95 under 300 ms |
| P2P connection establishment | P95 under 5 seconds on suitable networks |
| TURN fallback establishment | P95 under 8 seconds on tested networks |
| Call state consistency | No duplicate active calls |
| Audio codec | Opus |
| Audio transport | WebRTC |
| Normal call quality | Stable conversational audio |
| Server-side audio processing | None |

Targets are measured under specified test-network conditions, not guaranteed across all mobile networks.

---

## 12. INFRASTRUCTURE SCALING MODEL

Do not scale infrastructure according to merchant count alone.

Use these variables:

- M = registered merchants.
- C = simultaneous active calls.
- D = total monthly call minutes.
- R = percentage of call minutes relayed through TURN.
- B = average TURN egress bandwidth per call.

Planning assumptions:

- 3 calls per merchant per day.
- 3 minutes per call.
- 30 operating days per month.
- 10% TURN-relayed call minutes.
- Approximate TURN egress: 0.5–1.0 MB per call-minute.

These are planning assumptions, not measured Vạn An production statistics.

### 12.1. Capacity Scenarios

| Metric | 100 merchants | 1,000 merchants | 10,000 merchants |
|---|---:|---:|---:|
| Monthly call minutes | 27,000 | 270,000 | 2,700,000 |
| TURN minutes (10%) | 2,700 | 27,000 | 270,000 |
| Peak concurrent calls (1%) | 1 | 10 | 100 |
| Estimated TURN egress | 1.35–2.7 GB | 13.5–27 GB | 135–270 GB |

The 1% concurrency assumption is a capacity-planning scenario, not a measured adoption rate.

### 12.2. Infrastructure by Phase

#### Phase A: 100 Merchants

- Existing Gateway.
- Existing CoreHub.
- One Coturn VPS.
- 1–2 vCPU.
- 2 GB RAM.
- Public IP.
- Adequate UDP bandwidth.
- Basic monitoring.

Do not deploy SFU.

#### Phase B: 1,000 Merchants

- Existing Gateway, subject to load testing.
- One dedicated Coturn instance.
- Redis only if required for distributed SignalR or shared ephemeral state.
- 2 vCPU.
- 4 GB RAM.
- Network monitoring.
- Automated service recovery.

Do not add redundant servers without measured availability requirements.

#### Phase C: 10,000 Merchants

- Multiple Coturn instances.
- DNS-based or load-balancer-based TURN allocation.
- Separate monitoring.
- Redis backplane if Gateway is horizontally scaled.
- 2–3 Coturn nodes initially, subject to benchmark.
- Suggested starting node size: 4 vCPU / 8 GB RAM.
- Network capacity provisioned according to actual TURN traffic.
- Capacity headroom of at least 2x measured peak traffic.

Do not assume one server can safely handle all 100 concurrent TURN calls without benchmarking.

Coturn supports TURN authentication, multiple relay transports, monitoring and load-balancing approaches. Its published performance claims must not replace testing on the selected VPS and network. 

---

## 13. INFRASTRUCTURE COST MODEL

All amounts below are preliminary planning allowances in VND, not provider quotations.

### 13.1. Monthly Cost

| Cost item | 100 merchants | 1,000 merchants | 10,000 merchants |
|---|---:|---:|---:|
| Coturn infrastructure | 150–350k | 250–600k | 1–3M |
| Additional network/bandwidth allowance | 0–150k | 100–400k | 500k–2M |
| Monitoring/operations allowance | 0–100k | 0–200k | 200–800k |
| Total estimated incremental cost | 150–600k | 350k–1.2M | 1.7–5.8M |

The above estimates assume P2P remains the dominant connection mode.

They exclude:

- Existing Gateway and CoreHub hosting.
- Development labor.
- Taxes.
- Premium push-notification services.
- Paid managed TURN services.
- SFU.
- PSTN/SIP charges.

Actual costs must be recalculated using provider bandwidth pricing, egress policies, actual call minutes and measured TURN percentage.

### 13.2. Cost Optimization Rules

1. Prefer P2P.
2. Use TURN only when necessary.
3. Do not relay audio through Gateway.
4. Monitor TURN minutes separately.
5. Set TURN bandwidth limits.
6. Alert when relay percentage exceeds 20%.
7. Review cost per 1,000 call-minutes monthly.
8. Do not deploy LiveKit until group-call requirements exist.

---

## 14. OBSERVABILITY

Implement the following metrics:

### Business Metrics

- Total calls.
- Completed calls.
- Rejected calls.
- Missed calls.
- Failed calls.
- Average duration.
- Calls by application.
- Calls by tenant.
- Calls per merchant.

### Technical Metrics

- Active calls.
- SignalR connections.
- Signaling latency.
- ICE connection success rate.
- P2P success rate.
- TURN fallback percentage.
- TURN bandwidth.
- TURN allocation failures.
- Audio interruption.
- Call establishment time.

### Alerts

- TURN server unavailable.
- TURN bandwidth approaching capacity.
- TURN fallback above 20%.
- Call failure rate above 5%.
- SignalR connection failures.
- Abnormal call initiation volume.
- Unexpected increase in average connection time.

All thresholds must be configurable.

---

## 15. TESTING REQUIREMENTS

### 15.1. Functional Tests

- User A calls User B.
- User B accepts.
- User B rejects.
- User A cancels.
- User B is busy.
- Invitation expires.
- Either user ends the call.
- Duplicate events.
- Unauthorized call attempt.
- Cross-tenant unauthorized attempt.
- Reconnection after network loss.

### 15.2. WebRTC Tests

Test:

- Wi-Fi to Wi-Fi.
- Wi-Fi to mobile network.
- Mobile to mobile.
- Restrictive NAT.
- TURN-only connection.
- UDP unavailable.
- Microphone denied.
- Device audio interruption.

### 15.3. Load Tests

Test these stages:

| Stage | Concurrent calls | Signaling users |
|---|---:|---:|
| Initial | 5 | 100 |
| Intermediate | 20 | 1,000 |
| Expansion | 50 | 5,000 |
| Stress | 100 | 10,000 |

Signaling users and concurrent calls must be tested separately.

Load-test signaling using simulated clients.

Load-test actual audio using real WebRTC clients or controlled network emulation.

Do not simulate audio traffic by sending arbitrary WebSocket messages.

### 15.4. Regression Tests

Existing functionality must remain intact:

- Authentication.
- Tenant authorization.
- SignalR chat.
- Booking.
- ShopERP.
- KhachLink.
- TimLaThay.

---

## 16. IMPLEMENTATION PHASES

### Phase 0 – Codebase Audit

No implementation changes.

Deliverables:

- Existing SignalR architecture.
- Existing authentication flow.
- Existing tenant context.
- Existing user presence implementation.
- Existing notification mechanism.
- Existing database conventions.
- Existing PWA/browser compatibility.
- Infrastructure and firewall inventory.

Classify each requirement:

- EXISTING – Reuse.
- MODIFY – Extend existing implementation.
- MISSING – Implement.
- CONFLICT – Resolve architectural conflict.
- REMOVE – Remove only if demonstrably incorrect or redundant.

### Phase 1 – Core Voice Signaling

Implement:

- Call lifecycle.
- SignalR events.
- Authorization.
- State machine.
- Call records.
- Unit and integration tests.

No media server.

### Phase 2 – WebRTC P2P

Implement:

- Microphone access.
- RTCPeerConnection.
- SDP exchange.
- ICE exchange.
- Audio transmission.
- Mute/unmute.
- Connection monitoring.

### Phase 3 – Coturn Fallback

Implement:

- Ubuntu deployment.
- Secure credentials.
- UDP/TCP/TLS.
- Firewall configuration.
- TURN monitoring.
- Forced TURN testing.

### Phase 4 – PWA Integration

Integrate with:

- TimLaThay.
- KhachLink.
- Booking.

Verify Android Chrome and desktop browser.

### Phase 5 – Pilot

Initial pilot:

- 10 merchants.
- 20–50 users.
- 5 simultaneous calls.
- At least 100 test calls.
- Mix of Wi-Fi and mobile networks.

Collect actual usage and network metrics.

### Phase 6 – Production Rollout

Progression:

- 100 merchants.
- 1,000 merchants.
- 10,000 merchants.

Each stage requires passing the corresponding scaling gate.

---

## 17. SCALING GATES

### Gate 1: 100 Merchants

Required:

- Stable one-to-one calls.
- P2P success target of at least 85% in the tested pilot environment.
- TURN fallback works.
- No critical authorization issues.
- No regression in existing applications.

### Gate 2: 1,000 Merchants

Required:

- 20 concurrent-call test.
- SignalR stability.
- TURN bandwidth monitoring.
- No significant degradation in Gateway or CoreHub.
- Operational recovery procedures.

### Gate 3: 10,000 Merchants

Required:

- 100 concurrent-call test.
- Multiple Coturn nodes.
- Tested TURN allocation distribution.
- SignalR horizontal scaling strategy.
- Redis backplane where necessary.
- Network capacity verified.
- Security and abuse testing.
- Cost-per-call-minute report.

Do not advance to the next stage solely because the merchant registration count has increased.

---

## 18. ACCEPTANCE CRITERIA

The release is accepted only when:

1. Two authenticated users can establish an audio call.
2. P2P works without TURN when the network allows it.
3. TURN fallback works when direct connectivity is unavailable.
4. Call signaling does not transport audio.
5. Unauthorized users cannot access call sessions.
6. Cross-tenant isolation is verified.
7. Call state remains consistent after disconnects.
8. Call history is persisted correctly.
9. No audio recording is performed.
10. Android Chrome and desktop Chrome pass tests.
11. Existing chat and application functions remain operational.
12. Deployment and rollback instructions are documented.
13. Load tests and cost reports are delivered.

---

## 19. REQUIRED DELIVERABLES FROM DEVIN

Devin must deliver:

1. Codebase Audit Report.
2. Architecture Gap Analysis.
3. Updated System Architecture Diagram.
4. Database Migration Scripts.
5. Backend Voice Signaling Module.
6. Frontend Reusable Voice SDK.
7. Coturn Deployment Configuration.
8. Environment Variable Documentation.
9. Security Review.
10. Unit Tests.
11. Integration Tests.
12. WebRTC E2E Tests.
13. Load Test Scripts and Results.
14. Infrastructure Sizing Report.
15. Cost Model.
16. Deployment Guide.
17. Rollback Guide.
18. Updated Project State.

All deliverables must be based on the actual existing codebase.

---

## 20. NON-NEGOTIABLE ENGINEERING CONSTRAINTS

- Do not rewrite existing Gateway.
- Do not introduce a new microservice without justification.
- Do not move audio traffic through CoreHub.
- Do not move audio traffic through SignalR.
- Do not deploy LiveKit in v1.0.
- Do not introduce SIP in v1.0.
- Do not create duplicate authentication or tenant systems.
- Do not bypass authorization for convenience.
- Do not use fake successful WebRTC connections in tests.
- Do not use stub implementations as production functionality.
- Do not use Entity Framework EnsureCreated in production.
- Do not silently change existing database schemas.
- Preserve existing architecture and coding conventions.
- Follow existing TDD, E2E and guard-check requirements.
- Every infrastructure change must have a rollback procedure.

---

## 21. FINAL ARCHITECTURAL DECISION

Vạn An Voice Service v1.0 is a lightweight shared communication capability.

The default communication path is WebRTC P2P.

Coturn provides fallback connectivity.

Gateway provides signaling and authorization.

CoreHub stores metadata and call history.

SFU, SIP and AI voice features remain future extensions.

The architecture must support incremental growth without forcing Vạn An to purchase or operate large-scale telecommunication infrastructure prematurely.

**End of SRS v1.0.**