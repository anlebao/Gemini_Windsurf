# SRS — VẠN AN LOYALTY ALLIANCE
## Đặc tả yêu cầu phần mềm và kiểm toán khả năng đáp ứng của Base Code

**Phiên bản:** 1.0  
**Trạng thái:** Đề xuất để Devin kiểm tra, phản biện và đối chiếu với hệ thống hiện hữu  
**Phương pháp triển khai:** Audit-first, Reuse-first, Evidence-based, No Rewrite by Default  
**Phạm vi:** Liên minh điểm thưởng giữa các thành viên trong hệ sinh thái Vạn An

---

# PHẦN I. CHỈ THỊ BẮT BUỘC CHO DEVIN

## 1.1. Mục tiêu công việc

Rà soát toàn bộ base code hiện tại của Vạn An để xác định khả năng đáp ứng mô hình Liên minh Điểm thưởng Vạn An.

Không được mặc định rằng hệ thống chưa có chức năng. Phải kiểm tra những gì đã tồn tại trong source code, database, API, giao diện, phân quyền, cơ chế đồng bộ và các bài kiểm thử trước khi đề xuất thay đổi.

Mục tiêu cuối cùng là cung cấp một báo cáo kỹ thuật có bằng chứng, chỉ rõ:

1. Yêu cầu nào đã được đáp ứng đầy đủ.
2. Yêu cầu nào đã có nhưng chỉ đáp ứng một phần.
3. Yêu cầu nào chưa được triển khai.
4. Chức năng nào đã tồn tại nhưng sai nghiệp vụ, không an toàn hoặc không phù hợp với mô hình mới.
5. Chức năng nào có thể tái sử dụng trực tiếp.
6. Chức năng nào chỉ cần điều chỉnh.
7. Chức năng nào cần bổ sung mới.
8. Chức năng nào cần loại bỏ hoặc thay thế, kèm lý do và tác động.
9. Rủi ro khi triển khai trên kiến trúc hiện tại.
10. Kế hoạch thực hiện theo từng giai đoạn, có tiêu chí nghiệm thu.

**Không được viết lại toàn bộ module loyalty, tạo hệ thống song song hoặc thay đổi kiến trúc chỉ vì cho rằng một thiết kế mới tốt hơn.**

## 1.2. Quy trình bắt buộc

Devin phải thực hiện theo thứ tự sau.

### Giai đoạn A — Audit, chưa sửa code

- Xác định repository, project, branch, commit và các module liên quan.
- Đọc các quy tắc dự án, tài liệu kiến trúc và trạng thái công việc hiện có.
- Xác định module loyalty, khách hàng, cửa hàng/tenant, thành viên HTX, bán hàng, đơn hàng, khuyến mại, thanh toán, kế toán và phân quyền.
- Truy vết luồng nghiệp vụ từ giao diện đến API, service, database và đồng bộ.
- Chạy các bài kiểm thử hiện có trong môi trường phù hợp.
- Lập ma trận đáp ứng yêu cầu SRS, kèm bằng chứng cụ thể.

### Giai đoạn B — Phân tích khoảng cách

Với từng yêu cầu, xác định:

- Thành phần code hiện có có thể tái sử dụng.
- Phần cần sửa.
- Phần cần bổ sung.
- Phần có hành vi xung đột với yêu cầu.
- Rủi ro dữ liệu, bảo mật, đồng bộ, hiệu năng và tương thích ngược.
- Các vấn đề chưa thể kết luận vì thiếu code, test hoặc bằng chứng vận hành.

### Giai đoạn C — Đề xuất giải pháp

Đưa ra phương án thay đổi nhỏ nhất nhưng vẫn bảo đảm đúng nghiệp vụ và an toàn dữ liệu.

Phải nêu rõ:

- File, class, method, API, bảng dữ liệu hoặc migration dự kiến thay đổi.
- Lý do cần thay đổi.
- Tác động đến module khác.
- Cách bảo vệ dữ liệu hiện hữu.
- Cách tương thích ngược.
- Test cần bổ sung.
- Phương án rollback.

**Dừng sau khi hoàn thành báo cáo audit và kế hoạch thay đổi. Không tự ý sửa code hoặc chạy migration lên môi trường production nếu chưa được phê duyệt.**

Nếu môi trường dự án đã có quy trình cho phép triển khai tự động, vẫn phải tuân thủ các quy tắc kiểm soát thay đổi hiện hành và không được bỏ qua bước phê duyệt bắt buộc.

### Giai đoạn D — Triển khai sau khi được phê duyệt

Chỉ thực hiện những hạng mục đã được phê duyệt. Mỗi thay đổi phải có test, bằng chứng kết quả và cập nhật tài liệu. Không dùng stub, fake success, dữ liệu giả hoặc test chỉ kiểm tra HTTP 200 để tuyên bố hoàn thành nghiệp vụ.

---

# PHẦN II. BỐI CẢNH VÀ MỤC TIÊU SẢN PHẨM

## 2.1. Bối cảnh kinh doanh

Vạn An hướng đến liên kết các hộ kinh doanh, cửa hàng, nhà cung cấp và đơn vị dịch vụ trong các cụm kinh tế địa phương.

Các thành viên tham gia một chương trình ưu đãi chung, cho phép khách hàng tích lũy điểm khi mua hàng tại một thành viên và sử dụng điểm để nhận ưu đãi tại thành viên khác.

HTX hoặc pháp nhân được xác định hợp lệ sẽ điều phối chương trình. Hạ tầng phần mềm Vạn An hỗ trợ ghi nhận giao dịch, quản lý điểm, thực thi quy tắc ưu đãi, phát hiện gian lận và đối soát dữ liệu.

Mục tiêu không phải xây dựng một ví điện tử hay một loại tiền nội bộ.

## 2.2. Mục tiêu nghiệp vụ

- Khuyến khích khách hàng quay lại mua hàng trong mạng lưới thành viên.
- Tạo thêm giá trị cho thành viên khi tham gia liên minh.
- Cho phép nhiều cửa hàng cùng sử dụng một hạ tầng điểm thưởng có quy tắc thống nhất.
- Giúp thành viên kiểm soát chi phí ưu đãi.
- Cho phép HTX quản trị chương trình minh bạch.
- Hỗ trợ mở rộng từ một cụm địa phương lên nhiều HTX.
- Tận dụng tối đa code, dữ liệu và hạ tầng Vạn An hiện có.

