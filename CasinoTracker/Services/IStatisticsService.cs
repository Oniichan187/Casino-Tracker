using CasinoTracker.Models;

namespace CasinoTracker.Services;

public interface IStatisticsService
{
    Task<StatisticsData> ComputeAsync();
}
