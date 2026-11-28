SRS — Vạn An Membership Infrastructure

Document: van-an-membership-infrastructure-srs.md
Version: 1.0
Status: Proposed
Product: Vạn An Ecosystem
Module: Membership Infrastructure
Primary Users: Participant / Applicant / HTX Administrator / HTX Management / Vạn An Platform Administrator

1. Executive Summary
1.1 Purpose

Vạn An Membership Infrastructure là nền tảng hạ tầng số dùng để:

thu hút người tham gia vào mạng lưới Vạn An;
hỗ trợ đăng ký gia nhập HTX;
số hóa hồ sơ thành viên;
xác thực danh tính;
quản lý consent;
quản lý workflow xét duyệt;
quản lý Member ID;
duy trì Sổ đăng ký thành viên điện tử của từng HTX;
quản lý vốn/phí thành viên;
quản lý trạng thái thành viên;
cung cấp API cho Commerce, Loyalty, CRM, Accounting, Procurement và các module khác.

Mục tiêu chiến lược:

Một người dùng → một định danh Vạn An → có thể tham gia nhiều HTX → không phải khai lại thông tin.

2. Strategic Principle
2.1 Vạn An không sở hữu thành viên của HTX

Kiến trúc phải phân biệt:

VẠN AN NETWORK ID
        │
        ├──────── Participant
        │
        ├──────── Applicant
        │
        ├──────── HTX A Member
        │
        ├──────── HTX B Member
        │
        └──────── HTX C Member

Trong đó:

Vạn An Network ID

Là định danh nền tảng.

HTX Member ID

Là định danh thành viên của một HTX cụ thể.

Không được coi hai khái niệm này là một.

3. Legal & Governance Boundary
3.1 Nguyên tắc
Vạn An Platform
     │
     │ cung cấp infrastructure
     ▼
HTX
     │
     │ quyết định
     ▼
Admission / Membership

Vạn An không tự quyết định:

ai được kết nạp;
ai bị từ chối;
loại thành viên pháp lý của HTX;
mức vốn góp;
quyền biểu quyết;
chấm dứt tư cách thành viên.

Các quyết định này thuộc HTX theo Luật và Điều lệ.

Luật HTX 2023 quy định thành viên gồm thành viên chính thức, thành viên liên kết góp vốn và thành viên liên kết không góp vốn; đồng thời thành viên có thể đồng thời là thành viên của nhiều HTX nếu Điều lệ không quy định khác.

4. Product Objectives
4.1 Primary Objectives
O1 — Frictionless onboarding

Người bán hàng rong hoặc hộ kinh doanh có thể bắt đầu hồ sơ trong:

≤ 2 phút

trên điện thoại.

O2 — Legal workflow

Đảm bảo hồ sơ có đầy đủ:

thông tin cần thiết;
đơn tự nguyện gia nhập;
xác nhận Điều lệ;
thông tin vốn/phí;
trạng thái xét duyệt;
audit trail.
O3 — Electronic Member Registry

Cho phép HTX quản lý:

Sổ đăng ký thành viên điện tử

thay vì phụ thuộc vào Google Sheets hoặc hồ sơ rời rạc.

Luật HTX 2023 quy định sổ đăng ký thành viên chính thức và thành viên liên kết góp vốn có thể được lập dưới dạng văn bản giấy hoặc dữ liệu điện tử.

O4 — Multi-HTX

Một người có thể tham gia nhiều HTX mà không phải tạo tài khoản mới.

O5 — Scale

Kiến trúc phải hỗ trợ:

1 HTX
→ 10 HTX
→ 100 HTX
→ 1,000 HTX
→ 10,000 HTX

mà không cần nhân sự Vạn An tăng tuyến tính.

5. Non-Goals

Module này không chịu trách nhiệm:

quản trị HTX;
thay thế Hội đồng quản trị;
thay thế Đại hội thành viên;
quyết định kết nạp;
quyết định phân phối lợi nhuận;
thay thế hệ thống kế toán;
trở thành ví điện tử;
giữ tiền vốn góp của thành viên;
xử lý thanh toán nếu không có giấy phép/cơ chế pháp lý phù hợp;
tự động coi một người là thành viên chỉ vì họ submit form.
6. User Types
6.1 Participant

