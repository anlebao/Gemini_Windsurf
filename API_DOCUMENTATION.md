# 📚 Van An Ecosystem - API Documentation

> **Complete API Reference for Developers and Integrators**
>
> Version: 2.0 | Last Updated: 31/03/2026

---

## 🌐 Overview

Van An Ecosystem provides RESTful APIs for all system operations, including order management, voice commands, payments, and configuration.

### 🚀 Base URLs
- **Development**: `http://localhost:5001/api/v1`
- **Production**: `https://api.vanan.vn/v1`
- **Sandbox**: `https://sandbox-api.vanan.vn/v1`

### 🔐 Authentication
```http
Authorization: Bearer <JWT_TOKEN>
X-Tenant-ID: <TENANT_UUID>
X-Request-ID: <REQUEST_UUID>
```

---

## 📋 API Categories

### 1. Shop Configuration APIs
### 2. Order Management APIs  
### 3. Voice Command APIs
### 4. VietQR Payment APIs
### 5. Localization APIs
### 6. Onboarding APIs

---

## 🏪 SHOP CONFIGURATION APIS

### Get Shop Configuration
```http
GET /shopconfig/shops/{shopId}/config
```

**Response:**
```json
{
  "shopId": "12345678-1234-1234-1234-123456789012",
  "shopName": "Vạn An Cafe",
  "primaryColor": "#FF6B35",
  "secondaryColor": "#F7931E",
  "logoUrl": "https://cdn.vanan.vn/logos/default.png",
  "supportedLanguages": ["vi-VN", "en-US"],
  "timezone": "Asia/Ho_Chi_Minh",
  "currency": "VND",
  "vietqrConfig": {
    "bankId": "970422",
    "accountNo": "1234567890",
    "accountName": "VAN AN CAFE"
  }
}
```

### Update Shop Configuration
```http
PUT /shopconfig/shops/{shopId}/config
```

**Request:**
```json
{
  "shopName": "Vạn An Cafe Updated",
  "primaryColor": "#2E7D32",
  "secondaryColor": "#81C784",
  "logoUrl": "https://cdn.vanan.vn/logos/new-logo.png"
}
```

---

## 📦 ORDER MANAGEMENT APIS

### Create Order
```http
POST /orders
```

**Request:**
```json
{
  "productId": "12345678-1234-1234-1234-123456789012",
  "quantity": 2,
  "customerInfo": {
    "name": "Nguyễn Văn A",
    "phone": "0901234567",
    "deviceId": "customer-device-uuid"
  },
  "specialInstructions": "Ít đường, thêm đá",
  "trackingCode": "FLASH123"
}
```

**Response:**
```json
{
  "orderId": "87654321-4321-4321-4321-210987654321",
  "status": "pending",
  "totalPrice": 56000,
  "estimatedTime": "15 phút",
  "createdAt": "2026-03-31T14:30:00Z",
  "qrPaymentUrl": "https://api.vanan.vn/v1/payments/qr/87654321-4321-4321-4321-210987654321"
}
```

### Update Order Status
```http
PUT /orders/{orderId}/status
```

**Request:**
```json
{
  "status": "preparing",
  "notes": "Bắt đầu chế biến"
}
```

### Get Order Details
```http
GET /orders/{orderId}
```

**Response:**
```json
{
  "orderId": "87654321-4321-4321-4321-210987654321",
  "status": "preparing",
  "product": {
    "name": "Trà sữa truyền thống",
    "price": 28000
  },
  "quantity": 2,
  "totalPrice": 56000,
  "customer": {
    "name": "Nguyễn Văn A",
    "phone": "0901234567"
  },
  "timeline": [
    {
      "status": "pending",
      "timestamp": "2026-03-31T14:30:00Z",
      "notes": "Đơn hàng đã được tạo"
    },
    {
      "status": "preparing", 
      "timestamp": "2026-03-31T14:31:00Z",
      "notes": "Bắt đầu chế biến"
    }
  ]
}
```

---

## 🎤 VOICE COMMAND APIS

### Process Voice Command
```http
POST /voicecommand/process-audio
```

**Request:** (multipart/form-data)
```
audio: [binary audio file]
orderId: [optional order UUID]
language: vi-VN | en-US
```

**Response:**
```json
{
  "success": true,
  "transcript": "Xong đơn 12345",
  "commandType": "complete_order",
  "orderId": "87654321-4321-4321-4321-210987654321",
  "confidence": 0.95,
  "processingTime": 1250,
  "executed": true
}
```

