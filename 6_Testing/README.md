# 🧪 VanAn Ecosystem - Testing Framework

## 📋 Overview

Flexible testing framework with toggle-based configuration for the VanAn Ecosystem. Supports multiple test tiers from basic health checks to comprehensive chaos testing.

## 🏗️ Architecture

```
6_Testing/
├── .env.test                    # Toggle configuration
├── smoke-tests/                  # Tier 1: Basic health checks
├── e2e-tests/                    # Tier 2: End-to-end workflows
├── load-tests/                   # Tier 3: Performance testing
├── chaos-tests/                  # Tier 4: Resilience testing
├── utils/                        # Shared utilities
├── scripts/                      # Orchestration scripts
├── dashboard/                    # Test results dashboard
├── reports/                      # Generated reports
├── package.json                  # Dependencies
├── playwright.config.ts          # Playwright configuration
└── Dockerfile.quality-gate       # Docker configuration
```

## 🎛️ Toggle Configuration

Edit `.env.test` to control which test tiers run:

```bash
# Tier 1: Smoke Tests (Always ON)
SMOKE_TEST_ENABLED=true
SMOKE_TEST_TIMEOUT=30

# Tier 2: E2E Tests (Optional)
ENABLE_E2E=true
E2E_TEST_TIMEOUT=120

# Tier 3: Load Tests (Default OFF)
ENABLE_LOAD_TEST=false
LOAD_TEST_DURATION=60
LOAD_TEST_VUS=10

# Tier 4: Chaos Tests (Default OFF)
ENABLE_CHAOS=false
CHAOS_TEST_DURATION=300
```

## 🚀 Quick Start

### 1. Install Dependencies
```bash
cd 6_Testing
npm install
npx playwright install
```

### 2. Configure Tests
```bash
# Edit .env.test to enable/disable test tiers
cp .env.test.example .env.test
```

### 3. Run Tests

#### 🚀 One-Click System Test (Windows) - Recommended
Run full system with automatic orchestration:

##### Usage
```bash
run-all.bat smoke   # Core services only
run-all.bat e2e     # Business workflows
run-all.bat load    # Performance test
run-all.bat full    # Full system (recommended before release)
run-all.bat custom  # Use .env.test config
```

##### What it does
- Starts required containers based on mode
- Waits for system readiness
- Runs automated tests
- Outputs results directly to console
- Dumps logs if failure occurs
- Cleans up containers automatically

##### Architecture Mapping
| Mode  | Services |
|-------|----------|
| smoke | postgres, corehub, gateway |
| e2e   | + khachlink, shoperp |
| load  | + nats |
| full  | + redis |

##### Notes
- Docker must be running
- Ports 5001–5003, 5010 must be free
- Use `custom` mode to respect `.env.test`

#### Manual NPM Commands
```bash
# Basic Quality Gate
npm run test:quality-gate

# Full Audit (All Tiers)
npm run test:quality-gate:full

# Individual Tiers
npm run test:smoke      # Smoke tests only
npm run test:e2e        # E2E tests only
npm run test:load       # Load tests only
npm run test:chaos      # Chaos tests only
```

### 4. View Results
```bash
# Open dashboard
npm run dashboard

# Or open directly
open dashboard/index.html
```

## 🎯 Test Tiers

### Tier 1: Smoke Tests 🔥
**Purpose**: Basic health checks to ensure services are running
**Default**: Always ON
**Duration**: ~30 seconds

**Tests**:
- CoreHub health check (Port 5010)
- Gateway health check (Port 5001)
- KhachLink health check (Port 5002)
- ShopERP health check (Port 5003)
- Database connectivity
- NATS messaging

**Exit Criteria**: All services respond with 200 OK

### Tier 2: E2E Tests 🔄
**Purpose**: End-to-end business workflow validation
**Default**: Optional (configurable)
**Duration**: ~2 minutes

**Tests**:
- Customer can view product catalog
- Customer can add items to cart
- Customer can place order
- Staff can view orders in ShopERP
- Staff can update order status
- Order status reflects in customer app
- Inventory updates when order is processed

**Exit Criteria**: All critical user journeys work correctly

### Tier 3: Load Tests ⚡
**Purpose**: Performance testing under load
**Default**: OFF
**Duration**: Configurable (default 60s)

**Scenarios**:
- Health checks under load
- Order creation under load
- Order status checking under load
- Product catalog browsing under load
- Inventory checking under load
- Static assets performance

**Metrics**:
- Response time < 500ms (95th percentile)
- Error rate < 10%
- Concurrent users: Configurable

### Tier 4: Chaos Tests 🔥
**Purpose**: System resilience testing
**Default**: OFF
**Duration**: Configurable (default 300s)

**Scenarios**:
- Network latency injection
- Service failure simulation
- Packet loss simulation
- CPU stress testing
- Memory stress testing

**Metrics**:
- Recovery rate
- Error rate
- System resilience

## 📊 Dashboard

