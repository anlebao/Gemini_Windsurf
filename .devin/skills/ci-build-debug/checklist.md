# CI Build Debug — Quick Reference Card

## ERROR TAXONOMY (dotnet Release build)

```
CS2001  → Source file not found          → Tạo file thiếu hoặc fix csproj Include
CS0246  → Type/namespace not found       → Missing using / missing class definition
CS0311  → Type constraint violation      → Base class sai, verify inheritance chain
CS0535  → Interface not implemented      → Thiếu method, thêm stub hoặc fix base
CS8600  → Nullable assignment warning    → Thêm null check hoặc null-forgiving context
CS8602  → Dereference of possibly null   → Guard với if (x == null) return
MSB3277 → Assembly version conflict      → Align package versions trong Directory.Packages.props
AD0001  → Analyzer threw exception       → Thường do AnalyzerReleases.Unshipped.md thiếu
```

---

## TRIAGE DECISION TREE

```
CI FAIL
  │
  ├─ Lỗi có trong git HEAD không?
  │     ├─ KHÔNG → Pre-existing bug, fix trước khi tiếp tục
  │     └─ CÓ   → Regression từ commit mới nhất
  │
  ├─ Build Debug pass, Release fail?
  │     └─ CÓ → Nullable analysis / TreatWarningsAsErrors issue
  │              → Kiểm tra: GetMethod(...)!, Expression.Constant không typed
  │
  ├─ AnalyzerReleases.Unshipped.md missing?
  │     └─ CÓ → New-Item file rỗng
  │
  ├─ Base class không tồn tại?
  │     └─ CÓ → Tìm class EF/ASP.NET gốc đúng (DbContext, ControllerBase...)
  │
  └─ Assembly version conflict?
        └─ CÓ → Align versions trong Directory.Build.props / Directory.Packages.props
```

---

## POWERSHELL CHEAT SHEET (Windows dev environment)

```powershell
# Build chỉ 1 project (nhanh hơn full solution)
dotnet build <project>.csproj --configuration Release --no-restore 2>&1 | Select-Object -Last 15

# Lọc chỉ errors, bỏ warnings
dotnet build VanAn.sln --configuration Release 2>&1 |
    Select-String " error " | Select-String -NotMatch "warning"

# Verify file tồn tại
Test-Path "path/to/file"

# Tạo file rỗng (AnalyzerReleases.Unshipped.md)
New-Item -Path "path/AnalyzerReleases.Unshipped.md" -ItemType File -Value ""

# Edit file CRLF-safe (tránh lỗi edit tool trên Windows)
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$new = $content.Replace($old, $new)
[System.IO.File]::WriteAllText($path, $new, [System.Text.Encoding]::UTF8)

# Kiểm tra line ending
if ([System.IO.File]::ReadAllText($path) -match "`r`n") { "CRLF" } else { "LF" }

# Kiểm tra BOM
$b = [System.IO.File]::ReadAllBytes($path)
if ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { "UTF-8 BOM" } else { "No BOM" }
```

---

## DOTNET NULL-SAFE REFLECTION PATTERNS

```csharp
// PATTERN 1: GetMethod với overload tường minh
// Dùng khi method có thể có nhiều overloads (EF.Property, Math.Round, v.v.)
MethodInfo? method = typeof(SomeClass).GetMethod(
    nameof(SomeClass.SomeMethod),
    new[] { typeof(object), typeof(string) });   // chỉ định parameter types

if (method == null)
{
    Trace.TraceError("Method not resolved. Feature disabled.");
    return;
}

MethodInfo closed = method.MakeGenericMethod(typeof(Guid));

// PATTERN 2: Expression.Constant với type tường minh
// Dùng khi Roslyn cần biết exact type trong Release build
var c = Expression.Constant(value, typeof(Guid));   // NOT: Expression.Constant(value)

// PATTERN 3: Production-safe logging trong DbContext
// Console.WriteLine không hoạt động đúng trên Linux CI / production
System.Diagnostics.Trace.TraceError($"[{nameof(MyClass)}] {message}: {ex.Message}");
```

---

## SECURITY RULES (KHÔNG BAO GIỜ VI PHẠM)

- **RULE-SEC-1**: Không disable multi-tenancy query filter trong Release → Data Leakage
- **RULE-SEC-2**: Không dùng `#if DEBUG` để bypass security logic → Security bypass in prod
- **RULE-SEC-3**: Không commit secret / connection string vào git
- **RULE-SEC-4**: Không dùng `--configuration Debug` trên production environment

---

## GIT FLOW CHO CI FIX

```bash
# 1. Xác nhận branch
git branch --show-current

# 2. Kiểm tra diff trước commit
git diff --stat
git status --short

# 3. Stage selective
git add <specific-files>   # KHÔNG dùng git add . khi có bin/obj

# 4. Commit
git commit -m "fix: <short-cause> in <component>

- Root cause: <mô tả>
- Fix: <giải pháp>
- Impact: <test/build result>

Co-Authored-By: Devin <158243242+devin-ai-integration[bot]@users.noreply.github.com>"

# 5. Push
git push origin <branch>

# 6. Monitor CI (sau 30s)
gh run list --limit 3
gh run view <id> --log | Select-String "error|FAILED|passed"
```

---

## THÔNG TIN DỰ ÁN VÂN AN HOLDING ERP

| Key | Value |
|-----|-------|
| Solution | `VanAn.sln` |
| EF Core version | 8.0.8 |
| Target Framework | net8.0 |
| CI Runner | ubuntu-latest |
| Build config (CI) | Release |
| Package management | Central (Directory.Build.props + Directory.Packages.props) |
| Multi-tenancy | `IMustHaveTenant` interface + `ApplyMultiTenancyFilters()` in `VanAnDbContext` |
| TenantId storage type | `Guid` (shadow property via `EF.Property<Guid>`) |
| Branch main | `align-consumer-phase4` |
