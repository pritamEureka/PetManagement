namespace Pawzaroo.Application.Modules.Intelligence;

public record ScheduledReportDto(Guid Id, string Name, string Dataset, string Frequency, IReadOnlyList<string> Recipients,
    bool IsActive, DateTime? LastRunAt, DateTime NextRunAt, Guid OwnerUserId, DateTime CreatedAt, bool? LastRunSuccess);

public record SaveScheduledReportRequest(string Name, string Dataset, string Frequency, IReadOnlyList<string> Recipients, bool IsActive = true);

public record ReportRunDto(Guid Id, DateTime RanAt, bool Success, string? Error, int RowCount, int RecipientCount,
    int EmailsSent, bool CsvTruncated, bool Manual, bool HasCsv);

public record ReportCsvFile(string FileName, string Csv);

public interface IScheduledReportService
{
    static readonly IReadOnlyList<string> Datasets = new[]
    {
        "demand", "no-show", "capacity", "vaccine-coverage", "lost-pets", "store", "inventory", "anomalies",
    };

    Task<IReadOnlyList<ScheduledReportDto>> ListAsync(CancellationToken ct = default);
    Task<ScheduledReportDto> CreateAsync(SaveScheduledReportRequest req, CancellationToken ct = default);
    Task<ScheduledReportDto> UpdateAsync(Guid id, SaveScheduledReportRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ReportRunDto> RunNowAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportRunDto>> ListRunsAsync(Guid id, int take = 20, CancellationToken ct = default);
    Task<ReportCsvFile?> GetRunCsvAsync(Guid id, Guid runId, CancellationToken ct = default);

    /// <summary>Worker entry point: runs every active report whose NextRunAt has passed. Returns the number run.</summary>
    Task<int> RunDueAsync(CancellationToken ct = default);
}