### Process Text Command
```http
POST /voicecommand/process-text
```

**Request:**
```json
{
  "commandText": "Đơn mới trà sữa lớn",
  "language": "vi-VN",
  "orderId": null
}
```

**Response:**
```json
{
  "success": true,
  "transcript": "Đơn mới trà sữa lớn",
  "commandType": "new_order",
  "parameters": {
    "product": "trà sữa",
    "size": "lớn"
  },
  "confidence": 0.92,
  "processingTime": 800
}
```

### Text-to-Speech
```http
POST /voicecommand/text-to-speech
```

**Request:**
```json
{
  "text": "Đơn hàng của bạn đã sẵn sàng",
  "language": "vi-VN",
  "voice": "female"
}
```

**Response:**
```json
{
  "audioUrl": "https://api.vanan.vn/v1/audio/speech/abc123def456.mp3",
  "duration": 2.5,
  "expiresAt": "2026-03-31T15:30:00Z"
}
```

---

## 💳 VIETQR PAYMENT APIS

### Validate Bank Information
```http
POST /vietqr/validate-bank
```

**Request:**
```json
{
  "bankId": "970422",
  "accountNo": "1234567890",
  "accountName": "VAN AN CAFE"
}
```

**Response:**
```json
{
  "valid": true,
  "bankInfo": {
    "bankId": "970422",
    "bankName": "Ngân hàng TMCP Ngoại thương Việt Nam",
    "bankCode": "VCB",
    "bin": "970422"
  },
  "accountInfo": {
    "accountNo": "1234567890",
    "accountName": "VAN AN CAFE",
    "maskedAccountNo": "******7890"
  }
}
```

### Generate QR Code
```http
POST /vietqr/generate-qr
```

**Request:**
```json
{
  "orderId": "87654321-4321-4321-4321-210987654321",
  "amount": 56000,
  "description": "Thanh toán đơn hàng Vạn An",
  "bankId": "970422",
  "accountNo": "1234567890",
  "accountName": "VAN AN CAFE"
}
```

**Response:**
```json
{
  "qrCodeUrl": "https://img.vietqr.io/qr/abc123def456.png",
  "qrData": "000201010212308970422123456789002520000000106034VAN5802VN5908VAN AN6008Ho Chi Minh620708034VAN6304ABCD",
  "expiresAt": "2026-03-31T15:30:00Z",
  "transactionId": "TXN123456789"
}
```

### Check Payment Status
```http
GET /vietqr/payment-status/{transactionId}
```

**Response:**
```json
{
  "transactionId": "TXN123456789",
  "status": "completed",
  "amount": 56000,
  "paidAt": "2026-03-31T14:35:00Z",
  "bankReference": "VCB20260331143500"
}
```

---

## 🌍 LOCALIZATION APIS

### Get All Strings
```http
GET /localization/strings/{language}
```

**Response:**
```json
{
  "language": "vi-VN",
  "strings": {
    "common.buttons.save": "Lưu",
    "common.buttons.cancel": "Hủy",
    "common.buttons.submit": "Gửi",
    "product.name": "Tên sản phẩm",
    "product.price": "Giá",
    "order.status.pending": "Chờ xử lý",
    "order.status.preparing": "Đang chế biến",
    "order.status.ready": "Sẵn sàng",
    "order.status.completed": "Hoàn thành"
  },
  "lastUpdated": "2026-03-31T14:00:00Z"
}
```

### Get Specific String
```http
GET /localization/strings/{language}/{key}
```

**Response:**
```json
{
  "key": "common.buttons.save",
  "value": "Lưu",
  "language": "vi-VN",
  "fallbackUsed": false
}
```

### Update Localization
```http
PUT /localization/strings/{language}
```

**Request:**
```json
{
  "strings": {
    "new.feature.title": "Tính năng mới",
    "new.feature.description": "Mô tả tính năng mới"
  }
}
```

---

## 🚀 ONBOARDING APIS

### Get Available Templates
```http
GET /onboarding/templates
```

