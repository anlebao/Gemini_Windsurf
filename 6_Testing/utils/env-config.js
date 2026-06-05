const fs = require('fs');
const path = require('path');

function loadEnvConfig() {
  const envPath = path.join(__dirname, '../.env.test');
  
  // Default configuration
  const defaultConfig = {
    SMOKE_TEST_ENABLED: true,
    ENABLE_E2E: false,
    ENABLE_LOAD_TEST: false,
    ENABLE_CHAOS: false,
    COREHUB_URL: 'http://localhost:5010',
    GATEWAY_URL: 'http://localhost:5001',
    KHACHLINK_URL: 'http://localhost:3000',
    SHOPERP_URL: 'http://localhost:3001',
    SMOKE_TEST_TIMEOUT: 300,
    E2E_TEST_TIMEOUT: 600,
    LOAD_TEST_DURATION: 120,
    CHAOS_TEST_DURATION: 180
  };
  
  // Try to load from .env.test file
  if (fs.existsSync(envPath)) {
    try {
      const content = fs.readFileSync(envPath, 'utf8');
      const envVars = Object.fromEntries(
        content.split('\n')
          .filter(line => line.trim() && line.includes('=') && !line.startsWith('#'))
          .map(line => {
            const [key, ...valueParts] = line.split('=');
            return [key.trim(), valueParts.join('=').trim()];
          })
      );
      
      // Merge with defaults, converting string values to appropriate types
      const config = { ...defaultConfig };
      for (const [key, value] of Object.entries(envVars)) {
        if (value === 'true') {
          config[key] = true;
        } else if (value === 'false') {
          config[key] = false;
        } else if (!isNaN(value) && value !== '') {
          config[key] = parseInt(value, 10);
        } else {
          config[key] = value;
        }
      }
      
      return config;
    } catch (error) {
      console.warn(`Warning: Could not parse .env.test file: ${error.message}`);
      return defaultConfig;
    }
  }
  
  return defaultConfig;
}

function isTierEnabled(tier) {
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

function getTierConfig(tier) {
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
        timeout: config.E2E_TEST_TIMEOUT
      };
    case 'load':
      return {
        enabled: config.ENABLE_LOAD_TEST,
        duration: config.LOAD_TEST_DURATION
      };
    case 'chaos':
      return {
        enabled: config.ENABLE_CHAOS,
        duration: config.CHAOS_TEST_DURATION
      };
    default:
      return { enabled: false };
  }
}

// Alias for backward compatibility
function getTestConfig() {
  return loadEnvConfig();
}

module.exports = { loadEnvConfig, isTierEnabled, getTierConfig, getTestConfig };
