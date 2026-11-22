using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settlements")]
public class SettlementsController : ControllerBase
{
    private readonly ISettlementService _svc;
    public SettlementsController(ISettlementService svc) => _svc = svc;

    // ---------------- Store owner ----------------

    [HttpGet("mine")]
    [Authorize]
    public Task<StoreLedgerSummaryDto> MyLedger([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => _svc.GetMyLedgerAsync(page, pageSize, ct);

    [HttpGet("mine/payouts")]
    [Authorize]
    public Task<IReadOnlyList<StorePayoutDto>> MyPayouts(CancellationToken ct) => _svc.ListMyPayoutsAsync(ct);

    [HttpGet("mine/statement.csv")]
    [Authorize]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> MyStatement([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Csv(await _svc.ExportMyStatementAsync(from, to, ct));

    // ---------------- Admin ----------------

    [HttpGet("admin/batches")]
    [Permission(Permissions.Settings.View)]
    public Task<PageResult<SettlementBatchDto>> Batches([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.ListBatchesAsync(page, pageSize, ct);

    [HttpGet("admin/batches/{id:guid}")]
    [Permission(Permissions.Settings.View)]
    public Task<SettlementBatchDetailDto> Batch(Guid id, CancellationToken ct) => _svc.GetBatchAsync(id, ct);

    [HttpPost("admin/batches/generate")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<ActionResult<object>> Generate([FromBody] GenerateBatchInput? input, CancellationToken ct)
    {
        var result = await _svc.GenerateBatchAsync(input ?? new GenerateBatchInput(null, null), ct);
        return Ok(new { generated = result is not null, batch = result });
    }

    [HttpGet("admin/batches/{id:guid}/export.csv")]
    [Permission(Permissions.Settings.View)]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> ExportBatch(Guid id, CancellationToken ct) => Csv(await _svc.ExportBatchAsync(id, ct));

    [HttpPost("admin/batches/{id:guid}/reconcile")]
    [Permission(Permissions.Settings.Edit)]
    public Task<ReconcileResultDto> Reconcile(Guid id, [FromBody] ReconcileInput input, CancellationToken ct)
        => _svc.ReconcileBatchAsync(id, input, ct);

    [HttpGet("admin/payouts")]
    [Permission(Permissions.Settings.View)]
    public Task<PageResult<StorePayoutDto>> Payouts([FromQuery] StorePayoutStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => _svc.ListPayoutsAsync(status, page, pageSize, ct);

    [HttpPost("admin/payouts/{id:guid}/paid")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPayoutPaidInput input, CancellationToken ct)
    {
        await _svc.MarkPayoutPaidAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("admin/payouts/{id:guid}/fail")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Fail(Guid id, [FromBody] FailBody? body, CancellationToken ct)
    {
        await _svc.FailPayoutAsync(id, body?.Reason, ct);
        return NoContent();
    }

    [HttpGet("admin/stores/{storeId:guid}/statement.csv")]
    [Permission(Permissions.Settings.View)]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> StoreStatement(Guid storeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Csv(await _svc.ExportStoreStatementAsync(storeId, from, to, ct));

    public record FailBody(string? Reason);

    private FileContentResult Csv(CsvFile f)
    {
        // UTF-8 BOM so Excel opens non-ASCII store names correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(f.Content)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", f.FileName);
    }
}
