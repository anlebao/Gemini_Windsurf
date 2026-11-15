# Task ATRS-7: Risk & Policy Engine (FR-010, §9)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-2, ATRS-3 · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-010, §9.1 evidence classification, §9.2 R0-R4, §9.3 anti-abuse

## 1. OBJECTIVE

Policy engine deterministic (không AI/ML) đánh giá risk cho mọi hành động nhạy cảm: login, add device, recovery, dispute adjust, security setting change. Output có reason codes + policy version.

## 2. THIẾT KẾ

### 2.1 `IRiskPolicyEngine`
```csharp
Task<RiskDecision> EvaluateAsync(RiskContext ctx);  // ctx: subjectId, action, deviceId, signals, accountValue
```
- Input signals (từ ATRS-2 events + device registry + ledger refs):
  - Thiết bị mới / credential mới / nhiều auth fail gần đây
  - Recovery ngay sau đổi liên hệ; yêu cầu thay đổi bảo mật
  - Yêu cầu chuyển điểm/giá trị ví; nhiều recovery request ngắn hạn
  - Thiết bị LOST/REVOKED; đăng nhập bất thường (tín hiệu tối thiểu hợp pháp)
- Account value signal: điểm/ví balance bucket (đọc qua `ILoyaltyPointLedgerService.GetBalanceAsync` — KHÔNG tự tính), cờ "có ví/giá trị đáng kể" từ config ngưỡng.

### 2.2 Output
`{ Decision: ALLOW | ALLOW_WITH_STEP_UP | REQUIRE_MANUAL_REVIEW | TEMPORARY_RESTRICT | DENY, ReasonCodes[], PolicyVersion, MatchedRules[] }`
- Mỗi quyết định PHẢI có reason code + policy version (FR-010) — lưu vào `AccountSecurityEvents`/case.
- Rules = config (DB `SecurityPolicies` hoặc appsettings versioned) — **không hard-code ngưỡng** (SRS §9.2 note + §17.2).

### 2.3 R0-R4 mapping (§9.2)
- R0 routine → ALLOW; R1 new device → ALLOW_WITH_STEP_UP + notify; R2 assisted recovery → step-up + có thể delay; R3 high risk → REQUIRE_MANUAL_REVIEW + maker-checker; R4 suspected takeover → TEMPORARY_RESTRICT hành động nhạy cảm + investigate.
- Ngưỡng điểm/giá trị ví, delay time, số lần thử = config theo tenant/global policy (A21 policy screen consume sau — ATRS-10).

### 2.4 Anti-abuse hooks (§9.3)
- Rate limit per subject + per IP-signal (dùng pattern `MerchantSignupOtpService` cooldown/daily limit).
- Lockout tăng dần, không khóa vĩnh viễn (DoS).
- Thêm device mới KHÔNG tự xóa device cũ; recovery success → revoke theo policy (gọi ATRS-3).
- Không lộ ngưỡng/rules cho end user (chỉ reason code an toàn).

## 3. FILES (dự kiến)

- `vanan-atrs` hoặc CoreHub: `RiskPolicyEngine`, `SecurityPolicy` config entity/seed, `RiskContext`/`RiskDecision` records
- Consumers: ATRS-8 (recovery), ATRS-9 (dispute adjust), ATRS-5 (passkey ops), login hooks

## 4. TESTS

- [ ] Mỗi rule: input → expected decision + reason code (bảng test theo §9.2)
- [ ] Policy version ghi vào decision; đổi config → version đổi
- [ ] Không decision nào thiếu reason code
- [ ] Account value bucket ảnh hưởng đúng (R2→R3 khi vượt ngưỡng)
- [ ] Rate limit/lockout progressive; không permanent lock

## 5. ACCEPTANCE

- [ ] Engine evaluate được offline (unit) + wired vào ≥1 action thật (recovery submit)
- [ ] Admin có thể đọc policy version đang active (cho A21 sau)

## 6. RISKS

- Tín hiệu "đăng nhập bất thường" — tránh fingerprint xâm lấn; chỉ IP/UA-change/device-new ở mức tối thiểu hợp pháp (NFR-002).
- False positive R3/R4 → UX recovery bị chậm; cần config + manual review path mượt (A16).
