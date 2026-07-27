using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Loyalty-C WS-B/C: Mission system endpoints.
    /// Admin: mission CRUD (create, update, delete, list).
    /// Customer: view active missions + own completion history.
    /// Routes:
    ///   === Admin (cookie auth) ===
    ///   GET    /api/missions                    — list all missions (incl. inactive)
    ///   GET    /api/missions/active             — list active missions (customer-facing)
    ///   GET    /api/missions/{id}               — get mission detail
    ///   POST   /api/missions                    — create mission
    ///   PUT    /api/missions/{id}               — update mission
    ///   DELETE /api/missions/{id}               — soft-delete mission
    ///   === Customer (token auth) ===
    ///   GET    /api/missions/my/progress        — active missions + completion status
    ///   GET    /api/missions/my/completions     — customer's completion history
    /// </summary>
    [ApiController]
    [Route("api/missions")]
    [ResolveCustomerTenant]
    public class MissionsController(
        IMissionService missionService,
        ICustomerTokenService customerTokenService,
        ILogger<MissionsController> logger) : ControllerBase
    {
        private readonly IMissionService _missionService = missionService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ILogger<MissionsController> _logger = logger;

        // === Admin: Mission CRUD ===

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllMissions()
        {
            var missions = await _missionService.GetAllMissionsAsync();
            return Ok(missions.Select(MapMissionDto).ToList());
        }

        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveMissions()
        {
            var missions = await _missionService.GetActiveMissionsAsync();
            return Ok(missions.Select(MapMissionDto).ToList());
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> GetMission(Guid id)
        {
            var mission = await _missionService.GetMissionAsync(id);
            if (mission == null) return NotFound();
            return Ok(MapMissionDto(mission));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateMission([FromBody] CreateMissionRequest request)
        {
            try
            {
                var mission = await _missionService.CreateMissionAsync(
                    request.MissionType, request.Title, request.Description, request.PointsReward,
                    request.IsOneTime, request.DailyCap, request.SortOrder, request.Config);
                return CreatedAtAction(nameof(GetMission), new { id = mission.Id }, MapMissionDto(mission));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> UpdateMission(Guid id, [FromBody] UpdateMissionRequest request)
        {
            try
            {
                var mission = await _missionService.UpdateMissionAsync(
                    id, request.Title, request.Description, request.PointsReward,
                    request.IsOneTime, request.DailyCap, request.IsActive, request.SortOrder, request.Config);
                return Ok(MapMissionDto(mission));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> DeleteMission(Guid id)
        {
            bool deleted = await _missionService.DeleteMissionAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }

        // === Customer: Progress + Completions ===

        [HttpGet("my/progress")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMyProgress([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var missions = await _missionService.GetCustomerProgressAsync(customerId.Value);
            var completions = await _missionService.GetCustomerCompletionsAsync(customerId.Value);

            // Build progress DTO: for each mission, show completion count + last completion date
            var progress = missions.Select(m =>
            {
                var missionCompletions = completions.Where(c => c.MissionId == m.Id).ToList();
                return new MissionProgressDto
                {
                    MissionId = m.Id,
                    MissionType = m.MissionType.ToString(),
                    Title = m.Title,
                    Description = m.Description,
                    PointsReward = m.PointsReward,
                    IsOneTime = m.IsOneTime,
                    DailyCap = m.DailyCap,
                    CompletionCount = missionCompletions.Count,
                    LastCompletedAt = missionCompletions.FirstOrDefault()?.CompletedAt,
                    IsCompleted = m.IsOneTime && missionCompletions.Count > 0
                };
            }).ToList();

            return Ok(progress);
        }

        [HttpGet("my/completions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMyCompletions([FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = _customerTokenService.ValidateToken(token);
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

            var completions = await _missionService.GetCustomerCompletionsAsync(customerId.Value);
            return Ok(completions.Select(MapCompletionDto).ToList());
        }

        // === DTO Mappers ===

        private static MissionDto MapMissionDto(Mission m) => new()
        {
            Id = m.Id,
            MissionType = m.MissionType.ToString(),
            Title = m.Title,
            Description = m.Description,
            PointsReward = m.PointsReward,
            IsOneTime = m.IsOneTime,
            DailyCap = m.DailyCap,
            IsActive = m.IsActive,
            SortOrder = m.SortOrder,
            Config = m.Config
        };

        private static MissionCompletionDto MapCompletionDto(MissionCompletion c) => new()
        {
            Id = c.Id,
            MissionId = c.MissionId,
            CustomerId = c.CustomerId,
            CompletedAt = c.CompletedAt,
            PointsAwarded = c.PointsAwarded,
            Metadata = c.Metadata
        };
    }

    // === DTOs ===

    public class MissionDto
    {
        public Guid Id { get; set; }
        public string MissionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsReward { get; set; }
        public bool IsOneTime { get; set; }
        public int? DailyCap { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? Config { get; set; }
    }

    public class MissionProgressDto
    {
        public Guid MissionId { get; set; }
        public string MissionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsReward { get; set; }
        public bool IsOneTime { get; set; }
        public int? DailyCap { get; set; }
        public int CompletionCount { get; set; }
        public DateTime? LastCompletedAt { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class MissionCompletionDto
    {
        public Guid Id { get; set; }
        public Guid MissionId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime CompletedAt { get; set; }
        public int PointsAwarded { get; set; }
        public string? Metadata { get; set; }
    }

    public class CreateMissionRequest
    {
        public MissionType MissionType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsReward { get; set; }
        public bool IsOneTime { get; set; } = true;
        public int? DailyCap { get; set; }
        public int SortOrder { get; set; } = 0;
        public string? Config { get; set; }
    }

    public class UpdateMissionRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsReward { get; set; }
        public bool IsOneTime { get; set; } = true;
        public int? DailyCap { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public string? Config { get; set; }
    }
}
