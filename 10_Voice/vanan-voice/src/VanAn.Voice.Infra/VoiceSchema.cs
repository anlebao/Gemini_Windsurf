using Microsoft.EntityFrameworkCore;

namespace VanAn.Voice.Infra;

/// <summary>
/// Idempotent schema bootstrap (SRS v1.1 §20: no EF EnsureCreated in production).
/// Raw DDL keeps the standalone SQLite store stable across restarts and lets the
/// partial unique indexes (FR-007) exist from day one.
/// </summary>
public static class VoiceSchema
{
    public static void EnsureVoiceSchema(this VoiceDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS voice_calls (
                Id TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                CallerUserId TEXT NOT NULL,
                ReceiverUserId TEXT NOT NULL,
                SourceApplication TEXT NOT NULL,
                ContextType TEXT NULL,
                ContextReferenceId TEXT NULL,
                Status INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                AcceptedAt TEXT NULL,
                EndedAt TEXT NULL,
                DurationSeconds INTEGER NOT NULL,
                ConnectionType TEXT NULL,
                EndReason TEXT NULL,
                CallerConnectionId TEXT NULL,
                ReceiverConnectionId TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS voice_call_events (
                Id TEXT NOT NULL PRIMARY KEY,
                CallId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                ActorUserId TEXT NULL,
                OccurredAt TEXT NOT NULL,
                MetadataJson TEXT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_voice_calls_tenant_started ON voice_calls (TenantId, StartedAt);");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_voice_calls_caller_started ON voice_calls (CallerUserId, StartedAt);");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_voice_calls_receiver_started ON voice_calls (ReceiverUserId, StartedAt);");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_voice_call_events_call ON voice_call_events (CallId, OccurredAt);");

        // FR-007: one active call per user (caller side and receiver side).
        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_voice_calls_active_caller
            ON voice_calls (CallerUserId) WHERE Status IN (0,1,2,3,4,5);
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_voice_calls_active_receiver
            ON voice_calls (ReceiverUserId) WHERE Status IN (0,1,2,3,4,5);
            """);
    }
}