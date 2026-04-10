# VanAn Ecosystem - 100 Orders in 1 Minute Performance Test

## 🎯 Mục tiêu
Test hiệu năng hệ thống với 100 đơn hàng trong 1 phút để kiểm tra khả năng xử lý concurrent requests của VanAn Ecosystem.

## 📋 Yêu cầu hệ thống
- **k6**: Công cụ performance testing (https://k6.io/)
- **Gateway API**: Chạy trên http://localhost:5000
- **SQLite Database**: Có thể xử lý concurrent operations
- **File Logging**: Enabled để ghi log chi tiết

## 🚀 Cách chạy test

### Phương án 1: Dùng batch file (Khuyên nghị)
```bash
# Mở Command Prompt với quyền Administrator
cd 6_Testing
run-100-orders-test.bat
```

### Phương án 2: Dùng k6 trực tiếp
```bash
# Install k6 trước
# Windows: choco install k6
# Mac: brew install k6
# Linux: sudo apt-get install k6

# Chạy test
cd 6_Testing
k6 run --vus 10 --duration 60s load-tests/100-orders-1min.js > logs\100-orders-test.txt 2>&1
```

## 📊 Kịch bản test

### 🎯 Test Parameters
- **Virtual Users**: 10 users đồng thời
- **Duration**: 60 giây
- **Target Orders**: ~100 orders
- **Request Rate**: ~1.67 orders/second
- **Method**: POST http://localhost:5001/api/orders
- **Health Check**: http://localhost:5001/health

### 📈 Test Stages
1. **Ramp-up** (10s): Tăng từ 0 → 10 users
2. **Sustain** (40s): Giữ 10 users ổn định
3. **Ramp-down** (10s): Giảm từ 10 → 0 users

### 🎲 Test Data
- **Products**: 4 loại sản phẩm (Cà phê, Trà, Nước ép)
- **Customers**: Random customer data
- **Orders**: 1-3 items per order
- **Prices**: 20,000 - 35,000 VNĐ

## 📁 Files được tạo

### 📄 Log Files
- `logs\100-orders-test-{timestamp}.txt` - Console output chi tiết
- `logs\100-orders-metrics-{timestamp}.json` - Metrics JSON format
- `logs\gateway-{timestamp}.log` - Gateway service log

### 🔧 Configuration
- `load-tests/100-orders-1min.js` - K6 test script
- `load-tests/k6-config.json` - Test configuration
- `run-100-orders-test.bat` - Batch runner
- `analyze-results.ps1` - Results analyzer

## 📊 Metrics được thu thập

### 🎯 Performance Metrics
- **Order Creation Rate**: % orders thành công
- **Response Time**: Thời gian response trung bình
- **Throughput**: Orders/second
- **Error Rate**: % requests thất bại

### 📈 Response Time Distribution
- **Fast**: < 200ms
- **Medium**: 200-500ms  
- **Slow**: > 500ms

### 💰 Business Metrics
- **Total Revenue**: Tổng doanh thu test
- **Average Order Value**: Giá trị trung bình đơn hàng
- **Success Rate**: Tỷ lệ đơn hàng thành công

## 🔍 Phân tích kết quả

### Chạy analysis script:
```powershell
cd 6_Testing
.\analyze-results.ps1
```

### Manual analysis:
```bash
# Xem console log
type logs\100-orders-test-latest.txt

# Xem JSON metrics
type logs\100-orders-metrics-latest.json

# Filter orders only
findstr "ORDER_TEST:" logs\100-orders-test-latest.txt

# Filter errors only
findstr "ERROR_TEST:" logs\100-orders-test-latest.txt
```

## 🎯 Success Criteria

### ✅ Excellent (Passed all)
- Success Rate ≥ 95%
- Average Response Time < 500ms
- Orders/Second ≥ 1.5

### ⚠️ Good (Passed most)
- Success Rate ≥ 90%
- Average Response Time < 1000ms
- Orders/Second ≥ 1.0

### ❌ Needs Improvement
- Success Rate < 90%
- Average Response Time ≥ 1000ms
- Orders/Second < 1.0

## 🐛 Troubleshooting

### Common Issues:
1. **k6 not found**: Install k6 from https://k6.io/
2. **Gateway not running**: Start Gateway service on port 5001
3. **Database locked**: Ensure SQLite WAL mode enabled
4. **Port conflict**: Check if port 5001 is available

### Debug Commands:
```bash
# Check Gateway health
curl http://localhost:5001/health

# Test single order
curl -X POST http://localhost:5001/api/orders -H "Content-Type: application/json" -d "{\"customerId\":\"test\",\"items\":[{\"productId\":\"coffee-black\",\"quantity\":1}]}"
```

## 📝 Log Format

### Order Success Log:
```
ORDER_TEST: {"timestamp":"2026-04-06T21:30:00.000Z","customerId":"device-abc123","orderId":"order-def456","responseTime":245,"status":200,"success":true,"totalAmount":25000,"itemCount":1}
```

### Error Log:
```
ERROR_TEST: {"timestamp":"2026-04-06T21:30:00.000Z","error":"Connection timeout","stack":"..."}
```

## 🔄 Continuous Testing

### Automation script:
```bash
# Run test every hour
for /l %i in (1,1,24) do (
    echo Running test #%i
    run-100-orders-test.bat
    timeout /t 3600 /nobreak >nul
)
```

### Integration with CI/CD:
```yaml
# GitHub Actions example
- name: Performance Test
  run: |
    cd 6_Testing
    k6 run --vus 10 --duration 60s load-tests/100-orders-1min.js
```

## 📈 Expected Results

### For a healthy system:
- **~100 orders** completed in 60 seconds
- **95%+ success rate**
- **<500ms average response time**
- **<5% error rate**
- **No database deadlocks**

### Performance bottlenecks to watch:
- Database connection pooling
- Concurrent order processing
- Inventory stock updates
- Notification system performance

## 🎉 Next Steps

After running the test:
1. Review results using `analyze-results.ps1`
2. Check for performance bottlenecks
3. Optimize database queries if needed
4. Scale up if response times are high
5. Monitor system resources during test

---

**Ready to test? Run `run-100-orders-test.bat` and watch the magic happen! 🚀**
