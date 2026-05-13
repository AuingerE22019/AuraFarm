using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MembersController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<member>>> GetAll(CancellationToken ct)
        => await db.members.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<member>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.members.AsNoTracking().FirstOrDefaultAsync(x => x.member_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

