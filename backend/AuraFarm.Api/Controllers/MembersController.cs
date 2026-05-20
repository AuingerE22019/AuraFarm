using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MembersController(AuraFarmDbContext db, IConfiguration configuration) : ControllerBase
{
    private readonly string _connString = configuration.GetConnectionString("AuraFarm") ?? throw new InvalidOperationException();

    [HttpGet]
    [Authorize(Roles = "admin,manager,receptionist")]
    public async Task<IActionResult> GetRecruitedMembers()
    {
        var staffIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(staffIdString, out var staffId)) return Unauthorized();

        var list = new List<object>();
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        
        await using var cmd = new NpgsqlCommand("SELECT first_name, last_name, username, registration_date FROM Members WHERE recruited_by = @sid ORDER BY registration_date DESC", conn);
        cmd.Parameters.AddWithValue("sid", staffId);
        
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new {
                firstName = reader.GetString(0),
                lastName = reader.GetString(1),
                username = reader.IsDBNull(2) ? null : reader.GetString(2),
                registrationDate = reader.GetDateTime(3)
            });
        }
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin,manager,receptionist")]
    public async Task<ActionResult<member>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.members.AsNoTracking().FirstOrDefaultAsync(x => x.member_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

