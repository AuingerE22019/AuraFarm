using AuraFarm.Infrastructure.Persistence.Scaffold;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BookingsController(AuraFarmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<booking>>> GetAll(CancellationToken ct)
        => await db.bookings.AsNoTracking().ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<booking>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.bookings.AsNoTracking().FirstOrDefaultAsync(x => x.booking_id == id, ct);
        return entity is null ? NotFound() : Ok(entity);
    }
}

