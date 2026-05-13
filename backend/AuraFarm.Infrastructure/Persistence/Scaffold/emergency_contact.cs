using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class emergency_contact
{
    public Guid contact_id { get; set; }

    public string? first_name { get; set; }

    public string? last_name { get; set; }

    public string? phone_number { get; set; }

    public string? email { get; set; }

    public virtual ICollection<member_emergency_contact> member_emergency_contacts { get; set; } = new List<member_emergency_contact>();
}
