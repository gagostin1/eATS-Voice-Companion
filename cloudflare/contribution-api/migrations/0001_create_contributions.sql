CREATE TABLE IF NOT EXISTS contributions (
    id TEXT PRIMARY KEY,
    received_at TEXT NOT NULL,
    schema_version INTEGER NOT NULL CHECK (schema_version = 2),
    app_version TEXT NOT NULL,
    callsign TEXT NOT NULL,
    expected_command TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    review_status TEXT NOT NULL DEFAULT 'pending'
        CHECK (review_status IN ('pending', 'accepted', 'rejected')),
    reviewed_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_contributions_review_queue
    ON contributions (review_status, received_at);
