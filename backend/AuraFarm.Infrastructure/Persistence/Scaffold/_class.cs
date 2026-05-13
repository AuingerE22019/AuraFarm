using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class _class
{
    public Guid class_id { get; set; }

    public string? title { get; set; }

    public string? description { get; set; }

    public virtual ICollection<session> sessions { get; set; } = new List<session>();
}
