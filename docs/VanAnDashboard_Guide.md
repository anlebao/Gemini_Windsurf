# VanAn Dashboard - Real-time System Overview

## 🎯 Overview

VanAn Dashboard là một giao diện real-time chuyên nghiệp để hiển thị và theo dõi toàn bộ hệ thống VanAn Ecosystem, bao gồm PostgreSQL Central Hub và SQLite Edge Nodes.

## 🚀 Features

### 📊 Real-time Metrics
- **PostgreSQL Central Hub**: Tenant count, total orders, revenue, sync rate
- **SQLite Edge Nodes**: Local orders, sync status, WAL mode verification
- **Auto-refresh**: Cập nhật dữ liệu tự động mỗi 30 giây
- **Health Monitoring**: Kiểm tra trạng thái hệ thống real-time

### 🎨 Professional UI Design
- **Modern Design**: Material Design inspired với gradient colors
- **Responsive Layout**: Tương thích mọi kích thước màn hình
- **Interactive Charts**: Progress bars, status indicators, animations
- **Color-coded Alerts**: Visual feedback cho system health

### 📈 Data Visualization
- **Growth Metrics**: Percentage changes so với các kỳ trước
- **Sync Progress**: Real-time sync status với progress bars
- **Connection Status**: Live indicators cho service availability
- **Performance Trends**: Historical data comparisons

## 🛠️ Technical Implementation

### Architecture
```
VanAnDashboard.razor
├── IDashboardService (Backend Service)
├── PostgreSQL Metrics (Central Hub)
├── SQLite Metrics (Edge Nodes)
└── Real-time Updates (Timer-based)
```

### Data Sources

#### PostgreSQL (Central Hub)
- **Tenant Count**: `SELECT COUNT(DISTINCT TenantId) FROM Orders`
- **Total Orders**: `SELECT COUNT(*) FROM Orders WHERE TenantId IS NOT NULL`
- **Total Revenue**: `SELECT SUM(TotalAmount) FROM Orders WHERE TenantId IS NOT NULL`
- **Sync Rate**: Percentage of orders với LastSyncedAt không null

#### SQLite (Edge Nodes)
- **KhachLink Database**: `5_WebApps/KhachLink/vanan_khachlink.db`
- **ShopERP Database**: `5_WebApps/ShopERP/vanan_shoperp.db`
- **WAL Mode Check**: `PRAGMA journal_mode`
- **Local Orders**: `SELECT COUNT(*) FROM Orders`

### Services Integration

#### DashboardService.cs
```csharp
public interface IDashboardService
{
    Task<DashboardMetrics> GetPostgreSQLMetricsAsync();
    Task<SQLiteMetrics> GetSQLiteMetricsAsync(string nodeType);
    Task<SyncStatus> GetSyncStatusAsync();
    Task<SystemHealth> GetSystemHealthAsync();
}
```

## 📱 Access Dashboard

### Navigation
1. Mở KhachLink application: `http://localhost:5002`
2. Click vào **Dashboard** trong navigation menu
3. Hoặc truy cập trực tiếp: `http://localhost:5002/VanAnDashboard`

### Requirements
- KhachLink app đang chạy
- DashboardService đã được register trong DI container
- PostgreSQL và SQLite databases accessible

## 🎨 UI Components

### Header Section
- **Gradient Background**: Purple to blue gradient
- **System Title**: "VanAn Dashboard - Real-time System Overview"
- **Status Indicators**: Live connection status

### Metrics Cards
- **PostgreSQL Cards**: Blue theme, central hub metrics
- **SQLite Cards**: Navy theme, edge node metrics
- **Sync Cards**: Green/Yellow/Red based on status
- **Hover Effects**: Smooth transitions and shadows

### Progress Indicators
- **Sync Progress**: Visual progress bars cho sync rates
- **Color Coding**: Green (>90%), Yellow (70-90%), Red (<70%)
- **Animations**: Smooth fill transitions

### Alert System
- **Connection Issues**: Warning alerts cho service failures
- **Sync Problems**: Notifications cho pending sync items
- **System Health**: Overall system status indicators

## 📊 Metrics Explained

### PostgreSQL Central Hub Metrics

#### Tenant Count
- **Description**: Số lượng tenant hoạt động trong hệ thống
- **Growth**: So sánh với tháng trước
- **Importance**: Business expansion indicator

#### Total Orders
- **Description**: Tổng số orders toàn hệ thống
- **Growth**: So sánh với ngày trước
- **Importance**: Business activity metric

#### Total Revenue
- **Description**: Tổng doanh thu toàn hệ thống
- **Growth**: So sánh với tuần trước
- **Importance**: Financial performance

