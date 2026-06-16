using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

/// <summary>
/// Phase 7: SQLite-based vector store (offline-first, file-based)
/// Compatible with ADR-001 SQLite approach
/// Can be migrated to Qdrant for production
/// </summary>
public class SqliteVectorStore : IVectorStore, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteVectorStore> _logger;
    private readonly SqliteConnection _connection;

    public SqliteVectorStore(string connectionString, ILogger<SqliteVectorStore> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // Collections metadata table
        var createCollectionsSql = @"
            CREATE TABLE IF NOT EXISTS collections (
                name TEXT PRIMARY KEY,
                vector_size INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );";
        
        using var cmd = new SqliteCommand(createCollectionsSql, _connection);
        cmd.ExecuteNonQuery();
    }

    public Task<bool> CreateCollectionAsync(string name, int vectorSize)
    {
        try
        {
            // Insert collection metadata
            var insertSql = @"
                INSERT OR REPLACE INTO collections (name, vector_size, created_at)
                VALUES (@name, @vectorSize, @createdAt);";
            
            using var cmd = new SqliteCommand(insertSql, _connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@vectorSize", vectorSize);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();

            // Create collection table
            var createTableSql = $@"
                CREATE TABLE IF NOT EXISTS {name}_vectors (
                    id TEXT PRIMARY KEY,
                    source TEXT NOT NULL,
                    content TEXT NOT NULL,
                    document_type TEXT NOT NULL,
                    chunk_index INTEGER,
                    total_chunks INTEGER,
                    embedding TEXT NOT NULL, -- JSON array of floats
                    metadata TEXT, -- JSON dictionary
                    indexed_at TEXT NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_{name}_source ON {name}_vectors(source);
                CREATE INDEX IF NOT EXISTS idx_{name}_type ON {name}_vectors(document_type);
            ";
            
            using var createCmd = new SqliteCommand(createTableSql, _connection);
            createCmd.ExecuteNonQuery();
            
            _logger.LogInformation("Created collection: {Collection} with vector size {Size}", name, vectorSize);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection: {Collection}", name);
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteCollectionAsync(string name)
    {
        try
        {
            var dropSql = $@"
                DROP TABLE IF EXISTS {name}_vectors;
                DELETE FROM collections WHERE name = @name;";
            
            using var cmd = new SqliteCommand(dropSql, _connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
            
            _logger.LogInformation("Deleted collection: {Collection}", name);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection: {Collection}", name);
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync()
    {
        var sql = "SELECT name FROM collections ORDER BY name;";
        using var cmd = new SqliteCommand(sql, _connection);
        using var reader = cmd.ExecuteReader();
        
        var collections = new List<string>();
        while (reader.Read())
        {
            collections.Add(reader.GetString(0));
        }
        
        return Task.FromResult<IReadOnlyList<string>>(collections);
    }

    public Task<bool> CollectionExistsAsync(string name)
    {
        var sql = "SELECT 1 FROM collections WHERE name = @name LIMIT 1;";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@name", name);
        var result = cmd.ExecuteScalar();
        return Task.FromResult(result != null);
    }

    public Task<bool> UpsertAsync(string collection, DocumentChunk document)
    {
        try
        {
            var tableName = $"{collection}_vectors";
            var sql = $@"
                INSERT OR REPLACE INTO {tableName} 
                (id, source, content, document_type, chunk_index, total_chunks, embedding, metadata, indexed_at)
                VALUES 
                (@id, @source, @content, @docType, @chunkIndex, @totalChunks, @embedding, @metadata, @indexedAt);";
            
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@id", document.Id);
            cmd.Parameters.AddWithValue("@source", document.Source);
            cmd.Parameters.AddWithValue("@content", document.Content);
            cmd.Parameters.AddWithValue("@docType", document.DocumentType);
            cmd.Parameters.AddWithValue("@chunkIndex", document.ChunkIndex);
            cmd.Parameters.AddWithValue("@totalChunks", document.TotalChunks);
            cmd.Parameters.AddWithValue("@embedding", JsonSerializer.Serialize(document.Embedding));
            cmd.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(document.Metadata));
            cmd.Parameters.AddWithValue("@indexedAt", document.IndexedAt.ToString("O"));
            
            cmd.ExecuteNonQuery();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert document: {Id} to collection: {Collection}", 
                document.Id, collection);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> UpsertBatchAsync(string collection, IEnumerable<DocumentChunk> documents)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var doc in documents)
            {
                await UpsertAsync(collection, doc);
            }
            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to upsert batch to collection: {Collection}", collection);
            return false;
        }
    }

    public Task<bool> DeleteAsync(string collection, string documentId)
    {
        try
        {
            var tableName = $"{collection}_vectors";
            var sql = $"DELETE FROM {tableName} WHERE id = @id;";
            
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@id", documentId);
            var rowsAffected = cmd.ExecuteNonQuery();
            
            return Task.FromResult(rowsAffected > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {Id} from collection: {Collection}", 
                documentId, collection);
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collection, 
        float[] queryVector, 
        int topK = 5,
        Dictionary<string, string>? filters = null)
    {
        try
        {
            // For SQLite, we use cosine similarity
            var tableName = $"{collection}_vectors";
            var sql = $@"
                SELECT id, source, content, document_type, embedding, metadata
                FROM {tableName}";
            
            if (filters?.Count > 0)
            {
                // Simple JSON metadata filtering
                // In production, use proper JSON query or separate columns
            }
            
            using var cmd = new SqliteCommand(sql, _connection);
            using var reader = cmd.ExecuteReader();
            
            var results = new List<(string Id, string Source, string Content, float Score, Dictionary<string, string> Metadata)>();
            
            while (reader.Read())
            {
                var embeddingJson = reader.GetString(4);
                var storedEmbedding = JsonSerializer.Deserialize<float[]>(embeddingJson);
                
                if (storedEmbedding != null && storedEmbedding.Length == queryVector.Length)
                {
                    var score = CosineSimilarity(queryVector, storedEmbedding);
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(5)) ?? new();
                    
                    results.Add((
                        reader.GetString(0),  // id
                        reader.GetString(1),  // source
                        reader.GetString(2),  // content
                        score,
                        metadata
                    ));
                }
            }
            
            // Order by score descending and take topK
            var topResults = results
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .Select(r => new SearchResult
                {
                    DocumentId = r.Id,
                    Content = r.Content,
                    Score = r.Score,
                    Collection = collection,
                    Metadata = r.Metadata
                })
                .ToList();
            
            return Task.FromResult<IReadOnlyList<SearchResult>>(topResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search collection: {Collection}", collection);
            return Task.FromResult<IReadOnlyList<SearchResult>>(new List<SearchResult>());
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
