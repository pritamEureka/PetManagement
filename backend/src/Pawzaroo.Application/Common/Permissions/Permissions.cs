namespace Pawzaroo.Application.Common.Permissions;

/// <summary>
/// Canonical permission catalogue: {module}.{action}. Loaded into the DB on
/// startup and referenced by [Permission(Permissions.X.Y)] attributes.
///
/// Adding a new permission: declare it under the right module and the seeder
/// picks it up the next time the API starts. Do not delete codes that have
/// ever been assigned — orphan them with [Obsolete] instead.
/// </summary>
public static class Permissions
{
    // -- Generic action vocabulary ------------------------------------------------
    public static class Actions
    {
        public const string View       = "view";
        public const string Create     = "create";
        public const string Edit       = "edit";
        public const string Delete     = "delete";
        public const string Approve    = "approve";
        public const string Reject     = "reject";
        public const string Suspend    = "suspend";
        public const string Restore    = "restore";
        public const string Export     = "export";
        public const string Import     = "import";
        public const string Assign     = "assign";
        public const string Feature    = "feature";
        public const string Moderate   = "moderate";
        public const string Refund     = "refund";
        public const string Cancel     = "cancel";
        public const string Reschedule = "reschedule";
        public const string Chat       = "chat";
        public const string Book       = "book";
        public const string Publish    = "publish";
        public const string Unpublish  = "unpublish";
    }

    // -- Per-module surface -------------------------------------------------------
    public static class Users
    {
        public const string View    = "users.view";
        public const string Create  = "users.create";
        public const string Edit    = "users.edit";
        public const string Delete  = "users.delete";
        public const string Suspend = "users.suspend";
        public const string Restore = "users.restore";
        public const string Export  = "users.export";
        public const string Import  = "users.import";
        public const string Assign  = "users.assign";
    }

    public static class Posts
    {
        public const string View      = "posts.view";
        public const string Create    = "posts.create";
        public const string Edit      = "posts.edit";
        public const string Delete    = "posts.delete";
        public const string Moderate  = "posts.moderate";
        public const string Feature   = "posts.feature";
        public const string Publish   = "posts.publish";
        public const string Unpublish = "posts.unpublish";
    }

    public static class Comments
    {
        public const string View     = "comments.view";
        public const string Delete   = "comments.delete";
        public const string Moderate = "comments.moderate";
    }

    public static class Adoption
    {
        public const string View      = "adoption.view";
        public const string Create    = "adoption.create";
        public const string Edit      = "adoption.edit";
        public const string Delete    = "adoption.delete";
        public const string Approve   = "adoption.approve";
        public const string Reject    = "adoption.reject";
        public const string Feature   = "adoption.feature";
        public const string Publish   = "adoption.publish";
        public const string Unpublish = "adoption.unpublish";
        public const string Moderate  = "adoption.moderate";
    }

    public static class AdoptionRequests
    {
        public const string View   = "adoption_requests.view";
        public const string Create = "adoption_requests.create";
        public const string Cancel = "adoption_requests.cancel";
        public const string Chat   = "adoption_requests.chat";
    }

    public static class Pets
    {
        public const string View   = "pets.view";
        public const string Create = "pets.create";
        public const string Edit   = "pets.edit";
        public const string Delete = "pets.delete";
    }

    public static class Vets
    {
        public const string View    = "vets.view";
        public const string Edit    = "vets.edit";
        public const string Approve = "vets.approve";
        public const string Reject  = "vets.reject";
        public const string Suspend = "vets.suspend";
        public const string Restore = "vets.restore";
        public const string Feature = "vets.feature";
    }

    public static class Appointments
    {
        public const string View       = "appointments.view";
        public const string Create     = "appointments.create";
        public const string Cancel     = "appointments.cancel";
        public const string Reschedule = "appointments.reschedule";
        public const string Chat       = "appointments.chat";
    }

    public static class Prescriptions
    {
        public const string View   = "prescriptions.view";
        public const string Create = "prescriptions.create";
        public const string Edit   = "prescriptions.edit";
    }

    public static class Stores
    {
        public const string View    = "stores.view";
        public const string Edit    = "stores.edit";
        public const string Approve = "stores.approve";
        public const string Reject  = "stores.reject";
        public const string Suspend = "stores.suspend";
        public const string Restore = "stores.restore";
        public const string Feature = "stores.feature";
        public const string Refund  = "stores.refund";
    }

    public static class Sellers
    {
        public const string View    = "sellers.view";
        public const string Approve = "sellers.approve";
        public const string Reject  = "sellers.reject";
        public const string Suspend = "sellers.suspend";
    }

