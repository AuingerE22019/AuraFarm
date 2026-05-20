-- Demo classes and upcoming sessions (Schärding)
INSERT INTO Classes (title, description, difficulty)
SELECT v.title, v.description, v.difficulty::difficulty_level
FROM (VALUES
  ('Yoga Flow', 'Ruhiger Yoga-Kurs für Einsteiger und Fortgeschrittene.', 'beginner'),
  ('HIIT Power', 'Intensives Intervalltraining.', 'intermediate'),
  ('Strength Basics', 'Grundlagen Krafttraining mit Trainer.', 'beginner')
) AS v(title, description, difficulty)
WHERE NOT EXISTS (SELECT 1 FROM Classes c WHERE c.title = v.title);

INSERT INTO Sessions (class_id, location_id, room_id, trainer_id, start_time, end_time, max_participants, is_cancelled)
SELECT x.class_id, x.location_id, x.room_id, x.trainer_id, x.start_time, x.end_time, x.max_p, FALSE
FROM (
  SELECT
    (SELECT class_id FROM Classes WHERE title = 'Yoga Flow' LIMIT 1) AS class_id,
    (SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1) AS location_id,
    (SELECT r.room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city = 'Schärding' AND r.room_name = 'Yoga Room' LIMIT 1) AS room_id,
    (SELECT staff_id FROM Staff WHERE username = 'Admin' LIMIT 1) AS trainer_id,
    NOW() + INTERVAL '2 days' AS start_time,
    NOW() + INTERVAL '2 days 1 hour' AS end_time,
    10 AS max_p
  UNION ALL
  SELECT
    (SELECT class_id FROM Classes WHERE title = 'HIIT Power' LIMIT 1),
    (SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1),
    (SELECT r.room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city = 'Schärding' AND r.room_name = 'Cardio Room' LIMIT 1),
    (SELECT staff_id FROM Staff WHERE username = 'Admin' LIMIT 1),
    NOW() + INTERVAL '3 days',
    NOW() + INTERVAL '3 days 1 hour',
    12
  UNION ALL
  SELECT
    (SELECT class_id FROM Classes WHERE title = 'Strength Basics' LIMIT 1),
    (SELECT location_id FROM Locations WHERE city = 'Schärding' AND country_iso = 'AT' LIMIT 1),
    (SELECT r.room_id FROM Rooms r JOIN Locations l ON l.location_id = r.location_id WHERE l.city = 'Schärding' AND r.room_name = 'Strength Room' LIMIT 1),
    (SELECT staff_id FROM Staff WHERE username = 'Admin' LIMIT 1),
    NOW() + INTERVAL '4 days',
    NOW() + INTERVAL '4 days 1 hour',
    8
) AS x
WHERE NOT EXISTS (
  SELECT 1 FROM Sessions s WHERE s.class_id = x.class_id AND s.start_time = x.start_time
);
