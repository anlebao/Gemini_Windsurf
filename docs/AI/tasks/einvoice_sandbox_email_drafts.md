# Draft Email — Xin Sandbox Credentials cho EInvoice Integration

> **Mục đích:** 2 email draft để gửi Viettel Solution + MISA xin sandbox account (Wave 0)
> **Lưu ý:** Thay thế tất cả placeholder `[...]` bằng thông tin thật trước khi gửi
> **Gửi song song:** Cả 2 email nên gửi cùng lúc để tối ưu thời gian chờ (1-2 tuần)

---

## EMAIL 1: Viettel Solution — Xin Sandbox vinvoice 2.0

**To:** lienhe@viettelsolution.com.vn
**Cc:** [email kỹ thuật nếu có]
**Subject:** Yêu cầu cấp tài khoản Sandbox vInvoice 2.0 để tích hợp Hóa đơn điện tử cho nền tảng SaaS HKD

Kính gửi Bộ phận Hỗ trợ Kỹ thuật — Viettel Solution,

Tôi là [TÊN CỦA BẠN], [CHỨC DANH] tại [TÊN CÔNG TY / Tên dự án Vạn An Accounting System].

Chúng tôi đang phát triển nền tảng SaaS kế toán dành cho Hộ kinh doanh (HKD) ngành F&B tại Việt Nam, tuân thủ Thông tư 152/2025/TT-BTC về hóa đơn điện tử (HĐĐT) bắt buộc cho HKD. Nền tảng của chúng tôi đã xây dựng module HĐĐT với kiến trúc multi-provider, và Viettel S-Invoice là provider ưu tiên mà chúng tôi muốn tích hợp.

**Mục đích tích hợp:**
- Phát hành HĐĐT (có mã của CQT) cho HKD F&B thông qua API vInvoice 2.0
- Sử dụng chữ ký số HSM (Hardware Security Module) — không sử dụng USB Token (do ràng buộc multi-tenant SaaS)
- Tự động hóa toàn bộ flow: tạo hóa đơn → tra cứu → tải file PDF/XML → hủy hóa đơn

**Chúng tôi xin yêu cầu:**

1. **Tài khoản Sandbox vInvoice 2.0** trên môi trường `vinvoice.viettel.vn` để test integration
   - Username / Password cho API
   - Mã số thuế nhà cung cấp (supplierTaxCode) trên sandbox
   - Mẫu hóa đơn + Ký hiệu hóa đơn đã được CQT sandbox phê duyệt

2. **Đăng ký IP server vào whitelist** của Viettel
   - IP server test của chúng tôi: [IP SERVER CỦA BẠN — ví dụ: 113.161.82.x]
   - Vui lòng hướng dẫn quy trình đăng ký IP whitelist cho môi trường sandbox

3. **Hỗ trợ kỹ thuật** trong quá trình integration
   - Endpoint auth: `POST /auth/login` (Cookie-based)
   - Endpoint create: `POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}`
   - Endpoint search: `POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid`
   - Endpoint cancel: `POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice`
   - Endpoint get file: `POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile`

**Thông tin dự án:**
- **Tên sản phẩm:** Vạn An Accounting System (ShopERP)
- **Website:** [WEBSITE NẾU CÓ]
- **Mã số thuế công ty:** [MST CỦA BẠN]
- **Người liên hệ kỹ thuật:** [TÊN] — [SĐT] — [EMAIL]
- **Thời gian dự kiến integration:** [TUẦN THỨ X] — mong muốn hoàn tất test sandbox trong 1-2 tuần

Chúng tôi đã nghiên cứu tài liệu API v2.5 (06/2022) và sẵn sàng bắt đầu integration ngay khi nhận được tài khoản sandbox. Nếu cần ký hợp đồng NDA hoặc thỏa thuận đối tác tích hợp, vui lòng gửi mẫu để chúng tôi xem xét.

Rất mong nhận được phản hồi sớm từ Viettel Solution.

Trân trọng,

[TÊN CỦA BẠN]
[CHỨC DANH]
[TÊN CÔNG TY]
[SĐT] | [EMAIL]

---

## EMAIL 2: MISA — Xin AppID + Sandbox meInvoice

**To:** [email hỗ trợ đối tác MISA — cần tìm, có thể là api.support@misa.vn hoặc liên hệ qua website MISA meInvoice]
**Cc:** [email kỹ thuật nếu có]
**Subject:** Yêu cầu cấp AppID + tài khoản Sandbox meInvoice API để tích hợp Hóa đơn điện tử

Kính gửi Bộ phận Hỗ trợ Đối tác — MISA,

Tôi là [TÊN CỦA BẠN], [CHỨC DANH] tại [TÊN CÔNG TY / Tên dự án Vạn An Accounting System].

Chúng tôi đang phát triển nền tảng SaaS kế toán dành cho Hộ kinh doanh (HKD) ngành F&B tại Việt Nam, tuân thủ Thông tư 152/2025/TT-BTC về hóa đơn điện tử (HĐĐT) bắt buộc cho HKD. Nền tảng của chúng tôi đã xây dựng module HĐĐT với kiến trúc multi-provider, và MISA meInvoice là provider dự phòng (backup) mà chúng tôi muốn tích hợp bên cạnh Viettel S-Invoice.

