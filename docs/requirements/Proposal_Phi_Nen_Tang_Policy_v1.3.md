# VẠN AN ECOSYSTEM — PROPOSAL: PHÍ NỀN TẢNG POLICY v1.3

**Cơ chế phí nền tảng cho dịch vụ số — dựa trên đo lường tài nguyên, đóng góp và quỹ HTX**

| Thông tin tài liệu | Nội dung |
|---|---|
| Phiên bản | **v1.3** — đổi tên VA Credits → **"Phí nền tảng"**; Rate Card do SystemAdmin quản trị; Metering là module độc lập (container/VPS riêng); roadmap metering 3 giai đoạn |
| Ngày lập | 23/10/2026 (v1.0: 22/10/2026) |
| Phạm vi | Tenant; salesman; shipper; buyer; HTX; thành viên HTX có hoặc không góp vốn |
| Trạng thái | Chưa phải chính sách giá chính thức; cần kiểm chứng chi phí, xác minh base code và rà soát pháp lý trước triển khai |
| Tài liệu liên quan | SRS: `van_an_phi_nen_tang_metering_srs_v1.md` · Master plan: `docs/AI/plans/phi-nen-tang-metering-master-plan.md` |

---

## Tóm tắt điều hành

**Phí nền tảng** là cơ chế thu phí sử dụng dịch vụ số trong hệ sinh thái Vạn An, tính qua đơn vị định mức nội bộ gọi là **credit**. Phí nền tảng giúp chuẩn hóa cách phân bổ chi phí tài nguyên như AI, OCR, xử lý báo cáo, tự động hóa và lưu trữ bổ sung; đồng thời cho phép HTX tài trợ hạn mức cho thành viên và thưởng cho đóng góp đã xác minh.

Mô hình gồm ba cơ chế bổ trợ: (1) Phí nền tảng theo mức dùng – pay-as-you-use; (2) HTX Pool – quỹ credit chung do HTX quản lý; (3) Earned Credits – credit nhận từ đóng góp thực tế. Ba cơ chế dùng chung bộ máy định mức nhưng phải tách nguồn số dư, điều kiện sử dụng, người tài trợ và sổ cái.

Nguyên tắc thiết kế: không miễn phí 100%; không buộc mọi người mua gói lớn; không khóa chức năng thiết yếu khi hết credit; không nhầm credit với tiền, vốn góp HTX, tiền công hoặc điểm thưởng liên minh.

**Quyết định v1.3 (user chốt 23/10/2026):**

1. Tên gọi chính thức: **"Phí nền tảng"** (đơn vị định mức nội bộ vẫn là "credit").
2. **Catalog/Rate Card do SystemAdmin quản trị** — thêm/xóa/sửa hạng mục dịch vụ và mức giá linh hoạt, có version và ngày hiệu lực.
3. **Metering là module độc lập, ít dính mắc code hiện tại** — chạy riêng trên 1 docker container, có thể triển khai trên VPS riêng; có thể gắn vào hệ thống khác để tính phí dịch vụ (tích hợp qua API contract, không chia sẻ code/DB).
4. **Roadmap metering 3 giai đoạn:** MVP đơn giản nhất → Trung bình → Hoàn thiện (đáp ứng mọi yêu cầu khắc khe về kỹ thuật, pháp lý, kế toán, kinh doanh).

---

## 1. Bối cảnh và vấn đề cần giải quyết

- Mô hình SaaS thuê bao cố định có thể không phù hợp đồng đều với cửa hàng nhỏ, cộng tác viên, người mua và HTX.
- Chi phí kỹ thuật biến thiên theo loại tác vụ: lưu trữ, băng thông, CPU, AI/token, OCR, đồng bộ, báo cáo và tác vụ nền.
- Miễn phí hoàn toàn có thể khiến dịch vụ bị xem nhẹ, tạo tải không kiểm soát và không giúp người dùng hiểu giá trị.
- Thu phí theo từng MB, giây xử lý hoặc đơn vị CPU trực tiếp có thể khó hiểu, khó dự báo và tạo hóa đơn thiếu ổn định.
- HTX cần cơ chế phân bổ ngân sách công nghệ và khuyến khích thành viên đóng góp mà không phải cấp tiền mặt cho mọi hoạt động.
- Metering là bài toán **chung của nhiều hệ thống**, không riêng Vạn An — nên tách thành service độc lập để tái sử dụng và giảm coupling với codebase hiện tại.

---

## 2. Mục tiêu và nguyên tắc thiết kế

| Mục tiêu | Nguyên tắc đề xuất |
|---|---|
| Giảm rào cản gia nhập | Có gói khởi đầu chi phí thấp và hạn mức hữu hạn; không yêu cầu nạp lớn. |
| Bảo đảm giá trị cảm nhận | Nêu rõ dịch vụ, hạn mức, định mức và chi phí vượt mức; tránh "miễn phí mọi thứ mãi mãi". |
| Thu hồi chi phí hợp lý | Định giá dựa trên chi phí đo được, dự phòng, hỗ trợ, vận hành và biên lợi nhuận mục tiêu. |
| Tạo động lực mạng lưới | HTX có thể mua/cấp quỹ; thành viên có thể nhận credit theo đóng góp đã xác minh. |
| Minh bạch và kiểm toán được | Mọi cấp phát, sử dụng, điều chỉnh, hoàn trả đều có lý do, chủ thể và dấu thời gian. |
| Tách biệt các loại quyền lợi | Phí nền tảng không dùng chung sổ với điểm thưởng liên minh, tiền mặt, tiền công hay vốn góp. |
| **Metering độc lập, tái sử dụng** | Module đo lường là service riêng, giao tiếp qua API contract; gắn được vào hệ thống khác mà không phụ thuộc code Vạn An. |
| **Quản trị giá linh hoạt** | SystemAdmin tự thêm/xóa/sửa hạng mục dịch vụ và mức giá — không cần deploy code khi đổi biểu phí. |

---

## 3. Định nghĩa và ranh giới sản phẩm

**Phí nền tảng** là cơ chế thu phí sử dụng dịch vụ số của Vạn An theo điều khoản công bố, tính qua đơn vị định mức nội bộ gọi là **credit**. Giá trị credit được xác định theo danh mục dịch vụ và định mức đang có hiệu lực; không mặc nhiên là một đồng tiền, khoản tiền gửi, công cụ đầu tư hoặc quyền đòi tiền mặt. *(v1.0–v1.2 gọi là "VA Credits" — đổi tên v1.3.)*

### 3.1. Mô hình ba lớp

Hệ thống tách rõ ba lớp, mỗi lớp một bản chất khác nhau — không trộn lẫn:

