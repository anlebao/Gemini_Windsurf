# MCP Integration for ShopERP

> **Model Context Protocol (MCP) Configuration**  
> Cho phép AI làm việc trực tiếp với dự án: đọc Issue, tạo Branch, Commit, tạo PR.

## Overview

MCP (Model Context Protocol) là giao thức chuẩn để AI assistants tương tác với external tools và data sources. ShopERP sử dụng MCP để:

- **Đọc GitHub Issues** - Hiểu requirement từ issue descriptions
- **Tạo branch từ issue** - Automated branch naming
- **Commit và push** - Lưu changes trực tiếp
- **Tạo Pull Requests** - End-to-end feature development

## Architecture

```
┌─────────────────┐
│   Windsurf AI   │
│   (Cascade)     │
└────────┬────────┘
         │
         │ MCP Protocol
         ▼
┌─────────────────┐     ┌─────────────────┐
│   GitHub MCP    │────▶│  GitHub API     │
│   Server        │     │  (Issues, PRs)  │
└─────────────────┘     └─────────────────┘
         │
         │
┌─────────────────┐     ┌─────────────────┐
│  Filesystem MCP │────▶│  Local Files    │
│   Server        │     │  (Read/Write)   │
└─────────────────┘     └─────────────────┘
```

## MCP Servers

### 1. GitHub MCP

**Purpose**: Tương tác với GitHub repository

**Capabilities**:
- Read/Create/Update Issues
- Create/List/Update Pull Requests
- Create branches
- Commit và push changes
- Add comments

**Configuration**: `mcp/github-mcp/config.json`

**Required Token**:
```bash
# GitHub Personal Access Token với scopes:
# - repo (full control)
# - issues (read/write)
# - pull_requests (read/write)
# - contents (read/write)
export GITHUB_TOKEN=ghp_xxxxxxxxxxxx
```

**Security Constraints**:
- Chỉ hoạt động trên allowed repositories
- Blocked operations: delete repo, force push protected, remove collaborators
- Rate limit: 5000 requests/hour

### 2. Filesystem MCP

**Purpose**: Thao tác với local files

**Capabilities**:
- Read/Write files
- List directories
- Search files
- Edit multiple files
- Auto backup trước khi edit

**Configuration**: `mcp/filesystem-mcp/config.json`

**Allowed Paths**:
```
c:\VibeCoding\Gemini_Windsurf
c:\VibeCoding\Gemini_Windsurf\docs
c:\VibeCoding\Gemini_Windsurf\.windsurf
```

**Blocked Paths** (bảo vệ):
```
.git/          # Git internals
.env           # Environment secrets
secrets/       # Secret files
**/appsettings.Production.json  # Production config
```

**Auto-load on Startup**:
- `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`
- `.windsurf/rules/.windsurfrules`

## Workflows

### Workflow 1: Feature from Issue

```
User tạo Issue "Add expense entry form"
         ↓
AI reads Issue via GitHub MCP
         ↓
AI loads PROJECT_CONTEXT.md via Filesystem MCP
         ↓
AI creates branch: feature/expense-entry-form
         ↓
AI implements feature (ANALYZE → IMPLEMENT mode)
         ↓
AI commits changes
         ↓
AI creates PR với description từ issue
         ↓
User review và merge
```

### Workflow 2: Bug Fix

```
Bug reported in Issue #123
         ↓
AI reads Issue (reproduction steps)
         ↓
AI identifies root cause
         ↓
AI creates branch: fix/issue-123-{description}
         ↓
AI implements fix (FIX_ONLY mode)
         ↓
AI commits và tạo PR
         ↓
CI chạy tests (Phase 3)
         ↓
User review
```

### Workflow 3: Code Review

```
Developer tạo PR
         ↓
AI (Domain Guardian) review via GitHub MCP
         ↓
AI checks:
   - Domain layer changes
   - ADR compliance
   - UI Platform usage
         ↓
AI add comments vào PR
         ↓
Developer addresses feedback
```

## Installation

### Prerequisites

1. Node.js 20+
2. GitHub Personal Access Token

### Setup GitHub MCP

```bash
# 1. Install GitHub MCP server globally
npm install -g @modelcontextprotocol/server-github

# 2. Set environment variable
export GITHUB_TOKEN=your_github_token_here

# 3. Verify installation
npx @modelcontextprotocol/server-github --help
```

### Setup Filesystem MCP

```bash
# Install Filesystem MCP server
npm install -g @modelcontextprotocol/server-filesystem

# Verify
npx @modelcontextprotocol/server-filesystem --help
```

### Windsurf Configuration

Thêm vào Windsurf settings (`.windsurf/settings.json`):

```json
{
  "mcp": {
    "servers": [
      {
        "name": "github",
        "command": "npx -y @modelcontextprotocol/server-github",
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_TOKEN}"
        }
      },
      {
        "name": "filesystem",
        "command": "npx -y @modelcontextprotocol/server-filesystem c:\\VibeCoding\\Gemini_Windsurf"
      }
    ]
  }
}
```

## Security

### Token Scope (GitHub)

**Required**:
- `repo` - Full repository access
- `issues` - Issue management
- `pull_requests` - PR management

**Not Required** (blocked in config):
- `delete_repo`
- `admin:org`
- `user` (write)

### Path Restrictions (Filesystem)

- Chỉ có thể read/write trong project directory
- Không thể access: `.git/`, `.env`, secrets
- Auto backup trước mọi edit operation

### Audit Trail

Tất cả operations được log vào:
- `c:\VibeCoding\Gemini_Windsurf\.mcp-logs\`
- GitHub audit log (repo settings)
- CI/CD artifacts

## Testing MCP

### Test 1: Read Issue

```
Prompt: "Read GitHub issue #42 và tóm tắt requirement"
Expected: AI trả về nội dung issue và analysis
```

### Test 2: Create Branch

```
Prompt: "Tạo branch cho issue #42 tên 'feature/user-authentication'"
Expected: Branch created từ main/develop
```

### Test 3: Commit và PR

```
Prompt: "Commit changes với message 'Add auth service' và tạo PR"
Expected: Commit pushed, PR created với description
```

## Troubleshooting

### Issue: Token không hợp lệ
```
Solution: Regenerate GitHub token với đủ scopes
Verify: curl -H "Authorization: token $GITHUB_TOKEN" https://api.github.com/user
```

### Issue: Path không truy cập được
```
Solution: Kiểm tra allowed_paths trong filesystem config
Verify: AI có thể list directory trước khi read/write
```

### Issue: Rate limit exceeded
```
Solution: Chờ 1 giờ hoặc upgrade GitHub account
Kiểm tra: X-RateLimit-Remaining header trong API response
```

## Future MCP Integrations

| MCP | Purpose | Status |
|-----|---------|--------|
| PostgreSQL | Query database | Planned |
| Qdrant | Semantic search | Planned |
| Stripe | Payment operations | Future |
| SendGrid | Email notifications | Future |
| Slack | Team notifications | Future |

## References

- [MCP Specification](https://modelcontextprotocol.io/)
- [GitHub MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/github)
- [Filesystem MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem)
- `docs/knowledge-base/08-ai/AGENTS.md` - Agent workflows
- `docs/knowledge-base/08-ai/prompts/` - Prompt templates

---

*Version: 1.0*  
*Last Updated: June 1, 2026*  
*Status: Ready for testing*
