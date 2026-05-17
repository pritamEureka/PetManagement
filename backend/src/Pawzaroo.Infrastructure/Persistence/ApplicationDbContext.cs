using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    // Pets
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<PetPhoto> PetPhotos => Set<PetPhoto>();
    public DbSet<VaccinationRecord> VaccinationRecords => Set<VaccinationRecord>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<GroomingRecord> GroomingRecords => Set<GroomingRecord>();

    // Social
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<Hashtag> Hashtags => Set<Hashtag>();
    public DbSet<PostHashtag> PostHashtags => Set<PostHashtag>();
    public DbSet<PostPetTag> PostPetTags => Set<PostPetTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<PostShare> PostShares => Set<PostShare>();
    public DbSet<PostSave> PostSaves => Set<PostSave>();
    public DbSet<PostReport> PostReports => Set<PostReport>();
    public DbSet<Follow> Follows => Set<Follow>();

    // Adoption
    public DbSet<AdoptionListing> AdoptionListings => Set<AdoptionListing>();
    public DbSet<AdoptionListingPhoto> AdoptionListingPhotos => Set<AdoptionListingPhoto>();
    public DbSet<AdoptionRequest> AdoptionRequests => Set<AdoptionRequest>();
    public DbSet<AdoptionWantedPost> AdoptionWantedPosts => Set<AdoptionWantedPost>();
    public DbSet<SavedAdoptionListing> SavedAdoptionListings => Set<SavedAdoptionListing>();

    // Messaging
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageReadReceipt> MessageReadReceipts => Set<MessageReadReceipt>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<MessageReport> MessageReports => Set<MessageReport>();

    // Vet
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorAnimalType> DoctorAnimalTypes => Set<DoctorAnimalType>();
    public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
    public DbSet<DoctorHoliday> DoctorHolidays => Set<DoctorHoliday>();
    public DbSet<DoctorTimeSlot> DoctorTimeSlots => Set<DoctorTimeSlot>();
    public DbSet<DoctorCredentialDocument> DoctorCredentialDocuments => Set<DoctorCredentialDocument>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorSpecialty> DoctorSpecialties => Set<DoctorSpecialty>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentDispute> AppointmentDisputes => Set<AppointmentDispute>();
    public DbSet<DoctorReview> DoctorReviews => Set<DoctorReview>();

    // Store
    public DbSet<Domain.Store.Store> Stores => Set<Domain.Store.Store>();
    public DbSet<StoreOwnerProfile> StoreOwnerProfiles => Set<StoreOwnerProfile>();
    public DbSet<StoreDocument> StoreDocuments => Set<StoreDocument>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<StoreReview> StoreReviews => Set<StoreReview>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<CommissionConfiguration> CommissionConfigurations => Set<CommissionConfiguration>();
    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();

    // Services
    public DbSet<ServiceProviderProfile> ServiceProviders => Set<ServiceProviderProfile>();
    public DbSet<ServiceBooking> ServiceBookings => Set<ServiceBooking>();
    public DbSet<ServiceReview> ServiceReviews => Set<ServiceReview>();

    // Cross-cutting
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<AdminActionLog> AdminActionLogs => Set<AdminActionLog>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Identity / security
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserSuspension> UserSuspensions => Set<UserSuspension>();
    public DbSet<UserWarning> UserWarnings => Set<UserWarning>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<TwoFactorSettings> TwoFactorSettings => Set<TwoFactorSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("uuid-ossp");
        b.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global soft-delete filter for every AuditableEntity.
        foreach (var et in b.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(et.ClrType))
            {
                var param = System.Linq.Expressions.Expression.Parameter(et.ClrType, "e");
                var prop = System.Linq.Expressions.Expression.Property(param, nameof(AuditableEntity.IsDeleted));
                var notDeleted = System.Linq.Expressions.Expression.Not(prop);
                var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, param);
                b.Entity(et.ClrType).HasQueryFilter(lambda);
            }
        }

        base.OnModelCreating(b);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var actor = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
