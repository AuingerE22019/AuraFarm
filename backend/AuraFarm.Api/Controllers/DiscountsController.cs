using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DiscountsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<discount>>> GetAll(CancellationToken ct)
        => await db.discounts.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<discount>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.discounts.AsNoTracking().FirstOrDefaultAsync(x => x.discount_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

