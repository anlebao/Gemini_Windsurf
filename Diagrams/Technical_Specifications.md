# 🔧 VanAn Ecosystem - Technical Specifications

## 📋 Table of Contents
1. [API Specifications](#api-specifications)
2. [Database Schema](#database-schema)
3. [Message Formats](#message-formats)
4. [Configuration Management](#configuration-management)
5. [Error Handling](#error-handling)

---

## 🌐 API Specifications

### **CoreHub API Endpoints**

#### **Order Management**
```http
# Create Order
POST /api/orders
Content-Type: application/json
Authorization: DeviceId {customer-device-id}

{
  "customerDeviceId": "device_12345",
  "items": [
    {
      "productId": "guid-product-id",
      "quantity": 2,
      "unitPrice": 28000
    }
  ],
  "totalAmount": 56000
}

# Response
{
  "orderId": "guid-order-id",
  "status": "pending",
  "orderDate": "2026-03-25T12:00:00Z",
  "estimatedMinutes": 15,
  "totalAmount": 56000
}

# Get Order Status
GET /api/orders/{orderId}
Authorization: DeviceId {customer-device-id}

# Response
{
  "orderId": "guid-order-id",
  "customerDeviceId": "device_12345",
  "status": {
    "id": "processing",
    "displayName": "Đang pha chế",
    "sequence": 3
  },
  "orderDate": "2026-03-25T12:00:00Z",
  "statusStartedAt": "2026-03-25T12:02:00Z",
  "estimatedMinutes": 15,
  "items": [...],
  "totalAmount": 56000
}

# Update Order Status (Staff only)
PUT /api/orders/{orderId}/status
Authorization: Bearer {staff-token}
Content-Type: application/json

{
  "statusId": "processing",
  "staffId": "staff-biometric-hash"
}
```

#### **Inventory Management**
```http
# Check Ingredient Availability
GET /api/inventory/check?ingredientId={id}&quantity={amount}
Authorization: Bearer {staff-token}

# Response
{
  "available": true,
  "currentStock": 150,
  "requestedQuantity": 10,
  "remainingAfterDeduction": 140
}

# Get Low Stock Items
GET /api/inventory/low-stock
Authorization: Bearer {staff-token}

# Response
{
  "items": [
    {
      "ingredientId": "guid",
      "name": "Trà sữa",
      "currentStock": 5,
      "minThreshold": 20,
      "unit": "lit",
      "status": "critical"
    }
  ]
}

# Update Inventory
PUT /api/inventory/{ingredientId}
Authorization: Bearer {staff-token}
Content-Type: application/json

{
  "newQuantity": 200,
  "staffId": "staff-biometric-hash",
  "reason": "restock"
}
```

#### **Product Management**
```http
# Get Available Products
GET /api/products
Authorization: DeviceId {customer-device-id}

# Response
{
  "products": [
    {
      "id": "guid",
      "name": "Trà sữa truyền thống",
      "description": "Trà sữa với trân châu",
      "price": 28000,
      "isActive": true,
      "estimatedMinutes": 10,
      "ingredients": [
        {
          "name": "Trà sữa",
          "quantity": 200,
          "unit": "ml"
        }
      ]
    }
  ]
}

# Get Product Details
GET /api/products/{productId}
Authorization: DeviceId {customer-device-id}
```

#### **Workflow Configuration**
```http
# Get Workflow Statuses
GET /api/workflow/statuses
Authorization: Bearer {staff-token}

# Response
{
  "statuses": [
    {
      "id": "pending",
      "displayName": "Chờ xác nhận",
      "sequence": 1,
      "isActive": true,
      "requiresInventoryDeduction": false
    },
    {
      "id": "confirmed",
      "displayName": "Đã xác nhận",
      "sequence": 2,
      "isActive": true,
      "requiresInventoryDeduction": false
    },
    {
      "id": "processing",
      "displayName": "Đang pha chế",
      "sequence": 3,
      "isActive": true,
      "requiresInventoryDeduction": true
    }
  ]
}

# Update Workflow Configuration
PUT /api/workflow/statuses/{statusId}
Authorization: Bearer {staff-token}
Content-Type: application/json

{
  "isActive": true,
  "requiresInventoryDeduction": true,
  "sequence": 3
}
```

### **Gateway API Endpoints**

#### **Authentication**
```http
# Customer Authentication
POST /api/auth/customer
Content-Type: application/json

{
  "deviceId": "device_12345"
}

# Response
{
  "token": "jwt-token",
  "deviceId": "device_12345",
  "expiresIn": 3600
}

# Staff Authentication
POST /api/auth/staff
Content-Type: application/json

{
  "biometricHash": "sha256-hash",
  "deviceId": "terminal-001"
}

# Response
{
  "token": "jwt-token",
  "staffId": "staff-guid",
  "role": "barista",
  "permissions": ["order.update", "inventory.view"]
}
```

---

## 🗄️ Database Schema

### **SQLite Schema (KhachLink & ShopERP)**

```sql
-- Products Table
CREATE TABLE Products (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
    Price DECIMAL(10,2) NOT NULL,
    ImageUrl TEXT,
    IsActive BOOLEAN DEFAULT 1,
    EstimatedMinutes INTEGER DEFAULT 10,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Ingredients Table
CREATE TABLE Ingredients (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
    UnitPrice DECIMAL(10,2),
    Unit TEXT NOT NULL DEFAULT 'pcs',
    CurrentStock INTEGER DEFAULT 0,
    MinThreshold INTEGER DEFAULT 10,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Recipes Table (Product-Ingredient mapping)
CREATE TABLE Recipes (
    Id TEXT PRIMARY KEY,
    ProductId TEXT NOT NULL,
    IngredientId TEXT NOT NULL,
    Quantity DECIMAL(10,2) NOT NULL,
    Unit TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ProductId) REFERENCES Products(Id),
    FOREIGN KEY (IngredientId) REFERENCES Ingredients(Id)
);

-- Inventory Table
CREATE TABLE Inventory (
    Id TEXT PRIMARY KEY,
    IngredientId TEXT NOT NULL,
    CurrentStock INTEGER NOT NULL,
    MinThreshold INTEGER NOT NULL,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (IngredientId) REFERENCES Ingredients(Id)
);

-- Orders Table
CREATE TABLE Orders (
    Id TEXT PRIMARY KEY,
    CustomerDeviceId TEXT NOT NULL,
    OrderDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    TotalAmount DECIMAL(10,2) NOT NULL,
    CurrentStatusId TEXT NOT NULL DEFAULT 'pending',
    StatusStartedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    EstimatedMinutes INTEGER DEFAULT 15,
    StaffId TEXT,
    CompletedAt DATETIME,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- OrderItems Table
CREATE TABLE OrderItems (
    Id TEXT PRIMARY KEY,
    OrderId TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    TotalPrice DECIMAL(10,2) NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

-- OrderStatuses Table
CREATE TABLE OrderStatuses (
    Id TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Sequence INTEGER NOT NULL,
    IsActive BOOLEAN DEFAULT 1,
    RequiresInventoryDeduction BOOLEAN DEFAULT 0,
    Color TEXT DEFAULT '#007bff',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- OrderStatusHistory Table
CREATE TABLE OrderStatusHistory (
    Id TEXT PRIMARY KEY,
    OrderId TEXT NOT NULL,
    StatusId TEXT NOT NULL,
    ChangedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ChangedBy TEXT, -- StaffId or 'system'
    Notes TEXT,
    FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    FOREIGN KEY (StatusId) REFERENCES OrderStatuses(Id)
);

-- Staff Table
CREATE TABLE Staff (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    BiometricHash TEXT NOT NULL UNIQUE,
    Role TEXT NOT NULL DEFAULT 'barista',
    IsActive BOOLEAN DEFAULT 1,
    DeviceId TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Outbox Table (for reliable messaging)
CREATE TABLE Outbox (
    Id TEXT PRIMARY KEY,
    EventType TEXT NOT NULL,
    EventData TEXT NOT NULL, -- JSON
    Processed BOOLEAN DEFAULT 0,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ProcessedAt DATETIME,
    RetryCount INTEGER DEFAULT 0
);
```

### **PostgreSQL Schema (CoreHub)**

```sql
-- Core Database for centralized data
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Similar structure but with PostgreSQL-specific types
CREATE TABLE Products (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name VARCHAR(255) NOT NULL,
    Description TEXT,
    Price DECIMAL(10,2) NOT NULL,
    ImageUrl VARCHAR(500),
    IsActive BOOLEAN DEFAULT TRUE,
    EstimatedMinutes INTEGER DEFAULT 10,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Additional tables for multi-tenant support
CREATE TABLE Tenants (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name VARCHAR(255) NOT NULL,
    Domain VARCHAR(255),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE TenantProducts (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TenantId UUID NOT NULL,
    ProductId UUID NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
```

---

## 📨 Message Formats

### **NATS Message Subjects**

```
# Order Events
vanan.order.created
vanan.order.status.changed
vanan.order.completed
vanan.order.cancelled

# Inventory Events
vanan.inventory.low.stock
vanan.inventory.updated
vanan.inventory.restocked

# System Events
vanan.system.health.check
vanan.system.error
vanan.user.activity
```

### **Message Payloads**

#### **Order Created Event**
```json
{
  "eventId": "guid-event-id",
  "eventType": "vanan.order.created",
  "timestamp": "2026-03-25T12:00:00Z",
  "data": {
    "orderId": "guid-order-id",
    "customerDeviceId": "device_12345",
    "items": [
      {
        "productId": "guid-product-id",
        "productName": "Trà sữa truyền thống",
        "quantity": 2,
        "unitPrice": 28000,
        "totalPrice": 56000
      }
    ],
    "totalAmount": 56000,
    "status": "pending",
    "estimatedMinutes": 15,
    "orderDate": "2026-03-25T12:00:00Z"
  },
  "metadata": {
    "source": "corehub",
    "version": "1.0",
    "correlationId": "guid-correlation-id"
  }
}
```

#### **Order Status Changed Event**
```json
{
  "eventId": "guid-event-id",
  "eventType": "vanan.order.status.changed",
  "timestamp": "2026-03-25T12:05:00Z",
  "data": {
    "orderId": "guid-order-id",
    "oldStatus": {
      "id": "confirmed",
      "displayName": "Đã xác nhận"
    },
    "newStatus": {
      "id": "processing",
      "displayName": "Đang pha chế",
      "sequence": 3
    },
    "changedBy": {
      "staffId": "guid-staff-id",
      "staffName": "Nguyễn Văn A"
    },
    "changedAt": "2026-03-25T12:05:00Z",
    "notes": "Bắt đầu pha chế"
  },
  "metadata": {
    "source": "shoperp",
    "version": "1.0",
    "correlationId": "guid-correlation-id"
  }
}
```

#### **Low Stock Alert Event**
```json
{
  "eventId": "guid-event-id",
  "eventType": "vanan.inventory.low.stock",
  "timestamp": "2026-03-25T12:10:00Z",
  "data": {
    "ingredientId": "guid-ingredient-id",
    "ingredientName": "Trà sữa",
    "currentStock": 5,
    "minThreshold": 20,
    "unit": "liter",
    "status": "critical",
    "lastUpdated": "2026-03-25T12:10:00Z"
  },
  "metadata": {
    "source": "corehub",
    "version": "1.0",
    "priority": "high"
  }
}
```

---

## ⚙️ Configuration Management

### **Environment Variables**

#### **CoreHub Configuration**
```bash
# Database Configuration
ConnectionStrings__DefaultConnection=Host=postgres;Database=VanAnCoreHub;Username=vanan_admin;Password=VanAn@2024!

# NATS Configuration
NATS__Url=nats://nats:4222
NATS__ClientId=corehub
NATS__ClusterName=vanan-cluster

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:80

# JWT Configuration
JWT__SecretKey=your-secret-key-here
JWT__Issuer=VanAnEcosystem
JWT__Audience=VanAnApps
JWT__ExpiryMinutes=60

# Logging Configuration
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
Logging__LogLevel__Microsoft.Hosting.Lifetime=Information
```

#### **Gateway Configuration**
```bash
# Routing Configuration
Gateway__Routes__CoreHub__Path=/api/corehub/*
Gateway__Routes__CoreHub__Destination=http://corehub:80
Gateway__Routes__KhachLink__Path=/api/khachlink/*
Gateway__Routes__KhachLink__Destination=http://khachlink:80

# Authentication Configuration
Authentication__Jwt__SecretKey=your-secret-key-here
Authentication__Jwt__Issuer=VanAnEcosystem
Authentication__DeviceId__Enabled=true
Authentication__Biometric__Enabled=true

# Rate Limiting Configuration
RateLimiting__RequestsPerMinute=100
RateLimiting__BurstSize=20
```

#### **KhachLink Configuration**
```bash
# Database Configuration
ConnectionStrings__DefaultConnection=Data Source=vanan_khachlink.db

# API Configuration
ApiSettings__CoreHubUrl=http://gateway:5001
ApiSettings__TimeoutSeconds=30

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:80

# UI Configuration
UISettings__RefreshIntervalSeconds=30
UISettings__MaxOrdersPerPage=20
```

#### **ShopERP Configuration**
```bash
# Database Configuration
ConnectionStrings__DefaultConnection=Data Source=vanan_shoperp.db

# API Configuration
ApiSettings__CoreHubUrl=http://gateway:5001
ApiSettings__TimeoutSeconds=30

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:80

# Workflow Configuration
Workflow__AutoRefreshSeconds=10
Workflow__MaxConcurrentOrders=50
```

### **Docker Compose Environment Variables**

```yaml
version: '3.8'
services:
  corehub:
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=VanAnCoreHub;Username=vanan_admin;Password=VanAn@2024!
      - NATS__Url=nats://nats:4222
      - ASPNETCORE_ENVIRONMENT=Production
      - JWT__SecretKey=${JWT_SECRET_KEY}
      - JWT__Issuer=VanAnEcosystem
      - JWT__Audience=VanAnApps
      - JWT__ExpiryMinutes=60

  gateway:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Authentication__Jwt__SecretKey=${JWT_SECRET_KEY}
      - Authentication__Jwt__Issuer=VanAnEcosystem
      - RateLimiting__RequestsPerMinute=200

  khachlink:
    environment:
      - ConnectionStrings__DefaultConnection=Data Source=vanan_khachlink.db
      - ApiSettings__CoreHubUrl=http://gateway:5001
      - ASPNETCORE_ENVIRONMENT=Production

  shoperp:
    environment:
      - ConnectionStrings__DefaultConnection=Data Source=vanan_shoperp.db
      - ApiSettings__CoreHubUrl=http://gateway:5001
      - ASPNETCORE_ENVIRONMENT=Production
```

---

## 🚨 Error Handling

### **Standard Error Response Format**

```json
{
  "error": {
    "code": "ORDER_NOT_FOUND",
    "message": "Order with ID '123' not found",
    "details": {
      "orderId": "123",
      "timestamp": "2026-03-25T12:00:00Z",
      "requestId": "req-456"
    },
    "correlationId": "corr-789"
  },
  "metadata": {
    "timestamp": "2026-03-25T12:00:00Z",
    "path": "/api/orders/123",
    "method": "GET",
    "statusCode": 404
  }
}
```

### **Error Codes**

#### **Business Logic Errors**
- `ORDER_NOT_FOUND` (404): Order does not exist
- `INSUFFICIENT_INVENTORY` (400): Not enough ingredients
- `INVALID_STATUS_TRANSITION` (400): Invalid status change
- `DUPLICATE_ORDER` (409): Order already exists
- `PAYMENT_FAILED` (400): Payment processing failed

#### **Authentication/Authorization Errors**
- `UNAUTHORIZED` (401): Invalid credentials
- `FORBIDDEN` (403): Insufficient permissions
- `TOKEN_EXPIRED` (401): JWT token expired
- `INVALID_DEVICE_ID` (401): Device ID not recognized
- `INVALID_BIOMETRIC` (401): Biometric hash invalid

#### **Validation Errors**
- `INVALID_REQUEST` (400): Request validation failed
- `MISSING_REQUIRED_FIELD` (400): Required field missing
- `INVALID_FORMAT` (400): Invalid data format
- `VALUE_OUT_OF_RANGE` (400): Value outside allowed range

#### **System Errors**
- `DATABASE_ERROR` (500): Database operation failed
- `EXTERNAL_SERVICE_ERROR` (502): External service unavailable
- `MESSAGE_QUEUE_ERROR` (500): Message queue operation failed
- `INTERNAL_SERVER_ERROR` (500): Unexpected system error

### **Exception Handling Strategy**

#### **Global Exception Handler**
```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        var errorResponse = CreateErrorResponse(exception);
        
        httpContext.Response.StatusCode = errorResponse.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
        
        return true;
    }
    
    private ErrorResponse CreateErrorResponse(Exception exception)
    {
        return exception switch
        {
            BusinessException be => new BusinessErrorResponse(be),
            ValidationException ve => new ValidationErrorResponse(ve),
            AuthenticationException ae => new AuthenticationErrorResponse(ae),
            _ => new InternalServerErrorResponse(exception)
        };
    }
}
```

#### **Retry Policy Configuration**
```csharp
// Database operations
var dbRetryPolicy = Policy
    .Handle<SqlException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryAttempt, context) => 
        {
            logger.LogWarning($"Retry {retryAttempt} after {timespan.TotalSeconds}s delay");
        });

// External API calls
var apiRetryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(retryAttempt * 2));

// Message publishing
var messagingRetryPolicy = Policy
    .Handle<NATSConnectionException>()
    .WaitAndRetryAsync(5, retryAttempt => 
        TimeSpan.FromSeconds(1));
```

#### **Circuit Breaker Pattern**
```csharp
var circuitBreakerPolicy = Policy
    .Handle<Exception>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (exception, breakDelay) => 
        {
            logger.LogError($"Circuit broken due to: {exception.Message}");
        },
        onReset: () => 
        {
            logger.LogInformation("Circuit reset");
        });
```

---

*Last Updated: March 2026*
*Technical Specification Version: 1.0*
