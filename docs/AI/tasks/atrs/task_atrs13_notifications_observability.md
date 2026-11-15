# Task ATRS-13: Security Notifications + Observability

> **Master plan:** §4 · **Phụ thuộc:** ATRS-8, ATRS-9 · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-011 (notify), FR-015 (security notifications), §9.3, NFR-006, UI §10.4, §12.10-13

## 1. OBJECTIVE

Thông báo bảo mật đa kênh + metrics/alerting cho ATRS: mọi sự kiện nhạy cảm có notification phù hợp, không chứa bí mật, tôn trọng kênh user đã đăng ký.

## 2. THIẾT KẾ

### 2.1 Notification events → templates
| Event | Kênh | Nội dung (không bí mật) |
|---|---|---|
| Device registered/renamed/revoked/lost | email (nếu có) + in-app | device name, time, app source |
| Passkey created/used/removed | email + in-app | credential nickname, device |
| Recovery initiated/approved/rejected/completed | registered contact + in-app | case ref, status — KHÔNG chứa evidence/secret |
| Security setting changed | registered contact | what changed |
| Step-up success/fail (bat thường) | in-app + email | action, device, outcome |
| Dispute status change | in-app + contact | case ref, new status |
| Staff access sensitive case | audit only (không notify user chi tiết) | — |

- Kênh: `INotificationService` (email/SMS/push — reuse) + `INotificationCenterService` (in-app inbox `TenantNotification`).
- **Verify kênh khả dụng thực tế** (gap analysis: customer chủ yếu phone → SMS provider? email nếu `Customer.Email`; staff → email Brevo). Nếu kênh thiếu → card này ghi gap + đề xuất (P4 email-first đã chốt cho identity).
- Rule: thông báo không chứa mã/secret/đáp án; link tới status page an toàn.

### 2.2 Observability (NFR-006)
- Structured logging + correlation ID (đã có pattern) — KHÔNG PII/secret trong telemetry.
- Metrics: recovery created/approved/rejected, device registrations, secret-challenge failures, dispute backlog, lockouts, step-up failures.
- Alerts: spike failed auth/recovery/device-add (threshold config).
- Dashboard: reuse Grafana/metrics infra hiện có nếu có; tối thiểu logs + counters.

## 3. FILES (dự kiến)

- Notification template registry + dispatcher (có thể trong `vanan-atrs` hoặc reuse `CompositeNotificationService`)
- `AccountSecurityEvents` emit points (đã spec ATRS-2; card này đảm bảo mọi event quan trọng → notify đúng)
- Metrics instrumentation + alert config

## 4. TESTS

- [ ] Mỗi event → đúng template + đúng kênh theo preference
- [ ] Không secret/code/evidence trong payload notification (grep + test)
- [ ] User tắt kênh → không gửi kênh đó (nếu preference tồn tại)
- [ ] Metrics counters tăng đúng; alert rule fire trên spike giả lập

## 5. ACCEPTANCE

- [ ] User nhận được notify khi recovery/dispute đổi trạng thái (RV prod 1 case thật)
- [ ] NFR-006 dashboard/metrics tồn tại

## 6. RISKS

- SMS/ZNS chưa sẵn sàng → recovery notify cho customer không email chỉ còn in-app (yếu khi mất device) → **cần verify + có thể phụ thuộc P4**.
- Email Brevo free 300/ngày — volume notification phải ước tính, fallback queue.
