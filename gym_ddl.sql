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
    CHECK (end_date IS NULL OR end_date > start_date),
    CHECK (final_monthly_rate >= 0)
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