```
Lớp 1 — Usage Meter (module độc lập — xem §3.3)
  Đo token AI, GB-tháng, số trang OCR, khối lượng truy vấn, tác vụ
  thành công và chi phí nội bộ. Đây là DỮ LIỆU ĐO LƯỜNG, không phải
  số dư của người dùng.
        │  (usage × định mức công bố — Rate Card do SystemAdmin quản trị)
        ▼
Lớp 2 — Phí nền tảng (credits)
  Quyền lợi dịch vụ người dùng mua hoặc được HTX/Vạn An cấp — có
  nguồn, hạn mức, điều kiện sử dụng rõ ràng. Trừ credit khi dùng.
        │  (chỉ chiều mua/nạp)
        ▼
Lớp 3 — Thanh toán & Thương mại (VND)
  Ví tiền VND hiện hữu + hóa đơn/chứng từ. Phí thuê bao SaaS, phí
  vượt hạn mức, phí theo giá trị giao dịch ghi nhận riêng theo
  hợp đồng — độc lập với credits.
```

**Quy tắc chuyển đổi giữa các lớp:**

- Usage Meter → Credits: usage kỹ thuật quy đổi qua định mức công bố; metering KHÔNG trực tiếp trừ tiền.
- VND → Credits: chỉ một chiều (mua/nạp/cấp phát); credit không quy đổi ngược ra tiền.
- Credits → VND phí: một tác vụ **đã thanh toán bằng credit thì KHÔNG được thu thêm lần nữa từ ví VND**, trừ khi người dùng đã đồng ý một khoản phí khác được công bố riêng (ví dụ phí vượt gói).
- Phí thương mại (thuê bao, vượt mức, theo giá trị giao dịch) thuộc Lớp 3, thu bằng VND, chỉ áp dụng khi có căn cứ đo lường + hợp đồng + cơ chế đối soát. **Không áp dụng máy móc tỷ lệ hoa hồng trên giá trị ước tính** nếu chưa kiểm chứng khả năng đo, quan hệ nhân quả và sự đồng ý của khách hàng.

### 3.2. Phí nền tảng KHÔNG phải

| Không phải | Quy tắc |
|---|---|
| Tiền hoặc tiền điện tử | Không quy đổi tiền mặt, không cam kết mua lại, không quảng bá như phương tiện thanh toán bên ngoài. |
| Điểm thưởng liên minh | Điểm liên minh đang vận hành trên **AllianceWallet** (PostgreSQL, cross-tenant) đổi ưu đãi/giảm giá theo chính sách riêng; Phí nền tảng thanh toán dịch vụ số Vạn An; **không quy đổi 2 chiều trong MVP**. |
| Vốn góp HTX | Credit không tạo quyền biểu quyết, quyền sở hữu, quyền hưởng lợi nhuận hoặc quyền rút vốn. |
| Tiền công/hoa hồng | Credit không thay thế khoản thanh toán phải trả theo hợp đồng, pháp luật lao động hoặc thỏa thuận dịch vụ. |
| Tài sản chuyển nhượng tự do | Không chuyển nhượng giữa người dùng, trừ khi sau này có thiết kế và rà soát pháp lý riêng. |
| Ví tiền VND (Wallet hiện hữu) | `Wallet`/`WalletTransaction` đang vận hành: COD, hoa hồng, nạp trước (Deposit), rút tiền. Phí nền tảng không dùng chung sổ; tiền nạp mua credit vẫn ghi vào luồng tiền và hóa đơn riêng. |
| Quỹ cộng đồng (CommunityFund hiện hữu) | CommunityFund là quỹ system-wide do SysAdmin chi (không gắn tenant). HTX Pool là ngân sách gắn TenantId của HTX — khác phạm vi, không dùng chung sổ. |
| Phí thương mại SaaS khác | Phí thuê bao gói, phí vượt hạn mức, phí giao dịch theo giá trị thuộc Lớp 3 — ghi nhận riêng theo hợp đồng. |

### 3.3. Metering là module độc lập (quyết định v1.3)

Module đo lường (Usage Meter) được thiết kế như **service độc lập**, không phải thành phần nhúng trong codebase Vạn An:

- **Deploy riêng:** chạy trên 1 docker container riêng (`vanan-metering`), có thể tách sang VPS riêng khi tải lớn hoặc khi phục vụ hệ thống khác.
- **Tích hợp qua API contract:** ShopERP/Gateway/hệ thống khác gọi HTTP API (authorize/hold/capture, ingest usage, rate card) — không chia sẻ code, không chia sẻ DB. ShopERP chỉ chứa **API client mỏng** (pattern `GatewayAdminApiClientBase`/`OcrConfigApiClient`).
- **Storage riêng:** database riêng của metering module (SQLite cho MVP, PostgreSQL khi hoàn thiện) — không ghi trực tiếp vào VanAnDbContext/ShopERPDbContext.
- **Auth service-to-service:** API key hoặc JWT riêng; tenant context truyền qua claim (`TenantId`, `ShopInstanceId`) chứ không join trực tiếp bảng Tenant.
- **Tái sử dụng cho hệ thống khác:** contract được thiết kế generic (service code + định mức + usage event), có thể gắn vào bất kỳ hệ thống nào cần tính phí theo usage.
- **Fail isolation:** metering chết không làm sập hệ thống chính — chỉ task trả phí bị ảnh hưởng theo chính sách fail-open/fail-closed (§9.1.3).
- **Quyết định triển khai (chốt 23/10/2026):** stack **.NET 8 minimal API + EF Core + SQLite (GĐ-1/2) → PostgreSQL (GĐ-3)**; code ở folder `8_Metering/` trong monorepo hiện tại (solution riêng, image riêng — ranh giới là API contract, không phải vị trí repo); tách repo riêng khi có hệ thống ngoài thực sự adopt. Ledger credit nằm trong DB metering (§14.18).

---

## 4. Ba cơ chế Phí nền tảng được chọn

### 4.1. Phí nền tảng theo mức dùng – pay-as-you-use

- Người dùng mua credit hoặc được bao gồm một hạn mức trong gói dịch vụ.
- Áp dụng chủ yếu cho AI, OCR, phân tích dữ liệu, báo cáo nâng cao, lưu trữ bổ sung và tự động hóa.
- Các chức năng thiết yếu như đăng nhập, xem đơn hàng và thao tác cơ bản không nên bị khóa chỉ vì hết credit; giới hạn tùy loại sản phẩm.
- Hiển thị ước tính trước tác vụ tốn nhiều credit; yêu cầu xác nhận hoặc thiết lập ngưỡng ngân sách khi vượt mức.
- Có cơ chế hoàn credit cho lỗi hệ thống hoặc tác vụ không hoàn thành theo chính sách; chống trừ lặp khi retry.
- Trước khi xây ledger mới, đánh giá tái sử dụng cơ chế ví nạp trước bằng VND đang vận hành (Deposit wallet → trừ phí theo usage, ví dụ phí SMS OTP) — xem quyết định §14.

