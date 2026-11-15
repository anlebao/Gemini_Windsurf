# VAN_AN_Account_Trust_and_Recovery_Service_v1.0

**Tài liệu:** Software Requirements Specification (SRS)\
**Phiên bản:** 1.0\
**Trạng thái:** Draft for architecture and implementation review\
**Phạm vi:** Dịch vụ hồ sơ tín nhiệm tài khoản, nhận diện thiết bị, khôi
phục quyền truy cập và bằng chứng xử lý tranh chấp trong hệ sinh thái
Vạn An\
**Ngôn ngữ:** Tiếng Việt\
**Đối tượng:** Product Owner, Solution Architect, Backend, Frontend/PWA,
Security, QA, DevOps và Devin/AI coding agent

------------------------------------------------------------------------

## 1. Mục tiêu và định hướng

### 1.1. Bối cảnh

Vạn An cần một cơ chế thống nhất để:

-   Ghi nhận thiết bị được người dùng sử dụng khi đăng ký và trong quá
    trình sử dụng tài khoản.
-   Gắn tài khoản với các bằng chứng do người dùng cung cấp, gồm thông
    tin cá nhân khai báo và câu hỏi bí mật tùy chọn.
-   Ghi nhận lịch sử xác thực, thiết bị, thay đổi thông tin, giao dịch
    và các sự kiện bảo mật.
-   Hỗ trợ người dùng đổi/mất thiết bị, khôi phục tài khoản và xử lý
    khiếu nại.
-   Cung cấp bằng chứng có thể kiểm toán khi phát sinh tranh chấp về
    điểm thưởng, quyền lợi, số dư ví hoặc giao dịch.
-   Dùng chung một định danh tài khoản và chính sách bảo mật cho các ứng
    dụng Vạn An, không tạo các hệ thống định danh độc lập theo từng ứng
    dụng.

### 1.2. Mục tiêu sản phẩm

Xây dựng **Vạn An Account Trust & Recovery Service** (sau đây gọi tắt là
ATRS) để quản lý:

1.  **Account Identity:** định danh tài khoản nội bộ.
2.  **Device Identity:** đăng ký và nhận diện thiết bị bằng khóa mật mã
    khi nền tảng hỗ trợ.
3.  **Personal Evidence Profile:** thông tin cá nhân do người dùng khai
    báo, có nhãn nguồn gốc và trạng thái xác minh.
4.  **Secret Challenge:** câu hỏi/đáp án bí mật như một yếu tố hỗ trợ
    khôi phục, không phải phương thức xác thực độc lập.
5.  **Activity Evidence:** lịch sử sự kiện bảo mật, truy cập và tham
    chiếu giao dịch.
6.  **Recovery Workflow:** quy trình khôi phục tài khoản khi mất thiết
    bị hoặc không còn phương thức đăng nhập cũ.
7.  **Dispute Evidence:** hồ sơ bằng chứng, quy trình xem xét, quyết
    định và kiểm toán tranh chấp.

### 1.3. Nguyên tắc thiết kế bắt buộc

-   Không mặc định yêu cầu SMS OTP cho mọi lần đăng nhập.
-   Không coi Device ID, fingerprint, tên, ngày sinh hoặc câu trả lời bí
    mật là bằng chứng tuyệt đối về danh tính.
-   Không dùng một yếu tố duy nhất để cho phép khôi phục tài khoản có
    quyền lợi hoặc giá trị tài chính.
-   Khóa riêng (private key) không được gửi hoặc lưu trên CoreHub.
-   Không lưu câu trả lời bí mật ở dạng rõ.
-   Không sửa/xóa lịch sử giao dịch hoặc sự kiện kiểm toán; điều chỉnh
    bằng bản ghi mới có liên kết nguồn.
-   Phân biệt **xác thực quyền kiểm soát tài khoản**, **xác minh danh
    tính pháp lý** và **xác định tính đúng sai của giao dịch**.
-   Thu thập dữ liệu tối thiểu, đúng mục đích, có thông báo và cơ chế
    thực hiện quyền của chủ thể dữ liệu.
-   Bảo đảm tương thích với kiến trúc CoreHub/Gateway và các ứng dụng
    Vạn An hiện hữu; không tự ý thay thế hoặc phá vỡ cơ chế đăng nhập
    hiện tại.
-   Ưu tiên giải pháp có thể triển khai theo từng giai đoạn, không yêu
    cầu hạ tầng lớn ngay từ đầu.

------------------------------------------------------------------------

## 2. Phạm vi

### 2.1. Trong phạm vi

-   Tạo và quản lý hồ sơ ATRS gắn với `UserId` hiện hữu của CoreHub.
-   Đăng ký, liệt kê, thu hồi và đánh dấu rủi ro thiết bị.
-   Hỗ trợ WebAuthn/passkeys hoặc cơ chế khóa công khai tương thích nền
    tảng.
-   Ghi nhận thông tin cá nhân do người dùng tự khai báo.
-   Quản lý câu hỏi bí mật và xác minh đáp án theo giới hạn an toàn.
-   Ghi nhận sự kiện xác thực, thay đổi thông tin và khôi phục.
-   Tạo và quản lý yêu cầu khôi phục tài khoản.
-   Tạo hồ sơ tranh chấp, đính kèm tham chiếu bằng chứng, quyết định và
    lịch sử xử lý.
-   Cung cấp API dùng chung cho KhachLink, TimLaThay, Loyalty, Booking,
    ShopERP và các ứng dụng được cấp quyền.
-   Tích hợp tham chiếu với sổ giao dịch/ledger của Loyalty hoặc ví;
    không thay thế ledger.
-   Audit log, phân quyền, rate limit, cảnh báo và giám sát.

### 2.2. Ngoài phạm vi phiên bản 1.0

-   eKYC hoặc xác minh danh tính pháp lý bằng CCCD/cơ sở dữ liệu nhà
    nước.
-   Tích hợp trực tiếp API của tất cả nhà mạng.
-   Tự động kết luận tranh chấp tài chính bằng AI.
-   Tự động hoàn/điều chỉnh điểm hoặc tiền trong ví mà không qua hệ
    thống nghiệp vụ có thẩm quyền.
-   Hệ thống chấm điểm tín dụng hoặc xếp hạng con người.
-   Theo dõi vị trí liên tục, thu thập IMEI/serial hoặc fingerprint xâm
    lấn.
-   Xây dựng một Identity Provider mới thay thế hoàn toàn cơ chế xác
    thực hiện hữu.

### 2.3. Đối tượng sử dụng

  -----------------------------------------------------------------------
  Đối tượng                           Nhu cầu
  ----------------------------------- -----------------------------------
  Khách hàng/người mua                Đăng ký, đăng nhập, đổi thiết bị,
                                      khôi phục tài khoản, khiếu nại

  CTV                                 Bảo vệ tài khoản và quyền lợi/hoa
                                      hồng

  Chủ cửa hàng/nhân viên              Bảo vệ tài khoản và quyền truy cập
                                      tenant

  Nhân sự HTX/Vạn An                  Hỗ trợ người dùng theo quyền hạn

  Nhân viên xử lý tranh chấp          Xem bằng chứng, lập kết luận, đề
                                      xuất xử lý

  Quản trị bảo mật                    Quản lý chính sách, thiết bị, sự
                                      kiện và audit

  Ứng dụng tích hợp                   Gọi API ATRS theo quyền được cấp
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## 3. Thuật ngữ và phân biệt khái niệm

  -----------------------------------------------------------------------
  Thuật ngữ                           Định nghĩa
  ----------------------------------- -----------------------------------
  `UserId`                            Định danh tài khoản người dùng duy
                                      nhất trong CoreHub

  `DeviceId`                          Mã thiết bị do Vạn An tạo; không
                                      phải hardware ID bất biến

  Device Fingerprint                  Tập hợp tín hiệu thiết bị/trình
                                      duyệt; chỉ là tín hiệu rủi ro,
                                      không phải định danh chắc chắn

  Device Credential                   Cặp khóa hoặc credential được
                                      tạo/đăng ký trên thiết bị

  Public Key                          Khóa công khai lưu tại máy chủ để
                                      xác minh chữ ký

  Private Key                         Khóa riêng nằm trong
                                      authenticator/thiết bị; không được
                                      gửi về máy chủ

  Passkey/WebAuthn                    Cơ chế xác thực bằng public-key
                                      credential

  Personal Evidence                   Thông tin người dùng tự khai hoặc
                                      được xác minh từ nguồn khác

  Secret Challenge                    Câu hỏi và đáp án bí mật do người
                                      dùng lựa chọn

  Recovery                            Quy trình khôi phục quyền kiểm soát
                                      tài khoản

  Dispute                             Hồ sơ khiếu nại/tranh chấp về tài
                                      khoản, điểm, ví hoặc giao dịch

  Ledger                              Sổ giao dịch có tính kiểm toán;
                                      nguồn dữ liệu chuẩn để xác định số
                                      dư

  Evidence                            Bằng chứng hoặc tham chiếu bằng
                                      chứng, kèm nguồn gốc, thời điểm và
                                      mức tin cậy

  Step-up Authentication              Yêu cầu xác thực mạnh hơn trước
                                      hành động nhạy cảm
  -----------------------------------------------------------------------

**Phân biệt bắt buộc:**

-   `DeviceId` nhận diện một bản ghi thiết bị do Vạn An quản lý; nó có
    thể mất, bị reset hoặc bị sao chép.
