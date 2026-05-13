using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class discount
{
    public Guid discount_id { get; set; }

    public string? name { get; set; }

    public string? category { get; set; }

    public decimal? percent_off { get; set; }

    public decimal? fixed_off { get; set; }

    public bool? is_active { get; set; }

    public virtual ICollection<contract> contracts { get; set; } = new List<contract>();
}
