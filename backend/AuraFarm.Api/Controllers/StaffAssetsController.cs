using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace AuraFarm.Api.Controllers;

/// <summary>Staff view of their home location assets (rooms + equipment) and basic operations.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,manager,receptionist,trainer,cleaner")]
public sealed class StaffAssetsController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connString = configuration.GetConnectionString("AuraFarm")
        ?? throw new InvalidOperationException("No Connection String");

    private Guid? GetStaffId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var id) ? id : null;
    }

    public sealed record EquipmentDto(
        Guid EquipmentId,
        Guid RoomId,
        string? BrandModel,
        string? SerialNumber,
        string? Status,
        string? LastMaintenance,
        string? NextMaintenance);

    public sealed record RoomDto(
        Guid RoomId,
        string? RoomName,
        int? MaxOccupancy,
        string? FloorType,
        bool? HasAc,
        bool? HasSoundSystem,
        List<EquipmentDto> Equipment);

    public sealed record LocationAssetsDto(
        Guid LocationId,
        string? Name,
        string? CountryIso,
        string? City,
        List<RoomDto> Rooms);

    [HttpGet("home")]
    public async Task<ActionResult<LocationAssetsDto>> GetHomeAssets(CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        Guid? homeLocationId;
        await using (var staffCmd = new NpgsqlCommand("SELECT home_location_id FROM Staff WHERE staff_id = @sid", conn))
        {
            staffCmd.Parameters.AddWithValue("sid", NpgsqlDbType.Uuid, staffId.Value);
            var scalar = await staffCmd.ExecuteScalarAsync(ct);
            homeLocationId = scalar is DBNull or null ? null : (Guid?)scalar;
        }

        if (homeLocationId is null) return NotFound(new { message = "No home location assigned for staff user." });

        string? locName = null;
        string? locIso = null;
        string? locCity = null;
        await using (var locCmd = new NpgsqlCommand(
                       "SELECT location_id, name, country_iso, city FROM Locations WHERE location_id = @lid",
                       conn))
        {
            locCmd.Parameters.AddWithValue("lid", NpgsqlDbType.Uuid, homeLocationId.Value);
            await using var r = await locCmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return NotFound(new { message = "Home location not found." });
            var locId = r.GetGuid(0);
            locName = r.IsDBNull(1) ? null : r.GetString(1);
            locIso = r.IsDBNull(2) ? null : r.GetString(2);
            locCity = r.IsDBNull(3) ? null : r.GetString(3);
            homeLocationId = locId;
        }

        var rooms = new Dictionary<Guid, RoomDto>();
        await using (var cmd = new NpgsqlCommand(
                       """
                       SELECT
                           rm.room_id,
                           rm.room_name,
                           rm.max_occupancy,
                           rm.floor_type::text,
                           rm.has_ac,
                           rm.has_sound_system,
                           e.equipment_id,
                           e.brand_model,
                           e.serial_number,
                           e.status::text,
                           e.last_maintenance,
                           e.next_maintenance
                       FROM Rooms rm
                       LEFT JOIN Equipment e ON e.room_id = rm.room_id
                       WHERE rm.location_id = @lid
                       ORDER BY rm.room_name, e.serial_number
                       """,
                       conn))
        {
            cmd.Parameters.AddWithValue("lid", NpgsqlDbType.Uuid, homeLocationId.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var roomId = reader.GetGuid(0);
                if (!rooms.TryGetValue(roomId, out var room))
                {
                    room = new RoomDto(
                        roomId,
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.IsDBNull(4) ? (bool?)null : reader.GetBoolean(4),
                        reader.IsDBNull(5) ? (bool?)null : reader.GetBoolean(5),
                        new List<EquipmentDto>());
                    rooms.Add(roomId, room);
                }

                if (!reader.IsDBNull(6))
                {
                    var eqId = reader.GetGuid(6);
                    var lastMaint = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10).ToString("yyyy-MM-dd");
                    var nextMaint = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11).ToString("yyyy-MM-dd");
                    room.Equipment.Add(new EquipmentDto(
                        eqId,
                        roomId,
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        reader.IsDBNull(8) ? null : reader.GetString(8),
                        reader.IsDBNull(9) ? null : reader.GetString(9),
                        lastMaint,
                        nextMaint));
                }
            }
        }

        return Ok(new LocationAssetsDto(homeLocationId.Value, locName, locIso, locCity, rooms.Values.ToList()));
    }

    public sealed record UpdateEquipmentStatusRequest(string Status);

    [HttpPatch("equipment/{equipmentId:guid}/status")]
    public async Task<IActionResult> UpdateEquipmentStatus(Guid equipmentId, [FromBody] UpdateEquipmentStatusRequest req, CancellationToken ct)
    {
        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        var status = (req.Status ?? "").Trim().ToLowerInvariant();
        var allowed = new HashSet<string> { "operational", "under_repair", "broken", "retired" };
        if (!allowed.Contains(status))
            return BadRequest(new { message = "Invalid status. Allowed: operational, under_repair, broken, retired." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        // Ensure equipment belongs to staff's home location.
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Equipment e
            SET status = @status::equipment_status
            WHERE e.equipment_id = @eid
              AND EXISTS (
                SELECT 1
                FROM Rooms rm
                JOIN Locations l ON l.location_id = rm.location_id
                JOIN Staff s ON s.home_location_id = l.location_id
                WHERE rm.room_id = e.room_id
                  AND s.staff_id = @sid
              )
            """,
            conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("eid", NpgsqlDbType.Uuid, equipmentId);
        cmd.Parameters.AddWithValue("sid", NpgsqlDbType.Uuid, staffId.Value);

        var n = await cmd.ExecuteNonQueryAsync(ct);
        if (n != 1) return NotFound(new { message = "Equipment not found for your home location." });

        return Ok(new { equipmentId, status });
    }
}

