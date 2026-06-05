using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Debug script to test GetDependencies logic
var formula = @"SUM_ACCOUNT(""5"", ""Credit"") - SUM_ACCOUNT(""6"", ""Debit"") + TaxAmount";
var dependencies = new HashSet<string>();

// Extract SUM_ACCOUNT dependencies
if (formula.Contains("SUM_ACCOUNT"))
{
    var match = Regex.Match(formula, @"SUM_ACCOUNT\(""([^""]*)"",\s*""([^""]*)""\)", RegexOptions.IgnoreCase);
    if (match.Success)
    {
        var accountPattern = match.Groups[1].Value;
        var side = match.Groups[2].Value;
        dependencies.Add($"Account_{accountPattern}_{side}");
        Console.WriteLine($"Added SUM_ACCOUNT dependency: Account_{accountPattern}_{side}");
    }
}

// Extract BALANCE_ACCOUNT dependencies
if (formula.Contains("BALANCE_ACCOUNT"))
{
    var match = Regex.Match(formula, @"BALANCE_ACCOUNT\(""([^""]*)"",\s*""([^""]*)""\)", RegexOptions.IgnoreCase);
    if (match.Success)
    {
        var accountPattern = match.Groups[1].Value;
        dependencies.Add($"Account_{accountPattern}_Balance");
        Console.WriteLine($"Added BALANCE_ACCOUNT dependency: Account_{accountPattern}_Balance");
    }
}

// Extract variable dependencies
var variableMatches = Regex.Matches(formula, @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b");
Console.WriteLine($"Found {variableMatches.Count} variable matches:");
foreach (Match match in variableMatches)
{
    var variable = match.Groups[1].Value;
    Console.WriteLine($"  - {variable}");
    
    if (!variable.Equals("SUM_ACCOUNT", StringComparison.OrdinalIgnoreCase) &&
        !variable.Equals("BALANCE_ACCOUNT", StringComparison.OrdinalIgnoreCase) &&
        !variable.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase) &&
        !variable.Equals("RATIO", StringComparison.OrdinalIgnoreCase) &&
        !variable.Equals("Credit", StringComparison.OrdinalIgnoreCase) &&
        !variable.Equals("Debit", StringComparison.OrdinalIgnoreCase) &&
        !Regex.IsMatch(variable, @"^\d+$") && // Exclude plain account numbers
        !dependencies.Contains(variable))
    {
        dependencies.Add(variable);
        Console.WriteLine($"Added variable dependency: {variable}");
    }
}

Console.WriteLine($"\nFinal dependencies ({dependencies.Count}):");
foreach (var dep in dependencies)
{
    Console.WriteLine($"  - {dep}");
}
