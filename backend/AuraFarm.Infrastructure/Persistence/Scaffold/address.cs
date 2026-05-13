using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class address
{
    public Guid address_id { get; set; }

    public string street { get; set; } = null!;

    public string? house_number { get; set; }

    public string? addition { get; set; }

    public string zip_code { get; set; } = null!;

    public string city { get; set; } = null!;

    public string? state_province { get; set; }

    public string country_iso { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public virtual ICollection<location> locations { get; set; } = new List<location>();

    public virtual ICollection<member> members { get; set; } = new List<member>();

    public virtual ICollection<staff> staff { get; set; } = new List<staff>();
}
