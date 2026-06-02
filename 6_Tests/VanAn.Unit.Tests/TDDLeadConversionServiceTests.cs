using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using VanAn.Shared.Domain;
using VanAn.Unit.Tests.Domain;
using VanAn.Unit.Tests.Services;
using VanAn.Unit.Tests.Repositories;

namespace VanAn.Unit.Tests;

/// <summary>
/// TDD-compliant Unit tests for Lead Conversion Service
/// Layer 1: Unit Tests - Lead to Customer Conversion
/// </summary>
public class TDDLeadConversionServiceTests : TestBase
{
    [Fact(DisplayName = "ConvertLeadToCustomer_ValidLead_ShouldCreateCustomerAndUpdateLead")]
    public async Task ConvertLeadToCustomer_ValidLead_ShouldCreateCustomerAndUpdateLead()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadConversionService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var loyaltyRewardsServiceMock = CreateLoyaltyRewardsServiceMock();
        
        var service = new LeadConversionService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            customerRepositoryMock.Object,
            customerOnboardingRepositoryMock.Object,
            loyaltyRewardsServiceMock.Object);

        var leadId = Guid.NewGuid();
        var conversionReason = "Qualified lead - ready to convert";
        var lead = CreateTestLead();
        lead.Id = leadId;
        lead.Status = LeadStatus.Qualified;
        lead.PhoneNumber = "0987654321";

        leadRepositoryMock
            .Setup(x => x.GetByIdAsync(leadId))
            .ReturnsAsync(lead);

        leadRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Lead>()))
            .Returns(Task.CompletedTask);

        customerRepositoryMock
            .Setup(x => x.GetByPhoneNumberAsync(lead.PhoneNumber))
            .ReturnsAsync((Customer?)null);

        customerRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        customerOnboardingRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        loyaltyRewardsServiceMock
            .Setup(x => x.InitializeCustomerRewardsAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Setup customer repository mock for the new customer that will be created
        customerRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid customerId) => new Customer(new TenantId(Guid.NewGuid()), lead.FullName, lead.PhoneNumber));

        // Act
        var result = await service.ConvertLeadToCustomerAsync(leadId, conversionReason);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lead.FullName, result.FullName);
        Assert.Equal(lead.PhoneNumber, result.PhoneNumber);
        Assert.Equal(lead.Email, result.Email);
        Assert.Equal("Bronze", result.CustomerTier);
        Assert.Equal(50, result.LoyaltyPoints); // Welcome points
        Assert.True(result.IsActive);

        // Verify repositories were called
        leadRepositoryMock.Verify(x => x.GetByIdAsync(leadId), Times.Once);
        leadRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Lead>()), Times.Once);
        customerRepositoryMock.Verify(x => x.GetByPhoneNumberAsync(lead.PhoneNumber), Times.Once);
        customerRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Customer>()), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.AddAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
        loyaltyRewardsServiceMock.Verify(x => x.InitializeCustomerRewardsAsync(It.IsAny<Guid>(), 50), Times.Once);
    }

    [Fact(DisplayName = "ConvertLeadToCustomer_DuplicatePhone_ShouldReturnExistingCustomer")]
    public async Task ConvertLeadToCustomer_DuplicatePhone_ShouldReturnExistingCustomer()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadConversionService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var loyaltyRewardsServiceMock = CreateLoyaltyRewardsServiceMock();
        
        var service = new LeadConversionService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            customerRepositoryMock.Object,
            customerOnboardingRepositoryMock.Object,
            loyaltyRewardsServiceMock.Object);

        var leadId = Guid.NewGuid();
        var conversionReason = "Qualified lead - ready to convert";
        var lead = CreateTestLead();
        lead.Id = leadId;
        lead.Status = LeadStatus.Qualified;
        lead.PhoneNumber = "0987654321";

        var existingCustomer = CreateTestCustomer();
        existingCustomer.PhoneNumber = lead.PhoneNumber;

        leadRepositoryMock
            .Setup(x => x.GetByIdAsync(leadId))
            .ReturnsAsync(lead);

        customerRepositoryMock
            .Setup(x => x.GetByPhoneNumberAsync(lead.PhoneNumber))
            .ReturnsAsync(existingCustomer);

        // Act
        var result = await service.ConvertLeadToCustomerAsync(leadId, conversionReason);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingCustomer.Id, result.Id);
        Assert.Equal(existingCustomer.FullName, result.FullName);
        Assert.Equal(existingCustomer.PhoneNumber, result.PhoneNumber);

        // Verify repositories were called correctly
        leadRepositoryMock.Verify(x => x.GetByIdAsync(leadId), Times.Once);
        customerRepositoryMock.Verify(x => x.GetByPhoneNumberAsync(lead.PhoneNumber), Times.Once);
        customerRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Customer>()), Times.Never); // Should not add duplicate
    }

    [Fact(DisplayName = "ValidateLeadForConversion_QualifiedLead_ShouldReturnTrue")]
    public async Task ValidateLeadForConversion_QualifiedLead_ShouldReturnTrue()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadConversionService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var loyaltyRewardsServiceMock = CreateLoyaltyRewardsServiceMock();
        
        var service = new LeadConversionService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            customerRepositoryMock.Object,
            customerOnboardingRepositoryMock.Object,
            loyaltyRewardsServiceMock.Object);

        var leadId = Guid.NewGuid();
        var lead = CreateTestLead();
        lead.Id = leadId;
        lead.Status = LeadStatus.Qualified;
        lead.LeadScore = 80;
        lead.PhoneNumber = "0987654321";
        lead.Email = "test@example.com";

        leadRepositoryMock
            .Setup(x => x.GetByIdAsync(leadId))
            .ReturnsAsync(lead);

        // Act
        var result = await service.ValidateLeadForConversionAsync(leadId);

        // Assert
        Assert.True(result);
        leadRepositoryMock.Verify(x => x.GetByIdAsync(leadId), Times.Once);
    }

    [Fact(DisplayName = "StartCustomerOnboarding_NewCustomer_ShouldCreateOnboardingRecord")]
    public async Task StartCustomerOnboarding_NewCustomer_ShouldCreateOnboardingRecord()
    {
        // Arrange
        var loggerMock = CreateLoggerMock<LeadConversionService>();
        var leadRepositoryMock = CreateLeadRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var customerOnboardingRepositoryMock = CreateCustomerOnboardingRepositoryMock();
        var loyaltyRewardsServiceMock = CreateLoyaltyRewardsServiceMock();
        
        var service = new LeadConversionService(
            loggerMock.Object, 
            leadRepositoryMock.Object, 
            customerRepositoryMock.Object,
            customerOnboardingRepositoryMock.Object,
            loyaltyRewardsServiceMock.Object);

        var customerId = Guid.NewGuid();
        var customer = CreateTestCustomer();
        customer.Id = customerId;

        customerRepositoryMock
            .Setup(x => x.GetByIdAsync(customerId))
            .ReturnsAsync(customer);

        customerOnboardingRepositoryMock
            .Setup(x => x.GetByCustomerIdAsync(customerId))
            .ReturnsAsync((CustomerOnboarding?)null);

        customerOnboardingRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<CustomerOnboarding>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.StartCustomerOnboardingAsync(customerId);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(OnboardingStatus.NotStarted, result.Status);
        Assert.Equal(OnboardingStep.Welcome, result.CurrentStep);

        // Verify repositories were called
        customerRepositoryMock.Verify(x => x.GetByIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.GetByCustomerIdAsync(customerId), Times.Once);
        customerOnboardingRepositoryMock.Verify(x => x.AddAsync(It.IsAny<CustomerOnboarding>()), Times.Once);
    }
}

