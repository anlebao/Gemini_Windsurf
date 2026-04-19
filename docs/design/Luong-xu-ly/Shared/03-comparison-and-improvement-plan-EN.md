# Shared - Comparison and Improvement Plan

**Date:** April 11, 2026  
**Module:** 1_Shared  
**Status:** Analysis comparison and improvement planning

---

## **1. REALISTIC vs IDEAL COMPARISON**

### **1.1 Domain Model Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Entities** | Basic POCO entities | Rich entities with behaviors | **High** - Need rich domain models |
| **Value Objects** | Limited value objects | Immutable value objects | **High** - Need comprehensive VOs |
| **Aggregates** | No aggregate boundaries | Proper aggregate design | **High** - Need aggregate implementation |
| **Domain Events** | No domain events | Event-driven domain | **High** - Need event system |
| **Business Rules** | Hard-coded rules | Domain rules engine | **High** - Need rules engine |

### **1.2 Data Structure Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Entity Relationships** | Basic navigation properties | Rich relationships with validation | **Medium** - Need relationship enhancement |
| **Data Validation** | Basic attributes | FluentValidation with business rules | **Medium** - Need validation framework |
| **Database Mapping** | Basic EF Core mapping | Advanced mapping with configurations | **Medium** - Need mapping enhancement |
| **Query Optimization** | Basic LINQ queries | Optimized queries with specifications | **Medium** - Need query optimization |
| **Data Integrity** | Basic constraints | Comprehensive integrity rules | **Medium** - Need integrity enhancement |

### **1.3 Architecture Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Clean Architecture** | Partial compliance | Full Clean Architecture | **High** - Need complete compliance |
| **DDD Patterns** | Limited DDD implementation | Full DDD patterns | **High** - Need DDD enhancement |
| **Separation of Concerns** | Mixed concerns | Clear separation | **Medium** - Need separation |
| **Dependency Management** | Basic DI | Advanced DI with lifetimes | **Medium** - Need DI enhancement |
| **Module Organization** | Basic structure | Proper module organization | **Medium** - Need organization |

### **1.4 Infrastructure Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Repository Pattern** | Basic implementation | Advanced repository with specifications | **Medium** - Need repository enhancement |
| **Unit of Work** | Basic EF Core context | Advanced unit of work | **Medium** - Need UoW enhancement |
| **Caching** | No caching | Multi-layer caching | **High** - Need caching implementation |
| **Logging** | Basic logging | Structured logging | **Medium** - Need logging enhancement |
| **Configuration** | Basic configuration | Advanced configuration management | **Medium** - Need config enhancement |

---

## **2. PRIORITY ASSESSMENT**

### **2.1 Critical Issues (Priority 1)**
1. **No Rich Domain Models** - Basic POCO entities only
2. **No Value Objects** - Limited value object implementation
3. **No Aggregate Boundaries** - No aggregate design
4. **No Domain Events** - No event-driven architecture
5. **No Business Rules Engine** - Hard-coded business logic

### **2.2 Important Issues (Priority 2)**
1. **No Advanced Validation** - Basic validation only
2. **No Repository Specifications** - Basic repository pattern
3. **No Caching Strategy** - No caching implementation
4. **No Query Optimization** - Basic LINQ queries only
5. **No Structured Logging** - Basic logging only

### **2.3 Nice to Have (Priority 3)**
1. **No Event Sourcing** - No event persistence
2. **No Snapshot Strategy** - No aggregate snapshots
3. **No Advanced Mapping** - Basic EF Core mapping
4. **No Performance Monitoring** - No performance tracking
5. **No Data Migration Strategy** - Basic migrations only

---

## **3. IMPROVEMENT PLAN**

### **3.1 Phase 1: Domain Model Enhancement (Week 1-2)**