Người đã tham gia mạng lưới Vạn An nhưng chưa là thành viên HTX.

Ví dụ:

khách hàng;
người bán hàng rong;
CTV;
shipper;
supplier;
resident.
6.2 Applicant

Người đã gửi yêu cầu gia nhập HTX.

Participant
     ↓
Applicant

Nhưng:

Applicant ≠ Member

6.3 Official Member

Người đã được HTX công nhận theo quy trình và đáp ứng nghĩa vụ tương ứng.

Applicant
     ↓
HTX Approval
     ↓
Official Member
6.4 HTX Membership Officer

Nhân sự xử lý hồ sơ.

Permissions:

xem hồ sơ;
yêu cầu bổ sung;
xác minh;
đề xuất duyệt;
cập nhật trạng thái.

Không mặc định có quyền quyết định cuối cùng.

6.5 HTX Management

Người có thẩm quyền theo Điều lệ/quy chế HTX.

Có thể:

approve;
reject;
suspend;
terminate;
thay đổi trạng thái theo thẩm quyền.
6.6 Vạn An Platform Administrator

Chỉ quản trị hạ tầng.

Không được tùy tiện:

approve member;
sửa hồ sơ pháp lý;
thay đổi vốn góp;
thay đổi quyền biểu quyết.

Mọi thao tác đặc quyền phải có audit log.

7. Membership State Machine

Đây là thành phần cốt lõi.

                         ┌───────────────┐
                         │  PARTICIPANT  │
                         └───────┬───────┘
                                 │
                          Start Application
                                 │
                                 ▼
                         ┌───────────────┐
                         │   APPLICANT   │
                         └───────┬───────┘
                                 │
                    ┌────────────┼────────────┐
                    │            │            │
                    ▼            ▼            ▼
               Need Info     Rejected     Submitted
                    │                         │
                    └──────────┐              ▼
                               │        HTX Review
                               │              │
                               │       ┌──────┴──────┐
                               │       ▼             ▼
                               │    APPROVED       REJECTED
                               │       │
                               │       ▼
                               │  Capital/Fee
                               │       │
                               │       ▼
                               │   ACTIVE MEMBER
                               │
                               ▼
                         Application Updated

Sau đó:

ACTIVE
  │
  ├── SUSPENDED
  │
  ├── RESIGNED
  │
  └── TERMINATED

Không được xóa member record vật lý nếu việc xóa làm mất lịch sử pháp lý/audit cần lưu giữ.

8. Core User Journey
8.1 Entry Points

Người dùng có thể bắt đầu từ:

QR Code;
Zalo OA;
Vạn An App;
Web;
CTV invitation;
HTX invitation;
trực tiếp tại điểm HTX.
9. QR Onboarding

QR phải chứa:

HTX ID
+
Campaign/Source ID
+
Optional Referrer ID

Ví dụ:

join.vanan.vn/hx123?ref=CTV789

Backend xác định:

Target HTX = HX123
Referrer = CTV789

Không yêu cầu người dùng tự chọn HTX nếu QR đã xác định HTX.

10. Mobile Onboarding UX
Screen 1 — Welcome
Tham gia mạng lưới kinh tế khu vực

[Tham gia ngay]

Hiển thị:

tên HTX;
khu vực;
mục đích;
quyền lợi chính;
thông tin cơ bản.
Screen 2 — Phone Verification
Số điện thoại

[Nhận mã OTP]

OTP xác nhận ownership của số điện thoại.

Screen 3 — Basic Profile

Tối thiểu:

Họ tên;
số điện thoại;
khu vực;
vai trò dự kiến.

Ví dụ:

☐ Người bán
☐ CTV/Sales
☐ Shipper
☐ Supplier
☐ Khách hàng
☐ Khác
11. Identity Verification
Principle

Không yêu cầu thu thập toàn bộ giấy tờ ngay ở bước đầu nếu chưa cần thiết.

Workflow:

Basic onboarding
       ↓
Determine membership type
       ↓
Determine legal requirements
       ↓
Identity verification if required

Có thể hỗ trợ:

Level 1

OTP.

Level 2

Thông tin định danh cá nhân.

Level 3

