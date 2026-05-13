using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class contract
{
    public Guid contract_id { get; set; }

    public Guid? member_id { get; set; }

    public Guid? price_id { get; set; }

    public Guid? discount_id { get; set; }

    public DateOnly? start_date { get; set; }

    public DateOnly? end_date { get; set; }

    public decimal? final_monthly_rate { get; set; }

    public string? currency { get; set; }

    public virtual discount? discount { get; set; }

    public virtual member? member { get; set; }

    public virtual ICollection<payment> payments { get; set; } = new List<payment>();

    public virtual tier_price? price { get; set; }
}
