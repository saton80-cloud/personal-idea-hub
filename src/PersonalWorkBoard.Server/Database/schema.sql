CREATE TABLE IF NOT EXISTS users (
    id CHAR(36) PRIMARY KEY,
    user_name VARCHAR(80) NOT NULL UNIQUE,
    display_name VARCHAR(120) NOT NULL,
    password_salt VARBINARY(32) NOT NULL,
    password_hash VARBINARY(64) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS devices (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    name VARCHAR(120) NOT NULL,
    platform VARCHAR(40) NOT NULL,
    last_seen_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_devices_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_devices_owner (owner_id, revoked_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS sessions (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    device_id CHAR(36) NOT NULL,
    token_hash BINARY(32) NOT NULL UNIQUE,
    expires_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_sessions_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_sessions_device FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE,
    INDEX idx_sessions_expiry (expires_at, revoked_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS pairing_tickets (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    pc_device_id CHAR(36) NOT NULL,
    ticket_hash BINARY(32) NOT NULL UNIQUE,
    expires_at DATETIME(6) NOT NULL,
    used_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_pairing_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_pairing_pc FOREIGN KEY (pc_device_id) REFERENCES devices(id) ON DELETE CASCADE,
    INDEX idx_pairing_expiry (expires_at, used_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS work_items (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    parent_id CHAR(36) NULL,
    type VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    priority VARCHAR(16) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT NOT NULL,
    progress INT NOT NULL DEFAULT 0,
    next_action VARCHAR(500) NOT NULL DEFAULT '',
    target_date DATE NULL,
    current_version VARCHAR(40) NOT NULL DEFAULT 'V0.1',
    row_version BIGINT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    deleted_at DATETIME(6) NULL,
    CONSTRAINT fk_work_items_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_work_items_parent FOREIGN KEY (parent_id) REFERENCES work_items(id) ON DELETE SET NULL,
    INDEX idx_work_items_owner_status (owner_id, status, deleted_at),
    INDEX idx_work_items_parent (parent_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS work_tasks (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    work_item_id CHAR(36) NULL,
    title VARCHAR(200) NOT NULL,
    detail TEXT NOT NULL,
    status VARCHAR(24) NOT NULL,
    priority VARCHAR(16) NOT NULL,
    planned_date DATE NULL,
    due_at DATETIME(6) NULL,
    reminder_at DATETIME(6) NULL,
    estimated_minutes INT NOT NULL DEFAULT 30,
    actual_minutes INT NOT NULL DEFAULT 0,
    progress INT NOT NULL DEFAULT 0,
    row_version BIGINT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    deleted_at DATETIME(6) NULL,
    CONSTRAINT fk_tasks_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_tasks_work_item FOREIGN KEY (work_item_id) REFERENCES work_items(id) ON DELETE SET NULL,
    INDEX idx_tasks_owner_date (owner_id, planned_date, status, deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS progress_records (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    work_item_id CHAR(36) NOT NULL,
    summary VARCHAR(1000) NOT NULL,
    blocker VARCHAR(1000) NOT NULL DEFAULT '',
    next_action VARCHAR(500) NOT NULL DEFAULT '',
    progress_before INT NOT NULL,
    progress_after INT NOT NULL,
    spent_minutes INT NOT NULL DEFAULT 0,
    row_version BIGINT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    deleted_at DATETIME(6) NULL,
    CONSTRAINT fk_progress_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_progress_item FOREIGN KEY (work_item_id) REFERENCES work_items(id) ON DELETE CASCADE,
    INDEX idx_progress_item_time (work_item_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS change_requests (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    project_id CHAR(36) NOT NULL,
    source_idea_id CHAR(36) NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT NOT NULL,
    branch_type VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    target_version VARCHAR(40) NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    deleted_at DATETIME(6) NULL,
    CONSTRAINT fk_changes_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_changes_project FOREIGN KEY (project_id) REFERENCES work_items(id) ON DELETE CASCADE,
    INDEX idx_changes_project (project_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS acceptance_records (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    work_item_id CHAR(36) NOT NULL,
    phase VARCHAR(120) NOT NULL,
    result VARCHAR(24) NOT NULL,
    summary TEXT NOT NULL,
    optimizations_json JSON NOT NULL,
    follow_ups_json JSON NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    deleted_at DATETIME(6) NULL,
    CONSTRAINT fk_acceptance_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_acceptance_item FOREIGN KEY (work_item_id) REFERENCES work_items(id) ON DELETE CASCADE,
    INDEX idx_acceptance_item (work_item_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS change_feed (
    sequence BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    change_id CHAR(36) NOT NULL UNIQUE,
    owner_id CHAR(36) NOT NULL,
    source_device_id CHAR(36) NOT NULL,
    mutation_id CHAR(36) NULL,
    entity_type VARCHAR(48) NOT NULL,
    entity_id CHAR(36) NOT NULL,
    operation VARCHAR(16) NOT NULL,
    row_version BIGINT NOT NULL,
    payload_json JSON NOT NULL,
    changed_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_change_feed_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_change_feed_owner_sequence (owner_id, sequence),
    UNIQUE INDEX uq_change_feed_mutation (owner_id, mutation_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS sync_conflicts (
    id CHAR(36) PRIMARY KEY,
    owner_id CHAR(36) NOT NULL,
    device_id CHAR(36) NOT NULL,
    mutation_id CHAR(36) NOT NULL,
    entity_type VARCHAR(48) NOT NULL,
    entity_id CHAR(36) NOT NULL,
    client_version BIGINT NOT NULL,
    server_version BIGINT NOT NULL,
    client_payload_json JSON NOT NULL,
    server_payload_json JSON NOT NULL,
    reason VARCHAR(500) NOT NULL,
    resolved_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_conflicts_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_conflicts_owner_open (owner_id, resolved_at, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
