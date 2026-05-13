using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EquipmentController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<equipment>>> GetAll(CancellationToken ct)
        => await db.equipment.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<equipment>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.equipment.AsNoTracking().FirstOrDefaultAsync(x => x.equipment_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

