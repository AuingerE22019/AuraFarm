using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MemberEmergencyContactsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<member_emergency_contact>>> GetAll(CancellationToken ct)
        => await db.member_emergency_contacts.AsNoTracking().ToListAsync(ct);

    [HttpGet("{memberId:guid}/{contactId:guid}")]
    public async Task<ActionResult<member_emergency_contact>> GetById(Guid memberId, Guid contactId, CancellationToken ct)
    {
        var entity = await db.member_emergency_contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.member_id == memberId && x.contact_id == contactId, ct);

        return entity is null ? NotFound() : Ok(entity);
    }
}

