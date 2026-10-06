# Vạn An Appointment Booking & Staff Scheduling Infrastructure
## SRS — Software Requirements Specification

**Document:** `van_an_appointment_booking_srs.md`  
**Version:** 1.1 MVP  
**Status:** MVP Implementation Baseline — Simplified Scheduling / Polling / Financial Compliance Boundary  
**Product Scope:** Spa / Hair Salon / Karaoke / Massage / Private Clinic / Similar Appointment-based Tenants  
**Primary Goal:** Convert a QR scan into a low-friction appointment/order, while giving the tenant a simple operational workflow for confirmation, primary-staff assignment, availability and commission attribution. MVP explicitly avoids persistent realtime connections, arbitrary multi-resource scheduling and audio storage.

---

## 1. Executive Summary

Vạn An cần một **Appointment Booking Infrastructure** dùng chung cho nhiều tenant có mô hình bán dịch vụ theo lịch hẹn.

Các nhóm tenant mục tiêu gồm:

- Spa
- Hair salon / barber
- Massage
- Karaoke
- Beauty / nail
- Private clinic / phòng mạch tư nhân
- Các dịch vụ khác cần đặt lịch theo khung giờ + nhân viên/kỹ thuật viên

Luồng cốt lõi:

`QR Code → Booking Form → Chọn 1 Appointment Offering / Combo → Chọn thời gian → Chọn staff hoặc Bất kỳ ai phù hợp → Quick Tags / Speech-to-Text → Chọn đặt cọc → Gửi lịch → Tenant xác nhận → Gán staff chính → Theo dõi trạng thái → Hoàn tất → Payment/Invoice integration → Tính hoa hồng`

### Nguyên tắc sản phẩm

1. **Customer-first:** khách mở form từ QR và đặt lịch trong vài thao tác.
2. **Tap-first:** ưu tiên chạm/chọn; hạn chế nhập bàn phím.
3. **No complex scrolling:** giao diện không được buộc người dùng scroll dài để hoàn tất booking.
4. **Real-time status:** sau khi gửi booking, khách phải thấy trạng thái xác nhận/cập nhật rõ ràng.
5. **Operational-first:** tenant phải nhìn được ai đang rảnh, ai bận và ai đang phụ trách lịch.
6. **Multi-tenant isolation:** dữ liệu booking, staff, service, QR và commission phải tuyệt đối isolate theo tenant.
7. **QR attribution:** QR có thể đại diện cho tenant + salesman/CTV/sales channel.
8. **Commission based on qualified outcome:** không tính hoa hồng chỉ vì khách scan QR hoặc tạo booking; mặc định tính theo điều kiện hoàn thành/thanh toán đã cấu hình.
9. **No fake completion:** backend phải có logic thực sự cho availability, reservation conflict, status transitions và commission ledger; không dùng stub để vượt acceptance test.

---

# 2. Scope

## 2.1 In Scope — MVP

### Customer side

- QR entry
- Tenant-branded booking form
- Chọn ngày
- Chọn khung giờ
- Chọn 1 appointment offering (service hoặc combo/package)
- Có thể chọn add-on đã cấu hình sẵn nếu add-on không tạo thêm resource constraint
- Hiển thị thời lượng và giá dự kiến
- Chọn nhân viên/kỹ thuật viên hoặc “Bất kỳ ai phù hợp”
- Xem người còn khả dụng
- Quick Tags
- Speech-to-Text tùy trình duyệt/device; không lưu audio ở backend
- Nhập text note khi speech-to-text không khả dụng
- Chọn đặt cọc nếu tenant yêu cầu/cho phép
- Xác nhận booking
- Nhận booking code
- Theo dõi trạng thái booking bằng HTTP polling nhẹ; không yêu cầu WebSocket/SignalR cho MVP
- Hủy / yêu cầu thay đổi theo policy của tenant

### Tenant side

- Quản lý service/package
- Quản lý service duration
- Quản lý staff
- Khai báo staff skill/service eligibility
- Cấu hình lịch làm việc
- Cấu hình ngày nghỉ / unavailable time
- Xem staff availability
- Xem lịch booking theo ngày/tuần
- Xác nhận / từ chối booking
- Gán / đổi staff
- Điều chỉnh trạng thái booking
- Theo dõi tiền cọc
- Quản lý commission rule
- Quản lý QR theo tenant/salesman/channel
- Xem attribution và commission ledger

### Platform / Vạn An

- Tenant isolation
- QR attribution infrastructure
- Booking state machine
- Availability engine
- Conflict prevention
- Event/audit log
- Commission ledger
- Notifications
- Reporting foundation

---

# 3. Non-Goals — MVP

Không triển khai thành yêu cầu bắt buộc trong MVP:

- Full accounting system
- Payroll
- HR performance management
- Advanced medical records
- Electronic medical record / diagnosis workflow
- Inventory management
- Full POS replacement
- Marketplace discovery
- Cross-tenant shared staff
- Dynamic pricing engine
- AI recommendation engine
- Persistent WebSocket/SignalR connection as an MVP dependency
- Arbitrary N-service constraint solver / parallel multi-staff scheduling
- Backend audio/object-storage pipeline for customer voice notes
- Full e-invoice engine or tax filing engine inside Booking module

Các tính năng này chỉ được thiết kế extension point, không được làm phức tạp MVP.

---

# 4. Actors & Roles

## 4.1 Customer

Có quyền:

- tạo booking
- xem booking của mình qua booking token/verification
- xem status
- ghi chú
- thanh toán deposit
- yêu cầu cancel/reschedule theo policy

Không có quyền:

- xem dữ liệu staff nội bộ ngoài thông tin cần thiết để đặt lịch
- xem commission
- xem dữ liệu khách khác

## 4.2 Staff / Technician / Hairdresser / Doctor / Performer

Có quyền tùy tenant:

- xem lịch được phân công
- xem thời gian làm việc của mình
- cập nhật trạng thái sẵn sàng nếu được phép
- nhận thông báo booking

Không được xem dữ liệu của tenant khác.

