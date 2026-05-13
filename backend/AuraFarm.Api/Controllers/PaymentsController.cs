using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<payment>>> GetAll(CancellationToken ct)
        => await db.payments.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<payment>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.payments.AsNoTracking().FirstOrDefaultAsync(x => x.payment_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