#### System Sync Rate
- **Description**: Percentage orders đã sync từ edge nodes
- **Threshold**: >90% = Healthy, <80% = Needs attention
- **Importance**: Data consistency indicator

### SQLite Edge Nodes Metrics

#### Local Orders
- **Description**: Orders stored trong local SQLite databases
- **KhachLink**: Customer-facing orders
- **ShopERP**: Staff management orders
- **Importance**: Offline capability indicator

#### Pending Sync
- **Description**: Orders chờ đồng bộ lên central hub
- **Threshold**: <50 = Good, >100 = Warning
- **Importance**: Sync health indicator

#### WAL Mode
- **Description**: Write-Ahead Logging performance mode
- **Status**: ON = Optimized, OFF = Disabled
- **Importance**: Database performance indicator

## 🔄 Real-time Updates

### Auto-refresh Mechanism
- **Interval**: 30 seconds
- **Background Timer**: Non-blocking updates
- **Loading States**: Visual feedback during refresh
- **Error Handling**: Graceful fallback on connection issues

### Manual Refresh
- **Refresh Button**: Force immediate data update
- **Loading Spinner**: Visual feedback during refresh
- **Disabled State**: Prevent multiple simultaneous refreshes

## 🛠️ Configuration

### Service Registration
```csharp
// In Program.cs
builder.Services.AddScoped<IDashboardService, DashboardService>();
```

### Database Paths
```csharp
// KhachLink: 5_WebApps/KhachLink/vanan_khachlink.db
// ShopERP: 5_WebApps/ShopERP/vanan_shoperp.db
```

### Connection Strings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=vanan_khachlink.db"
  }
}
```

## 🔧 Troubleshooting

### Common Issues

#### Dashboard Not Loading
- **Check**: KhachLink app đang chạy
- **Verify**: DashboardService registered trong DI
- **Solution**: Restart KhachLink application

#### PostgreSQL Connection Failed
- **Check**: PostgreSQL service status
- **Verify**: Connection string trong appsettings
- **Solution**: Ensure PostgreSQL accessible

#### SQLite Database Not Found
- **Check**: Database file paths trong DashboardService
- **Verify**: File permissions và existence
- **Solution**: Create databases hoặc run app initialization

#### Sync Data Not Updating
- **Check**: LastSyncedAt values trong databases
- **Verify**: Sync service đang hoạt động
- **Solution**: Trigger manual sync hoặc restart sync services

### Debug Mode
```csharp
// Enable detailed logging
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

## 📈 Performance Optimization

### Database Queries
- **Indexed Columns**: TenantId, CreatedAt, LastSyncedAt
- **Query Optimization**: Efficient COUNT và SUM operations
- **Connection Pooling**: Reuse database connections

### Frontend Optimization
- **Component Caching**: Cache static data between refreshes
- **Lazy Loading**: Load data on-demand
- **Minimal Re-renders**: Optimize StateHasChanged calls

### Network Optimization
- **Async Operations**: Non-blocking database calls
- **Error Handling**: Graceful degradation
- **Timeout Management**: Prevent hanging requests

## 🎨 Customization

### Branding
- **Colors**: Modify CSS variables cho theme colors
- **Logo**: Replace VanAn branding elements
- **Typography**: Adjust font families và sizes

### Metrics
- **Add New Metrics**: Extend DashboardService interface
- **Custom Calculations**: Add business-specific metrics
- **External APIs**: Integrate additional data sources

### Layout
- **Grid System**: Adjust responsive breakpoints
- **Card Layout**: Modify metrics grid arrangement
- **Navigation**: Add menu items hoặc sections

## 🔒 Security Considerations

### Data Access
- **Authentication**: Require login cho dashboard access
- **Authorization**: Role-based access control
- **Data Filtering**: Tenant-specific data isolation

### Privacy
- **Sensitive Data**: Mask confidential information
- **Audit Trail**: Log dashboard access
- **Data Retention**: Limit historical data storage

## 🚀 Future Enhancements

### Planned Features
- **Historical Trends**: Long-term data visualization
- **Export Functionality**: Download reports (PDF, Excel)
- **Alert Notifications**: Email/SMS alerts cho critical issues
- **Mobile App**: Native mobile dashboard app

### Advanced Analytics
- **Predictive Analytics**: AI-powered insights
- **Anomaly Detection**: Automatic issue identification
- **Performance Benchmarking**: Industry comparisons
- **Custom Dashboards**: User-configurable views

---

**🎉 VanAn Dashboard is ready for production use! Access it via the navigation menu in KhachLink app.**
