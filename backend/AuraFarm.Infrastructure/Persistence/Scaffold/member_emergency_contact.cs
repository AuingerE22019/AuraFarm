using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class member_emergency_contact
{
    public Guid member_id { get; set; }

    public Guid contact_id { get; set; }

    public string? relation { get; set; }

    public int? priority { get; set; }

    public virtual emergency_contact contact { get; set; } = null!;

    public virtual member member { get; set; } = null!;
}