-   Device Credential chứng minh người yêu cầu có thể sử dụng khóa tương
    ứng với credential đã đăng ký.
-   Passkey có thể được đồng bộ giữa nhiều thiết bị; vì vậy không mặc
    nhiên chứng minh đang dùng đúng thiết bị vật lý ban đầu.
-   Thông tin tên/ngày sinh tự khai chỉ là dữ liệu khai báo, không phải
    danh tính đã xác minh.
-   Câu trả lời bí mật là yếu tố hỗ trợ có độ tin cậy thấp; không được
    là yếu tố duy nhất để cấp quyền.
-   Lịch sử giao dịch giúp chứng minh điều gì đã được hệ thống ghi nhận,
    không tự động chứng minh ai là người thực hiện nếu thiếu bằng chứng
    xác thực gắn với giao dịch.

------------------------------------------------------------------------

## 4. Kiến trúc logic đề xuất

### 4.1. Vị trí trong kiến trúc Vạn An

ATRS là dịch vụ nghiệp vụ dùng chung, tích hợp với CoreHub/Gateway và
dùng `UserId` hiện hữu. Không tạo tài khoản người dùng thứ hai.

Luồng logic:

1.  Ứng dụng Vạn An gửi yêu cầu qua Gateway.
2.  Gateway xác thực access token/phiên theo cơ chế hiện hữu.
3.  Gateway chuyển yêu cầu ATRS đến **`vanan-atrs`** — service độc lập
    (pattern `8_Metering`/`9_TenantAI`/`10_Voice`, port 5080, PostgreSQL
    riêng). **Ghi chú kiến trúc (ADR-001, 2026-11-15):** CoreHub là
    class library (không host API) — ATRS KHÔNG chạy trong CoreHub;
    contracts dùng chung đặt trong `1_Shared`/`VanAn.Atrs.Contracts`.
4.  ATRS xử lý hồ sơ, credential, recovery hoặc dispute.
5.  ATRS lưu dữ liệu nghiệp vụ và audit event.
6.  Khi cần thông tin giao dịch, ATRS truy vấn bằng API hoặc tham chiếu
    đến Loyalty/Wallet Ledger.
7.  ATRS không tự ghi sổ hoặc sửa số dư.

### 4.2. Các module

  -----------------------------------------------------------------------
  Module                              Trách nhiệm
  ----------------------------------- -----------------------------------
  Account Trust Profile               Hồ sơ ATRS tổng hợp của một UserId

  Device Registry                     Đăng ký, trạng thái, lịch sử thiết
                                      bị

  Credential Manager                  WebAuthn/passkey credential và
                                      challenge

  Personal Evidence Manager           Thông tin cá nhân, nguồn gốc, trạng
                                      thái xác minh

  Secret Challenge Manager            Thiết lập, đổi, xác minh câu hỏi bí
                                      mật

  Risk & Policy Engine                Đánh giá rủi ro và quyết định bước
                                      xác thực bổ sung

  Recovery Case Manager               Yêu cầu khôi phục, xét duyệt, thu
                                      hồi credential

  Dispute Case Manager                Hồ sơ tranh chấp và quyết định

  Evidence Collector                  Thu thập event, ledger reference,
                                      session reference

  Audit Service                       Nhật ký bất biến ở cấp ứng dụng và
                                      kiểm soát truy cập

  Integration API                     API cho ứng dụng và dịch vụ Vạn An
  -----------------------------------------------------------------------

### 4.3. Ranh giới trách nhiệm

-   Authentication hiện hữu vẫn là thành phần xác thực phiên/token cho
    đến khi có quyết định thay đổi riêng.
-   ATRS cung cấp bằng chứng và chính sách về thiết bị/khôi phục; không
    tự phát hành token đăng nhập tùy tiện.
-   Gateway là điểm kiểm soát truy cập API.
-   CoreHub là nguồn dữ liệu tài khoản chuẩn.
-   Loyalty/Wallet Ledger là nguồn dữ liệu chuẩn về phát sinh điểm, ghi
    nợ, ghi có, hoàn/điều chỉnh và số dư.
-   ATRS chỉ lưu tham chiếu ledger, bản chụp phục vụ điều tra nếu cần và
    metadata chứng cứ; không tạo ledger thứ hai.

------------------------------------------------------------------------

## 5. Yêu cầu chức năng

### FR-001 -- Liên kết hồ sơ với tài khoản hiện hữu

-   Mỗi hồ sơ ATRS liên kết đúng một `TrustSubjectId` — định danh nội
    bộ do ATRS tạo (UUID bất biến), KHÔNG dùng SĐT/email/DeviceId làm
    khóa chính.
-   `TrustSubjectId` map nhiều identity hiện hữu qua `TrustSubjectLink`
    (Customer per-tenant, DemoUser, DeviceId-cluster) — ADR-001
    (2026-11-15, đã chốt): Customer là per-tenant, một người có thể có
    nhiều Customer rows; profile bảo mật là xuyên tenant.
-   Một `TrustSubjectId` chỉ có một hồ sơ ATRS đang hoạt động.
-   Không tạo `UserId` mới chỉ để sử dụng ATRS.
-   Nếu tài khoản bị khóa/xóa theo chính sách hiện hữu, ATRS phải nhận
    sự kiện đồng bộ trạng thái phù hợp.
-   API phải kiểm tra quyền sở hữu hồ sơ; người dùng không được truy cập
    hồ sơ của `TrustSubjectId` khác.
-   **Recovery entry auth model (đã chốt 2026-11-15):** tiered proof —
    ① còn recovery code → verify code · ② không code → verified-contact
    OTP (email-first, P4) + captcha + risk engine · ③ không gì → manual
    review mặc định (R3) + maker-checker. Bắt buộc anti-enumeration,
    rate limit + lockout progressive, log `AccountSecurityEvents`.

### FR-002 -- Tạo hồ sơ Trust Profile

Khi người dùng đăng ký hoặc kích hoạt tính năng:

-   Tạo hồ sơ ATRS ở trạng thái `PENDING_SETUP`.
-   Ghi nhận thời điểm tạo, nguồn tạo, ứng dụng khởi tạo và phiên liên
    quan.
-   Cho phép hoàn tất hồ sơ từng phần.
-   Không chặn người dùng sử dụng chức năng cơ bản chỉ vì chưa khai báo
    toàn bộ thông tin, trừ khi nghiệp vụ có rủi ro cao yêu cầu.
-   Ghi nhận phiên bản thông báo quyền riêng tư và bằng chứng về sự đồng
    ý khi cần.

### FR-003 -- Đăng ký thiết bị

Hệ thống phải:

-   Tạo `DeviceId` ngẫu nhiên, duy nhất, không chứa dữ liệu cá nhân.
-   Ghi nhận loại thiết bị, hệ điều hành, trình duyệt/app, thời điểm
    đăng ký và ứng dụng nguồn ở mức tối thiểu cần thiết.
-   Cho phép đăng ký một hoặc nhiều thiết bị theo chính sách.
-   Hỗ trợ đặt tên thiết bị thân thiện, ví dụ "Điện thoại cá nhân".
-   Có trạng thái `PENDING`, `ACTIVE`, `SUSPICIOUS`, `REVOKED`, `LOST`.
-   Ghi lịch sử chuyển trạng thái, không ghi đè mất lịch sử cũ.
-   Không dựa vào IP, User-Agent, canvas fingerprint hoặc thông số màn
    hình như bằng chứng duy nhất.
-   Không yêu cầu hoặc cố truy cập IMEI/serial/hardware identifier trên
    PWA.

### FR-004 -- Device Credential / Passkey

-   Ưu tiên WebAuthn/passkey cho nền tảng tương thích.
-   Tạo challenge ngẫu nhiên, dùng một lần, có thời hạn ngắn và ràng
    buộc với phiên, origin, RP ID và mục đích.
-   Máy chủ xác minh attestation/assertion theo chính sách WebAuthn;
    không tự viết thuật toán mật mã.
-   Lưu credential ID, public key, counter/metadata cần thiết,
    transports nếu có, createdAt, lastUsedAt, status.
-   Không lưu private key.
-   Xử lý trường hợp authenticator không cung cấp counter đáng tin cậy;
    không dùng counter đơn lẻ làm tiêu chí kết luận gian lận.
-   Hỗ trợ nhiều credential trên cùng tài khoản.
-   Cho phép thu hồi credential.
-   Nếu credential được đồng bộ qua nền tảng, giao diện phải mô tả đúng
    đây là passkey có thể đồng bộ, không khẳng định nó chỉ tồn tại trên
    một thiết bị vật lý.
-   Không coi việc có passkey là xác minh danh tính pháp lý.

### FR-005 -- Device ID dự phòng cho PWA

Trong trường hợp WebAuthn không hỗ trợ hoặc người dùng từ chối:

-   Có thể tạo App Installation ID ngẫu nhiên lưu trong vùng lưu trữ ứng
    dụng.
-   Có thể dùng cookie/storage token có chữ ký hoặc cơ chế tương đương
    để nhận biết lần truy cập tiếp theo.
-   Device ID này chỉ là tín hiệu nhận diện thiết bị và hỗ trợ trải
    nghiệm.
-   Nếu dữ liệu trình duyệt bị xóa, hệ thống phải chấp nhận tạo device
    record mới sau quy trình xác minh phù hợp.
