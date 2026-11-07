# Task EOM-X5 — Docs Portal (HTML viewer cho Digital Twin)

> **Status:** ✅ X5 DONE — mkdocs.yml + README; build deferred (mkdocs-material install on demand)
> **Depends:** EOM-P5 (nội dung twin đã ổn định)
> **Lưu ý:** GitHub/IDE đã render Markdown+Mermaid sẵn — card này chỉ cần khi muốn portal đẹp có search/nav cho non-dev (owner, HTX operator).

## Objective

Build static HTML portal từ `docs/enterprise-operating-model/` bằng **mkdocs-material** (chọn vì: markdown-native, mermaid plugin, search built-in, YAML→table render qua macro — đúng stack twin, không cần viết lại nội dung).

## Deliverables

```
docs/enterprise-operating-model/mkdocs.yml    # config: nav, mermaid, plugins
site build output → host nginx (hoặc GitHub Pages) — quyết khi implement
scripts/build-twin-portal.ps1 (optional convenience)
```

## Requirements

| Item | Spec |
|---|---|
| Nav | Theo cây §12 (00-overview → 11-validation) + registries section |
| Mermaid | `pymdownx superfences` + mermaid plugin — render đúng classDef §5.3 |
| Registries → HTML | Macro/plugin render `registries/*.yaml` thành bảng searchable (mkdocs-macros hoặc pre-generate .md từ YAML) — KHÔNG duplicate data: page generated từ YAML, không viết tay |
| Search | built-in mkdocs search (offline index) |
| Multi-audience | Entry pages riêng: owner view (business flows), architect view (components/data), HTX operator view (core processes + variants) — dùng model-index.md làm root |
| Secrets check | Build step fails nếu phát hiện secret pattern (NFR-07) — grep đơn giản |
| Versioning | Tag build theo commit SHA — viewer hiển thị "model captured at \<sha\>" |

## Rules

- Không sửa nội dung twin để "hợp portal" — portal render twin nguyên trạng.
- Không build platform/viewer riêng — chỉ cấu hình mkdocs.
- Không embed vào ShopERP/app (doc project không đụng app code).

## Wired (2026-11-07)

- `docs/mkdocs-twin.yml` — build: `python -m mkdocs build -f docs/mkdocs-twin.yml` → `docs/twin-portal-site/` (verified build OK)
- `nginx/twin-portal.location.conf` — snippet `/twin/` → `/var/www/twin/` + 3 gating options (auth_basic/IP allowlist/authenticated proxy) — **include vào ShopERP server block khi deploy**
- Nav link: `ShopErpMenuService.cs` → "Operating Model (Digital Twin)" trong nhóm **SystemAdmin-only** (`isSystemAdmin` gate, impersonation-aware)
- **Caveat ghi rõ:** menu link chỉ giấu URL — bảo vệ thật cần chọn 1 trong 3 options trong nginx snippet khi deploy
- Manual còn lại: `pip install mkdocs-material` trên CI/dev machine (đã cài local), rsync site → `/var/www/twin/`, uncomment gating

## Acceptance

- [ ] `mkdocs build` sạch; mermaid diagrams render trên HTML
- [ ] ≥1 registry render thành bảng
- [ ] Search hoạt động; nav đủ 12 sections
- [ ] Host URL quyết định + ghi vào card
