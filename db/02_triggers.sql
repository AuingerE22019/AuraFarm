-- Audit Log für Member Änderungen (Security & Robustheit)
CREATE TABLE IF NOT EXISTS Member_Audit_Log (
    audit_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id UUID,
    action VARCHAR(20),
    changed_by_staff_id UUID,
    change_date TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Trigger Function
CREATE OR REPLACE FUNCTION log_member_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO Member_Audit_Log (member_id, action, changed_by_staff_id)
        VALUES (NEW.member_id, 'INSERT', NEW.recruited_by);
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        -- Wir loggen das Update. (changed_by_staff_id müsste im echten System vom Backend gesetzt werden, wir lassen es hier der Einfachheit halber null oder übernehmen das alte)
        INSERT INTO Member_Audit_Log (member_id, action, changed_by_staff_id)
        VALUES (NEW.member_id, 'UPDATE', NEW.recruited_by);
        RETURN NEW;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Trigger anwenden
DROP TRIGGER IF EXISTS trg_log_member_changes ON Members;
CREATE TRIGGER trg_log_member_changes
AFTER INSERT OR UPDATE ON Members
FOR EACH ROW EXECUTE FUNCTION log_member_changes();