-   Không được cấp quyền khôi phục hoặc giao dịch rủi ro cao chỉ dựa
    trên Installation ID.
-   Không được dùng fingerprint xâm lấn để cố tạo định danh bất biến.

### FR-006 -- Hồ sơ cá nhân khai báo

Các trường phiên bản 1.0:

-   Họ tên hiển thị.
-   Ngày/tháng/năm sinh (nếu thực sự cần cho mục đích đã thông báo).
-   Số điện thoại liên hệ (nếu đã có trong tài khoản hiện hữu).
-   Ngôn ngữ/tuỳ chọn liên hệ nếu cần cho UX.

Mỗi trường phải có metadata:

-   `value`
-   `source` (USER_DECLARED, VERIFIED_PROVIDER, STAFF_REVIEWED,
    MIGRATED)
-   `verificationStatus` (UNVERIFIED, VERIFIED, REJECTED, EXPIRED)
-   `collectedAt`
-   `updatedAt`
-   `privacyNoticeVersion`

Yêu cầu:

-   Không tự gắn nhãn "đã xác minh" cho dữ liệu người dùng tự khai.
-   Cho phép người dùng xem và sửa thông tin theo chính sách.
-   Thay đổi trường nhạy cảm phải ghi audit event.
-   Nếu có dữ liệu đã xác minh từ nguồn đáng tin cậy, phải lưu nguồn và
    thời điểm xác minh.
-   Không bắt buộc ngày sinh nếu không có mục đích nghiệp vụ cụ thể và
    hợp pháp.

### FR-007 -- Câu hỏi bí mật

Câu hỏi bí mật chỉ là một yếu tố hỗ trợ trong recovery/dispute.

Yêu cầu:

-   Cho phép người dùng chọn câu hỏi từ danh sách được phê duyệt hoặc
    tạo câu hỏi riêng nếu có kiểm soát.
-   Cho phép nhập đáp án và gợi ý riêng.
-   Gợi ý chỉ hiển thị cho chính người dùng trong luồng đã xác thực;
    không hiển thị cho nhân viên xử lý.
-   Không lưu đáp án rõ.
-   Chuẩn hóa đáp án theo quy tắc được công bố, ví dụ trim khoảng trắng
    và chuẩn hóa Unicode; không tự ý bỏ dấu hoặc thay đổi nội dung khiến
    người dùng không nhận ra.
-   Băm đáp án bằng thuật toán password hashing phù hợp như Argon2id với
    salt riêng; dùng pepper lưu trong secret manager nếu kiến trúc hỗ
    trợ.
-   Không ghi đáp án, gợi ý nhạy cảm hoặc hash vào log.
-   Có rate limit, lockout tăng dần và giám sát thử sai.
-   Có chức năng đổi câu hỏi/đáp án sau step-up authentication.
-   Có phương thức recovery thay thế nếu người dùng quên đáp án.
-   Không dùng câu trả lời bí mật một mình để đổi email/số liên hệ, thêm
    credential mới, chuyển quyền tài khoản hoặc giải phóng tiền/điểm có
    giá trị cao.
-   Cân nhắc không triển khai security question ở các luồng có thể thay
    thế bằng passkey/recovery code an toàn hơn.

### FR-008 -- Recovery Codes (khuyến nghị)

-   Cho phép người dùng tạo một bộ recovery codes dùng một lần sau khi
    đăng ký passkey.
-   Chỉ hiển thị một lần khi tạo; người dùng được khuyến nghị lưu ngoại
    tuyến an toàn.
-   Máy chủ chỉ lưu hash của từng code.
-   Mỗi code chỉ sử dụng được một lần.
-   Có thể thu hồi và tạo bộ mới sau step-up authentication.
-   Không gửi recovery code qua log, email hoặc thông báo không mã hóa.
-   Recovery code không thay thế quy trình xác minh bổ sung đối với tài
    khoản có rủi ro hoặc quyền lợi giá trị cao.

### FR-009 -- Activity Evidence

Ghi nhận các sự kiện tối thiểu:

-   Account created / activated.
-   Device registered / used / renamed / revoked / marked lost.
-   Passkey created / used / removed.
-   Login success / failure (chỉ metadata cần thiết).
-   Recovery initiated / approved / rejected / completed.
-   Personal profile changed.
-   Secret challenge created / changed / failed.
-   Security setting changed.
-   Sensitive action step-up success / failure.
-   Dispute created / evidence added / decision made.
-   Admin access to sensitive profile or case.

Mỗi event tối thiểu có:

-   `EventId`
-   `UserId`
-   `EventType`
-   `OccurredAt`
-   `ReceivedAt`
-   `ActorType` (USER, SYSTEM, STAFF, SERVICE)
-   `ActorId` hoặc service identity
-   `SourceApplication`
-   `DeviceId` nếu có
-   `SessionId`/correlation ID nếu có
-   `Outcome`
-   `ReasonCode`
-   `EvidenceReferences`
-   `SchemaVersion`

Không lưu bí mật xác thực hoặc dữ liệu cá nhân không cần thiết trong
event payload.

### FR-010 -- Risk & Step-up Policy

Risk engine phiên bản 1.0 dùng luật xác định (deterministic rules),
không bắt buộc AI/ML.

Tín hiệu rủi ro có thể gồm:

-   Thiết bị mới.
-   Credential mới hoặc vừa thay đổi.
-   Nhiều lần xác thực thất bại.
-   Recovery ngay sau thay đổi thông tin liên hệ.
-   Đăng nhập bất thường theo tín hiệu tối thiểu và được phép xử lý.
-   Yêu cầu thay đổi thông tin bảo mật.
-   Yêu cầu chuyển/điều chỉnh điểm hoặc giá trị ví.
-   Nhiều yêu cầu recovery trong thời gian ngắn.
-   Thiết bị bị đánh dấu mất hoặc bị thu hồi.

Kết quả policy:

-   `ALLOW`
-   `ALLOW_WITH_STEP_UP`
-   `REQUIRE_MANUAL_REVIEW`
-   `TEMPORARY_RESTRICT`
-   `DENY`

Không được quyết định chỉ bằng một điểm số mơ hồ. Mỗi quyết định phải có
reason code và policy version.

### FR-011 -- Recovery Request

Người dùng có thể tạo yêu cầu khôi phục khi:

-   Mất thiết bị cũ.
-   Thiết bị hỏng hoặc cài lại hệ điều hành.
-   Không còn passkey/credential.
-   Không còn recovery code.
-   Nghi ngờ tài khoản bị chiếm quyền.

Mỗi request phải có:

-   `RecoveryRequestId`
-   `UserId`
-   `RequestType`
-   `CreatedAt`
-   `CurrentAccessState`
-   `NewDeviceId` nếu có
-   `EvidenceSubmitted`
-   `RiskResult`
-   `Status`
-   `ReviewerId` nếu cần xét duyệt
-   `DecisionReason`
-   `CompletedAt`

Trạng thái:

-   `SUBMITTED`
-   `AUTOMATED_CHECK`
-   `PENDING_USER_ACTION`
-   `PENDING_REVIEW`
-   `APPROVED`
-   `REJECTED`
-   `CANCELLED`
-   `EXPIRED`
-   `COMPLETED`

Yêu cầu xử lý:

-   Cho phép hủy request bởi người tạo khi còn an toàn.
-   Chống gửi trùng bằng idempotency key.
-   Giới hạn số request trong một khoảng thời gian.
-   Thông báo về kênh liên hệ đã đăng ký nếu có, nhưng không để thông
    báo chứa bí mật.
-   Không tiết lộ cho người yêu cầu những dữ liệu cá nhân đang lưu ngoài
    mức cần thiết.
-   Nếu có thiết bị cũ còn truy cập được, ưu tiên phê duyệt recovery từ
    thiết bị cũ bằng step-up.
-   Nếu thiết bị cũ mất, áp dụng recovery assurance theo mức rủi ro và
    giá trị tài khoản.
-   Tài khoản có ví/giá trị đáng kể hoặc quyền quản trị phải có xét
    duyệt tăng cường.
-   Sau khi hoàn tất, thu hồi session/token cũ theo khả năng của hệ
    thống xác thực hiện hữu và yêu cầu người dùng đăng ký credential
    mới.
    **Cơ chế bắt buộc (đã chốt 2026-11-15):** `TokenVersion` — claim
    `tv` trong JWT staff + version trong customer token payload
    (IDataProtector); validator so với version hiện tại qua cache
    (TTL 60s, write-through khi bump); bump version khi recovery
    COMPLETED, đổi password/MFA, revoke device, break-glass. Một helper
    dùng chung `TokenVersionValidator` trong `1_Shared` cho mọi
    validator (Gateway/voice/tenantai) để tránh drift.
-   Có thời gian trì hoãn hoặc hạn chế giao dịch nhạy cảm sau recovery
    đối với trường hợp rủi ro cao; thời gian phải cấu hình theo chính
    sách và thông báo rõ.
-   Không khóa toàn bộ tài khoản một cách máy móc nếu chỉ cần hạn chế
    một số hành động rủi ro.

### FR-012 -- Dispute Case

Hỗ trợ các nhóm tranh chấp:

-   Tài khoản bị truy cập trái phép.
-   Mất/đổi thiết bị.
-   Điểm thưởng thiếu/thừa/sai lệch.
-   Ví hoặc quyền lợi hiển thị sai.
-   Giao dịch người dùng phủ nhận.
-   Giao dịch điều chỉnh/hoàn điểm.
-   Tranh chấp quyền sở hữu hoặc quyền quản lý tài khoản.

