# **ĐẶC TẢ YÊU CẦU NGHIỆP VỤ VÀ KỸ THUẬT (SRS)**

## **HỆ THỐNG QUẢN LÝ VÀ XOAY VÒNG KHÓA TỰ ĐỘNG \- VAN AN SOLUTION (VA-LKR)**

**Ngày cập nhật:** 23/06/2026  
**Trạng thái:** Sẵn sàng cho AI Code Generation (Vibe Coding / Agentic Workflow)  
**Line Spacing:** 1.25

## ---

**1\. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)**

### **1.1. Mục tiêu bài toán**

Xây dựng cơ chế xác thực đầu cuối nhằm chặn đứng triệt để các hình thức tấn công giả mạo request (Data Tampering) và tấn công phát lại (Replay Attack) vào các API Endpoint của hệ sinh thái Vạn An Solution. Cơ chế này phải đảm bảo an toàn ngay cả khi kẻ tấn công dịch ngược (Reverse Engineering) ứng dụng client, biết rõ cấu trúc JSON payload và địa chỉ endpoint.

### **1.2. Nguyên lý cốt lõi**

* Không thực hiện mã hóa toàn bộ payload (gây overload CPU). Thay vào đó, áp dụng **Chữ ký số (Digital Signature)** dựa trên mật mã bất đối xứng (Asymmetric Cryptography).  
* Client (Mobile App/POS/Edge Node) nắm giữ **Private Key** (Tuyệt mật trong phần cứng).  
* Server (Gateway Vạn An) nắm giữ **Public Key** tương ứng để xác thực.  
* Sử dụng giao thức gối đầu tinh gọn **VA-LKR (Van An Lightweight Key Rotation)** để tự động đổi khóa mà không phát sinh thêm RTT (Round Trip Time) và tối ưu băng thông đường truyền.

## ---

**2\. ĐẶC TẢ NGHIỆP VỤ VÀ LUỒNG ĐIỀU HƯỚNG (BUSINESS & WORKFLOW REQUIREMENTS)**

### **2.1. Luồng Khởi tạo và Đăng ký thiết bị (Device Enrollment)**

Thực hiện một lần duy nhất khi ứng dụng Client được kích hoạt hoặc cài đặt đầu tiên.

1. Client tự sinh cặp khóa (Public Key, Private Key) bằng thư viện phần cứng an toàn. **Tuyệt đối không sinh khóa tập trung tại Server rồi gửi về mạng.**  
2. Client gửi gói tin chứa Device\_ID, thông tin định danh và Public Key lên Gateway thông qua kênh bảo mật **HTTPS (TLS 1.3) \+ SSL Pinning**.  
3. Server xác thực danh tính thiết bị hợp lệ, lưu trữ Public Key vào Database và đồng bộ lên bộ nhớ đệm **Redis Cache** để phục vụ xác thực tốc độ cao.

### **2.2. Luồng Xoay vòng khóa gối đầu định kỳ (Piggybacked Key Rotation)**

Để tối ưu băng thông, luồng đổi khóa không gọi một API độc lập mà được *gối đầu (Piggybacked)* trực tiếp vào request nghiệp vụ thông thường khi đến chu kỳ đổi khóa (ví dụ: sau 24h hoặc sau 10,000 requests).

| Bước | Thành phần thực hiện | Mô tả chi tiết hành động nghiệp vụ   |
| :---- | :---- | :---- |
| 1 | **Client** | Sinh cặp khóa mới: (Priv\_New, Pub\_New). Đóng gói request nghiệp vụ, đính kèm Pub\_New vào HTTP Header. Thực hiện **Ký kép (Dual-Signing)** bằng khóa cũ (Priv\_Old) để chứng minh chính chủ. Gửi request đi. |
| 2 | **Server Gateway** | Lấy Pub\_Old từ Redis, xác thực chữ ký cũ. Nếu hợp lệ, chuyển tiếp Payload vào tầng nghiệp vụ xử lý. Đồng thời, lưu Pub\_New vào Redis dưới trạng thái PENDING\_VERIFICATION. Giữ lại Pub\_Old để dự phòng rớt gói tin. |
| 3 | **Server Response** | Tạo gói tin Response nghiệp vụ trả về cho Client, ký xác nhận (ACK) bằng Private Key của Server hoặc Token mã hóa bằng Pub\_New. |
| 4 | **Client xác nhận** | Nhận Response, kiểm tra ACK hợp lệ. Chính thức xóa bỏ hoàn toàn Priv\_Old trên thiết bị, đưa Priv\_New thành khóa hoạt động duy nhất. Các request sau sẽ ký bằng khóa mới. |
| 5 | **Server đóng luồng** | Khi nhận được request tiếp theo được ký bằng khóa mới hợp lệ, Server chuyển trạng thái Pub\_New thành ACTIVE và xóa bỏ vĩnh viễn khóa cũ khỏi hệ thống. |

