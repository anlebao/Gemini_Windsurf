import { writeFileSync, mkdirSync } from 'fs';
import { join } from 'path';

interface TestResult {
  testName: string;
  status: 'pass' | 'fail' | 'skip';
  duration?: number;
  details?: any;
  error?: string;
  timestamp: Date;
}

interface TierResult {
  tierName: string;
  enabled: boolean;
  status: 'pass' | 'fail' | 'skip' | 'not_run';
  tests: TestResult[];
  startTime: Date;
  endTime?: Date;
  duration?: number;
  architectDecision?: string;
}

export class TestReporter {
  private tierResults: Map<string, TierResult> = new Map();
  private currentTier: string = '';
  private reportPath: string;

  constructor(tierName: string) {
    this.currentTier = tierName;
    this.reportPath = join(process.cwd(), '6_Testing', 'reports');
    this.ensureReportDirectory();
    
    // Initialize tier result
    this.tierResults.set(tierName, {
      tierName,
      enabled: true,
      status: 'not_run',
      tests: [],
      startTime: new Date()
    });
  }

  private ensureReportDirectory(): void {
    try {
      mkdirSync(this.reportPath, { recursive: true });
    } catch (error) {
      console.error('Error creating report directory:', error);
    }
  }

  pass(testName: string, details?: any): void {
    const tier = this.tierResults.get(this.currentTier);
    if (tier) {
      tier.tests.push({
        testName,
        status: 'pass',
        details,
        timestamp: new Date()
      });
    }
  }

  fail(testName: string, details?: any, error?: string): void {
    const tier = this.tierResults.get(this.currentTier);
    if (tier) {
      tier.tests.push({
        testName,
        status: 'fail',
        details,
        error,
        timestamp: new Date()
      });
      tier.status = 'fail';
    }
  }

  skip(testName: string, reason: string = 'Bypassed by Architect'): void {
    const tier = this.tierResults.get(this.currentTier);
    if (tier) {
      tier.tests.push({
        testName,
        status: 'skip',
        details: { reason },
        timestamp: new Date()
      });
    }
  }

  log(message: string): void {
    console.log(`[${this.currentTier}] ${message}`);
  }

  setTierStatus(status: 'pass' | 'fail' | 'skip' | 'not_run'): void {
    const tier = this.tierResults.get(this.currentTier);
    if (tier) {
      tier.status = status;
    }
  }

  setArchitectDecision(decision: string): void {
    const tier = this.tierResults.get(this.currentTier);
    if (tier) {
      tier.architectDecision = decision;
      tier.status = 'skip';
    }
  }

  async generateReport(): Promise<void> {
    const tier = this.tierResults.get(this.currentTier);
    if (!tier) return;

    tier.endTime = new Date();
    tier.duration = tier.endTime.getTime() - tier.startTime.getTime();

    // Calculate overall status
    const failedTests = tier.tests.filter(t => t.status === 'fail').length;
    const passedTests = tier.tests.filter(t => t.status === 'pass').length;
    const skippedTests = tier.tests.filter(t => t.status === 'skip').length;

    if (failedTests > 0) {
      tier.status = 'fail';
    } else if (passedTests > 0) {
      tier.status = 'pass';
    } else if (skippedTests > 0) {
      tier.status = 'skip';
    }

    // Generate JSON report
    const jsonReport = {
      testSuite: 'VanAn Ecosystem',
      version: '1.0.0',
      timestamp: new Date().toISOString(),
      tier: tier,
      summary: {
        total: tier.tests.length,
        passed: passedTests,
        failed: failedTests,
        skipped: skippedTests,
        duration: tier.duration
      }
    };

    const jsonPath = join(this.reportPath, `${this.currentTier}-report.json`);
    writeFileSync(jsonPath, JSON.stringify(jsonReport, null, 2));

    // Generate HTML snippet for dashboard
    const htmlSnippet = this.generateHTMLSnippet(tier);
    const htmlPath = join(this.reportPath, `${this.currentTier}-snippet.html`);
    writeFileSync(htmlPath, htmlSnippet);

    this.log(`Report generated: ${jsonPath}`);
  }

  private generateHTMLSnippet(tier: TierResult): string {
    const statusColor = this.getStatusColor(tier.status);
    const statusIcon = this.getStatusIcon(tier.status);
    
    return `
<div class="tier-card" data-tier="${tier.tierName}">
    <div class="tier-header">
        <h3>${tier.tierName}</h3>
        <span class="status-badge status-${tier.status}" style="background-color: ${statusColor}">
            ${statusIcon} ${tier.status.toUpperCase()}
        </span>
    </div>
    <div class="tier-content">
        <div class="test-summary">
            <span class="test-count passed">${tier.tests.filter(t => t.status === 'pass').length} Passed</span>
            <span class="test-count failed">${tier.tests.filter(t => t.status === 'fail').length} Failed</span>
            <span class="test-count skipped">${tier.tests.filter(t => t.status === 'skip').length} Skipped</span>
        </div>
        <div class="test-duration">
            Duration: ${tier.duration ? (tier.duration / 1000).toFixed(2) : 'N/A'}s
        </div>
        ${tier.architectDecision ? `<div class="architect-note">Architect: ${tier.architectDecision}</div>` : ''}
    </div>
</div>
    `.trim();
  }

