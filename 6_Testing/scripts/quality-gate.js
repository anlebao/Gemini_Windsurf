#!/usr/bin/env node

/**
 * VanAn Ecosystem - Quality Gate Script
 * Orchestrates all testing tiers based on configuration
 */

const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const { loadEnvConfig } = require('../utils/env-config');

class QualityGate {
  constructor() {
    this.config = loadEnvConfig();
    this.results = new Map();
    this.startTime = Date.now();
    this.reportDir = path.join(__dirname, '../reports');
    this.ensureReportDirectory();
  }

  ensureReportDirectory() {
    if (!fs.existsSync(this.reportDir)) {
      fs.mkdirSync(this.reportDir, { recursive: true });
    }
  }

  log(message, level = 'INFO') {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] [${level}] ${message}`);
  }

  async executeCommand(command, description, timeout = 600000) {
    this.log(`Starting: ${description}`);
    
    const startTime = Date.now();
    
    return new Promise((resolve) => {
      const output = [];
      const errorOutput = [];
      
      const child = spawn(command, {
        cwd: __dirname,
        shell: true,
        stdio: 'pipe'
      });
      
      let timer;
      if (timeout) {
        timer = setTimeout(() => {
          child.kill();
          const duration = Date.now() - startTime;
          this.log(`\u274c ${description} - FAILED (${duration}ms)`);
          this.log(`Error: Command timed out after ${timeout}ms`);
          resolve({
            status: 'fail',
            duration,
            error: `Command timed out after ${timeout}ms`,
            description
          });
        }, timeout);
      }
      
      child.stdout.on('data', (data) => {
        output.push(data.toString());
      });
      
      child.stderr.on('data', (data) => {
        errorOutput.push(data.toString());
      });
      
      child.on('close', (code) => {
        if (timer) clearTimeout(timer);
        const duration = Date.now() - startTime;
        const fullOutput = output.join('');
        const fullError = errorOutput.join('');
        
        if (code === 0) {
          this.log(`\u2705 ${description} - PASSED (${duration}ms)`);
          resolve({
            status: 'pass',
            duration,
            output: fullOutput,
            description
          });
        } else {
          this.log(`\u274c ${description} - FAILED (${duration}ms)`);
          this.log(`Error: ${fullError || fullOutput || `Exit code: ${code}`}`);
          resolve({
            status: 'fail',
            duration,
            error: fullError || fullOutput || `Exit code: ${code}`,
            description
          });
        }
      });
      
      child.on('error', (error) => {
        if (timer) clearTimeout(timer);
        const duration = Date.now() - startTime;
        this.log(`\u274c ${description} - FAILED (${duration}ms)`);
        this.log(`Error: ${error.message}`);
        resolve({
          status: 'fail',
          duration,
          error: error.message,
          description
        });
      });
    });
  }

  async runSmokeTests() {
    if (!this.config.SMOKE_TEST_ENABLED) {
      this.log('🔥 Smoke tests disabled - skipping');
      return {
        status: 'skip',
        duration: 0,
        description: 'Smoke Tests',
        architectDecision: 'Bypassed by Architect - Smoke tests disabled'
      };
    }

    // Run Playwright smoke tests
    const command = 'npx playwright test smoke-tests/';
    return await this.executeCommand(
      command,
      'Smoke Tests (Playwright)',
      this.config.SMOKE_TEST_TIMEOUT * 1000
    );
  }

  async runE2ETests() {
    if (!this.config.ENABLE_E2E) {
      this.log('🔥 E2E tests disabled - skipping');
      return {
        status: 'skip',
        duration: 0,
        description: 'E2E Tests',
        architectDecision: 'Bypassed by Architect - E2E tests disabled'
      };
    }

    // Run Playwright E2E tests
    const command = 'npx playwright test e2e-tests/';
    return await this.executeCommand(
      command,
      'E2E Tests (Playwright)',
      this.config.E2E_TEST_TIMEOUT * 1000
    );
  }

  async runLoadTests() {
    if (!this.config.ENABLE_LOAD_TEST) {
      this.log('⚡ Load tests disabled - skipping');
      return {
        status: 'skip',
        duration: 0,
        description: 'Load Tests',
        architectDecision: 'Bypassed by Architect - Load tests disabled'
      };
    }

    const command = 'k6 run load-tests/load-test.js';
    return await this.executeCommand(
      command,
      'Load Tests',
      (this.config.LOAD_TEST_DURATION + 30) * 1000 // Add buffer time
    );
  }

  async runChaosTests() {
    if (!this.config.ENABLE_CHAOS) {
      this.log('🔥 Chaos tests disabled - skipping');
      return {
        status: 'skip',
        duration: 0,
        description: 'Chaos Tests',
        architectDecision: 'Bypassed by Architect - Chaos tests disabled'
      };
    }

    const command = 'python chaos-tests/chaos-engine.py';
    return await this.executeCommand(
      command,
      'Chaos Tests',
      (this.config.CHAOS_TEST_DURATION + 30) * 1000 // Add buffer time
    );
  }

  async generateDashboard() {
    const dashboardPath = path.join(__dirname, '../dashboard/index.html');
    const reportPath = path.join(this.reportDir, 'quality-gate-dashboard.html');
    
    // Copy dashboard to reports directory
    if (fs.existsSync(dashboardPath)) {
      fs.copyFileSync(dashboardPath, reportPath);
      this.log('📊 Dashboard generated');
    }
  }

  generateSummary() {
    const totalDuration = Date.now() - this.startTime;
    const results = Array.from(this.results.values());
    
    const passed = results.filter(r => r.status === 'pass').length;
    const failed = results.filter(r => r.status === 'fail').length;
    const skipped = results.filter(r => r.status === 'skip').length;

    return {
      testSuite: 'VanAn Ecosystem Quality Gate',
      version: '1.0.0',
      timestamp: new Date().toISOString(),
      duration: totalDuration,
      summary: {
        total: results.length,
        passed,
        failed,
        skipped
      },
      configuration: this.config,
      results: Object.fromEntries(this.results),
      status: failed > 0 ? 'fail' : (passed > 0 ? 'pass' : 'skip')
    };
  }

  saveReport(summary) {
    const reportPath = path.join(this.reportDir, `quality-gate-report-${Date.now()}.json`);
    fs.writeFileSync(reportPath, JSON.stringify(summary, null, 2));
    
    // Also save latest report
    const latestPath = path.join(this.reportDir, 'quality-gate-latest.json');
    fs.writeFileSync(latestPath, JSON.stringify(summary, null, 2));
    
    this.log(`📋 Report saved: ${reportPath}`);
  }

  async run() {
    const isFullAudit = process.argv.includes('--full-audit');
    
    this.log('🚀 VanAn Ecosystem - Quality Gate Starting');
    this.log(`📊 Configuration: Smoke=${this.config.SMOKE_TEST_ENABLED}, E2E=${this.config.ENABLE_E2E}, Load=${this.config.ENABLE_LOAD_TEST}, Chaos=${this.config.ENABLE_CHAOS}`);
    
    if (isFullAudit) {
      this.log('🔥 Full Audit Mode - All tiers enabled');
      // Temporarily enable all tiers for full audit
      this.config.ENABLE_LOAD_TEST = true;
      this.config.ENABLE_CHAOS = true;
    }

    try {
      // Run all test tiers
      this.results.set('smoke', await this.runSmokeTests());
      this.results.set('e2e', await this.runE2ETests());
      this.results.set('load', await this.runLoadTests());
      this.results.set('chaos', await this.runChaosTests());

      // Generate reports
      const summary = this.generateSummary();
      this.saveReport(summary);
      await this.generateDashboard();

      // Display results
      this.displayResults(summary);

      // Exit with appropriate code
      if (summary.status === 'fail') {
        this.log('❌ Quality Gate FAILED');
        process.exit(1);
      } else if (summary.status === 'pass') {
        this.log('✅ Quality Gate PASSED');
        process.exit(0);
      } else {
        this.log('⊘ Quality Gate SKIPPED');
        process.exit(0);
      }

    } catch (error) {
      this.log(`💥 Quality Gate ERROR: ${error.message}`, 'ERROR');
      process.exit(2);
    }
  }

  displayResults(summary) {
    console.log('\n' + '='.repeat(80));
    console.log('🧪 VANAN ECOSYSTEM - QUALITY GATE RESULTS');
    console.log('='.repeat(80));
    
    console.log(`⏱️  Total Duration: ${(summary.duration / 1000).toFixed(2)}s`);
    console.log(`📊 Summary: ${summary.summary.passed} passed, ${summary.summary.failed} failed, ${summary.summary.skipped} skipped`);
    console.log(`🎯 Overall Status: ${summary.status.toUpperCase()}`);
    
    console.log('\n📋 Tier Results:');
    Object.entries(summary.results).forEach(([tier, result]) => {
      const status = result.status === 'skip' ? '⊘' : (result.status === 'pass' ? '✅' : '❌');
      const duration = result.duration ? `${(result.duration / 1000).toFixed(2)}s` : 'N/A';
      console.log(`   ${status} ${tier.padEnd(8)}: ${result.description} (${duration})`);
      
      if (result.architectDecision) {
        console.log(`      📝 ${result.architectDecision}`);
      }
    });
    
    console.log('\n🔧 Configuration:');
    console.log(`   Smoke Tests: ${this.config.SMOKE_TEST_ENABLED ? '✅' : '❌'}`);
    console.log(`   E2E Tests: ${this.config.ENABLE_E2E ? '✅' : '❌'}`);
    console.log(`   Load Tests: ${this.config.ENABLE_LOAD_TEST ? '✅' : '❌'}`);
    console.log(`   Chaos Tests: ${this.config.ENABLE_CHAOS ? '✅' : '❌'}`);
    
    console.log('\n🌐 Service Endpoints:');
    console.log(`   CoreHub: ${this.config.COREHUB_URL}`);
    console.log(`   Gateway: ${this.config.GATEWAY_URL}`);
    console.log(`   KhachLink: ${this.config.KHACHLINK_URL}`);
    console.log(`   ShopERP: ${this.config.SHOPERP_URL}`);
    
    console.log('\n📊 Reports generated:');
    console.log(`   📋 JSON Report: ${path.join(this.reportDir, 'quality-gate-latest.json')}`);
    console.log(`   📊 Dashboard: ${path.join(this.reportDir, 'quality-gate-dashboard.html')}`);
    
    console.log('='.repeat(80));
  }
}

// Handle command line arguments
const args = process.argv.slice(2);
if (args.includes('--help') || args.includes('-h')) {
  console.log(`
VanAn Ecosystem - Quality Gate

Usage:
  node scripts/quality-gate.js [options]

Options:
  --full-audit    Enable all test tiers (including load and chaos)
  --help, -h      Show this help message

Examples:
  node scripts/quality-gate.js                # Run based on .env.test configuration
  node scripts/quality-gate.js --full-audit   # Run all tests regardless of configuration
  docker-compose run quality-gate              # Run via Docker Compose
  docker-compose run quality-gate --full-audit  # Full audit via Docker Compose

Configuration:
  Edit .env.test file to control which test tiers run
  - SMOKE_TEST_ENABLED=true/false
  - ENABLE_E2E=true/false
  - ENABLE_LOAD_TEST=true/false
  - ENABLE_CHAOS=true/false
`);
  process.exit(0);
}

// Run quality gate
const qualityGate = new QualityGate();
qualityGate.run().catch(error => {
  console.error('Quality gate failed:', error);
  process.exit(2);
});