    public static class Products
    {
        public const string View      = "products.view";
        public const string Create    = "products.create";
        public const string Edit      = "products.edit";
        public const string Delete    = "products.delete";
        public const string Feature   = "products.feature";
        public const string Publish   = "products.publish";
        public const string Unpublish = "products.unpublish";
        public const string Import    = "products.import";
        public const string Export    = "products.export";
    }

    public static class Orders
    {
        public const string View   = "orders.view";
        public const string Refund = "orders.refund";
        public const string Cancel = "orders.cancel";
        public const string Export = "orders.export";
    }

    public static class Delivery
    {
        public const string View   = "delivery.view";
        public const string Edit   = "delivery.edit";
        public const string Assign = "delivery.assign";
    }

    public static class Services
    {
        public const string View    = "services.view";
        public const string Approve = "services.approve";
        public const string Reject  = "services.reject";
        public const string Suspend = "services.suspend";
        public const string Book    = "services.book";
        public const string Cancel  = "services.cancel";
    }

    public static class Breeders
    {
        public const string View    = "breeders.view";
        public const string Approve = "breeders.approve";
        public const string Reject  = "breeders.reject";
        public const string Suspend = "breeders.suspend";
    }

    public static class AdoptionCenters
    {
        public const string View    = "adoption_centers.view";
        public const string Approve = "adoption_centers.approve";
        public const string Reject  = "adoption_centers.reject";
        public const string Suspend = "adoption_centers.suspend";
    }

    public static class Reviews
    {
        public const string View     = "reviews.view";
        public const string Delete   = "reviews.delete";
        public const string Moderate = "reviews.moderate";
    }

    public static class Messaging
    {
        public const string View     = "messaging.view";
        public const string Chat     = "messaging.chat";
        public const string Delete   = "messaging.delete";
        public const string Moderate = "messaging.moderate";
    }

    public static class Notifications
    {
        public const string View   = "notifications.view";
        public const string Create = "notifications.create";
        public const string Delete = "notifications.delete";
    }

    public static class Roles
    {
        public const string View   = "roles.view";
        public const string Create = "roles.create";
        public const string Edit   = "roles.edit";
        public const string Delete = "roles.delete";
        public const string Assign = "roles.assign";
    }

    public static class PermissionsAdmin
    {
        public const string View   = "permissions.view";
        public const string Assign = "permissions.assign";
    }

    public static class Reports
    {
        public const string View     = "reports.view";
        public const string Moderate = "reports.moderate";
        public const string Export   = "reports.export";
    }

    public static class Audit
    {
        public const string View   = "audit.view";
        public const string Export = "audit.export";
    }

    public static class Settings
    {
        public const string View = "settings.view";
        public const string Edit = "settings.edit";
    }

    public static class Moderation
    {
        public const string View     = "moderation.view";
        public const string Moderate = "moderation.moderate";
        public const string Approve  = "moderation.approve";
        public const string Reject   = "moderation.reject";
    }

    /// <summary>
    /// Discovers every (module, action) declared above via reflection.
    /// The seeder uses this to mirror code -> DB on startup.
    /// </summary>
    public static IEnumerable<(string Module, string Action)> All()
    {
        var nestedTypes = typeof(Permissions).GetNestedTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed   // static
                     && t.Name != nameof(Actions));
        foreach (var t in nestedTypes)
        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (f.GetValue(null) is not string code) continue;
            var parts = code.Split('.', 2);
            if (parts.Length != 2) continue;
            yield return (parts[0], parts[1]);
        }
    }
}

public static class SystemRoles
{
    public const string SuperAdmin      = "SuperAdmin";
    public const string Admin           = "Admin";
    public const string Moderator       = "Moderator";
    public const string SupportAgent    = "SupportAgent";
    public const string Veterinarian    = "Veterinarian";
    public const string StoreOwner      = "StoreOwner";
    public const string Seller          = "Seller";
    public const string ServiceProvider = "ServiceProvider";
    public const string Breeder         = "Breeder";
    public const string AdoptionCenter  = "AdoptionCenter";
    public const string DeliveryUser    = "DeliveryUser";
    public const string User            = "User";  // a.k.a. Pet Owner

    public static readonly string[] All =
    {
        SuperAdmin, Admin, Moderator, SupportAgent, Veterinarian,
        StoreOwner, Seller, ServiceProvider, Breeder, AdoptionCenter,
        DeliveryUser, User
    };
}