Identity verification/KYC phù hợp.

Level 4

Manual verification.

12. CCCD / Personal Data

Hệ thống không mặc định lưu ảnh CCCD chỉ vì người dùng đăng ký.

Nếu cần:

Collect
 ↓
Encrypt
 ↓
Access Control
 ↓
Retention Policy
 ↓
Delete/Archive according to policy

Phải ghi rõ:

mục đích thu thập;
dữ liệu nào được thu thập;
ai có quyền truy cập;
thời hạn lưu;
mục đích sử dụng;
cơ chế xử lý yêu cầu của chủ thể dữ liệu.

Không sử dụng Google Drive/Google Sheets làm kho dữ liệu định danh production.

13. Membership Application

Application phải chứa tối thiểu:

Application ID
Vạn An Network ID
HTX ID
Membership Type
Applicant Profile
Application Date
Consent Version
Charter Version
Identity Verification Status
Capital/Fee Status
Application Status
14. Digital Consent

Người dùng phải được xem:

Điều lệ HTX phiên bản nào;
điều kiện gia nhập;
quyền;
nghĩa vụ;
vốn/phí;
chính sách dữ liệu;
các điều khoản liên quan.

Hệ thống lưu:

Consent ID
User ID
Document ID
Document Version
Timestamp
IP/device metadata where appropriate
Consent Type
Evidence Reference

Mục tiêu là:

Có thể chứng minh người dùng đã xác nhận phiên bản tài liệu nào tại thời điểm nào.

Luật Giao dịch điện tử 2023 quy định về thông điệp dữ liệu và chữ ký điện tử; do đó module phải tách “electronic evidence/consent” khỏi khái niệm “chữ ký điện tử đáp ứng một yêu cầu pháp lý cụ thể”. Không được thiết kế giả định rằng chữ ký vẽ tay trên màn hình luôn tương đương chữ ký số.

15. Application Review

Dashboard HTX:

Applications
────────────────────────

NEW             128
NEED_INFO        21
UNDER_REVIEW     43
APPROVED         64
REJECTED          7

Mỗi application:

Applicant
Identity
Membership Type
Documents
Consent
Capital/Fee
Review History

Actions:

[Approve]

[Request More Information]

[Reject]
16. Approval Workflow

Không dùng:

Submit = Member

Mà:

Submit
 ↓
Validation
 ↓
HTX Review
 ↓
Approval
 ↓
Capital/Fee requirement
 ↓
Membership Activation
 ↓
Member Registry
17. Member Registry
17.1 Purpose

Đây là nguồn dữ liệu chính thức của HTX trên nền tảng cho hồ sơ thành viên.

Luật HTX 2023 yêu cầu HTX lập, cập nhật và lưu trữ sổ đăng ký thành viên chính thức, thành viên liên kết góp vốn; nội dung sổ bao gồm thông tin nhận dạng và thông tin vốn góp, giấy chứng nhận phần vốn góp theo quy định.

17.2 Data Model
Member
member_id
htx_id
vanan_network_id
membership_type
status
joined_at
approved_at
effective_at
terminated_at
MemberIdentity
identity_id
member_id
full_name
contact_address
nationality
personal_identifier
verification_status
CapitalContribution
capital_id
member_id
committed_amount
contributed_amount
contribution_date
asset_type
asset_value
certificate_number
certificate_date
MembershipDocument
document_id
member_id
document_type
document_version
storage_reference
hash
created_at
18. Important Architecture Rule

Không dùng:

Phone Number = Member ID

Thay vào đó:

VANAN_NETWORK_ID
       +
HTX_ID
       +
MEMBERSHIP_ID

Ví dụ:

Network ID:
VA-8F72...

HTX:
HTX-Q1-001

Member:
MEM-HTX-Q1-000182

Số điện thoại chỉ là một thuộc tính xác thực/liên hệ.

19. Multi-HTX Membership

Một user có thể:

VA-000123
   │
   ├── HTX-Q1 → ACTIVE MEMBER
   ├── HTX-Q3 → APPLICANT
   └── HTX-BD → PARTICIPANT

Không tạo 3 account.

Luật HTX 2023 cũng quy định thành viên có thể đồng thời là thành viên của nhiều HTX, trừ trường hợp Điều lệ có quy định khác.