### 4.2. HTX Pool – quỹ credit chung

- HTX mua quỹ credit hoặc nhận hạn mức tài trợ theo điều kiện, sau đó phân bổ cho tenant, salesman, shipper và thành viên.
- Mỗi khoản cấp phát ghi rõ nguồn: tiền HTX đã mua, chương trình Vạn An tài trợ, nhà tài trợ, hoặc ngân sách thưởng.
- HTX thiết lập hạn mức theo thành viên, mục đích, kỳ ngân sách và loại dịch vụ; có thể thu hồi phần chưa sử dụng nếu điều khoản cho phép.
- Cần phê duyệt theo phân quyền HTX; không cho một cá nhân tự ý tạo, cấp và tự duyệt ngân sách không giới hạn.
- Quỹ HTX không được mặc nhiên xem là tiền gửi hoặc khoản tiền của thành viên; quy chế tài chính và hợp đồng phải mô tả đúng bản chất.
- HTX Pool gắn TenantId của tenant HTX và cấp phát xuống tenant/thành viên theo ShopInstanceId (multi-VPS); không nhầm với CommunityFund system-wide.
- **Giới hạn MVP:** chỉ triển khai ở cấp ngân sách và hạn mức sử dụng (budget cap), chưa làm cơ chế phân phối/chuyển quyền sở hữu phức tạp — các câu hỏi quyền sở hữu liệt kê ở §14.

### 4.3. Earned Credits – thưởng theo đóng góp

- Chỉ cấp khi kết quả đóng góp đã được xác minh: giao dịch hợp lệ, onboarding hoàn thành, dữ liệu đạt chuẩn hoặc nhiệm vụ cộng đồng được duyệt.
- Không thưởng chỉ vì đăng ký tài khoản, tạo đơn giả, tự giới thiệu vòng tròn hoặc giao dịch bị hủy/gian lận.
- Mỗi chương trình có ngân sách, điều kiện, giới hạn mỗi người, thời hạn và quy trình khiếu nại.
- Credit chỉ đổi lấy dịch vụ số được liệt kê; không quy đổi tiền mặt và không thay thế lương, tiền giao hàng, hoa hồng hoặc nghĩa vụ thanh toán.
- Quy định rõ thời hạn sử dụng, trường hợp thu hồi do gian lận và cách xử lý khi tài khoản bị đóng.

---

## 5. Phân biệt các loại số dư

| Loại số dư | Nguồn | Sử dụng | Quy tắc chính |
|---|---|---|---|
| Purchased Credits | Người dùng/HTX mua theo hợp đồng | Thanh toán dịch vụ được hỗ trợ | Có lịch sử mua, hóa đơn/chứng từ và chính sách hoàn tiền phù hợp. |
| HTX Pool Credits | Quỹ do HTX mua hoặc được tài trợ | Cấp hạn mức cho đối tượng được HTX phê duyệt | Có ngân sách, người phê duyệt, mục đích và kỳ hạn. |
| Earned Credits | Chương trình thưởng đã được xác minh | Đổi dịch vụ đủ điều kiện | Có điều kiện thưởng, hạn mức, thời hạn và cơ chế chống gian lận. |
| Điểm thưởng liên minh | Theo chương trình loyalty riêng (AllianceWallet — PG, cross-tenant) | Đổi ưu đãi/giảm giá theo thể lệ | Không gộp vào Phí nền tảng; không quy đổi 2 chiều trong MVP. |
| Tiền thanh toán/tiền công | Theo giao dịch và hợp đồng | Thanh toán hàng hóa, giao hàng, hoa hồng hoặc nghĩa vụ khác | Theo luồng tiền và sổ kế toán riêng. |
| Ví tiền VND (WalletTransaction — hiện hữu) | Nạp trước/COD/hoa hồng/quỹ — đang vận hành | Chi tiêu, phí dịch vụ, rút tiền theo hợp đồng | Ledger bất biến, đơn vị VND; KHÔNG trộn với credits. |

---

## 6. Đối tượng và chính sách đề xuất

| Đối tượng | Cách tiếp cận | Ai có thể chi trả |
|---|---|---|
| Tenant | Hạn mức cơ bản; mua thêm khi dùng AI, OCR, báo cáo hoặc lưu trữ chuyên sâu. | Tenant; HTX tài trợ; chương trình Vạn An. |
| Salesman | Công cụ cơ bản có hạn mức nhỏ; credit nâng cao gắn với nhu cầu công việc. Vai trò hiện hữu: `CommunityRole(Salesman)` gắn trên Customer phía KhachLink. | Salesman, đơn vị sử dụng lao động/dịch vụ hoặc HTX theo thỏa thuận. Ưu tiên HTX tài trợ hạn mức thay vì tạo rào cản thanh toán cho cộng tác viên. |
| Shipper | Nhận việc và thao tác thiết yếu không bị khóa vì hết credit; tính riêng dịch vụ số nâng cao. Vai trò hiện hữu: `CommunityRole(Shipper)` gắn trên Customer phía KhachLink. | Shipper, HTX hoặc đơn vị vận hành theo hợp đồng. |
| Buyer | Tìm kiếm, xem thông tin và mua hàng cơ bản không bị tính credit mặc định; có thể được HTX tài trợ dịch vụ số đặc biệt. **(Loại khỏi MVP — buyer là customer cross-tenant đã có điểm liên minh; hai loại số dư gây nhiễu UX.)** | HTX, tenant hoặc chương trình tiếp thị; buyer chỉ trả nếu dịch vụ trả phí được nêu rõ. |
| HTX | Ví/quỹ tổng, bảng phân bổ, báo cáo tiêu thụ, giới hạn ngân sách và phê duyệt nhiều cấp. | HTX theo hợp đồng dịch vụ và quy chế được duyệt. |
| Thành viên HTX góp vốn | Được hưởng quyền lợi theo Điều lệ, nghị quyết và chính sách dịch vụ; không tự động có quyền rút credit thành tiền. | Bản thân hoặc quỹ HTX theo chính sách. |
| Thành viên HTX không góp vốn | Có thể mua dịch vụ và nhận credit thưởng/tài trợ theo điều kiện; phân biệt quyền thành viên với quyền của thành viên góp vốn. | Bản thân, HTX hoặc chương trình tài trợ. |

Lưu ý: mức phí và ưu đãi không nên tự động phân biệt chỉ dựa trên việc có góp vốn hay không. Mọi quyền lợi liên quan tư cách thành viên, vốn góp, sử dụng dịch vụ và phân phối thu nhập cần phù hợp Điều lệ HTX, nghị quyết hợp lệ và hợp đồng.

