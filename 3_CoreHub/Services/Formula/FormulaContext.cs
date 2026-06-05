using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Formula
{
    /// <summary>
    /// Domain-aware formula context for proper architecture
    /// Replaces Dictionary<string, decimal> hack with proper domain context
    /// </summary>
    public class FormulaContext(TenantId tenantId, AccountingPeriod period)
    {
        public TenantId TenantId { get; init; } = tenantId;
        public AccountingPeriod Period { get; init; } = period;
        public Dictionary<string, decimal> Variables { get; init; } = [];

        public FormulaContext WithVariable(string name, decimal value)
        {
            Variables[name] = value;
            return this;
        }

        public FormulaContext WithVariables(Dictionary<string, decimal> variables)
        {
            foreach (KeyValuePair<string, decimal> kvp in variables)
            {
                Variables[kvp.Key] = kvp.Value;
            }
            return this;
        }
    }
}