## 2.3. Ngoài phạm vi phiên bản đầu

Không xây dựng trong phạm vi MVP:

- Nạp tiền để mua điểm.
- Rút điểm thành tiền mặt.
- Chuyển điểm tự do giữa khách hàng.
- Mua bán điểm hoặc đầu cơ điểm.
- Sử dụng điểm như một phương tiện thanh toán độc lập.
- Phát hành khoản vay từ điểm thưởng.
- Dùng điểm để thanh toán các nghĩa vụ tài chính không thuộc chương trình ưu đãi.
- Tự động chuyển tiền giữa các thành viên chỉ dựa trên số điểm được sử dụng.
- Xây dựng lại toàn bộ hệ thống bán hàng, kế toán, khách hàng hoặc thanh toán nếu các module đó đã tồn tại.

Các chức năng trên chỉ được xem xét trong một dự án riêng, sau khi có đánh giá pháp lý và kiến trúc được phê duyệt.

---

# PHẦN III. NGUYÊN TẮC NGHIỆP VỤ CỐT LÕI

## 3.1. Bản chất điểm thưởng

Điểm thưởng là đơn vị ghi nhận quyền hưởng ưu đãi theo thể lệ chương trình.

Điểm không được mặc định là:

- Tiền hoặc số dư tiền.
- Tiền gửi hoặc khoản tiền khách hàng nạp trước.
- Khoản tiền được bảo đảm có thể rút.
- Công cụ đầu tư hoặc tài sản có lợi tức.
- Phương tiện thanh toán độc lập.

Không được dựa vào tên gọi “loyalty point” để kết luận chương trình không thuộc phạm vi điều chỉnh của pháp luật về thanh toán, khuyến mại hoặc thương mại điện tử. Phải đánh giá cả bản chất nghiệp vụ và cách vận hành thực tế.

## 3.2. Các nguyên tắc bắt buộc

1. Mọi điểm phát sinh phải có nguồn gốc từ một sự kiện nghiệp vụ hợp lệ hoặc một điều chỉnh được ủy quyền.
2. Không phát hành điểm từ giao dịch bị hủy, hoàn toàn bộ hoặc giao dịch gian lận.
3. Mỗi điểm sử dụng chỉ được ghi nhận thành công một lần.
4. Không cho phép số dư điểm âm, trừ khi có một quy trình nghiệp vụ riêng được phê duyệt rõ ràng.
5. Không cho phép thành viên tự ý sửa lịch sử điểm.
6. Không được xóa lịch sử phát sinh đã xác nhận; phải dùng sự kiện đảo hoặc điều chỉnh có truy vết.
7. Chính sách điểm phải có phiên bản và thời điểm hiệu lực.
8. Mỗi cửa hàng chỉ được áp dụng chính sách mà mình có quyền chấp nhận.
9. Điểm và tiền phải được tách biệt trong dữ liệu, API và báo cáo.
10. Không được tạo nghĩa vụ thanh toán tiền giữa các thành viên chỉ bằng một thao tác sử dụng điểm.
11. Tất cả thao tác nhạy cảm phải có phân quyền và audit log.
12. Phải có cơ chế xử lý khi khách trả hàng, đơn bị hoàn, tài khoản bị khóa hoặc thành viên rời chương trình.

---

# PHẦN IV. ĐỐI TƯỢNG VÀ QUYỀN HẠN

## 4.1. Khách hàng

Có quyền:

- Đăng ký hoặc tham gia chương trình theo điều kiện công bố.
- Xem số dư điểm khả dụng.
- Xem lịch sử tích, sử dụng, điều chỉnh và hết hạn điểm.
- Xem thể lệ, hạn sử dụng và các cửa hàng chấp nhận điểm.
- Sử dụng điểm để nhận ưu đãi nếu đáp ứng điều kiện.
- Gửi yêu cầu hỗ trợ hoặc khiếu nại.

Không có quyền:

- Tự chuyển điểm sang tài khoản khách hàng khác.
- Nạp tiền để tạo điểm.
- Rút điểm thành tiền.
- Tự sửa số dư hoặc lịch sử điểm.

## 4.2. Thành viên liên minh

Có quyền:

- Đăng ký tham gia chương trình.
- Đề xuất chính sách tích điểm và ưu đãi trong phạm vi được phép.
- Xem giao dịch và báo cáo thuộc phạm vi của mình.
- Tạm dừng hoặc rời chương trình theo điều kiện hợp đồng.
- Yêu cầu đối soát và xử lý tranh chấp.

Có trách nhiệm:

- Cung cấp dữ liệu giao dịch chính xác.
- Thực hiện ưu đãi đã công bố.
- Chịu trách nhiệm về sản phẩm, dịch vụ và giao dịch do mình cung cấp.
- Không tự phát hành điểm ngoài hệ thống được phê duyệt.
- Không tạo giao dịch giả nhằm lấy điểm.
- Không truy cập dữ liệu khách hàng ngoài phạm vi được cấp quyền.

## 4.3. HTX hoặc pháp nhân điều phối

Phạm vi quyền hạn phải được xác định trong hồ sơ pháp lý và hợp đồng thực tế.

Các chức năng dự kiến:

- Quản lý danh sách thành viên tham gia chương trình.
- Ban hành hoặc phê duyệt thể lệ.
- Phê duyệt chính sách ưu đãi chung.
- Theo dõi hiệu quả chương trình.
- Xử lý các khiếu nại thuộc phạm vi quản lý.
- Quản lý ngân sách khuyến mại hoặc xúc tiến thương mại nếu pháp nhân này được xác định là chủ thể chịu trách nhiệm.
- Xem báo cáo toàn chương trình trong phạm vi được phép.

Không mặc định HTX được sử dụng mọi dữ liệu khách hàng, được thu tiền hoặc được thực hiện mọi giao dịch tài chính thay cho thành viên.

## 4.4. Công ty Vạn An

