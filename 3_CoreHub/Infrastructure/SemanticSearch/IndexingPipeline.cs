using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch;

/// <summary>
/// Phase 7: Indexing pipeline for ingesting documents into semantic search
/// Chunks documents, generates embeddings, indexes to vector store
/// </summary>
public class IndexingPipeline
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<IndexingPipeline> _logger;

    // Chunking settings
    private const int MaxChunkSize = 1000;  // characters
    private const int ChunkOverlap = 200;   // characters overlap between chunks

    public IndexingPipeline(ISemanticSearchService searchService, ILogger<IndexingPipeline> logger)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Index a markdown file (ADR, domain doc, workflow, skill)
    /// </summary>
    public async Task<IndexResult> IndexMarkdownFileAsync(string filePath, string documentType)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new IndexResult 
                { 
                    DocumentId = filePath, 
                    Success = false, 
                    Error = "File not found" 
                };
            }

            var content = await File.ReadAllTextAsync(filePath);
            var collection = GetCollectionForDocumentType(documentType);
            
            // Chunk the document
            var chunks = ChunkMarkdown(content, filePath, documentType);
            
            // Index batch
            return await _searchService.IndexBatchAsync(chunks, collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index markdown file: {FilePath}", filePath);
            return new IndexResult 
            { 
                DocumentId = filePath, 
                Success = false, 
                Error = ex.Message 
            };
        }
    }

    /// <summary>
    /// Index a code file
    /// </summary>
    public async Task<IndexResult> IndexCodeFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new IndexResult 
                { 
                    DocumentId = filePath, 
                    Success = false, 
                    Error = "File not found" 
                };
            }

            var content = await File.ReadAllTextAsync(filePath);
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
            
            // Chunk the code
            var chunks = ChunkCode(content, filePath, extension);
            
            // Index batch
            return await _searchService.IndexBatchAsync(chunks, Collections.Codebase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index code file: {FilePath}", filePath);
            return new IndexResult 
            { 
                DocumentId = filePath, 
                Success = false, 
                Error = ex.Message 
            };
        }
    }

    /// <summary>
    /// Index a directory of documents
    /// </summary>
    public async Task<IReadOnlyList<IndexResult>> IndexDirectoryAsync(string directoryPath, string pattern, string documentType)
    {
        var results = new List<IndexResult>();
        
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory not found: {Directory}", directoryPath);
            return results;
        }

        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            IndexResult result;
            if (documentType == DocumentTypes.Code)
            {
                result = await IndexCodeFileAsync(file);
            }
            else
            {
                result = await IndexMarkdownFileAsync(file, documentType);
            }
            results.Add(result);
        }

        _logger.LogInformation("Indexed {Total} files from {Directory} ({Success} successful)", 
            files.Length, directoryPath, results.Count(r => r.Success));

        return results;
    }

    /// <summary>
    /// Chunk markdown document intelligently
    /// </summary>
    private List<DocumentChunk> ChunkMarkdown(string content, string source, string documentType)
    {
        var chunks = new List<DocumentChunk>();
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var line in lines)
        {
            // If adding this line would exceed chunk size, save current chunk
            if (currentChunk.Length + line.Length > MaxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.ToString(), source, documentType, chunkIndex++));
                
                // Start new chunk with overlap
                var overlapText = currentChunk.ToString();
                if (overlapText.Length > ChunkOverlap)
                {
                    currentChunk = new StringBuilder(overlapText.Substring(overlapText.Length - ChunkOverlap));
                }
                else
                {
                    currentChunk = new StringBuilder();
                }
            }

            currentChunk.AppendLine(line);
        }

        // Don't forget the last chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(CreateChunk(currentChunk.ToString(), source, documentType, chunkIndex++));
        }

        // Update total chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].TotalChunks = chunks.Count;
        }

        return chunks;
    }

    /// <summary>
    /// Chunk code by class/method boundaries (simplified)
    /// </summary>
    private List<DocumentChunk> ChunkCode(string content, string source, string language)
    {
        var chunks = new List<DocumentChunk>();
        
        // Simple approach: chunk by line blocks
        // In production, use AST parsing for better boundaries
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var line in lines)
        {
            if (currentChunk.Length + line.Length > MaxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.ToString(), source, DocumentTypes.Code, chunkIndex++, 
                    new Dictionary<string, string> { ["language"] = language }));
                currentChunk.Clear();
            }

            currentChunk.AppendLine(line);
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(CreateChunk(currentChunk.ToString(), source, DocumentTypes.Code, chunkIndex++,
                new Dictionary<string, string> { ["language"] = language }));
        }

        // Update total chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].TotalChunks = chunks.Count;
        }

        return chunks;
    }

    private DocumentChunk CreateChunk(string content, string source, string documentType, int index, Dictionary<string, string>? extraMetadata = null)
    {
        var chunk = new DocumentChunk
        {
            Source = source,
            Content = content.Trim(),
            DocumentType = documentType,
            ChunkIndex = index,
            Metadata = new Dictionary<string, string>
            {
                ["file_name"] = Path.GetFileName(source),
                ["directory"] = Path.GetDirectoryName(source) ?? ""
            }
        };

        if (extraMetadata != null)
        {
            foreach (var kv in extraMetadata)
            {
                chunk.Metadata[kv.Key] = kv.Value;
            }
        }

        return chunk;
    }

    private static string GetCollectionForDocumentType(string documentType)
    {
        return documentType.ToLower() switch
        {
            DocumentTypes.Adr => Collections.Adrs,
            DocumentTypes.Domain => Collections.Domains,
            DocumentTypes.Workflow => Collections.Workflows,
            DocumentTypes.Skill => Collections.Skills,
            DocumentTypes.Code => Collections.Codebase,
            DocumentTypes.Task => Collections.Tasks,
            _ => "misc"
        };
    }
}