#### **Day 1-3: Rich Domain Models**
```csharp
// Domain/Entities/Product.cs - Enhanced with domain logic
public class Product : AggregateRoot, IAggregateRoot
{
    private readonly List<ProductIngredient> _ingredients = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public ProductName Name { get; private set; }
    public ProductDescription Description { get; private set; }
    public Money Price { get; private set; }
    public ProductCategory Category { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    
    public IReadOnlyCollection<ProductIngredient> Ingredients => _ingredients.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Product() { } // For EF Core

    public Product(ProductName name, ProductDescription description, Money price, ProductCategory category)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Price = price;
        Category = category;
        Status = ProductStatus.Active;
        CreatedAt = DateTime.UtcNow;
        
        ValidateProduct();
        
        AddDomainEvent(new ProductCreatedEvent(Id, Name, Price, Category));
    }

    public void UpdatePrice(Money newPrice, Guid updatedBy)
    {
        if (newPrice <= 0)
            throw new DomainException("Price must be greater than 0");

        var oldPrice = Price;
        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        
        AddDomainEvent(new ProductPriceChangedEvent(Id, oldPrice, newPrice, updatedBy));
    }

    public void UpdateInformation(ProductName name, ProductDescription description, ProductCategory category, Guid updatedBy)
    {
        Name = name;
        Description = description;
        Category = category;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        
        AddDomainEvent(new ProductUpdatedEvent(Id, Name, Description, Category, updatedBy));
    }

    public void AddIngredient(IngredientId ingredientId, Quantity quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        var existingIngredient = _ingredients.FirstOrDefault(i => i.IngredientId == ingredientId);
        if (existingIngredient != null)
        {
            existingIngredient.UpdateQuantity(existingIngredient.Quantity + quantity);
        }
        else
        {
            var newIngredient = new ProductIngredient(ingredientId, quantity);
            _ingredients.Add(newIngredient);
        }

        AddDomainEvent(new ProductIngredientAddedEvent(Id, ingredientId, quantity));
    }

    public void RemoveIngredient(IngredientId ingredientId)
    {
        var ingredient = _ingredients.FirstOrDefault(i => i.IngredientId == ingredientId);
        if (ingredient != null)
        {
            _ingredients.Remove(ingredient);
            AddDomainEvent(new ProductIngredientRemovedEvent(Id, ingredientId));
        }
    }

    public void Activate()
    {
        if (Status == ProductStatus.Active)
            return;

        Status = ProductStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new ProductActivatedEvent(Id));
    }

    public void Deactivate(string reason)
    {
        if (Status == ProductStatus.Inactive)
            return;

        Status = ProductStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new ProductDeactivatedEvent(Id, reason));
    }

    public void Discontinue(string reason)
    {
        if (Status == ProductStatus.Discontinued)
            return;

        Status = ProductStatus.Discontinued;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new ProductDiscontinuedEvent(Id, reason));
    }

    public bool CanBeOrdered()
    {
        return Status == ProductStatus.Active && Ingredients.Any();
    }

    public Money CalculateCost(IIngredientPricingService pricingService)
    {
        var totalCost = Money.Zero;
        
        foreach (var ingredient in Ingredients)
        {
            var ingredientPrice = pricingService.GetPriceAsync(ingredient.IngredientId).Result;
            totalCost += ingredientPrice * ingredient.Quantity;
        }
        
        return totalCost;
    }

    public decimal CalculateProfitMargin(IIngredientPricingService pricingService)
    {
        var cost = CalculateCost(pricingService);
        if (cost.Amount == 0)
            return 0;

        return ((Price.Amount - cost.Amount) / Price.Amount) * 100;
    }

    private void ValidateProduct()
    {
        if (Name == null)
            throw new DomainException("Product name is required");

        if (Description == null)
            throw new DomainException("Product description is required");

        if (Price <= 0)
            throw new DomainException("Product price must be greater than 0");

        if (Category == null)
            throw new DomainException("Product category is required");
    }

    protected override void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Domain/ValueObjects/ProductName.cs
public class ProductName : ValueObject
{
    public string Value { get; }

    public const int MaxLength = 100;
    public const int MinLength = 2;

    protected ProductName() { } // For EF Core

    public ProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Product name cannot be empty");

        if (value.Length < MinLength)
            throw new DomainException($"Product name must be at least {MinLength} characters");

        if (value.Length > MaxLength)
            throw new DomainException($"Product name cannot exceed {MaxLength} characters");

        Value = value.Trim();
    }

    public static implicit operator string(ProductName productName) => productName.Value;
    public static implicit operator ProductName(string value) => new ProductName(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

// Domain/ValueObjects/ProductDescription.cs
public class ProductDescription : ValueObject
{
    public string Value { get; }

    public const int MaxLength = 500;

    protected ProductDescription() { } // For EF Core

    public ProductDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Product description cannot be empty");

        if (value.Length > MaxLength)
            throw new DomainException($"Product description cannot exceed {MaxLength} characters");

        Value = value.Trim();
    }

    public static implicit operator string(ProductDescription description) => description.Value;
    public static implicit operator ProductDescription(string value) => new ProductDescription(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

// Domain/ValueObjects/ProductCategory.cs
public class ProductCategory : ValueObject
{
    public string Value { get; }

    public static readonly ProductCategory Coffee = new ProductCategory("Coffee");
    public static readonly ProductCategory Tea = new ProductCategory("Tea");
    public static readonly ProductCategory Food = new ProductCategory("Food");
    public static readonly ProductCategory Beverage = new ProductCategory("Beverage");
    public static readonly ProductCategory Dessert = new ProductCategory("Dessert");

    protected ProductCategory() { } // For EF Core

    public ProductCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Product category cannot be empty");

        var validCategories = new[] { "Coffee", "Tea", "Food", "Beverage", "Dessert" };
        if (!validCategories.Contains(value))
            throw new DomainException($"Invalid product category: {value}");

        Value = value;
    }

    public static implicit operator string(ProductCategory category) => category.Value;
    public static implicit operator ProductCategory(string value) => new ProductCategory(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

// Domain/ValueObjects/Quantity.cs
public class Quantity : ValueObject
{
    public decimal Value { get; }
    public string Unit { get; }

    protected Quantity() { } // For EF Core

    public Quantity(decimal value, string unit)
    {
        if (value < 0)
            throw new DomainException("Quantity cannot be negative");

        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("Unit cannot be empty");

        Value = value;
        Unit = unit;
    }

    public static Quantity operator +(Quantity left, Quantity right)
    {
        if (left.Unit != right.Unit)
            throw new DomainException("Cannot add quantities with different units");

        return new Quantity(left.Value + right.Value, left.Unit);
    }

    public static Quantity operator -(Quantity left, Quantity right)
    {
        if (left.Unit != right.Unit)
            throw new DomainException("Cannot subtract quantities with different units");

        return new Quantity(left.Value - right.Value, left.Unit);
    }

    public static Quantity operator *(Quantity quantity, decimal multiplier)
    {
        return new Quantity(quantity.Value * multiplier, quantity.Unit);
    }

    public static Quantity operator /(Quantity quantity, decimal divisor)
    {
        if (divisor == 0)
            throw new DomainException("Cannot divide by zero");

        return new Quantity(quantity.Value / divisor, quantity.Unit);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }

    public override string ToString() => $"{Value} {Unit}";
}
```

#### **Day 4-5: Value Objects Implementation**
```csharp
// Domain/ValueObjects/Money.cs - Enhanced with currency conversion
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public static readonly Money Zero = new Money(0, "VND");

    protected Money() { } // For EF Core

    public Money(decimal amount, string currency = "VND")
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative");

        if (string.IsNullOrEmpty(currency))
            throw new DomainException("Currency is required");

        if (!IsValidCurrency(currency))
            throw new DomainException($"Invalid currency: {currency}");

        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot add money with different currencies");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot subtract money with different currencies");

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(Money money, int multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DomainException("Cannot divide by zero");

        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public static bool operator ==(Money left, Money right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Amount == right.Amount && left.Currency == right.Currency;
    }

    public static bool operator !=(Money left, Money right)
    {
        return !(left == right);
    }

    public Money ConvertTo(string targetCurrency, decimal exchangeRate)
    {
        if (!IsValidCurrency(targetCurrency))
            throw new DomainException($"Invalid target currency: {targetCurrency}");

        if (exchangeRate <= 0)
            throw new DomainException("Exchange rate must be greater than 0");

        return new Money(Amount * exchangeRate, targetCurrency);
    }

    public Money ApplyDiscount(decimal discountPercentage)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
            throw new DomainException("Discount percentage must be between 0 and 100");

        var discountAmount = Amount * (discountPercentage / 100);
        return new Money(Amount - discountAmount, Currency);
    }

    public Money ApplyTax(decimal taxPercentage)
    {
        if (taxPercentage < 0)
            throw new DomainException("Tax percentage cannot be negative");

        var taxAmount = Amount * (taxPercentage / 100);
        return new Money(Amount + taxAmount, Currency);
    }

    public override string ToString()
    {
        return $"{Amount:N2} {Currency}";
    }

    public override bool Equals(object obj)
    {
        if (obj is Money other)
        {
            return Amount == other.Amount && Currency == other.Currency;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Amount, Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot compare money with different currencies");
    }

    private static bool IsValidCurrency(string currency)
    {
        var validCurrencies = new[] { "VND", "USD", "EUR", "GBP", "JPY" };
        return validCurrencies.Contains(currency.ToUpperInvariant());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

// Domain/ValueObjects/Address.cs
public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    protected Address() { } // For EF Core

    public Address(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Street is required");

        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City is required");

        if (string.IsNullOrWhiteSpace(state))
            throw new DomainException("State is required");

        if (string.IsNullOrWhiteSpace(postalCode))
            throw new DomainException("Postal code is required");

        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("Country is required");

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    public override string ToString()
    {
        return $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}

// Domain/ValueObjects/Email.cs
public class Email : ValueObject
{
    public string Value { get; }

    protected Email() { } // For EF Core

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email is required");

        if (!IsValidEmail(value))
            throw new DomainException($"Invalid email format: {value}");

        Value = value.ToLowerInvariant().Trim();
    }

    public static implicit operator string(Email email) => email.Value;
    public static implicit operator Email(string value) => new Email(value);

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

// Domain/ValueObjects/PhoneNumber.cs
public class PhoneNumber : ValueObject
{
    public string Value { get; }
    public string CountryCode { get; }

    protected PhoneNumber() { } // For EF Core

    public PhoneNumber(string value, string countryCode = "+84")
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Phone number is required");

        if (string.IsNullOrWhiteSpace(countryCode))
            throw new DomainException("Country code is required");

        if (!IsValidPhoneNumber(value, countryCode))
            throw new DomainException($"Invalid phone number format: {value}");

        Value = NormalizePhoneNumber(value);
        CountryCode = countryCode;
    }

    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
    public static implicit operator PhoneNumber(string value) => new PhoneNumber(value);

    private static bool IsValidPhoneNumber(string phone, string countryCode)
    {
        // Basic validation - would need more sophisticated validation in production
        return phone.Length >= 10 && phone.All(char.IsDigit);
    }

    private static string NormalizePhoneNumber(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return CountryCode;
    }

    public override string ToString() => $"{CountryCode} {Value}";
}
```

