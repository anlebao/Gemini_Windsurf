import { readFileSync } from 'fs';
import { join } from 'path';
import { config } from 'dotenv';

interface TestConfig {
  // Smoke Tests
  SMOKE_TEST_ENABLED: boolean;
  SMOKE_TEST_TIMEOUT: number;
  
  // E2E Tests
  ENABLE_E2E: boolean;
  E2E_TEST_TIMEOUT: number;
  E2E_TEST_PARALLEL: boolean;
  
  // Load Tests
  ENABLE_LOAD_TEST: boolean;
  LOAD_TEST_DURATION: number;
  LOAD_TEST_VUS: number;
  LOAD_TEST_RAMP_UP: number;
  
  // Chaos Tests
  ENABLE_CHAOS: boolean;
  CHAOS_TEST_DURATION: number;
  CHAOS_TEST_LATENCY: boolean;
  CHAOS_TEST_FAILURES: boolean;
  
  // General Configuration
  TEST_ENVIRONMENT: string;
  TEST_REPORT_FORMAT: string;
  TEST_RETENTION_DAYS: number;
  
  // Quality Gate
  QUALITY_GATE_STRICT: boolean;
  QUALITY_GATE_TIMEOUT: number;
  
  // Service Endpoints
  COREHUB_URL: string;
  GATEWAY_URL: string;
  KHACHLINK_URL: string;
  SHOPERP_URL: string;
  
  // Database
  TEST_DATABASE_CLEANUP: boolean;
  TEST_DATABASE_SEED: boolean;
  
  // Reporting
  REPORT_EMAIL_ENABLED: boolean;
  REPORT_SLACK_ENABLED: boolean;
  REPORT_TEAMS_ENABLED: boolean;
}

