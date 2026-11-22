using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Intelligence;
using Pawzaroo.Domain.Intelligence;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Intelligence;

public class ScheduledReportService : IScheduledReportService
{
    /// <summary>Max CSV characters stored on a run row.</summary>
    public const int CsvInlineCap = 200_000;
    private const int MaxRecipients = 10;
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IOperationsIntelligenceService _intel;
    private readonly IEmailService _email;
    private readonly ILogger<ScheduledReportService> _log;

    public ScheduledReportService(ApplicationDbContext db, ICurrentUserService current, IOperationsIntelligenceService intel,
        IEmailService email, ILogger<ScheduledReportService> log)
    {
        _db = db;
        _current = current;
        _intel = intel;
        _email = email;
        _log = log;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // ---- schedule math (pure, unit-testable) ----------------------------------------------

    /// <summary>First run: the next 06:00 UTC.</summary>
    public static DateTime FirstRun(DateTime nowUtc)
    {
        var t = nowUtc.Date.AddHours(6);
        return t > nowUtc ? t : t.AddDays(1);
    }

    /// <summary>Next occurrence strictly after <paramref name="nowUtc"/>, stepping from <paramref name="previous"/>.</summary>
    public static DateTime Advance(ReportFrequency f, DateTime previous, DateTime nowUtc)
    {
        var next = previous;
        do
        {
            next = f switch
            {
                ReportFrequency.Daily => next.AddDays(1),
                ReportFrequency.Weekly => next.AddDays(7),
                _ => next.AddMonths(1),
            };
        } while (next <= nowUtc);
        return next;
    }

    // ---- CRUD ------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ScheduledReportDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.Set<ScheduledReport>().AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var runs = await _db.Set<ReportRun>().AsNoTracking().Where(r => ids.Contains(r.ScheduledReportId))
            .Select(r => new { r.ScheduledReportId, r.RanAt, r.Success }).ToListAsync(ct);
        var map = runs.GroupBy(x => x.ScheduledReportId)
            .ToDictionary(g => g.Key, g => (bool?)g.OrderByDescending(x => x.RanAt).First().Success);
        return rows.Select(r => ToDto(r, map.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<ScheduledReportDto> CreateAsync(SaveScheduledReportRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var (name, dataset, freq, recipients) = Validate(req);
        var r = new ScheduledReport
        {
            OwnerUserId = uid, Name = name, Dataset = dataset, Frequency = freq, Recipients = recipients,
            IsActive = req.IsActive, NextRunAt = FirstRun(DateTime.UtcNow),
        };
        _db.Set<ScheduledReport>().Add(r);
        await _db.SaveChangesAsync(ct);
        return ToDto(r, null);
    }

    public async Task<ScheduledReportDto> UpdateAsync(Guid id, SaveScheduledReportRequest req, CancellationToken ct = default)
    {
        Uid();
        var (name, dataset, freq, recipients) = Validate(req);
        var r = await Get(id, ct);
        var freqChanged = r.Frequency != freq;
        var reactivated = req.IsActive && !r.IsActive;
        r.Name = name; r.Dataset = dataset; r.Frequency = freq; r.Recipients = recipients; r.IsActive = req.IsActive;
        if (freqChanged || reactivated) r.NextRunAt = FirstRun(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return ToDto(r, null);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Uid();
        var r = await Get(id, ct);
        _db.Set<ScheduledReport>().Remove(r); // soft-deleted by the DbContext
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReportRunDto>> ListRunsAsync(Guid id, int take = 20, CancellationToken ct = default)
    {
        await Get(id, ct, track: false);
        take = Math.Clamp(take, 1, 100);
        return await _db.Set<ReportRun>().AsNoTracking().Where(x => x.ScheduledReportId == id)
            .OrderByDescending(x => x.RanAt).Take(take)
            .Select(x => new ReportRunDto(x.Id, x.RanAt, x.Success, x.Error, x.RowCount, x.RecipientCount, x.EmailsSent,
                x.CsvTruncated, x.Manual, x.CsvText != null))
            .ToListAsync(ct);
    }

    public async Task<ReportCsvFile?> GetRunCsvAsync(Guid id, Guid runId, CancellationToken ct = default)
    {
        var rep = await Get(id, ct, track: false);
        var run = await _db.Set<ReportRun>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId && x.ScheduledReportId == id, ct);
        if (run?.CsvText is null) return null;
        return new ReportCsvFile($"report-{rep.Dataset}-{run.RanAt:yyyyMMdd-HHmm}.csv", run.CsvText);
    }

    // ---- execution -------------------------------------------------------------------------

    public async Task<ReportRunDto> RunNowAsync(Guid id, CancellationToken ct = default)
    {
        Uid();
        var r = await Get(id, ct);
        var run = await ExecuteAsync(r, manual: true, ct);
        r.LastRunAt = run.RanAt;
        await _db.SaveChangesAsync(ct);
        return new ReportRunDto(run.Id, run.RanAt, run.Success, run.Error, run.RowCount, run.RecipientCount, run.EmailsSent,
            run.CsvTruncated, run.Manual, run.CsvText != null);
    }

    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.Set<ScheduledReport>().Where(r => r.IsActive && r.NextRunAt <= now)
            .OrderBy(r => r.NextRunAt).Take(20).ToListAsync(ct);
        if (due.Count == 0) return 0;
        foreach (var r in due)
        {
            // Advance first so a crash mid-run cannot cause a tight retry loop or duplicate sends.
            r.NextRunAt = Advance(r.Frequency, r.NextRunAt, now);
            r.LastRunAt = now;
        }
        await _db.SaveChangesAsync(ct);

        foreach (var r in due)
        {
            try { await ExecuteAsync(r, manual: false, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Scheduled report {ReportId} failed", r.Id);
            }
        }
        return due.Count;
    }

    private async Task<ReportRun> ExecuteAsync(ScheduledReport r, bool manual, CancellationToken ct)
    {
        var run = new ReportRun
        {
            ScheduledReportId = r.Id, RanAt = DateTime.UtcNow, Manual = manual, RecipientCount = r.Recipients.Count,
        };
        try
        {
            var csv = await _intel.ExportCsvAsync(r.Dataset, ct) ?? throw new InvalidOperationException($"Unknown dataset '{r.Dataset}'.");
            run.RowCount = Math.Max(0, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1);
            run.CsvTruncated = csv.Length > CsvInlineCap;
            run.CsvText = run.CsvTruncated ? csv[..CsvInlineCap] : csv;

            // SMTP delivers the FULL CSV as an attachment (no inline copy in the body). Transports without attachment
            // support (console stub) fall back to inlining the CSV through the IEmailService default method.
            var fileName = $"report-{r.Dataset}-{run.RanAt:yyyyMMdd-HHmm}.csv";
            var attachments = new[] { new EmailAttachment(fileName, "text/csv", System.Text.Encoding.UTF8.GetBytes(csv)) };
            var subject = $"[Pawzaroo] Scheduled report: {r.Name} ({r.Dataset}) - {run.RanAt:yyyy-MM-dd}";
            var body = $"Scheduled report \"{r.Name}\"\nDataset: {r.Dataset}\nGenerated: {run.RanAt:u}\nRows: {run.RowCount}\n\nThe CSV is attached ({fileName}).";
            foreach (var to in r.Recipients)
            {
                try { await _email.SendWithAttachmentsAsync(to, subject, body, attachments, null, ct); run.EmailsSent++; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogWarning(ex, "Report email to {To} failed", to);
                }
            }
            run.Success = run.EmailsSent > 0 || r.Recipients.Count == 0;
            if (!run.Success) run.Error = "No recipient could be emailed.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Success = false;
            run.Error = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
        }
        _db.Set<ReportRun>().Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    // ---- helpers ---------------------------------------------------------------------------

    private async Task<ScheduledReport> Get(Guid id, CancellationToken ct, bool track = true)
    {
        var q = track ? _db.Set<ScheduledReport>() : _db.Set<ScheduledReport>().AsNoTracking();
        return await q.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ScheduledReport", id);
    }

    private static (string Name, string Dataset, ReportFrequency Freq, List<string> Recipients) Validate(SaveScheduledReportRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        var name = (req.Name ?? "").Trim();
        if (name.Length is 0 or > 120) errors["name"] = new[] { "Name is required (max 120 characters)." };
        var dataset = (req.Dataset ?? "").Trim().ToLowerInvariant();
        if (!IScheduledReportService.Datasets.Contains(dataset)) errors["dataset"] = new[] { "Unknown dataset." };
        if (!Enum.TryParse<ReportFrequency>(req.Frequency, true, out var freq) || !Enum.IsDefined(freq))
            errors["frequency"] = new[] { "Frequency must be Daily, Weekly or Monthly." };
        var recipients = (req.Recipients ?? Array.Empty<string>()).Select(x => (x ?? "").Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct().ToList();
        if (recipients.Count == 0 || recipients.Count > MaxRecipients || recipients.Any(x => x.Length > 254 || !EmailRx.IsMatch(x)))
            errors["recipients"] = new[] { $"Provide 1-{MaxRecipients} valid email addresses." };
        if (errors.Count > 0) throw new ValidationException(errors);
        return (name, dataset, freq, recipients);
    }

    private static ScheduledReportDto ToDto(ScheduledReport r, bool? lastSuccess) =>
        new(r.Id, r.Name, r.Dataset, r.Frequency.ToString(), r.Recipients, r.IsActive, r.LastRunAt, r.NextRunAt,
            r.OwnerUserId, r.CreatedAt, lastSuccess);
}