Các quyền và trách nhiệm phụ thuộc vào vai trò thực tế trong chương trình và hợp đồng với HTX.

Có thể bao gồm:

- Phát triển mạng lưới thành viên.
- Hỗ trợ vận hành thương mại.
- Thực hiện các dịch vụ đã ký kết.
- Theo dõi hiệu quả kinh doanh trong phạm vi được phép.
- Hỗ trợ giải quyết vấn đề nghiệp vụ.

Không mặc định Công ty Vạn An là chủ thể phát hành điểm, người bán hàng hoặc bên bảo lãnh nghĩa vụ của HTX nếu chưa có căn cứ hợp đồng.

## 4.5. LBA Tech

- Cấp quyền sử dụng hệ thống theo hợp đồng với Công ty Vạn An.
- Bảo đảm các nghĩa vụ kỹ thuật, bảo trì và hỗ trợ theo hợp đồng.
- Bảo vệ tính toàn vẹn và an toàn của hệ thống.
- Không mặc định là chủ thể thương mại hoặc chủ thể kiểm soát dữ liệu chỉ vì sở hữu source code.

Phải kiểm tra hợp đồng và cách vận hành thực tế để xác định vai trò pháp lý về dữ liệu.

## 4.6. Quản trị hệ thống

Phải có quyền hạn riêng cho:

- Quản trị kỹ thuật.
- Quản trị chương trình điểm thưởng.
- Quản trị thành viên.
- Kiểm toán và tra cứu lịch sử.
- Xử lý sự cố.

Quyền truy cập dữ liệu không đồng nghĩa với quyền tự ý điều chỉnh số dư điểm hoặc phê duyệt chi phí.

---

# PHẦN V. YÊU CẦU CHỨC NĂNG

## FR-01. Quản lý chương trình liên minh

Hệ thống phải hỗ trợ tạo và quản lý chương trình điểm thưởng với các thông tin tối thiểu:

- Mã chương trình.
- Tên chương trình.
- Pháp nhân/chủ thể chịu trách nhiệm.
- Phạm vi địa lý hoặc nhóm thành viên.
- Ngày bắt đầu và kết thúc.
- Thể lệ và phiên bản thể lệ.
- Đối tượng khách hàng.
- Các thành viên tham gia.
- Chính sách tích điểm.
- Chính sách sử dụng điểm.
- Chính sách hết hạn và xử lý điểm khi chấm dứt.
- Trạng thái: Draft, Pending Approval, Active, Suspended, Closed.

Không cho phép chỉnh sửa âm thầm thể lệ đã có hiệu lực. Mọi thay đổi phải có phiên bản mới, thời điểm hiệu lực và người phê duyệt.

## FR-02. Quản lý thành viên liên minh

Hệ thống phải hỗ trợ:

- Đăng ký thành viên.
- Kiểm tra trạng thái hoạt động.
- Liên kết thành viên với tenant/cửa hàng hiện hữu.
- Ghi nhận ngày tham gia và ngày chấm dứt.
- Quản lý chính sách ưu đãi mà thành viên chấp nhận.
- Quản lý phạm vi dữ liệu được phép truy cập.
- Tạm dừng phát sinh điểm mới khi thành viên bị đình chỉ.
- Xử lý điểm khách hàng đã có khi thành viên rời chương trình theo thể lệ đã công bố.

Devin phải xác định hệ thống hiện có đã có tenant, merchant, organization hoặc membership model nào có thể tái sử dụng. Không tạo bảng thành viên trùng lặp nếu mô hình hiện hữu đã đáp ứng.

## FR-03. Quản lý khách hàng và tài khoản điểm

Hệ thống phải hỗ trợ:

- Liên kết tài khoản điểm với định danh khách hàng hiện có.
- Không tạo tài khoản điểm trùng lặp do một khách hàng đăng ký qua nhiều cửa hàng.
- Tra cứu số điểm khả dụng.
- Tra cứu lịch sử điểm.
- Trạng thái tài khoản: Active, Suspended, Closed.
- Khóa sử dụng điểm khi phát hiện rủi ro hoặc theo yêu cầu hợp lệ.
- Xử lý yêu cầu chỉnh sửa dữ liệu khách hàng theo chính sách dữ liệu áp dụng.

Không yêu cầu khách cung cấp thông tin nhạy cảm hoặc giấy tờ định danh vượt quá nhu cầu hợp pháp của chương trình.

## FR-04. Tích điểm từ giao dịch hợp lệ

Hệ thống phải hỗ trợ tích điểm từ dữ liệu giao dịch bán hàng có thật.

Luồng chuẩn:

1. Nhận sự kiện giao dịch từ module bán hàng hoặc nguồn tích hợp được phê duyệt.
2. Kiểm tra tenant, cửa hàng, khách hàng, trạng thái đơn và giá trị giao dịch.
3. Kiểm tra giao dịch đã được xử lý điểm hay chưa.
4. Áp dụng đúng phiên bản chính sách tích điểm tại thời điểm có hiệu lực.
5. Tạo bút toán điểm.
6. Cập nhật số dư khả dụng.
7. Ghi log và trả kết quả xử lý.

Các điều kiện bắt buộc:

- Sự kiện tích điểm có idempotency key hoặc cơ chế tương đương.
- Retry không tạo điểm trùng.
- Đơn bị hủy hoặc hoàn được xử lý theo quy tắc đảo điểm.
- Không tích điểm dựa trên một thông báo thành công giả.
- Nếu đồng bộ từ hệ thống offline, phải xử lý trùng lặp và sự kiện đến sai thứ tự.
- Không cho phép cửa hàng tự gửi một số điểm tùy ý mà không qua rule engine và xác thực giao dịch.

## FR-05. Quy tắc tính điểm

Rule engine phải hỗ trợ ít nhất:

- Tỷ lệ tích điểm theo giá trị giao dịch.
- Điều kiện giao dịch được tính điểm.
- Khoản mục được loại trừ.
- Giới hạn điểm tối đa mỗi giao dịch hoặc mỗi tài khoản.
- Điều kiện theo cửa hàng hoặc chiến dịch.
- Thời điểm ghi nhận điểm.
- Phiên bản chính sách.
- Làm tròn số và xử lý giá trị biên.
- Bật/tắt quy tắc theo thời gian.

