// ─────────────────────────────────────────────────────────────────────────────
// RV Smart Entry parser — reflection harness chạy TRONG container shoperp trên VPS.
// Load DLL ĐANG DEPLOY (/app/VanAn.CoreHub.dll) và gọi trực tiếp
// VoucherTextParseService.ParseAsync / ParseMultipleAsync (parser không có HTTP
// surface — nó chạy trong Blazor Server circuit). Cover toàn bộ case trong
// docs/guides/smart-entry-user-guide.md.
//
// Build (local, temp csproj — không commit csproj, xem invoke-rv.ps1):
//   dotnet publish <temp.csproj> -c Release
// Usage (trong container):
//   dotnet RvSmartEntryParser.dll [CoreHub.dll] [cases.json] [report-out.json] [tenantGuid]
// Exit code: 0 = all PASS · 1 = có FAIL · 2 = infra error
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

string dllPath   = args.ElementAtOrDefault(0) ?? "/app/VanAn.CoreHub.dll";
string casesPath = args.ElementAtOrDefault(1) ?? "cases.json";
string reportOut = args.ElementAtOrDefault(2) ?? "rv-smart-entry-report.json";
string tenantS   = args.ElementAtOrDefault(3) ?? "00000000-0000-0000-0000-000000000001";

// ── load deployed DLL (LoadFrom resolves deps from the DLL's own dir — /app) ──
var asm = Assembly.LoadFrom(dllPath);
var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
Console.WriteLine($"DLL : {dllPath}");
Console.WriteLine($"ASM : {asm.GetName().FullName}");
Console.WriteLine($"VER : {infoVer} | built {File.GetLastWriteTimeUtc(dllPath):yyyy-MM-dd HH:mm} UTC");

var svcType = asm.GetType("VanAn.CoreHub.Services.VoucherTextParseService")
    ?? throw new Exception("Type VanAn.CoreHub.Services.VoucherTextParseService not found — deploy khác version?");

var ctor = svcType.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
var svc = ctor.Invoke(Enumerable.Repeat<object?>(null, ctor.GetParameters().Length).ToArray());

var parseMi = svcType.GetMethod("ParseAsync")
    ?? throw new Exception("ParseAsync not found");
var multiMi = svcType.GetMethod("ParseMultipleAsync")
    ?? throw new Exception("ParseMultipleAsync not found");

// TenantId record(Guid Value) — có thể nằm trong CoreHub hoặc VanAn.Shared
var tenantType = asm.GetType("VanAn.Shared.Domain.TenantId")
    ?? Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dllPath))!, "VanAn.Shared.dll"))
         .GetType("VanAn.Shared.Domain.TenantId")
    ?? throw new Exception("TenantId type not found");
var tenant = Activator.CreateInstance(tenantType, Guid.Parse(tenantS))!;

// ── helpers ──────────────────────────────────────────────────────────────────
static object? Prop(object obj, string name) => obj.GetType().GetProperty(name)?.GetValue(obj);
static List<string> ToStrList(object? v)
    => v is IEnumerable e ? e.Cast<object?>().Select(x => x?.ToString() ?? "").ToList() : new();

