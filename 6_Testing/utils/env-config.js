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

module.exports = { loadEnvConfig };