**Mục đích tích hợp:**
- Phát hành HĐĐT (có mã của CQT) cho HKD F&B thông qua MISA meInvoice Integration API
- Sử dụng chữ ký số HSM (SignType = 2, đồng bộ) — không sử dụng USB Token (do ràng buộc multi-tenant SaaS)
- Tự động hóa flow: auth → tạo hóa đơn → tra cứu trạng thái → hủy hóa đơn

**Chúng tôi xin yêu cầu:**

1. **AppID** — mã ứng dụng tích hợp do MISA cấp (bắt buộc cho API auth)
   - Vui lòng hướng dẫn quy trình đăng ký AppID cho đối tác tích hợp

2. **Tài khoản Sandbox** trên môi trường `testapi.meinvoice.vn`
   - Username / Password cho API
   - TaxCode (mã số thuế) trên sandbox
   - Mẫu hóa đơn + Ký hiệu hóa đơn đã được CQT sandbox phê duyệt

3. **Xác nhận endpoints** — chúng tôi cần confirm 2 endpoints sau có tồn tại trong MISA meInvoice API không:
   - **Tra cứu trạng thái hóa đơn:** Endpoint nào? (GET hay POST? Path? Params?)
   - **Hủy hóa đơn:** Endpoint nào? (POST? Body structure? Required fields?)
   - Tài liệu API chúng tôi tham khảo: `doc.meinvoice.vn/itg/` — nhưng không thấy rõ 2 endpoints này

4. **Hỗ trợ kỹ thuật** trong quá trình integration
   - Endpoint auth: `POST /api/integration/auth/token` với body `{appid, taxcode, username, password}`
   - Endpoint create: `POST /api/integration/invoice` với `SignType: 2`
   - Token expiry: 15 ngày (vui lòng confirm)

**Thông tin dự án:**
- **Tên sản phẩm:** Vạn An Accounting System (ShopERP)
- **Website:** [WEBSITE NẾU CÓ]
- **Mã số thuế công ty:** [MST CỦA BẠN]
- **Người liên hệ kỹ thuật:** [TÊN] — [SĐT] — [EMAIL]
- **Thời gian dự kiến integration:** [TUẦN THỨ X] — mong muốn hoàn tất test sandbox trong 1-2 tuần

Chúng tôi đã nghiên cứu tài liệu MISA meInvoice Integration API và sẵn sàng bắt đầu integration ngay khi nhận được AppID + tài khoản sandbox. Nếu cần ký hợp đồng đối tác tích hợp (Partner Agreement) hoặc NDA, vui lòng gửi mẫu để chúng tôi xem xét.

Rất mong nhận được phản hồi sớm từ MISA.

Trân trọng,

[TÊN CỦA BẠN]
[CHỨC DANH]
[TÊN CÔNG TY]
[SĐT] | [EMAIL]

---

## Hướng dẫn sử dụng

### Trước khi gửi
1. Thay thế tất cả `[...]` placeholders bằng thông tin thật
2. Tìm email hỗ trợ đối tác MISA chính xác (có thể cần gọi hotline MISA hoặc qua website `meinvoice.vn`)
3. Xác định IP server test của bạn (chạy `curl ifconfig.me` hoặc hỏi ops team)
4. Nếu có website/demo product, thêm vào phần "Thông tin dự án"

### Sau khi gửi
1. **Theo dõi email phản hồi** trong 3-5 ngày làm việc
2. **Gọi hotline Viettel `1900.8119`** nếu không nhận phản hồi sau 3 ngày
3. **Gọi MISA hotline** (tìm trên website) nếu không nhận phản hồi sau 5 ngày
4. **Update `project_state.md`** Maintenance Log khi nhận được credentials
5. **KHÔNG commit credentials vào repo** — sử dụng `dotnet user-secrets` (hướng dẫn trong Wave 4 task card section 11.4)

### Khi nhận được credentials
- Lưu vào `dotnet user-secrets` (KHÔNG commit):
```bash
cd 3_CoreHub
dotnet user-secrets init
dotnet user-secrets set "EInvoiceProviders:Viettel:Username" "<sandbox user>"
dotnet user-secrets set "EInvoiceProviders:Viettel:Password" "<sandbox pass>"
dotnet user-secrets set "EInvoiceProviders:Viettel:TaxCode" "<supplier tax code>"
dotnet user-secrets set "EInvoiceProviders:Viettel:TemplateCode" "<template code>"
dotnet user-secrets set "EInvoiceProviders:Viettel:SerialNumber" "<series>"
dotnet user-secrets set "EInvoiceProviders:Misa:AppId" "<MISA appid>"
dotnet user-secrets set "EInvoiceProviders:Misa:TaxCode" "<tax code>"
dotnet user-secrets set "EInvoiceProviders:Misa:Username" "<MISA user>"
dotnet user-secrets set "EInvoiceProviders:Misa:Password" "<MISA pass>"
```
- Update `project_state.md` Section 6 (History Log): "Wave 0 — Viettel/MISA sandbox credentials received"