Ví dụ cấu hình thử nghiệm:

- Mỗi 10.000 đồng giá trị đủ điều kiện được nhận 1 điểm.
- Chỉ cấp điểm sau khi đơn hoàn tất.
- Không cấp điểm cho phần đơn đã bị hủy hoặc hoàn.
- Mỗi chương trình có thể áp dụng mức khác nhau theo thể lệ đã công bố.

Các con số trên là dữ liệu mẫu, không phải hằng số được hard-code.

## FR-06. Sử dụng điểm để nhận ưu đãi

Hệ thống phải cho phép khách hàng sử dụng điểm tại cửa hàng chấp nhận ưu đãi.

Luồng chuẩn:

1. Khách chọn sử dụng điểm.
2. Hệ thống xác thực khách hàng và cửa hàng.
3. Kiểm tra số dư điểm khả dụng.
4. Kiểm tra cửa hàng có chấp nhận điểm và chương trình còn hiệu lực.
5. Kiểm tra điều kiện đơn hàng, hạn mức và tỷ lệ giảm.
6. Khóa hoặc giữ số điểm cần sử dụng.
7. Xác nhận giao dịch giảm giá từ module bán hàng.
8. Ghi bút toán sử dụng điểm và cập nhật số dư.
9. Trả kết quả và lưu audit log.

Nếu đơn hàng thất bại hoặc timeout, hệ thống phải xác định giao dịch đã thành công hay chưa trước khi giải phóng điểm. Không được vừa trừ điểm vừa giữ nguyên giá phải trả, hoặc vừa hoàn điểm vừa giữ ưu đãi đã được áp dụng.

Không được cho phép dùng một số điểm hai lần do xử lý đồng thời.

## FR-07. Cấu hình giá trị ưu đãi

Hệ thống phải hỗ trợ quy tắc ưu đãi theo cửa hàng/chương trình.

Ví dụ:

- 100 điểm giúp giảm 5.000 đồng.
- Tối đa 10% giá trị đơn hàng.
- Không áp dụng cho một số sản phẩm/dịch vụ được quy định trước.
- Không cộng dồn với chương trình khác nếu thể lệ không cho phép.

Các tham số trên phải cấu hình được.

Phải phân biệt rõ:

- Số điểm bị tiêu thụ.
- Giá trị giảm giá trên đơn hàng.
- Chi phí khuyến mại của cửa hàng.
- Khoản hỗ trợ thương mại của HTX, nếu có.
- Khoản tiền phải trả giữa các pháp nhân, nếu có căn cứ riêng.

Không được xem các đại lượng này là cùng một giá trị mặc định.

## FR-08. Hạn sử dụng và điểm hết hạn

Hệ thống phải hỗ trợ:

- Hạn sử dụng theo từng lô điểm hoặc chính sách.
- Thông báo trước khi hết hạn nếu phù hợp.
- Tự động đánh dấu điểm hết hạn bằng job an toàn, có thể retry.
- Không làm hết hạn hai lần.
- Ghi nhận lịch sử và lý do.
- Xử lý điểm hết hạn khi chương trình kết thúc.

Không xóa lịch sử điểm hết hạn.

## FR-09. Đơn hàng hủy, trả hàng và hoàn tiền

Hệ thống phải xử lý:

- Hủy đơn trước khi tích điểm.
- Hủy đơn sau khi đã tích điểm.
- Hoàn toàn bộ đơn.
- Hoàn một phần đơn.
- Đơn đã sử dụng điểm rồi mới yêu cầu hoàn.
- Khách hàng đã dùng điểm được tích từ một giao dịch sau đó bị hoàn.
- Tài khoản không đủ điểm để đảo toàn bộ điểm đã cấp.

Devin phải đề xuất cách xử lý theo chính sách kinh doanh và kiểm tra tác động đến code hiện có.

Không được tự tạo số dư âm hoặc xóa bút toán lịch sử chỉ để làm cho số dư khớp.

Các trường hợp khách đã sử dụng ưu đãi cần có quy tắc công bằng, công khai và phù hợp pháp luật, không tự động biến thành khoản nợ tiền mặt của khách.

## FR-10. Sổ cái điểm thưởng

Phải có khả năng truy vết đầy đủ các sự kiện:

- Earn.
- Redeem.
- Expire.
- Reverse.
- Adjust.
- Hold.
- Release Hold.

Tên sự kiện có thể khác nếu hệ thống hiện tại đã có mô hình tương đương.

Mỗi bút toán cần lưu các trường tương đương:

- Transaction ID.
- Customer ID.
- Program ID.
- Merchant/Tenant ID.
- Event Type.
- Points Delta.
- Related Order ID.
- Related Ledger Entry ID.
- Idempotency Key.
- Rule Version.
- Status.
- Created At.
- Effective At.
- Actor/Source.
- Reason Code.
- Correlation ID.

Không bắt buộc tạo đúng các bảng có tên trên nếu base code đã có mô hình sổ cái đủ khả năng đáp ứng. Devin phải chỉ rõ cách tái sử dụng.

Yêu cầu bắt buộc:

- Lịch sử bất biến về mặt nghiệp vụ sau khi xác nhận.
- Điều chỉnh thông qua bút toán mới.
- Không cho phép sửa trực tiếp số dư tùy tiện.
- Có thể tái dựng số dư từ lịch sử hoặc có cơ chế kiểm chứng tương đương.
- Có đối soát giữa số dư lưu trữ và tổng các bút toán.
- Xử lý an toàn các giao dịch đồng thời.

## FR-11. Ngân sách ưu đãi

MVP phải hỗ trợ theo dõi ngân sách ưu đãi của từng thành viên hoặc chiến dịch nếu nghiệp vụ cần.

Hệ thống cần phân biệt:

- Ngân sách dự kiến.
- Ngân sách đã cam kết.
- Giá trị ưu đãi đã sử dụng.
- Giá trị bị hủy/đảo.
- Giá trị còn lại.

Không tự động chuyển tiền từ tài khoản HTX cho cửa hàng khi điểm được sử dụng.