  private getStatusColor(status: string): string {
    switch (status) {
      case 'pass': return '#28a745';
      case 'fail': return '#dc3545';
      case 'skip': return '#6c757d';
      case 'not_run': return '#ffc107';
      default: return '#6c757d';
    }
  }

  private getStatusIcon(status: string): string {
    switch (status) {
      case 'pass': return '✓';
      case 'fail': return '✗';
      case 'skip': return '⊘';
      case 'not_run': return '⏸';
      default: return '?';
    }
  }

  static generateDashboard(allTierResults: Map<string, TierResult>): string {
    const timestamp = new Date().toISOString();
    
    let htmlContent = `
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>VanAn Ecosystem - Test Dashboard</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }
        .dashboard {
            max-width: 1200px;
            margin: 0 auto;
        }
        .header {
            text-align: center;
            margin-bottom: 30px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        .tiers-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }
        .tier-card {
            background: white;
            border-radius: 10px;
            padding: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            transition: transform 0.2s ease;
        }
        .tier-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.15);
        }
        .tier-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
        }
        .tier-header h3 {
            margin: 0;
            color: #333;
        }
        .status-badge {
            padding: 5px 10px;
            border-radius: 15px;
            color: white;
            font-size: 12px;
            font-weight: bold;
        }
        .test-summary {
            display: flex;
            gap: 10px;
            margin-bottom: 10px;
        }
        .test-count {
            padding: 3px 8px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: bold;
            color: white;
        }
        .test-count.passed { background-color: #28a745; }
        .test-count.failed { background-color: #dc3545; }
        .test-count.skipped { background-color: #6c757d; }
        .test-duration {
            color: #666;
            font-size: 14px;
        }
        .architect-note {
            margin-top: 10px;
            padding: 8px;
            background-color: #f8f9fa;
            border-left: 4px solid #007bff;
            font-size: 12px;
            color: #495057;
        }
        .footer {
            text-align: center;
            color: #666;
            margin-top: 30px;
            font-size: 14px;
        }
        .status-pass { background-color: #28a745; }
        .status-fail { background-color: #dc3545; }
        .status-skip { background-color: #6c757d; }
        .status-not_run { background-color: #ffc107; }
    </style>
</head>
<body>
    <div class="dashboard">
        <div class="header">
            <h1>🧪 VanAn Ecosystem Test Dashboard</h1>
            <p>Real-time testing status and quality metrics</p>
            <small>Last updated: ${timestamp}</small>
        </div>
        
        <div class="tiers-container">
`;

    // Add tier cards
    allTierResults.forEach((tier) => {
      const statusColor = this.getStatusColorStatic(tier.status);
      const statusIcon = this.getStatusIconStatic(tier.status);
      
      htmlContent += `
            <div class="tier-card" data-tier="${tier.tierName}">
                <div class="tier-header">
                    <h3>${tier.tierName}</h3>
                    <span class="status-badge status-${tier.status}" style="background-color: ${statusColor}">
                        ${statusIcon} ${tier.status.toUpperCase()}
                    </span>
                </div>
                <div class="tier-content">
                    <div class="test-summary">
                        <span class="test-count passed">${tier.tests.filter(t => t.status === 'pass').length} Passed</span>
                        <span class="test-count failed">${tier.tests.filter(t => t.status === 'fail').length} Failed</span>
                        <span class="test-count skipped">${tier.tests.filter(t => t.status === 'skip').length} Skipped</span>
                    </div>
                    <div class="test-duration">
                        Duration: ${tier.duration ? (tier.duration / 1000).toFixed(2) : 'N/A'}s
                    </div>
                    ${tier.architectDecision ? `<div class="architect-note">Architect: ${tier.architectDecision}</div>` : ''}
                </div>
            </div>
`;
    });

    htmlContent += `
        </div>
        
        <div class="footer">
            <p>Generated by VanAn Ecosystem Test Framework v1.0.0</p>
            <p>Configuration: .env.test | Environment: ${process.env.TEST_ENVIRONMENT || 'development'}</p>
        </div>
    </div>
    
    <script>
        // Auto-refresh every 30 seconds
        setTimeout(() => location.reload(), 30000);
    </script>
</body>
</html>
`;

    return htmlContent;
  }

  private static getStatusColorStatic(status: string): string {
    switch (status) {
      case 'pass': return '#28a745';
      case 'fail': return '#dc3545';
      case 'skip': return '#6c757d';
      case 'not_run': return '#ffc107';
      default: return '#6c757d';
    }
  }

  private static getStatusIconStatic(status: string): string {
    switch (status) {
      case 'pass': return '✓';
      case 'fail': return '✗';
      case 'skip': return '⊘';
      case 'not_run': return '⏸';
      default: return '?';
    }
  }
}
