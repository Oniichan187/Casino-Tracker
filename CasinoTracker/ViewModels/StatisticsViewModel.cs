using System.Collections.ObjectModel;
using CasinoTracker.Controls;
using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class StatisticsViewModel : BaseViewModel
{
    private static readonly Color PrimaryColor = Color.FromArgb("#2E7D32");
    private static readonly Color AccentColor = Color.FromArgb("#F9A825");
    private static readonly Color MoneyColor = Color.FromArgb("#1976D2");
    private static readonly Color FoodColor = Color.FromArgb("#8D6E63");

    private readonly IStatisticsService _statistics;
    private readonly IDialogService _dialogs;

    public StatisticsViewModel(IStatisticsService statistics, IDialogService dialogs)
    {
        _statistics = statistics;
        _dialogs = dialogs;
        Title = "Statistics";
    }

    [ObservableProperty] private bool _hasData;

    /// <summary>True once a load finished without any session, so the empty state does not flash while loading.</summary>
    [ObservableProperty] private bool _isEmpty;

    // ---- overview tiles
    [ObservableProperty] private string _totalSessionsText = "0";
    [ObservableProperty] private string _totalDurationText = "0m";
    [ObservableProperty] private decimal _totalResult;
    [ObservableProperty] private string _totalResultText = "0.00";
    [ObservableProperty] private string _totalConsumptionText = "0.00";
    [ObservableProperty] private decimal _totalBalance;
    [ObservableProperty] private string _totalBalanceText = "0.00";
    [ObservableProperty] private string _totalBuyInsText = "0.00";
    [ObservableProperty] private string _totalCashOutsText = "0.00";
    [ObservableProperty] private decimal _hourlyRate;
    [ObservableProperty] private string _hourlyRateText = "0.00";
    [ObservableProperty] private string _winRateText = "–";
    [ObservableProperty] private string _averageDurationText = "–";
    [ObservableProperty] private string _longestSessionText = "–";
    [ObservableProperty] private string _bestSessionText = "–";
    [ObservableProperty] private string _worstSessionText = "–";
    [ObservableProperty] private string _favoriteCasinoText = "–";
    [ObservableProperty] private string _mostPlayedGameText = "–";
    [ObservableProperty] private string _mostConsumedText = "–";

    // ---- charts
    [ObservableProperty] private List<ChartSeries> _revenueSeries = new();
    [ObservableProperty] private List<BarItem> _monthlyBars = new();
    [ObservableProperty] private List<BarItem> _resultPerCasino = new();
    [ObservableProperty] private List<BarItem> _consumptionPerCasino = new();
    [ObservableProperty] private List<BarItem> _buyInsPerCasino = new();
    [ObservableProperty] private List<BarItem> _resultPerGame = new();
    [ObservableProperty] private List<BarItem> _topItems = new();

    public ObservableCollection<CasinoStatItem> Casinos { get; } = new();
    public ObservableCollection<GameStatItem> Games { get; } = new();
    [ObservableProperty] private bool _hasGames;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var data = await _statistics.ComputeAsync();
            Populate(data);
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Populate(StatisticsData d)
    {
        HasData = d.TotalSessions > 0;
        IsEmpty = !HasData;

        // Duration based figures only exist once a session has been finished.
        var noneFinished = d.FinishedSessions == 0;
        const string NoValue = "–";

        TotalSessionsText = d.TotalSessions.ToString();
        TotalDurationText = noneFinished ? NoValue : TimeHelper.FormatDuration(d.TotalDuration);
        TotalResult = d.TotalGameResult;
        TotalResultText = MoneyHelper.FormatSigned(d.TotalGameResult);
        TotalConsumptionText = MoneyHelper.Format(d.TotalConsumption);
        TotalBalance = d.TotalGameResult - d.TotalConsumption;
        TotalBalanceText = MoneyHelper.FormatSigned(TotalBalance);
        TotalBuyInsText = MoneyHelper.Format(d.TotalBuyIns);
        TotalCashOutsText = MoneyHelper.Format(d.TotalCashOuts);
        HourlyRate = d.HourlyRate;
        HourlyRateText = noneFinished ? NoValue : MoneyHelper.FormatSigned(d.HourlyRate) + " / h";
        WinRateText = noneFinished ? NoValue : $"{d.WinRate:P0}";
        AverageDurationText = noneFinished ? NoValue : TimeHelper.FormatDuration(d.AverageDuration);
        LongestSessionText = noneFinished ? NoValue : TimeHelper.FormatDuration(d.LongestSession);
        BestSessionText = d.BestSession is { } b ? $"{MoneyHelper.FormatSigned(b.Result)} · {b.Name} ({b.CasinoName})" : "–";
        WorstSessionText = d.WorstSession is { } w ? $"{MoneyHelper.FormatSigned(w.Result)} · {w.Name} ({w.CasinoName})" : "–";
        FavoriteCasinoText = d.FavoriteCasino ?? "–";
        MostPlayedGameText = d.MostPlayedGame ?? "–";
        MostConsumedText = d.MostConsumedItem is { } i ? $"{i.Count}x {i.Name}" : "–";

        // ---- revenue over time
        RevenueSeries = d.CumulativeResult.Count == 0
            ? new List<ChartSeries>()
            : new List<ChartSeries>
            {
                new()
                {
                    Name = "Game result (cumulative)",
                    Color = PrimaryColor,
                    Kind = SeriesKind.Line,
                    ShowMarkers = true,
                    Fill = true,
                    Points = d.CumulativeResult.Select(p => new ChartPoint(p.Timestamp, (double)p.Value)).ToList(),
                },
                new()
                {
                    Name = "incl. food & drinks",
                    Color = FoodColor,
                    Kind = SeriesKind.Line,
                    ShowMarkers = true,
                    Points = d.CumulativeBalance.Select(p => new ChartPoint(p.Timestamp, (double)p.Value)).ToList(),
                },
            };

        MonthlyBars = d.MonthlyResult
            .Select(p => new BarItem { Label = TimeHelper.ToLocal(p.Timestamp).ToString("MM.yy"), Value = (double)p.Value })
            .ToList();

        // ---- per casino
        ResultPerCasino = d.Casinos.Select(c => new BarItem { Label = c.Name, Value = (double)c.GameResult }).ToList();
        ConsumptionPerCasino = d.Casinos.Select(c => new BarItem { Label = c.Name, Value = (double)c.ConsumptionSpend, Color = FoodColor }).ToList();
        BuyInsPerCasino = d.Casinos.Select(c => new BarItem { Label = c.Name, Value = (double)c.BuyIns, Color = MoneyColor }).ToList();

        Casinos.Clear();
        foreach (var c in d.Casinos) Casinos.Add(new CasinoStatItem(c));

        // ---- per game
        ResultPerGame = d.Games.Select(g => new BarItem { Label = g.Name, Value = (double)g.Result }).ToList();
        Games.Clear();
        foreach (var g in d.Games) Games.Add(new GameStatItem(g));
        HasGames = Games.Count > 0;

        // ---- items
        TopItems = d.Items.Take(8)
            .Select(i => new BarItem { Label = i.Name, Value = i.Count, Color = i.IsBeverage ? AccentColor : FoodColor })
            .ToList();
    }
}
