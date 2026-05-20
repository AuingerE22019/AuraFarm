using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,manager,receptionist,trainer")]
public sealed class StaffClassesController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connString = configuration.GetConnectionString("AuraFarm")
        ?? throw new InvalidOperationException("No Connection String");

    private Guid? GetStaffId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var id) ? id : null;
    }

    private async Task<Guid?> GetHomeLocationIdAsync(NpgsqlConnection conn, Guid staffId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT home_location_id FROM Staff WHERE staff_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", staffId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is DBNull or null ? null : (Guid?)scalar;
    }

    public sealed record ClassRow(Guid ClassId, string Title, string? Description, string? Difficulty);
    public sealed record RoomOption(Guid RoomId, string? RoomName, int? MaxOccupancy);
    public sealed record TrainerOption(Guid StaffId, string Name, string Role);
    public sealed record MetaDto(Guid LocationId, string? LocationName, List<RoomOption> Rooms, List<TrainerOption> Trainers, List<ClassRow> Classes);

    [HttpGet("meta")]
    public async Task<ActionResult<MetaDto>> GetMeta(CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var locId = await GetHomeLocationIdAsync(conn, staffId.Value, ct);
        if (locId is null) return NotFound(new { message = "Kein Heimat-Standort für Staff." });

        string? locName = null;
        await using (var lc = new NpgsqlCommand("SELECT name FROM Locations WHERE location_id = @lid", conn))
        {
            lc.Parameters.AddWithValue("lid", locId.Value);
            locName = (string?)await lc.ExecuteScalarAsync(ct);
        }

        var rooms = new List<RoomOption>();
        await using (var rc = new NpgsqlCommand(
                       "SELECT room_id, room_name, max_occupancy FROM Rooms WHERE location_id = @lid ORDER BY room_name",
                       conn))
        {
            rc.Parameters.AddWithValue("lid", locId.Value);
            await using var r = await rc.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                rooms.Add(new RoomOption(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetInt32(2)));
        }

        var trainers = new List<TrainerOption>();
        await using (var tc = new NpgsqlCommand(
                       """
                       SELECT staff_id, first_name, last_name, role::text
                       FROM Staff
                       WHERE home_location_id = @lid OR role IN ('admin', 'manager', 'trainer')
                       ORDER BY last_name, first_name
                       """,
                       conn))
        {
            tc.Parameters.AddWithValue("lid", locId.Value);
            await using var r = await tc.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var fn = r.IsDBNull(1) ? "" : r.GetString(1);
                var ln = r.IsDBNull(2) ? "" : r.GetString(2);
                trainers.Add(new TrainerOption(r.GetGuid(0), $"{fn} {ln}".Trim(), r.GetString(3)));
            }
        }

        var classes = await LoadClassesAsync(conn, ct);
        return Ok(new MetaDto(locId.Value, locName, rooms, trainers, classes));
    }

    [HttpGet("classes")]
    public async Task<ActionResult<List<ClassRow>>> ListClasses(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        return Ok(await LoadClassesAsync(conn, ct));
    }

    public sealed record UpsertClassRequest(string Title, string? Description, string Difficulty);

    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] UpsertClassRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Titel ist erforderlich." });
        if (!IsValidDifficulty(req.Difficulty))
            return BadRequest(new { message = "Ungültiger Schwierigkeitsgrad." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO Classes (title, description, difficulty)
            VALUES (@t, @d, @diff::difficulty_level)
            RETURNING class_id
            """,
            conn);
        cmd.Parameters.AddWithValue("t", req.Title.Trim());
        cmd.Parameters.AddWithValue("d", (object?)req.Description?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("diff", req.Difficulty);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return Ok(new { classId = id, message = "Kurs angelegt." });
    }

    [HttpPut("classes/{id:guid}")]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] UpsertClassRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Titel ist erforderlich." });
        if (!IsValidDifficulty(req.Difficulty))
            return BadRequest(new { message = "Ungültiger Schwierigkeitsgrad." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Classes SET title = @t, description = @d, difficulty = @diff::difficulty_level
            WHERE class_id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("t", req.Title.Trim());
        cmd.Parameters.AddWithValue("d", (object?)req.Description?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("diff", req.Difficulty);
        cmd.Parameters.AddWithValue("id", id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound();
        return Ok(new { message = "Kurs aktualisiert." });
    }

    [HttpDelete("classes/{id:guid}")]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using (var check = new NpgsqlCommand("SELECT 1 FROM Sessions WHERE class_id = @id LIMIT 1", conn))
        {
            check.Parameters.AddWithValue("id", id);
            if (await check.ExecuteScalarAsync(ct) is not null)
                return BadRequest(new { message = "Kurs hat noch Termine und kann nicht gelöscht werden." });
        }
        await using var cmd = new NpgsqlCommand("DELETE FROM Classes WHERE class_id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound();
        return Ok(new { message = "Kurs gelöscht." });
    }

    public sealed record SessionRow(
        Guid SessionId,
        Guid ClassId,
        string ClassTitle,
        string? Difficulty,
        DateTime StartTime,
        DateTime EndTime,
        Guid LocationId,
        string? LocationName,
        Guid RoomId,
        string? RoomName,
        Guid TrainerId,
        string? TrainerName,
        int MaxParticipants,
        int BookedCount,
        bool IsCancelled);

    [HttpGet("sessions")]
    public async Task<ActionResult<List<SessionRow>>> ListSessions([FromQuery] bool upcomingOnly = true, CancellationToken ct = default)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var locId = await GetHomeLocationIdAsync(conn, staffId.Value, ct);
        if (locId is null) return NotFound(new { message = "Kein Heimat-Standort." });

        var list = new List<SessionRow>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT s.session_id, s.class_id, c.title, c.difficulty::text,
                   s.start_time, s.end_time, s.location_id, l.name, s.room_id, r.room_name,
                   s.trainer_id, st.first_name, st.last_name,
                   s.max_participants, s.is_cancelled,
                   (SELECT COUNT(*)::int FROM Bookings b
                    WHERE b.session_id = s.session_id AND b.status = 'confirmed')
            FROM Sessions s
            JOIN Classes c ON c.class_id = s.class_id
            JOIN Locations l ON l.location_id = s.location_id
            JOIN Rooms r ON r.room_id = s.room_id
            JOIN Staff st ON st.staff_id = s.trainer_id
            WHERE s.location_id = @lid
              AND (@upcoming = FALSE OR s.end_time >= NOW())
            ORDER BY s.start_time
            """,
            conn);
        cmd.Parameters.AddWithValue("lid", locId.Value);
        cmd.Parameters.AddWithValue("upcoming", upcomingOnly);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fn = reader.IsDBNull(11) ? "" : reader.GetString(11);
            var ln = reader.IsDBNull(12) ? "" : reader.GetString(12);
            list.Add(new SessionRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetDateTime(4),
                reader.GetDateTime(5),
                reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetGuid(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetGuid(10),
                $"{fn} {ln}".Trim(),
                reader.GetInt32(13),
                reader.GetInt32(15),
                reader.GetBoolean(14)));
        }
        return Ok(list);
    }

    public sealed record UpsertSessionRequest(
        Guid ClassId,
        Guid RoomId,
        Guid TrainerId,
        DateTime StartTime,
        DateTime EndTime,
        int MaxParticipants);

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] UpsertSessionRequest req, CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();
        if (req.EndTime <= req.StartTime)
            return BadRequest(new { message = "Ende muss nach Start liegen." });
        if (req.MaxParticipants < 1)
            return BadRequest(new { message = "Mindestens 1 Teilnehmerplatz." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var locId = await GetHomeLocationIdAsync(conn, staffId.Value, ct);
        if (locId is null) return NotFound(new { message = "Kein Heimat-Standort." });

        var err = await ValidateSessionRefsAsync(conn, locId.Value, req, ct);
        if (err is not null) return BadRequest(new { message = err });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO Sessions (class_id, location_id, room_id, trainer_id, start_time, end_time, max_participants)
            VALUES (@cid, @lid, @rid, @tid, @start, @end, @max)
            RETURNING session_id
            """,
            conn);
        cmd.Parameters.AddWithValue("cid", req.ClassId);
        cmd.Parameters.AddWithValue("lid", locId.Value);
        cmd.Parameters.AddWithValue("rid", req.RoomId);
        cmd.Parameters.AddWithValue("tid", req.TrainerId);
        cmd.Parameters.AddWithValue("start", req.StartTime);
        cmd.Parameters.AddWithValue("end", req.EndTime);
        cmd.Parameters.AddWithValue("max", req.MaxParticipants);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return Ok(new { sessionId = id, message = "Termin angelegt." });
    }

    [HttpPut("sessions/{id:guid}")]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpsertSessionRequest req, CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();
        if (req.EndTime <= req.StartTime)
            return BadRequest(new { message = "Ende muss nach Start liegen." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var locId = await GetHomeLocationIdAsync(conn, staffId.Value, ct);
        if (locId is null) return NotFound();

        await using (var own = new NpgsqlCommand("SELECT location_id FROM Sessions WHERE session_id = @id", conn))
        {
            own.Parameters.AddWithValue("id", id);
            var scalar = await own.ExecuteScalarAsync(ct);
            if (scalar is null or DBNull) return NotFound();
            if ((Guid)scalar != locId.Value) return Forbid();
        }

        var err = await ValidateSessionRefsAsync(conn, locId.Value, req, ct);
        if (err is not null) return BadRequest(new { message = err });

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Sessions
            SET class_id = @cid, room_id = @rid, trainer_id = @tid,
                start_time = @start, end_time = @end, max_participants = @max
            WHERE session_id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("cid", req.ClassId);
        cmd.Parameters.AddWithValue("rid", req.RoomId);
        cmd.Parameters.AddWithValue("tid", req.TrainerId);
        cmd.Parameters.AddWithValue("start", req.StartTime);
        cmd.Parameters.AddWithValue("end", req.EndTime);
        cmd.Parameters.AddWithValue("max", req.MaxParticipants);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { message = "Termin aktualisiert." });
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var locId = await GetHomeLocationIdAsync(conn, staffId.Value, ct);
        if (locId is null) return NotFound();

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand(
                           """
                           UPDATE Sessions SET is_cancelled = TRUE
                           WHERE session_id = @id AND location_id = @lid
                           """,
                           conn, tx))
            {
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("lid", locId.Value);
                if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                {
                    await tx.RollbackAsync(ct);
                    return NotFound();
                }
            }
            await using (var bc = new NpgsqlCommand(
                           """
                           UPDATE Bookings SET status = 'cancelled'
                           WHERE session_id = @id AND status = 'confirmed'
                           """,
                           conn, tx))
            {
                bc.Parameters.AddWithValue("id", id);
                await bc.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new { message = "Termin abgesagt." });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<List<ClassRow>> LoadClassesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<ClassRow>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT DISTINCT ON (LOWER(TRIM(title)))
                   class_id, title, description, difficulty::text
            FROM Classes
            ORDER BY LOWER(TRIM(title)), class_id
            """,
            conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ClassRow(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)));
        return list;
    }

    private static async Task<string?> ValidateSessionRefsAsync(
        NpgsqlConnection conn, Guid locationId, UpsertSessionRequest req, CancellationToken ct)
    {
        await using (var c = new NpgsqlCommand("SELECT 1 FROM Classes WHERE class_id = @id", conn))
        {
            c.Parameters.AddWithValue("id", req.ClassId);
            if (await c.ExecuteScalarAsync(ct) is null) return "Kurs nicht gefunden.";
        }
        await using (var r = new NpgsqlCommand(
                       "SELECT 1 FROM Rooms WHERE room_id = @id AND location_id = @lid", conn))
        {
            r.Parameters.AddWithValue("id", req.RoomId);
            r.Parameters.AddWithValue("lid", locationId);
            if (await r.ExecuteScalarAsync(ct) is null) return "Raum gehört nicht zum Standort.";
        }
        await using (var t = new NpgsqlCommand("SELECT 1 FROM Staff WHERE staff_id = @id", conn))
        {
            t.Parameters.AddWithValue("id", req.TrainerId);
            if (await t.ExecuteScalarAsync(ct) is null) return "Trainer nicht gefunden.";
        }
        return null;
    }

    private static bool IsValidDifficulty(string d) =>
        d is "beginner" or "intermediate" or "advanced" or "pro";
}
