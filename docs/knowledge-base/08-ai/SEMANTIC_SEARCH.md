# Semantic Search (Phase 7)

> **Phase 7 Deliverable** — AI-powered knowledge retrieval using vector embeddings

---

## Overview

Semantic Search cho phép AI tìm kiếm kiến thức theo **ngữ nghĩa** thay vì keyword matching. Không cần nhét toàn bộ Knowledge Base vào context.

**Architecture:**
```
User Question
      ↓
Embedding (Local/AI)
      ↓
SQLite Vector Store (now) / Qdrant (future)
      ↓
Top-K Relevant Docs
      ↓
AI Context (chỉ những gì cần thiết)
      ↓
Answer
```

---

## Collections

| Collection | Content | Chunk Strategy | Use Case |
|------------|---------|----------------|----------|
| `adrs` | ADR documents | Per ADR | "Find ADRs about accounting" |
| `domains` | Domain docs | Per entity | "How does Order domain work?" |
| `workflows` | `.windsurf/workflows/` | Per workflow | "Show me bug fix workflow" |
| `skills` | `.windsurf/skills/` | Per skill | "Find skills for build errors" |
| `codebase` | Source code | Per file/class | "Find InvoiceService implementation" |
| `tasks` | Task history | Per task | "What did we do last month?" |

---

## Usage

### Basic Search

```csharp
// Find ADRs about accounting
var adrs = await _search.FindAdrsAsync("accounting immutability", topK: 3);

// Find domain docs
var domains = await _search.FindDomainDocsAsync("Order aggregate", topK: 3);

// Find skills for error pattern
var skills = await _search.FindSkillsAsync("CS0311 build error", topK: 3);
```

### Advanced Search

```csharp
// Search across multiple collections
var results = await _search.SearchAcrossCollectionsAsync(
    "error handling pattern",
    new[] { Collections.Skills, Collections.Workflows },
    topK: 5);

// Custom collection search
var results = await _search.SearchAsync(
    "dependency injection",
    Collections.Codebase,
    topK: 10);
```

### Indexing Documents

```csharp
// Index a markdown file
var result = await _pipeline.IndexMarkdownFileAsync(
    "docs/decisions/ADR-001-SQLite-Offline-First.md",
    DocumentTypes.Adr);

// Index a directory
var results = await _pipeline.IndexDirectoryAsync(
    "docs/knowledge-base/02-domains",
    "*.md",
    DocumentTypes.Domain);

// Index code
var result = await _pipeline.IndexCodeFileAsync(
    "3_CoreHub/Services/InvoiceService.cs");
```

---

## Integration with Agents

### Knowledge Retrieval Agent (New)

```
Mode: RETRIEVE_ONLY
Trigger: AI needs context

Workflow:
1. Analyze user question
2. Determine relevant collections
3. Semantic search → Top-K docs
4. Inject into AI context
5. Generate response
```

### Feature Developer Agent

```
Before implementation:
  1. Search similar features in codebase
  2. Find relevant ADRs
  3. Retrieve domain patterns

After implementation:
  1. Index new code
  2. Update vector store
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "SemanticSearch": "Data Source=semantic_search.db"
  }
}
```

### DI Registration

```csharp
// Already configured in Program.cs
services.AddSingleton<IVectorStore>(...);
services.AddSingleton<IEmbeddingService>(...);
services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
services.AddSingleton<IndexingPipeline>();
```

---

## Migration to Qdrant

When ready for production:

1. **Setup Qdrant** (Docker/cloud)
2. **Update connection string**
   ```csharp
   // Replace SqliteVectorStore with QdrantVectorStore
   services.AddSingleton<IVectorStore>(sp =>
       new QdrantVectorStore("http://localhost:6333", ...));
   ```
3. **Re-index documents**
4. **Same interface, different backend**

---

## Files

| File | Path |
|------|------|
| Interface | `Infrastructure/SemanticSearch/ISemanticSearchService.cs` |
| Implementation | `Infrastructure/SemanticSearch/SemanticSearchService.cs` |
| Embedding Service | `Infrastructure/SemanticSearch/Services/IEmbeddingService.cs` |
| Local Embedding | `Infrastructure/SemanticSearch/Services/LocalEmbeddingService.cs` |
| Vector Store | `Infrastructure/SemanticSearch/Services/IVectorStore.cs` |
| SQLite Store | `Infrastructure/SemanticSearch/Services/SqliteVectorStore.cs` |
| Indexing Pipeline | `Infrastructure/SemanticSearch/IndexingPipeline.cs` |
| Models | `Infrastructure/SemanticSearch/Models/DocumentChunk.cs` |

---

## Performance

| Metric | SQLite | Qdrant (Future) |
|--------|--------|-----------------|
| Search latency | ~100ms | ~10ms |
| Scale | 10K docs | 1M+ docs |
| Similarity | Cosine | Cosine + advanced |
| Indexing | Synchronous | Async batch |

---

## Version History

| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-06-10 | Initial Phase 7 implementation (SQLite-based) |

---

*Next: Qdrant integration for production scale*