Nếu có cơ chế HTX tài trợ ưu đãi, phải có hợp đồng, quy trình phê duyệt và chứng từ phù hợp. Chức năng chi trả hoặc thanh toán bù trừ không thuộc phạm vi mặc định của MVP.

## FR-12. Đối soát và báo cáo

Cung cấp báo cáo:

- Điểm phát sinh theo cửa hàng.
- Điểm sử dụng theo cửa hàng.
- Điểm hết hạn.
- Điểm bị đảo do hoàn/hủy.
- Số dư điểm.
- Giao dịch lỗi và đang chờ xử lý.
- Tỷ lệ tích điểm và sử dụng điểm.
- Chi phí ưu đãi theo cửa hàng/chiến dịch nếu có dữ liệu hợp lệ.
- Chênh lệch đối soát.
- Nhật ký điều chỉnh thủ công.

Phải lọc được theo thời gian, chương trình, HTX, tenant/cửa hàng và trạng thái.

Thành viên chỉ xem được dữ liệu thuộc phạm vi của mình. HTX chỉ xem dữ liệu tổng hợp hoặc dữ liệu chi tiết theo phạm vi quyền được cấp.

## FR-13. Khiếu nại và điều chỉnh thủ công

Hệ thống phải cho phép:

- Tạo yêu cầu hỗ trợ.
- Tra cứu lịch sử giao dịch.
- Ghi nhận lý do khiếu nại.
- Phê duyệt điều chỉnh theo phân quyền.
- Ghi nhận người yêu cầu, người duyệt và lý do.
- Theo dõi trạng thái xử lý.
- Thông báo kết quả cho khách hàng nếu có kênh thông báo phù hợp.

Điều chỉnh thủ công không được bỏ qua audit log và giới hạn phân quyền.

## FR-14. Chấm dứt tham gia chương trình

Khi một cửa hàng rời liên minh:

- Không phát sinh điểm mới cho giao dịch sau thời điểm chấm dứt.
- Các giao dịch hợp lệ trước thời điểm chấm dứt vẫn phải được xử lý theo thể lệ.
- Hệ thống xác định điểm khách hàng còn tồn tại và các quyền lợi liên quan.
- Áp dụng chính sách chuyển tiếp hoặc chấm dứt đã công bố.
- Không tự ý xóa điểm hoặc dữ liệu lịch sử.
- Xử lý quyền truy cập dữ liệu của thành viên đã rời chương trình.

Khi toàn bộ chương trình kết thúc, phải có quy trình công bố, xử lý quyền lợi còn lại và lưu trữ dữ liệu theo nghĩa vụ áp dụng.

---

# PHẦN VI. YÊU CẦU PHI CHỨC NĂNG

## NFR-01. Bảo mật và phân quyền

- Tuân thủ cơ chế authentication và authorization hiện có.
- Không chỉ ẩn nút trên giao diện; API phải kiểm tra quyền thực sự.
- Cách ly dữ liệu theo tenant.
- Kiểm soát quyền xem dữ liệu khách hàng.
- Ghi audit log cho thao tác nhạy cảm.
- Không để quản trị kỹ thuật tự ý thay đổi số dư mà không có quy trình được kiểm soát.
- Kiểm tra các lỗ hổng truy cập chéo tenant.

## NFR-02. Tính toàn vẹn giao dịch

- Bảo đảm idempotency khi retry.
- Ngăn trừ điểm trùng.
- Xử lý transaction boundary rõ ràng.
- Có cơ chế phục hồi khi dịch vụ phụ thuộc lỗi.
- Không báo thành công khi chỉ mới nhận request mà chưa xác nhận trạng thái nghiệp vụ.
- Không để xử lý bất đồng bộ làm phát sinh điểm hai lần.

## NFR-03. Tương thích kiến trúc hiện hữu

Devin phải xác minh hệ thống thực tế trước khi chọn giải pháp.

Đối với các thành phần Vạn An đã có, cần đánh giá khả năng tái sử dụng:

- KhachLink và định danh khách hàng.
- ShopERP và dữ liệu bán hàng.
- Hệ thống loyalty hiện tại.
- Tenant/merchant registry.
- CoreHub/PostgreSQL.
- SQLite offline và cơ chế đồng bộ.
- API Gateway.
- Cơ chế authentication và phân quyền.
- Logging, monitoring và test framework.

Không mặc định rằng toàn bộ danh sách này đã được triển khai đầy đủ hoặc vẫn giữ nguyên thiết kế. Phải xác minh từ base code hiện tại.

Nếu SQLite offline và CoreHub có cùng liên quan đến nghiệp vụ điểm, phải chỉ rõ nơi nào là nguồn dữ liệu có thẩm quyền, cách đồng bộ và cách xử lý xung đột. Không cho phép một thiết bị offline tự tạo quyền sử dụng điểm đã được xác nhận trên máy khác nếu không có cơ chế bảo đảm tính toàn vẹn.

## NFR-04. Hiệu năng và khả năng mở rộng

- Thiết kế không phụ thuộc vào một cửa hàng hoặc một HTX duy nhất.
- Truy vấn lịch sử phải có phân trang.
- Có index phù hợp với truy vấn số dư, lịch sử và đối soát.
- Tác vụ hết hạn và đối soát phải chạy theo batch có thể retry.
- Không thực hiện quét toàn bộ lịch sử điểm trong mỗi lần sử dụng điểm.
- Đề xuất giới hạn tải dựa trên benchmark và năng lực thực tế của base code, không tự đưa ra số liệu đã đạt khi chưa kiểm thử.

## NFR-05. Quan sát và vận hành

- Log có correlation ID.
- Có chỉ số về lỗi tích điểm, lỗi sử dụng điểm, giao dịch chờ và chênh lệch đối soát.
- Có cảnh báo khi số dư không khớp hoặc xuất hiện hành vi gian lận.
- Có quy trình phục hồi và xử lý sự cố.
- Có thể truy vết một khiếu nại từ giao dịch bán hàng đến bút toán điểm.

---

# PHẦN VII. YÊU CẦU PHÁP LÝ VÀ TUÂN THỦ

Đây là yêu cầu nghiệp vụ hỗ trợ tuân thủ, không phải kết luận pháp lý thay cho luật sư.

