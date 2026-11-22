using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Intelligence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Forecasting, capacity and anomaly views. Guarded like the admin dashboard (users.view).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/intelligence")]
[Authorize]
public class OperationsIntelligenceController : ControllerBase
{
    private readonly IOperationsIntelligenceService _svc;
    public OperationsIntelligenceController(IOperationsIntelligenceService svc) => _svc = svc;

    [HttpGet("demand-forecast")]
    [Permission(Permissions.Users.View)]
    public Task<DemandForecastDto> Demand([FromQuery] int historyDays = 60, CancellationToken ct = default)
        => _svc.GetDemandForecastAsync(historyDays, ct);

    [HttpGet("no-show-rates")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<DoctorWeekRateDto>> NoShow([FromQuery] int weeks = 8, CancellationToken ct = default)
        => _svc.GetNoShowRatesAsync(weeks, ct);

    [HttpGet("capacity")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<CapacityPointDto>> Capacity([FromQuery] int daysBack = 14, [FromQuery] int daysAhead = 14, CancellationToken ct = default)
        => _svc.GetCapacityAsync(daysBack, daysAhead, ct);

    [HttpGet("vaccine-coverage")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<VaccineCoverageDto>> Vaccines(CancellationToken ct) => _svc.GetVaccineCoverageAsync(ct);

    [HttpGet("lost-pets")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<LostPetWeekDto>> LostPets([FromQuery] int weeks = 12, CancellationToken ct = default)
        => _svc.GetLostPetTrendAsync(weeks, ct);

    [HttpGet("store-trends")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<StoreTrendWeekDto>> Store([FromQuery] int weeks = 12, CancellationToken ct = default)
        => _svc.GetStoreTrendsAsync(weeks, ct);

    [HttpGet("inventory-runout")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<InventoryRunoutDto>> Inventory([FromQuery] int salesWindowDays = 30, CancellationToken ct = default)
        => _svc.GetInventoryRunoutAsync(salesWindowDays, ct);

    [HttpGet("anomalies")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<AnomalyDto>> Anomalies([FromQuery] int days = 60, [FromQuery] double threshold = 2.5, CancellationToken ct = default)
        => _svc.GetAnomaliesAsync(days, threshold, ct);

    /// <summary>dataset: demand | no-show | capacity | vaccine-coverage | lost-pets | store | inventory | anomalies</summary>
    [HttpGet("export/{dataset}")]
    [Permission(Permissions.Users.View)]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> Export(string dataset, CancellationToken ct)
    {
        var csv = await _svc.ExportCsvAsync(dataset, ct);
        if (csv is null) return NotFound();
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"intelligence-{dataset}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
