-- Persist ReAct agent runs for history in the UI / API
CREATE TABLE IF NOT EXISTS agent_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    goal TEXT NOT NULL,
    final_answer TEXT NOT NULL,
    scratchpad JSONB NOT NULL DEFAULT '[]'::jsonb,
    tools_used JSONB NOT NULL DEFAULT '[]'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_agent_runs_created_at ON agent_runs (created_at DESC);
