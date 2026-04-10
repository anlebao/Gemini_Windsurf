using System.ComponentModel.DataAnnotations;

namespace VanAn.Shared.DTOs;

/// <summary>
/// Voice note data transfer object with defensive constraints
/// </summary>
public record VoiceNoteDto
{
    [MaxLength(500)]  // 🛡️ DEFENSIVE: Max 500 chars
    public string? Text { get; init; }
    
    [MaxLength(150000)] // 🛡️ DEFENSIVE: Max ~110KB Base64
    public string? AudioBlob { get; init; }
    
    public bool TranscriptionSuccessful { get; init; }
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}