## 4.3 Tenant Admin / Owner / Manager

Có quyền:

- quản lý service
- quản lý staff
- quản lý working schedule
- xác nhận/reject booking
- gán staff
- quản lý deposit
- xem reports
- quản lý QR attribution
- quản lý commission rules

## 4.4 Salesman / CTV / Affiliate

Không nhất thiết là staff của tenant.

Có một hoặc nhiều attribution QR riêng.

Được xem tối thiểu:

- số lượt scan
- booking tạo ra
- booking qualified
- commission accrued
- commission paid

Không được xem dữ liệu khách ngoài phạm vi policy.

## 4.5 Platform Admin

Quản lý tenant, cấu hình hệ thống và audit/security nhưng không được bypass tenant isolation.

---

# 5. Customer UX Requirements

## 5.1 Core UX principle

Booking MVP phải có **tap-first interaction** và giải quyết một quyết định mỗi màn hình.

Mục tiêu:

- Không quá 4 màn hình chính.
- Không bắt buộc bàn phím cho các trường cấu trúc.
- Không yêu cầu customer scroll qua một long-form form.
- Các lựa chọn phổ biến dùng card/chip/button.
- Ghi chú ưu tiên Quick Tags; speech-to-text là progressive enhancement, không lưu file âm thanh.

## 5.2 Recommended screen architecture

### Screen 1 — Offering

Hiển thị:

- Tenant logo/name
- Category chips
- Appointment offering cards:
  - service đơn
  - combo/package có thời lượng cố định
- Có thể chọn optional add-ons nếu tenant cấu hình chúng là `non_scheduling_add_on`
- Giá
- Thời lượng tổng của appointment offering
- Sticky bottom summary

**MVP rule:** customer không được tự ghép một danh sách N service với scheduling logic tùy ý. N service chỉ xuất hiện cùng nhau thông qua package/combo đã được tenant cấu hình.

### Screen 2 — Time & Staff

Hiển thị:

- Ngày dạng horizontal date picker
- Các time slot khả dụng dạng large buttons
- Staff filter:
  - `Bất kỳ ai phù hợp`
  - Staff cụ thể
- Backend chỉ trả staff/time slot còn khả dụng.

MVP chỉ có **một primary staff assignment** cho một appointment. Không giải CSP cho nhiều staff song song.

### Screen 3 — Note & Deposit

Quick Tags dạng chips, ví dụ:

- `Phòng riêng`
- `Lần đầu đến`
- `Kỹ thuật viên nữ`
- `Cần chuẩn bị trước`

Voice input:

- tap microphone
- nói một câu ngắn
- client speech-to-text thành text
- cho phép sửa/xóa transcript
- nếu browser/device không hỗ trợ speech recognition → hiển thị text input
- **không upload hoặc lưu audio binary trong MVP**

Deposit:

- `Không đặt cọc`
- `Đặt cọc số tiền đã cấu hình`

### Screen 4 — Confirm

Hiển thị summary:

- tenant
- ngày/giờ
- offering/package
- add-ons nếu có
- total
- deposit
- staff nếu đã chọn
- note/quick tags
- policy hủy/reschedule

CTA lớn:

`XÁC NHẬN ĐẶT LỊCH`

Sau submit chuyển sang Booking Status.

# 6. Customer Interaction Rules

## 6.1 Avoid keyboard

Không yêu cầu customer nhập:

- service name
- staff name
- date
- time
- deposit amount

Chỉ text note/transcript là trường tự do thực sự cần thiết trong MVP.

## 6.2 Avoid excessive scrolling

Mỗi màn hình có một quyết định chính. Khi nhiều option:

- category filter
- compact horizontal chips
- load-more khi thật sự cần

Không render 100+ offering cards cùng lúc.

## 6.3 Touch target

CTA chính và option controls phải phù hợp mobile/touch interaction; khuyến nghị tối thiểu 44x44 CSS px.

## 6.4 Booking progress

Hiển thị context ngắn:

`Dịch vụ → Thời gian → Ghi chú → Xác nhận`

## 6.5 Error handling

Lỗi phải hướng dẫn hành động. Ví dụ conflict:

`Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.`

Không hiển thị mã kỹ thuật như `409 Conflict` cho customer.

# 7. QR Code Infrastructure

## 7.1 Purpose

QR là điểm vào chính để:

1. nhận diện tenant
2. nhận diện sales channel
3. nhận diện salesman/CTV
4. tạo attribution trước khi booking được tạo

## 7.2 QR identity

Mỗi QR phải có một opaque `qr_token`.

Không nhúng dữ liệu nhạy cảm hoặc commission rule trực tiếp trong QR.

Ví dụ concept URL:

`https://booking.vanan.vn/q/{qr_token}`

Backend resolve:

`qr_token → tenant_id + campaign_id + salesman_id + attribution_policy`

## 7.3 QR isolation rules

Khi resolve QR:

- token phải tồn tại
- token active
- tenant phải active
- salesman phải thuộc campaign/tenant được phép
- không được suy diễn tenant từ client input

Booking tạo ra phải gắn immutable attribution record.

## 7.4 Attribution snapshot

Khi customer scan QR:

create/update:

`BookingAttributionSession`

Fields tối thiểu:

- attribution_session_id
- tenant_id
- qr_id
- salesman_id nullable
- campaign_id nullable
- first_seen_at
- last_seen_at
- anonymous_session_id
- attribution_expiry_at

## 7.5 Attribution rules

Mặc định:

- first qualified QR attribution wins
- refresh không tạo attribution mới
- customer refresh/reopen cùng session không đổi salesman
- cross-tenant token contamination bị từ chối

Tenant có thể cấu hình sau MVP:

- first touch
- last touch
- explicit referral

Nhưng MVP chỉ implement một rule duy nhất để tránh ambiguity.

## 7.6 Attribution to booking

Khi booking được tạo:

`booking.attribution_id = current qualified attribution`

Snapshot các field commission-critical vào booking hoặc commission calculation context để historical records không bị thay đổi khi QR được chỉnh sửa về sau.