20. Membership Types

System phải configurable.

OFFICIAL_MEMBER
LINKED_CAPITAL_MEMBER
LINKED_NON_CAPITAL_MEMBER

Không hard-code quyền vào frontend.

Thay vào đó:

Membership Type
        ↓
Legal/HTX Configuration
        ↓
Permissions
        ↓
Benefits
21. Member ID Card

Sau khi ACTIVE:

┌────────────────────────────┐
│        VẠN AN NETWORK      │
│                            │
│        MEMBER              │
│                            │
│   Nguyễn Văn A             │
│   HTX: Cụm Q1              │
│                            │
│   Member ID: XXX           │
│                            │
│             [QR]           │
└────────────────────────────┘

QR không nên chứa dữ liệu CCCD.

Chỉ chứa:

Member verification token

Backend mới trả thông tin cần thiết.

22. Membership Verification

Khi quét Member QR:

QR
 ↓
Verification API
 ↓
Member Status

Ví dụ:

✓ Active Member
HTX: ...
Joined: ...

Không hiển thị:

CCCD;
địa chỉ;
vốn góp;
dữ liệu riêng tư

cho người không có quyền.

23. Capital/Fee Module

Module không phải hệ thống ngân hàng.

Nó chỉ quản lý:

Required
Committed
Paid
Outstanding
Verified

Ví dụ:

Vốn cam kết:       1.000.000
Đã góp:              500.000
Còn thiếu:           500.000

Payment có thể kết nối Accounting/Payment Gateway hợp pháp.

Không tạo ví nội bộ Vạn An để giữ tiền vốn góp chỉ vì muốn UX đơn giản.

24. Notification

Channels:

Zalo
SMS
Push
Email

Events:

APPLICATION_CREATED
DOCUMENT_REQUIRED
APPLICATION_APPROVED
APPLICATION_REJECTED
CAPITAL_DUE
MEMBERSHIP_ACTIVATED
MEMBERSHIP_SUSPENDED
MEMBERSHIP_TERMINATED
25. Zalo Integration

Zalo được thiết kế như:

Conversational Frontend

không phải database.

Zalo OA
   ↓
Vạn An API
   ↓
Membership Service
   ↓
Core Database

Ví dụ:

“Anh muốn đăng ký thành viên HTX?”

[Đăng ký]

→ OTP

→ hỏi từng thông tin

→ tạo Application.

26. Offline / Assisted Registration

Đối tượng như:

người bán hàng rong;
người lớn tuổi;
người ít dùng smartphone;

có thể được hỗ trợ bởi:

CTV / HTX Staff

Nhưng phải ghi:

Created By
Assisted By
Applicant Confirmation
Timestamp

Không cho phép CTV tự ý biến người khác thành member.

27. Assisted Registration Flow
CTV
 ↓
Create Applicant
 ↓
Applicant Phone OTP
 ↓
Enter Basic Information
 ↓
Applicant Reviews
 ↓
Applicant Confirms
 ↓
HTX Review

Nguyên tắc:

CTV hỗ trợ nhập dữ liệu, không sở hữu danh tính của applicant.

28. Audit Trail

Mọi hành động quan trọng phải audit:

Who
What
When
Before
After
Reason
Source

Ví dụ:

USER: ADMIN-001
ACTION: APPROVE_MEMBER
MEMBER: MEM-00182
TIME: 2026-09-29 14:21

Không cho phép sửa/xóa audit log thông thường.

29. Security Requirements
SR-01 — Encryption

Dữ liệu nhạy cảm phải được mã hóa:

at rest;
in transit.
SR-02 — RBAC

Role-based access control.

Platform Admin
HTX Admin
Membership Officer
Accounting
Reviewer
Member
SR-03 — Data minimization

Chỉ thu thập dữ liệu cần thiết.

SR-04 — Access logging

Mọi truy cập dữ liệu định danh quan trọng phải được log.

SR-05 — No CCCD in QR

QR không được chứa CCCD dạng plaintext.

SR-06 — No sensitive data in logs

Không log:

CCCD
OTP
full identity document
SR-07 — Tenant isolation

