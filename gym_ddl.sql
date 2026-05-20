CREATE TYPE user_role AS ENUM ('trainer', 'manager', 'admin', 'cleaner', 'receptionist');
CREATE TYPE floor_material AS ENUM ('rubber', 'wood', 'turf', 'mats', 'concrete');
CREATE TYPE tier_cycle AS ENUM ('monthly', 'annually');
CREATE TYPE access_level AS ENUM ('home_only', 'national', 'global');
CREATE TYPE contract_status AS ENUM ('active', 'paused', 'terminated', 'pending');
CREATE TYPE booking_status AS ENUM ('confirmed', 'cancelled', 'attended', 'no_show');
CREATE TYPE equipment_status AS ENUM ('operational', 'under_repair', 'retired', 'broken');
CREATE TYPE difficulty_level AS ENUM ('beginner', 'intermediate', 'advanced', 'pro');
CREATE TYPE payment_method AS ENUM ('card', 'transfer', 'cash', 'direct_debit');
CREATE TYPE payment_status AS ENUM ('paid', 'failed', 'refunded');

CREATE TABLE Addresses (
    address_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    street VARCHAR(150) NOT NULL,
    house_number VARCHAR(20),
    addition TEXT,
    zip_code VARCHAR(20) NOT NULL,
    city VARCHAR(100) NOT NULL,
    state_province VARCHAR(100),
    country_iso CHAR(2) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Membership_Tiers (
    tier_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tier_name VARCHAR(50) UNIQUE,
    access_level access_level,
    has_sauna BOOLEAN,
    has_solarium BOOLEAN,
    has_drinks BOOLEAN,
    has_coffee BOOLEAN
);

CREATE TABLE Discounts (
    discount_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50),
    category VARCHAR(20),
    percent_off DECIMAL(5,2),
    fixed_off DECIMAL(10,2),
    is_active BOOLEAN
);

CREATE TABLE Emergency_Contacts (
    contact_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    first_name VARCHAR(50),
    last_name VARCHAR(50),
    phone_number VARCHAR(20),
    email VARCHAR(100) NULL
);

CREATE TABLE Classes (
    class_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(100),
    description TEXT,
    difficulty difficulty_level
);

CREATE TABLE Locations (
    location_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    address_id UUID REFERENCES Addresses(address_id) ON DELETE SET NULL,
    manager_id UUID,
    name VARCHAR(100),
    country_iso CHAR(2),
    city VARCHAR(100),
    timezone VARCHAR(50),
    is_active BOOLEAN
);

CREATE TABLE Staff (
    staff_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    home_location_id UUID REFERENCES Locations(location_id),
    address_id UUID REFERENCES Addresses(address_id) ON DELETE SET NULL,
    first_name VARCHAR(50),
    last_name VARCHAR(50),
    username VARCHAR(50) UNIQUE,
    password_hash TEXT,
    email VARCHAR(100) UNIQUE,
    role user_role,
    specialization VARCHAR(100),
    CHECK (username IS NULL OR (char_length(username) >= 3 AND username !~ '\\s')),
    CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$')
);

ALTER TABLE Locations ADD CONSTRAINT fk_manager 
FOREIGN KEY (manager_id) REFERENCES Staff(staff_id) ON DELETE SET NULL;

CREATE TABLE Rooms (
    room_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id UUID REFERENCES Locations(location_id) ON DELETE CASCADE,
    room_name VARCHAR(50),
    max_occupancy INT,
    floor_type floor_material,
    has_ac BOOLEAN,
    has_sound_system BOOLEAN
);

CREATE TABLE Equipment (
    equipment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id UUID REFERENCES Rooms(room_id),
    brand_model VARCHAR(100),
    serial_number VARCHAR(50) UNIQUE,
    purchase_date DATE,
    last_maintenance DATE,
    next_maintenance DATE,
    status equipment_status,
    UNIQUE (serial_number)
);

CREATE TABLE Members (
    member_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    home_location_id UUID REFERENCES Locations(location_id),
    address_id UUID REFERENCES Addresses(address_id) ON DELETE SET NULL,
    first_name VARCHAR(50),
    last_name VARCHAR(50),
    username VARCHAR(50) UNIQUE,
    password_hash TEXT,
    email VARCHAR(100) UNIQUE,
    is_verified_student BOOLEAN,
    date_of_birth DATE,
    registration_date TIMESTAMPTZ,
    phone VARCHAR(20),
    recruited_by UUID REFERENCES Staff(staff_id) ON DELETE SET NULL,
    CHECK (username IS NULL OR (char_length(username) >= 3 AND username !~ '\\s')),
    CHECK (date_of_birth < CURRENT_DATE - INTERVAL '14 years'),
    CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$')
);

