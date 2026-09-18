using CasinoTracker.Helpers;
using CasinoTracker.Models;
using Xunit;

namespace CasinoTracker.Tests;

public class BankrollCalculatorTests
{
    private static Exchange Ex(decimal amount, long ts) => new() { Amount = amount, Timestamp = ts };

    private static PlayedGame Game(decimal amount, long start, long? end) =>
        new() { Amount = amount, Starttime = start, Endtime = end };

    [Fact]
    public void BuyIn_IncreasesChips_MoneyStaysZero()
    {
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10) }, Array.Empty<PlayedGame>());

        Assert.Equal(0m, s.Money);
        Assert.Equal(100m, s.Chips);
        Assert.Equal(100m, s.Bankroll);
    }

    [Fact]
    public void Win_IncreasesChips()
    {
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10) }, new[] { Game(50, 20, 30) });

        Assert.Equal(150m, s.Chips);
        Assert.Equal(0m, s.Money);
        Assert.Equal(100m, s.Bankroll); // wins do not change the bankroll
    }

    [Fact]
    public void Loss_DecreasesChips()
    {
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10) }, new[] { Game(-40, 20, 30) });

        Assert.Equal(60m, s.Chips);
        Assert.Equal(100m, s.Bankroll); // losses do not change the bankroll
    }

    [Fact]
    public void CashOut_MovesChipsToMoney()
    {
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10), Ex(-150, 40) }, new[] { Game(50, 20, 30) });

        Assert.Equal(150m, s.Money);
        Assert.Equal(0m, s.Chips);
        Assert.Equal(100m, s.Bankroll); // cash-out does not change the bankroll
    }

    [Fact]
    public void BuyIn_AfterCashOut_ReducesMoneyFirst()
    {
        // cashed out 150, then buy 50 chips again -> MO 100, CH 50, BR unchanged (covered by MO)
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10), Ex(-150, 40), Ex(50, 50) }, new[] { Game(50, 20, 30) });

        Assert.Equal(100m, s.Money);
        Assert.Equal(50m, s.Chips);
        Assert.Equal(100m, s.Bankroll);
    }

    [Fact]
    public void BuyIn_LargerThanMoney_ClampsMoneyToZero_ButChipsGetFullAmount()
    {
        // cashed out 150, then buy 200 chips -> MO 0 (not -50), CH 200, BR grows only by the extra 50 from the wallet
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10), Ex(-150, 40), Ex(200, 50) }, new[] { Game(50, 20, 30) });

        Assert.Equal(0m, s.Money);
        Assert.Equal(200m, s.Chips);
        Assert.Equal(150m, s.Bankroll);
    }

    [Fact]
    public void RunningGame_IsIgnored()
    {
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10) }, new[] { Game(0, 20, null) });

        Assert.Equal(100m, s.Chips);
    }

    [Fact]
    public void EventsAreOrderedByTimestamp_RegardlessOfInputOrder()
    {
        var exchanges = new[] { Ex(-80, 50), Ex(100, 10) }; // cash-out listed first but happens later
        var games = new[] { Game(-20, 20, 30) };

        var timeline = BankrollCalculator.Timeline(exchanges, games);

        Assert.Equal(3, timeline.Count);
        Assert.Equal(new long[] { 10, 30, 50 }, timeline.Select(p => p.Timestamp).ToArray());
        Assert.Equal(new[] { "Buy-in", "Lost", "Cash-out" }, timeline.Select(p => p.Label).ToArray());
        Assert.Equal(0m, timeline[^1].Chips);
        Assert.Equal(80m, timeline[^1].Money);
        Assert.Equal(100m, timeline[^1].Bankroll);
    }

    [Fact]
    public void Bankroll_IsSumOfAllWalletFundedBuyIns()
    {
        // 100 in, 30 out, 30 back in (covered by MO), 70 more in (from wallet) -> BR 170
        var s = BankrollCalculator.Compute(new[] { Ex(100, 10), Ex(-30, 20), Ex(30, 30), Ex(70, 40) }, Array.Empty<PlayedGame>());

        Assert.Equal(170m, s.Bankroll);
        Assert.Equal(0m, s.Money);
        Assert.Equal(170m, s.Chips);
    }

    [Fact]
    public void Bankroll_NeverDecreases()
    {
        var timeline = BankrollCalculator.Timeline(new[] { Ex(100, 10), Ex(-100, 40), Ex(-20, 50) }, new[] { Game(20, 20, 30) });

        Assert.All(timeline, p => Assert.Equal(100m, p.Bankroll));
    }

    [Fact]
    public void SameTimestamp_ExchangeIsAppliedBeforeGameResult()
    {
        var timeline = BankrollCalculator.Timeline(new[] { Ex(100, 10) }, new[] { Game(-30, 5, 10) });

        Assert.Equal("Buy-in", timeline[0].Label);
        Assert.Equal("Lost", timeline[1].Label);
        Assert.Equal(70m, timeline[1].Chips);
    }

    [Fact]
    public void Empty_IsAllZero()
    {
        var s = BankrollCalculator.Compute(Array.Empty<Exchange>(), Array.Empty<PlayedGame>());

        Assert.Equal(0m, s.Money);
        Assert.Equal(0m, s.Chips);
        Assert.Equal(0m, s.Bankroll);
    }

    [Fact]
    public void ChipsLeftMode_DeltaIsChipsLeftMinusChipsBeforeGame()
    {
        // Mirrors MainViewModel: delta = chipsLeft - current chips (running game has Amount 0)
        var before = BankrollCalculator.Compute(new[] { Ex(100, 10) }, new[] { Game(0, 20, null) });
        var delta = 35m - before.Chips;

        Assert.Equal(-65m, delta);

        var after = BankrollCalculator.Compute(new[] { Ex(100, 10) }, new[] { Game(delta, 20, 30) });
        Assert.Equal(35m, after.Chips);
    }
}
