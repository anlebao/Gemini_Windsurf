namespace VanAn.UI.Platform.Realtime;

/// <summary>
/// Realtime Platform P4: DTOs shared by the UI Platform realtime adapters and components.
/// Mirrors the JSON shapes of the Gateway /api/realtime/* endpoints (RealtimeController).
/// </summary>
public class RealtimeChatMessage
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class RealtimeChatHistoryResult
{
    public bool Success { get; set; }
    public Guid ConversationId { get; set; }
    public List<RealtimeChatMessage> Messages { get; set; } = new();
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class RealtimeSendResult
{
    public bool Success { get; set; }
    public Guid MessageId { get; set; }
    public DateTime SentAt { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>Latest known position of a subject. Lat/Lng are null when nothing has been recorded yet.</summary>
public class RealtimeLocationResult
{
    public bool Success { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public Guid? TrackerId { get; set; }
    public DateTime? RecordedAt { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class RealtimePingResult
{
    public bool Success { get; set; }
    public DateTime? RecordedAt { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>A lat/lng pair for map bounds (IMapJsAdapter.FitBoundsAsync).</summary>
public class RealtimeMapPoint
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
