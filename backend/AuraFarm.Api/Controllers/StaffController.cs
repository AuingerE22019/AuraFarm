using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StaffController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<staff>>> GetAll(CancellationToken ct)
        => await db.staff.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<staff>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.staff.AsNoTracking().FirstOrDefaultAsync(x => x.staff_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