---

# 8. Booking Model

## 8.1 Booking aggregate

Một booking có:

- 1 tenant
- 1 customer/session identity
- 1 appointment start
- 1 appointment end/duration
- 1 primary appointment offering
- 0..N non-scheduling add-ons
- 0..1 primary staff assignment
- 0..1 deposit transaction hiện tại
- 0..1 attribution snapshot
- 0..N notes/events
- 0..1 financial/invoice integration status

## 8.2 Appointment offering

`AppointmentOffering` là đơn vị scheduling của MVP.

Một offering có:

- offering_id
- tenant_id
- offering_type = `SERVICE | PACKAGE`
- display_name
- duration_minutes_snapshot
- price_snapshot
- required_staff_skill
- active

Package có thể chứa nhiều dịch vụ con nhưng **thời lượng scheduling đã được tenant cấu hình trước**.

## 8.3 Add-on

`AddOn` chỉ được dùng trong MVP nếu nó không làm phát sinh scheduling/resource constraint mới.

Ví dụ phù hợp:

- nước uống
- sản phẩm bán thêm
- phụ thu cố định

Nếu add-on làm tăng thời lượng hoặc cần staff/resource thứ hai, tenant phải tạo package/offering riêng ở MVP.

## 8.4 Historical snapshot

Giá, tên và thời lượng phải snapshot tại thời điểm booking để historical records không bị thay đổi khi catalog thay đổi.

# 9. Booking State Machine

## 9.1 Customer-visible statuses

- `PENDING_CONFIRMATION` — Đang chờ shop xác nhận
- `CONFIRMED` — Đã xác nhận
- `STAFF_ASSIGNED` — Đã phân công
- `CHECKED_IN` — Đã đến
- `IN_SERVICE` — Đang phục vụ
- `COMPLETED` — Hoàn tất
- `CANCELLED` — Đã hủy
- `REJECTED` — Shop từ chối
- `NO_SHOW` — Không đến

## 9.2 Internal states

Có thể dùng internal sub-state cho:

- deposit_pending
- deposit_paid
- deposit_failed
- staff_assignment_pending
- reschedule_requested

Không tạo quá nhiều status top-level gây phức tạp UI.

## 9.3 Allowed transitions

`PENDING_CONFIRMATION → CONFIRMED`

`PENDING_CONFIRMATION → REJECTED`

`PENDING_CONFIRMATION → CANCELLED`

`CONFIRMED → STAFF_ASSIGNED`

`CONFIRMED → CANCELLED`

`STAFF_ASSIGNED → CHECKED_IN`

`STAFF_ASSIGNED → CANCELLED`  <!-- RV P6 decision 2026-10-06 (user approve): create-with-staff (khách chọn staff lúc đặt) → StaffAssigned ngay → khách/tenant PHẢI hủy được trước khi phục vụ. CheckedIn/InService vẫn không hủy. -->

`CHECKED_IN → IN_SERVICE`

`IN_SERVICE → COMPLETED`

`STAFF_ASSIGNED → NO_SHOW`

Invalid transition phải bị backend reject.

Frontend không được tự đổi trạng thái mà chưa có server acceptance.

---

# 10. Customer Status Updates — MVP Polling

## 10.1 Requirement

Sau submit:

- server tạo booking thật
- customer chuyển sang status page
- status page kiểm tra trạng thái định kỳ bằng HTTP GET

Ví dụ:

`Đã gửi → Shop đang xử lý → Đã xác nhận → Đã phân công`

## 10.2 Transport

MVP **không phụ thuộc persistent WebSocket/SignalR connection**.

Recommended polling:

- mỗi 5 giây trong tối đa 2 phút đầu
- sau 2 phút: mỗi 10–15 giây khi trang còn mở
- dừng polling khi booking vào terminal state: `COMPLETED | CANCELLED | REJECTED | NO_SHOW`
- customer luôn có nút `Làm mới`

Nếu backend đang có SignalR, không cần xóa infrastructure hiện hữu; nhưng Booking MVP không được phụ thuộc vào nó để đúng chức năng.

## 10.3 Conditional GET

Status endpoint nên hỗ trợ ETag/Last-Modified hoặc cơ chế tương đương nếu stack hiện tại có sẵn, để response khi không đổi được nhẹ.

## 10.4 Notifications

Push/SMS/Zalo là extension point Phase 2. Booking MVP không chặn hoàn tất chỉ vì notification provider bên ngoài lỗi.

# 11. Staff / Technician Scheduling — MVP

## 11.1 Staff profile

Mỗi staff có:

- staff_id
- tenant_id
- display_name
- role
- active
- avatar optional
- service skills
- working schedule
- leave/unavailable periods

## 11.2 Staff service eligibility

Staff chỉ được chọn cho offering nếu có capability/skill phù hợp với offering.

## 11.3 Working schedule

Hỗ trợ:

- weekday recurring schedule
- exact date override
- break time
- leave
- unavailable time

## 11.4 MVP availability rule

Staff được coi là `AVAILABLE` khi đồng thời:

- active
- có skill match
- nằm trong working interval
- không ở break/leave/unavailable
- không có booking conflict trong khoảng appointment

Không có load-balancing algorithm hoặc solver tối ưu trong MVP.

## 11.5 Availability API

API phải trả lời được:

`Trong khoảng 14:00–15:00 ngày X, staff nào có thể nhận offering Y?`

Customer chỉ cần nhận danh sách available. Tenant admin có thể xem lý do unavailable tối thiểu.

# 12. Double Booking Prevention

Đây là yêu cầu bắt buộc ở backend.

Khi create/confirm/assign appointment:

1. server re-check availability
2. thực hiện atomic conflict check/transaction phù hợp DB
3. persist assignment
4. commit

Nếu hai requests tranh cùng staff/time:

- tối đa một request thành công
- request còn lại nhận business conflict

Không dựa vào JavaScript/UI để khóa slot.

# 13. Staff Assignment UX — Tenant Backend