**Mapping hệ thống hiện hữu:** Salesman/Shipper = `CommunityRole(RoleType)` gắn trên Customer (phía KhachLink/PG); Buyer = customer KhachLink cross-tenant; HTX = tenant loại HTX (TT71) gồm hồ sơ HTX và thành viên. Ví credits ở cấp tenant; hạn mức thành viên thực hiện qua budget cap, không phải ví riêng trong MVP.

---

## 7. Mô hình định mức và giá

Không lấy một đơn vị kỹ thuật duy nhất (MB, giây CPU hoặc số lượt gọi) làm giá bán cho tất cả. Nên đo các driver chi phí ở tầng kỹ thuật, sau đó chuyển thành định mức credit dễ hiểu ở tầng sản phẩm.

**Phân biệt 4 khái niệm (không trộn lẫn):**

| Khái niệm | Định nghĩa |
|---|---|
| Đơn vị đo kỹ thuật | Số liệu thu thập từ hệ thống (token, GB, trang, bản ghi). |
| Định mức credit | Số credit tiêu thụ theo một dịch vụ, do Vạn An công bố trên Rate Card. |
| Giá bán | Số tiền khách hàng trả để mua dịch vụ hoặc mua credit. |
| Chi phí nội bộ | Chi phí Vạn An chịu để cung cấp dịch vụ. |

| Nhóm dịch vụ | Đại lượng đo nội bộ | Đề xuất cách hiển thị |
|---|---|---|
| AI/LLM | Token đầu vào/đầu ra, mô hình sử dụng, cache và tác vụ | Credit theo tác vụ hoặc cấp độ xử lý; hiển thị ước tính. |
| OCR hóa đơn/tài liệu | Số trang, chất lượng ảnh, số lần xử lý và chi phí nhà cung cấp | Credit/trang hoặc theo nhóm tài liệu. |
| Báo cáo/phân tích | Khối lượng bản ghi, truy vấn, CPU/thời gian xử lý | Credit theo mức nhỏ/vừa/lớn; tránh công thức khó hiểu. |
| Lưu trữ | GB-tháng, bản sao lưu, thời gian lưu giữ | Hạn mức bao gồm; tính thêm theo GB-tháng. |
| Truyền dữ liệu | GB truyền ra/vào và chi phí hạ tầng liên quan | Chỉ tính riêng khi vượt ngưỡng đáng kể; nêu rõ chiều dữ liệu. |
| Tác vụ nền/đồng bộ | Bản ghi hoặc khối lượng thành công; retry và lỗi | Credit theo lượng xử lý thành công; không tính trùng retry do lỗi hệ thống. |
| **Traffic storefront tenant (KhachLink)** | Lượt truy cập trang store, phiên, nguồn truy cập | Credit theo lượt truy cập hợp lệ (loại bot) hoặc bậc thang theo kỳ; trọng tâm tính phí theo giá trị cho tenant. |
| **Tin nhắn/Chat (MessagingHub)** | Số tin nhắn gửi/nhận + dung lượng payload (KB) | Credit theo tin nhắn + theo khối lượng dữ liệu. |
| **Tìm kiếm & referral từ Directory (timlathay.com)** | Lượt hiển thị trong kết quả tìm kiếm; lượt click redirect tới storefront | Credit theo lượt redirect xác thực (lead đo được, khó gian lận hơn impression). |
| **Sử dụng tính năng theo module** | Booking, tài chính (/financial), quản lý kho (/inventory) — số action hoặc module-active theo kỳ | Credit theo action (booking tạo, báo cáo chạy, nghiệp vụ kho) hoặc theo gói tính năng/tháng. |

**Công thức kiểm soát nội bộ:**

- Chi phí tài nguyên kỳ = tổng (lượng tài nguyên đo được × đơn giá chi phí nội bộ).
- Giá bán dịch vụ = chi phí trực tiếp + phân bổ vận hành/hỗ trợ + dự phòng rủi ro + biên lợi nhuận mục tiêu.
- Số credit bị trừ = định mức công bố cho tác vụ × hệ số dịch vụ đang có hiệu lực.

Tỷ lệ quy đổi giữa credit và đồng Việt Nam chỉ được xác lập sau khi đo chi phí thật và rà soát pháp lý, kế toán, thuế. Không công bố rằng một credit tương đương một đồng nếu chưa có căn cứ và chưa xác định rõ nghĩa vụ liên quan. Mức quy đổi nội bộ dùng để tính chi phí (nếu có) không tự động trở thành cam kết quy đổi với khách hàng.

---

## 8. Nguyên tắc chống rào cản và chống cảm giác bị lừa

- Công khai định mức và phạm vi dịch vụ trước khi người dùng xác nhận.
- Không quảng cáo "miễn phí trọn đời" nếu thực tế có giới hạn; dùng cụm "hạn mức khởi đầu" và nêu rõ điều kiện.
- Cho xem số dư, lịch sử phát sinh, nguồn credit, ngày hết hạn và lý do điều chỉnh.
- Có cảnh báo ở các ngưỡng 50%, 80% và 100% hạn mức; cho phép đặt trần chi tiêu.
- Không tự động phát sinh khoản vượt hạn mức nếu chưa có sự đồng ý phù hợp.
- Có chính sách hoàn credit khi lỗi hệ thống, tác vụ thất bại hoặc bị tính trùng.
- Không khóa dữ liệu của người dùng chỉ vì hết credit; bảo đảm quyền xuất dữ liệu và quy trình chấm dứt dịch vụ theo hợp đồng.
- Tách rõ credit hết hạn, credit bị thu hồi do gian lận và credit không còn dùng được do dịch vụ ngừng cung cấp; thông báo trước theo điều khoản.
- **Chống thu kép:** một tác vụ đã thanh toán bằng credit không được thu thêm từ ví VND, trừ khi người dùng đã đồng ý một khoản phí khác được công bố riêng.
- **Chống chậm hệ thống (v1.3):** chi phí authorize phải nhỏ hơn nhiều chi phí tác vụ — task <500ms dùng quota/counter theo kỳ, task ≥1s mới dùng hold/capture đồng bộ.

---

## 9. Kiến trúc nghiệp vụ và yêu cầu hệ thống