Thông tin hồ sơ:

-   `DisputeCaseId`
-   `UserId`
-   `CaseType`
-   `RelatedTransactionIds`
-   `RelatedLedgerReferences`
-   `ClaimedIssue`
-   `SubmittedAt`
-   `EvidenceItems`
-   `RiskAndAuthEvents`
-   `AssignedReviewer`
-   `Status`
-   `Decision`
-   `DecisionReason`
-   `ResolutionReference`
-   `ClosedAt`

Trạng thái:

-   `OPEN`
-   `TRIAGE`
-   `WAITING_FOR_USER`
-   `UNDER_REVIEW`
-   `WAITING_FOR_EXTERNAL_EVIDENCE`
-   `DECIDED`
-   `RESOLVED`
-   `REJECTED`
-   `CLOSED`

Yêu cầu:

-   Một dispute có thể liên kết nhiều transaction/ledger references.
-   Evidence phải ghi rõ nguồn, người tạo, thời điểm, tính toàn vẹn và
    mức độ tin cậy.
-   Không cho nhân viên sửa nội dung bằng chứng gốc.
-   Bổ sung tài liệu/bằng chứng bằng bản ghi mới.
-   Mọi lần xem dữ liệu nhạy cảm phải được audit.
-   Quyết định phải có căn cứ và người chịu trách nhiệm.
-   Người xử lý không được tự phê duyệt điều chỉnh tài chính nếu không
    có quyền nghiệp vụ tương ứng.
-   Cho phép quy trình hai người phê duyệt (maker-checker) cho điều
    chỉnh vượt ngưỡng cấu hình.
-   Hỗ trợ xuất hồ sơ tranh chấp theo quyền hạn, che dữ liệu không cần
    thiết.

### FR-013 -- Loyalty/Wallet Evidence Integration

ATRS không phải sổ cái tài chính.

Khi xử lý khiếu nại điểm/ví, ATRS phải lấy hoặc tham chiếu:

-   Transaction ID.
-   Ledger entry ID.
-   Loại giao dịch.
-   Số lượng/giá trị và đơn vị.
-   Thời điểm ghi nhận.
-   Nguồn khởi tạo (service/tenant/actor).
-   Trạng thái giao dịch.
-   Reference đến giao dịch gốc nếu là reversal/adjustment.
-   Idempotency key hoặc correlation ID nếu có.
-   Kết quả đối soát.

Nguyên tắc:

-   Số dư được tính từ ledger theo quy tắc của dịch vụ sở hữu ledger.
-   Không sửa số dư trực tiếp.
-   Hoàn/điều chỉnh phải là giao dịch mới, có lý do, người/tiến trình
    khởi tạo và phê duyệt.
-   ATRS không được kết luận giao dịch hợp lệ chỉ vì thiết bị đã đăng ký
    được sử dụng.
-   Xác thực tốt không đồng nghĩa giao dịch nghiệp vụ chắc chắn đúng.
-   Nếu ledger không cung cấp dữ liệu hoặc không nhất quán, dispute phải
    chuyển sang trạng thái chờ đối soát.

### FR-014 -- Admin/Staff Review

-   RBAC theo vai trò và phạm vi tenant/đơn vị.
-   Tách quyền xem hồ sơ, xem bằng chứng, phê duyệt recovery, quyết định
    dispute và thực hiện điều chỉnh ledger.
-   Nhân viên hỗ trợ tuyến đầu không được xem secret answer/hash hoặc
    private credential.
-   Mọi hành động của nhân viên phải có actor ID, lý do, timestamp và
    correlation ID.
-   Tác vụ nhạy cảm yêu cầu MFA/step-up cho nhân viên.
-   Quy trình maker-checker cho thao tác có tác động tới quyền lợi/giá
    trị tài chính.
-   Không cho phép tự xử lý case do chính nhân viên tạo nếu chính sách
    xung đột lợi ích yêu cầu tách biệt.

### FR-015 -- User Transparency

Người dùng phải có thể:

-   Xem danh sách thiết bị đã đăng ký.
-   Xem thời điểm sử dụng gần nhất ở mức phù hợp.
-   Đặt tên thiết bị.
-   Đánh dấu thiết bị bị mất.
-   Thu hồi thiết bị/credential nếu đã có phương thức thay thế an toàn.
-   Xem trạng thái yêu cầu recovery/dispute.
-   Xem thông báo về thay đổi bảo mật quan trọng.
-   Xem và cập nhật dữ liệu cá nhân theo quyền và chính sách.
-   Rút lại sự đồng ý đối với mục đích xử lý khi pháp luật cho phép;
    việc rút lại không làm mất hiệu lực xử lý đã thực hiện hợp pháp
    trước đó và có thể ảnh hưởng chức năng liên quan.

------------------------------------------------------------------------

## 6. Luồng nghiệp vụ chính

### 6.1. Đăng ký tài khoản và tạo hồ sơ ATRS

1.  Người dùng đăng ký theo luồng hiện hữu của Vạn An.
2.  CoreHub tạo hoặc xác định `UserId`.
3.  ATRS nhận event hoặc được gọi để tạo Trust Profile.
4.  Ứng dụng giải thích mục đích thu thập và xử lý dữ liệu.
5.  Người dùng khai báo thông tin cá nhân cần thiết.
6.  Người dùng có thể đăng ký passkey/WebAuthn nếu thiết bị hỗ trợ.
7.  Hệ thống tạo Device ID riêng của Vạn An.
8.  Người dùng có thể thiết lập recovery codes.
9.  Người dùng có thể thiết lập secret challenge tùy chọn, kèm cảnh báo
    đây không phải phương thức bảo mật duy nhất.
10. ATRS ghi audit events và hoàn tất hồ sơ ở trạng thái phù hợp.

**Lưu ý UX:** Không nên bắt buộc người dùng hoàn thành tất cả thành phần
trước khi được sử dụng tính năng cơ bản. Có thể dùng trạng thái `BASIC`,
`PROTECTED`, `RECOVERY_READY` thay vì một cờ hoàn tất duy nhất.

### 6.2. Đăng nhập từ thiết bị đã đăng ký

1.  Ứng dụng gọi cơ chế xác thực hiện hữu.
2.  Nếu dùng passkey, server tạo WebAuthn challenge.
3.  Authenticator ký challenge.
4.  Server xác minh assertion.
5.  ATRS ghi nhận credential/device và event.
6.  Risk policy đánh giá hành động tiếp theo.
7.  Gateway/Authentication Service cấp hoặc duy trì phiên theo cơ chế
    hiện hữu.

### 6.3. Đăng nhập từ thiết bị mới nhưng còn thiết bị cũ

1.  Người dùng đăng nhập trên thiết bị mới.
2.  Hệ thống yêu cầu xác nhận bằng passkey có thể dùng trên thiết bị mới
    hoặc xác nhận từ thiết bị cũ.
3.  Người dùng xác thực step-up trên phương thức đã đăng ký.
4.  ATRS ghi nhận yêu cầu thêm thiết bị.
5.  Nếu đạt policy, đăng ký credential/device mới.
6.  Thông báo về thiết bị mới tới kênh liên hệ đã có.
7.  Giữ thiết bị cũ hoạt động hoặc cho người dùng thu hồi theo lựa chọn.

### 6.4. Mất thiết bị và không còn phương thức cũ

1.  Người dùng tạo Recovery Request.
2.  Hệ thống thu thập thông tin tối thiểu và bằng chứng hỗ trợ.
3.  Kiểm tra recovery code nếu có.
4.  Đối chiếu dữ liệu hồ sơ đã khai báo, secret challenge nếu có và lịch
    sử hoạt động phù hợp.
5.  Risk Engine quyết định tự động, yêu cầu thêm bằng chứng hoặc chuyển
    nhân viên.
6.  Trường hợp rủi ro cao được manual review và maker-checker khi cần.
7.  Nếu được duyệt, thu hồi credential/session cũ theo khả năng hệ
    thống.
8.  Người dùng đăng ký passkey/credential mới.
9.  Hệ thống áp dụng hạn chế giao dịch nhạy cảm hoặc delay theo policy.
10. Gửi thông báo và ghi đầy đủ audit events.

### 6.5. Khiếu nại điểm thưởng/ví

1.  Người dùng tạo Dispute Case từ ứng dụng hoặc bộ phận hỗ trợ.
2.  ATRS xác định `UserId` và các giao dịch liên quan.
3.  Lấy ledger references từ dịch vụ Loyalty/Wallet.
4.  Thu thập auth events, device events, session references và thay đổi
    bảo mật quanh thời điểm giao dịch.
5.  Đánh giá nguồn gốc và tính toàn vẹn của bằng chứng.
6.  Chuyển người có thẩm quyền xem xét.
7.  Nếu cần điều chỉnh, tạo yêu cầu nghiệp vụ tới Loyalty/Wallet.
8.  Loyalty/Wallet thực hiện reversal/adjustment theo quy trình riêng.
9.  ATRS lưu resolution reference và đóng hồ sơ.

------------------------------------------------------------------------

## 7. Mô hình dữ liệu logic

Tên bảng dưới đây là đề xuất logic; Devin phải đối chiếu schema và
naming convention hiện tại trước khi tạo migration.

