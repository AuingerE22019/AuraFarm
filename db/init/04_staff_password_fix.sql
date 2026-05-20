-- Fix Staff demo login: bcrypt hash for plain password "password" (username Admin).
-- Apply on existing DB: docker exec -i aurafarm-db psql -U aurafarm -d aurafarm < db/init/04_staff_password_fix.sql

UPDATE Staff
SET password_hash = '$2a$11$zs4EOxjggQqFrJsFdWoui.Rbt1oC65xPuF9zWLkP2ixv3bFWB/Ste'
WHERE username = 'Admin';
