using Microsoft.EntityFrameworkCore;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserProfile> UserProfiles { get; }

    // Pets
    DbSet<Pet> Pets { get; }
    DbSet<PetPhoto> PetPhotos { get; }
    DbSet<VaccinationRecord> VaccinationRecords { get; }
    DbSet<MedicalRecord> MedicalRecords { get; }
    DbSet<GroomingRecord> GroomingRecords { get; }

    // Social
    DbSet<Post> Posts { get; }
    DbSet<PostMedia> PostMedia { get; }
    DbSet<Hashtag> Hashtags { get; }
    DbSet<PostHashtag> PostHashtags { get; }
    DbSet<PostPetTag> PostPetTags { get; }
    DbSet<Comment> Comments { get; }
    DbSet<CommentReaction> CommentReactions { get; }
    DbSet<Reaction> Reactions { get; }
    DbSet<PostShare> PostShares { get; }
    DbSet<PostSave> PostSaves { get; }
    DbSet<PostReport> PostReports { get; }
    DbSet<Follow> Follows { get; }

    // Adoption
    DbSet<AdoptionListing> AdoptionListings { get; }
    DbSet<AdoptionListingPhoto> AdoptionListingPhotos { get; }
    DbSet<AdoptionRequest> AdoptionRequests { get; }
    DbSet<AdoptionWantedPost> AdoptionWantedPosts { get; }
    DbSet<SavedAdoptionListing> SavedAdoptionListings { get; }

    // Messaging
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationParticipant> ConversationParticipants { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }
    DbSet<MessageReadReceipt> MessageReadReceipts { get; }
    DbSet<UserBlock> UserBlocks { get; }
    DbSet<MessageReport> MessageReports { get; }

    // Vet
    DbSet<Doctor> Doctors { get; }
    DbSet<DoctorAnimalType> DoctorAnimalTypes { get; }
    DbSet<DoctorAvailability> DoctorAvailabilities { get; }
    DbSet<DoctorHoliday> DoctorHolidays { get; }
    DbSet<DoctorTimeSlot> DoctorTimeSlots { get; }
    DbSet<DoctorCredentialDocument> DoctorCredentialDocuments { get; }
    DbSet<Specialty> Specialties { get; }
    DbSet<DoctorSpecialty> DoctorSpecialties { get; }
    DbSet<Prescription> Prescriptions { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentDispute> AppointmentDisputes { get; }
    DbSet<DoctorReview> DoctorReviews { get; }

    // Store
    DbSet<Store> Stores { get; }
    DbSet<StoreOwnerProfile> StoreOwnerProfiles { get; }
    DbSet<StoreDocument> StoreDocuments { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductReview> ProductReviews { get; }
    DbSet<StoreReview> StoreReviews { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<InventoryAdjustment> InventoryAdjustments { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<ReturnRequest> ReturnRequests { get; }
    DbSet<CommissionConfiguration> CommissionConfigurations { get; }
    DbSet<ShippingAddress> ShippingAddresses { get; }

    // Services
    DbSet<ServiceProviderProfile> ServiceProviders { get; }
    DbSet<ServiceBooking> ServiceBookings { get; }
    DbSet<ServiceReview> ServiceReviews { get; }

    // Cross-cutting
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<InAppNotification> Notifications { get; }
    DbSet<ContentReport> ContentReports { get; }
    DbSet<ModerationAction> ModerationActions { get; }
    DbSet<AdminActionLog> AdminActionLogs { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    // Identity / security
    DbSet<UserDevice> UserDevices { get; }
    DbSet<UserSuspension> UserSuspensions { get; }
    DbSet<UserWarning> UserWarnings { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<TwoFactorSettings> TwoFactorSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