### 7.1. `AccountTrustProfiles`

  Field            Type gợi ý          Ghi chú
  ---------------- ------------------- ----------------------------------------
  Id               UUID                Primary key
  UserId           FK/UUID             Liên kết tài khoản CoreHub, unique
  Status           varchar             PENDING_SETUP/ACTIVE/RESTRICTED/CLOSED
  AssuranceState   varchar             BASIC/PROTECTED/RECOVERY_READY
  CreatedAt        timestamp           UTC
  UpdatedAt        timestamp           UTC
  RowVersion       concurrency token   Chống ghi đè đồng thời

### 7.2. `UserDevices`

  ----------------------------------------------------------------------------------------
  Field                   Type gợi ý              Ghi chú
  ----------------------- ----------------------- ----------------------------------------
  Id                      UUID                    Device record ID

  UserId                  UUID                    Chủ tài khoản

  DeviceId                UUID/string random      ID do Vạn An cấp

  DeviceType              varchar                 Mobile/Desktop/Unknown

  Platform                varchar                 OS/browser/app ở mức tối thiểu

  FriendlyName            varchar                 Tên người dùng đặt

  RegistrationMethod      varchar                 WEBAUTHN/INSTALLATION_ID/OTHER

  Status                  varchar                 PENDING/ACTIVE/SUSPICIOUS/LOST/REVOKED

  FirstSeenAt             timestamp               UTC

  LastSeenAt              timestamp               UTC

  RevokedAt               timestamp nullable      UTC

  RiskMetadata            JSON                    Không chứa dữ liệu fingerprint nhạy cảm
                                                  không cần thiết
  ----------------------------------------------------------------------------------------

Unique/index đề xuất: - Unique (`UserId`, `DeviceId`). - Index
(`UserId`, `Status`). - Index (`LastSeenAt`).

### 7.3. `DeviceCredentials`

  Field            Type gợi ý           Ghi chú
  ---------------- -------------------- --------------------------
  Id               UUID                 PK
  UserId           UUID                 Chủ tài khoản
  DeviceRecordId   UUID                 FK UserDevices
  CredentialId     binary/base64url     Credential ID
  PublicKey        binary/text          Public key
  SignCount        bigint               Nếu authenticator hỗ trợ
  Aaguid           string nullable      Chỉ khi cần
  Transports       JSON nullable        Transport metadata
  BackupEligible   bool nullable        Metadata WebAuthn nếu có
  BackupState      bool nullable        Metadata WebAuthn nếu có
  Status           varchar              ACTIVE/REVOKED
  CreatedAt        timestamp            UTC
  LastUsedAt       timestamp nullable   UTC

Ràng buộc: - Credential ID phải unique toàn hệ thống theo cách biểu diễn
chuẩn. - Không có field PrivateKey. - Không log public key/credential
payload không cần thiết.

### 7.4. `PersonalEvidenceProfiles`

  Field                  Type gợi ý              Ghi chú
  ---------------------- ----------------------- ------------------------------------------------
  Id                     UUID                    PK
  UserId                 UUID                    Chủ tài khoản
  FieldKey               varchar                 FULL_NAME/DOB/CONTACT_PHONE
  EncryptedValue         encrypted text/binary   Mã hóa khi lưu nếu phù hợp
  Source                 varchar                 USER_DECLARED/VERIFIED_PROVIDER/STAFF_REVIEWED
  VerificationStatus     varchar                 UNVERIFIED/VERIFIED/REJECTED/EXPIRED
  CollectedAt            timestamp               UTC
  UpdatedAt              timestamp               UTC
  PrivacyNoticeVersion   varchar                 Phiên bản thông báo
  ConsentReference       string nullable         Tham chiếu bằng chứng đồng ý

Nên cân nhắc tách bảng theo field nếu cần kiểm soát quyền truy cập chi
tiết. Không tạo bản sao dữ liệu cá nhân đã có trong User Profile nếu chỉ
cần tham chiếu.

**Quyết định đã chốt (2026-11-15, ADR-001):** Personal Evidence là lớp
**metadata/provenance/reference** — KHÔNG copy PII đã có trong
`Customer`/`DemoUser`. Map nguồn thật: `CONTACT_PHONE` → đọc từ
`Customer.PhoneNumber` + verify state / `DemoUser.VerifiedChannel` +
`PhoneVerifiedAt`; `DOB` → `Customer.Birthday` (loyalty mission đã có).
Bảng này chỉ lưu `source`, `verificationStatus`, `privacyNoticeVersion`,
`consentReference` và pointer (không duplicate giá trị).

### 7.5. `SecretChallenges`

  Field            Type gợi ý                Ghi chú
  ---------------- ------------------------- -----------------------------------------
  Id               UUID                      PK
  UserId           UUID                      Chủ tài khoản
  QuestionCode     varchar                   Mã câu hỏi
  AnswerHash       binary/text               Argon2id hash
  AnswerSalt       binary                    Nếu không được nhúng trong encoded hash
  HintEncrypted    encrypted text nullable   Gợi ý, không phải đáp án
  Status           varchar                   ACTIVE/REVOKED
  FailedAttempts   int                       Có thể dùng counter có kiểm soát
  LockedUntil      timestamp nullable        UTC
  CreatedAt        timestamp                 UTC
  UpdatedAt        timestamp                 UTC

Không lưu đáp án rõ hoặc log đáp án. Pepper nếu dùng phải lưu trong
secret manager/KMS, không cùng database.

### 7.6. `RecoveryRequests`

  Field                Type gợi ý           Ghi chú
  -------------------- -------------------- ---------------------------------------
  Id                   UUID                 PK
  UserId               UUID                 Chủ tài khoản
  RequestType          varchar              LOST_DEVICE/NO_CREDENTIAL/COMPROMISED
  Status               varchar              Trạng thái recovery
  NewDeviceId          UUID nullable        Thiết bị mới
  EvidenceSummary      JSON                 Chỉ metadata/tóm tắt
  RiskDecision         varchar              ALLOW/REVIEW/DENY...
  PolicyVersion        varchar              Phiên bản luật
  ReviewerId           UUID nullable        Người xử lý
  DecisionReasonCode   varchar nullable     Mã lý do
  CreatedAt            timestamp            UTC
  CompletedAt          timestamp nullable   UTC

### 7.7. `DisputeCases`, `DisputeEvidenceItems`, `DisputeDecisions`

Tách hồ sơ, từng bằng chứng và quyết định thành các bảng riêng.

`DisputeEvidenceItems` tối thiểu: - Id - DisputeCaseId - EvidenceType -
SourceSystem - SourceReference - CapturedAt - RecordedAt - IntegrityHash
nếu có - SubmittedBy - ReliabilityClass - Description -
AccessClassification

`DisputeDecisions` tối thiểu: - Id - DisputeCaseId - Decision -
DecisionReason - PolicyVersion - DecidedBy - ApprovedBy nếu
maker-checker - DecidedAt - ResolutionReference

### 7.8. `AccountSecurityEvents`

Bảng append-only ở cấp ứng dụng, hoặc publish tới nền tảng event/audit
hiện hữu.

Tối thiểu: - EventId - UserId - EventType - ActorType - ActorId -
SourceApplication - DeviceId - SessionReference - OccurredAt -
ReceivedAt - Outcome - ReasonCode - CorrelationId - PayloadVersion -
MinimalMetadata

------------------------------------------------------------------------

## 8. API contract đề xuất

Đây là API logic; Devin phải tuân theo convention API hiện hữu,
versioning, auth middleware và DTO chuẩn của codebase.

  ---------------------------------------------------------------------------------------------------
  Method                  Endpoint logic                                      Mục đích
  ----------------------- --------------------------------------------------- -----------------------
  GET                     `/api/v1/account-trust/me`                          Xem hồ sơ ATRS của mình

  POST                    `/api/v1/account-trust/setup`                       Khởi tạo/hoàn thiện hồ
                                                                              sơ

  GET                     `/api/v1/account-trust/devices`                     Liệt kê thiết bị

  POST                    `/api/v1/account-trust/devices/register`            Đăng ký device record

  POST                    `/api/v1/account-trust/devices/{id}/revoke`         Thu hồi thiết bị

  POST                    `/api/v1/account-trust/webauthn/register/options`   Tạo registration
                                                                              challenge

  POST                    `/api/v1/account-trust/webauthn/register/verify`    Xác minh registration

  POST                    `/api/v1/account-trust/webauthn/auth/options`       Tạo authentication
                                                                              challenge

  POST                    `/api/v1/account-trust/webauthn/auth/verify`        Xác minh assertion

  PUT                     `/api/v1/account-trust/personal-evidence`           Cập nhật thông tin khai
                                                                              báo

  POST                    `/api/v1/account-trust/secret-challenge`            Thiết lập/đổi secret
                                                                              challenge

  POST                    `/api/v1/account-trust/secret-challenge/verify`     Xác minh đáp án trong
                                                                              policy

  POST                    `/api/v1/account-trust/recovery-requests`           Tạo yêu cầu recovery

  GET                     `/api/v1/account-trust/recovery-requests/{id}`      Xem trạng thái recovery

  POST                    `/api/v1/account-trust/disputes`                    Tạo hồ sơ khiếu nại

  GET                     `/api/v1/account-trust/disputes/{id}`               Xem trạng thái hồ sơ

  POST                    `/api/v1/account-trust/disputes/{id}/evidence`      Bổ sung bằng chứng
  ---------------------------------------------------------------------------------------------------