## ---

**3\. ĐẶC TẢ KỸ THUẬT CHI TIẾT (TECHNICAL SPECIFICATIONS)**

### **3.1. Thuật toán và Định dạng dữ liệu**

* **Thuật toán khuyến nghị:** Ed25519 (EdDSA) hoặc ECDSA secp256r1 (NIST P-256). Ưu tiên Ed25519 vì kích thước khóa nhỏ (32 bytes), chữ ký nhỏ (64 bytes) và tốc độ tính toán phần cứng vượt trội.  
* **Nén khóa (Public Key Compression):** Client phải nén Public Key thành dạng Hex/Binary chỉ truyền tọa độ X kết hợp 1 byte chỉ thị chẵn/lẻ của Y nhằm giảm 50% dung lượng truyền tải qua mạng (Đạt chuẩn tối ưu 33 bytes đối với ECDSA).

### **3.2. Cấu trúc HTTP Request Header bổ sung**

Mọi request gửi lên Gateway bắt buộc phải đính kèm các trường thông tin sau trong Custom HTTP Headers:

* X-VanAn-Device-ID: Chuỗi định danh duy nhất của thiết bị đầu cuối.  
* X-VanAn-Timestamp: Thời gian Client gửi request (Định dạng Epoch Unix Timestamp tính bằng giây).  
* X-VanAn-Nonce: Chuỗi ký tự ngẫu nhiên, sử dụng một lần duy nhất (Unique per request).  
* X-VanAn-Signature: Chữ ký số sinh ra từ việc ký chuỗi băm của payload và các thông số metadata.  
* X-VanAn-New-Pub: *(Tùy chọn)* Chỉ xuất hiện khi Client kích hoạt trạng thái gối đầu đổi khóa định kỳ. Chứa Public Key mới dạng nén Hex.

### **3.3. Quy trình tính toán chữ ký số tại Client**

Chuỗi\_Ký \= To\_String(HTTP\_Method) \+ "\\n" \+  
           Request\_Path \+ "\\n" \+  
           X-VanAn-Device-ID \+ "\\n" \+  
           X-VanAn-Timestamp \+ "\\n" \+  
           X-VanAn-Nonce \+ "\\n" \+  
           (X-VanAn-New-Pub nếu có) \+ "\\n" \+  
           SHA256(JSON\_Body\_Raw);

Signature \= Ed25519\_Sign(Private\_Key\_Client, Chuỗi\_Ký);

### **3.4. Quy trình xác thực logic tại Server Gateway (Middleware Layer)**

1. **Kiểm tra điều kiện biên thời gian (Timestamp Validation):**  
   Nếu Abs(Server\_Current\_Time \- X-VanAn-Timestamp) \> 60 giây \-\> REJECT (Chống Replay Attack lệch giờ)  
2. **Kiểm tra trùng lặp (Nonce Validation):**  
   Kiểm tra sự tồn tại của cặp (Device\_ID, Nonce) trong Redis Cache.  
   Nếu tồn tại \-\> REJECT (Chống Replay Attack lặp lại gói tin).  
   Nếu không \-\> Lưu cặp này vào Redis với TTL \= 60 giây.  
