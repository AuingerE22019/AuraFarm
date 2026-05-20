-- Contract lifecycle: month-aligned billing, pause/resume, cancellation, renewal choice
ALTER TABLE Contracts
  ADD COLUMN IF NOT EXISTS billing_cycle tier_cycle,
  ADD COLUMN IF NOT EXISTS commitment_end_date DATE,
  ADD COLUMN IF NOT EXISTS auto_renew BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN IF NOT EXISTS renewal_price_id UUID REFERENCES Tier_Prices(price_id),
  ADD COLUMN IF NOT EXISTS cancelled_at TIMESTAMPTZ,
  ADD COLUMN IF NOT EXISTS pause_effective_date DATE,
  ADD COLUMN IF NOT EXISTS resume_effective_date DATE;

UPDATE Contracts c
SET billing_cycle = tp.billing_cycle,
    commitment_end_date = COALESCE(
      c.commitment_end_date,
      (DATE_TRUNC('month', c.start_date) + INTERVAL '1 month' - INTERVAL '1 day')::date
    ),
    renewal_price_id = COALESCE(c.renewal_price_id, c.price_id),
    auto_renew = COALESCE(c.auto_renew, TRUE)
FROM Tier_Prices tp
WHERE tp.price_id = c.price_id
  AND c.billing_cycle IS NULL;
