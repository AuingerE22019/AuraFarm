using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class tier_price
{
    public Guid price_id { get; set; }

    public Guid? tier_id { get; set; }

    public decimal? monthly_amount { get; set; }

    public string? currency { get; set; }

    public DateOnly? end_date { get; set; }

    public virtual ICollection<contract> contracts { get; set; } = new List<contract>();

    public virtual membership_tier? tier { get; set; }
}