3. **Xác thực chữ ký toán học:**  
   Lấy Public Key của Client từ Redis Cache tương ứng với Device\_ID.  
   Tự dựng lại "Chuỗi\_Ký" theo quy tắc chuẩn ở mục 3.3 từ dữ liệu nhận được.  
   Thực hiện hàm Ed25519\_Verify(Public\_Key, Chuỗi\_Ký, X-VanAn-Signature).  
   Nếu kết quả \= False \-\> REJECT (Dữ liệu bị sửa đổi hoặc giả mạo chữ ký).

## ---

**4\. KIẾN TRÚC XỬ LÝ SỰ CỐ VÀ ĐIỀU KIỆN BIÊN (FAULT TOLERANCE & EDGE CASES)**

### **4.1. Sự cố mất đồng bộ khóa (Desynchronization do mất mạng)**

**Kịch bản sự cố:** Client gửi yêu cầu đổi khóa kèm X-VanAn-New-Pub. Server xác thực thành công, lưu khóa mới ở trạng thái tạm thời và gửi Response về. Gói tin Response bị drop giữa đường do rớt mạng 4G. Client không nhận được Response nên vẫn giữ cặp khóa cũ. Request kế tiếp Client ký bằng khóa cũ, nhưng Server cấu hình chỉ nhận khóa mới.  
**Giải pháp: Cơ chế Cửa sổ trượt song song 2 khóa (Sliding Window of Keys)**

* Khi Server nhận được yêu cầu đổi khóa hợp lệ, Server kích hoạt bộ đếm thời gian hiệu lực song song (Grace Period) là **5 phút**.  
* Trong vòng 5 phút này, cấu trúc Redis lưu trữ của Device\_ID sẽ chứa cả 2 khóa: Active\_Public\_Key\_Old và Pending\_Public\_Key\_New.  
* Hệ thống Gateway chấp nhận request hợp lệ ký bằng **bất kỳ khóa nào trong 2 khóa trên**.  
* Nếu Client gửi request ký bằng khóa cũ: Server xử lý nghiệp vụ bình thường và tiếp tục gửi lại gói tin Response kèm tín hiệu ép buộc đổi khóa (ACK Retry).  
* Nếu Client gửi request ký bằng khóa mới: Server lập tức đóng cửa sổ trượt, xóa vĩnh viễn khóa cũ, nâng cấp khóa mới lên trạng thái duy nhất hoạt động (ACTIVE).

### **4.2. Cơ chế Thu hồi khóa khẩn cấp (Key Revocation)**

* Khi thiết bị bị báo mất hoặc phát hiện hành vi bạo lực hạ tầng (Brute-force / Spam request sai chữ ký \> 5 lần liên tục), hệ thống lập tức gọi hàm Hủy khóa.  
* Xóa bỏ ngay thông tin của Device\_ID này trong Redis Cache và cập nhật trạng thái trong Database trường Is\_Active \= false. Mọi request tiếp sau sẽ bị chặn đứng tại Layer Middleware Gateway với mã lỗi 401 Unauthorized (Device Blocked).

## ---

**5\. HƯỚNG DẪN CÀI ĐẶT CHO AI CODE GENERATION (PROMPTS GUIDE)**

Khi đưa tài liệu này vào AI để sinh mã nguồn, hãy áp dụng chỉ thị (Prompt) cấu trúc dưới đây:  
*"Hãy đóng vai là Kiến trúc sư hệ thống và Chuyên gia bảo mật cao cấp .NET 8\. Dựa trên tài liệu đặc tả SRS của Vạn An Solution bên trên, hãy viết một **Custom Middleware** bằng C\# .NET 8 Web API thực hiện việc xác thực chữ ký số đầu vào. Sử dụng thư viện mã hóa Ed25519 (NSec.Cryptography hoặc tương đương). Middleware phải kết nối với IDistributedCache (Redis) để thực hiện kiểm tra cấu trúc Nonce (chống Replay Attack) và xử lý cơ chế Cửa sổ trượt gối đầu 2 khóa (Sliding Window) trong vòng 5 phút khi có Header X-VanAn-New-Pub. Viết mã nguồn tối ưu, không block luồng (sử dụng async/await hoàn toàn), xử lý ngoại lệ chặt chẽ và ghi log đầy đủ."*