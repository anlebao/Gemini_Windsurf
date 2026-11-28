# Review và định hướng cải tiến UI/UX Shop AI Vạn An

## Kết luận

Bản hiện tại đang sử dụng không gian chưa hợp lý. Tuy nhiên, chưa cần
đập bỏ toàn bộ để xây dựng lại từ đầu.

Phương án đề xuất là **Redesign mạnh phần UI/UX, giữ lại những thành
phần nghiệp vụ và logic đang hoạt động tốt**.

## 1. Review bản hiện tại -- Những điểm cần cải tiến

### 1.1. Khoảng không gian phía trên

Phần nền/banner cửa hàng chiếm nhiều diện tích nhưng không đóng góp trực
tiếp vào trải nghiệm hội thoại.

**Đề xuất:** Khi vào AI, chuyển sang chế độ làm việc tập trung; ẩn hoặc
thu gọn banner cửa hàng; dành diện tích chính cho hội thoại và tương tác
với AI.

### 1.2. Khung hội thoại

Khung trắng lớn nhưng nội dung chỉ chiếm phần nhỏ phía trên. Khoảng
trống giữa câu hỏi gợi ý và ô nhập quá lớn.

**Đề xuất:** - Hội thoại sử dụng toàn bộ chiều cao khả dụng. - Tin nhắn
tự động mở rộng theo nội dung. - Ô nhập luôn nằm cố định phía dưới. -
Nội dung hội thoại cuộn độc lập.

### 1.3. Header và thanh điều hướng

Phần tên trợ lý, nút xổ xuống và hai tab đang chiếm khá nhiều chiều cao.

**Đề xuất:** Gộp thành một thanh header gọn, có avatar AI, tên trợ lý,
trạng thái và nút mở rộng.

### 1.4. Thanh nhập liệu

Ô nhập hiện quá nhỏ; các nút loa, microphone, gửi tách rời và chưa có
phân cấp trực quan.

**Đề xuất:** Một Smart Input duy nhất, tích hợp đính kèm, voice, gửi và
các chức năng mở rộng.

### 1.5. Quick Suggestions

Các câu hỏi gợi ý rất hữu ích cho khách hàng ít có thói quen sử dụng AI.

**Đề xuất:** Giữ lại nhưng chuyển thành các chip thông minh, tự thay đổi
theo cửa hàng và ngữ cảnh.

## 2. Vấn đề lớn nhất: Cách sử dụng không gian

Đề xuất thay đổi cấu trúc hiện tại.

Thay vì: - Header chiếm diện tích. - Banner cửa hàng nằm phía sau. - Một
hộp hội thoại nằm giữa màn hình. - Phía dưới hộp hội thoại có khoảng
trống lớn.

Hãy chuyển sang:

**AI Chat là nội dung chính của trang, không phải một hộp thoại nằm bên
trong trang.**

### Bố cục desktop đề xuất

-   **Header gọn:** tên trợ lý, trạng thái, lịch sử, mở rộng và đóng.
-   **Sidebar tùy chọn:** hội thoại, lịch sử, dịch vụ, đặt lịch, hỗ trợ.
-   **Main chat:** chiếm toàn bộ vùng trung tâm.
-   **Smart Input:** nằm dưới cùng, luôn dễ tiếp cận.
-   **Scroll độc lập:** nội dung hội thoại cuộn riêng, không làm cuộn cả
    trang.

Sidebar hội thoại có thể ẩn hoàn toàn trong chế độ khách hàng mới.

## 3. Hai chế độ giao diện thay vì ép tất cả vào một bố cục

Vạn An có hai nhóm trải nghiệm khác nhau: - AI phục vụ khách hàng mua
sắm, hỏi dịch vụ, đặt lịch. - AI phục vụ chủ cửa hàng, nhân viên và
người quản trị.

Không nên dùng cùng một bố cục cứng cho cả hai.

### 3.1. Customer AI -- Trợ lý khách hàng

Ưu tiên: - Hội thoại đơn giản. - Gợi ý dịch vụ. - Tìm cửa hàng. - Đặt
hàng, đặt lịch. - Hỗ trợ khách hàng.

Không cần sidebar phức tạp hay Agent selector.

### 3.2. Business AI -- Trợ lý chủ quán

Ưu tiên: - Báo cáo kinh doanh. - Quản lý đơn hàng. - Quản lý khách
hàng. - Phân tích dữ liệu. - Thực thi nghiệp vụ. - Kết nối các công cụ
Vạn An.

Hai chế độ dùng chung AI Core, Conversation Engine, Design System và các
component. Chỉ khác Context, quyền truy cập và cách trình bày.