CREATE TABLE Member_Emergency_Contacts (
    member_id UUID REFERENCES Members(member_id) ON DELETE CASCADE,
    contact_id UUID REFERENCES Emergency_Contacts(contact_id),
    relation VARCHAR(50),
    priority INT,
    PRIMARY KEY (member_id, contact_id)
);

CREATE TABLE Tier_Prices (
    price_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tier_id UUID REFERENCES Membership_Tiers(tier_id),
    billing_cycle tier_cycle,
    monthly_amount DECIMAL(10, 2),
    currency CHAR(3) DEFAULT 'EUR',
    end_date DATE NULL,
    CHECK (monthly_amount > 0)
);

-- Add-on packages (optional extras; can represent combos as well)
CREATE TABLE Addon_Packages (
    addon_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    addon_name VARCHAR(80) UNIQUE NOT NULL,
    includes_sauna BOOLEAN DEFAULT FALSE,
    includes_solarium BOOLEAN DEFAULT FALSE,
    includes_drinks BOOLEAN DEFAULT FALSE,
    includes_coffee BOOLEAN DEFAULT FALSE,
    is_combo BOOLEAN DEFAULT FALSE
);

CREATE TABLE Addon_Prices (
    addon_price_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    addon_id UUID REFERENCES Addon_Packages(addon_id) ON DELETE CASCADE,
    billing_cycle tier_cycle NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    currency CHAR(3) DEFAULT 'EUR',
    CHECK (amount > 0)
);

CREATE TABLE Contracts (
    contract_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id UUID REFERENCES Members(member_id) ON DELETE RESTRICT,
    price_id UUID REFERENCES Tier_Prices(price_id),
    discount_id UUID REFERENCES Discounts(discount_id) NULL,
    start_date DATE,
    end_date DATE NULL,
    final_monthly_rate DECIMAL(10,2),
    currency CHAR(3),
    status contract_status,
    billing_cycle tier_cycle,
    commitment_end_date DATE,
    auto_renew BOOLEAN NOT NULL DEFAULT TRUE,
    renewal_price_id UUID REFERENCES Tier_Prices(price_id),
    renewal_updated_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    pause_effective_date DATE,
    resume_effective_date DATE,
    CHECK (end_date IS NULL OR end_date > start_date),
    CHECK (final_monthly_rate >= 0)
);

CREATE TABLE Contract_Addons (
    contract_id UUID REFERENCES Contracts(contract_id) ON DELETE CASCADE,
    addon_price_id UUID REFERENCES Addon_Prices(addon_price_id) ON DELETE RESTRICT,
    PRIMARY KEY (contract_id, addon_price_id)
);

CREATE TABLE Contract_Renewal_Addons (
    contract_id UUID REFERENCES Contracts(contract_id) ON DELETE CASCADE,
    addon_price_id UUID REFERENCES Addon_Prices(addon_price_id) ON DELETE RESTRICT,
    PRIMARY KEY (contract_id, addon_price_id)
);

CREATE TABLE Payments (
    payment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_id UUID REFERENCES Contracts(contract_id) ON DELETE RESTRICT,
    amount DECIMAL(10,2),
    payment_date TIMESTAMPTZ,
    billing_period_start DATE,
    billing_period_end DATE,
    method payment_method,
    status payment_status,
    CHECK (amount > 0)
);

CREATE TABLE Sessions (
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    class_id UUID REFERENCES Classes(class_id) ON DELETE RESTRICT,
    location_id UUID REFERENCES Locations(location_id),
    room_id UUID REFERENCES Rooms(room_id),
    trainer_id UUID REFERENCES Staff(staff_id),
    start_time TIMESTAMPTZ,
    end_time TIMESTAMPTZ,
    max_participants INT,
    is_cancelled BOOLEAN DEFAULT FALSE,
    CHECK (end_time > start_time)
);

CREATE TABLE Bookings (
    booking_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id UUID REFERENCES Members(member_id),
    session_id UUID REFERENCES Sessions(session_id),
    booked_at TIMESTAMPTZ,
    status booking_status,
    UNIQUE (member_id, session_id)
);

-- Initiale Daten
-- Countries: Austria (AT) + Germany (DE) with a few city locations (mock data).
-- Each location gets 1 room, except Schärding (AT) which gets 3 rooms.
-- In Schärding, 2 rooms have 5 devices each: Cardio + Strength (incl. cable machine).

-- Addresses
INSERT INTO Addresses (street, house_number, zip_code, city, country_iso)
VALUES
  ('Ringstraße', '1', '1010', 'Wien', 'AT'),
  ('Mariahilfer Straße', '12', '4020', 'Linz', 'AT'),
  ('Mirabellplatz', '3', '5020', 'Salzburg', 'AT'),
  ('Hauptplatz', '5', '4780', 'Schärding', 'AT'),
  ('Marienplatz', '1', '80331', 'München', 'DE'),
  ('Alexanderplatz', '7', '10178', 'Berlin', 'DE'),
  ('Reeperbahn', '9', '20359', 'Hamburg', 'DE');

