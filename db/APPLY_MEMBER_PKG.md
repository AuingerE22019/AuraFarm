# Member registration package (`member_pkg`)

The Docker init scripts run only on **first** database volume creation.

If your Postgres volume already exists and you need the updated `member_pkg.register_new_member` (staff-only minimal registration), apply the SQL manually:

```bash
docker exec -i aurafarm-db psql -U aurafarm -d aurafarm < db/01_packages.sql
```

Or paste the contents of `db/01_packages.sql` into your SQL client.

## Staff PDF / `POST /api/Registration/staff/members` returns 400

The API calls `member_pkg.register_new_member` with the **current** six-parameter signature. If the database still has an older overload or body, PostgreSQL raises an error and the endpoint responds with **400** and a JSON `message`.

After applying `db/01_packages.sql`, you can sanity-check in `psql`:

```sql
SELECT member_pkg.register_new_member(
  (SELECT staff_id FROM staff WHERE username = 'Admin' LIMIT 1),
  'Test', 'User',
  'tempuser_test', '$2a$11$abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJK',
  true
);
```

Expect a **UUID** string on success.
