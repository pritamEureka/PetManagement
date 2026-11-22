namespace Pawzaroo.Application.Modules.Intelligence;

public record DemandPoint(string Date, double? Actual, double? MovingAvg7, double? Projected);
public record DemandForecastDto(IReadOnlyList<DemandPoint> Points, double TrendPerDay, double ProjectedTotalNext14Days);

public record DoctorWeekRateDto(string WeekStart, Guid DoctorId, string DoctorName, int Total, int NoShows, int Cancelled,
    double NoShowRate, double CancellationRate);

public record CapacityPointDto(string Date, int Booked, int Capacity, double UtilisationPct);

public record VaccineCoverageDto(string Species, int TotalPets, int UpToDate, double CoveragePct);

public record LostPetWeekDto(string WeekStart, int Reported, int Reunited, int StillOpen, double RecoveryRate);

public record StoreTrendWeekDto(string WeekStart, int Orders, int CartUsers, int OrderingUsers, double ConversionPct,
    int ReturnRequests, double ReturnRatePct);

public record InventoryRunoutDto(Guid ProductId, string Name, string Sku, int Stock, double AvgDailySales,
    double? DaysRemaining, string? RunOutDate);

public record AnomalyDto(string Metric, string Date, double Value, double ZScore, string Direction);

public interface IOperationsIntelligenceService
{
    Task<DemandForecastDto> GetDemandForecastAsync(int historyDays, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorWeekRateDto>> GetNoShowRatesAsync(int weeks, CancellationToken ct = default);
    Task<IReadOnlyList<CapacityPointDto>> GetCapacityAsync(int daysBack, int daysAhead, CancellationToken ct = default);
    Task<IReadOnlyList<VaccineCoverageDto>> GetVaccineCoverageAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LostPetWeekDto>> GetLostPetTrendAsync(int weeks, CancellationToken ct = default);
    Task<IReadOnlyList<StoreTrendWeekDto>> GetStoreTrendsAsync(int weeks, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryRunoutDto>> GetInventoryRunoutAsync(int salesWindowDays, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyDto>> GetAnomaliesAsync(int days, double threshold, CancellationToken ct = default);

    /// <summary>CSV for one dataset: demand, no-show, capacity, vaccine-coverage, lost-pets, store, inventory, anomalies. Null if unknown.</summary>
    Task<string?> ExportCsvAsync(string dataset, CancellationToken ct = default);
}