HTX A không được truy cập dữ liệu thành viên HTX B nếu không có quyền hợp lệ.

30. Multi-Tenant Architecture

Đây là requirement cực kỳ quan trọng cho chiến lược 10.000 HTX.

                VẠN AN PLATFORM
                       │
              Membership Service
                       │
        ┌──────────────┼──────────────┐
        │              │              │
      HTX A          HTX B          HTX C
        │              │              │
     Members        Members        Members

Mỗi record phải có:

tenant_id / htx_id

Database layer phải enforce isolation.

Không dựa vào frontend để bảo mật tenant.

31. API
POST /api/membership/applications

Create application.

GET /api/membership/applications/{id}

Get application.

POST /api/membership/applications/{id}/submit

Submit application.

POST /api/membership/applications/{id}/request-info

Request additional information.

POST /api/membership/applications/{id}/approve

Approve.

POST /api/membership/applications/{id}/reject

Reject.

GET /api/htx/{htxId}/members

List members.

GET /api/members/{memberId}

Get authorized member.

GET /api/members/{memberId}/status

Verify membership.

GET /api/members/{memberId}/qr

Generate verification QR.

32. Integration Architecture
                  ┌──────────────────┐
                  │    ZALO OA       │
                  └────────┬─────────┘
                           │
┌─────────┐       ┌───────▼────────┐       ┌─────────────┐
│   QR    │──────▶│ Membership API │◀──────│ Vạn An App  │
└─────────┘       └───────┬────────┘       └─────────────┘
                           │
                 ┌─────────▼─────────┐
                 │ Membership Engine │
                 └─────────┬─────────┘
                           │
         ┌─────────────────┼──────────────────┐
         ▼                 ▼                  ▼
    Identity           Workflow            Registry
         │                 │                  │
         └─────────────────┼──────────────────┘
                           ▼
                    CoreHub / DB
                           │
          ┌────────────────┼────────────────┐
          ▼                ▼                ▼
      Accounting       Loyalty          Commerce
33. Integration with Vạn An Ecosystem

Membership Infrastructure phải trở thành identity layer, không phải module độc lập.

Commerce
Member → Buy/Sell
Loyalty
Member → Points → Benefits
Procurement
Member → Collective Purchase
Accounting
Member → Capital/Fee/Transactions
Task Marketplace
Member → Tasks → Income
Community
Member → Local Network
34. Referral / Growth Engine

Mỗi applicant có thể có:

referrer_id
source
campaign_id
cluster_id

Ví dụ:

CTV A
  ↓
10 sellers
  ↓
5 sellers refer
  ↓
50 sellers

Nhưng hệ thống phải phân biệt:

Referral attribution

với:

Quyền sở hữu thành viên.

CTV giới thiệu người dùng không có quyền kiểm soát thành viên đó.

35. Growth Metrics

Dashboard Vạn An:

Acquisition
QR scans
Applications
Conversion
Activation
Approved
Active Members
First Transaction
Retention
30D
90D
180D
Network
Members / HTX
Active Members / HTX
Transactions / Member
Economics
Revenue / Member
Contribution / Member
CAC
Payback
36. North Star Metric

Không dùng:

Số lượng thành viên đăng ký

vì rất dễ tạo “thành viên ảo”.

Nên dùng:

Active Economic Members

Một member được xem là active khi trong kỳ có ít nhất một hoạt động kinh tế hợp lệ theo định nghĩa của HTX/Vạn An, ví dụ:

mua;
bán;
cung cấp;
giao hàng;
sử dụng dịch vụ HTX;
tham gia hoạt động kinh tế được quy định.

Có thể theo dõi:

Active Members
        ×
Economic Transactions
        ×
Retention
37. Anti-Gaming

Hệ thống phải chống:

Fake members

Không tính:

Created account

là member active.

Self-referral

Không tính referral vòng.

Duplicate identity

Một định danh không được tạo nhiều hồ sơ không kiểm soát.

Fake transactions

Không dùng giao dịch tự mua/bán để tạo GMV giả.

Incentive abuse

Giới hạn:

referral;
rewards;
bonuses;
repeated transactions.
38. MVP Scope

Tôi không khuyên Vạn An xây toàn bộ hệ thống ngay.

Phase 1 — 1 HTX