Manager mở booking → `Assign Staff`.

MVP hiển thị trước:

1. staff capability match + available
2. staff cụ thể nếu manager muốn chỉ định

Không cần load-balancing tự động.

Manual override luôn phải re-check conflict server-side.

# 14. “Who is Available?” Dashboard

Tenant có dashboard trả lời:

`Ai đang rảnh lúc 14:30 cho offering X?`

Kết quả tối thiểu:

| Staff | Skill | Time | Status |
|---|---|---|---|
| A | Massage 60m | 14:30 | Available |
| B | Massage 60m | 14:30 | Busy |
| C | Massage 60m | 14:30 | Break |

MVP không yêu cầu thuật toán tối ưu tải nhân viên.

# 15. Voice-to-Text Note — MVP

## 15.1 Functional requirements

Customer có thể:

- tap microphone
- nói ghi chú ngắn
- nhận transcript text
- chỉnh sửa transcript
- xóa transcript

Quick Tags phải là lựa chọn nhanh hơn và được hiển thị trước microphone.

## 15.2 Storage rule

**Không lưu audio binary ở backend trong MVP.**

Server chỉ lưu:

- note_id
- booking_id
- tenant_id
- source = `TEXT | SPEECH_TO_TEXT | QUICK_TAG`
- text_content
- created_at

## 15.3 Browser/device fallback

Speech recognition là progressive enhancement.

Nếu browser/device không hỗ trợ hoặc permission bị từ chối:

- không fail booking
- chuyển sang text input
- ghi nhận `source = TEXT`

Không được bắt customer upload file WAV/M4A chỉ để đáp ứng booking note.

## 15.4 Privacy

Note là customer-provided content. Không đưa sẵn các trường hỏi chẩn đoán hoặc thông tin sức khỏe không cần thiết.

Đối với tenant phòng mạch, note có thể chứa dữ liệu sức khỏe do khách tự nhập/nói; phải áp dụng policy dữ liệu nhạy cảm của platform/tenant và hạn chế quyền truy cập theo role.

# 16. Deposit, Payment & E-Invoice Integration Boundary

## 16.1 Deposit modes

Tenant có thể cấu hình:

- no deposit
- fixed amount
- configured percentage nếu payment engine hiện hữu hỗ trợ

## 16.2 Deposit legal classification

Không hard-code rằng mọi khoản `deposit` đều là doanh thu hoặc đều phải xuất hóa đơn tại thời điểm thu.

Booking/Payment phải phân biệt tối thiểu:

- `SECURITY_DEPOSIT` — khoản đặt cọc/bảo đảm thực hiện
- `PREPAYMENT_FOR_SERVICE` — tiền trả trước cho dịch vụ
- `FINAL_PAYMENT` — tiền thanh toán khi hoàn tất

Việc xác định thời điểm lập hóa đơn phải theo tax profile, bản chất giao dịch và quy định hiện hành của tenant, không do UI tự quyết.

Theo Nghị định 70/2025/NĐ-CP (có hiệu lực từ 01/06/2025), đối với cung cấp dịch vụ, thời điểm lập hóa đơn nói chung là thời điểm hoàn thành dịch vụ; nếu có thu tiền trước hoặc trong khi cung cấp dịch vụ thì thời điểm lập hóa đơn là thời điểm thu tiền, với ngoại lệ được quy định riêng cho một số khoản đặt cọc/tạm ứng của nhóm dịch vụ nhất định. Vì vậy Booking module phải lưu được bản chất khoản tiền và truyền dữ liệu cho lớp HĐĐT/kế toán thay vì hard-code một quy tắc chung cho mọi tenant. 

## 16.3 Payment state

- NOT_REQUIRED
- PENDING
- PAID
- FAILED
- PARTIALLY_REFUNDED
- REFUNDED
- FORFEITED

## 16.4 Invoice integration state

MVP không xây full e-invoice engine. Booking module chỉ cần:

- lưu payment/invoice-trigger facts
- emit domain event/outbox
- nhận provider reference/status
- expose trạng thái cho tenant

Fields tối thiểu:

- invoice_status = `NOT_REQUIRED | PENDING | ISSUED | FAILED | CANCELLED`
- invoice_trigger = `ON_PAYMENT | ON_COMPLETION | EXTERNAL_RULE | NOT_APPLICABLE`
- e_invoice_provider
- e_invoice_reference nullable
- invoice_error_code nullable
- invoice_issued_at nullable

**Quan trọng:** `invoice_trigger` là snapshot theo tax/integration policy tại thời điểm transaction; không cho frontend tự chọn để né nghĩa vụ thuế.

## 16.5 E-invoice applicability profile

Tenant phải có tax/invoice profile do admin/Accounting module cấu hình:

- legal_entity_type
- tax_method/profile
- e_invoice_mode
- direct_to_consumer_applicability
- provider_connection_status

SRS này không tự kết luận mọi Spa/Salon/Karaoke/Clinic đều thuộc cùng một chế độ HĐĐT máy tính tiền. Đặc biệt tenant phòng mạch cần được cấu hình/kiểm tra theo ngành nghề và chế độ thuế thực tế.

## 16.6 Financial event boundary

Các event tối thiểu:

`DepositPaid`
`PaymentCaptured`
`ServiceCompleted`
`InvoiceRequired`
`InvoiceIssued`
`InvoiceFailed`
`RefundCompleted`

Booking module là source of booking facts; Accounting/E-Invoice module là source of accounting/invoice state.

# 17. Commission / Sales Attribution

## 17.1 Principle

Commission không tính từ scan QR hoặc booking chưa đạt qualification.

MVP recommended qualification:

`COMPLETED + payment qualified + attribution valid`

## 17.2 Commission entities

### QRCode

- qr_id
- tenant_id
- salesman_id nullable
- campaign_id nullable
- token_hash/token reference
- active
- created_at
- revoked_at

### Salesman / Payee

Ngoài `salesman_id`, ledger phải biết **loại người nhận commission** để phục vụ thuế/chi trả:

- `EMPLOYEE`
- `INDIVIDUAL_CONTRACTOR`
- `BUSINESS_ENTITY`
- `OTHER`

Fields bổ sung tối thiểu:

- contract_type nullable
- tax_residency nullable
- tax_identity_ref nullable
- payment_account_ref nullable

### CommissionRule

- rule_id
- tenant_id
- service_id/offering_id nullable
- salesman_id nullable
- commission_type
- commission_value
- qualification_status

### CommissionLedgerEntry

- ledger_entry_id
- tenant_id
- booking_id
- salesman_id
- qr_id
- commission_rule_snapshot
- base_amount
- gross_commission_amount
- tax_rule_version nullable
- tax_withheld_amount
- net_commission_amount
- withholding_reason_code nullable
- currency
- state
- created_at
- finalized_at
- paid_at nullable

## 17.3 Current Vietnam withholding rule integration

Không hard-code `tax_withheld = gross * 10%` cho mọi payee.

Theo quy định đang có hiệu lực từ 01/07/2026, khoản chi tiền lương, tiền công, tiền thù lao, tiền chi khác cho cá nhân cư trú không ký hợp đồng hoặc ký HĐLĐ dưới 03 tháng mà mức chi trả từ **5 triệu đồng/lần trở lên** thì tổ chức/cá nhân trả thu nhập phải khấu trừ 10% trước khi trả; dưới 5 triệu đồng/lần thì có cơ chế khấu trừ 10% khi cá nhân yêu cầu. Trường hợp ký HĐLĐ từ 03 tháng trở lên áp dụng cơ chế khấu trừ theo quy định tương ứng thay vì coi mặc định là 10%. 

Vì vậy MVP cần một `TaxWithholdingPolicy`/tax-rule adapter có version, tối thiểu lấy các inputs:

- payee type
- residency
- contract type
- payment amount per occasion
- current tax rule version

Kết quả phải snapshot:

`gross → tax withheld → net payable`

SRS không biến Booking module thành engine tư vấn thuế; nó chỉ phải **lưu đủ facts và calculation snapshot** để Accounting/Payroll/Tax module xử lý đúng.

## 17.4 Commission states

- PENDING
- EARNED
- VOIDED
- PAID
- REVERSED

## 17.5 Historical immutability

Sau khi ledger entry finalized:

- rule snapshot không mutate
- tax snapshot không mutate
- điều chỉnh phải tạo adjustment/reversal entry mới

# 18. Multi-tenant Isolation

Đây là security boundary bắt buộc.

## 18.1 Every core entity must include tenant scope

Tối thiểu:

- tenant_id
- booking_id
- service_id
- staff_id
- qr_id
- attribution_id
- commission ledger
- voice note metadata

## 18.2 Backend rule

Không tin `tenant_id` từ frontend.

Tenant context phải được suy ra từ authenticated context hoặc trusted QR resolution/session.

Mọi repository/query phải enforce tenant scope.

## 18.3 Negative tests

Bắt buộc có automated tests:

- Tenant A không đọc booking Tenant B
- Tenant A không assign staff Tenant B
- QR Tenant A không tạo booking cho Tenant B
- salesman A không xem commission của salesman B ngoài scope được cấp
- public booking token của Tenant A không resolve dữ liệu Tenant B

---

# 19. Core Data Model

Các bảng/entity MVP khuyến nghị:

```text
Tenant
Customer
Salesman
ServiceCategory
Service
AppointmentOffering
AppointmentOfferingItem
AddOn
Staff
StaffService
StaffWorkingSchedule
StaffScheduleOverride
QRChannel
AttributionSession
Booking
BookingItem
BookingStaffAssignment
PaymentTransaction
InvoiceIntegrationRecord
BookingEvent
CommissionRule
CommissionLedgerEntry
```

## 19.1 Booking minimum fields

```text
booking_id
public_booking_code
tenant_id
customer_id nullable
attribution_id nullable
start_at
end_at
status
customer_note nullable
estimated_total
actual_total nullable
deposit_required
deposit_type nullable
payment_status
invoice_status
invoice_trigger
created_at
updated_at
version
```

`version` được dùng cho optimistic concurrency nếu stack hiện tại phù hợp.

---

# 20. API Requirements

Tên endpoint chỉ là baseline; Devin phải map vào API conventions hiện hữu, không tạo API song song vô lý.

## Public booking

```http
GET  /api/public/booking/qr/{qrToken}
GET  /api/public/tenants/{tenantId}/services
GET  /api/public/availability
POST /api/public/bookings
GET  /api/public/bookings/{publicBookingToken}
POST /api/public/bookings/{publicBookingToken}/cancel
# Không có audio upload endpoint trong MVP
# Voice-to-text gửi dưới dạng text note cùng create/update booking
```

## Tenant operations

```http
GET  /api/tenant/bookings
GET  /api/tenant/bookings/{id}
POST /api/tenant/bookings/{id}/confirm
POST /api/tenant/bookings/{id}/reject
POST /api/tenant/bookings/{id}/assign-staff
POST /api/tenant/bookings/{id}/change-staff
POST /api/tenant/bookings/{id}/check-in
POST /api/tenant/bookings/{id}/start
POST /api/tenant/bookings/{id}/complete
GET  /api/tenant/staff/availability
GET  /api/tenant/bookings/{id}/financial-status
GET  /api/tenant/staff/schedules
```

## QR / commission

```http
POST /api/tenant/qr-channels
GET  /api/tenant/qr-channels
POST /api/tenant/qr-channels/{id}/revoke
GET  /api/tenant/commission
GET  /api/salesman/commission
```

---

# 21. Idempotency & Concurrency

## 21.1 Create booking

`POST /bookings` must support idempotency key.

Recommended header:

```http
Idempotency-Key: <client-generated-uuid>
```

Same request retried due network issue must not create duplicate booking.

## 21.2 Confirm booking

Confirm endpoint must also be idempotent.

## 21.3 Staff assignment

Assigning same staff twice should not generate duplicate assignment records.

