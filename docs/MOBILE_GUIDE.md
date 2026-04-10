# 📱 Van An Ecosystem - Mobile Guide

> **Hướng dẫn sử dụng Mobile App cho nhân viên Vạn An**
> 
> Version: 2.1 | Last Updated: 01/04/2026

---

## 📑 MỤC LỤC

1. [Giới thiệu Mobile App](#chương-1-giới-thiệu-mobile-app)
2. [Cài đặt và Đăng nhập](#chương-2-cài-đặt-và-đăng-nhập)
3. [Giao diện chính](#chương-3-giao-diện-chính)
4. [Quản lý đơn hàng](#chương-4-quản-lý-đơn-hàng)
5. [Thanh toán và VietQR](#chương-5-thanh-toán-và-vietqr)
6. [Voice Commands](#chương-6-voice-commands)
7. [Thông báo và Cảnh báo](#chương-7-thông-báo-và-cảnh-báo)
8. [Cài đặt và Tùy chỉnh](#chương-8-cài-đặt-và-tùy-chỉnh)

---

## 📱 CHƯƠNG 1: GIỚI THIỆU MOBILE APP

### 1.1 Tổng quan

**Van An Mobile App** là ứng dụng di động dành cho nhân viên, giúp quản lý đơn hàng, thanh toán, và vận hành quán trà sữa một cách hiệu quả và chuyên nghiệp.

**Tính năng chính:**
- **📱 Quản lý đơn hàng**: Nhận và xử lý đơn hàng real-time
- **💳 Thanh toán VietQR**: Tạo và xác nhận mã QR thanh toán
- **🎤 Voice Commands**: Điều khiển bằng giọng nói (Tiếng Việt/English)
- **📊 Dashboard**: Xem thống kê doanh thu và hiệu suất
- **🔔 Thông báo**: Nhận alert về đơn hàng và cập nhật hệ thống
- **👥 Nhân sự**: Xem lịch làm việc và chấm công

### 1.2 Yêu cầu hệ thống

**iOS:**
- **Phiên bản**: iOS 14.0 trở lên
- **Thiết bị**: iPhone 8 trở lên
- **Dung lượng**: 50MB
- **Kết nối**: WiFi hoặc 4G/5G

**Android:**
- **Phiên bản**: Android 8.0 (API 26) trở lên
- **Thiết bị**: RAM 2GB trở lên
- **Dung lượng**: 45MB
- **Kết nối**: WiFi hoặc 4G/5G

### 1.3 Tải và cài đặt

**App Store (iOS):**
1. Mở App Store
2. Tìm kiếm "Van An Staff"
3. Click "Get" → "Install"
4. Chờ cài đặt hoàn tất

**Google Play (Android):**
1. Mở Google Play Store
2. Tìm kiếm "Van An Staff"
3. Click "Install"
4. Chờ cài đặt hoàn tất

**Direct Download:**
1. Truy cập: `https://mobile.vanan.com`
2. Chọn phiên bản phù hợp
3. Tải và cài đặt APK (Android)

---

## 🔐 CHƯƠNG 2: CÀI ĐẶT VÀ ĐĂNG NHẬP

### 2.1 Lần đầu sử dụng

**Bước 1: Mở ứng dụng**
- Click icon Van An Staff trên màn hình
- Chờ ứng dụng tải xong

**Bước 2: Đăng nhập**
```
┌─────────────────────────────────┐
│        🏪 Vạn An Staff            │
├─────────────────────────────────┤
│  📧 Email                        │
│  ┌─────────────────────────────┐ │
│  │ staff@vanan.com              │ │
│  └─────────────────────────────┘ │
│                                 │
│  🔑 Password                    │
│  ┌─────────────────────────────┐ │
│  │ •••••••••                    │ │
│  └─────────────────────────────┘ │
│                                 │
│  [👁️ Show] [🔒 Login]           │
└─────────────────────────────────┘
```

**Bước 3: Xác nhận 2FA**
- Nhận mã 6 số qua SMS/Zalo
- Nhập mã xác nhận
- Click "Xác nhận"

### 2.2 Quên mật khẩu

**Khôi phục mật khẩu:**
1. Click "Quên mật khẩu?"
2. Nhập email đăng ký
3. Nhận link reset qua email
4. Tạo mật khẩu mới
5. Đăng nhập lại

### 2.3 Cài đặt bảo mật

**Face ID / Touch ID:**
- Settings → Security → Biometric Login
- Kích hoạt Face ID/Touch ID
- Đăng nhập nhanh bằng vân tay/khuôn mặt

**PIN Code:**
- Settings → Security → PIN Code
- Tạo PIN 6 số
- Dùng cho quick login

---

## 🏠 CHƯƠNG 3: GIAO DIỆN CHÍNH

### 3.1 Dashboard

**Layout chính:**
```
┌─────────────────────────────────────────────────────────────┐
│  👤 Staff Name    🔔 3    ⚙️ Settings    📱 +84-XXX-XXX-XXXX   │
├─────────────────────────────────────────────────────────────┤
│  💰 Doanh thu hôm nay      📈 Xu hướng      ⏰ Giờ làm việc   │
│     2,450,000 VNĐ         ↗ +12%        7:30 - 16:30      │
├─────────────────────────────────────────────────────────────┤
│  📦 Đơn hàng chờ xử lý    🎯 Hiệu suất    👥 Đánh giá       │
│     5 đơn               95%          4.8/5.0           │
├─────────────────────────────────────────────────────────────┤
│  📱 Quick Actions                                               │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐             │
│  │ 📦 New  │ │ 🎤 Voice│ │ 💳 QR    │ │ 📊 Report│             │
│  │ Order  │ │ Command│ │ Payment │ │         │             │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘             │
├─────────────────────────────────────────────────────────────┤
│  📋 Đơn hàng gần đây                                          │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ #1234 | Trà sữa L | 45k | ⏱️ 5 phút | 🏠 Bàn 5          │ │
│  │ #1235 | Cà phê M | 35k | ⏱️ 2 phút | 🚗 Delivery       │ │
│  │ #1236 | Trà sữa S | 50k | ⏱️ 1 phút | 🏠 Bàn 3          │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Bottom Navigation

**5 tab chính:**
- **🏠 Home**: Dashboard và quick actions
- **📦 Orders**: Quản lý đơn hàng
- **💳 Payment**: Thanh toán và VietQR
- **🎤 Voice**: Voice commands
- **👤 Profile**: Thông tin cá nhân và cài đặt

### 3.3 Gesture Controls

**Swipe actions:**
- **Swipe left**: Hoàn thành đơn hàng
- **Swipe right**: Xem chi tiết đơn hàng
- **Swipe down**: Refresh danh sách
- **Long press**: Menu context

**Quick gestures:**
- **Double tap**: Mở nhanh đơn hàng mới
- **Pinch zoom**: Zoom in/out trên dashboard
- **Shake device**: Voice command activation

---

## 📦 CHƯƠNG 4: QUẢN LÝ ĐƠN HÀNG

### 4.1 Nhận đơn hàng mới

**Notification khi có đơn mới:**
```
┌─────────────────────────────────────────┐
│         📢 ĐƠN HÀNG MỚI!              │
├─────────────────────────────────────────┤
│  🧋 Trà sữa size L                     │
│  💰 45,000 VNĐ                        │
│  🏠 Bàn 5                              │
│  ⏰ 2 phút trước                        │
│                                     │
│  [📱 Xem] [✅ Nhận] [❌ Từ chối]        │
└─────────────────────────────────────────┘
```

**Chi tiết đơn hàng:**
```
┌─────────────────────────────────────────────────────────────┐
│                    📦 CHI TIẾT ĐƠN HÀNG #1234              │
├─────────────────────────────────────────────────────────────┤
│  🧋 Sản phẩm: Trà sữa size L                                    │
│  🍬 Topping: Trân châu, thạch                                  │
│  🍯 Độ ngọt: 70%                                               │
│  🧊 Đá: Ít đá                                                   │
│  💰 Giá: 45,000 VNĐ                                             │
│  🏠 Bàn: 5                                                      │
│  ⏰ Thời gian: 14:30                                            │
│  👤 Khách: Nguyễn Văn A                                         │
│  📱 SĐT: 0912-345-678                                          │
├─────────────────────────────────────────────────────────────┤
│  📝 Ghi chú: Ít đá, thêm nhiều trân châu                        │
├─────────────────────────────────────────────────────────────┤
│  [🎤 Voice] [💳 QR] [✅ Hoàn thành] [❌ Hủy]                  │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Xử lý đơn hàng

**Workflow xử lý:**
1. **Nhận đơn**: Click "Nhận" hoặc swipe right
2. **Xác nhận**: Kiểm tra chi tiết đơn hàng
3. **Chuẩn bị**: Báo cho barista làm đồ
4. **Hoàn thành**: Click "Hoàn thành" khi xong
5. **Thông báo**: Khách hàng nhận thông báo

**Trạng thái đơn hàng:**
- **⏳ Đang chờ**: Mới nhận, chưa xử lý
- **👨‍🍳 Đang làm**: Barista đang chuẩn bị
- **🚀 Giao hàng**: Đang giao/deliver
- **✅ Hoàn thành**: Khách đã nhận
- **❌ Đã hủy**: Bị hủy bởi staff/khách

### 4.3 Bulk Actions

**Xử lý nhiều đơn:**
1. Chọn multiple orders
2. Click "Bulk Action"
3. Chọn action: "Start Prep", "Complete", "Cancel"
4. Xác nhận action

**Quick actions:**
- **Start All**: Bắt đầu tất cả đơn chờ
- **Complete All**: Hoàn thành tất cả đơn đã làm
- **Cancel Selected**: Hủy đơn đã chọn

---

## 💳 CHƯƠNG 5: THANH TOÁN VÀ VIETQR

### 5.1 Tạo mã QR VietQR

**Tạo QR cho đơn hàng:**
1. Mở đơn hàng cần thanh toán
2. Click "💳 Tạo QR"
3. Xem mã QR trên màn hình
4. Khách hàng quét mã bằng app ngân hàng

**QR Code Display:**
```
┌─────────────────────────────────────────┐
│         💳 THANH TOÁN VIETQR           │
├─────────────────────────────────────────┤
│  ┌─────────────────────────────────┐   │
│  │                                 │   │
│  │         ████ ████ ████           │   │
│  │         █  █ █  █ █  █           │   │
│  │         ████ ████ ████           │   │
│  │         █  █    █ █  █           │   │
│  │         ████ ████ ████           │   │
│  │                                 │   │
│  │     Van An Tra Sua              │   │
│  │       45,000 VNĐ                │   │
│  └─────────────────────────────────┘   │
│                                     │
│  📱 Quét mã QR để thanh toán          │
│  ⏰ Mã QR hiệu lực: 5 phút            │
│  🔄 Tự động làm mới sau 5 phút       │
│                                     │
│  [🔄 Làm mới] [📋 Chi tiết]          │
└─────────────────────────────────────────┘
```

### 5.2 Xác nhận thanh toán

**Tự động xác nhận:**
- App tự động kiểm tra trạng thái thanh toán
- Cập nhật trạng thái đơn hàng khi thanh toán thành công
- Gửi thông báo cho cả staff và khách hàng

**Manual xác nhận:**
1. Kiểm tra app ngân hàng của quán
2. Click "✅ Xác nhận thanh toán"
3. Nhập mã giao dịch (nếu cần)
4. Cập nhật trạng thái đơn hàng

### 5.3 Lịch sử thanh toán

**View payment history:**
- **Tab Payment**: "Lịch sử thanh toán"
- **Filter**: Theo ngày, theo trạng thái
- **Details**: Click vào payment để xem chi tiết

**Payment details:**
- **Mã giao dịch**: Transaction ID
- **Thời gian**: Payment timestamp
- **Số tiền**: Amount paid
- **Phương thức**: Payment method
- **Trạng thái**: Success/Failed/Pending

---

## 🎤 CHƯƠNG 6: VOICE COMMANDS

### 6.1 Kích hoạt Voice Commands

**Cách 1: Nút micro**
1. Click nút 🎤 trên màn hình
2. Chờ "Listening..." xuất hiện
3. Nói lệnh rõ ràng
4. Chờ hệ thống xử lý

**Cách 2: Shake device**
1. Lắc nhẹ điện thoại
2. Voice command tự động kích hoạt
3. Nói lệnh
4. Xác nhận kết quả

**Cách 3: Wake word**
1. Nói "Hey Van An"
2. Chờ beep sound
3. Nói lệnh
4. Xử lý lệnh

### 6.2 Các lệnh cơ bản

**Lệnh tiếng Việt:**
| Lệnh | Mô tả | Ví dụ |
|------|-------|-------|
| **Đơn mới** | Tạo đơn hàng mới | "Đơn mới" |
| **Xong đơn [số]** | Hoàn thành đơn | "Xong đơn 1234" |
| **Kiểm tra đơn** | Xem đơn hàng | "Kiểm tra đơn" |
| **Thanh toán QR** | Tạo mã QR | "Thanh toán QR" |
| **Báo cáo** | Xem báo cáo | "Báo cáo hôm nay" |
| **Trợ giúp** | Hiển thị help | "Trợ giúp" |

**Lệnh tiếng Anh:**
| Command | Description | Example |
|----------|-------------|---------|
| **New order** | Create new order | "New order" |
| **Complete order [number]** | Complete order | "Complete order 1234" |
| **Check orders** | View orders | "Check orders" |
| **QR payment** | Create QR code | "QR payment" |
| **Report** | View reports | "Today's report" |
| **Help** | Show help | "Help" |

### 6.3 Lệnh nâng cao

**Quản lý nhân sự:**
- "Lịch làm việc hôm nay"
- "Chấm công"
- "Báo cáo hiệu suất"

**Marketing:**
- "Khởi động khuyến mãi"
- "Kiểm tra social media"
- "Gửi thông báo"

**Báo cáo:**
- "Doanh thu tuần này"
- "Sản phẩm bán chạy"
- "Khách hàng VIP"

### 6.4 Tips sử dụng Voice

**🎯 Best Practices:**
- Nói rõ ràng, tốc độ vừa phải
- Môi trường yên tĩnh để tăng độ chính xác
- Sử dụng từ khóa chính trong lệnh
- Chờ hệ thống xác nhận trước khi lệnh tiếp theo

**⚡ Accuracy Tips:**
- Giữ điện thoại cách miệng 20-30cm
- Tránh nhạc nền ồn ào
- Phát âm chuẩn tiếng Việt/English
- Sử dụng headset cho chất lượng tốt nhất

---

## 🔔 CHƯƠNG 7: THÔNG BÁO VÀ CẢNH BÁO

### 7.1 Loại thông báo

**🔔 Push Notifications:**
- **Đơn hàng mới**: Khi có đơn mới
- **Thanh toán**: Khi khách thanh toán
- **Cập nhật**: Trạng thái đơn hàng thay đổi
- **Hệ thống**: Bảo trì, cập nhật

**📱 In-app Notifications:**
- **Task reminders**: Nhắc nhắc công việc
- **Performance alerts**: Cảnh báo hiệu suất
- **Schedule updates**: Thay đổi lịch làm việc
- **System alerts**: Cảnh bảo hệ thống

### 7.2 Cấu hình thông báo

**Notification Settings:**
```
┌─────────────────────────────────────────┐
│         🔔 THIẾT LẬP THÔNG BÁO        │
├─────────────────────────────────────────┤
│  ☑️ Đơn hàng mới                        │
│  ☑️ Thanh toán thành công                │
│  ☑️ Cập nhật đơn hàng                    │
│  ☑️ Lịch làm việc                        │
│  ☑️ Hiệu suất cá nhân                    │
│  ☑️ Cảnh báo hệ thống                    │
│  ☑️ Marketing và khuyến mãi              │
│  ☑️ Báo cáo doanh thu                   │
├─────────────────────────────────────────┤
│  🔊 Âm thanh: Bật                      │
│  📳 Rung: Bật                          │
│  🌙 Không làm phiền: Tắt (22:00-7:00)    │
├─────────────────────────────────────────┤
│           [💾 Lưu] [❌ Hủy]             │
└─────────────────────────────────────────┘
```

### 7.3 Priority Notifications

**🚨 Critical Alerts:**
- **System down**: Hệ thống gặp sự cố
- **Payment failed**: Thanh toán thất bại
- **Security alert**: Cảnh báo bảo mật
- **Emergency**: Khẩn cấp

**⚠️ Important Alerts:**
- **High value order**: Đơn hàng giá trị cao
- **VIP customer**: Khách hàng VIP
- **Performance issue**: Vấn đề hiệu suất
- **Schedule change**: Thay đổi lịch

**ℹ️ Informational:**
- **Daily summary**: Tóm tắt ngày
- **Marketing updates**: Cập nhật marketing
- **System updates**: Cập nhật hệ thống
- **News**: Tin tức nội bộ

---

## ⚙️ CHƯƠNG 8: CÀI ĐẶT VÀ TÙY CHỈNH

### 8.1 Profile Settings

**Thông tin cá nhân:**
```
┌─────────────────────────────────────────┐
│            👤 THÔNG TIN CÁ NHÂN         │
├─────────────────────────────────────────┤
│  📷 Avatar                            │
│  ┌─────────────────────────────────┐   │
│  │                                 │   │
│  │        [📷 Chọn ảnh]           │   │
│  └─────────────────────────────────┘   │
│                                     │
│  👤 Họ tên: Nguyễn Văn B               │
│  📧 Email: b.vanan@staff.com          │
│  📱 SĐT: +84-912-345-678             │
│  🏢 Vị trí: Barista                    │
│  🏪 Cửa hàng: Vạn An Trà Sữa           │
│  📅 Ngày bắt đầu: 01/01/2024           │
├─────────────────────────────────────────┤
│        [✏️ Chỉnh sửa] [💾 Lưu]         │
└─────────────────────────────────────────┘
```

**Change Password:**
1. Settings → Profile → Change Password
2. Nhập mật khẩu hiện tại
3. Nhập mật khẩu mới
4. Xác nhận mật khẩu mới
5. Click "Change Password"

### 8.2 App Preferences

**Giao diện:**
- **Theme**: Light/Dark/Auto
- **Font size**: Small/Medium/Large
- **Language**: Tiếng Việt/English
- **Animation**: Bật/Tắt

**Hiệu suất:**
- **Auto-refresh**: 30 giây/1 phút/5 phút
- **Data sync**: WiFi only/WiFi+Mobile
- **Image quality**: High/Medium/Low
- **Cache size**: 50MB/100MB/200MB

**Privacy:**
- **Location**: Always/While using/Never
- **Camera**: Allow/Deny
- **Microphone**: Allow/Deny
- **Contacts**: Allow/Deny

### 8.3 Advanced Settings

**Developer Options:**
```
┌─────────────────────────────────────────┐
│         🔧 DEVELOPER OPTIONS           │
├─────────────────────────────────────────┤
│  🐛 Debug mode: Tắt                    │
│  📊 Performance monitoring: Bật         │
│  📝 Crash reporting: Bật                │
│  🔄 Auto-sync: Bật                      │
│  📡 API endpoint: Production           │
│  🔐 SSL verification: Bật               │
│  📱 Device ID: auto-generated          │
├─────────────────────────────────────────┤
│  🧪 Test features:                     │
│  ☑️ Voice command beta                 │
│  ☑️ New UI experiment                 │
│  ☐ AR ordering mode                   │
│  ☐ AI recommendations                 │
├─────────────────────────────────────────┤
│        [💾 Lưu] [🔄 Reset]             │
└─────────────────────────────────────────┘
```

**Network Settings:**
- **API timeout**: 10/30/60 seconds
- **Retry attempts**: 3/5/10 times
- **Connection timeout**: 5/10/15 seconds
- **Data usage**: Monitor/Limit/Unlimited

---

## 🚀 PERFORMANCE OPTIMIZATION

### 8.4 Battery Optimization

**Tips tiết kiệm pin:**
- **Background refresh**: Tắt khi không cần
- **Push notifications**: Chỉ critical alerts
- **Location services**: Only when using app
- **Auto-sync**: WiFi only

**Battery saving mode:**
- Tự động kích hoạt khi pin < 20%
- Giảm frequency auto-refresh
- Tắt animations
- Lower image quality

### 8.5 Data Management

**Cache Management:**
- **Clear cache**: Settings → Storage → Clear Cache
- **Clear data**: Settings → Storage → Clear Data
- **Backup data**: Settings → Backup & Restore

**Storage usage:**
```
┌─────────────────────────────────────────┐
│            📱 QUẢN LÝ BỘ NHỚ           │
├─────────────────────────────────────────┤
│  💾 Cache: 125 MB                       │
│  📸 Images: 89 MB                        │
│  📊 Database: 45 MB                      │
│  📝 Logs: 12 MB                          │
│  📦 Other: 23 MB                         │
│                                     │
│  Tổng dung lượng: 294 MB                │
│  Trống: 6.8 GB                         │
├─────────────────────────────────────────┤
│  [🗑️ Clear Cache] [📊 Clear Data]       │
└─────────────────────────────────────────┘
```

---

## 📞 SUPPORT & HELP

### 8.6 Troubleshooting

**Common Issues:**
- **App crashes**: Restart app, clear cache
- **Login issues**: Check credentials, reset password
- **Notifications not working**: Check settings
- **Voice commands not working**: Check microphone permissions

**Self-service:**
- **Help center**: In-app help section
- **FAQ**: Frequently asked questions
- **Video tutorials**: Step-by-step guides
- **Community forum**: Staff community

### 8.7 Contact Support

**In-app Support:**
1. Settings → Help & Support
2. Choose issue type
3. Describe problem
4. Attach screenshots (optional)
5. Submit ticket

**Direct Contact:**
- **Hotline**: 1900-XXXX (24/7)
- **Email**: mobile@vanan.com
- **Zalo**: 1900-XXXX
- **Live Chat**: In-app chat

---

## 📊 USAGE STATISTICS

### 8.8 Performance Metrics

**App Performance:**
- **Startup time**: < 3 seconds
- **Order processing**: < 30 seconds
- **QR generation**: < 2 seconds
- **Voice recognition**: < 5 seconds

**User Metrics:**
- **Daily active users**: 85%
- **Average session**: 45 minutes
- **Orders processed/day**: 50+/user
- **Customer satisfaction**: 4.7/5.0

---

## 🎯 BEST PRACTICES

### 8.9 Usage Tips

**📱 Mobile Best Practices:**
- Keep app updated to latest version
- Enable auto-sync for real-time data
- Use voice commands for hands-free operation
- Set up notifications for important alerts

**🔧 Maintenance Tips:**
- Clear cache weekly
- Check storage space monthly
- Backup data before major updates
- Report bugs immediately

**📚 Training Tips:**
- Practice voice commands regularly
- Learn keyboard shortcuts
- Explore all features
- Share tips with team members

---

## 🎉 CONCLUSION

**Van An Mobile App** là công cụ mạnh mẽ giúp nhân viên:
- **⚡ Tăng hiệu suất**: Xử lý đơn hàng nhanh hơn
- **🎤 Tiện lợi**: Điều khiển bằng giọng nói
- **💳 Hiện đại**: Thanh toán QR không tiền mặt
- **📊 Minh bạch**: Theo dõi hiệu suất real-time
- **🔔 Kết nối**: Luôn cập nhật thông tin

**📱 Download now:**
- **App Store**: Search "Van An Staff"
- **Google Play**: Search "Van An Staff"
- **Direct**: https://mobile.vanan.com

**📞 Need help?**
- **Hotline**: 1900-XXXX
- **Email**: mobile@vanan.com
- **In-app**: Settings → Help & Support

---

**🎯 Nâng tầm trải nghiệm nhân viên với Van An Mobile App!**

**📱 Trở thành nhân viên thông minh, hiệu quả và chuyên nghiệp!**
