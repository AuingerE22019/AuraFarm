-- Member registration package (PostgreSQL)
CREATE SCHEMA IF NOT EXISTS member_pkg;

-- Staff creates a member with temp credentials only. Email, DOB, phone, address are completed by the member at first login.
-- Emergency contacts are added only from the member dashboard (not here).
CREATE OR REPLACE FUNCTION member_pkg.register_new_member(
    p_staff_id UUID,
    p_first_name VARCHAR,
    p_last_name VARCHAR,
    p_temp_username VARCHAR,
    p_temp_password_hash VARCHAR,
    p_is_student BOOLEAN DEFAULT FALSE
) RETURNS UUID AS $$
DECLARE
    v_member_id UUID;
BEGIN
    INSERT INTO Members (
        first_name,
        last_name,
        username,
        password_hash,
        email,
        date_of_birth,
        phone,
        registration_date,
        recruited_by,
        is_verified_student
    ) VALUES (
        p_first_name,
        p_last_name,
        p_temp_username,
        p_temp_password_hash,
        NULL,
        NULL,
        NULL,
        CURRENT_TIMESTAMP,
        p_staff_id,
        p_is_student
    )
    RETURNING member_id INTO v_member_id;

    RETURN v_member_id;
EXCEPTION
    WHEN unique_violation THEN
        RAISE EXCEPTION 'Username existiert bereits.';
    WHEN check_violation THEN
        RAISE EXCEPTION 'Ungültige Daten: Bitte Eingaben prüfen.';
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Ein unerwarteter Fehler ist aufgetreten: %', SQLERRM;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
