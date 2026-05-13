using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MembershipTiersController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<membership_tier>>> GetAll(CancellationToken ct)
        => await db.membership_tiers.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<membership_tier>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.membership_tiers.AsNoTracking().FirstOrDefaultAsync(x => x.tier_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

