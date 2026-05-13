using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ContractsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<contract>>> GetAll(CancellationToken ct)
        => await db.contracts.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<contract>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.contracts.AsNoTracking().FirstOrDefaultAsync(x => x.contract_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

