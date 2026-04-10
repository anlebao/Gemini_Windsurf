using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using VanAn.Unit.Tests.Domain;
using VanAn.Unit.Tests.Services;
using VanAn.Unit.Tests.Repositories;

namespace VanAn.Unit.Tests;

/// <summary>
/// TDD-compliant Unit tests for Lead Management Service
/// Layer 1: Unit Tests - Lead Lifecycle Management
/// </summary>
public class TDDLeadManagementServiceTests : TestBase
{
    [Fact(DisplayName = "CreateLead_ValidLead_ShouldCreateAndReturnLead")]
    public async Task CreateLead_ValidLead_ShouldCreateAndReturnLead()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadManagementService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var leadActivityRepositoryMock = CreateLeadActivityRepositoryMock();
        
        var service = new LeadManagementService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            leadActivityRepositoryMock.Object);

        var lead = CreateTestLead();

        // Act
        var result = await service.CreateLeadAsync(lead);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lead.FullName, result.FullName);
        Assert.Equal(lead.PhoneNumber, result.PhoneNumber);
        Assert.Equal(lead.Email, result.Email);
        Assert.Equal(LeadStatus.New, result.Status);
        Assert.True(result.CreatedAt > DateTime.MinValue);

        // Verify repositories were called
        leadRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Lead>()), Times.Once);
        leadActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<LeadActivity>()), Times.Once);
    }

    [Fact(DisplayName = "UpdateLeadStatus_ValidStatus_ShouldUpdateAndLogActivity")]
    public async Task UpdateLeadStatus_ValidStatus_ShouldUpdateAndLogActivity()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadManagementService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var leadActivityRepositoryMock = CreateLeadActivityRepositoryMock();
        
        var service = new LeadManagementService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            leadActivityRepositoryMock.Object);

        var leadId = Guid.NewGuid();
        var newStatus = LeadStatus.Qualified;
        var staffId = Guid.NewGuid();

        var existingLead = CreateTestLead();
        existingLead.Id = leadId;
        existingLead.Status = LeadStatus.New;
        existingLead.UpdatedAt = DateTime.UtcNow.AddMinutes(-1); // Set to past time

        leadRepositoryMock
            .Setup(x => x.GetByIdAsync(leadId))
            .ReturnsAsync(existingLead);

        // Setup UpdateAsync to actually update the object
        leadRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Lead>()))
            .Callback<Lead>(lead => existingLead = lead)
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.UpdateLeadStatusAsync(leadId, newStatus, staffId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newStatus, result.Status);
        Assert.True(result.UpdatedAt > DateTime.UtcNow.AddMinutes(-5)); // Updated within last 5 minutes

        // Verify repositories were called
        leadRepositoryMock.Verify(x => x.GetByIdAsync(leadId), Times.Once);
        leadRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Lead>()), Times.Once);
        leadActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<LeadActivity>()), Times.Once);
    }

    [Fact(DisplayName = "AssignLeadToStaff_ValidLead_ShouldAssignAndLogActivity")]
    public async Task AssignLeadToStaff_ValidLead_ShouldAssignAndLogActivity()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadManagementService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var leadActivityRepositoryMock = CreateLeadActivityRepositoryMock();
        
        var service = new LeadManagementService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            leadActivityRepositoryMock.Object);

        var leadId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var existingLead = CreateTestLead();
        existingLead.Id = leadId;
        existingLead.AssignedStaffId = null;
        existingLead.UpdatedAt = DateTime.UtcNow.AddMinutes(-1); // Set to past time

        leadRepositoryMock
            .Setup(x => x.GetByIdAsync(leadId))
            .ReturnsAsync(existingLead);

        // Setup UpdateAsync to actually update the object
        leadRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Lead>()))
            .Callback<Lead>(lead => existingLead = lead)
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.AssignLeadToStaffAsync(leadId, staffId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(staffId, result.AssignedStaffId);
        Assert.True(result.UpdatedAt > DateTime.UtcNow.AddMinutes(-5)); // Updated within last 5 minutes

        // Verify repositories were called
        leadRepositoryMock.Verify(x => x.GetByIdAsync(leadId), Times.Once);
        leadRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Lead>()), Times.Once);
        leadActivityRepositoryMock.Verify(x => x.AddAsync(It.IsAny<LeadActivity>()), Times.Once);
    }

    [Fact(DisplayName = "GetLeadsByStatus_ValidStatus_ShouldReturnFilteredLeads")]
    public async Task GetLeadsByStatus_ValidStatus_ShouldReturnFilteredLeads()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadManagementService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var leadActivityRepositoryMock = CreateLeadActivityRepositoryMock();
        
        var service = new LeadManagementService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            leadActivityRepositoryMock.Object);

        var status = LeadStatus.New;
        var expectedLeads = new List<Lead>
        {
            CreateTestLead(),
            CreateTestLead()
        };
        expectedLeads[0].Status = status;
        expectedLeads[1].Status = status;

        leadRepositoryMock
            .Setup(x => x.GetByStatusAsync(status))
            .ReturnsAsync(expectedLeads);

        // Act
        var result = await service.GetLeadsByStatusAsync(status);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, lead => Assert.Equal(status, lead.Status));

        // Verify repository was called
        leadRepositoryMock.Verify(x => x.GetByStatusAsync(status), Times.Once);
    }
}

