using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SessionsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<session>>> GetAll(CancellationToken ct)
        => await db.sessions.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<session>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.sessions.AsNoTracking().FirstOrDefaultAsync(x => x.session_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