Phạm vi:

QR
OTP
Basic Profile
Application
Consent
HTX Review
Approval
Member Registry
Member ID
QR Verification
Audit Log

Chưa cần:

KYC phức tạp;
AI;
blockchain;
smart contract;
ví nội bộ;
multi-HTX advanced routing.
39. Phase 2 — 10 HTX

Thêm:

Multi-Tenant
Zalo OA
Assisted Registration
Capital/Fee Tracking
Document Versioning
Notification
Analytics
40. Phase 3 — 100 HTX

Thêm:

HTX Template
Configurable Membership Rules
Self-Service HTX Admin
API
Accounting Integration
Identity Verification Integration
Advanced Audit
41. Phase 4 — 1,000+ HTX

Thêm:

Network Identity
Cross-HTX Membership
Cross-Cluster Verification
Federated Services
Regional Administration
Advanced Analytics
AI Assistant
Automated Compliance Checks
42. Definition of Done — MVP

Module chỉ được coi là hoàn thành khi:

Functional
 User đăng ký bằng QR.
 OTP hoạt động.
 Application được tạo.
 Applicant xem Điều lệ/terms.
 Consent được lưu.
 HTX review được application.
 HTX approve/reject được.
 Approved applicant trở thành Active Member theo workflow.
 Member xuất hiện trong electronic registry.
 Member ID được tạo.
 QR verification hoạt động.
 Member status hiển thị chính xác.
 Audit log đầy đủ.
Security
 Tenant isolation.
 RBAC.
 Không log CCCD/OTP.
 QR không chứa dữ liệu định danh plaintext.
 Sensitive data encryption.
 Access audit.
Governance
 Vạn An không tự approve member.
 HTX có quyền quyết định.
 Có document/version history.
 Có approval evidence.
 Không xóa lịch sử membership tùy tiện.
43. Kiến trúc cuối cùng tôi đề xuất
                    VẠN AN
           MEMBERSHIP INFRASTRUCTURE
                         │
        ┌────────────────┼────────────────┐
        │                │                │
    Identity          Workflow         Registry
        │                │                │
        └────────────────┼────────────────┘
                         │
                  HTX Membership
                         │
          ┌──────────────┼───────────────┐
          ▼              ▼               ▼
       Applicant       Member        Former Member
          │              │
          │              │
          └──────────────┼───────────────┐
                         │               │
                     VẠN AN ID           │
                         │               │
             ┌───────────┼───────────┐   │
             ▼           ▼           ▼   │
          Commerce    Loyalty    Procurement
             │           │           │
             └───────────┼───────────┘
                         ▼
                 LOCAL ECONOMIC
                    NETWORK
44. Nguyên tắc kiến trúc quan trọng nhất

Tôi muốn ghi nguyên văn 6 nguyên tắc này vào SRS để Devin/Windsurf không tự “đơn giản hóa” sai kiến trúc:

P1 — Vạn An provides infrastructure; HTX owns membership decisions.

P2 — Participant ≠ Applicant ≠ Official Member.

P3 — Vạn An Network ID ≠ HTX Member ID.

P4 — Zalo/QR are onboarding channels, not the system of record.

P5 — Electronic consent/evidence ≠ automatically equivalent to every legally required form of electronic signature.

P6 — Member count is not the growth metric; active economic participation is.

Đặc biệt P2 và P3 sẽ giúp Vạn An tránh một lỗi kiến trúc rất lớn sau này: biến Vạn An thành “công ty mẹ quản lý thành viên của hàng nghìn HTX”. Mô hình đúng là Vạn An xây lớp hạ tầng chung; mỗi HTX giữ quyền quản trị thành viên của mình. Điều này cũng phù hợp với việc Luật HTX đặt nghĩa vụ lập, cập nhật và lưu trữ sổ đăng ký thành viên lên chính HTX.

Nếu triển khai thực tế, tôi sẽ lấy SRS này làm “contract” cho Devin, rồi tách tiếp thành Domain Model → DB Schema → API Contract → RBAC Matrix → State Machine → E2E Test Cases → Implementation Tasks, thay vì để Devin tự thiết kế từng phần và có nguy cơ biến module này thành một form đăng ký đơn giản.