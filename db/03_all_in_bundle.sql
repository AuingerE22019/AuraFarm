-- Add All In bundle to existing databases (idempotent)
INSERT INTO Addon_Packages (addon_name, includes_sauna, includes_solarium, includes_drinks, includes_coffee, is_combo)
SELECT 'All In Bundle (alles inkl.)', TRUE, TRUE, TRUE, TRUE, TRUE
WHERE NOT EXISTS (
  SELECT 1 FROM Addon_Packages WHERE addon_name = 'All In Bundle (alles inkl.)'
);

INSERT INTO Addon_Prices (addon_id, billing_cycle, amount, currency)
SELECT a.addon_id, 'monthly', 22.90, 'EUR'
FROM Addon_Packages a
WHERE a.addon_name = 'All In Bundle (alles inkl.)'
  AND NOT EXISTS (
    SELECT 1 FROM Addon_Prices ap
    WHERE ap.addon_id = a.addon_id AND ap.billing_cycle = 'monthly'
  );

INSERT INTO Addon_Prices (addon_id, billing_cycle, amount, currency)
SELECT a.addon_id, 'annually', 229.00, 'EUR'
FROM Addon_Packages a
WHERE a.addon_name = 'All In Bundle (alles inkl.)'
  AND NOT EXISTS (
    SELECT 1 FROM Addon_Prices ap
    WHERE ap.addon_id = a.addon_id AND ap.billing_cycle = 'annually'
  );
