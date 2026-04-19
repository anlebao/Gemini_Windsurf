// windsurf-guard.js v6.0 - VÁN AN ACCOUNTING STRICT MODE
// Enforced by Grok Orchestrator - Zero Tolerance

console.log("ð WINDSURF GUARD v6.0 - VÁN AN ACCOUNTING STRICT MODE");

const fs = require('fs');
const path = require('path');

const violations = [];

// RULE 1: NO BUSINESS LOGIC IN ACCOUNTING GATEWAY (Group 1 scope)
const accountingGatewayFiles = ['2_Gateway/Controllers/AccountingController.cs'];

accountingGatewayFiles.forEach(file => {
    if (fs.existsSync(file)) {
        const content = fs.readFileSync(file, 'utf8');
        if ((content.includes('new ') || content.includes('await ')) &&
            !content.includes('Inject') && !content.includes('_service')) {
            violations.push(`ð BUSINESS LOGIC IN ACCOUNTING GATEWAY: ${file}`);
        }
    }
});

// RULE 2: NO HACKS / SUPPRESSION
const hackPatterns = [
    '<GenerateAssemblyInfo>false',
    '<NoWarn>',
    '#pragma warning disable',
    'SuppressMessage',
    'Ignore',
    'TODO: hack',
    'pre-existing'
];

const csprojFiles = ['VanAn.Accounting.csproj', 'VanAn.CoreHub.csproj', 'VanAn.ShopERP.csproj', 'VanAn.KhachLink.csproj'];
csprojFiles.forEach(file => {
    if (fs.existsSync(file)) {
        const content = fs.readFileSync(file, 'utf8');
        hackPatterns.forEach(pattern => {
            if (content.includes(pattern)) {
                violations.push(`ð HACK DETECTED in ${file}: ${pattern}`);
            }
        });
    }
});

// RULE 3: IMMUTABILITY CHECK FOR ACCOUNTINGENTRY
if (fs.existsSync('VanAn.Accounting/Domain/AccountingEntry.cs')) {
    const content = fs.readFileSync('VanAn.Accounting/Domain/AccountingEntry.cs', 'utf8');
    if (!content.includes('Once created, never changed') || 
        !content.includes('Reversal Entry is the only way')) {
        violations.push(`ð IMMUTABILITY COMMENT MISSING in AccountingEntry.cs`);
    }
}

// OUTPUT
if (violations.length > 0) {
    console.log("â GUARD FAILED:");
    violations.forEach(v => console.log(v));
    console.log("\nð SUBMISSION REJECTED. Fix all violations before resubmitting.");
    process.exit(1);
} else {
    console.log("â GUARD PASSED - All strict rules compliant.");
    process.exit(0);
}