using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class member
{
    public Guid member_id { get; set; }

    public Guid? home_location_id { get; set; }

    public Guid? address_id { get; set; }

    public string? first_name { get; set; }

    public string? last_name { get; set; }

    public string? email { get; set; }

    public bool? is_verified_student { get; set; }

    public DateOnly? date_of_birth { get; set; }

    public DateTime? registration_date { get; set; }

    public string? phone { get; set; }

    public string? username { get; set; }

    public string? password_hash { get; set; }

    public virtual address? address { get; set; }

    public virtual ICollection<booking> bookings { get; set; } = new List<booking>();

    public virtual ICollection<contract> contracts { get; set; } = new List<contract>();

    public virtual location? home_location { get; set; }

    public virtual ICollection<member_emergency_contact> member_emergency_contacts { get; set; } = new List<member_emergency_contact>();
}
