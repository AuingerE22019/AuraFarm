using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ClassesController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<_class>>> GetAll(CancellationToken ct)
        => await db.classes.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<_class>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.classes.AsNoTracking().FirstOrDefaultAsync(x => x.class_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