// TDD-compliant implementation (will fail initially - RED phase)
public class LeadManagementService : ILeadManagementService
{
    private readonly ILogger<LeadManagementService> _logger;
    private readonly ILeadRepository _leadRepository;
    private readonly ILeadActivityRepository _leadActivityRepository;

    public LeadManagementService(
        ILogger<LeadManagementService> logger,
        ILeadRepository leadRepository,
        ILeadActivityRepository leadActivityRepository)
    {
        _logger = logger;
        _leadRepository = leadRepository;
        _leadActivityRepository = leadActivityRepository;
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Creating new lead: {FullName}", lead.FullName);
        
        lead.Id = Guid.NewGuid();
        lead.Status = LeadStatus.New;
        lead.CreatedAt = DateTime.UtcNow;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.ContactAttempts = 0;
        lead.LeadScore = 0;

        await _leadRepository.AddAsync(lead);

        // Log creation activity
        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            ActivityType = LeadActivityType.Created,
            Description = "Lead created",
            ActivityDate = DateTime.UtcNow
        };

        await _leadActivityRepository.AddAsync(activity);

        _logger.LogInformation("Lead created successfully: {LeadId}", lead.Id);
        return lead;
    }

    public async Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Updating lead status: {LeadId} to {Status}", leadId, status);
        
        var lead = await _leadRepository.GetByIdAsync(leadId);
        if (lead == null)
        {
            throw new ArgumentException($"Lead not found: {leadId}");
        }

        var oldStatus = lead.Status;
        lead.Status = status;
        lead.UpdatedAt = DateTime.UtcNow;

        if (staffId.HasValue)
        {
            lead.AssignedStaffId = staffId;
        }

        await _leadRepository.UpdateAsync(lead);

        // Log status change activity
        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            ActivityType = LeadActivityType.StatusChanged,
            Description = $"Status changed from {oldStatus} to {status}",
            CreatedByStaffId = staffId,
            ActivityDate = DateTime.UtcNow
        };

        await _leadActivityRepository.AddAsync(activity);

        _logger.LogInformation("Lead status updated: {LeadId} to {Status}", leadId, status);
        return lead;
    }

    public async Task<Lead> AssignLeadToStaffAsync(Guid leadId, Guid staffId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Assigning lead {LeadId} to staff {StaffId}", leadId, staffId);
        
        var lead = await _leadRepository.GetByIdAsync(leadId);
        if (lead == null)
        {
            throw new ArgumentException($"Lead not found: {leadId}");
        }

        lead.AssignedStaffId = staffId;
        lead.UpdatedAt = DateTime.UtcNow;

        await _leadRepository.UpdateAsync(lead);

        // Log assignment activity
        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            ActivityType = LeadActivityType.Created,
            Description = $"Lead assigned to staff member",
            CreatedByStaffId = staffId,
            ActivityDate = DateTime.UtcNow
        };

        await _leadActivityRepository.AddAsync(activity);

        _logger.LogInformation("Lead assigned: {LeadId} to staff {StaffId}", leadId, staffId);
        return lead;
    }

    public async Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Retrieving leads by status: {Status}", status);
        
        var leads = await _leadRepository.GetByStatusAsync(status);
        
        _logger.LogInformation("Retrieved {Count} leads with status {Status}", leads.Count, status);
        return leads;
    }
}