-- Locations (name + city)
INSERT INTO Locations (name, address_id, country_iso, city, timezone, is_active)
VALUES
  ('AuraFarm Wien', (SELECT address_id FROM Addresses WHERE city = 'Wien' AND country_iso = 'AT' LIMIT 1), 'AT', 'Wien', 'Europe/Vienna', TRUE),
  ('AuraFarm Linz', (SELECT address_id FROM Addresses WHERE city = 'Linz' AND country_iso = 'AT' LIMIT 1), 'AT', 'Linz', 'Europe/Vienna', TRUE),
  ('AuraFarm Salzburg', (SELECT address_id FROM Addresses WHERE city = 'Salzburg' AND country_iso = 'AT' LIMIT 1), 'AT', 'Salzburg', 'Europe/Vienna', TRUE),
  ('AuraFarm Schärding', (SELECT address_id FROM Addresses WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1), 'AT', 'Schärding', 'Europe/Vienna', TRUE),
  ('AuraFarm München', (SELECT address_id FROM Addresses WHERE city = 'München' AND country_iso = 'DE' LIMIT 1), 'DE', 'München', 'Europe/Berlin', TRUE),
  ('AuraFarm Berlin', (SELECT address_id FROM Addresses WHERE city = 'Berlin' AND country_iso = 'DE' LIMIT 1), 'DE', 'Berlin', 'Europe/Berlin', TRUE),
  ('AuraFarm Hamburg', (SELECT address_id FROM Addresses WHERE city = 'Hamburg' AND country_iso = 'DE' LIMIT 1), 'DE', 'Hamburg', 'Europe/Berlin', TRUE);

-- Rooms
-- One room per location (default)
INSERT INTO Rooms (location_id, room_name, max_occupancy, floor_type, has_ac, has_sound_system)
SELECT l.location_id, 'Main Room', 25, 'rubber', TRUE, TRUE
FROM Locations l
WHERE l.city <> 'Schärding';

-- Schärding gets 3 rooms: Cardio, Strength, Yoga
INSERT INTO Rooms (location_id, room_name, max_occupancy, floor_type, has_ac, has_sound_system)
VALUES
  ((SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1), 'Cardio Room', 20, 'rubber', TRUE, TRUE),
  ((SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1), 'Strength Room', 20, 'rubber', TRUE, TRUE),
  ((SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1), 'Yoga Room', 16, 'mats', TRUE, TRUE);

-- Equipment (only for Schärding Cardio + Strength rooms; Yoga has none)
-- Cardio Room: 5 devices
INSERT INTO Equipment (room_id, brand_model, serial_number, purchase_date, last_maintenance, next_maintenance, status)
VALUES
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Cardio Room' LIMIT 1),
    'Treadmill Pro X', 'AT-SCH-CARDIO-001', CURRENT_DATE - INTERVAL '300 days', CURRENT_DATE - INTERVAL '30 days', CURRENT_DATE + INTERVAL '60 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Cardio Room' LIMIT 1),
    'Treadmill Pro X', 'AT-SCH-CARDIO-002', CURRENT_DATE - INTERVAL '280 days', CURRENT_DATE - INTERVAL '40 days', CURRENT_DATE + INTERVAL '50 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Cardio Room' LIMIT 1),
    'Bike Air 2000', 'AT-SCH-CARDIO-003', CURRENT_DATE - INTERVAL '260 days', CURRENT_DATE - INTERVAL '20 days', CURRENT_DATE + INTERVAL '70 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Cardio Room' LIMIT 1),
    'Bike Air 2000', 'AT-SCH-CARDIO-004', CURRENT_DATE - INTERVAL '240 days', CURRENT_DATE - INTERVAL '25 days', CURRENT_DATE + INTERVAL '65 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Cardio Room' LIMIT 1),
    'RowErg R1', 'AT-SCH-CARDIO-005', CURRENT_DATE - INTERVAL '220 days', CURRENT_DATE - INTERVAL '15 days', CURRENT_DATE + INTERVAL '75 days', 'operational');

