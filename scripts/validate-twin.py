#!/usr/bin/env python3
"""Digital Twin registry validator — SRS v2.1 §11.3.

Checks (per §11.3):
  1. ID format + uniqueness per registry
  2. Required fields per schema
  3. Referential integrity (all *Ref values resolve; orphan VARIANT = error)
  4. Enum values (executionType, confidence, variability, status, ...)
  5. Evidence format (SRS §9.1): path@commit | path#L | none:<reason>
  6. Coverage warnings (missing owner / missing software link) — warnings, not errors

Usage:  python scripts/validate-twin.py [--quiet]
Exit:   0 = no errors (warnings allowed) · 1 = errors found
"""
import io, json, re, sys
from pathlib import Path

import yaml

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

REG_DIR = Path(__file__).resolve().parent.parent / "docs" / "enterprise-operating-model" / "registries"

errors, warnings = [], []


def load_schemas():
    schemas = {}
    for p in sorted((REG_DIR / "schema").glob("*.schema.json")):
        schemas[p.stem.replace(".schema", "")] = json.loads(p.read_text(encoding="utf-8"))
    return schemas


def load_registries():
    regs = {}
    for p in sorted(REG_DIR.glob("*.yaml")):
        data = yaml.safe_load(p.read_text(encoding="utf-8")) or {}
        regs[p.stem] = data.get("entries") or []
    return regs


def check():
    schemas, regs = load_schemas(), load_registries()

    # Build global id index: id -> registry name
    id_index = {}
    for name, entries in regs.items():
        seen = set()
        for i, e in enumerate(entries):
            loc = f"{name}[{i}]"
            if not isinstance(e, dict):
                errors.append(f"{loc}: entry is not a mapping")
                continue
            eid = e.get("id")
            # 1. ID format + uniqueness
            sch = schemas.get(name)
            if sch and eid:
                if not re.match(sch["idPattern"], str(eid)):
                    errors.append(f"{loc}: id '{eid}' does not match {sch['idPattern']}")
                if eid in seen:
                    errors.append(f"{loc}: duplicate id '{eid}'")
                seen.add(eid)
                if eid in id_index:
                    errors.append(f"{loc}: id '{eid}' already used in {id_index[eid]}")
                id_index[eid] = name
            elif not eid:
                errors.append(f"{loc}: missing 'id'")

    prefix_to_reg = {s["idPrefix"]: n for n, s in schemas.items()}

    for name, entries in regs.items():
        sch = schemas.get(name)
        if not sch:
            warnings.append(f"registry '{name}': no schema file found")
            continue
        for i, e in enumerate(entries):
            if not isinstance(e, dict):
                continue
            loc = f"{name}:{e.get('id', f'[{i}]')}"
            # 2. Required fields
            for f in sch["required"]:
                if f not in e or e[f] in (None, ""):
                    errors.append(f"{loc}: missing required field '{f}'")
            # 4. Enums
            for f, allowed in sch.get("enums", {}).items():
                if f in e and e[f] is not None:
                    vals = e[f] if isinstance(e[f], list) else [e[f]]
                    for v in vals:
                        if v not in allowed:
                            errors.append(f"{loc}: {f}='{v}' not in {allowed}")
            # 5. Evidence format
            ev = e.get("evidence")
            if ev is not None:
                evs = ev if isinstance(ev, list) else [ev]
                for x in evs:
                    if not re.match(sch["evidencePattern"], str(x)):
                        errors.append(f"{loc}: evidence '{x}' bad format (need path@commit | path#L | none:<reason>)")
            # 3. Referential integrity
            for f, target_prefix in sch.get("references", {}).items():
                if f not in e or e[f] is None:
                    continue
                vals = e[f] if isinstance(e[f], list) else [e[f]]
                target_reg = prefix_to_reg.get(target_prefix)
                for v in vals:
                    if v not in id_index:
                        errors.append(f"{loc}: {f} '{v}' does not resolve")
                    elif id_index[v] != target_reg:
                        errors.append(f"{loc}: {f} '{v}' points to {id_index[v]}, expected {target_reg}")
            # SRS 6.6: VARIANT process requires parentRef
            if name == "processes" and e.get("variability") == "VARIANT" and not e.get("parentRef"):
                errors.append(f"{loc}: VARIANT process requires parentRef to CORE parent")
            # 6. Coverage warnings
            if name == "activities":
                if not e.get("humanActorRef") and not e.get("softwareActorRef"):
                    warnings.append(f"{loc}: activity has no owner (humanActorRef/softwareActorRef)")
                if not e.get("moduleRef") and not e.get("apiRefs"):
                    warnings.append(f"{loc}: activity has no software link (moduleRef/apiRefs)")
            if name == "risks" and not e.get("ctrlRefs"):
                warnings.append(f"{loc}: risk has no linked control")

    return errors, warnings


def main():
    errs, warns = check()
    quiet = "--quiet" in sys.argv
    if not quiet:
        for w in warns:
            print(f"WARN  {w}")
    for x in errs:
        print(f"ERROR {x}")
    print(f"\n{len(errs)} error(s), {len(warns)} warning(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
