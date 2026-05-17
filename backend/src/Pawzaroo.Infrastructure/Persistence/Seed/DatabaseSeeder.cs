using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Identity;
using P = Pawzaroo.Application.Common.Permissions.Permissions;

namespace Pawzaroo.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedPermissionsAsync(db, ct);
        await SeedRolesAsync(db, ct);
        await SeedSuperAdminAsync(db, hasher, ct);
        await CatalogSeeder.SeedAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPermissionsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var existing = (await db.Permissions.Select(p => new { p.Module, p.Action }).ToListAsync(ct))
            .Select(x => (x.Module, x.Action)).ToHashSet();
        foreach (var (m, a) in P.All())
        {
            if (existing.Contains((m, a))) continue;
            db.Permissions.Add(new Permission { Module = m, Action = a, Description = $"{m}.{a}" });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var allPerms = await db.Permissions.ToListAsync(ct);
        var allCodes = allPerms.Select(p => $"{p.Module}.{p.Action}").ToArray();

        async Task<Role> Ensure(string name, string description)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);
            if (role is null)
            {
                role = new Role { Name = name, Description = description, IsSystem = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
            }
            return role;
        }

        var superAdmin   = await Ensure(SystemRoles.SuperAdmin,      "Full access to every module.");
        var admin        = await Ensure(SystemRoles.Admin,           "Module administration.");
        var moderator    = await Ensure(SystemRoles.Moderator,       "Content moderation across posts, comments, reviews, messaging.");
        var support      = await Ensure(SystemRoles.SupportAgent,    "Customer support: read users + orders, refund, chat.");
        var vet          = await Ensure(SystemRoles.Veterinarian,    "Vet doctor: own profile, appointments, prescriptions.");
        var store        = await Ensure(SystemRoles.StoreOwner,      "Marketplace store: own products, orders, refunds.");
        var seller       = await Ensure(SystemRoles.Seller,          "Individual seller: own products and orders.");
        var serviceP     = await Ensure(SystemRoles.ServiceProvider, "Grooming / training / boarding / walking provider.");
        var breeder      = await Ensure(SystemRoles.Breeder,         "Breeder: pets and adoption listings.");
        var adoptionCtr  = await Ensure(SystemRoles.AdoptionCenter,  "Adoption center / shelter.");
        var delivery     = await Ensure(SystemRoles.DeliveryUser,    "Logistics / delivery courier.");
        var user         = await Ensure(SystemRoles.User,            "Default end user (Pet Owner).");

        // SuperAdmin -> everything. PermissionService also auto-grants in code,
        // but the explicit grant keeps the admin UI showing reality.
        await GrantAsync(db, superAdmin, allCodes, ct);

        // Admin -> everything except role create/delete (SuperAdmin-only destructive).
        await GrantAsync(db, admin, allCodes
            .Where(c => c is not (P.Roles.Create or P.Roles.Delete)).ToArray(), ct);

        await GrantAsync(db, moderator, new[]
        {
            P.Posts.View, P.Posts.Moderate, P.Posts.Delete, P.Posts.Unpublish,
            P.Comments.View, P.Comments.Moderate, P.Comments.Delete,
            P.Reviews.View, P.Reviews.Moderate, P.Reviews.Delete,
            P.Messaging.View, P.Messaging.Moderate, P.Messaging.Delete,
            P.Moderation.View, P.Moderation.Moderate, P.Moderation.Approve, P.Moderation.Reject,
            P.Reports.View, P.Reports.Moderate,
            P.Users.View, P.Users.Suspend, P.Users.Restore,
        }, ct);

        await GrantAsync(db, support, new[]
        {
            P.Users.View, P.Users.Edit, P.Users.Suspend, P.Users.Restore, P.Users.Export,
            P.Orders.View, P.Orders.Refund, P.Orders.Cancel, P.Orders.Export,
            P.Appointments.View, P.Appointments.Reschedule, P.Appointments.Cancel,
            P.Messaging.View, P.Messaging.Chat, P.Messaging.Moderate,
            P.Reports.View, P.Audit.View,
        }, ct);

        await GrantAsync(db, vet, new[]
        {
            P.Vets.View, P.Vets.Edit,
            P.Appointments.View, P.Appointments.Create, P.Appointments.Cancel,
            P.Appointments.Reschedule, P.Appointments.Chat,
            P.Prescriptions.View, P.Prescriptions.Create, P.Prescriptions.Edit,
            P.Pets.View,
            P.Posts.View, P.Posts.Create,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, store, new[]
        {
            P.Stores.View, P.Stores.Edit,
            P.Products.View, P.Products.Create, P.Products.Edit, P.Products.Delete,
            P.Products.Publish, P.Products.Unpublish, P.Products.Import, P.Products.Export,
            P.Orders.View, P.Orders.Refund, P.Orders.Cancel, P.Orders.Export,
            P.Reviews.View,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, seller, new[]
        {
            P.Products.View, P.Products.Create, P.Products.Edit, P.Products.Delete,
            P.Products.Publish, P.Products.Unpublish,
            P.Orders.View, P.Orders.Cancel,
            P.Reviews.View,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, serviceP, new[]
        {
            P.Services.View, P.Services.Book, P.Services.Cancel,
            P.Appointments.View, P.Appointments.Cancel, P.Appointments.Reschedule, P.Appointments.Chat,
            P.Reviews.View,
            P.Posts.View, P.Posts.Create,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, breeder, new[]
        {
            P.Pets.View, P.Pets.Create, P.Pets.Edit, P.Pets.Delete,
            P.Adoption.View, P.Adoption.Create, P.Adoption.Edit, P.Adoption.Delete,
            P.Adoption.Publish, P.Adoption.Unpublish,
            P.AdoptionRequests.View, P.AdoptionRequests.Chat,
            P.Posts.View, P.Posts.Create,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, adoptionCtr, new[]
        {
            P.Pets.View, P.Pets.Create, P.Pets.Edit, P.Pets.Delete,
            P.Adoption.View, P.Adoption.Create, P.Adoption.Edit, P.Adoption.Delete,
            P.Adoption.Publish, P.Adoption.Unpublish,
            P.AdoptionRequests.View, P.AdoptionRequests.Chat,
            P.Posts.View, P.Posts.Create,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, delivery, new[]
        {
            P.Orders.View, P.Orders.Cancel,
            P.Delivery.View, P.Delivery.Edit,
            P.Notifications.View,
        }, ct);

        await GrantAsync(db, user, new[]
        {
            P.Posts.View, P.Posts.Create, P.Posts.Edit, P.Posts.Delete,
            P.Comments.View,
            P.Pets.View, P.Pets.Create, P.Pets.Edit, P.Pets.Delete,
            P.Adoption.View, P.Adoption.Create, P.Adoption.Edit,
            P.AdoptionRequests.View, P.AdoptionRequests.Create, P.AdoptionRequests.Cancel, P.AdoptionRequests.Chat,
            P.Vets.View,
            P.Appointments.View, P.Appointments.Create, P.Appointments.Cancel,
            P.Appointments.Reschedule, P.Appointments.Chat,
            P.Stores.View, P.Products.View,
            P.Orders.View, P.Orders.Cancel,
            P.Services.View, P.Services.Book,
            P.Reviews.View,
            P.Messaging.View, P.Messaging.Chat,
            P.Notifications.View,
        }, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task GrantAsync(ApplicationDbContext db, Role role,
        IEnumerable<string> codes, CancellationToken ct)
    {
        var byCode = await db.Permissions
            .ToDictionaryAsync(p => p.Module + "." + p.Action, p => p.Id, ct);
        var existing = (await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync(ct))
            .ToHashSet();
        foreach (var c in codes.Distinct())
        {
            if (!byCode.TryGetValue(c, out var pid)) continue;
            if (existing.Contains(pid)) continue;
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
        }
    }

    private static async Task SeedSuperAdminAsync(ApplicationDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        const string email = "superadmin@pawzaroo.local";
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;

        var role = await db.Roles.FirstAsync(r => r.Name == SystemRoles.SuperAdmin, ct);
        var u = new User
        {
            Email = email,
            DisplayName = "Super Admin",
            PasswordHash = hasher.Hash("Admin@12345"),
            EmailConfirmed = true,
            IsActive = true
        };
        db.Users.Add(u);
        await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = role.Id });
        await db.SaveChangesAsync(ct);
    }
}
