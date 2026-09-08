using System.Threading.Channels;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 3 EXPANDED: Bounded in-memory queue cho async audit logging.
/// Security + KhachLink audit logs enqueue ở đây (fire-and-forget) — không chặn luồng chính.
/// Accounting audit KHÔNG qua queue — AuditTrailService ghi sync trực tiếp (bảo toàn kế toán).
/// FullMode = DropOldest: khi đầy (capacity 1000) log cũ nhất bị drop — app không bao giờ block.
/// </summary>
public class AuditLogQueue
{
    private readonly Channel<AuditLog> _channel = Channel.CreateBounded<AuditLog>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true  // chỉ AuditLogBackgroundWriter đọc
        });

    /// <summary>Non-blocking enqueue. Trả false nếu channel đầy và log bị drop (best-effort).</summary>
    public bool TryEnqueue(AuditLog log) => _channel.Writer.TryWrite(log);

    /// <summary>Dequeue 1 log nếu có (không block). Trả false nếu queue rỗng.</summary>
    public bool TryDequeue(out AuditLog log) => _channel.Reader.TryRead(out log!);

    /// <summary>Số log đang chờ trong queue (monitoring/diagnostics).</summary>
    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;
}
