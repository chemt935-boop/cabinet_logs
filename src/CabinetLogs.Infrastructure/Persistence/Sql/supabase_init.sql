-- =============================================================================
-- Cabinet Logs Backend — Supabase PostgreSQL Initialization Script
-- =============================================================================
-- You can apply the schema either by running:
--   dotnet ef database update --project src/CabinetLogs.Infrastructure --startup-project src/CabinetLogs.Api
-- OR by running this script directly in the Supabase SQL Editor.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1. Cabinets (auto-created categories from log entries)
CREATE TABLE IF NOT EXISTS public.cabinets (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL,
    normalized_name varchar(150) NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_cabinets_normalized_name
    ON public.cabinets (normalized_name);

-- 2. Client Devices (PC / laptop / mobile Flutter installations)
CREATE TABLE IF NOT EXISTS public.client_devices (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name             varchar(150) NOT NULL,
    platform         varchar(32) NULL,
    installation_key varchar(200) NULL,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_client_devices_installation_key
    ON public.client_devices (installation_key)
    WHERE installation_key IS NOT NULL;

-- 3. Logs (maintenance / issue records)
CREATE TABLE IF NOT EXISTS public.logs (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cabinet_id             uuid NOT NULL,
    issue                  varchar(10000) NOT NULL,
    occurred_at            timestamptz NOT NULL,
    photo_path             varchar(500) NULL,
    client_device_id       uuid NULL,
    created_by_device_name varchar(150) NOT NULL,
    created_at             timestamptz NOT NULL DEFAULT now(),
    updated_at             timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT fk_logs_cabinets_cabinet_id
        FOREIGN KEY (cabinet_id) REFERENCES public.cabinets (id) ON DELETE RESTRICT,
    CONSTRAINT fk_logs_client_devices_client_device_id
        FOREIGN KEY (client_device_id) REFERENCES public.client_devices (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_logs_cabinet_id_occurred_at
    ON public.logs (cabinet_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_logs_occurred_at
    ON public.logs (occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_logs_client_device_id
    ON public.logs (client_device_id);

-- 4. Pending Storage Cleanups (retry queue for failed Supabase Storage deletions)
CREATE TABLE IF NOT EXISTS public.pending_storage_cleanups (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    object_path       varchar(500) NOT NULL,
    attempt_count     integer NOT NULL DEFAULT 0,
    last_error        varchar(1000) NULL,
    created_at        timestamptz NOT NULL DEFAULT now(),
    last_attempted_at timestamptz NULL,
    next_retry_at     timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_pending_storage_cleanups_next_retry_at
    ON public.pending_storage_cleanups (next_retry_at);

-- =============================================================================
-- Row Level Security (RLS)
-- =============================================================================
-- Enable RLS with NO public anon/authenticated policies so Supabase's public
-- PostgREST API cannot accidentally read or mutate these tables directly.
-- The .NET backend connects server-side with privileged PostgreSQL credentials.
-- =============================================================================
ALTER TABLE public.cabinets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.client_devices ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pending_storage_cleanups ENABLE ROW LEVEL SECURITY;

-- =============================================================================
-- Private Storage Bucket (optional SQL creation — or create via Supabase Dashboard)
-- =============================================================================
INSERT INTO storage.buckets (id, name, public)
VALUES ('cabinet-log-photos', 'cabinet-log-photos', false)
ON CONFLICT (id) DO NOTHING;