## 4. Những gì nên giữ, sửa và loại bỏ

  Thành phần hiện tại            Quyết định
  ------------------------------ ------------------------
  Shop AI                        Giữ, đổi nhận diện
  Trợ lý AI / Chủ quán           Giữ, thiết kế lại
  Câu hỏi gợi ý                  Giữ, nâng cấp
  Bong bóng hội thoại            Giữ, làm mới
  Thẻ sản phẩm/dịch vụ           Giữ
  Nút thêm giỏ hàng              Giữ logic
  Ô nhập liệu                    Thiết kế lại
  Nút loa                        Đánh giá lại chức năng
  Nút microphone                 Giữ và nâng cấp
  Khung trắng cố định            Loại bỏ cấu trúc cũ
  Banner nền phía sau            Thu gọn khi mở AI
  Khoảng trống không cần thiết   Loại bỏ

**Không được xóa các tính năng hiện hữu rồi thay bằng một mockup đẹp
nhưng không hoạt động.**

## 5. Các thông số UX đề xuất

Đây là các mục tiêu thiết kế ban đầu, cần kiểm tra trên kích thước màn
hình thực tế.

  Thành phần                 Desktop          Mobile
  --------------------- ------------ ---------------
  Header                    52--60px        48--56px
  Sidebar                 220--250px          Drawer
  Chat area                Linh hoạt   Toàn màn hình
  Input area               76--100px        68--90px
  Message max width              85%             94%
  Card radius               12--16px        12--14px
  Floating window         420--520px    Bottom sheet
  Conversation scroll        Độc lập         Độc lập

### Quy tắc responsive

-   Không cố định chiều cao khung chat theo một con số.
-   Dùng `100dvh` và xử lý an toàn với bàn phím mobile.
-   Tự động cuộn đến tin nhắn mới, nhưng không ép người dùng đang đọc
    tin cũ phải cuộn xuống.
-   Không để thanh nhập liệu che mất nội dung cuối cùng.

## 6. Cách triển khai đề xuất cho Devin

Không giao yêu cầu chung chung kiểu "Make the UI more beautiful".

Yêu cầu Devin làm theo bốn bước: 1. Audit code hiện tại, xác định chính
xác cấu trúc component, CSS và logic. 2. Đề xuất lại layout
desktop/mobile, kèm bảng Existing / Modify / Add / Remove. 3. Xây dựng
prototype UI mới trước, kiểm tra bằng Playwright trên các kích thước màn
hình. 4. Sau khi duyệt UI mới mới tích hợp lại toàn bộ nghiệp vụ hiện
tại.

## 7. Các quyết định UX cần chốt

### A. Khi người dùng mở Shop AI, hiển thị kiểu nào?

-   **Full-page:** AI chiếm toàn bộ không gian trang. Đây là phương án
    được khuyến nghị.
-   **Side panel:** AI mở từ bên phải, vẫn nhìn thấy trang cửa hàng.
-   **Floating:** AI nổi trên trang như cửa sổ chat.

### B. Có cần sidebar lịch sử hội thoại không?

-   **Adaptive:** chỉ hiện khi cần, tùy vai trò. Đây là phương án được
    khuyến nghị.
-   **Always:** luôn hiển thị trên desktop.
-   **Never:** không cần sidebar.

### C. Phân biệt AI khách hàng và AI chủ quán thế nào?

-   **Dual mode:** hai chế độ Customer AI / Business AI, dùng chung nền
    tảng. Đây là phương án được khuyến nghị.
-   **Single mode:** một giao diện, tự thay đổi theo tài khoản đăng
    nhập.

## 8. Kết luận và khuyến nghị

Không cần đập đi xây lại toàn bộ hệ thống. Nhưng nên mạnh dạn bỏ cấu
trúc hộp chat hiện tại.

Vạn An cần chuyển từ tư duy **nhúng chatbot vào trang bán hàng** sang
**thiết kế một không gian AI phù hợp với từng vai trò người dùng**.

### Quyết định kiến trúc quan trọng

-   Dùng chung AI Core, Conversation Engine và Design System.
-   Customer AI và Business AI có bố cục, ngữ cảnh và quyền truy cập phù
    hợp với từng nhóm người dùng.
-   Giữ nguyên các chức năng nghiệp vụ đang hoạt động.
-   Redesign phần trình bày, bố cục, tương tác và responsive.
-   Không thay thế chức năng thật bằng dữ liệu giả hoặc prototype tĩnh.

Đây là thay đổi đáng đầu tư nhất đối với giao diện Shop AI hiện tại.
