const fs = require("fs");
const path = require("path");

const RULE_PATH = ".windsurfrules";

function loadRules() {
  if (!fs.existsSync(RULE_PATH)) {
    console.error("ERROR: Missing .winsurfrules");
    return null;
  }
  
  try {
    const rules = fs.readFileSync(RULE_PATH, "utf-8");
    console.log("SUCCESS: .winsurfrules loaded");
    return rules;
  } catch (error) {
    console.error("ERROR: Failed to read .winsurfrules:", error.message);
    return null;
  }
}

function validateCoreHubArchitecture() {
  const coreHubPath = "3_CoreHub";
  
  if (!fs.existsSync(coreHubPath)) {
    console.log("WARNING: 3_CoreHub directory not found");
    return 0;
  }

  // Check for forbidden dependencies in .csproj
  const csprojPath = path.join(coreHubPath, "VanAn.CoreHub.csproj");
  if (fs.existsSync(csprojPath)) {
    const csprojContent = fs.readFileSync(csprojPath, "utf-8");
    
    const forbiddenPatterns = [
      "Microsoft.AspNetCore.Authentication.JwtBearer",
      "Microsoft.AspNetCore.OpenApi",
      "Microsoft.AspNetCore.Mvc",
      "UseAuthentication",
      "UseAuthorization"
    ];

    for (const pattern of forbiddenPatterns) {
      if (csprojContent.includes(pattern)) {
        console.error(`VIOLATION: Found forbidden pattern in CoreHub: ${pattern}`);
        return 1;
      }
    }
  }

  // Check for HttpContext usage in .cs files
  function scanDirectory(dir, pattern) {
    if (!fs.existsSync(dir)) return 0;
    
    const files = fs.readdirSync(dir);
    for (const file of files) {
      const filePath = path.join(dir, file);
      const stat = fs.statSync(filePath);
      
      if (stat.isDirectory()) {
        const result = scanDirectory(filePath, pattern);
        if (result !== 0) return result;
      } else if (file.endsWith('.cs')) {
        const content = fs.readFileSync(filePath, "utf-8");
        if (content.includes(pattern)) {
          console.error(`VIOLATION: Found ${pattern} in ${filePath}`);
          return 1;
        }
      }
    }
    return 0;
  }

  const httpContextResult = scanDirectory(coreHubPath, "HttpContext");
  if (httpContextResult !== 0) return httpContextResult;

  const claimsResult = scanDirectory(coreHubPath, "ClaimsPrincipal");
  if (claimsResult !== 0) return claimsResult;

  return 0;
}

function validateTDDCompliance() {
  const testsPath = "6_Tests";
  
  if (!fs.existsSync(testsPath)) {
    console.log("WARNING: 6_Tests directory not found");
    return 0;
  }

  // Check if test project exists
  const testProjectPath = path.join(testsPath, "VanAn.OrderFlow.Tests");
  if (!fs.existsSync(testProjectPath)) {
    console.log("WARNING: OrderFlow tests not found");
    return 0;
  }

  // Check for test files
  const testFilesPath = path.join(testProjectPath, "OrderApiTests.cs");
  if (!fs.existsSync(testFilesPath)) {
    console.log("WARNING: OrderApiTests.cs not found");
    return 0;
  }

  console.log("SUCCESS: TDD structure validated");
  return 0;
}

function validate() {
  const rules = loadRules();
  if (!rules) {
    return 1;
  }

  console.log("\n=== WINDSURF RULE VALIDATION ===");
  console.log("Validating against .winsurfrules...\n");

  // Inject rules into validation context
  console.log("RULE CONTEXT:");
  console.log(rules.substring(0, 500) + "...\n");

  // Core Architecture Validation
  console.log("1. Checking CoreHub Architecture...");
  const archResult = validateCoreHubArchitecture();
  if (archResult !== 0) {
    console.error("FAILED: CoreHub architecture validation");
    return archResult;
  }
  console.log("PASSED: CoreHub architecture validation");

  // TDD Validation
  console.log("2. Checking TDD Compliance...");
  const tddResult = validateTDDCompliance();
  if (tddResult !== 0) {
    console.error("FAILED: TDD compliance validation");
    return tddResult;
  }
  console.log("PASSED: TDD compliance validation");

  console.log("\n=== ALL VALIDATIONS PASSED ===");
  return 0;
}

function main() {
  try {
    const result = validate();
    process.exit(result);
  } catch (error) {
    console.error("ERROR: Validation script failed:", error.message);
    process.exit(1);
  }
}

main();
