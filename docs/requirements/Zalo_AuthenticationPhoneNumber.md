Mô hình **Xác thực ngược qua Zalo OA (User-Initiated / MO-OTP)** là một "tuyệt chiêu" lách chi phí cực kỳ thông minh trong hệ sinh thái Zalo Platform tại Việt Nam.

Cơ chế này dựa trên quy tắc kinh doanh của Zalo: **Mọi tin nhắn do Người dùng chủ động gửi tới Zalo OA (User-Initiated Message) đều HOÀN TOÀN MIỄN PHÍ và kích hoạt "Cửa sổ tương tác 48 giờ" (48-hour Interaction Window).**

Dưới đây là thiết kế chi tiết luồng kỹ thuật (Technical Architecture & Flow) để áp dụng cho **KhachLink \<---\> Gateway PG**:

## **1\. Kiến trúc luồng xác thực (Step-by-Step Sequence)**

\[ KhachLink (Client) \]        \[ Gateway PG \]          \[ Zalo Webhook \]        \[ Zalo OA / User \]  
         │                          │                        │                        │  
         │ 1\. Bấm "Xác thực SĐT"     │                        │                        │  
         ├─────────────────────────►│                        │                        │  
         │                          │ 2\. Gen Session & Code  │                        │  
         │ 3\. Trả về Deep Link/QR  │    (ví dụ: "VA 8923")   │                        │  
         │◄─────────────────────────┤                        │                        │  
         │                          │                        │                        │  
         │ 4\. Click Deep Link/Mở OA ─────────────────────────────────────────────────►│  
         │                                                                            │ 5\. Khách bấm Gửi "VA 8923"  
         │                                                                            ├────────────────────────►  
         │                          │                        │ 6\. Event user\_send\_text │  
         │                          │◄───────────────────────┴────────────────────────┤  
         │                          │ 7\. Verify OTP \+ Get Zalo User ID/SĐT            │  
         │ 8\. Polling/WebSocket Check│                                                │  
         │◄─────────────────────────┤                                                │  
         │    (Xác thực thành công\!)│                                                │

## **2\. Chi tiết các bước triển khai kỹ thuật**

### **Bước 1: Khởi tạo Yêu cầu Xác thực (KhachLink)**

* Người dùng trên KhachLink bấm nút **"Xác thực tài khoản qua Zalo"**.  
* KhachLink gọi API về Gateway PG: `POST /api/v1/auth/zalo-mo/init`.

### **Bước 2: Sinh mã OTP ngẫu nhiên & Deep Link (Gateway PG)**

Gateway sinh ra một mã xác thực độc nhất đi kèm thời gian hết hạn (ví dụ: 3 phút):

* **Verification Code:** `VA` \+ `4 chữ số ngẫu nhiên` (Ví dụ: `VA 8923`).  
* Gateway lưu Cache (Redis/Memory) cặp Key-Value: `Key: VA_8923` ➔ `Value: { SessionId, Status: "PENDING", ExpiredAt: ... }`.  
* Gateway tạo ra một đường dẫn **Zalo Deep Link**:  
  * Dạng URL: `[https://zalo.me/](https://zalo.me/)<ZALO_OA_ID>?text=VA%208923`  
  * Khi người dùng bấm vào đường dẫn này trên điện thoại, ứng dụng Zalo sẽ tự động mở ra, vào thẳng khung Chat của Zalo OA và **điền sẵn chuỗi "VA 8923" vào ô nhập liệu**.

### **Bước 3: Người dùng gửi tin nhắn (Tương tác trên Zalo)**

* Khách hàng chỉ cần bấm nút **Gửi (Send)** trên ứng dụng Zalo.  
* Vì hành động này do **Khách chủ động bấm**, bạn **không tốn 1 đồng phí ZNS nào**.

### **Bước 4: Xử lý Webhook (Zalo Server ➔ Gateway PG)**

* Zalo Server lập tức bắn một `Event: user_send_text` về URL Webhook đã đăng ký của Gateway PG.  
* **Payload Zalo gửi về Webhook:**  
* JSON

