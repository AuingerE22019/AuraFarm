using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class membership_tier
{
    public Guid tier_id { get; set; }

    public string? tier_name { get; set; }

    public bool? has_sauna { get; set; }

    public bool? has_solarium { get; set; }

    public bool? has_drinks { get; set; }

    public bool? has_coffee { get; set; }

    public virtual ICollection<tier_price> tier_prices { get; set; } = new List<tier_price>();
}