-- Strength Room: 5 devices (incl. cable machine); models can repeat, serials unique
INSERT INTO Equipment (room_id, brand_model, serial_number, purchase_date, last_maintenance, next_maintenance, status)
VALUES
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Strength Room' LIMIT 1),
    'Power Rack PR-1', 'AT-SCH-STR-001', CURRENT_DATE - INTERVAL '500 days', CURRENT_DATE - INTERVAL '45 days', CURRENT_DATE + INTERVAL '45 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Strength Room' LIMIT 1),
    'Power Rack PR-1', 'AT-SCH-STR-002', CURRENT_DATE - INTERVAL '480 days', CURRENT_DATE - INTERVAL '50 days', CURRENT_DATE + INTERVAL '40 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Strength Room' LIMIT 1),
    'Cable Machine CM-5', 'AT-SCH-STR-003', CURRENT_DATE - INTERVAL '420 days', CURRENT_DATE - INTERVAL '35 days', CURRENT_DATE + INTERVAL '55 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Strength Room' LIMIT 1),
    'Adjustable Bench AB-2', 'AT-SCH-STR-004', CURRENT_DATE - INTERVAL '390 days', CURRENT_DATE - INTERVAL '25 days', CURRENT_DATE + INTERVAL '65 days', 'operational'),
  ((SELECT room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city='Schärding' AND r.room_name='Strength Room' LIMIT 1),
    'Leg Press LP-10', 'AT-SCH-STR-005', CURRENT_DATE - INTERVAL '365 days', CURRENT_DATE - INTERVAL '20 days', CURRENT_DATE + INTERVAL '70 days', 'operational');

-- Membership tiers (3 models)
INSERT INTO Membership_Tiers (tier_name, access_level, has_sauna, has_solarium, has_drinks, has_coffee)
VALUES
  ('Home Membership', 'home_only', FALSE, FALSE, FALSE, FALSE),
  ('National Membership', 'national', FALSE, FALSE, FALSE, FALSE),
  ('Global Membership', 'global', FALSE, FALSE, FALSE, FALSE);

-- Tier prices: monthly_amount is the monthly payment; annually rows store the full-year total (÷12 at checkout).
INSERT INTO Tier_Prices (tier_id, billing_cycle, monthly_amount, currency)
VALUES
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='Home Membership' LIMIT 1), 'monthly', 29.90, 'EUR'),
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='Home Membership' LIMIT 1), 'annually', 299.00, 'EUR'),
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='National Membership' LIMIT 1), 'monthly', 39.90, 'EUR'),
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='National Membership' LIMIT 1), 'annually', 399.00, 'EUR'),
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='Global Membership' LIMIT 1), 'monthly', 49.90, 'EUR'),
  ((SELECT tier_id FROM Membership_Tiers WHERE tier_name='Global Membership' LIMIT 1), 'annually', 499.00, 'EUR');

-- Add-ons (single + combo)
INSERT INTO Addon_Packages (addon_name, includes_sauna, includes_solarium, includes_drinks, includes_coffee, is_combo)
VALUES
  ('Sauna Add-on', TRUE, FALSE, FALSE, FALSE, FALSE),
  ('Solarium Add-on', FALSE, TRUE, FALSE, FALSE, FALSE),
  ('Drinks Add-on', FALSE, FALSE, TRUE, FALSE, FALSE),
  ('Coffee Add-on', FALSE, FALSE, FALSE, TRUE, FALSE),
  ('Wellness Bundle (Sauna + Solarium)', TRUE, TRUE, FALSE, FALSE, TRUE),
  ('All In Bundle (alles inkl.)', TRUE, TRUE, TRUE, TRUE, TRUE);

INSERT INTO Addon_Prices (addon_id, billing_cycle, amount, currency)
VALUES
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Sauna Add-on' LIMIT 1), 'monthly', 9.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Sauna Add-on' LIMIT 1), 'annually', 99.00, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Solarium Add-on' LIMIT 1), 'monthly', 7.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Solarium Add-on' LIMIT 1), 'annually', 79.00, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Drinks Add-on' LIMIT 1), 'monthly', 5.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Drinks Add-on' LIMIT 1), 'annually', 59.00, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Coffee Add-on' LIMIT 1), 'monthly', 3.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Coffee Add-on' LIMIT 1), 'annually', 39.00, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Wellness Bundle (Sauna + Solarium)' LIMIT 1), 'monthly', 14.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='Wellness Bundle (Sauna + Solarium)' LIMIT 1), 'annually', 149.00, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='All In Bundle (alles inkl.)' LIMIT 1), 'monthly', 22.90, 'EUR'),
  ((SELECT addon_id FROM Addon_Packages WHERE addon_name='All In Bundle (alles inkl.)' LIMIT 1), 'annually', 229.00, 'EUR');

-- Admin user: home location Schärding
INSERT INTO Staff (home_location_id, first_name, last_name, username, password_hash, email, role)
VALUES (
  (SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1),
  'Admin',
  'Superuser',
  'Admin',
  '$2a$11$zs4EOxjggQqFrJsFdWoui.Rbt1oC65xPuF9zWLkP2ixv3bFWB/Ste',
  'admin@aurafarm.com',
  'admin'
);
