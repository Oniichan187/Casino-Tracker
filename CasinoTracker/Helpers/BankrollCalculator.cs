using CasinoTracker.Models;

namespace CasinoTracker.Helpers;

/// <summary>Bankroll (BR) = Money (MO) + Chips (CH).</summary>
public readonly record struct BankrollSnapshot(decimal Money, decimal Chips)
{
    public decimal Bankroll => Money + Chips;
}

public sealed record BankrollPoint(long Timestamp, decimal Money, decimal Chips, string Label)
{
    public decimal Bankroll => Money + Chips;
}

/// <summary>
/// Pure bankroll logic, independent of UI and database:
///  * CH = everything paid in + everything won - everything paid out.
///  * MO = everything paid out. A later buy-in is subtracted from MO first; if the buy-in is larger
///    than MO, MO becomes 0 while CH still grows by the full buy-in (the rest comes from an untracked wallet).
///  * BR = MO + CH.
/// </summary>
public static class BankrollCalculator
{
    public static BankrollSnapshot ApplyExchange(BankrollSnapshot state, decimal amount)
    {
        if (amount >= 0)
        {
            // Money -> chips
            return new BankrollSnapshot(Math.Max(0m, state.Money - amount), state.Chips + amount);
        }

        // Chips -> money (amount is negative)
        return new BankrollSnapshot(state.Money - amount, state.Chips + amount);
    }

    public static BankrollSnapshot ApplyGameResult(BankrollSnapshot state, decimal amount) =>
        new(state.Money, state.Chips + amount);

    public static BankrollSnapshot Compute(IEnumerable<Exchange> exchanges, IEnumerable<PlayedGame> games)
    {
        var timeline = Timeline(exchanges, games);
        return timeline.Count == 0 ? default : new BankrollSnapshot(timeline[^1].Money, timeline[^1].Chips);
    }

    /// <summary>
    /// Replays all exchanges and finished games in chronological order and returns
    /// the state after each event.
    /// </summary>
    public static List<BankrollPoint> Timeline(IEnumerable<Exchange> exchanges, IEnumerable<PlayedGame> games)
    {
        var events = new List<(long Ts, int Order, decimal Amount, bool IsExchange, string Label)>();

        foreach (var e in exchanges)
            events.Add((e.Timestamp, 0, e.Amount, true, e.Amount >= 0 ? "Buy-in" : "Cash-out"));

        foreach (var g in games)
        {
            if (g.Endtime is null) continue; // still running, result unknown
            events.Add((g.Endtime.Value, 1, g.Amount, false, g.Amount >= 0 ? "Won" : "Lost"));
        }

        var state = new BankrollSnapshot(0m, 0m);
        var result = new List<BankrollPoint>(events.Count);
        foreach (var ev in events.OrderBy(e => e.Ts).ThenBy(e => e.Order))
        {
            state = ev.IsExchange ? ApplyExchange(state, ev.Amount) : ApplyGameResult(state, ev.Amount);
            result.Add(new BankrollPoint(ev.Ts, state.Money, state.Chips, ev.Label));
        }
        return result;
    }
}