#### **Day 6-7: Aggregate Implementation**
```csharp
// Domain/Aggregates/Customer.cs - Customer Aggregate Root
public class Customer : AggregateRoot, IAggregateRoot
{
    private readonly List<Order> _orders = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public CustomerName Name { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public Address Address { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public LoyaltyPoints LoyaltyPoints { get; private set; }
    
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Customer() { } // For EF Core

    public Customer(CustomerName name, Email email, PhoneNumber phoneNumber, Address address)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        Status = CustomerStatus.Active;
        LoyaltyPoints = LoyaltyPoints.Zero;
        CreatedAt = DateTime.UtcNow;
        
        ValidateCustomer();
        
        AddDomainEvent(new CustomerCreatedEvent(Id, Name, Email));
    }

    public void UpdateInformation(CustomerName name, PhoneNumber phoneNumber, Address address)
    {
        Name = name;
        PhoneNumber = phoneNumber;
        Address = address;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new CustomerUpdatedEvent(Id, Name, PhoneNumber, Address));
    }

    public void UpdateEmail(Email newEmail)
    {
        Email = newEmail;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new CustomerEmailUpdatedEvent(Id, Email));
    }

    public void AddLoyaltyPoints(int points)
    {
        if (points <= 0)
            throw new DomainException("Loyalty points must be greater than 0");

        LoyaltyPoints = LoyaltyPoints.Add(points);
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new LoyaltyPointsAddedEvent(Id, points));
    }

    public void RedeemLoyaltyPoints(int points)
    {
        if (points <= 0)
            throw new DomainException("Loyalty points must be greater than 0");

        if (LoyaltyPoints.Value < points)
            throw new DomainException("Insufficient loyalty points");

        LoyaltyPoints = LoyaltyPoints.Subtract(points);
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new LoyaltyPointsRedeemedEvent(Id, points));
    }

    public void PlaceOrder(Order order)
    {
        if (Status != CustomerStatus.Active)
            throw new DomainException("Cannot place order for inactive customer");

        _orders.Add(order);
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new OrderPlacedEvent(Id, order.Id));
    }

    public void Activate()
    {
        if (Status == CustomerStatus.Active)
            return;

        Status = CustomerStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new CustomerActivatedEvent(Id));
    }

    public void Deactivate(string reason)
    {
        if (Status == CustomerStatus.Inactive)
            return;

        Status = CustomerStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new CustomerDeactivatedEvent(Id, reason));
    }

    public bool CanPlaceOrder()
    {
        return Status == CustomerStatus.Active;
    }

    public Money GetTotalOrderValue()
    {
        return _orders.Where(o => o.Status == OrderStatus.Completed)
                      .Sum(o => o.TotalAmount);
    }

    public int GetOrderCount()
    {
        return _orders.Count;
    }

    public bool IsVipCustomer()
    {
        return LoyaltyPoints.Value >= 1000 || GetTotalOrderValue() >= 1000000;
    }

    private void ValidateCustomer()
    {
        if (Name == null)
            throw new DomainException("Customer name is required");

        if (Email == null)
            throw new DomainException("Customer email is required");

        if (PhoneNumber == null)
            throw new DomainException("Customer phone number is required");

        if (Address == null)
            throw new DomainException("Customer address is required");
    }

    protected override void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Domain/ValueObjects/LoyaltyPoints.cs
public class LoyaltyPoints : ValueObject
{
    public int Value { get; }

    public static readonly LoyaltyPoints Zero = new LoyaltyPoints(0);

    protected LoyaltyPoints() { } // For EF Core

    public LoyaltyPoints(int value)
    {
        if (value < 0)
            throw new DomainException("Loyalty points cannot be negative");

        Value = value;
    }

    public static LoyaltyPoints operator +(LoyaltyPoints left, LoyaltyPoints right)
    {
        return new LoyaltyPoints(left.Value + right.Value);
    }

    public static LoyaltyPoints operator -(LoyaltyPoints left, LoyaltyPoints right)
    {
        return new LoyaltyPoints(left.Value - right.Value);
    }

    public static bool operator >(LoyaltyPoints left, LoyaltyPoints right)
    {
        return left.Value > right.Value;
    }

    public static bool operator <(LoyaltyPoints left, LoyaltyPoints right)
    {
        return left.Value < right.Value;
    }

    public static bool operator >=(LoyaltyPoints left, LoyaltyPoints right)
    {
        return left.Value >= right.Value;
    }

    public static bool operator <=(LoyaltyPoints left, LoyaltyPoints right)
    {
        return left.Value <= right.Value;
    }

    public LoyaltyPoints Add(int points)
    {
        return new LoyaltyPoints(Value + points);
    }

    public LoyaltyPoints Subtract(int points)
    {
        return new LoyaltyPoints(Value - points);
    }

    public bool CanRedeem(int points)
    {
        return Value >= points;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}

// Domain/ValueObjects/CustomerName.cs
public class CustomerName : ValueObject
{
    public string FirstName { get; }
    public string LastName { get; }

    public string FullName => $"{FirstName} {LastName}";

    protected CustomerName() { } // For EF Core

    public CustomerName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required");

        if (firstName.Length > 50)
            throw new DomainException("First name cannot exceed 50 characters");

        if (lastName.Length > 50)
            throw new DomainException("Last name cannot exceed 50 characters");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
    }

    public override string ToString() => FullName;
}
```

### **3.2 Phase 2: Advanced Validation (Week 3-4)**

