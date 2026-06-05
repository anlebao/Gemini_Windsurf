namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Voice note data transfer object with defensive constraints
    /// </summary>
    public record VoiceNoteDto
    {
        public string? Text { get; init; }

        public string? AudioBlob { get; init; }

        public bool TranscriptionSuccessful { get; init; }
        public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
    }
}
