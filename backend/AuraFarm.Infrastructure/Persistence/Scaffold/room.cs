using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class room
{
    public Guid room_id { get; set; }

    public Guid? location_id { get; set; }

    public string? room_name { get; set; }

    public int? max_occupancy { get; set; }

    public bool? has_ac { get; set; }

    public bool? has_sound_system { get; set; }

    public virtual ICollection<equipment> equipment { get; set; } = new List<equipment>();

    public virtual location? location { get; set; }

    public virtual ICollection<session> sessions { get; set; } = new List<session>();
}
