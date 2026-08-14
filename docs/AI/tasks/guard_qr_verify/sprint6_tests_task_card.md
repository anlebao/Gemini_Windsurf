# TASK CARD — Sprint 6: Tests + E2E (Issue #126)

> **Status:** 📋 PENDING
> **Priority:** P6 — After Sprint 3+4+5 approval
> **Branch:** `feature/guard-qr-verify`
> **Mode:** IMPLEMENT (Test Phase)
> **Domain modification:** NO
> **Playwright:** ENABLED (Sprint 6 = post-implementation, per Playwright guard rules)

## Objective
Unit tests (GuardService + Domain) + Integration tests (API) + 1 Playwright E2E spec (full flow). CI pipeline ALL PASS.

## Prerequisites
- [ ] Sprint 1-5 complete
- [ ] Build 0 errors
- [ ] Feature flag `Guard:QrVerifyEnabled` = true in test config

## Task 1: Domain Unit Tests

### File: `6_Tests/VanAn.Unit.Tests/Guard/VehicleSessionTests.cs`
- `Create_SetsStatusToIssued`
- `Create_SetsIdToVehicleSessionIdValue` (Single-Identity pattern)
- `Claim_FromIssued_TransitionsToClaimed`
- `Claim_FromClaimed_ThrowsInvalidOperationException` (INV-G06 — không ghi đè CustomerId)
- `Claim_FromVoided_ThrowsInvalidOperationException`
- `Claim_FromCheckedOut_ThrowsInvalidOperationException` (C→A migration fail khi vé đã dùng)
- `Claim_FromIssued_WithNullCustomerId_SetsCustomerId` (Channel C→A migration — INV-G05)
- `Checkout_FromClaimed_TransitionsToCheckedOut`
- `Checkout_FromIssued_TransitionsToCheckedOut` (paper ticket, no claim — Channel C direct)
- `Checkout_FromVoided_ThrowsInvalidOperationException`
- `Flag_FromIssued_TransitionsToFlagged`
- `Flag_FromClaimed_TransitionsToFlagged`
- `Void_FromIssued_TransitionsToVoided`
- `Void_FromCheckedOut_ThrowsInvalidOperationException`

## Task 2: GuardService Unit Tests

### File: `6_Tests/VanAn.Unit.Tests/Guard/GuardServiceTests.cs`
- `IssueAsync_ValidInput_CreatesSessionWithHashedToken`
- `IssueAsync_GeneratesUniqueShortCodePerTenantPerDay`
- `ClaimAsync_ByQrPayload_TransitionsToClaimed`
- `ClaimAsync_ByShortCode_TransitionsToClaimed`
- `ClaimAsync_AlreadyClaimed_Throws` (INV-G06)
- `ClaimAsync_ChannelCToA_PaperTicketClaimedLater_Succeeds` (INV-G05 — CustomerId null→set)
- `ClaimAsync_ChannelCToA_AlreadyCheckedOut_Throws` (vé đã dùng, không claim được)
- `ClaimAsync_ChannelCToA_AlreadyVoided_Throws` (vé hết hạn)
- `ClaimAsync_Voided_Throws`
- `VerifyAsync_ValidQr_ReturnsSessionWithPhotos`
- `VerifyAsync_VoidedQr_ReturnsError`
- `VerifyAsync_UnknownQr_ReturnsError`
- `CheckoutAsync_ValidSession_TransitionsToCheckedOut`
- `FlagAsync_ValidSession_TransitionsToFlagged`
- `VoidAsync_ValidSession_TransitionsToVoided`
- `GetTodaySessionsAsync_PaginatesCorrectly`
- `GetTodayStatsAsync_CountsCorrectly`
- `PresignUploadAsync_ReturnsTwoPresignedUrls`

## Task 3: API Integration Tests

### File: `6_Tests/VanAn.Integration.Tests/Guard/GuardControllerTests.cs`
- `POST_presign-upload_returns_presigned_urls` (200)
- `POST_issue_creates_session_returns_qr` (200)
- `POST_claim_by_qr_payload_claims_session` (200)
- `POST_claim_by_short_code_claims_session` (200)
- `POST_claim_already_claimed_returns_409` (409)
- `POST_claim_channel_c_to_a_null_customerId_then_claim_succeeds` (200 — C→A migration)
- `POST_claim_already_checked_out_returns_409` (409 — C→A fail, vé đã dùng)
- `POST_claim_already_voided_returns_409` (409 — C→A fail, vé hết hạn)
- `POST_verify_valid_qr_returns_session` (200)
- `POST_verify_unknown_qr_returns_404` (404)
- `POST_checkout_transitions_to_checked_out` (200)
- `POST_flag_transitions_to_flagged` (200)
- `GET_today_sessions_returns_paginated_list` (200)
- `GET_today_sessions_unauthorized_returns_401` (401)
- `GET_today_sessions_wrong_tenant_returns_empty` (multi-tenant isolation)
- `POST_issue_without_guard_role_returns_403` (403)

## Task 4: Playwright E2E (1 spec — Gate 4 compliance)

### File: `6_Testing/e2e-tests/guard-qr-verify.spec.ts`
**Flow:**
1. Login ShopERP as Guard (DevLoginController `#if DEBUG`)
2. Navigate to `/guard/scan` → Issue tab
3. Capture/upload plate photo + customer photo (use test images)
4. Enter plate number → click "Tạo QR"
5. Verify QR displayed + short code displayed
6. (Channel C — paper) — do NOT claim yet (simulate paper ticket only)
7. Switch to Verify tab → scan QR (paste payload — simulate scanning paper ticket)
8. Verify plate + 2 photos displayed
9. Click "Match — Check-out"
10. Verify success toast
11. Switch to Today tab → verify session shows CheckedOut
12. Verify stats updated (checkOut count +1)

**Channel C→A migration sub-flow (separate test or same spec):**
13. Issue new QR (step 2-5 again) — Channel C (no claim)
14. (Simulate customer opens KhachLink later) — API call `/api/guard/claim` with qrPayload + customerId
15. Verify claim succeeds (200) — CustomerId now set
16. (Simulate guard scans) — `/api/guard/verify` → verify `claimedBy` is populated
17. Checkout → verify success
18. (Edge case) Issue new QR → checkout first → then attempt claim → verify 409 (vé đã dùng)

**Playwright governance:**
- Single spec (max 1 per FIX_ONLY/IMPLEMENT session per rules)
- Run AFTER build pass + implementation complete
- Use `playwright_validation.md` workflow

## Task 5: CI verification
- Run full CI pipeline locally: `dotnet test` (all tests) + `guard-check.ps1`
- Confirm: 0 new failures, all existing tests still pass
- Confirm: new tests included in count

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `dotnet test` ALL PASS (existing + new)
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Playwright spec PASS
- [ ] CI pipeline ALL PASS (push to branch, verify GitHub Actions)

## Files Modified (expected)
1. `6_Tests/VanAn.Unit.Tests/Guard/VehicleSessionTests.cs` — NEW
2. `6_Tests/VanAn.Unit.Tests/Guard/GuardServiceTests.cs` — NEW
3. `6_Tests/VanAn.Integration.Tests/Guard/GuardControllerTests.cs` — NEW
4. `6_Testing/e2e-tests/guard-qr-verify.spec.ts` — NEW

## Rollback
- Tests are additive — no rollback needed
- If E2E fails: triage per `playwright_triage.md` (do NOT fix in this sprint unless trivial)

## Approval Gate
- [ ] All tests pass + CI green
- [ ] User approval → merge to main → deploy → RV
