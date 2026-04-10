# 🏗️ VanAn Ecosystem - System Architecture Diagrams

## 📋 Table of Contents
1. [Use Case Diagram](#use-case-diagram)
2. [Class Diagram](#class-diagram)
3. [Sequence Diagram](#sequence-diagram)
4. [Data Flow Diagram (DFD)](#data-flow-diagram-dfd)
5. [Deployment Architecture](#deployment-architecture)

---

## 🎯 Use Case Diagram

```mermaid
graph TD
    subgraph "Khách hàng (KhachLink)"
        A[Khách hàng] --> B[Xem thực đơn]
        A --> C[Đặt hàng]
        A --> D[Theo dõi đơn hàng]
        A --> E[Thanh toán]
    end
    
    subgraph "Nhân viên (ShopERP)"
        F[Nhân viên bếp] --> G[Xem đơn hàng]
        F --> H[Cập nhật trạng thái]
        F --> I[Quản lý tồn kho]
        J[Quản lý] --> K[Cấu hình workflow]
        J --> L[Xem báo cáo]
    end
    
    subgraph "Hệ thống"
        M[CoreHub] --> N[Quản lý đơn hàng]
        M --> O[Xử lý thanh toán]
        M --> P[Quản lý tồn kho]
        Q[Gateway] --> R[Routing & Authentication]
        S[Database] --> T[Lưu trữ dữ liệu]
        U[NATS] --> V[Messaging]
    end
```

### 📝 Use Case Descriptions

#### **Khách hàng (KhachLink)**
- **Xem thực đơn**: Hiển thị danh sách sản phẩm có sẵn
- **Đặt hàng**: Tạo đơn hàng mới với các sản phẩm đã chọn
- **Theo dõi đơn hàng**: Xem trạng thái hiện tại của đơn hàng
- **Thanh toán**: Xử lý thanh toán đơn hàng

#### **Nhân viên (ShopERP)**
- **Xem đơn hàng**: Hiển thị danh sách đơn hàng cần xử lý
- **Cập nhật trạng thái**: Thay đổi trạng thái của đơn hàng
- **Quản lý tồn kho**: Kiểm tra và cập nhật số lượng tồn kho
- **Cấu hình workflow**: Tùy chỉnh luồng xử lý đơn hàng
- **Xem báo cáo**: Xem thống kê và báo cáo kinh doanh

---

## 🏛️ Class Diagram

```mermaid
classDiagram
    %% Domain Models
    class Product {
        +Guid Id
        +string Name
        +string Description
        +decimal Price
        +DateTime CreatedAt
        +bool IsActive
    }
    
    class Ingredient {
        +Guid Id
        +string Name
        +string Description
        +decimal UnitPrice
        +string Unit
        +int CurrentStock
        +int MinThreshold
    }
    
    class Recipe {
        +Guid Id
        +Guid ProductId
        +Guid IngredientId
        +decimal Quantity
        +string Unit
    }
    
    class Inventory {
        +Guid Id
        +Guid IngredientId
        +int CurrentStock
        +int MinThreshold
        +DateTime LastUpdated
    }
    
    class Order {
        +Guid Id
        +string CustomerDeviceId
        +DateTime OrderDate
        +decimal TotalAmount
        +string CurrentStatusId
        +DateTime StatusStartedAt
        +List~OrderItem~ Items
    }
    
    class OrderItem {
        +Guid Id
        +Guid OrderId
        +Guid ProductId
        +int Quantity
        +decimal UnitPrice
        +decimal TotalPrice
    }
    
    class OrderStatus {
        +string Id
        +string DisplayName
        +int Sequence
        +bool IsActive
        +bool RequiresInventoryDeduction
    }
    
    %% Services
    class OrderWorkflowService {
        +IOrderWorkflowService
        +Task~Order~ CreateOrderAsync(OrderRequest request)
        +Task UpdateOrderStatusAsync(string orderId, string statusId)
        +Task~List~OrderStatus~~ GetAvailableStatusesAsync()
        +Task~Order~ GetOrderAsync(string orderId)
    }
    
    class InventoryService {
        +IInventoryService
        +Task~bool~ CheckAvailabilityAsync(Guid ingredientId, int quantity)
        +Task DeductInventoryAsync(Guid ingredientId, int quantity)
        +Task~List~Inventory~~ GetLowStockItemsAsync()
        +Task UpdateInventoryAsync(Guid ingredientId, int newQuantity)
    }
    
    %% Infrastructure
    class VanAnSqliteDbContext {
        +DbSet~Product~ Products
        +DbSet~Ingredient~ Ingredients
        +DbSet~Recipe~ Recipes
        +DbSet~Inventory~ Inventories
        +DbSet~Order~ Orders
        +DbSet~OrderItem~ OrderItems
        +DbSet~OrderStatus~ OrderStatuses
    }
    
    %% Relationships
    Product ||--o{ Recipe : "has"
    Ingredient ||--o{ Recipe : "used in"
    Ingredient ||--|| Inventory : "tracked by"
    Order ||--o{ OrderItem : "contains"
    Product ||--o{ OrderItem : "ordered as"
    Order ||--|| OrderStatus : "has status"
    
    OrderWorkflowService ..> Order : "manages"
    OrderWorkflowService ..> OrderStatus : "uses"
    InventoryService ..> Inventory : "manages"
    InventoryService ..> Ingredient : "tracks"
    
    VanAnSqliteDbContext ..> Product : "persists"
    VanAnSqliteDbContext ..> Ingredient : "persists"
    VanAnSqliteDbContext ..> Recipe : "persists"
    VanAnSqliteDbContext ..> Inventory : "persists"
    VanAnSqliteDbContext ..> Order : "persists"
    VanAnSqliteDbContext ..> OrderItem : "persists"
    VanAnSqliteDbContext ..> OrderStatus : "persists"
```

---

## 🔄 Sequence Diagram

### **1. Customer Order Flow**

```mermaid
sequenceDiagram
    participant C as Khách hàng
    participant KL as KhachLink App
    participant G as Gateway
    participant CH as CoreHub
    participant DB as Database
    participant N as NATS
    participant SE as ShopERP
    
    C->>KL: Chọn sản phẩm
    KL->>KL: Tính tổng tiền
    C->>KL: Xác nhận đặt hàng
    KL->>G: POST /api/orders (OrderRequest)
    G->>G: Validate Request
    G->>CH: Forward to OrderService
    CH->>DB: Check inventory
    DB-->>CH: Inventory available
    CH->>DB: Create Order
    DB-->>CH: Order created
    CH->>N: Publish OrderCreated event
    CH-->>G: OrderResponse
    G-->>KL: Order confirmation
    KL-->>C: Hiển thị thông báo
    
    N->>SE: Notify new order
    SE->>SE: Update order list
```

### **2. Order Status Update Flow**

```mermaid
sequenceDiagram
    participant E as Nhân viên
    participant SE as ShopERP
    participant G as Gateway
    participant CH as CoreHub
    participant DB as Database
    participant N as NATS
    participant KL as KhachLink
    
    E->>SE: Click "Cập nhật trạng thái"
    SE->>G: PUT /api/orders/{id}/status
    G->>CH: UpdateOrderStatusAsync
    CH->>DB: Update order status
    DB-->>CH: Status updated
    CH->>N: Publish OrderStatusChanged event
    CH-->>G: Success response
    G-->>SE: Confirmation
    SE-->>E: UI updated
    
    N->>KL: Real-time status update
    KL->>KL: Update order tracking
    KL-->>C: Hiển thị trạng thái mới
```

### **3. Inventory Management Flow**

```mermaid
sequenceDiagram
    participant CH as CoreHub
    participant IS as InventoryService
    participant DB as Database
    participant N as NATS
    participant SE as ShopERP
    
    CH->>IS: DeductInventoryAsync(ingredientId, quantity)
    IS->>DB: Check current stock
    DB-->>IS: Current stock level
    IS->>IS: Calculate new stock
    IS->>DB: Update inventory
    DB-->>IS: Inventory updated
    
    alt Stock below threshold
        IS->>N: Publish LowStockAlert event
        N->>SE: Notify low stock
        SE->>SE: Show alert notification
    end
```

---

## 📊 Data Flow Diagram (DFD)

### **Level 0 - Context Diagram**

```mermaid
graph TD
    subgraph "VanAn Ecosystem"
        A[KhachLink App]
        B[ShopERP App]
        C[CoreHub API]
        D[Gateway]
        E[Database]
        F[NATS Messaging]
    end
    
    G[Khách hàng] --> A
    H[Nhân viên] --> B
    A --> D
    B --> D
    D --> C
    C --> E
    C --> F
    F --> B
```

### **Level 1 - System DFD**

```mermaid
graph TD
    subgraph "External Entities"
        CUST[Khách hàng]
        STAFF[Nhân viên]
    end
    
    subgraph "System Boundary"
        subgraph "Web Applications"
            KL[KhachLink App]
            SE[ShopERP App]
        end
        
        subgraph "API Gateway"
            GW[Gateway Service]
        end
        
        subgraph "Core Services"
            CH[CoreHub API]
            OW[Order Workflow Service]
            IS[Inventory Service]
        end
        
        subgraph "Data Stores"
            DB[(SQLite Database)]
            MSG[(NATS Messaging)]
        end
    end
    
    CUST --> KL
    STAFF --> SE
    
    KL --> GW
    SE --> GW
    
    GW --> CH
    CH --> OW
    CH --> IS
    
    OW --> DB
    IS --> DB
    CH --> DB
    
    OW --> MSG
    MSG --> SE
```

### **Level 2 - Order Processing DFD**

```mermaid
graph TD
    subgraph "Order Processing Subsystem"
        subgraph "Processes"
            P1[1.0 Validate Order]
            P2[2.0 Check Inventory]
            P3[3.0 Create Order]
            P4[4.0 Update Status]
            P5[5.0 Notify Customer]
            P6[6.0 Notify Staff]
        end
        
        subgraph "Data Stores"
            D1[(Orders)]
            D2[(Inventory)]
            D3[(Products)]
            D4[(Order Statuses)]
        end
        
        subgraph "External Entities"
            E1[Customer Request]
            E2[Staff Notification]
            E3[Customer Notification]
        end
    end
    
    E1 --> P1
    P1 --> P2
    P2 --> D2
    D2 --> P3
    P3 --> D1
    P3 --> P4
    P4 --> D4
    P4 --> P5
    P4 --> P6
    P5 --> E3
    P6 --> E2
    
    D3 --> P1
    D1 --> P4
```

---

## 🚀 Deployment Architecture

```mermaid
graph TB
    subgraph "Load Balancer"
        LB[Nginx/Traefik]
    end
    
    subgraph "Application Layer"
        subgraph "Web Apps"
            KL[KhachLink:5002]
            SE[ShopERP:5003]
        end
        
        subgraph "API Services"
            GW[Gateway:5001]
            CH[CoreHub:5010]
        end
    end
    
    subgraph "Infrastructure Layer"
        subgraph "Database"
            PG[(PostgreSQL:5432)]
            SL1[(SQLite:KhachLink)]
            SL2[(SQLite:ShopERP)]
        end
        
        subgraph "Messaging"
            NATS[NATS:4222]
        end
        
        subgraph "Monitoring"
            PGADMIN[pgAdmin:5050]
        end
    end
    
    subgraph "External"
        CUST[Customers]
        STAFF[Staff]
    end
    
    CUST --> LB
    STAFF --> LB
    LB --> KL
    LB --> SE
    KL --> GW
    SE --> GW
    GW --> CH
    CH --> PG
    CH --> NATS
    NATS --> SE
    KL --> SL1
    SE --> SL2
    PGADMIN --> PG
```

---

## 📋 Component Interactions

### **Message Flow Patterns**

```mermaid
graph LR
    subgraph "Request-Response"
        A[Client] -->|HTTP Request| B[Gateway]
        B -->|Route| C[CoreHub]
        C -->|Query| D[Database]
        D -->|Response| C
        C -->|Response| B
        B -->|Response| A
    end
    
    subgraph "Event-Driven"
        E[CoreHub] -->|Publish| F[NATS]
        F -->|Broadcast| G[ShopERP]
        F -->|Broadcast| H[KhachLink]
    end
    
    subgraph "Outbox Pattern"
        I[CoreHub] -->|Write| J[Outbox Table]
        K[Background Worker] -->|Read| J
        K -->|Publish| F
    end
```

---

## 🔐 Security Architecture

```mermaid
graph TD
    subgraph "Security Layers"
        L1[Network Security<br/>- HTTPS/TLS<br/>- Firewall Rules]
        L2[Authentication<br/>- DeviceId (Customers)<br/>- BiometricHash (Staff)]
        L3[Authorization<br/>- Role-based Access<br/>- API Key Validation]
        L4[Data Protection<br/>- Encrypted at Rest<br/>- Encrypted in Transit]
    end
    
    subgraph "Identity Management"
        CUST[Customer Identity<br/>DeviceId-based]
        STAFF[Staff Identity<br/>BiometricHash-based]
    end
    
    L1 --> L2
    L2 --> L3
    L3 --> L4
    
    CUST --> L2
    STAFF --> L2
```

---

## 📈 Performance & Scalability

### **Horizontal Scaling Strategy**

```mermaid
graph TB
    subgraph "Current Scale"
        APP1[KhachLink Instance]
        APP2[ShopERP Instance]
        API1[CoreHub Instance]
        GW1[Gateway Instance]
    end
    
    subgraph "Scaled Architecture"
        LB[Load Balancer]
        APPS[Multiple App Instances]
        APIS[Multiple API Instances]
        GW_CLUSTER[Gateway Cluster]
    end
    
    subgraph "Data Layer"
        DB_CLUSTER[Database Cluster]
        CACHE[Redis Cache]
        QUEUE[Message Queue]
    end
    
    APP1 -.-> APPS
    APP2 -.-> APPS
    API1 -.-> APIS
    GW1 -.-> GW_CLUSTER
    
    LB --> APPS
    APPS --> GW_CLUSTER
    GW_CLUSTER --> APIS
    APIS --> DB_CLUSTER
    APIS --> CACHE
    APIS --> QUEUE
```

---

## 🎯 Key Design Principles

### **1. Clean Architecture**
- **Domain Layer**: Business logic and entities
- **Application Layer**: Use cases and services
- **Infrastructure Layer**: Data access and external services
- **Presentation Layer**: Web applications and APIs

### **2. Microservices Pattern**
- **Bounded Contexts**: Clear service boundaries
- **Database per Service**: Independent data stores
- **Event-Driven Communication**: Loose coupling
- **API Gateway**: Single entry point

### **3. Zero-Friction Identity**
- **DeviceId**: Customer identification
- **BiometricHash**: Staff authentication
- **No Passwords**: Eliminate friction

### **4. Outbox Pattern**
- **Transactional Consistency**: Reliable event publishing
- **Background Processing**: Async message delivery
- **Error Handling**: Retry mechanisms

---

## 📚 Glossary

| Term | Description |
|------|-------------|
| **KhachLink** | Customer-facing ordering application |
| **ShopERP** | Staff management application |
| **CoreHub** | Central API service |
| **Gateway** | API routing and authentication |
| **DeviceId** | Unique customer identifier |
| **BiometricHash** | Staff biometric identifier |
| **Outbox Pattern** | Reliable event publishing pattern |
| **NATS** | Lightweight messaging system |
| **Workflow** | Order processing flow |

---

*Last Updated: March 2026*
*Architecture Version: 1.0*
