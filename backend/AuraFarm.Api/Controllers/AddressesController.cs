using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AddressesController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<address>>> GetAll(CancellationToken ct)
        => await db.addresses.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<address>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.addresses.AsNoTracking().FirstOrDefaultAsync(x => x.address_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

