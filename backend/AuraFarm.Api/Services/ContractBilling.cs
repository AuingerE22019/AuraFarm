namespace AuraFarm.Api.Services;

/// <summary>Calendar-month billing (payment day = 1st). Proration for partial first month.</summary>
public static class ContractBilling
{
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    public static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    public static DateOnly EndOfMonth(DateOnly d) =>
        new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

    public static DateOnly FirstOfNextMonth(DateOnly d) => FirstOfMonth(d).AddMonths(1);

    public static DateOnly FirstFullBillingMonth(DateOnly signupDate) =>
        signupDate.Day == 1 ? FirstOfMonth(signupDate) : FirstOfNextMonth(signupDate);

    public static DateOnly InitialCommitmentEnd(DateOnly signupDate, string billingCycle)
    {
        if (billingCycle == "annually")
            return EndOfMonth(FirstFullBillingMonth(signupDate).AddMonths(11));
        return EndOfMonth(signupDate);
    }

    public static decimal ProrateMonthly(decimal monthlyRate, DateOnly from, DateOnly throughInclusive)
    {
        var daysInMonth = DateTime.DaysInMonth(from.Year, from.Month);
        var days = throughInclusive.DayNumber - from.DayNumber + 1;
        if (days <= 0 || daysInMonth <= 0) return 0m;
        return decimal.Round(monthlyRate * days / daysInMonth, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ProratedSignupAmount(decimal monthlyRate, DateOnly signupDate)
    {
        if (signupDate.Day == 1) return monthlyRate;
        return ProrateMonthly(monthlyRate, signupDate, EndOfMonth(signupDate));
    }

    public static DateOnly NextBillingDate(DateOnly today, string status, DateOnly? pauseEffective, DateOnly? resumeEffective)
    {
        if (status == "paused") return resumeEffective ?? FirstOfNextMonth(today);
        if (pauseEffective is { } pe && pe > today) return pe;
        return today.Day == 1 ? today : FirstOfNextMonth(today);
    }

    /// <summary>First day the follow-up contract term applies (day after current commitment ends; aligns to 1st of month).</summary>
    public static DateOnly RenewalEffectiveFrom(DateOnly commitmentEndDate) => commitmentEndDate.AddDays(1);
}
