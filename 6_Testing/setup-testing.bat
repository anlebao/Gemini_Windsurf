@echo off
echo VanAn Ecosystem - Testing Framework Setup
echo ========================================
echo.

REM Check Node.js installation
echo Checking Node.js installation...
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Node.js is not installed!
    echo Please install Node.js first:
    echo 1. Download from: https://nodejs.org/
    echo 2. Install LTS version
    echo 3. Restart terminal
    echo 4. Run this script again
    pause
    exit /b 1
)

echo Node.js found:
node --version
echo npm version:
npm --version
echo.

REM Install dependencies
echo Installing testing framework dependencies...
echo.

echo [1/4] Installing npm packages...
npm install
if %errorlevel% neq 0 (
    echo ERROR: npm install failed!
    pause
    exit /b 1
)

echo.
echo [2/4] Installing Playwright browsers...
npx playwright install
if %errorlevel% neq 0 (
    echo ERROR: Playwright installation failed!
    pause
    exit /b 1
)

echo.
echo [3/4] Installing Playwright system dependencies...
npx playwright install-deps
if %errorlevel% neq 0 (
    echo WARNING: Playwright system dependencies installation failed!
    echo This might be okay on Windows.
)

echo.
echo [4/4] Creating reports directory...
if not exist reports mkdir reports
echo Reports directory created.

echo.
echo ========================================
echo ✅ Testing Framework Setup Complete!
echo ========================================
echo.
echo Next steps:
echo 1. Edit .env.test to configure test tiers
echo 2. Run: npm run test:quality-gate
echo 3. View dashboard: npm run dashboard
echo.
echo Available commands:
echo - npm run test:smoke     (Tier 1 only)
echo - npm run test:e2e       (Tier 2 only)
echo - npm run test:quality-gate (Based on .env.test)
echo - npm run test:quality-gate:full (All tiers)
echo.
pause