static JsonObject Project(object p) => new()
{
    ["intent"]         = Prop(p, "Intent")?.ToString(),
    ["amount"]         = Prop(p, "Amount") is decimal a ? (decimal?)a : null,
    ["date"]           = Prop(p, "Date") is DateTime d ? d.ToString("yyyy-MM-dd") : null,
    ["paymentMethod"]  = Prop(p, "PaymentMethod")?.ToString(),
    ["fromAccount"]    = Prop(p, "FromAccountCode")?.ToString(),
    ["toAccount"]      = Prop(p, "ToAccountCode")?.ToString(),
    ["doiTuong"]       = Prop(p, "DoiTuong")?.ToString(),
    ["category"]       = Prop(p, "Category")?.ToString(),
    ["fee"]            = Prop(p, "FeeAmount") is decimal f ? (decimal?)f : null,
    ["confidence"]     = Prop(p, "Confidence")?.ToString(),
    ["missing"]        = new JsonArray(ToStrList(Prop(p, "MissingFields")).Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
    ["warnings"]       = new JsonArray(ToStrList(Prop(p, "Warnings")).Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
};

async Task<object> InvokeParse(MethodInfo mi, string text)
{
    var taskObj = mi.Invoke(svc, new object?[] { tenant, text, CancellationToken.None, false })!;
    await (Task)taskObj;
    return taskObj.GetType().GetProperty("Result")!.GetValue(taskObj)!;
}

static object? GetV(object? parsed, string field) => field switch
{
    "intent"        => Prop(parsed!, "Intent")?.ToString(),
    "amount"        => Prop(parsed!, "Amount") is decimal a ? a : null,
    "fee"           => Prop(parsed!, "FeeAmount") is decimal f ? f : null,
    "paymentMethod" => Prop(parsed!, "PaymentMethod")?.ToString(),
    "fromAccount"   => Prop(parsed!, "FromAccountCode")?.ToString(),
    "toAccount"     => Prop(parsed!, "ToAccountCode")?.ToString(),
    "doiTuong"      => Prop(parsed!, "DoiTuong")?.ToString(),
    "category"      => Prop(parsed!, "Category")?.ToString(),
    "confidence"    => Prop(parsed!, "Confidence")?.ToString(),
    _ => null,
};

// ── run cases ────────────────────────────────────────────────────────────────
var cases = JsonNode.Parse(File.ReadAllText(casesPath))!.AsArray();
var results = new JsonArray();
int pass = 0, fail = 0;
string? lastGroup = null;

foreach (var node in cases)
{
    var c = node!.AsObject();
    string id    = c["id"]!.GetValue<string>();
    string group = c["group"]?.GetValue<string>() ?? "";
    string text  = c["text"]!.GetValue<string>();
    bool multi   = c["multi"]?.GetValue<bool>() ?? false;
    var expect   = c["expect"] as JsonObject;

    if (group != lastGroup) { Console.WriteLine($"\n── {group} ──"); lastGroup = group; }

    var mismatches = new List<string>();
    JsonObject? got = null;
    try
    {
        object parsed;
        int count = 1;
        if (multi)
        {
            var res = await InvokeParse(multiMi, text);
            var list = ((IEnumerable)res).Cast<object>().ToList();
            count = list.Count;
            parsed = list.First();
            if (expect?["resultCount"] is { } rc && rc.GetValue<int>() != count)
                mismatches.Add($"resultCount: expect {rc.GetValue<int>()}, got {count}");
        }
        else parsed = await InvokeParse(parseMi, text);

        got = Project(parsed);
        got["resultCount"] = count;

        if (expect != null)
        {
            foreach (var (key, val) in expect)
            {
                if (key == "resultCount") continue;
                switch (key)
                {
                    case "dateOffsetDays":
                    {
                        int off = val!.GetValue<int>();
                        var want = DateTime.Today.AddDays(off).ToString("yyyy-MM-dd");
                        if (got["date"]?.GetValue<string>() != want)
                            mismatches.Add($"date: expect {want} (offset {off}), got {got["date"]?.GetValue<string>()}");
                        break;
                    }
                    case "dateMD":
                    {
                        var parts = val!.GetValue<string>().Split('/');
                        var d = (DateTime)Prop(parsed, "Date")!;
                        if (d.Day != int.Parse(parts[0]) || d.Month != int.Parse(parts[1]))
                            mismatches.Add($"date: expect day/month {val.GetValue<string>()}, got {got["date"]?.GetValue<string>()}");
                        break;
                    }
                    case "dateISO":
                        if (got["date"]?.GetValue<string>() != val!.GetValue<string>())
                            mismatches.Add($"date: expect {val.GetValue<string>()}, got {got["date"]?.GetValue<string>()}");
                        break;
                    case "missingContains":
                    case "warningContains":
                    {
                        var list = ToStrList(Prop(parsed, key == "missingContains" ? "MissingFields" : "Warnings"));
                        foreach (var s in val!.AsArray())
                        {
                            var needle = s!.GetValue<string>();
                            if (!list.Any(x => x.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                                mismatches.Add($"{key}: '{needle}' not in [{string.Join(" | ", list)}]");
                        }
                        break;
                    }
                    case "doiTuongContains":
                    {
                        var v = Prop(parsed, "DoiTuong")?.ToString() ?? "";
                        var needle = val!.GetValue<string>();
                        if (!v.Contains(needle, StringComparison.OrdinalIgnoreCase))
                            mismatches.Add($"doiTuong: expect contains '{needle}', got '{v}'");
                        break;
                    }
                    default:
                    {
                        var actual = GetV(parsed, key);
                        bool eq;
                        if (actual is decimal dec)
                            eq = val is JsonValue jvd && jvd.TryGetValue<decimal>(out var wd) && dec == wd;
                        else if (val is null)
                            eq = actual is null;
                        else if (val is JsonValue jvs && jvs.TryGetValue<string>(out var ws))
                            eq = string.Equals(actual?.ToString(), ws, StringComparison.Ordinal);
                        else { mismatches.Add($"{key}: unsupported expect type {val}"); break; }
                        if (!eq) mismatches.Add($"{key}: expect {val}, got {actual ?? "null"}");
                        break;
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        mismatches.Add($"EXCEPTION: {ex.InnerException?.Message ?? ex.Message}");
    }

    bool ok = mismatches.Count == 0;
    if (ok) pass++; else fail++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {id} \"{text}\"" +
        (ok ? $" — {got?["intent"]} {got?["amount"]:n0}" : ""));
    foreach (var m in mismatches) Console.WriteLine($"       └ {m}");
    if (!ok && got != null) Console.WriteLine($"       └ got: {got.ToJsonString()}");

    results.Add(new JsonObject
    {
        ["id"] = id, ["group"] = group, ["text"] = text, ["pass"] = ok,
        ["mismatches"] = new JsonArray(mismatches.Select(m => (JsonNode)JsonValue.Create(m)!).ToArray()),
        ["got"] = got,
    });
}

Console.WriteLine("\n════════════════════════════════════════════════════════════");
Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL / {pass + fail} total");
Console.WriteLine("════════════════════════════════════════════════════════════");

File.WriteAllText(reportOut, new JsonObject
{
    ["dll"] = dllPath,
    ["assembly"] = asm.GetName().FullName,
    ["informationalVersion"] = infoVer,
    ["dllLastWriteUtc"] = File.GetLastWriteTimeUtc(dllPath).ToString("o"),
    ["runUtc"] = DateTime.UtcNow.ToString("o"),
    ["tenantId"] = tenantS,
    ["pass"] = pass,
    ["fail"] = fail,
    ["results"] = results,
}.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"report → {reportOut}");
return fail == 0 ? 0 : 1;