**Response:**
```json
{
  "templates": [
    {
      "id": "cafe-template",
      "name": "Quán Cafe",
      "description": "Template hoàn chỉnh cho quán cà phê, trà sữa",
      "category": "cafe",
      "features": ["voice_commands", "vietqr_payments", "multi_language"],
      "estimatedSetupTime": "5 phút",
      "productsCount": 25,
      "ingredientsCount": 50
    },
    {
      "id": "beauty-template",
      "name": "Spa & Beauty",
      "description": "Template cho spa, salon làm đẹp",
      "category": "beauty",
      "features": ["appointment_booking", "customer_management", "multi_language"],
      "estimatedSetupTime": "3 phút",
      "servicesCount": 15,
      "staffCount": 8
    }
  ]
}
```

### Apply Template
```http
POST /onboarding/shops/{shopId}/apply-template
```

**Request:**
```json
{
  "templateId": "cafe-template",
  "customizations": {
    "shopName": "Vạn An Cafe",
    "shopAddress": "123 Nguyễn Huệ, Q1, TP.HCM",
    "shopPhone": "1900-1234",
    "shopEmail": "info@vanan.vn"
  }
}
```

**Response:**
```json
{
  "success": true,
  "message": "Template applied successfully",
  "setupSummary": {
    "productsCreated": 25,
    "ingredientsCreated": 50,
    "recipesCreated": 25,
    "workflowStepsCreated": 8,
    "estimatedSetupTime": "5 phút"
  }
}
```

### Quick Setup
```http
POST /onboarding/shops/{shopId}/quick-setup
```

**Request:**
```json
{
  "templateType": "cafe-template",
  "shopName": "Vạn An Cafe",
  "shopAddress": "123 Nguyễn Huệ, Q1, TP.HCM",
  "shopPhone": "1900-1234",
  "shopEmail": "info@vanan.vn"
}
```

**Response:**
```json
{
  "success": true,
  "shopId": "12345678-1234-1234-1234-123456789012",
  "shopInfo": {
    "name": "Vạn An Cafe",
    "address": "123 Nguyễn Huệ, Q1, TP.HCM",
    "phone": "1900-1234",
    "email": "info@vanan.vn"
  },
  "productsCount": 25,
  "ingredientsCount": 50,
  "workflowStepsCount": 8,
  "estimatedSetupTime": "00:05:00",
  "nextSteps": [
    "Cấu hình VietQR nhận tiền",
    "Kiểm tra tồn kho nguyên liệu",
    "Test voice commands",
    "Tùy chỉnh branding"
  ]
}
```

---

## 📊 RESPONSE FORMATS

### Success Response
```json
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully",
  "timestamp": "2026-03-31T14:30:00Z",
  "requestId": "req_abc123def456"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input parameters",
    "details": [
      {
        "field": "productId",
        "message": "Product ID is required"
      }
    ]
  },
  "timestamp": "2026-03-31T14:30:00Z",
  "requestId": "req_abc123def456"
}
```

### HTTP Status Codes
- `200 OK` - Request successful
- `201 Created` - Resource created successfully
- `400 Bad Request` - Invalid input parameters
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource conflict
- `422 Unprocessable Entity` - Validation failed
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

---

## 🚀 RATE LIMITING

### Rate Limits by Plan
| Plan | Requests/Minute | Requests/Hour | Concurrent |
|------|-----------------|---------------|-------------|
| Basic | 100 | 5,000 | 10 |
| Professional | 500 | 25,000 | 50 |
| Enterprise | 2,000 | 100,000 | 200 |

### Rate Limit Headers
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1640995200
```

---

## 🧪 TESTING & SANDBOX

### Sandbox Environment
- **URL**: `https://sandbox-api.vanan.vn/v1`
- **Credentials**: Provided on request
- **Features**: Full API functionality without real transactions

### Test Data
```bash
# Test Bank Account (Vietcombank)
Bank ID: 970422
Account No: 1234567890
Account Name: VAN AN TEST

# Test Order
Product ID: 12345678-1234-1234-1234-123456789012
Amount: 50000 VND
```

---

## 📞 SUPPORT

### API Support
- **Documentation**: https://docs.vanan.vn
- **Status Page**: https://status.vanan.vn
- **Support Email**: api-support@vanan.vn
- **Developer Chat**: https://discord.gg/vanan-devs

### SDKs & Libraries
- **.NET**: `VanAn.SDK.NET`
- **JavaScript**: `@vanan/sdk-js`
- **Python**: `vanan-sdk-python`
- **PHP**: `vanan-sdk-php`

---

**© 2026 Van An Ecosystem - API Documentation v2.0**
