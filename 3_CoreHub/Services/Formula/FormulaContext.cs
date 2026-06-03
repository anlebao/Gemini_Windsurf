using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Formula
{
    /// <summary>
    /// Domain-aware formula context for proper architecture
    /// Replaces Dictionary<string, decimal> hack with proper domain context
    /// </summary>
    public class FormulaContext
    {
        public TenantId TenantId { get; init; }
        public AccountingPeriod Period { get; init; }
        public Dictionary<string, decimal> Variables { get; init; } = new();
        
        public FormulaContext(TenantId tenantId, AccountingPeriod period)
        {
            TenantId = tenantId;
            Period = period;
        }
        
        public FormulaContext WithVariable(string name, decimal value)
        {
            Variables[name] = value;
            return this;
        }
        
        public FormulaContext WithVariables(Dictionary<string, decimal> variables)
        {
            foreach (var kvp in variables)
            {
                Variables[kvp.Key] = kvp.Value;
            }
            return this;
        }
    }
}