Devin phải xác định các điểm trong hệ thống có ảnh hưởng đến việc tuân thủ và liệt kê những vấn đề cần người có chuyên môn pháp lý xác nhận trước khi phát hành.

## LEG-01. Thể lệ chương trình

Phải hỗ trợ lưu trữ và công bố:

- Chủ thể chịu trách nhiệm.
- Điều kiện tham gia.
- Cách tích và sử dụng điểm.
- Phạm vi cửa hàng chấp nhận điểm.
- Điều kiện ưu đãi và hạn chế.
- Thời hạn điểm.
- Quy tắc khi hoàn/hủy đơn.
- Cơ chế khiếu nại.
- Xử lý điểm khi cửa hàng hoặc chương trình kết thúc.
- Các điều kiện thay đổi thể lệ.

## LEG-02. Phân định điểm thưởng và thanh toán

Hệ thống không được mặc định cho phép:

- Nạp tiền mua điểm.
- Rút điểm thành tiền.
- Chuyển điểm tự do giữa người dùng.
- Giao dịch mua bán điểm.
- Dùng điểm như một tài khoản tiền.
- Chuyển tiền tự động giữa các thành viên chỉ dựa trên điểm đã sử dụng.

Mọi yêu cầu mở rộng phải có đánh giá pháp lý và phê duyệt riêng.

## LEG-03. Thương mại điện tử và khuyến mại

Devin phải xác định:

- Ứng dụng có chức năng nào cho phép giới thiệu sản phẩm, đặt hàng hoặc kết nối người mua với nhiều người bán.
- Chủ thể nào đứng tên chương trình điểm thưởng.
- Chủ thể nào là thương nhân thực hiện khuyến mại.
- Các thông tin thể lệ phải công khai.
- Các thủ tục thông báo hoặc đăng ký có thể áp dụng.
- Trách nhiệm của nền tảng và các thành viên.

Không được kết luận rằng HTX hoặc chương trình điểm thưởng được miễn nghĩa vụ thương mại điện tử chỉ vì không có tính năng rút tiền mặt.

## LEG-04. Dữ liệu cá nhân

- Xác định vai trò pháp lý của các bên liên quan đến dữ liệu.
- Giới hạn dữ liệu thu thập theo mục đích.
- Có thông báo quyền riêng tư phù hợp.
- Quản lý quyền truy cập và chia sẻ dữ liệu.
- Hỗ trợ yêu cầu của chủ thể dữ liệu theo quy định áp dụng.
- Có chính sách lưu trữ và xóa/ẩn danh khi phù hợp.
- Ghi nhận sự kiện truy cập dữ liệu nhạy cảm.
- Không chia sẻ danh sách khách hàng chi tiết giữa các thành viên chỉ vì cùng tham gia HTX.

## LEG-05. Thuế, kế toán và hóa đơn

Phải phân biệt dữ liệu phục vụ chương trình điểm với dữ liệu kế toán.

Hệ thống phải cho phép truy xuất:

- Giá trị giao dịch gốc.
- Giá trị giảm giá.
- Giá trị khách thanh toán.
- Điểm được cấp.
- Điểm được sử dụng.
- Đơn hoàn/hủy.
- Khoản hỗ trợ thương mại nếu có.
- Chủ thể chịu chi phí ưu đãi.

Không tự động coi điểm là tiền, doanh thu hoặc chi phí kế toán. Việc ghi nhận phải dựa trên chính sách kế toán, chứng từ và tư vấn thuế đã được phê duyệt.

---

# PHẦN VIII. MÔ HÌNH DỮ LIỆU THAM CHIẾU

Đây là mô hình logic để Devin đối chiếu với base code, không phải chỉ thị tạo ngay các bảng mới.

| Entity logic | Mục đích |
|---|---|
| LoyaltyProgram | Thông tin chương trình và trạng thái |
| ProgramVersion | Phiên bản thể lệ và chính sách |
| ProgramMember | Quan hệ tham gia giữa chương trình và thành viên |
| LoyaltyAccount | Tài khoản điểm của khách hàng |
| LoyaltyRule | Quy tắc tích và sử dụng điểm |
| LoyaltyLedgerEntry | Lịch sử điểm bất biến về mặt nghiệp vụ |
| LoyaltyHold | Điểm tạm khóa trong giao dịch sử dụng |
| LoyaltyRedemption | Ghi nhận việc đổi điểm lấy ưu đãi |
| LoyaltyAdjustmentCase | Hồ sơ điều chỉnh và khiếu nại |
| PromotionBudget | Ngân sách ưu đãi nếu có |
| AuditEvent | Nhật ký hành động nhạy cảm |

Devin phải xác định entity, bảng hoặc aggregate hiện có tương ứng với từng mục.

Không tạo bảng trùng lặp nếu có thể mở rộng mô hình hiện hữu một cách an toàn.

Mọi migration mới phải có:

- Kế hoạch backup.
- Khả năng triển khai an toàn.
- Xử lý dữ liệu hiện hữu.
- Khả năng rollback hoặc phương án phục hồi được mô tả rõ.
- Test migration và test tương thích ngược.

---

# PHẦN IX. TIÊU CHÍ NGHIỆM THU VÀ TEST CASE

Các test dưới đây là điều kiện nghiệm thu tối thiểu. Devin cần bổ sung test theo phát hiện từ base code.

## TC-01. Tích điểm hợp lệ

Given: đơn hàng hợp lệ, đã hoàn tất, khách hàng và cửa hàng đủ điều kiện.

When: hệ thống xử lý sự kiện tích điểm.

Then: điểm được cấp đúng chính sách và có bút toán truy vết được.

## TC-02. Retry sự kiện

Given: một giao dịch đã được cấp điểm.

When: cùng sự kiện được gửi lại nhiều lần.

Then: không phát sinh điểm trùng.

## TC-03. Đơn bị hủy hoặc hoàn

Given: đơn đã phát sinh điểm.

When: đơn bị hủy hoặc hoàn theo nghiệp vụ.

Then: hệ thống tạo sự kiện đảo/điều chỉnh phù hợp; lịch sử gốc vẫn được giữ.

## TC-04. Sử dụng điểm đồng thời