// TDD-compliant implementation (will fail initially - RED phase)
public class LeadConversionService : ILeadConversionService
{
    private readonly ILogger<LeadConversionService> _logger;
    private readonly ILeadRepository _leadRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerOnboardingRepository _customerOnboardingRepository;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;

    public LeadConversionService(
        ILogger<LeadConversionService> logger,
        ILeadRepository leadRepository,
        ICustomerRepository customerRepository,
        ICustomerOnboardingRepository customerOnboardingRepository,
        ILoyaltyRewardsService loyaltyRewardsService)
    {
        _logger = logger;
        _leadRepository = leadRepository;
        _customerRepository = customerRepository;
        _customerOnboardingRepository = customerOnboardingRepository;
        _loyaltyRewardsService = loyaltyRewardsService;
    }

    public async Task<Customer> ConvertLeadToCustomerAsync(Guid leadId, string conversionReason)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Converting lead {LeadId} to customer", leadId);
        
        var lead = await _leadRepository.GetByIdAsync(leadId);
        if (lead == null)
        {
            throw new ArgumentException($"Lead not found: {leadId}");
        }

        // Check for existing customer with same phone number
        var existingCustomer = await _customerRepository.GetByPhoneNumberAsync(lead.PhoneNumber);
        if (existingCustomer != null)
        {
            _logger.LogInformation("Customer already exists with phone {Phone}", lead.PhoneNumber);
            return existingCustomer;
        }

