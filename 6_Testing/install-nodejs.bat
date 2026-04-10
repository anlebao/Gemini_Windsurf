@echo off
echo Installing Node.js for VanAn Ecosystem Testing Framework...
echo.

REM Check if Node.js is already installed
node --version >nul 2>&1
if %errorlevel% equ 0 (
    echo Node.js is already installed:
    node --version
    echo.
    echo npm version:
    npm --version
    echo.
    echo Installation not needed.
    pause
    exit /b 0
)

echo Node.js not found. Please install Node.js manually:
echo.
echo 1. Download Node.js from: https://nodejs.org/
echo 2. Download the LTS version (recommended)
echo 3. Run the installer
echo 4. Restart your terminal/command prompt
echo.
echo After installation, run this script again to verify.
echo.
echo Opening Node.js download page...
start https://nodejs.org/en/download/
echo.
pause
