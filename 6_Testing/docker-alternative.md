# 🐳 Docker Alternative - No Node.js Required

## 🚀 Quick Start with Docker

Nếu bạn gặp vấn đề cài đặt Node.js hoặc npm, có thể sử dụng Docker để chạy testing framework.

### **Option 1: Quality Gate với Docker**
```bash
# Build và chạy quality gate
docker-compose -f docker-compose.quality-gate.yml build quality-gate
docker-compose -f docker-compose.quality-gate.yml run quality-gate

# Full audit mode
docker-compose -f docker-compose.quality-gate.yml run quality-gate --full-audit
```

### **Option 2: Chỉ chạy Smoke Tests**
```bash
docker-compose -f docker-compose.quality-gate.yml run quality-gate
```

### **Option 3: Với Dashboard**
```bash
# Chạy dashboard riêng
docker-compose -f docker-compose.quality-gate.yml --profile dashboard up test-dashboard

# Truy cập: http://localhost:8080
```

### **Option 4: Load Testing riêng**
```bash
docker-compose -f docker-compose.quality-gate.yml --profile load-testing up load-tester
```

### **Option 5: Chaos Testing riêng**
```bash
docker-compose -f docker-compose.quality-gate.yml --profile chaos-testing up chaos-engine
```

## 📋 Configuration

Edit `.env.test` để control test tiers:
```bash
SMOKE_TEST_ENABLED=true
ENABLE_E2E=true
ENABLE_LOAD_TEST=false
ENABLE_CHAOS=false
```

## 🎯 Ưu điểm Docker:
- ✅ Không cần cài Node.js local
- ✅ Không cần npm
- ✅ Môi trường nhất quán
- ✅ Dễ dàng deploy
- ✅ Tự động cài dependencies

## 📊 Xem Results
```bash
# Reports được lưu trong:
ls 6_Testing/reports/

# Hoặc xem dashboard:
open http://localhost:8080
```

## 🔧 Troubleshooting Docker
```bash
# Rebuild nếu có thay đổi
docker-compose -f docker-compose.quality-gate.yml build --no-cache quality-gate

# Xóa containers cũ
docker-compose -f docker-compose.quality-gate.yml down -v
```

Đây là giải pháp nhanh nhất nếu bạn gặp vấn đề với Node.js/npm cài đặt!
