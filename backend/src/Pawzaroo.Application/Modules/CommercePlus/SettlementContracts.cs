using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.CommercePlus;

public record LedgerEntryDto(
    Guid Id, Guid StoreId, Guid? OrderId, Guid? OrderItemId, LedgerEntryType Type,
    decimal Amount, string Currency, DateTime AvailableAt, string? Memo, DateTime CreatedAt);

public record StoreLedgerSummaryDto(
    Guid StoreId, string Currency, int HoldDays,
    decimal TotalBalance, decimal AvailableBalance, decimal OnHoldBalance, decimal PaidOut,
    PageResult<LedgerEntryDto> Entries);

public record StorePayoutDto(
    Guid Id, Guid SettlementBatchId, string BatchReference, Guid StoreId, string StoreName,
    decimal Amount, string Currency, StorePayoutStatus Status, string? GatewayReference,
    decimal? GatewayAmount, string? Notes, DateTime CreatedAt, DateTime? PaidAt, DateTime? ReconciledAt);

public record SettlementBatchDto(
    Guid Id, string Reference, SettlementBatchStatus Status, string Currency, decimal TotalAmount,
    int PayoutCount, DateTime CutoffAt, DateTime CreatedAt, DateTime? PaidAt, DateTime? ReconciledAt);

public record SettlementBatchDetailDto(SettlementBatchDto Batch, IReadOnlyList<StorePayoutDto> Payouts);

public record MarkPayoutPaidInput(string GatewayReference, decimal? GatewayAmount, string? Notes);

public record ReconcileLine(Guid PayoutId, decimal GatewayAmount, string? GatewayReference);
public record ReconcileInput(IReadOnlyList<ReconcileLine> Lines);

public record ReconcileResultDto(int Matched, int Mismatched, IReadOnlyList<Guid> MismatchedPayoutIds, SettlementBatchDto Batch);

public record GenerateBatchInput(DateTime? CutoffAt, decimal? MinimumPayout);

public record CsvFile(string FileName, string Content);

/// <summary>Used by OrderService/DeliveryService hooks. Never throws into the order flow.</summary>
public interface ILedgerRecorder
{
    /// <summary>Idempotently writes Earning + Commission entries for every item of the order.</summary>
    Task RecordOrderEarningsAsync(Guid orderId, CancellationToken ct = default);
    /// <summary>Idempotently writes Refund + commission Reversal entries. amount null = full refund.</summary>
    Task RecordOrderRefundAsync(Guid orderId, decimal? amount, CancellationToken ct = default);
    /// <summary>Idempotent (once per order item) Refund + commission Reversal for an approved return. amount null = full item net.</summary>
    Task RecordItemRefundAsync(Guid orderItemId, decimal? amount, CancellationToken ct = default);
}

public interface ISettlementService
{
    // Store side
    Task<StoreLedgerSummaryDto> GetMyLedgerAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<StorePayoutDto>> ListMyPayoutsAsync(CancellationToken ct = default);
    Task<CsvFile> ExportMyStatementAsync(DateTime? from, DateTime? to, CancellationToken ct = default);

    // Admin side
    Task<PageResult<SettlementBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<SettlementBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<SettlementBatchDetailDto?> GenerateBatchAsync(GenerateBatchInput input, CancellationToken ct = default);
    Task MarkPayoutPaidAsync(Guid payoutId, MarkPayoutPaidInput input, CancellationToken ct = default);
    Task FailPayoutAsync(Guid payoutId, string? reason, CancellationToken ct = default);
    Task<ReconcileResultDto> ReconcileBatchAsync(Guid batchId, ReconcileInput input, CancellationToken ct = default);
    Task<CsvFile> ExportBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<CsvFile> ExportStoreStatementAsync(Guid storeId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<PageResult<StorePayoutDto>> ListPayoutsAsync(StorePayoutStatus? status, int page, int pageSize, CancellationToken ct = default);
}
