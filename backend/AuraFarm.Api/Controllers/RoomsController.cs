using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RoomsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<room>>> GetAll(CancellationToken ct)
        => await db.rooms.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<room>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.rooms.AsNoTracking().FirstOrDefaultAsync(x => x.room_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

