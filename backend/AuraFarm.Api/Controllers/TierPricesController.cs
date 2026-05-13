using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TierPricesController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<tier_price>>> GetAll(CancellationToken ct)
        => await db.tier_prices.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<tier_price>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.tier_prices.AsNoTracking().FirstOrDefaultAsync(x => x.price_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