| Thành phần | Yêu cầu tối thiểu | Trạng thái hiện hữu* |
|---|---|---|
| **Metering Service (độc lập)** | Service riêng `vanan-metering` — docker container riêng, có thể VPS riêng; HTTP API contract generic; DB riêng; auth service-to-service; có thể gắn vào hệ thống khác. | **BUILD** — module mới, thiết kế tách biệt (§3.3). |
| Catalog/Rate Card | Mã dịch vụ, định mức credit, phiên bản giá, thời gian hiệu lực, nhóm khách hàng và điều kiện áp dụng. **SystemAdmin thêm/xóa/sửa hạng mục và mức giá qua UI/API — không cần deploy code; mọi thay đổi có version + ngày hiệu lực + audit.** | **BUILD** — chưa có rate card; precedent cấu hình qua `SystemSetting` (SmsOtpFeePerVerification). |
| Usage Metering | Ghi nhận lượng sử dụng, tác vụ, tenant/HTX, người yêu cầu, trạng thái thành công, thời gian và chi phí ước tính. | **BUILD** — chưa có; precedent charge-per-use: `CollaboratorVerificationService` (check số dư → task → trừ phí flat qua WalletTransaction). |
| Wallet/Ledger | Sổ cái bất biến theo giao dịch; không sửa số dư trực tiếp; điều chỉnh bằng bút toán đảo/điều chỉnh có lý do. Source of truth trên PG (Gateway) hoặc DB riêng của metering module (xem quyết định §14); mirror chỉ-đọc xuống ShopERP qua NATS (pattern AllianceWallet); mọi bản ghi gắn TenantId và ShopInstanceId. | **EXTEND** — pattern ledger bất biến đã có (WalletTransaction, AllianceWallet/AllianceTransaction). |
| Credit Source | Gắn nhãn Purchased, HTX Pool, Earned; theo dõi nguồn tài trợ, chủ sở hữu quyền lợi, hạn dùng và điều kiện. | **BUILD** — gắn nhãn nguồn cho credit là mới. |
| Authorization/Budget | Kiểm tra quyền dùng credit, giới hạn theo kỳ, mục đích và ngân sách; hỗ trợ phê duyệt. Authorize đồng bộ qua Metering API kèm Idempotency-Key trước khi chạy tác vụ. | **EXTEND** — idempotency đã có (Idempotency-Key booking, idempotencyKey alliance); budget cap per-member là mới. |
| Idempotency/Retry | Mỗi tác vụ có khóa chống trùng; retry do lỗi hệ thống không tự động trừ credit lần nữa. | **REUSE** — pattern Idempotency-Key đã chạy production. |
| Credit Hold/Settlement | Ước tính → giữ chỗ (hold) → chốt theo chi phí thực tế (capture), giải phóng phần dư. Bắt buộc cho tác vụ chi phí biến thiên (AI/OCR) để bảo đảm nguyên tắc không vượt hạn mức. | **BUILD** — chưa có cơ chế hold. |
| Metering Path (client mỏng) | ShopERP chỉ chứa API client mỏng (pattern `GatewayAdminApiClientBase`): authorize đồng bộ trước khi chạy, capture/usage event sau khi xong. | **BUILD** — client mới; tái sử dụng pattern API client hiện hữu. |
| Invoice/Accounting | Ghi nhận vào JournalEntry/AccountingEntry trên sổ pháp nhân nền tảng (PG). Phương án hạch toán và thời điểm xuất HĐĐT: xem ma trận §11 — **OPEN, chờ xác minh**. | **EXTEND** — JournalEntry/AccountingEntry + module e-invoice đã có; cần loại bút toán/chứng từ mới cho phí nền tảng. |
| Audit/Dispute | Nhật ký cấp phát, tiêu thụ, hoàn trả, hết hạn, điều chỉnh; hỗ trợ tra soát và khiếu nại. | **EXTEND** — AuditLog có sẵn; quy trình khiếu nại là mới. |
| Alerts/Reports | Cảnh báo số dư, mức dùng bất thường, ngân sách HTX, chi phí theo dịch vụ và mức thu hồi chi phí. | **EXTEND** — AlertNotifier (Telegram/Zalo stub) có sẵn. |
| Fraud/Risk Engine | Tái sử dụng fraud engine hiện hữu (FraudFlagType, RiskScore, cơ chế Held/Rejected của commission); mở rộng rule cho phí nền tảng — không xây hệ mới. | **EXTEND** — engine đã chạy; rule mới. |

\* *Trạng thái là đánh giá sơ bộ tại thời điểm viết policy — phải được kiểm chứng lại trong pha ANALYZE khi triển khai (xem §14).*

### 9.1. Điều kiện nhất quán (bắt buộc)

1. **Không ra quyết định chi tiêu credit trên bản mirror lỗi thời.** Mirror chỉ đọc để hiển thị; mọi authorize/hold/capture đi qua Metering Service.
2. **Idempotency ở cả hai bước hold và capture** — mỗi giao dịch có mã định danh duy nhất (Idempotency-Key end-to-end).
3. **Metering Service không liên lạc được:** tác vụ tốn credit phải từ chối hoặc xếp chờ; chỉ các chức năng thiết yếu **được phê duyệt trước trong danh sách** mới fail-open. Metering chết KHÔNG được làm sập POS/kiosk/chức năng cốt lõi.
4. **Xử lý giao dịch lơ lửng:** hold có timeout tự giải phóng; xử lý được trường hợp tác vụ thành công nhưng phản hồi mất (capture idempotent) và tác vụ thất bại sau khi đã hold (release/refund).
5. **Ngưỡng overhead (v1.3):** authorize đồng bộ chỉ cho task nặng (≥1s); task nhẹ/tần suất cao dùng quota theo kỳ hoặc counter gộp — tránh metering làm chậm đường nóng.

> Chi tiết state machine (hold → capture / release / refund / retry) và tiêu chí kiểm thử thuộc SRS kỹ thuật — policy này chỉ ghi yêu cầu.

---

## 10. Kiểm soát, gian lận và phân quyền HTX

- Phân tách người tạo yêu cầu, người duyệt ngân sách và người đối soát.
- Không cho tự thưởng credit cho chính mình; mọi thưởng cần rule hoặc phê duyệt và có log.
- Phát hiện đăng ký trùng, giao dịch giả, hoàn/hủy bất thường, thao túng giới thiệu và tiêu thụ đột biến — tái sử dụng fraud engine hiện hữu (`FraudFlagType`: SelfDeal, WashTrading, AccountFarming…; `RiskScore` + trạng thái Held/Rejected của commission).
- Đặt trần theo người dùng, tenant, HTX, dịch vụ và kỳ thời gian.
- Đối soát định kỳ giữa usage log, ledger, hợp đồng, hóa đơn và sổ kế toán; đối soát ledger với usage log theo TenantId/ShopInstanceId.
- Có quy trình đóng băng giao dịch nghi gian lận, thông báo, giải trình và khiếu nại.
- **Rate Card governance (v1.3):** thay đổi hạng mục/định mức ghi audit đầy đủ (ai sửa, giá trị cũ→mới, ngày hiệu lực); định mức có hiệu lực trong tương lai cho phép chuẩn bị đợt giá mới mà không ảnh hưởng giao dịch đang chạy.

---

## 11. Kế toán, thuế và rà soát pháp lý

