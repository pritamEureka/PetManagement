using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Shared.Constants;

namespace Pawzaroo.Application.Modules
{
    /// <summary>
    /// Lightweight registrations for each domain module. Handlers / validators /
    /// services for a module live under Modules/{Name}/Features/* and are
    /// registered from the module's Register() method. New modules get added
    /// here once.
    /// </summary>
    public static class ModuleRegistry
    {
        public static IServiceCollection AddModules(this IServiceCollection services)
        {
            IModule[] modules =
            {
                new Identity.IdentityModule(),
                new UserProfile.UserProfileModule(),
                new PetProfile.PetProfileModule(),
                new Feed.FeedModule(),
                new Adoption.AdoptionModule(),
                new Messaging.MessagingModule(),
                new VetDoctor.VetDoctorModule(),
                new Appointment.AppointmentModule(),
                new Store.StoreModule(),
                new Marketplace.MarketplaceModule(),
                new Cart.CartModule(),
                new Order.OrderModule(),
                new Payment.PaymentModule(),
                new Review.ReviewModule(),
                new Notification.NotificationModule(),
                new AdminApproval.AdminApprovalModule(),
                new RBAC.RbacModule(),
                new Media.MediaModule(),
                new AuditLog.AuditLogModule(),
                new Reports.ReportsModule()
            };
            foreach (var m in modules) m.Register(services);
            return services;
        }
    }
}

namespace Pawzaroo.Application.Modules.UserProfile   { public class UserProfileModule   : IModule { public string Name => ModuleNames.UserProfile;   public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.PetProfile    { public class PetProfileModule    : IModule { public string Name => ModuleNames.PetProfile;    public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Feed          { public class FeedModule          : IModule { public string Name => ModuleNames.Feed;          public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Adoption      { public class AdoptionModule      : IModule { public string Name => ModuleNames.Adoption;      public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Messaging     { public class MessagingModule     : IModule { public string Name => ModuleNames.Messaging;     public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.VetDoctor     { public class VetDoctorModule     : IModule { public string Name => ModuleNames.VetDoctor;     public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Appointment   { public class AppointmentModule   : IModule { public string Name => ModuleNames.Appointment;   public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Store         { public class StoreModule         : IModule { public string Name => ModuleNames.Store;         public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Marketplace
{
    // Validators live in Modules/Marketplace/Validators; the Application-wide
    // assembly scan in DependencyInjection.AddValidatorsFromAssembly picks
    // them up automatically, so this module's Register stays empty.
    public class MarketplaceModule : IModule
    {
        public string Name => ModuleNames.Marketplace;
        public IServiceCollection Register(IServiceCollection s) => s;
    }
}
namespace Pawzaroo.Application.Modules.Cart          { public class CartModule          : IModule { public string Name => ModuleNames.Cart;          public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Order         { public class OrderModule         : IModule { public string Name => ModuleNames.Order;         public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Payment       { public class PaymentModule       : IModule { public string Name => ModuleNames.Payment;       public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Review        { public class ReviewModule        : IModule { public string Name => ModuleNames.Review;        public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Notification  { public class NotificationModule  : IModule { public string Name => ModuleNames.Notification;  public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.AdminApproval { public class AdminApprovalModule : IModule { public string Name => ModuleNames.AdminApproval; public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.RBAC          { public class RbacModule          : IModule { public string Name => ModuleNames.RBAC;          public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Media         { public class MediaModule         : IModule { public string Name => ModuleNames.Media;         public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.AuditLog      { public class AuditLogModule      : IModule { public string Name => ModuleNames.AuditLog;      public IServiceCollection Register(IServiceCollection s) => s; } }
namespace Pawzaroo.Application.Modules.Reports       { public class ReportsModule       : IModule { public string Name => ModuleNames.Reports;       public IServiceCollection Register(IServiceCollection s) => s; } }
