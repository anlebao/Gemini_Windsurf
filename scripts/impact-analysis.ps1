#!/usr/bin/env pwsh
<#
.SYNOPSIS
    VanAn change-impact analysis. Single source of truth: deployment-impact.yml.

.DESCRIPTION
    Computes deploy/build/verification sets for a set of changed files.
    SAFETY RULES (non-negotiable):
      1. UNKNOWN path -> FULL build/deploy/RV. Never default to scoped.
      2. No-downgrade: -RequestedScope smaller than the impact set -> BLOCK (exit 1).
      3. -ForceFull escalation is always allowed.
    False positive (deploy extra) is ALWAYS preferred over false negative (missed dependency).

.PARAMETER ChangedFiles
    Explicit list of changed repo-relative paths (forward slashes; backslashes normalized).

.PARAMETER DiffBase
    Git ref to diff against: `git diff --name-only <DiffBase> HEAD`. Ignored when -ChangedFiles is provided.

.PARAMETER ConfigPath
    Path to deployment-impact.yml (default: <repo-root>/deployment-impact.yml).

.PARAMETER ForceFull
    Escalate to FULL build/deploy/RV regardless of classification.

.PARAMETER RequestedScope
    Comma-separated app names the caller wants to deploy (e.g. "shoperp").
    If the computed deploy set is NOT a subset of the scope -> BLOCK (exit 1).

.PARAMETER Json
    Emit machine-readable JSON to stdout (top-level execution only).

.EXAMPLE
    ./scripts/impact-analysis.ps1 -ChangedFiles "5_WebApps/ShopERP/Components/Pages/Home.razor" -Json
.EXAMPLE
    ./scripts/impact-analysis.ps1 -DiffBase origin/main -Json