#### **Day 8-10: FluentValidation Implementation**
```csharp
// Application/Validation/Validators/CreateProductCommandValidator.cs
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100)
            .WithMessage("Product name must be between 2 and 100 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Product description cannot exceed 500 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0");

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(BeValidCategory)
            .WithMessage("Invalid product category");

        RuleFor(x => x.Ingredients)
            .NotEmpty()
            .WithMessage("Product must have at least one ingredient");

        RuleForEach(x => x.Ingredients)
            .ChildRules(ingredient =>
            {
                ingredient.RuleFor(i => i.IngredientId)
                    .NotEmpty()
                    .WithMessage("Ingredient ID is required");

                ingredient.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Ingredient quantity must be greater than 0");
            });
    }

    private bool BeValidCategory(string category)
    {
        var validCategories = new[] { "Coffee", "Tea", "Food", "Beverage", "Dessert" };
        return validCategories.Contains(category);
    }
}

// Application/Validation/Validators/CreateCustomerCommandValidator.cs
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(50)
            .WithMessage("First name must be between 2 and 50 characters");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(50)
            .WithMessage("Last name must be between 2 and 50 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Valid email address is required");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Valid phone number is required");

        RuleFor(x => x.Street)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Street address is required and cannot exceed 200 characters");

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("City is required and cannot exceed 100 characters");

        RuleFor(x => x.State)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("State is required and cannot exceed 100 characters");

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .Matches(@"^[0-9]{5,10}$")
            .WithMessage("Valid postal code is required");

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Country is required and cannot exceed 100 characters");
    }
}

// Application/Validation/Validators/UpdateOrderCommandValidator.cs
public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .NotEmpty()
                    .WithMessage("Product ID is required");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than 0");

                item.RuleFor(i => i.UnitPrice)
                    .GreaterThan(0)
                    .WithMessage("Unit price must be greater than 0");
            });

        RuleFor(x => x)
            .Must(HaveValidTotalAmount)
            .WithMessage("Order total amount must match sum of item totals");
    }

    private bool HaveValidTotalAmount(UpdateOrderCommand command)
    {
        var calculatedTotal = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        return Math.Abs(calculatedTotal - command.TotalAmount) < 0.01m;
    }
}

// Application/Validation/Behaviors/ValidationBehavior.cs
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators, ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Any())
            {
                var errorMessage = string.Join(Environment.NewLine, failures.Select(f => f.ErrorMessage));
                _logger.LogWarning("Validation failed for {RequestType}: {Errors}", typeof(TRequest).Name, errorMessage);
                
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
```

#### **Day 11-12: Business Rules Validation**
```csharp
// Domain/Rules/ProductBusinessRules.cs
public static class ProductBusinessRules
{
    public static IBusinessRule ProductNameMustBeUnique(string name, IProductRepository repository)
    {
        return new ProductNameMustBeUniqueRule(name, repository);
    }

    public static IBusinessRule ProductPriceMustBeGreaterThanCost(Money price, Money cost)
    {
        return new ProductPriceMustBeGreaterThanCostRule(price, cost);
    }

    public static IBusinessRule ProductMustHaveValidIngredients(List<ProductIngredient> ingredients, IIngredientRepository repository)
    {
        return new ProductMustHaveValidIngredientsRule(ingredients, repository);
    }

    public static IBusinessRule ProductCannotBeDiscontinuedIfHasActiveOrders(Guid productId, IOrderRepository repository)
    {
        return new ProductCannotBeDiscontinuedIfHasActiveOrdersRule(productId, repository);
    }
}

// Domain/Rules/ProductNameMustBeUniqueRule.cs
public class ProductNameMustBeUniqueRule : IBusinessRule
{
    private readonly string _name;
    private readonly IProductRepository _repository;

    public ProductNameMustBeUniqueRule(string name, IProductRepository repository)
    {
        _name = name;
        _repository = repository;
    }

    public bool IsBroken()
    {
        var existingProduct = _repository.GetByNameAsync(_name).Result;
        return existingProduct != null;
    }

    public string Message => $"Product name '{_name}' is already in use";
}

// Domain/Rules/ProductPriceMustBeGreaterThanCostRule.cs
public class ProductPriceMustBeGreaterThanCostRule : IBusinessRule
{
    private readonly Money _price;
    private readonly Money _cost;

    public ProductPriceMustBeGreaterThanCostRule(Money price, Money cost)
    {
        _price = price;
        _cost = cost;
    }

    public bool IsBroken()
    {
        return _price <= _cost;
    }

    public string Message => "Product price must be greater than cost";
}

// Domain/Rules/ProductMustHaveValidIngredientsRule.cs
public class ProductMustHaveValidIngredientsRule : IBusinessRule
{
    private readonly List<ProductIngredient> _ingredients;
    private readonly IIngredientRepository _repository;

    public ProductMustHaveValidIngredientsRule(List<ProductIngredient> ingredients, IIngredientRepository repository)
    {
        _ingredients = ingredients;
        _repository = repository;
    }

    public bool IsBroken()
    {
        if (!_ingredients.Any())
            return true;

        var ingredientIds = _ingredients.Select(i => i.IngredientId).ToList();
        var existingIngredients = _repository.GetByIdsAsync(ingredientIds).Result;
        
        return existingIngredients.Count != ingredientIds.Count;
    }

    public string Message => "One or more ingredients are not valid";
}

// Domain/Rules/ProductCannotBeDiscontinuedIfHasActiveOrdersRule.cs
public class ProductCannotBeDiscontinuedIfHasActiveOrdersRule : IBusinessRule
{
    private readonly Guid _productId;
    private readonly IOrderRepository _repository;

    public ProductCannotBeDiscontinuedIfHasActiveOrdersRule(Guid productId, IOrderRepository repository)
    {
        _productId = productId;
        _repository = repository;
    }

    public bool IsBroken()
    {
        var activeOrders = _repository.GetActiveOrdersByProductIdAsync(_productId).Result;
        return activeOrders.Any();
    }

    public string Message => "Product cannot be discontinued while there are active orders";
}

// Application/Services/ProductDomainService.cs
public class ProductDomainService
{
    private readonly IProductRepository _productRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly IIngredientPricingService _pricingService;

    public ProductDomainService(
        IProductRepository productRepository,
        IIngredientRepository ingredientRepository,
        IIngredientPricingService pricingService)
    {
        _productRepository = productRepository;
        _ingredientRepository = ingredientRepository;
        _pricingService = pricingService;
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request)
    {
        // Validate business rules
        var rules = new List<IBusinessRule>
        {
            ProductBusinessRules.ProductNameMustBeUnique(request.Name, _productRepository)
        };

        // Add ingredient validation if ingredients are provided
        if (request.Ingredients?.Any() == true)
        {
            var ingredientIds = request.Ingredients.Select(i => i.IngredientId).ToList();
            var ingredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
            
            if (ingredients.Count != ingredientIds.Count)
            {
                throw new DomainException("One or more ingredients are not valid");
            }

            // Calculate cost and validate price
            var totalCost = Money.Zero;
            foreach (var ingredient in request.Ingredients)
            {
                var ingredientPrice = await _pricingService.GetPriceAsync(ingredient.IngredientId);
                totalCost += ingredientPrice * ingredient.Quantity;
            }

            rules.Add(ProductBusinessRules.ProductPriceMustBeGreaterThanCost(request.Price, totalCost));
        }

        BusinessRuleValidator.Validate(rules.ToArray());

        // Create product
        var product = new Product(request.Name, request.Description, request.Price, request.Category);

        // Add ingredients if provided
        if (request.Ingredients?.Any() == true)
        {
            foreach (var ingredient in request.Ingredients)
            {
                product.AddIngredient(ingredient.IngredientId, ingredient.Quantity);
            }
        }

        return product;
    }

    public async Task<bool> CanDiscontinueProductAsync(Guid productId)
    {
        var rule = ProductBusinessRules.ProductCannotBeDiscontinuedIfHasActiveOrders(productId, _productRepository);
        return !rule.IsBroken();
    }

    public async Task<Money> CalculateProductCostAsync(Guid productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            throw new DomainException("Product not found");

        return await product.CalculateCost(_pricingService);
    }

    public async Task<decimal> CalculateProfitMarginAsync(Guid productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            throw new DomainException("Product not found");

        return await product.CalculateProfitMargin(_pricingService);
    }
}
```

