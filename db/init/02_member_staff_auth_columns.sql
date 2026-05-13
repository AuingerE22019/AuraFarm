ALTER TABLE IF EXISTS Staff
  ADD COLUMN IF NOT EXISTS username VARCHAR(50) UNIQUE,
  ADD COLUMN IF NOT EXISTS password_hash TEXT;

ALTER TABLE IF EXISTS Members
  ADD COLUMN IF NOT EXISTS username VARCHAR(50) UNIQUE,
  ADD COLUMN IF NOT EXISTS password_hash TEXT;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint
    WHERE conname = 'staff_username_format_chk'
  ) THEN
    ALTER TABLE Staff
      ADD CONSTRAINT staff_username_format_chk
      CHECK (username IS NULL OR (char_length(username) >= 3 AND username !~ '\\s'));
  END IF;

  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint
    WHERE conname = 'members_username_format_chk'
  ) THEN
    ALTER TABLE Members
      ADD CONSTRAINT members_username_format_chk
      CHECK (username IS NULL OR (char_length(username) >= 3 AND username !~ '\\s'));
  END IF;
END $$;