#>
[CmdletBinding()]
param(
    [string[]]$ChangedFiles,
    [string]$DiffBase = "",
    [string]$ConfigPath = "",
    [switch]$ForceFull,
    [string]$RequestedScope = "",
    [switch]$Json
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

# ============================================================
# GLOB MATCHER
#   *  = any chars EXCEPT '/'   (cannot cross directory boundary)
#   ** = any chars INCLUDING '/' (crosses directories)
#   Patterns match repo-relative paths (forward slashes).
#   A pattern without '/' naturally anchors to repo root.
# ============================================================
function ConvertTo-GlobRegex {
    param([string]$Pattern)
    $p = $Pattern.Trim().TrimStart('/')
    if ($p -eq "") { throw "Empty glob pattern." }
    $sb = [System.Text.StringBuilder]::new('^')
    $i = 0
    while ($i -lt $p.Length) {
        $c = $p[$i]
        if ($c -eq '*') {
            if ($i + 1 -lt $p.Length -and $p[$i + 1] -eq '*') {
                if ($i + 2 -lt $p.Length -and $p[$i + 2] -eq '/') {
                    [void]$sb.Append('(?:.*/)?'); $i += 3; continue
                } else {
                    [void]$sb.Append('.*'); $i += 2; continue
                }
            } else {
                [void]$sb.Append('[^/]*'); $i += 1; continue
            }
        }
        elseif ($c -eq '?') { [void]$sb.Append('[^/]'); $i += 1; continue }
        elseif ($c -match '[.\[\]()+\^${}|\\]') { [void]$sb.Append('\' + $c); $i += 1; continue }
        else { [void]$sb.Append($c); $i += 1 }
    }
    [void]$sb.Append('$')
    return $sb.ToString()
}

function Test-GlobMatch {
    param([string]$Path, [string]$Pattern)
    $regex = ConvertTo-GlobRegex -Pattern $Pattern
    return [regex]::IsMatch($Path, $regex)
}

function Test-GlobMatchAny {
    param([string]$Path, [object]$Patterns)
    foreach ($p in @($Patterns)) {
        if (Test-GlobMatch -Path $Path -Pattern ([string]$p)) { return $true }
    }
    return $false
}

# ============================================================
# CONSTRAINED YAML SUBSET PARSER
#   Supports: nested maps by indentation, "- item" lists,
#   "- key: value" list items, inline "[a, b]" lists, quoted scalars.
#   Full-line comments (#). Tabs are rejected.
# ============================================================
function ConvertFrom-ImpactYaml {
    param([string[]]$Lines)

    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $Lines) {
        $trimmed = $line.TrimEnd()
        if ($trimmed -match '^\s*#') { continue }
        if ($trimmed -match '^\s*$') { continue }
        if ($trimmed -match '\t') { throw "Tab indentation is not allowed in deployment-impact.yml: '$line'" }
        $indent = $line.Length - $line.TrimStart().Length
        $items.Add([PSCustomObject]@{ Indent = $indent; Text = $trimmed.Trim() })
    }
    if ($items.Count -eq 0) { throw "deployment-impact.yml is empty." }

    $script:yamlIdx = 0
    $script:yamlItems = $items

    function Convert-Scalar {
        param([string]$Value)
        $v = $Value.Trim()
        if ($v -match '^\[(.*)\]$') {
            $inner = $Matches[1]
            if ($inner.Trim() -eq "") { return ,@() }
            $parts = $inner -split ','
            $out = @()
            foreach ($part in $parts) {
                $p = $part.Trim()
                if ($p.Length -ge 2 -and (($p[0] -eq '"' -and $p[-1] -eq '"') -or ($p[0] -eq "'" -and $p[-1] -eq "'"))) {
                    $p = $p.Substring(1, $p.Length - 2)
                }
                $out += $p
            }
            return ,$out
        }
        if ($v.Length -ge 2 -and (($v[0] -eq '"' -and $v[-1] -eq '"') -or ($v[0] -eq "'" -and $v[-1] -eq "'"))) {
            return $v.Substring(1, $v.Length - 2)
        }
        return $v
    }

    function Parse-ChildBlock {
        param([int]$ParentIndent)
        # The value of 'key:' is the block starting on the next line.
        # YAML indentation is RELATIVE: the block indent = indent of the first child line.
        if ($script:yamlIdx -ge $script:yamlItems.Count) { return "" }
        $next = $script:yamlItems[$script:yamlIdx]
        if ($next.Indent -le $ParentIndent) { return "" }
        if ($next.Text -match '^-\s') { return ,(Parse-ListBlock $next.Indent) }
        return ,(Parse-MapBlock $next.Indent)
    }

    function Parse-ListBlock {
        param([int]$Indent)
        $list = [System.Collections.Generic.List[object]]::new()
        while ($script:yamlIdx -lt $script:yamlItems.Count) {
            $it = $script:yamlItems[$script:yamlIdx]
            if ($it.Indent -lt $Indent) { break }
            if ($it.Indent -gt $Indent) { throw "Unexpected indentation at line: '$($it.Text)'" }
            if ($it.Text -notmatch '^-\s+(.+)$') { throw "Expected list item at line: '$($it.Text)'" }
            $script:yamlIdx++
            $content = $Matches[1]
            if ($content -match '^([^:]+):\s*(.*)$') {
                $key = $Matches[1].Trim()
                $val = $Matches[2].Trim()
                $map = [ordered]@{}
                if ($val -eq "") {
                    $map[$key] = Parse-ChildBlock $it.Indent
                } else {
                    $map[$key] = Convert-Scalar $val
                }
                # Merge continuation map entries (siblings of this list item, e.g. "affects:", "reason:")
                # Discard the returned map: it IS $map (MergeInto) — otherwise it leaks into the output stream.
                while ($script:yamlIdx -lt $script:yamlItems.Count -and $script:yamlItems[$script:yamlIdx].Indent -gt $it.Indent) {
                    $null = Parse-MapBlock $script:yamlItems[$script:yamlIdx].Indent $map
                }
                $list.Add($map)
            } else {
                $list.Add((Convert-Scalar $content))
            }
        }
        return ,$list.ToArray()
    }

    function Parse-MapBlock {
        param([int]$Indent, [System.Collections.Specialized.OrderedDictionary]$MergeInto)
        $map = if ($MergeInto) { $MergeInto } else { [ordered]@{} }
        while ($script:yamlIdx -lt $script:yamlItems.Count) {
            $it = $script:yamlItems[$script:yamlIdx]
            if ($it.Indent -lt $Indent) { break }
            if ($it.Indent -gt $Indent) { throw "Unexpected indentation at line: '$($it.Text)'" }
            if ($it.Text -match '^-\s') { throw "List item where map expected at line: '$($it.Text)'" }
            if ($it.Text -notmatch '^([^:]+):\s*(.*)$') { throw "Cannot parse line: '$($it.Text)'" }
            $script:yamlIdx++
            $key = $Matches[1].Trim()
            $val = $Matches[2].Trim()
            if ($val -eq "") {
                $map[$key] = Parse-ChildBlock $it.Indent
            } else {
                $map[$key] = Convert-Scalar $val
            }
        }
        return $map
    }

    return Parse-MapBlock 0
}

# ============================================================
# CORE
# ============================================================
function Invoke-ImpactAnalysis {
    param(
        [string[]]$ChangedFiles,
        [string]$DiffBase = "",
        [string]$ConfigPath = "",
        [switch]$ForceFull,
        [string]$RequestedScope = ""
    )

    # ---- resolve repo root + config ----
    # $PSScriptRoot inside functions = directory of the defining script (works when dot-sourced)
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    if (-not $ConfigPath) { $ConfigPath = Join-Path $repoRoot "deployment-impact.yml" }
    if (-not (Test-Path $ConfigPath)) { throw "Config not found: $ConfigPath" }
    $cfg = ConvertFrom-ImpactYaml (Get-Content $ConfigPath -Encoding UTF8)

    # ---- validate config ----
    if ($cfg['version'] -ne "1") { throw "deployment-impact.yml: unsupported version '$($cfg['version'])' (expected 1)." }
    if (-not $cfg.Contains('apps')) { throw "deployment-impact.yml: missing 'apps'." }
    $appNames = @()
    foreach ($k in $cfg['apps'].Keys) {
        $v = $cfg['apps'][$k]
        if (-not $v.Contains('image')) { throw "deployment-impact.yml: app '$k' missing 'image'." }
        if (-not $v.Contains('paths')) { throw "deployment-impact.yml: app '$k' missing 'paths'." }
        $appNames += [string]$k
    }
    foreach ($d in @($cfg['dependencies'])) {
        if (-not $d.Contains('path')) { throw "deployment-impact.yml: dependency entry missing 'path'." }
        if (-not $d.Contains('affects')) { throw "deployment-impact.yml: dependency '$($d['path'])' missing 'affects'." }
        foreach ($a in @($d['affects'])) {
            if ([string]$a -notin $appNames) { throw "deployment-impact.yml: dependency '$($d['path'])' affects unknown app '$a' (known: $($appNames -join ', '))." }
        }
    }
    if (-not $cfg.Contains('unknown') -or $cfg['unknown'] -ne "full") {
        throw "deployment-impact.yml: 'unknown' must be 'full' (safety valve)."
    }

    # ---- collect changed files ----
    if ($ChangedFiles.Count -eq 0) {
        if (-not $DiffBase) { throw "Provide -ChangedFiles or -DiffBase." }
        $diff = & git diff --name-only $DiffBase HEAD 2>$null
        if ($LASTEXITCODE -ne 0) { throw "git diff --name-only $DiffBase HEAD failed." }
        $ChangedFiles = @($diff)
    }
    $files = @()
    foreach ($f in $ChangedFiles) {
        if (-not $f) { continue }
        $n = ($f -replace '\\', '/').Trim()
        $n = $n -replace '^\./', ''
        if ($n) { $files += $n }
    }
    $files = @($files | Sort-Object -Unique)

    # ---- classify each file ----
    $severity = "NO_DEPLOY"          # NO_DEPLOY < APP < MULTI_APP < OPS < UNKNOWN
    $deployApps = @{}                # set of app names
    $unknownPaths = @()
    $matchedRules = @()
    $intentSet = @{}

    foreach ($file in $files) {
        $fileSev = $null
        $fileApps = @{}

        # ops (highest path-level trigger)
        if (Test-GlobMatchAny -Path $file -Patterns $cfg['ops_paths']) {
            $fileSev = "OPS"
            $matchedRules += "$file -> OPS (ops_paths)"
        }
        # dependency graph
        foreach ($d in @($cfg['dependencies'])) {
            if (Test-GlobMatch -Path $file -Pattern ([string]$d['path'])) {
                if (-not $fileSev -or $fileSev -eq "APP") { $fileSev = "MULTI_APP" }
                foreach ($a in @($d['affects'])) { $fileApps[[string]$a] = $true }
                $matchedRules += "$file -> DEP($($d['path'])) affects $((@($d['affects'])) -join ',')"
            }
        }
        # app own paths
        foreach ($k in $appNames) {
            foreach ($p in @($cfg['apps'][$k]['paths'])) {
                if (Test-GlobMatch -Path $file -Pattern ([string]$p)) {
                    if (-not $fileSev) { $fileSev = "APP" }
                    $fileApps[[string]$k] = $true
                    $matchedRules += "$file -> APP($k) via $p"
                }
            }
        }
        # no rule matched yet -> no_deploy or UNKNOWN (safety valve)
        if (-not $fileSev) {
            if (Test-GlobMatchAny -Path $file -Patterns $cfg['no_deploy_paths']) {
                $fileSev = "NO_DEPLOY"
                $matchedRules += "$file -> NO_DEPLOY (no_deploy_paths)"
            } else {
                $fileSev = "UNKNOWN"
                $unknownPaths += $file
                $matchedRules += "$file -> UNKNOWN (no rule matched)"
            }
        }

        # aggregate severity (UNKNOWN > OPS > MULTI_APP > APP > NO_DEPLOY)
        switch ($fileSev) {
            "UNKNOWN"   { $severity = "UNKNOWN" }
            "OPS"       { if ($severity -in @("NO_DEPLOY", "APP", "MULTI_APP")) { $severity = "OPS" } }
            "MULTI_APP" { if ($severity -in @("NO_DEPLOY", "APP")) { $severity = "MULTI_APP" } }
            "APP"       { if ($severity -eq "NO_DEPLOY") { $severity = "APP" } }
        }
        foreach ($a in $fileApps.Keys) { $deployApps[$a] = $true }
    }

    # ops/unknown -> ALL apps
    if ($severity -in @("OPS", "UNKNOWN")) {
        foreach ($k in $appNames) { $deployApps[$k] = $true }
    }

    # ---- intents (verification hints, contract-based) ----
    if ($cfg.Contains('intents')) {
        foreach ($file in $files) {
            foreach ($intentName in $cfg['intents'].Keys) {
                $intent = $cfg['intents'][$intentName]
                if (Test-GlobMatchAny -Path $file -Patterns $intent['paths']) {
                    foreach ($v in @($intent['verification'])) { $intentSet[[string]$v] = $true }
                }
            }
        }
    }

    # ---- verification set ----
    $verif = @{}
    switch ($severity) {
        "APP"       { @("build", "unit", "deploy_smoke") | ForEach-Object { $verif[$_] = $true } }
        "MULTI_APP" { @("build", "unit", "integration", "deploy_smoke") | ForEach-Object { $verif[$_] = $true } }
        "OPS"       { @("build", "unit", "integration", "startup", "deploy_smoke", "targeted_rv") | ForEach-Object { $verif[$_] = $true } }
        "UNKNOWN"   { @("build", "unit", "integration", "startup", "deploy_smoke", "targeted_rv") | ForEach-Object { $verif[$_] = $true } }
    }
    foreach ($k in $intentSet.Keys) { $verif[$k] = $true }

    # ---- escalation (always allowed) ----
    $escalated = $false
    if ($ForceFull) {
        $escalated = $true
        foreach ($k in $appNames) { $deployApps[$k] = $true }
        $verif = @{ "build" = $true; "unit" = $true; "integration" = $true; "startup" = $true; "deploy_smoke" = $true; "targeted_rv" = $true }
    }

    # ---- deploy action ----
    $deployAction = if ($severity -eq "NO_DEPLOY" -and -not $ForceFull) { "noop" }
                    elseif ($severity -in @("OPS", "UNKNOWN") -or $ForceFull) { "full" }
                    else { "deploy" }

    # ---- no-downgrade guard ----
    $blocked = $false
    $blockReason = ""
    if ($RequestedScope) {
        $scope = @($RequestedScope -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $missing = @($deployApps.Keys | Where-Object { $_ -notin $scope } | Sort-Object)
        if ($missing.Count -gt 0) {
            $blocked = $true
            $blockReason = "RequestedScope [$($RequestedScope)] does not cover impact set [$((@($deployApps.Keys) | Sort-Object) -join ', ')]. Missing: $($missing -join ', '). Downgrade BLOCKED - run FULL or widen scope."
        }
    }

    $deploySet = @($deployApps.Keys | Sort-Object)

    $summary = "classification=$severity deploy_action=$deployAction deploy_set=[$($deploySet -join ',')]"
    if ($blocked) { $summary += " BLOCKED: $blockReason" }

    return [PSCustomObject]@{
        classification    = $severity
        deploy_action     = $deployAction
        deploy_set        = $deploySet
        build_set         = @($deploySet)
        verification_set  = @($verif.Keys | Sort-Object)
        needs_ci          = ($severity -ne "NO_DEPLOY") -or ($intentSet.Count -gt 0)
        unknown_paths     = @($unknownPaths)
        matched_rules     = @($matchedRules)
        escalated         = $escalated
        blocked           = $blocked
        block_reason      = $blockReason
        summary           = $summary
    }
}

# ============================================================
# TOP LEVEL (skipped when dot-sourced by tests)
# ============================================================
if ($MyInvocation.InvocationName -ne '.') {
    try {
        $result = Invoke-ImpactAnalysis -ChangedFiles $ChangedFiles -DiffBase $DiffBase `
            -ConfigPath $ConfigPath -ForceFull:$ForceFull -RequestedScope $RequestedScope
        if ($Json) { $result | ConvertTo-Json -Depth 6 }
        else { Write-Host $result.summary }
        if ($result.blocked) { exit 1 }
        exit 0
    }
    catch {
        Write-Error $_.Exception.Message
        exit 1
    }
}
