using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Persistence.Seed;

/// <summary>
/// Reference data: product categories, brands, vet specialties, baseline
/// system settings (feature flags, marketplace commission, SLA windows).
/// </summary>
public static class CatalogSeeder
{
    private static readonly (string Slug, string Name)[] ProductCategories =
    {
        ("food",              "Pet Food"),
        ("treats",            "Treats & Chews"),
        ("medicine",          "Medicine & Supplements"),
        ("litter-bedding",    "Litter & Bedding"),
        ("toys",              "Toys & Enrichment"),
        ("grooming",          "Grooming"),
        ("accessories",       "Accessories"),
        ("training",          "Training"),
        ("aquatic",           "Aquatic Supplies"),
        ("bird-supplies",     "Bird Supplies"),
        ("livestock-feed",    "Livestock Feed"),
        ("farm-equipment",    "Farm & Stable Equipment")
    };

    private static readonly string[] Brands =
    {
        "Pawzaroo Basics", "Royal Tail", "Whiskers & Co", "FurNation",
        "Barnyard Best", "AquaLife", "FeatherFriend"
    };

    private static readonly (string Slug, string Name)[] VetSpecialties =
    {
        ("general",       "General Practice"),
        ("surgery",       "Surgery"),
        ("dermatology",   "Dermatology"),
        ("cardiology",    "Cardiology"),
        ("ophthalmology", "Ophthalmology"),
        ("dentistry",     "Dentistry"),
        ("oncology",      "Oncology"),
        ("neurology",     "Neurology"),
        ("behavior",      "Behavior"),
        ("exotic",        "Exotic Animals"),
        ("large-animal",  "Large Animals"),
        ("repro",         "Reproduction")
    };

    private static readonly (string Key, string Category, object Value, string Desc)[] SystemSettings =
    {
        ("marketplace.default_commission_percent", "marketplace", 10.0m,
            "Default platform commission applied when a store doesn't override."),
        ("adoption.approval_sla_hours", "adoption", 48,
            "Hours before an adoption listing approval becomes overdue."),
        ("vet.approval_sla_hours", "vet", 72, "Vet approval SLA."),
        ("store.approval_sla_hours", "store", 72, "Store approval SLA."),
        ("notification.email_enabled", "notifications", true, "Master toggle for outbound email."),
        ("notification.push_enabled", "notifications", true, "Master toggle for push notifications."),
        ("feed.max_post_length", "feed", 5000, "Hard limit on post content length."),
        ("messaging.max_attachment_mb", "messaging", 25, "Per-attachment size cap."),
        ("uploads.max_file_mb", "media", 50, "Per-upload size cap."),
        ("auth.access_token_minutes", "auth", 60, "Access token TTL (read by token issuer config)."),
    };

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        foreach (var (slug, name) in ProductCategories)
            if (!await db.ProductCategories.AnyAsync(c => c.Slug == slug, ct))
                db.ProductCategories.Add(new ProductCategory { Slug = slug, Name = name });

        foreach (var name in Brands)
            if (!await db.Brands.AnyAsync(b => b.Name == name, ct))
                db.Brands.Add(new Brand { Name = name });

        foreach (var (slug, name) in VetSpecialties)
            if (!await db.Specialties.AnyAsync(s => s.Slug == slug, ct))
                db.Specialties.Add(new Specialty { Slug = slug, Name = name });

        foreach (var (key, category, value, desc) in SystemSettings)
            if (!await db.SystemSettings.AnyAsync(s => s.Key == key, ct))
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key, Category = category, Description = desc,
                    ValueJson = JsonSerializer.Serialize(new { value })
                });

        await db.SaveChangesAsync(ct);
    }
}
