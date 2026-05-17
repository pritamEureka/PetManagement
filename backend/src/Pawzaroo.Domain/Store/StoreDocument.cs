using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

public enum StoreDocumentType
{
    TradeLicense = 0,
    NationalId = 1,
    AddressProof = 2,
    TaxCertificate = 3,
    BankStatement = 4,
    Other = 99
}

/// <summary>Auxiliary KYC document upload — referenced by StoreOwnerProfile.</summary>
public class StoreDocument : AuditableEntity
{
    public Guid StoreOwnerProfileId { get; set; }
    public StoreOwnerProfile StoreOwnerProfile { get; set; } = default!;

    public StoreDocumentType Type { get; set; }
    public string FileName { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Notes { get; set; }
}
