using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Intelligence;

public enum ReportFrequency { Daily, Weekly, Monthly }

/// <summary>A recurring CSV export of one operations-intelligence dataset, emailed to a recipient list.</summary>
public class ScheduledReport : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = default!;
    /// <summary>demand | no-show | capacity | vaccine-coverage | lost-pets | store | inventory | anomalies</summary>
    public string Dataset { get; set; } = default!;
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;
    public List<string> Recipients { get; set; } = new();
    public DateTime? LastRunAt { get; set; }
    public DateTime NextRunAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ReportRun> Runs { get; set; } = new List<ReportRun>();
}

/// <summary>One execution of a scheduled report. The CSV is kept inline (capped) for download from the history.</summary>
public class ReportRun : BaseEntity
{
    public Guid ScheduledReportId { get; set; }
    public ScheduledReport ScheduledReport { get; set; } = default!;
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int RowCount { get; set; }
    public int RecipientCount { get; set; }
    public int EmailsSent { get; set; }
    /// <summary>CSV text, truncated to the service's inline cap (see <see cref="CsvTruncated"/>).</summary>
    public string? CsvText { get; set; }
    public bool CsvTruncated { get; set; }
    public bool Manual { get; set; }
}