Admin endpoints phải nằm trong route/permission riêng, không dùng chung
quyền người dùng.

### 8.1. API security

-   Yêu cầu access token hợp lệ theo cơ chế hiện tại.
-   Kiểm tra `UserId` từ principal/token; không tin `UserId` do client
    gửi.
-   CSRF protection nếu dùng cookie-based session.
-   CORS allowlist theo domain ứng dụng Vạn An.
-   Rate limit theo tài khoản, IP/network signal ở mức hợp pháp và
    endpoint.
-   Idempotency cho create recovery/dispute và các command có thể retry.
-   Challenge WebAuthn phải one-time, expire và gắn đúng origin/RP ID.
-   Không trả thông tin giúp dò tài khoản hoặc dò đáp án.
-   Không tiết lộ dữ liệu cá nhân trong error message.
-   API response phải có correlation ID để truy vết.

------------------------------------------------------------------------

## 9. Chính sách bằng chứng và mức đảm bảo

Không dùng một điểm số cộng đơn giản để tuyên bố một người là chủ tài
khoản. Phải đánh giá loại bằng chứng, nguồn, tính độc lập, thời điểm và
rủi ro.

### 9.1. Phân loại tham khảo

  -----------------------------------------------------------------------
  Bằng chứng              Giá trị tham khảo       Giới hạn
  ----------------------- ----------------------- -----------------------
  WebAuthn assertion từ   Cao cho quyền sử dụng   Không xác minh danh
  credential đã đăng ký   credential              tính pháp lý

  Recovery code hợp lệ,   Trung bình--cao         Có thể bị đánh cắp
  chưa dùng                                       

  Xác nhận từ thiết       Cao                     Thiết bị có thể bị
  bị/credential cũ                                chiếm quyền

  Device Installation ID  Thấp--trung bình        Có thể mất/sao chép

  Device fingerprint      Thấp                    Có thể thay đổi/giả mạo

  Secret answer           Thấp                    Dễ đoán/lộ

  Họ tên/ngày sinh tự     Thấp                    Không chứng minh danh
  khai                                            tính

  Ledger event có kiểm    Cao để chứng minh bản   Không tự chứng minh
  toán                    ghi giao dịch           người thao tác

  Staff decision có       Giá trị quy trình       Phụ thuộc kiểm soát nội
  maker-checker                                   bộ
  -----------------------------------------------------------------------

### 9.2. Mức xử lý recovery đề xuất

  -----------------------------------------------------------------------
  Mức                     Điều kiện ví dụ         Hành động
  ----------------------- ----------------------- -----------------------
  R0 -- Routine           Credential hợp lệ,      Cho phép thao tác
                          thiết bị không có tín   thường
                          hiệu bất thường         

  R1 -- New Device        Credential hợp lệ hoặc  Thêm thiết bị, thông
                          xác nhận thiết bị cũ    báo

  R2 -- Recovery Assisted Mất thiết bị, có        Step-up, có thể cần trì
                          recovery code hoặc bằng hoãn
                          chứng độc lập phù hợp   

  R3 -- High Risk         Không còn               Manual review,
                          credential/recovery     maker-checker
                          code, có quyền lợi giá  
                          trị cao hoặc tín hiệu   
                          bất thường              

  R4 -- Suspected         Có dấu hiệu chiếm quyền Tạm hạn chế hành động
  Takeover                rõ ràng                 nhạy cảm, điều tra
  -----------------------------------------------------------------------

Ngưỡng tiền/điểm, thời gian delay và số lần thử phải cấu hình theo chính
sách nghiệp vụ, không hard-code trong code.

### 9.3. Quy tắc chống lạm dụng

-   Rate limit theo UserId và các tín hiệu phù hợp.
-   Lockout tăng dần, tránh khóa vĩnh viễn dễ bị lợi dụng để DoS tài
    khoản.
-   Không cho phép kiểm tra đáp án bí mật vô hạn.
-   Không tiết lộ câu hỏi/gợi ý trong quy trình chưa xác thực.
-   Thông báo thay đổi bảo mật qua kênh liên hệ đã đăng ký.
-   Thêm thiết bị mới không tự động xóa thiết bị cũ.
-   Recovery thành công phải thu hồi session/credential cũ theo policy.
-   Không cho phép cùng một nhân viên vừa tạo vừa phê duyệt điều chỉnh
    giá trị tài chính vượt ngưỡng.
-   Không cho phép support staff thay đổi thông tin bảo mật chỉ dựa trên
    lời nói của người gọi.

------------------------------------------------------------------------

## 10. Yêu cầu phi chức năng

### NFR-001 -- Security

-   Tuân thủ OWASP ASVS phù hợp với mức rủi ro của ứng dụng.
-   Dùng thư viện WebAuthn đã được kiểm chứng; không tự triển khai thuật
    toán chữ ký.
-   TLS cho mọi kết nối.
-   Mã hóa dữ liệu cá nhân nhạy cảm khi lưu.
-   Secrets/pepper/keys quản lý qua secret manager hoặc KMS nếu hạ tầng
    cho phép.
-   RBAC/least privilege.
-   Audit access tới dữ liệu nhạy cảm.
-   Không log password, secret answer, recovery code, token hoặc private
    key.
-   Dependency scanning và security testing trong CI/CD.

### NFR-002 -- Privacy

-   Có data inventory và mục đích xử lý cho từng trường.
-   Chỉ thu thập dữ liệu cần thiết.
-   Phân biệt dữ liệu tự khai và dữ liệu đã xác minh.
-   Có retention schedule.
-   Có quy trình xử lý yêu cầu truy cập/chỉnh sửa/xóa/hạn chế xử lý theo
    quy định áp dụng.
-   Đánh giá nghĩa vụ lập hồ sơ đánh giá tác động xử lý dữ liệu cá nhân
    và các nghĩa vụ liên quan trước triển khai.
-   Không dùng Device ID để theo dõi chéo mục đích hoặc chia sẻ cho bên
    thứ ba ngoài mục đích đã thông báo và căn cứ hợp pháp.

### NFR-003 -- Availability & Resilience

-   Nếu ATRS tạm ngừng, không được làm sập các chức năng đọc cơ bản
    không cần ATRS.
-   Các hành động nhạy cảm có thể fail closed hoặc yêu cầu step-up theo
    policy.
-   Retry phải idempotent.
-   Có backup, restore test và migration rollback.
-   Event phải có khả năng retry/dead-letter theo nền tảng hiện hữu.

### NFR-004 -- Performance

Mục tiêu ban đầu để kiểm thử, không phải số liệu tải đã đo:

-   API đọc Trust Profile: p95 dưới 300 ms khi không gọi dịch vụ ngoài.
-   API liệt kê thiết bị: p95 dưới 300 ms.
-   Ghi audit event đồng bộ hoặc qua outbox không làm kéo dài đáng kể
    luồng người dùng.
-   WebAuthn ceremony phụ thuộc thiết bị/trình duyệt và không áp SLA
    mạng tuyệt đối.
-   Recovery/dispute review là workflow bất đồng bộ.

### NFR-005 -- Scalability

-   Hỗ trợ tăng trưởng từ pilot tới hàng triệu tài khoản.
-   Phân trang danh sách thiết bị/event/case.
-   Index theo UserId, trạng thái, thời gian, case ID.
-   Không tải toàn bộ lịch sử vào một request.
-   Tách truy vấn nghiệp vụ khỏi truy vấn audit lớn khi cần.
-   Không yêu cầu microservices độc lập ngay từ đầu; có thể triển khai
    module trong CoreHub nếu phù hợp codebase, nhưng giữ boundary rõ
    ràng.

### NFR-006 -- Observability

-   Structured logging, metrics, tracing/correlation ID.
-   Dashboard cho số lượng recovery, failure, lockout, device
    registration, dispute backlog.
-   Alert khi tăng đột biến các lần thử secret challenge, recovery hoặc
    thay đổi thiết bị.
-   Không đưa PII/secret vào telemetry.

------------------------------------------------------------------------

## 11. Bảo vệ dữ liệu và tuân thủ pháp lý

Trước khi phát hành, đội pháp lý/tuân thủ phải rà soát quy định hiện
hành tại Việt Nam, đặc biệt pháp luật về bảo vệ dữ liệu cá nhân và các
quy định chuyên ngành áp dụng cho dịch vụ/giá trị mà Vạn An cung cấp.

Yêu cầu sản phẩm:

1.  Thông báo minh bạch về loại dữ liệu, mục đích, thời hạn lưu, bên xử
    lý/nhận dữ liệu và quyền của chủ thể dữ liệu.
2.  Cơ chế ghi nhận sự đồng ý khi căn cứ xử lý yêu cầu sự đồng ý; sự
    đồng ý phải cụ thể và có thể chứng minh.
3.  Không gộp consent cho mục đích không liên quan.
4.  Không bắt buộc cung cấp ngày sinh hoặc secret question nếu không cần
    thiết cho chức năng mà người dùng yêu cầu.
5.  Có cơ chế rút lại sự đồng ý và giải thích hệ quả hợp lý đối với tính
    năng.
6.  Có quy trình yêu cầu truy cập, chỉnh sửa, xóa hoặc hạn chế xử lý
    theo luật.
7.  Xác định retention period cho hồ sơ recovery/dispute và audit; không
    lưu vô thời hạn theo mặc định.
