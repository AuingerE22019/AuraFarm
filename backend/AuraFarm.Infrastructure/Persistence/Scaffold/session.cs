using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class session
{
    public Guid session_id { get; set; }

    public Guid? class_id { get; set; }

    public Guid? location_id { get; set; }

    public Guid? room_id { get; set; }

    public Guid? trainer_id { get; set; }

    public DateTime? start_time { get; set; }

    public DateTime? end_time { get; set; }

    public int? max_participants { get; set; }

    public bool? is_cancelled { get; set; }

    public virtual _class? _class { get; set; }

    public virtual ICollection<booking> bookings { get; set; } = new List<booking>();

    public virtual location? location { get; set; }

    public virtual room? room { get; set; }

    public virtual staff? trainer { get; set; }
}