Trước khi phát hành chính thức, cần có ý kiến của kế toán và tư vấn pháp lý Việt Nam về bản chất của việc bán credit trước, cấp credit tài trợ, thưởng credit, hết hạn/thu hồi, hoàn tiền và ghi nhận doanh thu. Không nên mặc nhiên ghi nhận toàn bộ tiền thu khi bán credit là doanh thu dịch vụ đã hoàn thành; thời điểm và cách ghi nhận phải theo bản chất hợp đồng và chuẩn mực/quy định áp dụng.

- Rà soát điều khoản sử dụng, hợp đồng SaaS, điều kiện hết hạn và hoàn trả.
- Rà soát quy định liên quan HTX, thẩm quyền sử dụng quỹ, nghị quyết và công khai lợi ích thành viên.
- Rà soát hóa đơn điện tử, thuế và chứng từ cho tiền mua dịch vụ, khoản tài trợ và dịch vụ hoàn thành.
- Rà soát rủi ro nếu credit có khả năng chuyển nhượng, quy đổi, mua lại hoặc được chấp nhận như phương tiện thanh toán; phiên bản này không đề xuất các tính năng đó.
- Rà soát bảo vệ dữ liệu cá nhân, quyền truy cập log sử dụng và thời hạn lưu giữ dữ liệu.

### 11.1. Ma trận hạch toán theo trường hợp (trạng thái OPEN, chờ kế toán/pháp lý xác minh)

Ghi sổ pháp nhân nền tảng Vạn An (system scope, PG), **tách khỏi sổ HKD của tenant**. Các phương án dưới đây là *đề xuất baseline để review*, chưa phải quyết định:

| Trường hợp | Chứng từ | Hạch toán đề xuất | Trạng thái |
|---|---|---|---|
| Bán credit, đã thu tiền, chưa sử dụng | HĐĐT (đề xuất: xuất khi thu tiền qua module e-invoice hiện hữu) | Nợ 112 / Có 3387 (doanh thu chưa thực hiện); thuế theo hóa đơn | **OPEN** — chờ xác nhận thời điểm lập HĐ và tài khoản |
| Cấp credit tài trợ/thưởng (không thu tiền) | Chứng từ nội bộ + quyết định duyệt | Không ghi doanh thu; đề xuất ghi chi phí khi tiêu thụ | **OPEN** |
| Dịch vụ tiêu thụ bằng credit | Usage record + ledger entry | Kết chuyển 3387 → doanh thu (511) theo định mức quy đổi | **OPEN** |
| Credit hết hạn / bị thu hồi / hoàn | Chứng từ điều chỉnh có lý do | Điều chỉnh 3387 (doanh thu khác hoặc giảm nợ) theo bản chất | **OPEN** |
| Hủy giao dịch / hoàn tiền credit đã mua | Hóa đơn điều chỉnh/hủy + chứng từ chi | Hoàn 3387 + chi tiền theo hợp đồng | **OPEN** |

*Nguyên tắc: không suy ra quy định pháp lý từ tên tài khoản — mỗi dòng cần chữ ký xác nhận của người phụ trách kế toán trước khi code.*

---

## 12. Kế hoạch triển khai

### 12.1. Roadmap metering module — 3 giai đoạn (v1.3, user chốt)

| Giai đoạn | Phạm vi | Điều kiện qua cổng |
|---|---|---|
| **GĐ-1 — MVP đơn giản nhất** | `vanan-metering` container riêng · SQLite · ingest usage event (batch + single) · counter per tenant/service/kỳ · Rate Card do SystemAdmin CRUD · **ví credit quota**: admin cấp hạn mức, trừ credit theo usage · **cảnh báo khi ví còn 50% và 10%** (đã dùng 50%/90% — ngưỡng cấu hình) · **deny khi hết số dư** + thông báo + hướng dẫn top-up · **top-up thủ công qua VietQR**: SystemAdmin cấu hình số tài khoản / tên thụ hưởng / ngân hàng nhận tiền → user quét QR chuyển khoản → admin xác nhận → cộng credit · API client mỏng ở ShopERP | Usage không trùng (idempotency-key); rate card sửa không cần deploy; deny/check p95 đạt ngưỡng; top-up e2e (QR → xác nhận → credit vào ví). **Lưu ý: top-up thủ công vẫn là nhận tiền thật → cần chứng từ/đối soát tối thiểu (xem cảnh báo §11.1).** |
| **GĐ-2 — Trung bình (pilot)** | Hold/Capture/Release · ledger đa nguồn (purchased/pool/earned) · hạn mức kỳ + cảnh báo quota · hoàn credit · hoàn tất lịch sử giao dịch · pilot 1 HTX + nhóm tenant · mirror read-only về ShopERP | Người dùng hiểu hóa đơn; quy trình hoàn/khiếu nại hoạt động; không lỗi ledger. |
| **GĐ-3 — Hoàn thiện** | HTX Pool + Earned credits + phê duyệt đa cấp · đối soát định kỳ với usage/ledger/hóa đơn · tích hợp kế toán (bút toán theo ma trận §11.1 đã duyệt) · fraud rules · credit expiry/thu hồi · backup/DR · scale VPS riêng nếu cần · **thu tiền thật** | Xác minh chi phí, kế toán (ký ma trận §11.1), pháp lý, đối soát, unit economics; kiểm toán nội bộ + giám sát gian lận + quy trình vận hành. |

### 12.2. Kế hoạch thí điểm tổng (theo policy)

| Giai đoạn | Công việc | Điều kiện qua cổng |
|---|---|---|
| 0. Thiết kế | Chọn 5–10 tác vụ; đo chi phí trực tiếp; thiết kế rate card; chốt điều khoản và quyền hạn. | Có chi phí cơ sở, định mức có thể giải thích, rà soát pháp lý sơ bộ; đã chốt phương án đơn vị credit (§14); **đã có ma trận xác minh base code REUSE/MODIFY/EXTEND/BUILD/OPEN**; đã phân loại chức năng cốt lõi/trả phí/tạm ngừng. |
| 1. Ví quota + enforce nhẹ | GĐ-1 metering chạy thật: admin cấp hạn mức credit, trừ theo usage, **cảnh báo còn 50%/10%**, **deny khi hết số dư** + hướng dẫn top-up VietQR thủ công (admin cấu hình tài khoản nhận + xác nhận nhận tiền). Shadow report vẫn chạy song song để so sánh chi phí thực tế. | Không trùng usage; không trừ quá số dư; top-up xác nhận đúng người; độ chính xác đo lường đạt ngưỡng. |
| 2. Pilot có kiểm soát | GĐ-2: một HTX (hoặc tenant test đã seed sẵn), một nhóm tenant và một nhóm CTV; hiển thị hạn mức, cảnh báo, tra soát. | Người dùng hiểu hóa đơn; có quy trình hoàn credit và xử lý khiếu nại. |
| 3. Đánh giá | Đo tỷ lệ sử dụng, chuyển đổi trả phí, chi phí phục vụ, mức hài lòng, tranh chấp. | Đạt ngưỡng unit economics và không có lỗi nghiêm trọng về ledger/quyền lợi. |
| 4. Thu tiền thật & mở rộng | GĐ-3: mở thêm dịch vụ và HTX; tự động hóa đối soát và báo cáo; tích hợp luồng ví VND và đơn hàng hiện hữu nếu kiểm kê xác nhận phù hợp. | **Chỉ sau khi xác minh chi phí, kế toán, pháp lý, khả năng đối soát và mức sẵn sàng trả tiền.** |

