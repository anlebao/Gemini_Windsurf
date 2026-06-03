using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Logging;

namespace VanAn.Gateway.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class VoiceCommandController : ControllerBase
{
    private readonly IVoiceCommandService _voiceCommandService;
    private readonly IAudioStorageService _audioStorageService;
    private readonly ILogger<VoiceCommandController> _logger;
    private readonly string _audioDirectory = "./audio-storage"; // Hardcoded safe directory

    public VoiceCommandController(
        IVoiceCommandService voiceCommandService,
        IAudioStorageService audioStorageService,
        ILogger<VoiceCommandController> logger)
    {
        _voiceCommandService = voiceCommandService;
        _audioStorageService = audioStorageService;
        _logger = logger;
    }

    [HttpPost("process-audio")]
    public async Task<ActionResult<VoiceCommand>> ProcessAudioCommand(
        [FromForm] IFormFile audioFile,
        [FromForm] string orderId)
    {
        try
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest(new { error = "No audio file provided" });
            }

            // Read audio data
            using var memoryStream = new MemoryStream();
            await audioFile.CopyToAsync(memoryStream);
            var audioData = memoryStream.ToArray();

            // Save audio file
            var savedAudio = await _audioStorageService.SaveAudioAsync(
                audioData, audioFile.FileName, orderId);

            // Convert audio to text and process command
            var base64Audio = Convert.ToBase64String(audioData);
            var commandResult = await _voiceCommandService.ProcessVoiceCommandAsync(base64Audio, orderId);

            // Execute command if valid
            if (commandResult)
            {
                // Command processed successfully
            }

            return Ok(new { Success = commandResult });
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.AudioCommandError(_logger, ex, orderId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("text-command")]
    public async Task<ActionResult<VoiceCommand>> ProcessTextCommand([FromBody] TextCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            if (string.IsNullOrEmpty(request.CommandText))
            {
                return BadRequest(new { error = "Command text is required" });
            }

            // Process text command directly
            var commandResult = await _voiceCommandService.ProcessVoiceCommandAsync(request.CommandText, request.OrderId ?? Guid.NewGuid().ToString());

            return Ok(new { Success = commandResult });
        }
        catch (ArgumentException ex)
        {
            VoiceCommandLogger.TextCommandArgumentError(_logger, ex, request.CommandText);
            return BadRequest(new { error = "Invalid command format" });
        }
        catch (InvalidOperationException ex)
        {
            VoiceCommandLogger.TextCommandOperationError(_logger, ex, request.CommandText);
            return BadRequest(new { error = "Command cannot be executed" });
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.TextCommandError(_logger, ex, request.CommandText);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("tts")]
    public ActionResult<string> TextToSpeech([FromBody] TtsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            if (string.IsNullOrEmpty(request.Text))
            {
                return BadRequest(new { error = "Text is required" });
            }

            // For now, return a dummy audio URL
            var audioUrl = "/audio/speech.mp3";

            return Ok(new { AudioUrl = audioUrl });
        }
        catch (ArgumentException ex)
        {
            VoiceCommandLogger.TextToSpeechArgumentError(_logger, ex, request.Text);
            return BadRequest(new { error = "Invalid text format" });
        }
        catch (InvalidOperationException ex)
        {
            VoiceCommandLogger.TextToSpeechUnavailableError(_logger, ex, request.Text);
            return StatusCode(503, new { error = "Text to speech service unavailable" });
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.TextToSpeechError(_logger, ex, request.Text);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("audio/{audioId}")]
    public async Task<ActionResult> GetAudioFile(string audioId)
    {
        try
        {
            // 1. Kiểm tra Regex nghiêm ngặt (chỉ cho phép chữ, số, gạch ngang)
            if (!System.Text.RegularExpressions.Regex.IsMatch(audioId, @"^[a-zA-Z0-9\-_]+$"))
            {
                return BadRequest("Invalid audio ID format.");
            }

            // 2. TẠO CHUỖI MỚI hoàn toàn từ việc lấy tên file an toàn
            var safeFileName = Path.GetFileNameWithoutExtension(audioId) + ".mp3"; // Ép cứng đuôi file
            var safePath = Path.Combine(_audioDirectory, safeFileName);

            // 3. Nếu Roslyn vẫn báo lỗi CA3003 sau khi đã cô lập chuỗi, HÃY DÙNG PRAGMA ĐỂ TẮT CỤC BỘ vì chúng ta đã validate tuyệt đối an toàn:
#pragma warning disable CA3003 // Taint-safe: Validated by strict Regex and forced extension
            if (!System.IO.File.Exists(safePath)) 
                return NotFound();
            var fileBytes = await System.IO.File.ReadAllBytesAsync(safePath, HttpContext.RequestAborted).ConfigureAwait(false);
#pragma warning restore CA3003

            var contentType = "audio/mpeg"; // Hardcoded for MP3
            return File(fileBytes, contentType, safeFileName);
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.GetAudioFileError(_logger, ex);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("audio/{audioId}")]
    public async Task<ActionResult<bool>> DeleteAudioFile(string audioId)
    {
        try
        {
            var deleted = await _audioStorageService.DeleteAudioAsync(audioId);
            return Ok(new { Deleted = deleted });
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.LogVoiceCommandError(_logger, ex);
            return StatusCode(500, false);
        }
    }

    [HttpPost("cleanup-expired")]
    public async Task<ActionResult<CleanupResult>> CleanupExpiredAudioFiles()
    {
        try
        {
            var cleaned = await _audioStorageService.CleanupExpiredAudioFilesAsync();
            var expiredFiles = await _audioStorageService.GetExpiredAudioFilesAsync();

            return Ok(new CleanupResult
            {
                CleanedFiles = cleaned,
                TotalExpired = expiredFiles.Count,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            VoiceCommandLogger.CleanupError(_logger, ex);
            return StatusCode(500, new CleanupResult
            {
                CleanedFiles = false,
                TotalExpired = 0,
                Timestamp = DateTime.UtcNow,
                Error = ex.Message
            });
        }
    }

    private static string ParseCommandType(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return "unknown";

        var lowerText = commandText.ToLowerInvariant();

        if (lowerText.Contains("sẵn sàng", StringComparison.OrdinalIgnoreCase) || 
            lowerText.Contains("ready", StringComparison.OrdinalIgnoreCase))
        {
            return "update_status";
        }
        if (lowerText.Contains("xong đơn", StringComparison.OrdinalIgnoreCase) || 
            lowerText.Contains("hoàn thành", StringComparison.OrdinalIgnoreCase))
        {
            return "update_status";
        }
        else if (lowerText.Contains("đơn mới", StringComparison.OrdinalIgnoreCase) || 
                 lowerText.Contains("mới", StringComparison.OrdinalIgnoreCase))
        {
            return "new_order";
        }
        else if (lowerText.Contains("sẵn sàng", StringComparison.OrdinalIgnoreCase))
        {
            return "update_status";
        }

        return "unknown";
    }
}

// Request/Response DTOs
public record TextCommandRequest
{
    public string CommandText { get; init; } = string.Empty;
    public string? OrderId { get; init; }
    public string? Parameters { get; init; }
}

public record TtsRequest
{
    public string Text { get; init; } = string.Empty;
    public string Language { get; init; } = "vi-VN";
}

public record CleanupResult
{
    public bool CleanedFiles { get; init; }
    public int TotalExpired { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Error { get; init; }
}
