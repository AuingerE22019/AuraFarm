using System;
using System.Collections.Generic;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class payment
{
    public Guid payment_id { get; set; }

    public Guid? contract_id { get; set; }

    public decimal? amount { get; set; }

    public DateTime? payment_date { get; set; }

    public DateOnly? billing_period_start { get; set; }

    public DateOnly? billing_period_end { get; set; }

    public virtual contract? contract { get; set; }
}
