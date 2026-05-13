using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class location
{
    public Guid location_id { get; set; }

    public Guid? address_id { get; set; }

    public Guid? manager_id { get; set; }

    public string? name { get; set; }

    public string? country_iso { get; set; }

    public string? city { get; set; }

    public string? timezone { get; set; }

    public bool? is_active { get; set; }

    public virtual address? address { get; set; }

    public virtual staff? manager { get; set; }

    public virtual ICollection<member> members { get; set; } = new List<member>();

    public virtual ICollection<room> rooms { get; set; } = new List<room>();

    public virtual ICollection<session> sessions { get; set; } = new List<session>();

    public virtual ICollection<staff> staff { get; set; } = new List<staff>();
}