---

# 22. Booking Conflict Rules

Conflict check must consider:

- staff assignment
- booking start/end
- staff break
- leave
- service capability
- tenant operating hours

Optional future resources:

- room
- chair
- karaoke room
- massage bed
- device/equipment

The data model should leave extension point for resource booking but MVP need not implement it.

---

# 23. Notification & Event Model

Every significant transition emits an event.

Examples:

```text
BookingCreated
BookingConfirmed
BookingRejected
BookingStaffAssigned
BookingCancelled
DepositPaid
BookingCheckedIn
BookingStarted
BookingCompleted
CommissionEarned
CommissionReversed
```

Events should be auditable.

At minimum store:

- event_id
- tenant_id
- aggregate_type
- aggregate_id
- event_type
- actor_type
- actor_id nullable
- timestamp
- metadata

---

# 24. Audit Requirements

Tenant operations require audit log for:

- confirm
- reject
- change time
- assign staff
- reassign staff
- cancel
- change deposit policy
- change commission rule
- revoke QR

Audit log must record before/after values where relevant.

---

# 25. Public Booking Security

Public booking page is anonymous, therefore:

- QR token must be unguessable
- public booking code must not expose sequential internal ID
- customer status access must use signed/opaque token
- APIs must rate-limit public endpoints
- file upload must be restricted and validated

Không trả về:

- internal tenant IDs nếu không cần
- commission data
- internal staff conflicts
- private customer data của khách khác

---

# 26. QR Attribution Abuse / Fraud Controls

MVP cần chống mức cơ bản:

1. QR revoked → không tạo attribution mới.
2. QR belonging to tenant A cannot be attached to tenant B booking.
3. Multiple rapid scans không tạo vô hạn attribution sessions.
4. Refresh không chuyển attribution.
5. Booking cancellation không tạo commission.
6. Reversal sau refund/cancel phải reverse commission nếu rule yêu cầu.

Future:

- device fingerprint
- anti-self-referral
- suspicious velocity detection
- commission abuse scoring

Không overbuild MVP.

---

# 27. Analytics

MVP events cần đủ để trả lời:

### Funnel

`QR Scan → Booking Started → Service Selected → Time Selected → Booking Submitted → Confirmed → Completed`

### Salesman

`QR scans → bookings → qualified bookings → completed bookings → commission`

### Tenant

- booking volume
- confirmation rate
- cancellation rate
- no-show rate
- utilization by staff
- utilization by time slot
- revenue / completed booking

---

# 28. Performance Requirements

## Public booking

Target:

- initial usable UI ≤ 3 seconds on normal mobile 4G when backend healthy
- availability API p95 ≤ 500ms under target load
- booking create p95 ≤ 800ms excluding external payment latency

## Tenant dashboard

- booking list p95 ≤ 1 second for normal daily dataset
- availability query p95 ≤ 500ms under normal tenant load

Nếu hệ thống hiện tại có SLO khác, phải reconcile với baseline thay vì tạo một SLA độc lập.

---

# 29. Offline / Network Failure

Public booking không được báo “đã đặt” nếu server chưa persist successfully.

Nếu client mất mạng sau submit:

- giữ client-side request state
- retry với same idempotency key
- sau khi server acknowledge mới hiển thị booking created

Không tạo cảm giác booking thành công dựa trên client-only state.

---

# 30. Medical Tenant Considerations

Đối với phòng mạch tư nhân:

MVP chỉ quản lý:

- lịch hẹn
- dịch vụ/appointment type
- doctor/staff availability
- deposit/payment
- customer-provided note

Không coi appointment note là hồ sơ bệnh án.

Nếu sau này chứa thông tin y tế nhạy cảm:

- cần data classification
- access control cao hơn
- retention policy riêng
- audit sâu hơn

Không nên mở rộng phạm vi EMR trong module booking MVP.

---

# 31. Acceptance Criteria — Customer

## AC-C01 QR entry

Given customer scans QR của salesman A tại tenant T  
When landing page loads  
Then tenant T được resolve chính xác  
And attribution session gắn salesman A.

## AC-C02 Offering selection

Given tenant có nhiều appointment offerings  
When customer mở Screen 1  
Then customer chọn được 1 service hoặc 1 combo/package  
And nếu package chứa nhiều service thì duration đã được tenant cấu hình trước  
And customer không phải tự ghép N service với scheduling logic tùy ý.

## AC-C03 Availability

Given service Y cần staff skill Y  
When customer chọn 15:00  
Then chỉ staff capable + available mới được hiển thị.

## AC-C04 Race condition

Given chỉ còn 1 slot với staff A  
When customer 1 và customer 2 submit gần như đồng thời  
Then tối đa một booking được assign staff A cho slot đó.

## AC-C05 Voice-to-text note

Given customer chạm microphone và browser/device hỗ trợ speech recognition  
When customer nói xong  
Then transcript text được gắn đúng booking/tenant  
And không có audio binary nào phải được upload/lưu ở backend.

Given speech recognition không khả dụng  
When customer tiếp tục booking  
Then text input fallback được hiển thị  
And booking vẫn hoàn tất bình thường.

## AC-C06 Status

Given booking được submit  
When tenant confirms  
Then customer status page cập nhật `CONFIRMED` mà không cần tạo booking mới.

---

# 32. Acceptance Criteria — Tenant

## AC-T01 Booking queue

Manager thấy booking mới ở `PENDING_CONFIRMATION`.

## AC-T02 Confirm

Manager confirm booking → customer nhận status update.

## AC-T03 Assign

Manager chỉ assign staff có capability + available.

## AC-T04 Staff schedule

Manager xem được lịch staff theo ngày/tuần.

## AC-T05 Availability

Manager query được “ai đang rảnh” theo service/date/time.

## AC-T06 Audit

Mọi confirm/reject/reassign đều có audit event.

---

# 33. Acceptance Criteria — QR / Commission

## AC-Q01 Tenant isolation

QR của Tenant A không thể tạo booking attributable cho Tenant B.

