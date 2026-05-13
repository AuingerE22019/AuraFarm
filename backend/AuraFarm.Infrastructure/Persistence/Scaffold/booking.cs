using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class booking
{
    public Guid booking_id { get; set; }

    public Guid? member_id { get; set; }

    public Guid? session_id { get; set; }

    public DateTime? booked_at { get; set; }

    public virtual member? member { get; set; }

    public virtual session? session { get; set; }
}
