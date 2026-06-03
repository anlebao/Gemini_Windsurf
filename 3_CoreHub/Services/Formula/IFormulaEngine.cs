using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Formula
{
    /// <summary>
    /// Formula Engine Interface - Production Ready
    /// Supports FINAL DSL syntax: SUM_ACCOUNT("5*", "Credit")
    /// </summary>
    public interface IFormulaEngine
    {
        /// <summary>
        /// Evaluate formula with domain context
        /// </summary>
        /// <param name="formula">Formula string using FINAL DSL syntax</param>
        /// <param name="context">Domain-aware formula context with tenant, period, and variables</param>
        /// <returns>Calculated result</returns>
        decimal Evaluate(string formula, FormulaContext context);
        
        /// <summary>
        /// Evaluate formula with variables (legacy compatibility)
        /// </summary>
        /// <param name="formula">Formula string using FINAL DSL syntax</param>
        /// <param name="variables">Variable values for evaluation</param>
        /// <returns>Calculated result</returns>
        decimal Evaluate(string formula, Dictionary<string, decimal> variables);
        
        /// <summary>
        /// Validate formula syntax
        /// </summary>
        /// <param name="formula">Formula string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateFormula(string formula);
        
        /// <summary>
        /// Get formula dependencies
        /// </summary>
        /// <param name="formula">Formula string to analyze</param>
        /// <returns>List of dependency names</returns>
        List<string> GetDependencies(string formula);
    }
}
