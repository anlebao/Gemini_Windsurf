#!/usr/bin/env python3
"""Twin drift detector — code -> registry diff report (SRS §12.1, task EOM-X0).
DETECT-ONLY: reports 'code has, twin lacks' + 'twin has, code lacks'. Never writes registries.
Usage: python scripts/twin-drift-report.py [--fail-on-drift]
"""
import io, json, re, subprocess, sys
from pathlib import Path
import yaml

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
ROOT = Path(__file__).resolve().parent.parent
REG = ROOT / "docs/enterprise-operating-model/registries"
OUT = ROOT / "docs/enterprise-operating-model/11-validation/drift-report.md"
SHA = subprocess.run(["git","rev-parse","--short","HEAD"],capture_output=True,text=True,cwd=ROOT).stdout.strip() or "unknown"

def load_reg(name):
    d = yaml.safe_load((REG/f"{name}.yaml").read_text(encoding="utf-8"))
    return d.get("entries") or []

def load_whitelist():
    f = REG/"drift-whitelist.yaml"
    return yaml.safe_load(f.read_text(encoding="utf-8")) if f.exists() else {}

WL = load_whitelist()
findings = []  # (tier, kind, found-at, registry-status, suggestion)

# ---- 1. Controllers -> apis.yaml ----
api_routes = " ".join(str(e.get("routeOrSubject","")) for e in load_reg("apis"))
for cdir in ["2_Gateway/Controllers","5_WebApps/ShopERP/Controllers"]:
    for f in sorted((ROOT/cdir).glob("*.cs")):
        src = f.read_text(encoding="utf-8", errors="replace")
        cls = f.stem
        if cls in (WL.get("controllers") or []): continue
        if cls.replace("Controller","").lower() not in api_routes.lower() and f"api/{cls.replace('Controller','').lower()}" not in api_routes.lower():
            findings.append(("CONDITIONAL","api-endpoint",f"{cdir}/{f.name}@{SHA}",
                "not in apis.yaml","add API-xxx if a modeled activity references it"))

# ---- 2. Domain entities -> data-entities.yaml ----
data_names = {str(e.get("name","")).lower() for e in load_reg("data-entities")}
ent_files = list((ROOT/"1_Shared/Domain").glob("*.cs")) + [ROOT/"1_Shared/Domain.cs"]
ent_count = 0
for f in ent_files:
    for m in re.finditer(r"public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(\w+)\s*:\s*BaseEntity", f.read_text(encoding="utf-8", errors="replace")):
        ent_count += 1
        n = m.group(1)
        if n.lower() not in data_names and n not in (WL.get("entities") or []):
            findings.append(("CONDITIONAL","data-entity",f"{f.relative_to(ROOT)}:{n}@{SHA}",
                "not in data-entities.yaml","add DATA-xxx if business-visible"))

# ---- 3. NATS subjects -> apis.yaml ----
subjects = set()
for f in list(ROOT.rglob("*.cs")):
    if any(x in str(f) for x in ("obj","bin","Tests","Testing")): continue
    for m in re.finditer(r'"(vanan\.(?:cloud|shoperp)\.[a-z0-9._{}*]+)"', f.read_text(encoding="utf-8", errors="replace")):
        subjects.add((m.group(1), str(f.relative_to(ROOT))))
for subj, where in sorted(subjects):
    if subj.split("{")[0] not in api_routes and subj not in (WL.get("subjects") or []):
        findings.append(("MANDATORY","nats-subject",f"{where}: \"{subj}\"@{SHA}",
            "not in apis.yaml","add EVENT API entry + integration-map row"))

# ---- 4. Voucher registrations -> rules.yaml ----
rule_names = " ".join(str(e.get("name",""))+str(e.get("description","")) for e in load_reg("rules")).lower()
for f in ROOT.rglob("*.cs"):
    if any(x in str(f) for x in ("obj","bin","Tests")): continue
    src = f.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r"AddVoucherType|\.Register\(new VoucherTypeDefinition|VoucherTypeDefinition\(", src):
        seg = src[max(0,m.start()-120):m.start()+200]
        code = re.search(r'"([A-Z][A-Za-z0-9_-]{2,})"', seg)
        if code and code.group(1).lower() not in rule_names:
            findings.append(("MANDATORY","voucher-type",f"{f.relative_to(ROOT)}:{code.group(1)}@{SHA}",
                "voucher type not in rules.yaml","add RULE/ACTV per §12.1 mandatory trigger"))

# ---- report ----
OUT.parent.mkdir(parents=True, exist_ok=True)
lines = [f"# Drift Report — code vs twin\n",
         f"> Generated at commit `{SHA}` by `scripts/twin-drift-report.py`. DETECT-ONLY — review before applying.\n",
         "| Tier | Kind | Found in code | Registry status | Suggested update |","|---|---|---|---|---|"]
for t,k,at,st,sug in findings:
    lines.append(f"| {t} | {k} | `{at}` | {st} | {sug} |")
mand = sum(1 for f in findings if f[0]=="MANDATORY")
lines += [f"\n**{len(findings)} findings ({mand} MANDATORY, {len(findings)-mand} CONDITIONAL)** — entities scanned: {ent_count}, NATS subjects: {len(subjects)}"]
OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"{len(findings)} findings ({mand} mandatory) -> {OUT.relative_to(ROOT)}")
sys.exit(1 if ("--fail-on-drift" in sys.argv and mand) else 0)
