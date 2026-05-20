-- Merge duplicate class templates (same title) and remove extras
UPDATE Sessions s
SET class_id = x.keep_id
FROM (
    SELECT c.class_id AS dup_id, k.keep_id
    FROM Classes c
    JOIN LATERAL (
        SELECT c2.class_id AS keep_id
        FROM Classes c2
        WHERE LOWER(TRIM(c2.title)) = LOWER(TRIM(c.title))
        ORDER BY c2.class_id
        LIMIT 1
    ) k ON TRUE
    WHERE c.class_id <> k.keep_id
) x
WHERE s.class_id = x.dup_id;

DELETE FROM Classes c
USING (
    SELECT c.class_id
    FROM Classes c
    JOIN LATERAL (
        SELECT c2.class_id AS keep_id
        FROM Classes c2
        WHERE LOWER(TRIM(c2.title)) = LOWER(TRIM(c.title))
        ORDER BY c2.class_id
        LIMIT 1
    ) k ON TRUE
    WHERE c.class_id <> k.keep_id
) doomed
WHERE c.class_id = doomed.class_id;
