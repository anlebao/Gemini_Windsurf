@echo off
echo VanAn Ecosystem - Network Issues Fix
echo =====================================
echo.

echo Checking npm configuration...
echo.

REM Check if npm is available
npm --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: npm not found. Please install Node.js first.
    pause
    exit /b 1
)

echo npm version:
npm --version
echo.

echo Current npm proxy settings:
echo ==========================
npm config get proxy
npm config get https-proxy
npm config get registry
echo.

echo Checking network connectivity...
echo ==========================
ping registry.npmjs.org -n 2 >nul
if %errorlevel% equ 0 (
    echo ✅ registry.npmjs.org is reachable
) else (
    echo ❌ registry.npmjs.org is NOT reachable
    echo This might be a network/firewall issue.
)

echo.
echo Attempting to fix common network issues...
echo ==========================

echo [1/4] Clearing npm cache...
npm cache clean --force

echo.
echo [2/4] Setting npm registry to default...
npm config set registry https://registry.npmjs.org/

echo.
echo [3/4] Removing proxy settings (if any)...
npm config delete proxy
npm config delete https-proxy

echo.
echo [4/4] Setting timeout values...
npm config set timeout 60000
npm config set fetch-timeout 60000
npm config set fetch-retry-mintimeout 20000
npm config set fetch-retry-maxtimeout 120000

echo.
echo Configuration updated. Current settings:
echo =====================================
npm config list
echo.

echo Now trying to install packages...
echo ==========================
echo.

echo Running: npm install
npm install

if %errorlevel% equ 0 (
    echo.
    echo ✅ npm install successful!
    echo.
    echo Next step: Install Playwright browsers
    echo Command: npx playwright install
) else (
    echo.
    echo ❌ npm install still failing.
    echo.
    echo Alternative solutions:
    echo 1. Check your internet connection
    echo 2. Try using VPN
    echo 3. Use npm mirror: npm config set registry https://registry.npmmirror.com/
    echo 4. Use Docker: docker-compose -f docker-compose.quality-gate.yml run quality-gate
    echo 5. Install packages manually from different network
)

echo.
pause