        // Create new customer using production Customer from VanAn.Shared.Domain
        var customer = new Customer(lead.TenantId, lead.FullName, lead.PhoneNumber, lead.Email)
        {
            CustomerTier = "Bronze",
            LoyaltyPoints = 50, // Welcome points
            IsActive = true
        };

        await _customerRepository.AddAsync(customer);

        // Update lead status
        lead.Status = LeadStatus.Converted;
        lead.ConvertedCustomerId = customer.Id;
        lead.ConversionDate = DateTime.UtcNow;
        lead.ConversionReason = conversionReason;
        lead.UpdatedAt = DateTime.UtcNow;

        await _leadRepository.UpdateAsync(lead);

        // Start onboarding
        await StartCustomerOnboardingAsync(customer.Id);

        // Initialize loyalty rewards
        await _loyaltyRewardsService.InitializeCustomerRewardsAsync(customer.Id, 50);

        _logger.LogInformation("Lead converted successfully: {LeadId} -> {CustomerId}", leadId, customer.Id);
        return customer;
    }

    public async Task<bool> ValidateLeadForConversionAsync(Guid leadId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Validating lead for conversion: {LeadId}", leadId);
        
        var lead = await _leadRepository.GetByIdAsync(leadId);
        if (lead == null)
        {
            _logger.LogWarning("Lead not found for validation: {LeadId}", leadId);
            return false;
        }

        // Validation rules
        var isValid = lead.Status == LeadStatus.Qualified &&
                     lead.LeadScore >= 70 &&
                     !string.IsNullOrEmpty(lead.PhoneNumber) &&
                     !string.IsNullOrEmpty(lead.Email) &&
                     !string.IsNullOrEmpty(lead.FullName);

        _logger.LogInformation("Lead validation result: {LeadId} -> {IsValid}", leadId, isValid);
        return isValid;
    }

    public async Task<CustomerOnboarding> StartCustomerOnboardingAsync(Guid customerId)
    {
        // GREEN PHASE: Full implementation
        _logger.LogInformation("Starting onboarding for customer: {CustomerId}", customerId);
        
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
        {
            throw new ArgumentException($"Customer not found: {customerId}");
        }

        // Check if onboarding already exists
        var existingOnboarding = await _customerOnboardingRepository.GetByCustomerIdAsync(customerId);
        if (existingOnboarding != null)
        {
            _logger.LogInformation("Onboarding already exists for customer: {CustomerId}", customerId);
            return existingOnboarding;
        }

        // Create new onboarding
        var onboarding = new CustomerOnboarding
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OnboardingStatus.NotStarted,
            CurrentStep = OnboardingStep.Welcome,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TenantId = customer.TenantId
        };

        await _customerOnboardingRepository.AddAsync(onboarding);

        _logger.LogInformation("Onboarding started for customer: {CustomerId}", customerId);
        return onboarding;
    }
}