Given: khách hàng có 100 điểm khả dụng.

When: hai request đồng thời cùng yêu cầu sử dụng 80 điểm.

Then: tối đa một request được xác nhận nếu không đủ điểm cho cả hai; số dư không âm và không có sử dụng trùng.

## TC-05. Cửa hàng không tham gia

Given: cửa hàng không nằm trong phạm vi chương trình.

When: khách yêu cầu dùng điểm tại cửa hàng đó.

Then: giao dịch bị từ chối với lý do nghiệp vụ rõ ràng.

## TC-06. Vượt hạn mức ưu đãi

Given: chính sách chỉ cho giảm tối đa 10% giá trị đơn.

When: request yêu cầu giảm vượt giới hạn.

Then: hệ thống từ chối hoặc tính lại theo quy tắc được phê duyệt; không tin giá trị giảm do client gửi lên.

## TC-07. Hết hạn điểm

Given: một lô điểm đã hết hạn.

When: tác vụ hết hạn được chạy nhiều lần.

Then: điểm chỉ hết hạn một lần, số dư và lịch sử vẫn nhất quán.

## TC-08. Phân quyền tenant

Given: tài khoản cửa hàng A.

When: truy vấn dữ liệu khách hàng hoặc giao dịch thuộc cửa hàng B ngoài phạm vi được cấp.

Then: hệ thống từ chối truy cập tại backend.

## TC-09. Điều chỉnh thủ công

Given: người dùng không có quyền phê duyệt.

When: gửi request điều chỉnh số dư.

Then: request bị từ chối, không phát sinh bút toán.

## TC-10. Đơn hàng thất bại giữa chừng

Given: điểm đã được giữ nhưng module bán hàng timeout.

When: hệ thống khôi phục và xác minh trạng thái đơn.

Then: giao dịch được hoàn tất hoặc giải phóng điểm đúng một lần; không mất điểm và không cấp ưu đãi hai lần.

## TC-11. Đồng bộ offline

Given: thiết bị offline gửi lại sự kiện đã được xử lý hoặc gửi sự kiện sai thứ tự.

When: Sync Worker đồng bộ lên CoreHub.

Then: không nhân đôi điểm, không ghi đè lịch sử và không làm sai số dư.

## TC-12. Thành viên rời chương trình

Given: cửa hàng đã rời chương trình.

When: có giao dịch mới hoặc khách sử dụng điểm theo chính sách chuyển tiếp.

Then: giao dịch mới bị xử lý theo trạng thái thành viên; quyền lợi cũ được xử lý theo thể lệ đã công bố.

## TC-13. Kiểm tra số dư

Given: có chuỗi bút toán Earn, Redeem, Expire và Reverse.

When: chạy đối soát.

Then: số dư khả dụng và số dư được tính từ ledger phải khớp theo quy tắc hệ thống.

## TC-14. Bảo vệ dữ liệu

Given: thành viên chỉ được xem dữ liệu thuộc phạm vi cho phép.

When: người dùng thử truy vấn dữ liệu ngoài phạm vi qua API trực tiếp.

Then: dữ liệu không bị tiết lộ.

## TC-15. Không biến điểm thành tiền

Given: hệ thống đang chạy chương trình điểm thưởng.

When: request cố gắng nạp tiền, rút điểm, chuyển điểm tự do hoặc tạo lệnh chuyển tiền dựa trên điểm.

Then: chức năng không được cung cấp trong phạm vi MVP và request bị từ chối an toàn.

---

# PHẦN X. MA TRẬN AUDIT BẮT BUỘC

Devin phải tạo một ma trận có ít nhất các cột sau:

| Cột | Nội dung |
|---|---|
| Requirement ID | Mã yêu cầu trong SRS |
| Requirement | Nội dung cần đáp ứng |
| Status | Đã có / Một phần / Chưa có / Sai / Chưa xác minh |
| Evidence | File, class, method, API, bảng dữ liệu hoặc test |
| Current Behavior | Hành vi thực tế của code hiện tại |
| Gap | Điểm chưa đáp ứng hoặc bằng chứng còn thiếu |
| Reuse | Thành phần có thể dùng lại |
| Change Required | Thay đổi đề xuất |
| Risk | Rủi ro kỹ thuật/nghiệp vụ |
| Test | Test cần có hoặc test đã chạy |
| Priority | P0/P1/P2/P3 |
| Approval | Đã phê duyệt hay chưa |

### Quy tắc đánh giá

- **Đã có:** phải có bằng chứng code và test phù hợp.
- **Một phần:** nêu chính xác phần đã có và phần còn thiếu.
- **Chưa có:** đã kiểm tra phạm vi liên quan và không tìm thấy triển khai tương ứng.
- **Sai:** có code nhưng hành vi vi phạm yêu cầu hoặc có lỗi thiết kế.
- **Chưa xác minh:** không đủ bằng chứng, chưa chạy được test hoặc phụ thuộc bên ngoài.

Không được đánh dấu “đã có” chỉ vì có entity, DTO, giao diện hoặc API mang tên tương tự.

---

# PHẦN XI. PHÂN LOẠI ƯU TIÊN

## P0 — Phải giải quyết trước khi phát hành

- Trừ điểm trùng hoặc cấp điểm trùng.
- Sử dụng điểm vượt số dư.
- Truy cập chéo tenant.
- Có thể sửa số dư mà không kiểm soát.
- Không truy vết được lịch sử điểm.
- Retry hoặc đồng bộ có thể tạo sai lệch điểm.
- Chức năng thực tế cho phép nạp/rút/chuyển tiền ngoài phạm vi được phê duyệt.

## P1 — Bắt buộc cho MVP

- Quản lý chương trình và thành viên.
- Tích điểm theo giao dịch hợp lệ.
- Sử dụng điểm lấy ưu đãi.
- Hủy/hoàn và đảo điểm.
- Hạn sử dụng điểm.
- Phân quyền và audit log.
- Báo cáo đối soát.
- Quy trình xử lý lỗi.

## P2 — Sau khi MVP ổn định

- Quản lý ngân sách ưu đãi nâng cao.
- Phân tích hành vi khách hàng trong phạm vi dữ liệu được phép.
- Tối ưu chiến dịch và phân nhóm khách hàng.
- Dashboard toàn liên minh.
- Tự động cảnh báo gian lận nâng cao.