8.  Kiểm soát chuyển dữ liệu cho nhà cung cấp bên ngoài.
9.  Đánh giá vai trò controller/processor giữa các pháp nhân Vạn An và
    đối tác.
10. Không quảng bá "Device ID xác minh danh tính" hoặc "câu hỏi bí mật
    chứng minh chủ tài khoản".

**Lưu ý:** Tài liệu này là yêu cầu kỹ thuật/sản phẩm, không thay thế ý
kiến tư vấn pháp lý. Legal review phải kiểm tra văn bản đang có hiệu lực
tại thời điểm triển khai, loại dữ liệu, mục đích, quy mô và vai trò pháp
lý của từng pháp nhân.

------------------------------------------------------------------------

## 12. Tích hợp với hệ sinh thái Vạn An

  -----------------------------------------------------------------------
  Hệ thống                            Vai trò tích hợp
  ----------------------------------- -----------------------------------
  CoreHub                             Nguồn identity/domain dùng chung
                                      (class library — KHÔNG host ATRS
                                      API; xem ADR-001)

  `vanan-atrs`                        Service ATRS độc lập (port 5080,
                                      PG riêng) — hồ sơ trust, device
                                      credential, recovery, dispute

  Gateway                             Xác thực API, routing,
                                      authorization, rate limit

  KhachLink                           Đăng ký thiết bị người mua, xem
                                      thiết bị, recovery

  TimLaThay                           Dùng chung UserId và trust profile

  Loyalty                             Tham chiếu ledger điểm và dispute

  Wallet (nếu có)                     Tham chiếu ledger giá trị; step-up
                                      trước thao tác nhạy cảm

  Booking                             Bảo vệ tài khoản và dữ liệu đặt chỗ

  ShopERP                             Bảo vệ tài khoản nhân viên/chủ
                                      shop; áp policy theo vai trò

  HTX/Co-op module                    Phân quyền staff và workflow xử lý
                                      theo tenant
  -----------------------------------------------------------------------

Không cho phép một tenant hoặc ứng dụng đọc toàn bộ personal evidence
của người dùng chỉ vì được xác thực trong hệ sinh thái. Mỗi
client/service phải có scope rõ ràng.

------------------------------------------------------------------------

## 13. Phân quyền đề xuất

  ------------------------------------------------------------------------------------
  Role             Xem profile cơ    Xem device     Recovery      Dispute   Điều chỉnh
                              bản                     review       review       ledger
  ---------------- -------------- ------------- ------------ ------------ ------------
  End User               Của mình      Của mình  Tạo/yêu cầu  Tạo/xem của        Không
                                                                     mình 

  Customer Support            Tối  Metadata hạn  Không duyệt       Triage        Không
  L1                 thiểu/masked           chế                           

  Recovery              Theo case     Theo case   Duyệt theo    Không mặc        Không
  Reviewer                                             quyền         định 

  Dispute Reviewer      Theo case     Theo case    Không mặc        Xử lý    Không mặc
                                                        định                      định

  Finance/Ledger     Theo nhu cầu         Không        Không     Xem tham      Theo hệ
  Operator                                                          chiếu thống ledger

  Security Admin   Theo quyền đặc       Quản lý        Chính     Giám sát        Không
                             biệt                  sách/giám              
                                                         sát              

  Super Admin      Không mặc định          Theo     Theo quy     Theo quy    Không mặc
                    toàn quyền dữ   break-glass        trình        trình         định
                             liệu                                         
  ------------------------------------------------------------------------------------

Mọi quyền đặc biệt phải có audit, lý do và thời hạn nếu là quyền tạm
thời.

------------------------------------------------------------------------

## 14. Tiêu chí nghiệm thu (Acceptance Criteria)

### AC-001 -- Account binding

-   Tạo được đúng một ATRS profile cho một `UserId`.
-   Không tạo user mới.
-   Người dùng A không thể đọc/sửa profile của người dùng B.

### AC-002 -- Device registry

-   Đăng ký được Device ID ngẫu nhiên.
-   Có thể thêm nhiều thiết bị theo policy.
-   Có thể đánh dấu mất và thu hồi.
-   Xóa browser storage không làm hệ thống nhầm rằng thiết bị mới chắc
    chắn là thiết bị cũ.

### AC-003 -- WebAuthn

-   Đăng ký credential thành công trên browser/platform được hỗ trợ.
-   Assertion hợp lệ được chấp nhận.
-   Challenge hết hạn, sai origin/RP ID, sai chữ ký hoặc replay bị từ
    chối.
-   Credential revoked không thể dùng để xác thực.
-   Không có private key trong database, log hoặc API response.

### AC-004 -- Personal evidence

-   Lưu được nguồn gốc và trạng thái xác minh.
-   Dữ liệu tự khai không bị gắn nhãn verified.
-   Có audit khi thay đổi.
-   Có masking/authorization.

### AC-005 -- Secret challenge

-   Đáp án không được lưu rõ.
-   Không xuất hiện trong log.
-   Có rate limit và lockout phù hợp.
-   Đáp án đúng không đủ để tự động cấp quyền recovery rủi ro cao.
-   Không thể dò đáp án qua response timing hoặc error message khác biệt
    rõ rệt.

### AC-006 -- Recovery

-   Tạo được recovery request với trạng thái đầy đủ.
-   Có idempotency và rate limit.
-   Hỗ trợ manual review.
-   Recovery được duyệt phải tạo event, thu hồi credential/session cũ
    theo policy và cho phép đăng ký phương thức mới.
-   Không có đường tắt chỉ dựa trên tên, ngày sinh hoặc secret answer.

### AC-007 -- Dispute

-   Tạo hồ sơ và liên kết transaction/ledger references.
-   Không sửa dữ liệu ledger từ ATRS.
-   Mọi quyết định có actor, reason, timestamp và policy version.
-   Điều chỉnh điểm/ví được thực hiện bởi hệ thống ledger theo quy trình
    riêng.

### AC-008 -- Audit & privacy

-   Mọi thao tác nhạy cảm có audit event.
-   Nhân viên không xem được secret answer.
-   API không trả dữ liệu thừa.
-   Có thể xuất lịch sử xử lý theo quyền.
-   Có quy trình retention/deletion được cấu hình.

### AC-009 -- Resilience

-   Retry không tạo trùng recovery/dispute.
-   Lỗi ATRS không làm hỏng giao dịch ledger.
-   Có backup/restore test.
-   Có migration rollback hoặc phương án forward-fix rõ ràng.

------------------------------------------------------------------------

## 15. Kế hoạch kiểm thử

### 15.1. Unit tests

-   Device ID generation và uniqueness.
-   State transition của device/recovery/dispute.
-   Policy evaluation.
-   Authorization theo UserId và role.
-   Secret answer normalization/hash/verify.
-   Rate limit/lockout.
-   Idempotency.
-   Audit event creation.

### 15.2. Integration tests

-   CoreHub user lifecycle ↔ ATRS profile.
-   Gateway auth ↔ ATRS authorization.
-   WebAuthn registration/assertion ↔ credential persistence.
-   ATRS ↔ Loyalty ledger references.
-   ATRS ↔ notification service nếu có.
-   Outbox/event retry và duplicate delivery.

### 15.3. Security tests

-   IDOR/BOLA: truy cập profile/device/case của người khác.
-   Replay WebAuthn challenge.
-   Origin/RP ID mismatch.
-   Brute-force secret challenge.
-   Recovery enumeration.
-   Session fixation/hijacking.
-   CSRF/CORS misconfiguration.
-   Privilege escalation ở admin endpoints.
-   Mass assignment.
-   Sensitive data leakage trong logs/errors.
-   Race condition khi thu hồi credential và recovery đồng thời.
-   Abuse lockout để gây DoS.
-   Insider access và maker-checker bypass.

### 15.4. E2E scenarios bắt buộc

1.  Người dùng mới đăng ký, tạo profile và đăng ký passkey.
2.  Người dùng đăng nhập lại trên thiết bị đã đăng ký.
3.  Người dùng thêm thiết bị mới khi còn thiết bị cũ.
4.  Người dùng mất thiết bị nhưng còn recovery code.
5.  Người dùng mất cả thiết bị và recovery code, phải manual review.
6.  Kẻ tấn công biết tên/ngày sinh/secret answer nhưng không có
    credential.
7.  Người dùng báo thiết bị cũ bị mất và thu hồi.
8.  Người dùng khiếu nại giao dịch trừ điểm.
9.  Người dùng khiếu nại số dư ví.
10. Nhân viên không đủ quyền cố xem secret hoặc duyệt recovery.
11. Hai nhân viên thực hiện maker-checker.
12. ATRS mất kết nối trong lúc Loyalty ghi ledger.

------------------------------------------------------------------------

## 16. Lộ trình triển khai

### Phase 0 -- Codebase discovery và design validation

-   Đọc cơ chế User/Auth hiện hữu.
-   Đọc schema User, session, device, audit và notification hiện hữu.
-   Đọc Gateway authorization và tenant context.
-   Đọc Loyalty/Wallet ledger contract.
-   Xác định thư viện WebAuthn phù hợp với stack hiện tại.
-   Lập bảng reuse/modify/new/remove.
-   Không tạo migration hoặc sửa code trước khi hoàn tất báo cáo
    discovery và được duyệt.

### Phase 1 -- Trust Profile & Device Registry