The test dashboard provides real-time visibility into test results:

- **Status Indicators**: Pass/Fail/Skip for each tier
- **Progress Bars**: Visual test completion status
- **Metrics**: Duration, test counts, success rates
- **Configuration**: Current test configuration
- **Auto-refresh**: Updates every 30 seconds

Access at: `http://localhost:8080` (when running with Docker)

## 🐳 Docker Integration

### Quality Gate Service
```bash
# Build and run quality gate
docker-compose -f docker-compose.quality-gate.yml build quality-gate
docker-compose -f docker-compose.quality-gate.yml run quality-gate

# Full audit mode
docker-compose -f docker-compose.quality-gate.yml run quality-gate --full-audit
```

### Individual Test Services
```bash
# Load testing only
docker-compose -f docker-compose.quality-gate.yml --profile load-testing up load-tester

# Chaos testing only
docker-compose -f docker-compose.quality-gate.yml --profile chaos-testing up chaos-engine

# Dashboard only
docker-compose -f docker-compose.quality-gate.yml --profile dashboard up test-dashboard
```

## 📝 Configuration Options

### Environment Variables
```bash
# Test Control
SMOKE_TEST_ENABLED=true          # Enable smoke tests
ENABLE_E2E=true                  # Enable E2E tests
ENABLE_LOAD_TEST=false           # Enable load tests
ENABLE_CHAOS=false               # Enable chaos tests

# Timeouts (seconds)
SMOKE_TEST_TIMEOUT=30
E2E_TEST_TIMEOUT=120
LOAD_TEST_DURATION=60
CHAOS_TEST_DURATION=300

# Load Testing
LOAD_TEST_VUS=10                 # Virtual users
LOAD_TEST_RAMP_UP=10             # Ramp-up time

# Chaos Testing
CHAOS_TEST_LATENCY=true          # Enable latency injection
CHAOS_TEST_FAILURES=false        # Enable failure simulation
CHAOS_INTENSITY=0.3             # Chaos intensity (0.0-1.0)

# Service Endpoints
COREHUB_URL=http://localhost:5010
GATEWAY_URL=http://localhost:5001
KHACHLINK_URL=http://localhost:5002
SHOPERP_URL=http://localhost:5003
```

## 📋 Reports

Test results are generated in multiple formats:

### JSON Reports
- `reports/quality-gate-latest.json` - Latest results
- `reports/quality-gate-report-{timestamp}.json` - Historical reports
- `reports/playwright-report.json` - Playwright detailed report

### HTML Reports
- `reports/playwright-html-report/index.html` - Playwright HTML report
- `reports/quality-gate-dashboard.html` - Quality gate dashboard

### JUnit Reports
- `reports/playwright-junit.xml` - CI/CD integration

## 🔧 Troubleshooting

### Common Issues

#### 1. Playwright Installation
```bash
# Reinstall Playwright browsers
npx playwright install

# Install system dependencies
npx playwright install-deps
```

#### 2. Permission Issues
```bash
# Fix script permissions
chmod +x scripts/quality-gate.js
```

#### 3. Docker Issues
```bash
# Rebuild quality gate image
docker-compose -f docker-compose.quality-gate.yml build --no-cache quality-gate
```

#### 4. Service Connectivity
```bash
# Check if services are running
curl http://localhost:5010/health
curl http://localhost:5001/health
curl http://localhost:5002/health
curl http://localhost:5003/health
```

### Debug Mode

Enable debug logging:
```bash
# Set debug environment variable
export DEBUG=true
npm run test:quality-gate
```

## 🚀 CI/CD Integration

### GitHub Actions Example
```yaml
name: Quality Gate
on: [push, pull_request]

jobs:
  quality-gate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'
          
      - name: Install dependencies
        run: |
          cd 6_Testing
          npm ci
          npx playwright install
          
      - name: Run Quality Gate
        run: |
          cd 6_Testing
          npm run test:quality-gate
          
      - name: Upload Reports
        uses: actions/upload-artifact@v3
        with:
          name: test-reports
          path: 6_Testing/reports/
```

## 📚 Best Practices

### 1. Test Configuration
- Keep smoke tests always enabled
- Enable E2E tests for critical deployments
- Use load tests for performance validation
- Run chaos tests in staging environments

### 2. Test Data
- Use deterministic test data
- Clean up test data after each run
- Isolate test environments

### 3. Monitoring
- Monitor test execution time
- Track test success rates
- Alert on test failures

### 4. Maintenance
- Regularly update test dependencies
- Review test configurations
- Maintain test documentation

## 🤝 Contributing

1. Add new test scenarios to appropriate tiers
2. Update configuration options as needed
3. Improve dashboard visualizations
4. Add new chaos scenarios
5. Enhance error handling

## 📞 Support

For issues and questions:
- Check troubleshooting section
- Review configuration options
- Examine test logs in reports directory
- Contact the development team

---

*VanAn Ecosystem Testing Framework v1.0.0*