## P3 — Chưa nằm trong MVP

- Quỹ xúc tiến thương mại có cơ chế tài trợ chung.
- Đối soát nghĩa vụ tài chính giữa thành viên.
- Chuyển điểm giữa người dùng.
- Quy đổi điểm thành tiền.
- Tích hợp các sản phẩm tài chính.

Các hạng mục P3 không được tự ý triển khai trong dự án này.

---

# PHẦN XII. ĐỊNH DẠNG BÁO CÁO DEVIN PHẢI TRẢ VỀ

Báo cáo phải gồm các phần:

## 12.1. Executive Summary

- Base code hiện tại đáp ứng bao nhiêu yêu cầu.
- Các điểm nghẽn lớn nhất.
- Rủi ro P0/P1.
- Khuyến nghị triển khai hay chưa triển khai.
- Ước lượng công việc dựa trên code thực tế, nêu rõ giả định.

Không được tự tạo phần trăm hoàn thành nếu không có phương pháp đếm và bằng chứng rõ ràng.

## 12.2. Architecture Discovery

Sơ đồ luồng hiện tại:

- Nguồn phát sinh giao dịch.
- Module loyalty.
- API.
- Database.
- Sync Worker nếu có.
- Hệ thống báo cáo.
- Các module liên quan.

Phải chỉ ra luồng nào đã được xác minh từ code và luồng nào mới là giả thuyết.

## 12.3. Requirement Coverage Matrix

Ma trận đầy đủ cho toàn bộ FR, NFR, LEG và TC có liên quan.

## 12.4. Reuse / Modify / Add / Remove

Phân loại từng thành phần:

- Reuse: dùng lại nguyên trạng.
- Modify: điều chỉnh thành phần hiện có.
- Add: bổ sung chức năng hoặc thành phần mới.
- Remove: loại bỏ hoặc thay thế phần sai, có bằng chứng và phương án chuyển đổi.

Không được đề xuất Remove nếu chưa phân tích phụ thuộc và dữ liệu hiện hữu.

## 12.5. Risk Register

Mỗi rủi ro cần có:

- Mô tả.
- Bằng chứng.
- Khả năng xảy ra.
- Mức ảnh hưởng.
- Biện pháp giảm thiểu.
- Chủ thể cần quyết định.

## 12.6. Implementation Plan

Chia thành các PR hoặc giai đoạn nhỏ, có:

- Mục tiêu.
- Danh sách file/module dự kiến.
- Phụ thuộc.
- Migration.
- Test.
- Tiêu chí nghiệm thu.
- Kế hoạch rollback.

## 12.7. Những câu hỏi cần người dùng quyết định

Không tự suy đoán những quyết định ảnh hưởng đến:

- Chủ thể pháp lý đứng tên chương trình.
- Ai tài trợ ưu đãi.
- Chính sách điểm khi hoàn/hủy.
- Thời hạn điểm.
- Giới hạn sử dụng.
- Cách xử lý khi thành viên rời chương trình.
- Cách xử lý điểm khi toàn bộ chương trình kết thúc.
- Cơ chế tài trợ ưu đãi chung trong tương lai.

Nếu thiếu quyết định, ghi rõ giả định và tiếp tục audit phần có thể kiểm chứng; không dừng toàn bộ công việc chỉ vì thiếu một số thông tin nghiệp vụ.

---

# PHẦN XIII. DEFINITION OF DONE

Chỉ được kết luận audit hoàn tất khi:

1. Có ma trận đáp ứng đầy đủ các yêu cầu liên quan.
2. Mỗi kết luận quan trọng có bằng chứng cụ thể.
3. Đã kiểm tra các luồng tích điểm, sử dụng điểm, hoàn/hủy và đồng bộ.
4. Đã xác định được nơi lưu số dư và lịch sử điểm.
5. Đã đánh giá phân quyền, cách ly tenant và xử lý đồng thời.
6. Đã chỉ rõ những thành phần có thể tái sử dụng.
7. Đã tách yêu cầu pháp lý cần xác nhận khỏi kết luận kỹ thuật.
8. Có kế hoạch thay đổi nhỏ nhất và tiêu chí nghiệm thu.
9. Không có sửa đổi code ngoài phạm vi được phê duyệt.
10. Các giả định và hạn chế của audit được ghi rõ.

Chỉ được tuyên bố tính năng hoạt động sau khi có test hoặc bằng chứng vận hành phù hợp; không coi việc viết code, tạo migration hay thêm giao diện là hoàn thành nghiệp vụ.

---

# PHẦN XIV. THÔNG ĐIỆP THỰC THI CHO DEVIN

Hãy bắt đầu bằng việc đọc các quy tắc dự án và xác định trạng thái base code hiện tại.

Không triển khai ngay theo thiết kế trong tài liệu này. Hãy xem SRS là bộ yêu cầu mục tiêu cần được đối chiếu với hệ thống thực tế.

Ưu tiên tái sử dụng các module Vạn An đã có; không tạo một hệ thống loyalty thứ hai nếu có thể mở rộng an toàn hệ thống hiện hữu.

Không thay đổi kiến trúc, database hoặc API công khai chỉ để làm cho code giống tài liệu. Nếu phát hiện xung đột giữa SRS và kiến trúc hiện tại, hãy nêu rõ bằng chứng, phương án và tác động trước khi sửa.

Hãy phản biện SRS nếu có yêu cầu trùng lặp, không khả thi, gây rủi ro bảo mật, không phù hợp với mô hình dữ liệu hoặc chưa đủ rõ để triển khai.

**Deliverable đầu tiên là báo cáo audit có bằng chứng và kế hoạch thay đổi được đề xuất. Chưa sửa code cho đến khi báo cáo được xem xét và phê duyệt.**

Kết quả cần trả lời được câu hỏi cốt lõi:

**“Với base code Vạn An hiện tại, cần tận dụng những gì, sửa chính xác những gì, bổ sung những gì và loại bỏ những gì để triển khai Liên minh Điểm thưởng an toàn mà không xây lại những thứ đã có?”**