{  
  "app\_id": "YOUR\_ZALO\_APP\_ID",  
  "sender": {  
    "id": "ZALO\_USER\_ID\_123456"  
  },  
  "recipient": {  
    "id": "YOUR\_OA\_ID"  
  },  
  "event\_name": "user\_send\_text",  
  "message": {  
    "text": "VA 8923",  
    "msg\_id": "msg\_987654"  
  },  
  "timestamp": 1700000000  
}

*   
* 

### **Bước 5: Verify & Lấy Số điện thoại (Gateway PG)**

1. Gateway nhận Webhook, đọc `message.text` ➔ Thấy chuỗi `VA 8923`.  
2. Kiểm tra trong Redis: Mã `VA 8923` hợp lệ và chưa hết hạn.  
3. **Truy xuất SĐT / Identity:**  
   * Vì người dùng vừa gửi tin nhắn vào OA, cửa sổ tương tác 48h được mở.  
   * Gateway gọi API của Zalo: `GET [https://openapi.zalo.me/v2.0/oa/getprofile?data=](https://openapi.zalo.me/v2.0/oa/getprofile?data=){"user_id":"ZALO_USER_ID_123456"}`.  
   * Zalo trả về thông tin Profile (Họ tên, Avatar, và **Số điện thoại** nếu người dùng đã cấp quyền cấp SĐT cho OA/App).  
4. Gateway cập nhật trạng thái Redis: `Key: VA_8923` ➔ `Status: "SUCCESS", Phone: "090xxxxxxx"`.

### **Bước 6: Phản hồi về Client (KhachLink)**

* KhachLink (đang chờ ở màn hình) dùng cơ chế **Long-Polling** hoặc **WebSocket/SignalR** lắng nghe trạng thái của `SessionId`.  
* Nhận được trạng thái `"SUCCESS"`, KhachLink tự động chuyển màn hình sang: *"Xác thực thành công\! Xin chào \[Tên Khách\]"*.

## **3\. So sánh Đánh giá Đạt/Mất (Trade-offs)**

| Tiêu chí | ZNS Send OTP (Kiểu cũ) | Zalo MO-OTP (Cách này) |
| :---- | :---- | :---- |
| **Chi phí** | 🔴 200đ \- 300đ / lần gửi | 🟢 **0 VNĐ (Miễn phí 100%)** |
| **Tỷ lệ thành công** | 🟢 Rất cao | 🟢 Rất cao (Khách có sẵn Zalo) |
| **UX / Trải nghiệm** | Khách chờ SMS ➔ Tự gõ mã 6 số | Khách click Link ➔ Bấm "Gửi" trên Zalo |
| **Bảo mật (Chống Spam)** | Dễ bị Bot Spam yêu cầu gửi OTP làm cạn tiền | Anti-spam tuyệt đối (Bot không thể tự bấm gửi trên Zalo của user) |
| **Độ phức tạp Dev** | Đơn giản (Gọi 1 API gửi ZNS) | Cần triển khai Webhook Server \+ Deep Link |

## **4\. Bẫy kỹ thuật cần lưu ý (Dành cho Dev / Devin)**

1. **Rào cản SĐT riêng tư trên Zalo:**  
   * Mặc định, `getprofile` API của Zalo OA sẽ trả về `Zalo User ID`, Name, Avatar.  
   * Để lấy được **Số điện thoại thực tế** của khách từ Zalo User ID, ứng dụng Zalo App của bạn cần đăng ký quyền **Access User Phone Number** và xin cấp duyệt (Granting Scope) từ người dùng.  
2. **Cơ chế Fallback khi mở trên Desktop Chrome:**  
   * Nếu người dùng lướt KhachLink trên máy tính (Desktop Browser), bấm Deep Link `zalo.me` sẽ mở Zalo Web hoặc ứng dụng Zalo PC.  
   * **Giải pháp tốt hơn cho Desktop:** Hiển thị một **mã QR Code** chứa đường dẫn Deep Link. Khách dùng camera điện thoại quét QR ➔ Mở Zalo ➔ Bấm Gửi.

