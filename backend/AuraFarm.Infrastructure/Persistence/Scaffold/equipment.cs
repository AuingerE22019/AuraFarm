using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class equipment
{
    public Guid equipment_id { get; set; }

    public Guid? room_id { get; set; }

    public string? brand_model { get; set; }

    public string? serial_number { get; set; }

    public DateOnly? purchase_date { get; set; }

    public DateOnly? last_maintenance { get; set; }

    public DateOnly? next_maintenance { get; set; }

    public virtual room? room { get; set; }
}