## AC-Q02 Salesman attribution

Booking tạo qua QR salesman S phải preserve salesman attribution xuyên suốt booking lifecycle.

## AC-Q03 Cancellation

Booking cancelled before qualification → commission = 0 / not earned.

## AC-Q04 Completion

Booking completed + qualified payment → commission ledger entry được tạo.

## AC-Q05 Historical stability

Admin sửa commission rate mới không làm thay đổi commission history của booking cũ.

## AC-Q06 Revoked QR

QR revoked không được tạo attribution mới.

---

# 34. Required Automated Test Matrix

Devin MUST implement automated tests cho tối thiểu:

### Unit tests

- working schedule
- break/leave exclusion
- service/offering-to-staff skill matching
- fixed package duration
- availability calculation
- commission calculation
- attribution resolution
- state transition validation
- tax-withholding rule adapter using versioned test fixtures

### Integration tests

- create booking
- idempotent create
- confirm
- assign primary staff
- double booking prevention
- deposit/payment state
- invoice event/outbox generation
- QR attribution persistence
- commission finalization/reversal
- tenant isolation

### E2E tests

1. QR → offering → time → quick tag / speech-to-text fallback → booking
2. QR salesman → completed booking → commission
3. Two customers race for same staff/time
4. Tenant A vs Tenant B isolation
5. Tenant confirmation updates customer status through polling
6. Staff unavailable due to leave
7. Deposit/payment success → correct financial status
8. Invoice-required event generated according to tenant tax/integration profile
9. Commission withholding fixture for a qualifying individual payee

# 35. Definition of Done

Feature không được coi là hoàn thành chỉ vì UI render được hoặc frontend state đổi.

Done khi:

- DB persistence hoạt động thật
- server-side validation hoạt động
- double-booking protection hoạt động thật
- tenant isolation có automated tests
- QR attribution persist thật
- commission ledger có historical snapshot
- status polling hoạt động với terminal-state stopping
- payment/invoice integration state được persist thật
- tax rule snapshot được persist khi commission phát sinh khấu trừ
- E2E happy path + critical negative path pass
- không còn TODO/stub tại critical business path

Không yêu cầu để đạt MVP:

- SignalR/WebSocket
- audio object storage
- arbitrary multi-service scheduler
- load-balancing algorithm cho staff

# 36. Recommended Implementation Order — MVP

## Phase 1 — Core transaction

1. Tenant/service/staff model
2. AppointmentOffering + Package
3. Working schedule
4. Availability engine đơn giản
5. Booking aggregate + state machine
6. Customer booking UI

## Phase 2 — Operational backend

7. Tenant booking queue
8. Confirm/reject
9. Primary staff assignment
10. Staff day/week calendar
11. Who-is-available view
12. Double-booking protection

## Phase 3 — QR attribution

13. QR channel
14. Attribution session
15. Booking attribution snapshot
16. Salesman dashboard

## Phase 4 — Payment / financial boundary

17. Deposit/payment state
18. Financial events/outbox
19. InvoiceIntegrationRecord
20. External e-invoice provider adapter contract

## Phase 5 — Commission / tax snapshot

21. Commission rule
22. Commission ledger
23. Tax withholding rule adapter
24. Finalize/reverse/payable reporting

## Phase 6 — UX hardening

25. Quick Tags
26. Speech-to-text progressive enhancement
27. Polling performance optimization
28. Mobile E2E

## Phase 7 — Hardening

29. concurrency tests
30. tenant isolation tests
31. rate limiting
32. audit
33. performance test

# 37. Architecture Guidance for Devin

## 37.1 Reuse existing platform primitives

Devin MUST first inspect the existing Vạn An codebase for:

- tenant identity/context
- authentication/authorization
- CoreHub APIs
- PostgreSQL patterns
- notification/realtime infrastructure (reuse if present, but not required for MVP)
- file/storage infrastructure only if already needed elsewhere
- QR utilities
- notification infrastructure
- audit/event infrastructure

Không tự tạo framework thứ hai nếu infrastructure hiện tại đã có primitive tương đương.

## 37.2 Source of truth

Booking state, staff availability, assignment và commission phải được authoritative ở backend/database.

Frontend chỉ là projection.

## 37.3 Avoid duplicated business logic

Không implement availability một kiểu ở frontend và một kiểu khác ở backend.

Rule phải có một authoritative domain implementation; frontend chỉ sử dụng API response.

---

# 38. UX Guardrails for Implementation

Devin không được “đơn giản hóa” UX thành một long-form form.

Bắt buộc giữ:

- service cards/chips
- date/time selection
- staff availability
- voice record action
- clear sticky summary
- confirmation screen
- status page

Không được yêu cầu customer nhập thủ công các lựa chọn đã có trong catalog.

Không thêm 5–10 trường profile trước khi cho phép booking nếu business requirement không cần thiết.

Booking phải ưu tiên:

`Chọn → Chọn → Ghi âm (nếu cần) → Xác nhận`

thay vì:

`Nhập → Nhập → Nhập → Scroll → Nhập → Submit`.

---

# 39. Example Booking Flow

Customer scan QR của Salesman S:

```text
QR_S
  ↓
Resolve QR
  ↓
Tenant A + Salesman S
  ↓
Booking Form
  ↓
Select: Combo Massage + Foot Care (fixed 90m)
  ↓
Select: 14:30
  ↓
Available Staff:
  - A ✅
  - B ✅
  - C ❌ Busy
  ↓
Choose A
  ↓
Quick tag: “Phòng yên tĩnh”
  ↓
🎙 Speech-to-text: “Em muốn phòng yên tĩnh.” (text only)
  ↓
Deposit: 100,000đ
  ↓
Confirm
  ↓
Booking #VA-8F23
Status = PENDING_CONFIRMATION
  ↓
Tenant Manager confirms
  ↓
Status = CONFIRMED
  ↓
Staff A assigned
  ↓
Status = STAFF_ASSIGNED
  ↓
Service completed
  ↓
Status = COMPLETED
  ↓
Commission qualification check
  ↓
CommissionLedgerEntry = EARNED
```

