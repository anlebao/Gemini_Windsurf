import sys

path = r'C:\VibeCoding\Gemini_Windsurf\run-accounting-full.bat'
with open(path, 'rb') as f:
    content = f.read().decode('utf-8')

fixes = 0

# Fix 1: Step 1 - chỉ target VanAn.Core.Tests project, dùng Release, bỏ cd 6_Tests
old1 = (
    'echo \U0001f4e6 Step 1: Unit/Integration Tests (6_Tests)...\r\n'
    'cd 6_Tests\r\n'
    'dotnet test --filter "FullyQualifiedName~Accounting"\r\n'
    'if %errorlevel% neq 0 (\r\n'
    '    echo \u274c Unit/Integration tests failed\r\n'
    '    cd ..\r\n'
    '    exit /b 1\r\n'
    ')\r\n'
    'echo \u2705 Unit/Integration tests passed\r\n'
    'cd ..'
)
new1 = (
    'echo \U0001f4e6 Step 1: Unit/Integration Tests (6_Tests)...\r\n'
    'dotnet test "6_Tests\\VanAn.Core.Tests\\VanAn.Core.Tests.csproj" --configuration Release --filter "FullyQualifiedName~Accounting"\r\n'
    'if %errorlevel% neq 0 (\r\n'
    '    echo \u274c Unit/Integration tests failed\r\n'
    '    exit /b 1\r\n'
    ')\r\n'
    'echo \u2705 Unit/Integration tests passed'
)

# Fix 2: Step 2 - Debug -> Release
old2 = 'dotnet build VanAn.ShopERP.csproj --configuration Debug -nologo -v q'
new2 = 'dotnet build VanAn.ShopERP.csproj --configuration Release -nologo -v q'

# Fix 3: Step 3 - thêm --configuration Release vào dotnet run
old3 = 'dotnet run --no-build --no-launch-profile > ..\\..\\shoperp.log 2>&1"'
new3 = 'dotnet run --no-build --no-launch-profile --configuration Release > ..\\..\\shoperp.log 2>&1"'

if old1 in content:
    content = content.replace(old1, new1)
    print('Fix 1 applied: Step 1 - target specific project + Release')
    fixes += 1
else:
    print('Fix 1 NOT FOUND - searching fragments...')
    if 'cd 6_Tests' in content:
        print('  Found: cd 6_Tests')
    if 'dotnet test --filter' in content:
        print('  Found: dotnet test --filter')

if old2 in content:
    content = content.replace(old2, new2)
    print('Fix 2 applied: Step 2 - Debug -> Release')
    fixes += 1
else:
    print('Fix 2 NOT FOUND')

if old3 in content:
    content = content.replace(old3, new3)
    print('Fix 3 applied: Step 3 - add --configuration Release to dotnet run')
    fixes += 1
else:
    print('Fix 3 NOT FOUND')

with open(path, 'wb') as f:
    f.write(content.encode('utf-8'))
print(f'Done. {fixes}/3 fixes applied.')