### **3.3 Phase 3: Performance Optimization (Week 5-6)**

#### **Day 13-15: Caching Strategy**
```csharp
// Infrastructure/Caching/ICacheService.cs
public interface ICacheService
{
    Task<T> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
}

// Infrastructure/Caching/RedisCacheService.cs
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value) : default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, serialized, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key {Key}", key);
            return false;
        }
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        var cachedValue = await GetAsync<T>(key);
        if (cachedValue != null)
        {
            return cachedValue;
        }

        var value = await factory();
        await SetAsync(key, value, expiry);
        return value;
    }
}

// Infrastructure/Caching/CachedProductRepository.cs
public class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedProductRepository> _logger;

    public CachedProductRepository(
        IProductRepository repository,
        ICacheService cacheService,
        ILogger<CachedProductRepository> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Product> GetByIdAsync(Guid id)
    {
        var cacheKey = $"product:{id}";
        return await _cacheService.GetOrCreateAsync(cacheKey, () => _repository.GetByIdAsync(id), TimeSpan.FromMinutes(10));
    }

    public async Task<Product> GetByNameAsync(string name)
    {
        var cacheKey = $"product:name:{name}";
        return await _cacheService.GetOrCreateAsync(cacheKey, () => _repository.GetByNameAsync(name), TimeSpan.FromMinutes(10));
    }

    public async Task<List<Product>> GetByIdsAsync(List<Guid> ids)
    {
        var cacheKey = $"products:batch:{string.Join(",", ids)}";
        return await _cacheService.GetOrCreateAsync(cacheKey, () => _repository.GetByIdsAsync(ids), TimeSpan.FromMinutes(5));
    }

    public async Task<List<Product>> GetByCategoryAsync(string category, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"products:category:{category}:{page}:{pageSize}";
        return await _cacheService.GetOrCreateAsync(cacheKey, () => _repository.GetByCategoryAsync(category, page, pageSize), TimeSpan.FromMinutes(5));
    }

    public async Task AddAsync(Product product)
    {
        await _repository.AddAsync(product);
        
        // Invalidate relevant caches
        await InvalidateProductCaches(product);
    }

    public async Task UpdateAsync(Product product)
    {
        await _repository.UpdateAsync(product);
        
        // Invalidate relevant caches
        await InvalidateProductCaches(product);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        
        // Invalidate relevant caches
        await _cacheService.RemoveAsync($"product:{id}");
        await _cacheService.RemoveAsync($"products:*");
    }

    private async Task InvalidateProductCaches(Product product)
    {
        var tasks = new List<Task>
        {
            _cacheService.RemoveAsync($"product:{product.Id}"),
            _cacheService.RemoveAsync($"product:name:{product.Name}"),
            _cacheService.RemoveAsync($"products:category:{product.Category}:*"),
            _cacheService.RemoveAsync($"products:batch:*")
        };

        await Task.WhenAll(tasks);
    }
}
```

#### **Day 16-17: Query Optimization**
```csharp
// Infrastructure/Repositories/Specifications/ISpecification.cs
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    List<string> IncludeStrings { get; }
    Expression<Func<T, object>> OrderBy { get; }
    Expression<Func<T, object>> OrderByDescending { get; }
    int Take { get; }
    int Skip { get; }
    bool IsPagingEnabled { get; }
}

// Infrastructure/Repositories/Specifications/BaseSpecification.cs
public abstract class BaseSpecification<T> : ISpecification<T>
{
    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    public Expression<Func<T, bool>> Criteria { get; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public List<string> IncludeStrings { get; } = new();
    public Expression<Func<T, object>> OrderBy { get; private set; }
    public Expression<Func<T, object>> OrderByDescending { get; private set; }
    public int Take { get; private set; }
    public int Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    protected void AddInclude(string includeString)
    {
        IncludeStrings.Add(includeString);
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    {
        OrderByDescending = orderByDescendingExpression;
    }

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }
}

// Infrastructure/Repositories/Specifications/ProductSpecifications.cs
public class ProductByIdSpecification : BaseSpecification<Product>
{
    public ProductByIdSpecification(Guid id) : base(p => p.Id == id)
    {
        AddInclude(p => p.Ingredients);
    }
}

public class ProductByNameSpecification : BaseSpecification<Product>
{
    public ProductByNameSpecification(string name) : base(p => p.Name == name)
    {
        AddInclude(p => p.Ingredients);
    }
}

public class ProductByCategorySpecification : BaseSpecification<Product>
{
    public ProductByCategorySpecification(string category, int page, int pageSize) 
        : base(p => p.Category == category && p.Status == ProductStatus.Active)
    {
        AddInclude(p => p.Ingredients);
        ApplyOrderBy(p => p.Name);
        ApplyPaging((page - 1) * pageSize, pageSize);
    }
}

public class ActiveProductSpecification : BaseSpecification<Product>
{
    public ActiveProductSpecification() : base(p => p.Status == ProductStatus.Active)
    {
        AddInclude(p => p.Ingredients);
        ApplyOrderBy(p => p.Name);
    }
}

public class ProductSearchSpecification : BaseSpecification<Product>
{
    public ProductSearchSpecification(string searchTerm, int page, int pageSize) 
        : base(p => (p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm)) && p.Status == ProductStatus.Active)
    {
        AddInclude(p => p.Ingredients);
        ApplyOrderBy(p => p.Name);
        ApplyPaging((page - 1) * pageSize, pageSize);
    }
}

// Infrastructure/Repositories/SpecificationEvaluator.cs
public static class SpecificationEvaluator<T>
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> specification)
    {
        var query = inputQuery;

        // Modify the IQueryable
        if (specification.Criteria != null)
        {
            query = query.Where(specification.Criteria);
        }

        // Includes all expression-based includes
        query = specification.Includes.Aggregate(query,
            (current, include) => current.Include(include));

        // Include all string-based includes
        query = specification.IncludeStrings.Aggregate(query,
            (current, include) => current.Include(include));

        // Apply ordering if expressions are set
        if (specification.OrderBy != null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending != null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        // Apply paging if enabled
        if (specification.IsPagingEnabled)
        {
            query = query.Skip(specification.Skip).Take(specification.Take);
        }

        return query;
    }
}

// Infrastructure/Repositories/OptimizedProductRepository.cs
public class OptimizedProductRepository : IProductRepository
{
    private readonly VanAnDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OptimizedProductRepository> _logger;

    public OptimizedProductRepository(
        VanAnDbContext context,
        ICacheService cacheService,
        ILogger<OptimizedProductRepository> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Product> GetByIdAsync(Guid id)
    {
        var specification = new ProductByIdSpecification(id);
        return await GetBySpecificationAsync(specification);
    }

    public async Task<Product> GetByNameAsync(string name)
    {
        var specification = new ProductByNameSpecification(name);
        return await GetBySpecificationAsync(specification);
    }

    public async Task<List<Product>> GetByIdsAsync(List<Guid> ids)
    {
        var cacheKey = $"products:batch:{string.Join(",", ids)}";
        return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
        {
            var products = await _context.Products
                .Where(p => ids.Contains(p.Id))
                .Include(p => p.Ingredients)
                .AsNoTracking()
                .ToListAsync();

            return products;
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<List<Product>> GetByCategoryAsync(string category, int page = 1, int pageSize = 20)
    {
        var specification = new ProductByCategorySpecification(category, page, pageSize);
        return await GetListBySpecificationAsync(specification);
    }

    public async Task<List<Product>> GetActiveProductsAsync()
    {
        var specification = new ActiveProductSpecification();
        return await GetListBySpecificationAsync(specification);
    }

    public async Task<List<Product>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 20)
    {
        var specification = new ProductSearchSpecification(searchTerm, page, pageSize);
        return await GetListBySpecificationAsync(specification);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();
        
        // Invalidate relevant caches
        await InvalidateProductCaches(product);
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        
        // Invalidate relevant caches
        await InvalidateProductCaches(product);
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await GetByIdAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            
            // Invalidate relevant caches
            await _cacheService.RemoveAsync($"product:{id}");
            await _cacheService.RemoveAsync($"products:*");
        }
    }

    private async Task<Product> GetBySpecificationAsync(ISpecification<Product> specification)
    {
        var query = SpecificationEvaluator.GetQuery(_context.Products, specification);
        return await query.AsNoTracking().FirstOrDefaultAsync();
    }

    private async Task<List<Product>> GetListBySpecificationAsync(ISpecification<Product> specification)
    {
        var query = SpecificationEvaluator.GetQuery(_context.Products, specification);
        return await query.AsNoTracking().ToListAsync();
    }

    private async Task InvalidateProductCaches(Product product)
    {
        var tasks = new List<Task>
        {
            _cacheService.RemoveAsync($"product:{product.Id}"),
            _cacheService.RemoveAsync($"product:name:{product.Name}"),
            _cacheService.RemoveAsync($"products:category:{product.Category}:*"),
            _cacheService.RemoveAsync($"products:batch:*"),
            _cacheService.RemoveAsync($"products:active:*"),
            _cacheService.RemoveAsync($"products:search:*")
        };

        await Task.WhenAll(tasks);
    }
}
```

