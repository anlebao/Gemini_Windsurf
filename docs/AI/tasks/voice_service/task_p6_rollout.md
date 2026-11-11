# Task P6 — PRODUCTION ROLLOUT: Voice Service (`vanan-voice`)

> **Status:** PENDING — chờ P5 pilot pass Gate 1
> **Created:** 2026-11-11 · **Phase:** P6 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 §12.2 (infra by phase), §17 (scaling gates), §18 (acceptance)

---

## 1. Mục tiêu

Rollout 100 → 1.000 → 10.000 merchants, mỗi mốc qua đúng scaling gate. Không bao giờ tăng scale chỉ vì số merchant đăng ký (SRS §18) — scale theo concurrent calls + TURN minutes đo được.

## 2. Các mốc (SRS §12.2, §17)

### Gate 1: 100 merchants

- Đã đạt ở P5 (85% P2P, TURN fallback, không regression).
- Mở rộng từ 10 → 100 merchants; theo dõi metrics liên tục.

### Gate 2: 1.000 merchants

- Điều kiện: 20 concurrent-call test pass, SignalR stable, TURN bandwidth monitored, restart reconciliation verified, không degradation Gateway/CoreHub.
- Infra: 1 Coturn node (2 vCPU/4GB), Gateway/module vẫn single instance — **chưa Redis** (SRS §3.4).

### Gate 3: 10.000 merchants

- Điều kiện: 100 concurrent-call test (capacity plan ~190 peak + 2× headroom), multiple Coturn nodes + TURN allocation tested, **Redis backplane + horizontal module instances** (đây là trigger chính thức — SRS §3.4/§12.2), security & abuse testing (§16.3 SRS), cost-per-call-minute report, §13.2 re-run với TURN % thật.

## 3. Hoạt động bắt buộc mỗi mốc

1. Load test đúng mốc (concurrent calls + signaling users riêng — SRS §16.4).
2. Rollback procedure chạy thử (module + Coturn + Redis nếu có).
3. Cost review hàng tháng (per 1.000 call-minutes + TURN %).
4. Alert thresholds cấu hình lại theo số đo thật (SRS §15 alerts).
5. Cập nhật project_state + SRS nếu phát sinh lệch.

## 4. Non-negotiables (SRS §21 — nhắc lại)

- Không cắt call giữa chừng vì hết credit; metering chết không được làm voice chết (fail-open).
- Không duplicate metering data; không thêm bảng ngoài `voice_calls`/`voice_call_events` khi chưa qua Gate review.
- Mọi infra change có rollback.

## 5. Output

- Kết quả từng gate (load test, cost report, security review) + quyết định tiếp theo.
- Deployment + rollback guide cập nhật theo từng mốc.