-   AccountTrustProfiles.
-   UserDevices.
-   Basic Personal Evidence.
-   Audit events.
-   API và UI quản lý thiết bị.
-   Không bắt buộc WebAuthn ở giai đoạn đầu nếu chưa tích hợp xong.

### Phase 2 -- WebAuthn & Recovery Codes

-   WebAuthn registration/authentication.
-   Credential management.
-   Recovery codes.
-   Device step-up.
-   Thu hồi credential.

### Phase 3 -- Recovery Workflow

-   Recovery request.
-   Policy engine luật xác định.
-   Manual review.
-   Thông báo bảo mật.
-   Session/credential revocation integration.

### Phase 4 -- Dispute Evidence

-   Dispute cases.
-   Evidence references.
-   Loyalty/Wallet ledger integration.
-   Maker-checker.
-   Báo cáo và export.

### Phase 5 -- Hardening & Scale

-   Penetration/security testing.
-   Privacy review.
-   Backup/restore.
-   Observability.
-   Load test.
-   Tối ưu retention và phân vùng dữ liệu nếu cần.

------------------------------------------------------------------------

## 17. Ràng buộc triển khai cho Devin / AI Coding Agent

### 17.1. Quy tắc trước khi code

Devin phải:

1.  Đọc toàn bộ codebase liên quan đến User/Auth, CoreHub, Gateway, PWA,
    audit, Loyalty/Wallet.
2.  Xác định công nghệ, framework, convention và schema thực tế.
3.  Không giả định hệ thống đang dùng ASP.NET/Blazor hoặc bất kỳ
    framework nào nếu chưa kiểm tra.
4.  Tạo báo cáo `BASE CODE vs SRS GAP ANALYSIS` theo 4 nhóm:
    -   Existing -- đã có và có thể tái sử dụng.
    -   Modify -- có nhưng cần sửa.
    -   Missing -- chưa có, cần bổ sung.
    -   Wrong/Remove -- sai hoặc cần loại bỏ, kèm lý do và tác động.
5.  Xác định xung đột với luồng đăng nhập hiện tại.
6.  Đề xuất migration/API/permission trước khi triển khai.
7.  Nêu rõ các điểm chưa xác định và hỏi Product Owner khi có quyết định
    nghiệp vụ không thể suy ra an toàn.

### 17.2. Quy tắc không được vi phạm

-   Không tạo User/Auth service thứ hai.
-   Không tạo `UserId` độc lập.
-   Không phá vỡ login/session/token hiện tại.
-   Không tạo mock/stub để giả vờ WebAuthn hoặc recovery đã hoạt động.
-   Không dùng Device Fingerprint làm bằng chứng duy nhất.
-   Không lưu private key hoặc secret answer dạng rõ.
-   Không tự tạo thuật toán mật mã.
-   Không sửa số dư/ledger từ ATRS.
-   Không dùng `EnsureCreated` trong production.
-   Không bỏ qua migration, authorization, tenant isolation hoặc audit.
-   Không hard-code ngưỡng rủi ro/giá trị tài chính.
-   Không tự động cấp quyền admin hoặc quyền xem PII cho support.
-   Không tuyên bố hoàn tất nếu chưa có test thực thi và bằng chứng
    test.

### 17.3. Quy tắc chất lượng

-   TDD cho domain rules quan trọng.
-   Integration tests cho persistence, auth và WebAuthn.
-   Playwright E2E cho luồng người dùng nếu frontend hỗ trợ.
-   Có negative tests cho mọi endpoint nhạy cảm.
-   Có migration up/down hoặc phương án rollback phù hợp.
-   Có security review trước release.
-   Cập nhật `project_state.md` và tài liệu kiến trúc theo convention
    repository nếu các file này đang tồn tại.
-   Không sửa các module ngoài phạm vi nếu chưa chứng minh có phụ thuộc
    trực tiếp.

### 17.4. Deliverables bắt buộc

1.  `BASE_CODE_GAP_ANALYSIS.md`
2.  Architecture decision record (ADR).
3.  ERD/data model.
4.  API contract/OpenAPI.
5.  Migration scripts.
6.  Backend implementation.
7.  Frontend/PWA integration.
8.  Unit/integration/E2E tests.
9.  Security test report.
10. Deployment and rollback guide.
11. Operational runbook cho recovery/dispute.
12. Danh sách known limitations và residual risks.

------------------------------------------------------------------------

## 18. Các quyết định Product Owner cần xác nhận

> **Cập nhật 2026-11-15 — đã chốt theo `docs/AI/plans/atrs-decision-support.md`:**
> ① Ngày sinh: tùy chọn/omit v1.0 · ② Secret question: **OMIT** (D7) · ③ Số thiết bị tối đa: **3** (đã có `DeviceRegistrationService`) · ④ Delay recovery: config, default hạn chế giao dịch nhạy cảm 24h · ⑤ Maker-checker: mọi adjust điểm cần 2 người (ngưỡng config) · ⑥ Nhóm nhân viên: 8 permission groups UI §4.2 (D5) · ⑦ Retention: case+evidence 5 năm, security events 2 năm, personal evidence theo mục đích · ⑧ Kênh liên hệ: email (nếu có) + in-app; SMS/ZNS phụ thuộc P4 · ⑨ Apps đợt đầu: KhachLink + ShopERP admin · ⑩ Session/token: giữ JWT + customer token, thêm `TokenVersion` revocation (D3).
>
> Các mục dưới đây giữ nguyên danh sách gốc để đối chiếu:

1.  Người dùng có bắt buộc khai báo ngày sinh không, hay chỉ khuyến
    nghị?
2.  Câu hỏi bí mật có bắt buộc không, hay thay bằng recovery
    codes/passkeys?
3.  Số lượng thiết bị tối đa mặc định là bao nhiêu?
4.  Recovery có thời gian trì hoãn bao lâu đối với tài khoản có điểm/giá
    trị ví?
5.  Ngưỡng nào yêu cầu maker-checker?
6.  Những nhóm nhân viên nào được quyền xem và xử lý dispute?
7.  Dữ liệu cá nhân và hồ sơ dispute được lưu bao lâu theo mục đích và
    nghĩa vụ pháp lý?
8.  Kênh thông báo bảo mật nào đang có sẵn và đã được người dùng đăng
    ký?
9.  Các ứng dụng nào được tích hợp trong đợt đầu?
10. Cơ chế xác thực hiện hữu nào sẽ tiếp tục là nguồn cấp session/token?

**Giá trị mặc định an toàn đề xuất:**

-   Ngày sinh: tùy chọn nếu không có nhu cầu nghiệp vụ/pháp lý cụ thể.
-   Secret question: tùy chọn, không phải recovery factor duy nhất.
-   Passkey: khuyến nghị.
-   Recovery codes: khuyến nghị.
-   Nhiều thiết bị: cho phép, có giới hạn cấu hình.
-   Tài khoản có giá trị tài chính/quyền quản trị: recovery tăng cường.
-   OTP: không phải mặc định; có thể là một phương án dự phòng nếu được
    phê duyệt và có nhà cung cấp phù hợp.
-   Device Installation ID: chỉ là tín hiệu nhận diện, không phải
    credential bảo mật.

------------------------------------------------------------------------

## 19. Definition of Done

ATRS v1.0 chỉ được xem là hoàn thành khi:

-   Tích hợp với `UserId` và authentication hiện hữu, không tạo identity
    silo.
-   Có Device Registry và trạng thái thiết bị.
-   Có WebAuthn/passkey hoạt động thật trên các nền tảng được công bố hỗ
    trợ, hoặc có tài liệu rõ phần chưa hỗ trợ.
-   Có recovery workflow với kiểm soát rủi ro.
-   Secret challenge không thể dùng đơn lẻ để chiếm tài khoản.
-   Có audit trail và quyền truy cập phân lớp.
-   Có dispute workflow và liên kết ledger reference.
-   Không có thao tác sửa số dư trực tiếp từ ATRS.
-   Các acceptance criteria trọng yếu đều có test.
-   Security/privacy review được hoàn tất.
-   Có backup/restore, vận hành, giám sát và rollback plan.
-   Product Owner chấp thuận các quyết định nghiệp vụ còn mở.

------------------------------------------------------------------------

## 20. Kết luận thiết kế

ATRS không phải là một hệ thống "Device ID thay OTP". Đây là một lớp
quản trị bằng chứng về quyền kiểm soát tài khoản và lịch sử xử lý sự cố
dùng chung toàn hệ sinh thái Vạn An.

Kiến trúc cần bảo đảm bốn ranh giới:

1.  **Device Identity** giúp nhận diện thiết bị và xác minh quyền sử
    dụng credential.
2.  **Personal Evidence** lưu thông tin khai báo/xác minh với nguồn gốc
    rõ ràng.
3.  **Recovery & Dispute** đánh giá nhiều bằng chứng, áp dụng chính sách
    và lưu quyết định.
4.  **Transaction Ledger** vẫn là nguồn dữ liệu chuẩn về điểm/giá trị;
    ATRS chỉ cung cấp bằng chứng về tài khoản và tham chiếu giao dịch.

Mục tiêu cuối cùng là giảm phụ thuộc OTP và giảm ma sát cho người dùng,
nhưng không đánh đổi an toàn tài khoản, tính toàn vẹn của quyền lợi hoặc
khả năng kiểm toán của Vạn An.