### **3.4 Phase 4: Testing & Documentation (Week 7-8)**

#### **Day 18-20: Comprehensive Testing**
```csharp
// Tests/Unit/Domain/ProductTests.cs
public class ProductTests
{
    [Fact]
    public void CreateProduct_Should_Set_Initial_Status_To_Active()
    {
        // Arrange
        var name = new ProductName("Test Coffee");
        var description = new ProductDescription("Test Description");
        var price = new Money(25000);
        var category = ProductCategory.Coffee;

        // Act
        var product = new Product(name, description, price, category);

        // Assert
        product.Status.Should().Be(ProductStatus.Active);
        product.Name.Should().Be(name);
        product.Description.Should().Be(description);
        product.Price.Should().Be(price);
        product.Category.Should().Be(category);
    }

    [Fact]
    public void UpdatePrice_Should_Change_Price_And_Add_Event()
    {
        // Arrange
        var product = CreateTestProduct();
        var newPrice = new Money(30000);
        var updatedBy = Guid.NewGuid();

        // Act
        product.UpdatePrice(newPrice, updatedBy);

        // Assert
        product.Price.Should().Be(newPrice);
        product.UpdatedAt.Should().NotBeNull();
        product.UpdatedBy.Should().Be(updatedBy);
        product.DomainEvents.Should().Contain(e => e is ProductPriceChangedEvent);
    }

    [Fact]
    public void AddIngredient_Should_Add_To_Ingredients_And_Add_Event()
    {
        // Arrange
        var product = CreateTestProduct();
        var ingredientId = new IngredientId(Guid.NewGuid());
        var quantity = new Quantity(100, "g");

        // Act
        product.AddIngredient(ingredientId, quantity);

        // Assert
        product.Ingredients.Should().HaveCount(1);
        product.Ingredients.First().IngredientId.Should().Be(ingredientId);
        product.Ingredients.First().Quantity.Should().Be(quantity);
        product.DomainEvents.Should().Contain(e => e is ProductIngredientAddedEvent);
    }

    [Fact]
    public void Deactivate_Should_Change_Status_To_Inactive_And_Add_Event()
    {
        // Arrange
        var product = CreateTestProduct();
        var reason = "Seasonal discontinuation";

        // Act
        product.Deactivate(reason);

        // Assert
        product.Status.Should().Be(ProductStatus.Inactive);
        product.DomainEvents.Should().Contain(e => e is ProductDeactivatedEvent);
    }

    [Fact]
    public void CanBeOrdered_Should_Return_True_For_Active_Product_With_Ingredients()
    {
        // Arrange
        var product = CreateTestProduct();
        product.AddIngredient(new IngredientId(Guid.NewGuid()), new Quantity(100, "g"));

        // Act & Assert
        product.CanBeOrdered().Should().BeTrue();
    }

    [Fact]
    public void CanBeOrdered_Should_Return_False_For_Inactive_Product()
    {
        // Arrange
        var product = CreateTestProduct();
        product.Deactivate("Test");

        // Act & Assert
        product.CanBeOrdered().Should().BeFalse();
    }

    [Fact]
    public void CreateProduct_With_Invalid_Price_Should_Throw_Exception()
    {
        // Arrange
        var name = new ProductName("Test Coffee");
        var description = new ProductDescription("Test Description");
        var price = new Money(-1); // Invalid price
        var category = ProductCategory.Coffee;

        // Act & Assert
        Assert.Throws<DomainException>(() => new Product(name, description, price, category));
    }

    private Product CreateTestProduct()
    {
        return new Product(
            new ProductName("Test Coffee"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );
    }
}

// Tests/Unit/ValueObjects/MoneyTests.cs
public class MoneyTests
{
    [Theory]
    [InlineData(100, "VND")]
    [InlineData(0, "USD")]
    [InlineData(999.99, "EUR")]
    public void CreateMoney_With_Valid_Values_Should_Succeed(decimal amount, string currency)
    {
        // Act
        var money = new Money(amount, currency);

        // Assert
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency.ToUpperInvariant());
    }

    [Theory]
    [InlineData(-1, "VND")]
    [InlineData(100, "")]
    [InlineData(100, "INVALID")]
    public void CreateMoney_With_Invalid_Values_Should_Throw_Exception(decimal amount, string currency)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => new Money(amount, currency));
    }

    [Fact]
    public void Add_Money_With_Same_Currency_Should_Return_Correct_Result()
    {
        // Arrange
        var money1 = new Money(100, "VND");
        var money2 = new Money(50, "VND");

        // Act
        var result = money1 + money2;

        // Assert
        result.Amount.Should().Be(150);
        result.Currency.Should().Be("VND");
    }

    [Fact]
    public void Add_Money_With_Different_Currency_Should_Throw_Exception()
    {
        // Arrange
        var money1 = new Money(100, "VND");
        var money2 = new Money(50, "USD");

        // Act & Assert
        Assert.Throws<DomainException>(() => money1 + money2);
    }

    [Fact]
    public void ApplyDiscount_Should_Return_Correct_Amount()
    {
        // Arrange
        var money = new Money(100, "VND");
        var discountPercentage = 10;

        // Act
        var result = money.ApplyDiscount(discountPercentage);

        // Assert
        result.Amount.Should().Be(90);
        result.Currency.Should().Be("VND");
    }

    [Fact]
    public void ApplyTax_Should_Return_Correct_Amount()
    {
        // Arrange
        var money = new Money(100, "VND");
        var taxPercentage = 10;

        // Act
        var result = money.ApplyTax(taxPercentage);

        // Assert
        result.Amount.Should().Be(110);
        result.Currency.Should().Be("VND");
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 99, false)]
    [InlineData(100, 101, false)]
    public void Comparison_Operators_Should_Work_Correctly(decimal amount1, decimal amount2, bool expectedEqual)
    {
        // Arrange
        var money1 = new Money(amount1, "VND");
        var money2 = new Money(amount2, "VND");

        // Act & Assert
        (money1 == money2).Should().Be(expectedEqual);
        (money1 != money2).Should().Be(!expectedEqual);
        (money1 > money2).Should().Be(amount1 > amount2);
        (money1 < money2).Should().Be(amount1 < amount2);
    }
}

// Tests/Integration/ProductRepositoryTests.cs
public class ProductRepositoryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly IProductRepository _repository;
    private readonly VanAnDbContext _context;

    public ProductRepositoryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _repository = _factory.Services.GetRequiredService<IProductRepository>();
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Fact]
    public async Task AddProduct_Should_Persist_To_Database()
    {
        // Arrange
        var product = new Product(
            new ProductName("Test Coffee"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );

        // Act
        await _repository.AddAsync(product);

        // Assert
        var savedProduct = await _context.Products.FindAsync(product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct.Name.Should().Be(product.Name);
        savedProduct.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task GetProductById_Should_Return_Cached_Result()
    {
        // Arrange
        var product = new Product(
            new ProductName("Test Coffee"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );
        await _repository.AddAsync(product);

        // Act
        var result1 = await _repository.GetByIdAsync(product.Id);
        var result2 = await _repository.GetByIdAsync(product.Id);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Id.Should().Be(result2.Id);
    }

    [Fact]
    public async Task GetProductsByCategory_Should_Return_Filtered_Results()
    {
        // Arrange
        var coffee = new Product("Coffee 1", "Description 1", 25000, ProductCategory.Coffee);
        var tea = new Product("Tea 1", "Description 1", 20000, ProductCategory.Tea);
        
        await _repository.AddAsync(coffee);
        await _repository.AddAsync(tea);

        // Act
        var coffeeProducts = await _repository.GetByCategoryAsync("Coffee");
        var teaProducts = await _repository.GetByCategoryAsync("Tea");

        // Assert
        coffeeProducts.Should().HaveCount(1);
        coffeeProducts.First().Category.Should().Be(ProductCategory.Coffee);
        
        teaProducts.Should().HaveCount(1);
        teaProducts.First().Category.Should().Be(ProductCategory.Tea);
    }

    [Fact]
    public async Task SearchProducts_Should_Return_Matching_Results()
    {
        // Arrange
        var coffee = new Product("Vietnamese Coffee", "Strong coffee", 25000, ProductCategory.Coffee);
        var tea = new Product("Green Tea", "Refreshing tea", 20000, ProductCategory.Tea);
        
        await _repository.AddAsync(coffee);
        await _repository.AddAsync(tea);

        // Act
        var results = await _repository.SearchProductsAsync("Coffee");

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Contain("Coffee");
    }
}
```

