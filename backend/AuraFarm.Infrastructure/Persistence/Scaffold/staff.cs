using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class staff
{
    public Guid staff_id { get; set; }

    public Guid? home_location_id { get; set; }

    public Guid? address_id { get; set; }

    public string? first_name { get; set; }

    public string? last_name { get; set; }

    public string? email { get; set; }

    public string? specialization { get; set; }

    public string? username { get; set; }

    public string? password_hash { get; set; }

    public virtual address? address { get; set; }

    public virtual location? home_location { get; set; }

    public virtual ICollection<location> locations { get; set; } = new List<location>();

    public virtual ICollection<session> sessions { get; set; } = new List<session>();
}