---

## 13. KPI đánh giá thành công

| Nhóm | KPI |
|---|---|
| Kinh tế | Chi phí hạ tầng/tenant hoạt động; chi phí mỗi tác vụ; doanh thu dịch vụ; tỷ lệ thu hồi chi phí; biên đóng góp. |
| Sử dụng | Tỷ lệ kích hoạt; tỷ lệ dùng hết hạn mức; tác vụ thành công; mức sử dụng theo vai trò; tỷ lệ mua thêm. |
| Trải nghiệm | Tỷ lệ người dùng hiểu cách tính; số khiếu nại tính phí; tỷ lệ hoàn credit; mức bỏ dùng sau khi hết hạn mức. |
| HTX | Tỷ lệ quỹ được dùng đúng mục đích; số thành viên hưởng lợi; chi phí trên thành viên hoạt động; chênh lệch ngân sách. |
| Kiểm soát | Tỷ lệ giao dịch trùng; chênh lệch ledger/usage; gian lận; lỗi cấp phát; thời gian xử lý khiếu nại. |
| **Metering (v1.3)** | Overhead authorize p95 < ngưỡng đặt (đề xuất ≤50ms); usage event loss rate ≈ 0; thời gian đổi rate card không cần deploy = 0. |

---

## 14. Các quyết định cần chốt trước khi phát triển

**Nhóm mô hình & đơn vị:**

1. ✅ ~~Danh mục dịch vụ trong MVP~~ — **ĐÃ CHỐT (23/10/2026):** trọng tâm = **traffic storefront tenant** trên KhachLink (lượt truy cập · số tin nhắn + dung lượng chat · lượt tìm kiếm & redirect từ timlathay.com) và **sử dụng tính năng theo module**: booking, tài chính (`/financial`), quản lý kho (`/inventory`). Nhóm định mức task-based (OCR/AI/export/báo cáo/lưu trữ) giữ làm nguồn mở rộng GĐ-2+.
2. ✅ ~~Đơn vị credit mới hay ví VND~~ — **ĐÃ CHỐT (23/10/2026): Option A — CreditWallet riêng trong metering module.** Lý do: HTX Pool và Earned là quyền lợi không có tiền backing; ví VND (có rút tiền) mà ghi earned credit sẽ rất gần "phát hành giá trị lưu trữ" → rủi ro pháp lý cao hơn. Deposit wallet VND giữ vai trò precedent flow, không tái sử dụng làm số dư credit.
3. Credit được mua theo gói nào và có/không có mức tối thiểu.
4. Có cho phép quy đổi điểm liên minh (AllianceWallet) ↔ credit không? **Khuyến nghị: KHÔNG trong MVP.**
5. Thu tiền nạp credit qua pipeline đơn hàng hiện hữu (`Order.SourceType="credit_topup"` → OrderFlow + PaymentCompliance + QRChannel + e-invoice) hay luồng thu riêng?

**Nhóm chính sách sử dụng:**

6. Credit mua, credit HTX cấp và credit thưởng có thời hạn khác nhau hay không.
7. Thứ tự trừ credit khi một ví có nhiều nguồn và quy tắc xử lý credit hết hạn.
8. Có cho phép hoàn tiền phần credit đã mua chưa sử dụng không; quy tắc áp dụng theo loại hợp đồng.
9. Ngưỡng cảnh báo, trần chi tiêu và quy tắc tác vụ nền.
10. Danh sách phân loại chức năng: **cốt lõi (không bao giờ khóa) / trả phí / có thể tạm ngừng khi hết hạn mức** — kèm danh sách fail-open được phê duyệt trước (§9.1.3).

**Nhóm HTX Pool (chốt trước khi vượt phạm vi MVP):**

11. Ai sở hữu quyền quyết định sử dụng ngân sách HTX (vai trò, ngưỡng phê duyệt)?
12. Ai chịu chi phí khi HTX cấp credit cho một thành viên (HTX / Vạn An / nhà tài trợ — nguồn nào chịu chi phí cuối cùng)?
13. Credit đã cấp nhưng chưa dùng có thể thu hồi trong trường hợp nào?
14. Credit đã cấp có được chuyển sang kỳ tiếp theo khi HTX hết ngân sách không?
15. Thành viên rời HTX hoặc HTX ngừng hoạt động thì quyền lợi còn lại xử lý thế nào?
16. Ai được quyền cấp, duyệt, thu hồi và điều chỉnh credit trong HTX.

**Nhóm kiến trúc (v1.3):**

17. ✅ ~~Metering nhúng hay tách module~~ — **ĐÃ CHỐT: module độc lập `vanan-metering`, container/VPS riêng, tích hợp qua API contract** (§3.3).
18. ✅ ~~Ledger credit đặt ở đâu~~ — **ĐÃ CHỐT (23/10/2026): DB riêng của metering module** (`CreditLedgerEntry` trong SQLite/PG của `vanan-metering`). PG Gateway chỉ nhận kết quả đối soát/bút toán kế toán — bảo toàn tính độc lập để gắn sang hệ thống khác. Backup metering DB = critical (số dư nằm ở đó).
19. ✅ ~~Ai quản trị Rate Card~~ — **ĐÃ CHỐT: SystemAdmin CRUD qua UI/API, có version + hiệu lực + audit.**

**Nhóm xác minh & pháp lý:**

20. Xác minh base code và lập **ma trận REUSE / MODIFY / EXTEND / BUILD / OPEN** cho từng thành phần §9 trước khi implement (deliverable của pha ANALYZE).
21. Mô hình kế toán, thuế và điều khoản pháp lý trước khi mở bán — ký xác nhận từng dòng ma trận §11.1.
22. Định nghĩa state machine cho hold, capture, release, refund, retry (thuộc SRS kỹ thuật — đã có trong `van_an_phi_nen_tang_metering_srs_v1.md`).

---

## 15. Khuyến nghị kết luận