#### **Day 21-22: Documentation Generation**
```csharp
// Infrastructure/Documentation/DomainDocumentationGenerator.cs
public class DomainDocumentationGenerator
{
    private readonly IAssemblyProvider _assemblyProvider;
    private readonly ILogger<DomainDocumentationGenerator> _logger;

    public DomainDocumentationGenerator(IAssemblyProvider assemblyProvider, ILogger<DomainDocumentationGenerator> logger)
    {
        _assemblyProvider = assemblyProvider;
        _logger = logger;
    }

    public async Task<string> GenerateDomainDocumentationAsync()
    {
        var assembly = _assemblyProvider.GetAssembly();
        var domainTypes = GetDomainTypes(assembly);
        
        var documentation = new StringBuilder();
        documentation.AppendLine("# Domain Model Documentation");
        documentation.AppendLine($"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        documentation.AppendLine();

        foreach (var type in domainTypes)
        {
            await GenerateTypeDocumentationAsync(documentation, type);
            documentation.AppendLine();
        }

        return documentation.ToString();
    }

    private List<Type> GetDomainTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                       (typeof(IAggregateRoot).IsAssignableFrom(t) || 
                        typeof(IEntity).IsAssignableFrom(t) || 
                        typeof(IValueObject).IsAssignableFrom(t)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    private async Task GenerateTypeDocumentationAsync(StringBuilder documentation, Type type)
    {
        documentation.AppendLine($"## {type.Name}");
        documentation.AppendLine();

        // Add XML comments if available
        var xmlComments = await GetXmlCommentsAsync(type);
        if (!string.IsNullOrEmpty(xmlComments))
        {
            documentation.AppendLine(xmlComments);
            documentation.AppendLine();
        }

        // Add properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name);

        if (properties.Any())
        {
            documentation.AppendLine("### Properties");
            documentation.AppendLine();
            
            foreach (var property in properties)
            {
                documentation.AppendLine($"- **{property.Name}** ({property.PropertyType.Name})");
                
                var propertyComments = await GetXmlCommentsAsync(property);
                if (!string.IsNullOrEmpty(propertyComments))
                {
                    documentation.AppendLine($"  - {propertyComments}");
                }
                
                documentation.AppendLine();
            }
        }

        // Add methods
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == type)
            .OrderBy(m => m.Name);

        if (methods.Any())
        {
            documentation.AppendLine("### Methods");
            documentation.AppendLine();
            
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var parameterList = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                
                documentation.AppendLine($"- **{method.Name}**({parameterList}) -> {method.ReturnType.Name}");
                
                var methodComments = await GetXmlCommentsAsync(method);
                if (!string.IsNullOrEmpty(methodComments))
                {
                    documentation.AppendLine($"  - {methodComments}");
                }
                
                documentation.AppendLine();
            }
        }

        // Add example usage
        documentation.AppendLine("### Example Usage");
        documentation.AppendLine();
        documentation.AppendLine("```csharp");
        documentation.AppendLine("// Example usage would be here");
        documentation.AppendLine("```");
        documentation.AppendLine();
    }

    private async Task<string> GetXmlCommentsAsync(MemberInfo member)
    {
        // This would read XML documentation files
        // For now, return placeholder
        return await Task.FromResult("Documentation would be here");
    }
}

