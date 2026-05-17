using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

/// <summary>
/// Optional 1:1 extended profile. Splits long-tail attributes off the hot User
/// table (which is read on every authenticated request) and parks free-form
/// preferences in a JSONB blob.
/// </summary>
public class UserProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateRegion { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? Website { get; set; }
    public string? FacebookHandle { get; set; }
    public string? InstagramHandle { get; set; }
    public string? TwitterHandle { get; set; }

    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "USD";

    /// <summary>Free-form JSONB: notification opt-ins, dietary prefs, accessibility flags...</summary>
    public string? PreferencesJson { get; set; }
}
