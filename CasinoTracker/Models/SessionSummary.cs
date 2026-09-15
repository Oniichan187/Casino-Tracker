namespace CasinoTracker.Models;

/// <summary>Light-weight row for the sessions list (read model).</summary>
public sealed class SessionSummary
{
    public required Session Session { get; init; }
    public string CasinoName { get; init; } = string.Empty;
    public decimal GameResult { get; init; }
    public decimal BuyIns { get; init; }
    public decimal CashOuts { get; init; }
    public decimal ConsumptionSpend { get; init; }
    public int ConsumedCount { get; init; }
}