// Infrastructure/Documentation/ApiDocumentationGenerator.cs
public class ApiDocumentationGenerator
{
    private readonly IAssemblyProvider _assemblyProvider;
    private readonly ILogger<ApiDocumentationGenerator> _logger;

    public ApiDocumentationGenerator(IAssemblyProvider assemblyProvider, ILogger<ApiDocumentationGenerator> logger)
    {
        _assemblyProvider = assemblyProvider;
        _logger = logger;
    }

    public async Task<string> GenerateApiDocumentationAsync()
    {
        var assembly = _assemblyProvider.GetAssembly();
        var controllerTypes = GetControllerTypes(assembly);
        
        var documentation = new StringBuilder();
        documentation.AppendLine("# API Documentation");
        documentation.AppendLine($"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        documentation.AppendLine();

        foreach (var controllerType in controllerTypes)
        {
            await GenerateControllerDocumentationAsync(documentation, controllerType);
            documentation.AppendLine();
        }

        return documentation.ToString();
    }

    private List<Type> GetControllerTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                       typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();
    }

    private async Task GenerateControllerDocumentationAsync(StringBuilder documentation, Type controllerType)
    {
        documentation.AppendLine($"## {controllerType.Name.Replace("Controller", "")}");
        documentation.AppendLine();

        // Get route prefix
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        if (routeAttribute != null)
        {
            documentation.AppendLine($"**Route:** `{routeAttribute.Template}`");
            documentation.AppendLine();
        }

        // Get actions
        var actions = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType == typeof(IActionResult) || 
                        m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            .OrderBy(m => m.Name);

        foreach (var action in actions)
        {
            await GenerateActionDocumentationAsync(documentation, action);
            documentation.AppendLine();
        }
    }

    private async Task GenerateActionDocumentationAsync(StringBuilder documentation, MethodInfo action)
    {
        var httpMethod = GetHttpMethod(action);
        var route = GetActionRoute(action);
        var parameters = action.GetParameters();
        
        documentation.AppendLine($"### {httpMethod} {route}");
        documentation.AppendLine();

        // Add description
        documentation.AppendLine($"**Action:** {action.Name}");
        documentation.AppendLine();

        // Add parameters
        if (parameters.Any())
        {
            documentation.AppendLine("**Parameters:**");
            documentation.AppendLine();
            
            foreach (var param in parameters)
            {
                documentation.AppendLine($"- `{param.Name}` ({param.ParameterType.Name})");
                documentation.AppendLine();
            }
        }

        // Add response
        documentation.AppendLine("**Response:**");
        documentation.AppendLine();
        documentation.AppendLine("```json");
        documentation.AppendLine("{");
        documentation.AppendLine("  \"success\": true,");
        documentation.AppendLine("  \"data\": {");
        documentation.AppendLine("    // Response data would be here");
        documentation.AppendLine("  }");
        documentation.AppendLine("}");
        documentation.AppendLine("```");
        documentation.AppendLine();
    }

    private string GetHttpMethod(MethodInfo method)
    {
        if (method.GetCustomAttribute<HttpGetAttribute>() != null) return "GET";
        if (method.GetCustomAttribute<HttpPostAttribute>() != null) return "POST";
        if (method.GetCustomAttribute<HttpPutAttribute>() != null) return "PUT";
        if (method.GetCustomAttribute<HttpDeleteAttribute>() != null) return "DELETE";
        if (method.GetCustomAttribute<HttpPatchAttribute>() != null) return "PATCH";
        return "UNKNOWN";
    }

    private string GetActionRoute(MethodInfo method)
    {
        var routeAttribute = method.GetCustomAttribute<RouteAttribute>();
        if (routeAttribute != null)
        {
            return routeAttribute.Template;
        }

        // Default route based on action name
        return method.Name.ToLowerInvariant();
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Domain Model Enhancement**
- [ ] Implement rich domain models
- [ ] Add comprehensive value objects
- [ ] Create proper aggregates
- [ ] Add domain events
- [ ] Implement business rules
- [ ] Add domain services

### **4.2 Week 3-4: Advanced Validation**
- [ ] Implement FluentValidation
- [ ] Add validation behaviors
- [ ] Create business rules engine
- [ ] Add custom validators
- [ ] Implement validation pipeline
- [ ] Add error handling

### **4.3 Week 5-6: Performance Optimization**
- [ ] Implement caching strategy
- [ ] Add repository specifications
- [ ] Optimize database queries
- [ ] Add connection pooling
- [ ] Implement async patterns
- [ ] Add performance monitoring

### **4.4 Week 7-8: Testing & Documentation**
- [ ] Create comprehensive unit tests
- [ ] Add integration tests
- [ ] Implement performance tests
- [ ] Generate documentation
- [ ] Add API documentation
- [ ] Create developer guides

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >95% for domain models
- **Domain Logic:** 100% encapsulated in domain
- **Validation Coverage:** 100% business rules validated
- **Cache Hit Rate:** >85%

### **5.2 Business Metrics**
- **Domain Model Completeness:** 100% business concepts modeled
- **Business Rule Coverage:** 100% rules implemented
- **Value Object Usage:** 100% primitives replaced
- **Aggregate Consistency:** 100% invariants enforced

### **5.3 Technical Metrics**
- **Query Performance:** <50ms average
- **Cache Performance:** <10ms average
- **Memory Usage:** <500MB under normal load
- **Compile Time:** <30 seconds

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Domain Complexity** - Start with simple models
2. **Performance Impact** - Monitor and optimize
3. **Cache Invalidation** - Implement proper strategies
4. **Validation Overhead** - Optimize validation pipeline

### **6.2 Business Risks**
1. **Domain Model Changes** - Implement versioning
2. **Business Rule Evolution** - Make rules configurable
3. **Data Migration** - Plan migration strategy
4. **Team Adoption** - Provide training and documentation

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Domain Modeling** - Review and enhance models
2. **Value Objects** - Identify and implement VOs
3. **Aggregate Design** - Define aggregate boundaries
4. **Validation Rules** - Define business rules

### **7.2 Short-term Goals (2 Weeks)**
1. **Rich Domain** - Complete domain enhancement
2. **Advanced Validation** - Implement validation framework
3. **Performance Optimization** - Add caching and optimization
4. **Testing** - Add comprehensive tests

### **7.3 Long-term Goals (2 Months)**
1. **Complete Domain** - Full domain-driven design
2. **Performance Excellence** - Optimized for production
3. **Documentation** - Complete documentation set
4. **Team Training** - Knowledge transfer completed

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic POCO entities** with limited behavior
- **Few value objects** and no aggregates
- **Basic validation** and no business rules
- **No performance** optimization

### **8.2 Target State**
- **Rich domain models** with business logic
- **Comprehensive value objects** and aggregates
- **Advanced validation** and business rules engine
- **High performance** with caching and optimization

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Domain-Driven Design** as foundation
- **Performance-focused** optimization
- **Quality-first** testing and documentation

**Status:** Shared module has significant gaps but clear improvement plan with professional domain-driven architecture.