export function loadEnvConfig(): TestConfig {
  // Load .env.test file
  const envPath = join(process.cwd(), '.env.test');
  
  try {
    const envContent = readFileSync(envPath, 'utf8');
    const envLines = envContent.split('\n');
    
    const config: any = {};
    
    envLines.forEach(line => {
      const trimmedLine = line.trim();
      if (trimmedLine && !trimmedLine.startsWith('#')) {
        const [key, ...valueParts] = trimmedLine.split('=');
        if (key && valueParts.length > 0) {
          const value = valueParts.join('=').trim();
          config[key] = value;
        }
      }
    });
    
    return {
      // Smoke Tests
      SMOKE_TEST_ENABLED: config.SMOKE_TEST_ENABLED === 'true',
      SMOKE_TEST_TIMEOUT: parseInt(config.SMOKE_TEST_TIMEOUT) || 30,
      
      // E2E Tests
      ENABLE_E2E: config.ENABLE_E2E === 'true',
      E2E_TEST_TIMEOUT: parseInt(config.E2E_TEST_TIMEOUT) || 120,
      E2E_TEST_PARALLEL: config.E2E_TEST_PARALLEL === 'true',
      
      // Load Tests
      ENABLE_LOAD_TEST: config.ENABLE_LOAD_TEST === 'true',
      LOAD_TEST_DURATION: parseInt(config.LOAD_TEST_DURATION) || 60,
      LOAD_TEST_VUS: parseInt(config.LOAD_TEST_VUS) || 10,
      LOAD_TEST_RAMP_UP: parseInt(config.LOAD_TEST_RAMP_UP) || 10,
      
      // Chaos Tests
      ENABLE_CHAOS: config.ENABLE_CHAOS === 'true',
      CHAOS_TEST_DURATION: parseInt(config.CHAOS_TEST_DURATION) || 300,
      CHAOS_TEST_LATENCY: config.CHAOS_TEST_LATENCY === 'true',
      CHAOS_TEST_FAILURES: config.CHAOS_TEST_FAILURES === 'true',
      
      // General Configuration
      TEST_ENVIRONMENT: config.TEST_ENVIRONMENT || 'development',
      TEST_REPORT_FORMAT: config.TEST_REPORT_FORMAT || 'html',
      TEST_RETENTION_DAYS: parseInt(config.TEST_RETENTION_DAYS) || 7,
      
      // Quality Gate
      QUALITY_GATE_STRICT: config.QUALITY_GATE_STRICT === 'true',
      QUALITY_GATE_TIMEOUT: parseInt(config.QUALITY_GATE_TIMEOUT) || 600,
      
      // Service Endpoints
      COREHUB_URL: config.COREHUB_URL || 'http://localhost:5010',
      GATEWAY_URL: config.GATEWAY_URL || 'http://localhost:5001',
      KHACHLINK_URL: config.KHACHLINK_URL || 'http://localhost:5002',
      SHOPERP_URL: config.SHOPERP_URL || 'http://localhost:5003',
      
      // Database
      TEST_DATABASE_CLEANUP: config.TEST_DATABASE_CLEANUP === 'true',
      TEST_DATABASE_SEED: config.TEST_DATABASE_SEED === 'true',
      
      // Reporting
      REPORT_EMAIL_ENABLED: config.REPORT_EMAIL_ENABLED === 'true',
      REPORT_SLACK_ENABLED: config.REPORT_SLACK_ENABLED === 'true',
      REPORT_TEAMS_ENABLED: config.REPORT_TEAMS_ENABLED === 'true',
    };
    
  } catch (error) {
    console.error('Error loading .env.test file:', error);
    
    // Return default configuration
    return {
      SMOKE_TEST_ENABLED: true,
      SMOKE_TEST_TIMEOUT: 30,
      ENABLE_E2E: true,
      E2E_TEST_TIMEOUT: 120,
      E2E_TEST_PARALLEL: false,
      ENABLE_LOAD_TEST: false,
      LOAD_TEST_DURATION: 60,
      LOAD_TEST_VUS: 10,
      LOAD_TEST_RAMP_UP: 10,
      ENABLE_CHAOS: false,
      CHAOS_TEST_DURATION: 300,
      CHAOS_TEST_LATENCY: true,
      CHAOS_TEST_FAILURES: false,
      TEST_ENVIRONMENT: 'development',
      TEST_REPORT_FORMAT: 'html',
      TEST_RETENTION_DAYS: 7,
      QUALITY_GATE_STRICT: false,
      QUALITY_GATE_TIMEOUT: 600,
      COREHUB_URL: 'http://localhost:5010',
      GATEWAY_URL: 'http://localhost:5001',
      KHACHLINK_URL: 'http://localhost:5002',
      SHOPERP_URL: 'http://localhost:5003',
      TEST_DATABASE_CLEANUP: true,
      TEST_DATABASE_SEED: true,
      REPORT_EMAIL_ENABLED: false,
      REPORT_SLACK_ENABLED: false,
      REPORT_TEAMS_ENABLED: false,
    };
  }
}

export function isTierEnabled(tier: 'smoke' | 'e2e' | 'load' | 'chaos'): boolean {
  const config = loadEnvConfig();
  
  switch (tier) {
    case 'smoke':
      return config.SMOKE_TEST_ENABLED;
    case 'e2e':
      return config.ENABLE_E2E;
    case 'load':
      return config.ENABLE_LOAD_TEST;
    case 'chaos':
      return config.ENABLE_CHAOS;
    default:
      return false;
  }
}

export function getTierConfig(tier: 'smoke' | 'e2e' | 'load' | 'chaos') {
  const config = loadEnvConfig();
  
  switch (tier) {
    case 'smoke':
      return {
        enabled: config.SMOKE_TEST_ENABLED,
        timeout: config.SMOKE_TEST_TIMEOUT
      };
    case 'e2e':
      return {
        enabled: config.ENABLE_E2E,
        timeout: config.E2E_TEST_TIMEOUT,
        parallel: config.E2E_TEST_PARALLEL
      };
    case 'load':
      return {
        enabled: config.ENABLE_LOAD_TEST,
        duration: config.LOAD_TEST_DURATION,
        vus: config.LOAD_TEST_VUS,
        rampUp: config.LOAD_TEST_RAMP_UP
      };
    case 'chaos':
      return {
        enabled: config.ENABLE_CHAOS,
        duration: config.CHAOS_TEST_DURATION,
        latency: config.CHAOS_TEST_LATENCY,
        failures: config.CHAOS_TEST_FAILURES
      };
    default:
      return { enabled: false };
  }
}