Thông qua Phí nền tảng Policy v1.3 như một đề xuất thiết kế để thí điểm có kiểm soát, chưa phải biểu phí thương mại chính thức. Ưu tiên đo chi phí thật, dùng hạn mức nhỏ có giá trị, cho HTX quản lý quỹ minh bạch và thưởng theo đóng góp đã xác minh. Không phát hành cơ chế quy đổi tiền mặt, chuyển nhượng tự do hoặc coi credit là tài sản tài chính.

Ba điều không thỏa hiệp:

- Không xây ledger mới nếu có thể tái sử dụng an toàn cơ chế hiện hữu; nhưng cũng không ép Phí nền tảng vào ví VND nếu hai loại quyền lợi có bản chất khác nhau.
- Không phát hành credit trả trước và thu tiền thật trước khi chốt được quy tắc sử dụng, hoàn trả, hạch toán và trách nhiệm pháp lý.
- Không để agent/dev tự quyết định các giả định kinh doanh chưa được phê duyệt chỉ vì chúng đã xuất hiện trong proposal.

Bước tiếp theo: xây dựng Rate Card MVP cho 5–10 tác vụ (SystemAdmin CRUD), chạy GĐ-1 shadow billing trên `vanan-metering`, hoàn thiện quy tắc ledger và xin rà soát kế toán/pháp lý trước khi thu tiền thật. Đồng thời đối chiếu từng thành phần §9 với hệ thống hiện hữu (Wallet VND, AllianceWallet, fraud engine, order pipeline) để chốt phạm vi tái sử dụng trước khi xây mới.

---

## Phụ lục A — Lý do chỉnh sửa v1.1 (rà soát kiến trúc, 22/10/2026)

- §3/§5 — Bổ sung ranh giới với Wallet VND, AllianceWallet, CommunityFund: hệ thống đã vận hành 2 ledger và 1 quỹ; thiếu ranh giới sẽ tạo sổ cái thứ 3 trùng lặp.
- §4.1/§14 — Thêm phương án ví nạp trước VND (Deposit wallet): cơ chế nạp-trước-trừ-phí đã chạy (phí SMS OTP); đơn vị VND giảm rủi ro pháp lý/thuế so với phát hành đơn vị credit mới.
- §6 — Map đối tượng về entity thật (CommunityRole/Customer/tenant HTX) và loại buyer khỏi MVP: buyer đã có điểm liên minh, thêm credits gây nhiễu UX.
- §9 — Ledger PG-only trên Gateway + mirror SQLite qua NATS: quỹ HTX span nhiều tenant/ShopInstance không thể nằm trong 1 file SQLite per-VPS; theo precedent AllianceWallet.
- §9 — Thêm Credit Hold/Settlement + authorize đồng bộ kèm Idempotency-Key: tác vụ chi phí biến thiên (AI token) cần giữ chỗ để bảo đảm "không vượt hạn mức".
- §10 — Tái sử dụng fraud engine (FraudFlagType/RiskScore) thay vì xây hệ mới.
- §11 — Cụ thể hóa kế toán: TK 3387 trên sổ pháp nhân nền tảng, HĐĐT khi thu tiền qua e-invoice hiện hữu; tách khỏi sổ HKD của tenant.
- §14 — Thêm quyết định quy đổi điểm↔credits (khuyến nghị: không) và nạp tiền qua Order.SourceType="credit_topup" (tái sử dụng order/payment/invoice pipeline).

## Phụ lục B — Lý do chỉnh sửa v1.2 (phản biện business/architecture, 22/10/2026)

- §3.1 — Thêm mô hình ba lớp (Usage Meter / VA Service Credits / Thanh toán VND): làm tường minh quan hệ giữa metering, quyền lợi và tiền thật; hợp nhất với cơ chế định giá SaaS đề xuất trước đó. Lớp 3 giữ phí thương mại độc lập, không biến credit thành tiền thanh toán mới.
- §3.1/§8 — Thêm quy tắc chống thu kép: tác vụ đã trừ credit không được thu thêm từ ví VND (trừ phí riêng đã công bố).
- §7 — Thêm bảng phân biệt 4 khái niệm (đơn vị đo / định mức / giá bán / chi phí nội bộ) và câu chốt: mức quy đổi nội bộ không tự động thành cam kết quy đổi với khách hàng.
- §9 — Thêm cột "Trạng thái hiện hữu" (REUSE/MODIFY/EXTEND/BUILD) làm đánh giá sơ bộ; xác minh đầy đủ là điều kiện qua cổng Phase 0.
- §9.1 — Thêm 4 điều kiện nhất quán ledger (mirror chỉ-đọc; idempotency hold+capture; fail-open chỉ cho danh sách duyệt trước; xử lý giao dịch lơ lửng).
- §11.1 — Hạ văn khẩu khẳng định xuống "phương án đề xuất — OPEN chờ xác minh" và thêm ma trận 5 trường hợp hạch toán.
- §14 — Thêm 6 câu hỏi quyền sở hữu HTX Pool, câu hỏi phân loại chức năng, yêu cầu ma trận xác minh base code và state machine.
- §12 — Đổi tên giai đoạn 4 thành "Thu tiền thật & mở rộng" + gate rõ.

## Phụ lục C — Lý do chỉnh sửa v1.3 (quyết định user, 23/10/2026)

- Toàn văn — Đổi tên "VA Credits" → **"Phí nền tảng"**: tên gọi phản ánh bản chất thu phí nền tảng; "credit" giữ làm đơn vị định mức nội bộ (không phải tiền).
- §9 Rate Card — Nâng yêu cầu: **SystemAdmin CRUD hạng mục + mức giá qua UI/API** (version + hiệu lực + audit) — đổi biểu phí không cần deploy code.
- §3.3/§9 — **Metering thành module độc lập `vanan-metering`**: giảm coupling với codebase hiện tại, có thể gắn sang hệ thống khác để tính phí, deploy container/VPS riêng, fail isolation.
- §8/§9.1 — Thêm nguyên tắc overhead: authorize đồng bộ chỉ cho task nặng (≥1s), task nhẹ dùng quota/counter — tránh metering làm chậm đường nóng.
- §12.1 — Roadmap metering 3 giai đoạn: MVP đơn giản nhất (usage + rate card + shadow) → Trung bình (hold/capture, ví credit, pilot) → Hoàn thiện (HTX Pool, đối soát, kế toán, thu tiền thật).
- §14 — Chốt #17 (metering độc lập) và #19 (Rate Card admin); thêm #18 (vị trí ledger: PG Gateway vs DB metering riêng — khuyến nghị DB riêng).
- §7/§14.1 — Chốt danh mục định mức MVP theo định hướng **đo giá trị sử dụng**: traffic storefront (visit · chat · referral từ timlathay.com) + tính năng module (booking · tài chính · kho). Sự kiện phía client (page view) đi qua public ingest, không giữ API key ở client; sự kiện phía server (chat message, feature action) hook trực tiếp.
