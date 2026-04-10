-- Initialize VanAn CoreHub Database
-- This script runs when PostgreSQL container starts

-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schemas
CREATE SCHEMA IF NOT EXISTS domain;
CREATE SCHEMA IF NOT EXISTS outbox;
CREATE SCHEMA IF NOT EXISTS sync;

-- Create domain tables (will be populated by Entity Framework migrations)
-- These are placeholder commands - EF will create the actual tables

-- Grant permissions
GRANT ALL ON SCHEMA domain TO vanan_admin;
GRANT ALL ON SCHEMA outbox TO vanan_admin;
GRANT ALL ON SCHEMA sync TO vanan_admin;

-- Create indexes for better performance (EF will create more specific ones)
-- These are basic indexes that EF might not create automatically

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'VanAn CoreHub database initialized successfully';
END $$;
