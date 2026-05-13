using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LocationsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<location>>> GetAll(CancellationToken ct)
        => await db.locations.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<location>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.locations.AsNoTracking().FirstOrDefaultAsync(x => x.location_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

