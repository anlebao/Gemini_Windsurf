---
name: ci-build-debug
description: "Diagnose & fix CI/CD build failures (dotnet Release, missing files, pre-existing errors). Learned from VanAn Holding ERP sessions."
argument-hint: "[error-type|full]"
allowed-tools:
  - read
  - grep
  - glob
  - exec
triggers:
  - user
  - model
---

# CI Build Debug — Workflow & Runbook

Bạn vừa được kích hoạt để phân tích và sửa lỗi CI build. Thực hiện **tuần tự** các Phase dưới đây, dừng lại và báo cáo nếu phát hiện bất kỳ điều gì đáng chú ý trước khi tiếp tục Phase tiếp theo.

---

## PHASE 1 — TRIAGE: Xác định nguồn gốc lỗi

### 1.1 Đọc CI log gần nhất
```
# Nếu có GitHub Actions:
gh run list --limit 5
gh run view <run-id> --log | tail -100
```
Nếu không có gh CLI, hỏi user paste log.

### 1.2 Phân loại lỗi (theo Priority)
Xác định lỗi thuộc loại nào (có thể nhiều loại cùng lúc):

| Priority | Loại lỗi | Dấu hiệu nhận biết |
|----------|----------|-------------------|
| P0 | Missing source file | `CS2001: Source file could not be found` |
| P0 | Missing type/class | `CS0246: type or namespace not found` |
| P0 | Missing base class | `does not implement interface`, `CS0311` |
| P1 | Nullable analysis (Release) | `CS8600`, `CS8602`, `CS8604` với `TreatWarningsAsErrors=true` |
| P1 | Null-forgiving operator abuse | `GetMethod(...)!` không có null check tường minh |
| P2 | Assembly version conflict | `MSB3277: Found conflicts between different versions` |
| P2 | Analyzer project errors | `AnalyzerReleases.Unshipped.md could not be found` |
| P3 | Pre-existing warnings promoted | warnings trở thành errors trong Release config |

### 1.3 Phân biệt: Pre-existing vs. Regression

Kiểm tra xem lỗi có tồn tại TRƯỚC thay đổi hiện tại không:
```bash
# Lỗi có trong HEAD commit không?
git show HEAD:<file-path> | grep -n "<error-symbol>"

# File có lịch sử trong git không?
git log --all --oneline -- <file-path>

# So sánh build output trước và sau
git stash && dotnet build --configuration Release 2>&1 | tail -20
git stash pop
```

---

## PHASE 2 — ROOT CAUSE ANALYSIS: Điều tra sâu

### 2.1 Kiểm tra cấu hình build
```bash
# EF Core version
cat Directory.Build.props | grep EntityFrameworkCore
cat Directory.Packages.props | grep EntityFrameworkCore

# Nullable & TreatWarningsAsErrors
grep -r "TreatWarningsAsErrors\|Nullable\|WarningsAsErrors" **/*.csproj

# Target framework
grep -r "TargetFramework" **/*.csproj
```

### 2.2 Kiểm tra các pattern nguy hiểm trong code
```bash
# Null-forgiving operator trên Reflection (nguy hiểm trong Release)
grep -rn "GetMethod.*!" **/*.cs
grep -rn "GetProperty.*!" **/*.cs

# Base class / Interface không tồn tại
grep -rn ": Base[A-Z]" **/*.cs  # verify từng class tìm được

# Analyzer release files thiếu
find . -name "AnalyzerReleases.Shipped.md" | while read f; do
  dir=$(dirname "$f")
  if [ ! -f "$dir/AnalyzerReleases.Unshipped.md" ]; then
    echo "MISSING: $dir/AnalyzerReleases.Unshipped.md"
  fi
done
```

### 2.3 Kiểm tra Reflection pattern với EF Core
Nếu lỗi liên quan đến `EF.Property` hoặc dynamic Expression Tree:
```bash
grep -rn "typeof(EF).GetMethod\|GetMethod(nameof(EF" **/*.cs
```

Dấu hiệu nguy hiểm:
- `typeof(EF).GetMethod(nameof(EF.Property))!` — dùng `!` thay vì null check
- `Expression.Constant(value)` — không chỉ định type tường minh
- Không có `if (method == null) return;` guard

---

## PHASE 3 — FIX IMPLEMENTATION

### 3.1 Fix P0: Missing file (AnalyzerReleases.Unshipped.md)
Tạo file rỗng đúng convention:
```bash
# PowerShell
New-Item -Path "<path>/AnalyzerReleases.Unshipped.md" -ItemType File -Value ""
```
File này nên trống (chưa có rules nào chưa được ship).

### 3.2 Fix P0: Missing base class
Nếu `class Foo : BaseFoo(options)` mà `BaseFoo` không tồn tại:
1. Tìm class gốc đúng (thường là `DbContext`, `ControllerBase`, v.v.)
2. Thay thế tường minh, KHÔNG dùng `!` hay workaround

```csharp
// SAI: BaseVanAnDbContext không tồn tại
public class VanAnDbContext(...) : BaseVanAnDbContext(options), IVanAnDbContext

// ĐÚNG: Kế thừa từ DbContext của EF Core
public class VanAnDbContext(...) : DbContext(options), IVanAnDbContext
```

### 3.3 Fix P1: Null-safe Reflection cho EF.Property
Pattern chuẩn — loại bỏ `!`, thêm null check tường minh, type rõ ràng:

```csharp
// SAI — dùng ! che lỗi, fail Nullable Analysis trong Release
System.Reflection.MethodInfo propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!
    .MakeGenericMethod(typeof(Guid));

// ĐÚNG — type-safe, null-guarded, unambiguous cho Roslyn Release
System.Reflection.MethodInfo? efPropertyOpenMethod = typeof(EF).GetMethod(
    nameof(EF.Property),
    new[] { typeof(object), typeof(string) });  // chỉ định overload tường minh

if (efPropertyOpenMethod == null)
{
    System.Diagnostics.Trace.TraceError(
        "Could not resolve EF.Property<TProperty>. Filters NOT applied.");
    return;
}

System.Reflection.MethodInfo genericPropertyMethod =
    efPropertyOpenMethod.MakeGenericMethod(typeof(Guid));
```

Và khi tạo Expression constant:
```csharp
// SAI — type không tường minh, Roslyn có thể inference sai
Expression.Constant(currentTenantId)

// ĐÚNG — type tường minh, unambiguous
Expression.Constant(currentTenantId, typeof(Guid))
```

### 3.4 Fix P3: Logging production-safe
```csharp
// SAI — Console.WriteLine không phù hợp production/Linux CI
Console.WriteLine($"Failed to apply filter: {ex.Message}");

// ĐÚNG — Trace.TraceError hoạt động cả Debug lẫn Release, cả Windows lẫn Linux
System.Diagnostics.Trace.TraceError($"Failed to apply filter to {entityType.ClrType.Name}: {ex.Message}");
```

---

## PHASE 4 — VALIDATION

### 4.1 Build từng project trước (isolation)
```bash
# Build project có lỗi trước, nhanh hơn build toàn solution
dotnet build <project>.csproj --configuration Release --no-restore 2>&1 | tail -20

# Nếu pass, build toàn solution
dotnet build VanAn.sln --configuration Release --no-restore 2>&1 | Select-Object -Last 15

# Lọc chỉ errors (bỏ qua warnings)
dotnet build VanAn.sln --configuration Release 2>&1 | Select-String "error" | Select-String -NotMatch "warning"
```

### 4.2 Kiểm tra Debug và Release đều pass
```bash
dotnet build <project>.csproj --configuration Debug   2>&1 | tail -5
dotnet build <project>.csproj --configuration Release 2>&1 | tail -5
```

### 4.3 Run tests liên quan
```bash
# Tests liên quan đến thay đổi
dotnet test 6_Tests/<TestProject>/ --configuration Release --filter "FullyQualifiedName~<ClassName>"
```

---

## PHASE 5 — COMMIT & PUSH

### 5.1 Kiểm tra diff trước khi commit
```bash
git diff --stat
git status --short
```

### 5.2 Commit với message rõ ràng
```bash
git add <changed-files>
git commit -m "fix: <mô tả ngắn gọn nguyên nhân và giải pháp>

- <bullet 1: lỗi gì, ở đâu>
- <bullet 2: giải pháp>
- <bullet 3: impact>

Generated with Devin CLI

Co-Authored-By: Devin <158243242+devin-ai-integration[bot]@users.noreply.github.com>"
```

### 5.3 Verify CI sau khi push
```bash
git push origin <branch>

# Sau ~30s:
gh run list --limit 3
gh run view <run-id> --log | grep -E "error|Error|FAILED|passed"
```

---

## LESSONS LEARNED (từ VanAn Holding ERP session)

| # | Lesson | Pattern |
|---|--------|---------|
| L1 | CRLF vs LF trên Windows | Edit bằng PowerShell `[IO.File]::ReadAllText` + `.Replace()` thay vì edit tool khi file dùng CRLF |
| L2 | EF.Property Reflection | Luôn dùng `new[] { typeof(object), typeof(string) }` khi `GetMethod` generic |
| L3 | Null-forgiving `!` là nợ kỹ thuật | Mỗi `!` là 1 ticking time bomb cho Release + nullable analyzer |
| L4 | Pre-existing vs Regression | Luôn `git show HEAD:<file>` để confirm lỗi có trước hay do ta tạo ra |
| L5 | Analyzer project cần cả 2 files | `AnalyzerReleases.Shipped.md` + `AnalyzerReleases.Unshipped.md` |
| L6 | Base class phantom | Khi `BaseXxx` không tồn tại → tìm class EF/ASP.NET Core gốc thay thế |
| L7 | Multi-tenancy security | KHÔNG BAO GIỜ disable query filter trong Release — data leakage catastrophe |
| L8 | Console.WriteLine trên Linux CI | Dùng `Trace.TraceError` hoặc injected `ILogger` thay thế |

---

## TÓM TẮT CHECKLIST NHANH

Khi nhận CI failure, chạy checklist này:

- [ ] Đọc CI log — lấy đúng error message
- [ ] Phân loại P0/P1/P2/P3
- [ ] Confirm pre-existing hay regression (`git show HEAD:<file>`)
- [ ] Kiểm tra `AnalyzerReleases.Unshipped.md` tồn tại
- [ ] Kiểm tra base class tồn tại trong codebase
- [ ] Kiểm tra `GetMethod(...)!` pattern → replace bằng null-safe
- [ ] Build project riêng lẻ trước → sau đó full solution
- [ ] Build cả Debug lẫn Release
- [ ] Run tests liên quan
- [ ] Commit message mô tả nguyên nhân + giải pháp
- [ ] Verify CI pass sau push
