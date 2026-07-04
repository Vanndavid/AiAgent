CREATE TABLE IF NOT EXISTS applications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company TEXT NOT NULL,
    role TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'saved'
        CHECK (status IN ('saved', 'applied', 'interview', 'offer', 'rejected')),
    applied_at TIMESTAMPTZ,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_applications_status ON applications (status);
CREATE INDEX IF NOT EXISTS idx_applications_applied_at ON applications (applied_at DESC NULLS LAST);

CREATE TABLE IF NOT EXISTS application_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id UUID NOT NULL REFERENCES applications (id) ON DELETE CASCADE,
    from_status TEXT CHECK (
        from_status IS NULL
        OR from_status IN ('saved', 'applied', 'interview', 'offer', 'rejected')
    ),
    to_status TEXT NOT NULL CHECK (
        to_status IN ('saved', 'applied', 'interview', 'offer', 'rejected')
    ),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_application_events_application_id
    ON application_events (application_id, created_at ASC);
