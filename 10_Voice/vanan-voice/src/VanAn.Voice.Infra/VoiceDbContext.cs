using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Infra;

public sealed class VoiceDbContext(DbContextOptions<VoiceDbContext> options) : DbContext(options)
{
    public DbSet<VoiceCall> VoiceCalls => Set<VoiceCall>();
    public DbSet<VoiceCallEvent> VoiceCallEvents => Set<VoiceCallEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var call = modelBuilder.Entity<VoiceCall>();
        call.ToTable("voice_calls");
        call.HasKey(e => e.Id);
        call.Property(e => e.Id).ValueGeneratedNever();
        call.Property(e => e.Status).HasConversion<int>();
        call.Property(e => e.TenantId).IsRequired();
        call.Property(e => e.SourceApplication).HasMaxLength(64);
        call.Property(e => e.ContextType).HasMaxLength(32);
        call.Property(e => e.ContextReferenceId).HasMaxLength(128);
        call.Property(e => e.ConnectionType).HasMaxLength(16);
        call.Property(e => e.EndReason).HasMaxLength(64);
        call.Property(e => e.CallerConnectionId).HasMaxLength(128);
        call.Property(e => e.ReceiverConnectionId).HasMaxLength(128);

        var evt = modelBuilder.Entity<VoiceCallEvent>();
        evt.ToTable("voice_call_events");
        evt.HasKey(e => e.Id);
        evt.Property(e => e.EventType).HasMaxLength(64).IsRequired();
        evt.Property(e => e.MetadataJson).HasColumnType("TEXT");

        // FR-007 DB-level enforcement — partial unique index per active user role.
        // SQLite supports filtered indexes; statuses are stored as int (see CallStatus).
        call.HasIndex(e => e.CallerUserId)
            .HasDatabaseName("ux_voice_calls_active_caller")
            .HasFilter("Status IN (0,1,2,3,4,5)");
        call.HasIndex(e => e.ReceiverUserId)
            .HasDatabaseName("ux_voice_calls_active_receiver")
            .HasFilter("Status IN (0,1,2,3,4,5)");
    }
}