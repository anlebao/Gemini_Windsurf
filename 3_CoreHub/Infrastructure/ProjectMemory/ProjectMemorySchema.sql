-- Phase 6: Project Memory Schema (SQLite-Compatible)
-- This is the SQLite version of the PostgreSQL schema from ROADMAP_AI_ASSISTED_DEVELOPMENT.md
-- Can be migrated to PostgreSQL using the same table structures

-- Tasks: Track individual AI work sessions
CREATE TABLE IF NOT EXISTS ai_tasks (
    id TEXT PRIMARY KEY,              -- UUID as TEXT for SQLite compatibility
    title TEXT NOT NULL,
    description TEXT,
    agent_name TEXT,                 -- 'Build Fixer', 'Feature Developer', etc.
    status TEXT,                     -- 'pending', 'in_progress', 'completed', 'failed'
    git_branch TEXT,
    git_commit_hash TEXT,
    created_at TEXT,                 -- ISO 8601 format: 2026-06-10T21:30:00Z
    completed_at TEXT,
    metadata TEXT                    -- JSON string for SQLite (use JSONB in PostgreSQL)
);

-- Features: Aggregate multiple tasks into features
CREATE TABLE IF NOT EXISTS ai_features (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    related_adr_ids TEXT,            -- JSON array of ADR IDs: '["ADR-001","ADR-003"]'
    status TEXT,
    created_at TEXT,
    completed_at TEXT
);

-- Feature-Task relationship (many-to-many)
CREATE TABLE IF NOT EXISTS ai_feature_tasks (
    feature_id TEXT REFERENCES ai_features(id) ON DELETE CASCADE,
    task_id TEXT REFERENCES ai_tasks(id) ON DELETE CASCADE,
    PRIMARY KEY (feature_id, task_id)
);

-- Decisions: ADR history and decision log
CREATE TABLE IF NOT EXISTS ai_decisions (
    id TEXT PRIMARY KEY,
    adr_id TEXT,                     -- References decisions/ADR-*.md files
    context TEXT,
    decision TEXT,
    consequences TEXT,               -- JSON string
    made_at TEXT,
    made_by TEXT                     -- 'user' or agent_name
);

-- Agent History: Detailed action log
CREATE TABLE IF NOT EXISTS ai_agent_history (
    id TEXT PRIMARY KEY,
    agent_name TEXT,
    task_id TEXT REFERENCES ai_tasks(id) ON DELETE SET NULL,
    action TEXT,                     -- e.g., 'fixed_build_error', 'implemented_feature'
    input_summary TEXT,
    output_summary TEXT,
    files_modified TEXT,             -- JSON array: '["file1.cs","file2.cs"]'
    success BOOLEAN,
    executed_at TEXT
);

-- Sprint/Session tracking (Vạn An specific)
CREATE TABLE IF NOT EXISTS ai_sessions (
    id TEXT PRIMARY KEY,
    session_code TEXT,               -- e.g., 'S1', 'S2', 'F0'
    feature_name TEXT,               -- e.g., 'UC1 QR Checkout', 'Sprint 3 E-Invoice'
    start_time TEXT,
    end_time TEXT,
    tests_total INTEGER,
    tests_passed INTEGER,
    status TEXT,                     -- 'in_progress', 'completed', 'failed'
    summary TEXT
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_tasks_agent ON ai_tasks(agent_name);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON ai_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created ON ai_tasks(created_at);
CREATE INDEX IF NOT EXISTS idx_agent_history_agent ON ai_agent_history(agent_name);
CREATE INDEX IF NOT EXISTS idx_agent_history_task ON ai_agent_history(task_id);
CREATE INDEX IF NOT EXISTS idx_decisions_adr ON ai_decisions(adr_id);
CREATE INDEX IF NOT EXISTS idx_sessions_feature ON ai_sessions(feature_name);

-- Initial data seed from project history
-- (Run insert statements separately after schema creation)