---

# 40. Critical Risks to Avoid

## Risk 1 — QR chỉ là link marketing

Không đạt yêu cầu. QR phải là attribution primitive có transaction linkage.

## Risk 2 — Availability chỉ kiểm tra bằng JavaScript

Không an toàn. Backend phải enforce.

## Risk 3 — Customer thấy tất cả staff rồi backend mới báo conflict

UX tệ. Availability endpoint phải lọc server-side.

## Risk 4 — Commission tính ngay khi booking tạo

Dễ gian lận và phải reverse hàng loạt. Dùng qualification state.

## Risk 5 — Tenant ID đến từ frontend

Có nguy cơ cross-tenant access. Tenant context phải trusted.

## Risk 6 — Lưu audio voice note thành một backend subsystem

Không cần cho MVP. Dùng Quick Tags + Speech-to-Text text only.

## Risk 7 — Arbitrary N-service scheduling

Dễ biến MVP thành CSP engine. Dùng AppointmentOffering/Package với duration cố định.

## Risk 8 — Không phân biệt deposit với prepayment

Có thể làm sai logic hóa đơn/kế toán. Payment phải lưu classification và financial trigger facts.

## Risk 9 — Hard-code 10% TNCN cho mọi salesman

Sai đối tượng. Phải dùng tax rule adapter theo payee/contract/residency/payment threshold.

## Risk 10 — Long form

Đi ngược core UX requirement. Form phải task-oriented, multi-step ngắn.

## Risk 11 — Booking module tự trở thành hệ thống HĐĐT/kế toán

MVP chỉ cần financial facts + event/outbox + provider integration boundary; Accounting/E-Invoice là module chuyên trách.

---

# 41. Suggested MVP Success Metrics

Sau pilot, theo dõi:

- QR scan → booking start conversion
- booking completion rate
- median booking completion time
- percentage of bookings completed without keyboard
- confirmation response time
- staff assignment time
- double-booking incidents = 0
- booking cancellation/no-show rate
- QR-attributed completed bookings
- commission accuracy

Đặc biệt theo dõi:

**Median time from QR scan → successful booking**

Đây là KPI UX quan trọng nhất của module.

---

# 42. Final Product Principle

Module này không nên được xây như một “online form đặt lịch”.

Nó phải được coi là:

> **Transaction + Scheduling + Attribution Infrastructure cho các doanh nghiệp bán dịch vụ theo lịch hẹn.**

Customer side tối giản đến mức:

`Scan QR → Chọn service/combo → Chọn giờ → Quick Tag / Nói để chuyển thành text → Đặt cọc (nếu có) → Xác nhận`

Tenant side mạnh về vận hành:

`Nhận booking → Xác nhận → Biết ai rảnh → Phân công → Theo dõi → Hoàn tất → Tính commission`

Và platform side phải bảo đảm:

`Tenant Isolation + Availability Integrity + QR Attribution + Payment/Invoice Facts + Auditability + Commission Ledger`.

---

# 43. Vietnam Legal / Compliance Baseline — MVP

## 43.1 Electronic invoices

Nghị định 70/2025/NĐ-CP sửa đổi Nghị định 123/2020/NĐ-CP có hiệu lực từ 01/06/2025 và quy định về hóa đơn điện tử khởi tạo từ máy tính tiền đối với các nhóm người bán đủ điều kiện. Hệ thống vì vậy phải thiết kế một financial/invoice integration boundary, nhưng không được suy luận rằng tất cả tenant đều có cùng chế độ hóa đơn. 

Đối với dịch vụ, quy tắc thời điểm lập hóa đơn và trường hợp thu tiền trước phải được xử lý theo quy định hiện hành và bản chất khoản tiền. Booking system chỉ thu thập/snapshot facts; module HĐĐT/kế toán quyết định và thực thi nghiệp vụ hóa đơn theo tax profile. 

## 43.2 Personal income tax on commission

Từ 01/07/2026, Nghị định 253/2026/NĐ-CP quy định cơ chế khấu trừ đối với cá nhân cư trú không ký hợp đồng hoặc ký HĐLĐ dưới 03 tháng: mức chi trả từ 05 triệu đồng/lần trở lên thì khấu trừ 10%; dưới 05 triệu đồng/lần có cơ chế khấu trừ 10% khi cá nhân yêu cầu. Vì vậy commission ledger phải snapshot gross / withholding / net và lý do áp dụng rule, nhưng không hard-code 10% cho mọi payee. 

## 43.3 Personal data

Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 có hiệu lực từ 01/01/2026. MVP phải tuân nguyên tắc data minimization: chỉ thu thập dữ liệu cần cho booking, attribution, payment và các nghĩa vụ liên quan; không mở rộng booking note thành hồ sơ y tế/EMR. 

> Legal rules may change. Devin must not encode legal assertions as scattered constants. Put tax/invoice rules behind versioned configuration/adapters owned by the relevant Accounting/Tax integration module.

# 44. Devin Execution Instruction

Before coding, Devin MUST:

1. Inspect existing architecture and identify reusable tenant/auth/database/realtime/storage primitives.
2. Produce a short implementation plan mapping this SRS to actual repository modules/files.
3. Identify any existing module that conflicts with this design.
4. Implement backend/domain rules before UI shortcuts.
5. Add automated tests for critical business rules before declaring feature complete.
6. Run existing project guardrails, build, unit tests and E2E tests relevant to booking.
7. Report any requirement that cannot be implemented exactly, with the concrete repository/runtime constraint and the least-invasive alternative.

Devin MUST NOT:

- stub availability
- fake commission calculation
- hard-code one staff member
- bypass tenant isolation
- trust client tenant_id
- declare booking success before server persistence
- implement commission only in frontend
- reintroduce audio storage just to simulate voice input; MVP voice input is text-only speech recognition with typed fallback
- remove QR attribution to simplify MVP
- bypass concurrency protection

**End of SRS v1.1 MVP**
