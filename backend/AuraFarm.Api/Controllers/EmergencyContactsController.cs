using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmergencyContactsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<emergency_contact>>> GetAll(CancellationToken ct)
        => await db.emergency_contacts.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<emergency_contact>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.emergency_contacts.AsNoTracking().FirstOrDefaultAsync(x => x.contact_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

