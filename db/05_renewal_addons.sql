-- Planned add-ons after current contract term (renewal)
CREATE TABLE IF NOT EXISTS Contract_Renewal_Addons (
    contract_id UUID REFERENCES Contracts(contract_id) ON DELETE CASCADE,
    addon_price_id UUID REFERENCES Addon_Prices(addon_price_id) ON DELETE RESTRICT,
    PRIMARY KEY (contract_id, addon_price_id)
);
