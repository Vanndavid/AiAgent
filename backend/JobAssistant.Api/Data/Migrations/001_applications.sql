-- Applications table (initial schema)
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
CREATE INDEX IF NOT EXISTS idx_applications_company_lower ON applications (LOWER(company));
CREATE INDEX IF NOT EXISTS idx_applications_role_lower ON applications (LOWER(role));
