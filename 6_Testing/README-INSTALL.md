# 🚀 VanAn Ecosystem - Testing Framework Installation Guide

## ⚠️ Prerequisites

You need **Node.js** installed to run the testing framework.

## 📦 Installation Steps

### 1. Install Node.js (Required)

#### Option A: Download from Official Site
1. Visit: **https://nodejs.org/**
2. Download **LTS** version (recommended v20.x)
3. Run the installer
4. Restart your terminal/command prompt

#### Option B: Using Package Manager
```bash
# Using Chocolatey (Windows)
choco install nodejs-lts

# Using Scoop (Windows)
scoop install node

# Using Homebrew (macOS)
brew install node
```

### 2. Verify Installation
```bash
node --version
npm --version
```

Expected output:
```
v20.12.2
10.5.0
```

### 3. Setup Testing Framework
```bash
cd 6_Testing
npm install
npx playwright install
```

### 4. Run First Test
```bash
npm run test:smoke
```

## 🔧 Quick Setup Script

I've created a helper script for Windows users:

```bash
cd 6_Testing
setup-testing.bat
```

This script will:
- ✅ Check Node.js installation
- ✅ Install npm dependencies  
- ✅ Install Playwright browsers
- ✅ Create reports directory
- ✅ Provide next steps

## 🚨 Troubleshooting

### Issue: 'npm' is not recognized
**Solution**: Install Node.js first (step 1)

### Issue: Playwright browser installation fails
**Solution**: 
```bash
npx playwright install-deps
# Or install manually
npx playwright install chromium
npx playwright install firefox
npx playwright install webkit
```

### Issue: Permission errors on Windows
**Solution**: Run terminal as Administrator

### Issue: Slow browser download
**Solution**: Set environment variable:
```bash
set PLAYWRIGHT_BROWSERS_PATH=0
npm install
npx playwright install
```

## 🎯 After Installation

Once Node.js is installed, you can:

1. **Configure tests**: Edit `.env.test`
2. **Run smoke tests**: `npm run test:smoke`
3. **Run quality gate**: `npm run test:quality-gate`
4. **View dashboard**: `npm run dashboard`

## 📚 Available Commands

```bash
# Individual test tiers
npm run test:smoke      # Tier 1: Health checks
npm run test:e2e        # Tier 2: End-to-end
npm run test:load       # Tier 3: Performance
npm run test:chaos      # Tier 4: Resilience

# Quality gate orchestration
npm run test:quality-gate          # Based on .env.test
npm run test:quality-gate:full     # All tiers enabled

# Utilities
npm run dashboard        # Open test dashboard
npm run clean:reports    # Clean old reports
```

## 🐳 Docker Alternative

If you prefer Docker (no Node.js needed locally):

```bash
# Build and run quality gate
docker-compose -f docker-compose.quality-gate.yml build quality-gate
docker-compose -f docker-compose.quality-gate.yml run quality-gate

# With dashboard
docker-compose -f docker-compose.quality-gate.yml --profile dashboard up test-dashboard
```

## 📞 Support

If you still have issues:

1. **Check Node.js version**: Must be v18+ (v20+ recommended)
2. **Clear npm cache**: `npm cache clean --force`
3. **Reinstall**: Delete `node_modules` and run `npm install` again
4. **Check permissions**: Ensure write access to `6_Testing` directory

---

**Happy Testing! 🧪**
