using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
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

namespace Pawzaroo.Infrastructure.Persistence.Seed;

public static class DemoDataSeeder
{
    private const string Password = "PawzarooDemo!2026";
    private const string AssetRoot = "https://images.unsplash.com";

    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == "maya.rahman@example.com", ct))
            return;

        var now = DateTime.UtcNow;
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, ct);
        var categories = await db.ProductCategories.ToDictionaryAsync(c => c.Slug, ct);
        var brands = await db.Brands.ToDictionaryAsync(b => b.Name, ct);
        var specialties = await db.Specialties.ToDictionaryAsync(s => s.Slug, ct);
        var passwordHash = hasher.Hash(Password);

        var admin = User("admin.farhana@example.com", "Farhana Chowdhury", "farhana.admin", "Dhaka", "Operations lead reviewing approvals, reports, and marketplace activity.", "+8801711001100", now.AddDays(-11), passwordHash);
        var moderator = User("samir.haque@example.com", "Samir Haque", "samir.moderates", "Chattogram", "Community moderator with a soft spot for rescue stories.", "+8801812002200", now.AddDays(-7), passwordHash);
        var support = User("nabila.support@example.com", "Nabila Karim", "nabila.support", "Sylhet", "Support specialist helping pet parents resolve orders and bookings.", "+8801913003300", now.AddDays(-2), passwordHash);
        var maya = User("maya.rahman@example.com", "Maya Rahman", "maya.and.milo", "Dhanmondi, Dhaka", "Cat parent, foster volunteer, and weekend plant collector.", "+8801714550101", now.AddHours(-5), passwordHash);
        var arif = User("arif.hasan@example.com", "Arif Hasan", "arif.with.buddy", "Banani, Dhaka", "New dog dad learning training routines one walk at a time.", "+8801814550202", now.AddHours(-12), passwordHash);
        var liza = User("liza.akter@example.com", "Liza Akter", "liza.rescues", "Mirpur, Dhaka", "Coordinates neighborhood rescues and adoption drives.", "+8801914550303", now.AddDays(-1), passwordHash);
        var doctorUser = User("dr.nusrat@example.com", "Dr. Nusrat Jahan", "dr.nusrat", "Gulshan, Dhaka", "Small animal veterinarian focused on dermatology and preventive care.", "+8801614550404", now.AddHours(-8), passwordHash);
        var storeUser = User("tanvir.petmart@example.com", "Tanvir Ahmed", "tanvir.petmart", "Uttara, Dhaka", "Runs a family pet supplies store with same-day local delivery.", "+8801514550505", now.AddHours(-14), passwordHash);
        var groomerUser = User("rima.grooming@example.com", "Rima Sultana", "rima.grooms", "Mohammadpur, Dhaka", "Mobile grooming provider for anxious pets and senior cats.", "+8801314550606", now.AddDays(-3), passwordHash);
        var breederUser = User("omar.breeder@example.com", "Omar Faruq", "omar.farm", "Savar, Dhaka", "Responsible breeder and farm-animal caretaker.", "+8801414550707", now.AddDays(-6), passwordHash);
        var suspendedUser = User("rakib.review@example.com", "Rakib Islam", "rakib.review", "Dhaka", "Marketplace buyer under review after repeated report activity.", "+8801714550808", now.AddDays(-20), passwordHash, isSuspended: true);

        var users = new[] { admin, moderator, support, maya, arif, liza, doctorUser, storeUser, groomerUser, breederUser, suspendedUser };
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        AddRole(db, roles, admin, SystemRoles.Admin);
        AddRole(db, roles, moderator, SystemRoles.Moderator);
        AddRole(db, roles, support, SystemRoles.SupportAgent);
        AddRole(db, roles, maya, SystemRoles.User);
        AddRole(db, roles, arif, SystemRoles.User);
        AddRole(db, roles, liza, SystemRoles.AdoptionCenter);
        AddRole(db, roles, doctorUser, SystemRoles.Veterinarian);
        AddRole(db, roles, storeUser, SystemRoles.StoreOwner);
        AddRole(db, roles, groomerUser, SystemRoles.ServiceProvider);
        AddRole(db, roles, breederUser, SystemRoles.Breeder);
        AddRole(db, roles, suspendedUser, SystemRoles.User);

        db.UserProfiles.AddRange(
            Profile(maya, "Maya", "Rahman", "Dhanmondi 8/A", "Dhaka", "Dhaka", "1209", "BDT", new { notifications = new { adoption = true, orders = true }, home = "apartment" }),
            Profile(arif, "Arif", "Hasan", "Road 11, Banani", "Dhaka", "Dhaka", "1213", "BDT", new { notifications = new { appointments = true }, preferredPets = new[] { "dog" } }),
            Profile(liza, "Liza", "Akter", "Section 6, Mirpur", "Dhaka", "Dhaka", "1216", "BDT", new { rescueVolunteer = true, transport = "weekends" }),
            Profile(doctorUser, "Nusrat", "Jahan", "Gulshan Avenue", "Dhaka", "Dhaka", "1212", "BDT", new { clinic = "Paws & Pulse Veterinary Care" }),
            Profile(storeUser, "Tanvir", "Ahmed", "Sector 7, Uttara", "Dhaka", "Dhaka", "1230", "BDT", new { storePickup = true }),
            Profile(groomerUser, "Rima", "Sultana", "Tajmahal Road", "Dhaka", "Dhaka", "1207", "BDT", new { mobileService = true }),
            Profile(breederUser, "Omar", "Faruq", "Hemayetpur", "Savar", "Dhaka", "1340", "BDT", new { animalTypes = new[] { "goat", "dog" } })
        );

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = maya.Id,
            TokenHash = "demo-refresh-token-maya-2026",
            ExpiresAt = now.AddDays(21),
            CreatedAt = now.AddDays(-2),
            CreatedByIp = "103.106.239.22"
        });

        db.UserDevices.AddRange(
            Device(maya, "maya-chrome-pixel-8", "Chrome on Pixel 8", "Dhaka", true, now.AddDays(-12), now.AddHours(-4)),
            Device(arif, "arif-edge-windows", "Edge on Windows Laptop", "Dhaka", true, now.AddDays(-9), now.AddHours(-11)),
            Device(storeUser, "tanvir-shop-tablet", "Chrome on Shop Tablet", "Dhaka", true, now.AddDays(-18), now.AddHours(-2))
        );

        db.TwoFactorSettings.Add(new TwoFactorSettings
        {
            UserId = admin.Id,
            IsEnabled = true,
            EncryptedSecret = "demo:v1:encrypted-totp-seed",
            RecoveryCodesHash = JsonSerializer.Serialize(new[] { "hash:9f2a", "hash:73bc" }),
            EnabledAt = now.AddDays(-10)
        });

        db.OtpCodes.Add(new OtpCode
        {
            UserId = arif.Id,
            Purpose = OtpPurpose.PasswordReset,
            CodeHash = "demo-password-reset-code-hash",
            Destination = arif.Email,
            ExpiresAt = now.AddMinutes(25),
            Attempts = 1
        });

        var milo = Pet(maya, "Milo", AnimalType.Cat, "Domestic Shorthair", Gender.Male, now.AddYears(-3), 4.8m, "Grey tabby", "CAT-DHK-0142", true);
        var buddy = Pet(arif, "Buddy", AnimalType.Dog, "Golden Retriever", Gender.Male, now.AddYears(-2), 27.4m, "Golden", "DOG-BAN-2081", false);
        var luna = Pet(liza, "Luna", AnimalType.Cat, "Calico", Gender.Female, now.AddMonths(-14), 3.7m, "Calico", "RES-MIR-0097", true);
        var koko = Pet(breederUser, "Koko", AnimalType.Goat, "Black Bengal", Gender.Female, now.AddYears(-1), 19.2m, "Black", "FARM-SVR-0204", false);
        db.Pets.AddRange(milo, buddy, luna, koko);

        db.PetPhotos.AddRange(
            Photo(milo, $"{AssetRoot}/photo-1574144611937-0df059b5ef3e?auto=format&fit=crop&w=900&q=80", "Milo keeping watch from the window."),
            Photo(buddy, $"{AssetRoot}/photo-1552053831-71594a27632d?auto=format&fit=crop&w=900&q=80", "Buddy after his morning walk."),
            Photo(luna, $"{AssetRoot}/photo-1589883661923-6476cb0ae9f2?auto=format&fit=crop&w=900&q=80", "Luna resting after vaccination.")
        );
        db.VaccinationRecords.AddRange(
            new VaccinationRecord { Pet = milo, VaccineName = "FVRCP Booster", AdministeredOn = now.AddMonths(-5), NextDueOn = now.AddMonths(7), AdministeredByVet = "Dr. Nusrat Jahan", Notes = "No adverse reaction." },
            new VaccinationRecord { Pet = buddy, VaccineName = "Rabies", AdministeredOn = now.AddMonths(-8), NextDueOn = now.AddMonths(4), AdministeredByVet = "Paws & Pulse Veterinary Care", Notes = "Annual booster." }
        );
        db.GroomingRecords.Add(new GroomingRecord { Pet = buddy, PerformedOn = now.AddDays(-16), ServiceType = "Full bath and coat trim", Provider = "Rima's Gentle Grooming", Notes = "Used sensitive skin shampoo." });

        var doctor = new Doctor
        {
            User = doctorUser,
            LicenseNumber = "DVM-BVC-2016-4472",
            Specialty = "Dermatology",
            ExperienceYears = 10,
            About = "Experienced in itchy-skin cases, nutrition plans, vaccination schedules, and senior pet wellness.",
            ClinicName = "Paws & Pulse Veterinary Care",
            ClinicAddress = "House 21, Road 44, Gulshan 2",
            City = "Dhaka",
            Country = "Bangladesh",
            ConsultationFee = 1200m,
            ConsultationType = ConsultationType.Both,
            OnlineAvailable = true,
            OfflineAvailable = true,
            ApprovalStatus = ApprovalStatus.Approved,
            RatingAverage = 4.8m,
            RatingCount = 18,
            AutoConfirmAppointments = true
        };
        db.Doctors.Add(doctor);
        db.DoctorAnimalTypes.AddRange(
            new DoctorAnimalType { Doctor = doctor, AnimalType = AnimalType.Cat },
            new DoctorAnimalType { Doctor = doctor, AnimalType = AnimalType.Dog },
            new DoctorAnimalType { Doctor = doctor, AnimalType = AnimalType.Rabbit }
        );
        db.DoctorSpecialties.AddRange(
            new DoctorSpecialty { Doctor = doctor, Specialty = specialties["general"] },
            new DoctorSpecialty { Doctor = doctor, Specialty = specialties["dermatology"] }
        );
        db.DoctorAvailabilities.AddRange(
            Availability(doctor, DayOfWeek.Sunday, 9, 13, ConsultationType.Offline),
            Availability(doctor, DayOfWeek.Tuesday, 15, 19, ConsultationType.Both),
            Availability(doctor, DayOfWeek.Thursday, 10, 14, ConsultationType.Online)
        );
        db.DoctorHolidays.Add(new DoctorHoliday { Doctor = doctor, Date = DateOnly.FromDateTime(now.AddDays(12)), Reason = "Veterinary conference" });
        db.DoctorCredentialDocuments.Add(new DoctorCredentialDocument
        {
            Doctor = doctor,
            Kind = CredentialKind.License,
            Title = "Bangladesh Veterinary Council License",
            FileUrl = "/uploads/credentials/nusrat-bvc-license.pdf",
            IssuingAuthority = "Bangladesh Veterinary Council",
            DocumentNumber = "BVC-2016-4472",
            IssuedOn = new DateOnly(2016, 7, 12),
            ExpiresOn = new DateOnly(2027, 7, 11),
            Verified = true,
            VerifiedAt = now.AddDays(-9),
            VerifiedByUser = admin
        });

        var slotBooked = Slot(doctor, now.AddDays(2).Date.AddHours(10), ConsultationType.Offline, SlotStatus.Booked);
        var slotOpen = Slot(doctor, now.AddDays(3).Date.AddHours(16), ConsultationType.Online, SlotStatus.Available);
        db.DoctorTimeSlots.AddRange(slotBooked, slotOpen);
        var completedAppointment = new Appointment
        {
            Doctor = doctor,
            PatientUser = maya,
            Pet = milo,
            ScheduledAt = now.AddDays(-21).Date.AddHours(11),
            DurationMinutes = 30,
            Type = ConsultationType.Offline,
            Status = AppointmentStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 1200m,
            Symptoms = "Excessive scratching around neck and ears.",
            FollowUpNotes = "Improved after diet adjustment and topical treatment.",
            CompletedAt = now.AddDays(-21).Date.AddHours(11).AddMinutes(35)
        };
        var upcomingAppointment = new Appointment
        {
            Doctor = doctor,
            PatientUser = arif,
            Pet = buddy,
            TimeSlot = slotBooked,
            ScheduledAt = slotBooked.StartUtc,
            DurationMinutes = 30,
            Type = ConsultationType.Offline,
            Status = AppointmentStatus.Confirmed,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 1200m,
            Symptoms = "Limping lightly after park play.",
            ConfirmedAt = now.AddDays(-1)
        };
        db.Appointments.AddRange(completedAppointment, upcomingAppointment);
        slotBooked.Appointment = upcomingAppointment;

        db.MedicalRecords.Add(new MedicalRecord
        {
            Pet = milo,
            Appointment = completedAppointment,
            VisitDate = completedAppointment.ScheduledAt,
            Diagnosis = "Mild flea allergy dermatitis",
            Treatment = "Topical flea prevention and omega-3 supplement.",
            PrescriptionFileUrl = "/uploads/prescriptions/milo-dermatitis.pdf",
            Notes = "Review skin condition in four weeks."
        });
        db.Prescriptions.Add(new Prescription
        {
            Appointment = completedAppointment,
            IssuedBy = doctor,
            IssuedAt = completedAppointment.ScheduledAt.AddMinutes(32),
            Notes = "Apply only to clean, dry skin.",
            FileUrl = "/uploads/prescriptions/milo-dermatitis.pdf",
            ItemsJson = JsonSerializer.Serialize(new[]
            {
                new { drug = "FleaShield spot-on", dose = "1 pipette", frequency = "once monthly", duration = "3 months", instructions = "Apply between shoulder blades." },
                new { drug = "Omega pet oil", dose = "2 ml", frequency = "daily", duration = "30 days", instructions = "Mix with food." }
            }),
            ValidUntil = now.AddMonths(2)
        });
        db.DoctorReviews.Add(new DoctorReview { Doctor = doctor, User = maya, Appointment = completedAppointment, Rating = 5, Comment = "Dr. Nusrat explained the treatment clearly and Milo recovered quickly." });
        db.AppointmentDisputes.Add(new AppointmentDispute
        {
            Appointment = completedAppointment,
            OpenedByUser = maya,
            Reason = "Prescription upload delay",
            Details = "File was unavailable for a few hours after the visit.",
            Status = AppointmentDisputeStatus.Resolved,
            ResolvedByUser = support,
            ResolvedAt = now.AddDays(-20),
            Resolution = "Prescription was re-uploaded and confirmed by the patient."
        });

        var ownerProfile = new StoreOwnerProfile
        {
            User = storeUser,
            LegalName = "Tanvir Ahmed",
            BusinessName = "Green Paw Supplies",
            TradeLicenseNumber = "TRAD/DHK/2024/88121",
            NationalIdNumber = "19901234567000123",
            TaxId = "TIN-7845129630",
            TradeLicenseDocUrl = "/uploads/store-kyc/green-paw-trade-license.pdf",
            NationalIdDocUrl = "/uploads/store-kyc/tanvir-nid.pdf",
            AddressProofDocUrl = "/uploads/store-kyc/green-paw-utility.pdf",
            KycStatus = ApprovalStatus.Approved,
            SubmittedAt = now.AddDays(-19),
            DecidedAt = now.AddDays(-17),
            AdminNotes = "Documents match submitted store address."
        };
        db.StoreOwnerProfiles.Add(ownerProfile);
        db.StoreDocuments.AddRange(
            new StoreDocument { StoreOwnerProfile = ownerProfile, Type = StoreDocumentType.TradeLicense, FileName = "trade-license.pdf", Url = ownerProfile.TradeLicenseDocUrl!, Notes = "Valid through 2027." },
            new StoreDocument { StoreOwnerProfile = ownerProfile, Type = StoreDocumentType.AddressProof, FileName = "utility-bill.pdf", Url = ownerProfile.AddressProofDocUrl!, Notes = "Shop electricity bill." }
        );
        var store = new Store
        {
            OwnerUser = storeUser,
            Name = "Green Paw Supplies",
            Description = "Curated food, litter, grooming, and enrichment products with Dhaka city delivery.",
            LogoUrl = "/uploads/stores/green-paw-logo.png",
            BannerUrl = "/uploads/stores/green-paw-banner.jpg",
            Address = "Shop 14, Sector 7 Market",
            City = "Dhaka",
            Country = "Bangladesh",
            PhoneNumber = "+8801514550505",
            Email = "orders@greenpaw.example.com",
            ApprovalStatus = ApprovalStatus.Approved,
            CommissionPercent = 9.5m
        };
        db.Stores.Add(store);

        var catFood = Product(store, categories["food"], brands["Royal Tail"], "Royal Tail Indoor Cat Chicken 2kg", "Complete dry food for adult indoor cats with hairball support.", "GPS-FOOD-CAT-2KG", 2450m, 2290m, 42, true, 4.7m, 31);
        var leash = Product(store, categories["accessories"], brands["Pawzaroo Basics"], "Reflective Comfort Dog Leash", "Padded handle leash with reflective stitching for evening walks.", "GPS-ACC-LEASH-RF", 850m, null, 24, true, 4.5m, 12);
        var shampoo = Product(store, categories["grooming"], brands["FurNation"], "Sensitive Skin Oatmeal Shampoo", "Gentle oatmeal shampoo for itchy or dry coats.", "GPS-GRM-OAT-500", 690m, 640m, 36, false, 4.6m, 9);
        db.Products.AddRange(catFood, leash, shampoo);
        db.ProductImages.AddRange(
            Image(catFood, $"{AssetRoot}/photo-1589924691995-400dc9ecc119?auto=format&fit=crop&w=900&q=80"),
            Image(leash, $"{AssetRoot}/photo-1601758124510-52d02ddb7cbd?auto=format&fit=crop&w=900&q=80"),
            Image(shampoo, $"{AssetRoot}/photo-1616190264687-b7ebf1a391cd?auto=format&fit=crop&w=900&q=80")
        );

        db.CommissionConfigurations.AddRange(
            new CommissionConfiguration { Scope = CommissionScope.Global, CommissionPercent = 10m, EffectiveFrom = now.AddMonths(-3), Notes = "Standard marketplace commission." },
            new CommissionConfiguration { Scope = CommissionScope.Store, Store = store, CommissionPercent = 9.5m, EffectiveFrom = now.AddDays(-17), Notes = "Approved store launch rate." },
            new CommissionConfiguration { Scope = CommissionScope.Category, Category = categories["medicine"], CommissionPercent = 8m, EffectiveFrom = now.AddMonths(-1), Notes = "Lower rate for medicine and supplements." }
        );

        db.ShippingAddresses.AddRange(
            new ShippingAddress { User = maya, Label = "Home", RecipientName = "Maya Rahman", PhoneNumber = maya.PhoneNumber!, AddressLine1 = "House 42, Road 8/A", City = "Dhaka", State = "Dhaka", Country = "Bangladesh", PostalCode = "1209", IsDefault = true },
            new ShippingAddress { User = arif, Label = "Apartment", RecipientName = "Arif Hasan", PhoneNumber = arif.PhoneNumber!, AddressLine1 = "Block C, Road 11, Banani", City = "Dhaka", State = "Dhaka", Country = "Bangladesh", PostalCode = "1213", IsDefault = true }
        );
        var cart = new Cart { User = arif, Currency = "BDT", Status = CartStatus.Active };
        db.Carts.Add(cart);
        db.CartItems.AddRange(
            new CartItem { Cart = cart, User = arif, Product = leash, Quantity = 1, UnitPriceSnapshot = leash.Price, AddedAt = now.AddHours(-3) },
            new CartItem { Cart = cart, User = arif, Product = shampoo, Quantity = 2, UnitPriceSnapshot = shampoo.DiscountPrice!.Value, AddedAt = now.AddHours(-2) }
        );

        var order = new Order
        {
            User = maya,
            OrderNumber = "PZ-202605-1001",
            Subtotal = 2290m + 640m,
            ShippingFee = 80m,
            Tax = 0m,
            Total = 3010m,
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Paid,
            ShipmentStatus = ShipmentStatus.Delivered,
            ShippingAddress = "House 42, Road 8/A",
            ShippingCity = "Dhaka",
            ShippingCountry = "Bangladesh",
            TrackingNumber = "ECO-DHK-447201"
        };
        var orderItem1 = new OrderItem { Order = order, Product = catFood, Store = store, Quantity = 1, UnitPrice = 2290m, Total = 2290m, CommissionAmount = 217.55m };
        var orderItem2 = new OrderItem { Order = order, Product = shampoo, Store = store, Quantity = 1, UnitPrice = 640m, Total = 640m, CommissionAmount = 60.80m };
        db.Orders.Add(order);
        db.OrderItems.AddRange(orderItem1, orderItem2);
        db.Payments.Add(new Payment { Order = order, Amount = order.Total, Method = "card", TransactionRef = "SSL-DEMO-260519-001", Status = PaymentStatus.Paid });
        db.ReturnRequests.Add(new ReturnRequest { OrderItem = orderItem2, User = maya, Reason = "Bottle cap was loose on arrival.", Status = ApprovalStatus.Pending, RefundAmount = 640m });
        db.InventoryAdjustments.AddRange(
            new InventoryAdjustment { Product = catFood, Order = order, QuantityChange = -1, QuantityAfter = 42, Reason = InventoryReason.Sale, PerformedBy = storeUser, Notes = "Order PZ-202605-1001 fulfilled." },
            new InventoryAdjustment { Product = shampoo, Order = order, QuantityChange = -1, QuantityAfter = 36, Reason = InventoryReason.Sale, PerformedBy = storeUser, Notes = "Order PZ-202605-1001 fulfilled." },
            new InventoryAdjustment { Product = leash, QuantityChange = 12, QuantityAfter = 24, Reason = InventoryReason.Restock, PerformedBy = storeUser, Notes = "Supplier restock received." }
        );
        db.ProductReviews.AddRange(
            new ProductReview { Product = catFood, User = maya, Rating = 5, Comment = "Milo adjusted to this food quickly and the resealable bag helps." },
            new ProductReview { Product = leash, User = arif, Rating = 4, Comment = "Comfortable handle and bright enough for evening walks." }
        );
        db.StoreReviews.Add(new StoreReview { Store = store, User = maya, Order = order, Rating = 5, Comment = "Packed carefully and delivered within the promised window.", IsVerifiedPurchase = true });

        var serviceProvider = new ServiceProviderProfile
        {
            User = groomerUser,
            ProviderType = ServiceProviderType.Grooming,
            BusinessName = "Rima's Gentle Grooming",
            About = "Low-stress grooming for cats, senior dogs, and first-time appointments.",
            Address = "Tajmahal Road, Mohammadpur",
            City = "Dhaka",
            Country = "Bangladesh",
            PhoneNumber = groomerUser.PhoneNumber,
            BasePrice = 1500m,
            ApprovalStatus = ApprovalStatus.Approved,
            RatingAverage = 4.9m,
            RatingCount = 22
        };
        db.ServiceProviders.Add(serviceProvider);
        var booking = new ServiceBooking
        {
            ServiceProvider = serviceProvider,
            User = arif,
            Pet = buddy,
            ScheduledAt = now.AddDays(-16).Date.AddHours(15),
            Status = AppointmentStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 1800m,
            Notes = "Buddy is nervous around dryers; towel dry preferred."
        };
        db.ServiceBookings.Add(booking);
        db.ServiceReviews.Add(new ServiceReview { ServiceProvider = serviceProvider, User = arif, Booking = booking, Rating = 5, Comment = "Rima handled Buddy patiently and sent updates during the visit." });

        var listing = new AdoptionListing
        {
            Owner = liza,
            Pet = luna,
            Title = "Gentle calico Luna needs a quiet home",
            PetName = "Luna",
            Description = "Luna was rescued near Mirpur and has settled into indoor life. She is affectionate after a short warm-up period.",
            AnimalType = AnimalType.Cat,
            Breed = "Calico",
            AgeMonths = 14,
            Gender = Gender.Female,
            Size = AnimalSize.Small,
            Color = "Calico",
            Vaccinated = true,
            VaccinationDetails = "FVRCP and rabies up to date.",
            NeuteredSpayed = true,
            HealthCondition = "Healthy; mild food sensitivity.",
            BehaviorNotes = "Quiet, litter-trained, prefers calm spaces.",
            GoodWithChildren = true,
            GoodWithOtherPets = true,
            Location = "Mirpur, Dhaka",
            AdoptionFee = 1200m,
            ReasonForListing = "Rescued foster cat ready for permanent adoption.",
            ContactPreference = ContactPreference.Chat,
            Status = AdoptionListingStatus.Approved,
            SubmittedAt = now.AddDays(-8),
            DecidedAt = now.AddDays(-7),
            DecidedByUser = admin
        };
        var adoptedListing = new AdoptionListing
        {
            Owner = breederUser,
            Title = "Playful mixed-breed puppy already adopted",
            PetName = "Tara",
            Description = "Tara found a home after a home-check and trial weekend.",
            AnimalType = AnimalType.Dog,
            Breed = "Local mixed breed",
            AgeMonths = 5,
            Gender = Gender.Female,
            Size = AnimalSize.Medium,
            Color = "Brown and white",
            Vaccinated = true,
            NeuteredSpayed = false,
            Location = "Savar, Dhaka",
            AdoptionFee = 0m,
            ContactPreference = ContactPreference.Phone,
            Status = AdoptionListingStatus.Adopted,
            SubmittedAt = now.AddDays(-24),
            DecidedAt = now.AddDays(-23),
            DecidedByUser = admin,
            AdoptedAt = now.AddDays(-12),
            AdoptedByUser = arif
        };
        db.AdoptionListings.AddRange(listing, adoptedListing);
        db.AdoptionListingPhotos.AddRange(
            new AdoptionListingPhoto { AdoptionListing = listing, Url = $"{AssetRoot}/photo-1573865526739-10659fec78a5?auto=format&fit=crop&w=900&q=80", OrderIndex = 0 },
            new AdoptionListingPhoto { AdoptionListing = adoptedListing, Url = $"{AssetRoot}/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&w=900&q=80", OrderIndex = 0 }
        );
        db.AdoptionRequests.AddRange(
            new AdoptionRequest { AdoptionListing = listing, Requester = maya, Message = "I have a quiet apartment and would like to meet Luna this Friday.", Status = AdoptionRequestStatus.Pending },
            new AdoptionRequest { AdoptionListing = adoptedListing, Requester = arif, Message = "We can provide a fenced balcony and daily walks.", Status = AdoptionRequestStatus.Selected }
        );
        db.AdoptionWantedPosts.Add(new AdoptionWantedPost
        {
            Requester = arif,
            AnimalType = AnimalType.Dog,
            Breed = "Small or medium local mixed breed",
            PreferredAgeMonthsMin = 4,
            PreferredAgeMonthsMax = 18,
            PreferredSize = AnimalSize.Medium,
            PreferredLocation = "Dhaka",
            ExperienceWithPets = "First-time dog guardian with trainer support.",
            HomeEnvironment = HomeEnvironment.Apartment,
            OtherPetsAtHome = "None",
            ReasonForAdoption = "Looking for a companion suited to apartment life.",
            ContactPreference = ContactPreference.Chat,
            Description = "Open to rescue dogs who are comfortable with a gradual settling-in period.",
            Status = AdoptionListingStatus.PendingApproval
        });
        db.SavedAdoptionListings.Add(new SavedAdoptionListing { User = arif, AdoptionListing = listing, CreatedAt = now.AddDays(-2) });

        var hashtagCare = new Hashtag { Tag = "petcare" };
        var hashtagAdopt = new Hashtag { Tag = "adoptdontshop" };
        var hashtagDhaka = new Hashtag { Tag = "dhakapets" };
        db.Hashtags.AddRange(hashtagCare, hashtagAdopt, hashtagDhaka);
        var post1 = new Post { Author = maya, Content = "Milo finished his follow-up visit today. The flea allergy plan is working and his coat is finally growing back evenly.", AnimalType = AnimalType.Cat, Location = "Dhanmondi, Dhaka" };
        var post2 = new Post { Author = liza, Content = "Luna is ready to meet patient adopters this week. She loves window naps and quiet evenings.", AnimalType = AnimalType.Cat, Location = "Mirpur, Dhaka" };
        var post3 = new Post { Author = storeUser, Content = "Fresh stock of sensitive-skin grooming supplies arrived today. We can bundle them with food orders inside Dhaka.", AnimalType = AnimalType.Dog, Location = "Uttara, Dhaka" };
        db.Posts.AddRange(post1, post2, post3);
        db.PostMedia.AddRange(
            new PostMedia { Post = post1, Url = milo.PrimaryPhotoUrl!, OrderIndex = 0 },
            new PostMedia { Post = post2, Url = $"{AssetRoot}/photo-1573865526739-10659fec78a5?auto=format&fit=crop&w=900&q=80", OrderIndex = 0 },
            new PostMedia { Post = post3, Url = $"{AssetRoot}/photo-1616190264687-b7ebf1a391cd?auto=format&fit=crop&w=900&q=80", OrderIndex = 0 }
        );
        db.PostHashtags.AddRange(
            new PostHashtag { Post = post1, Hashtag = hashtagCare },
            new PostHashtag { Post = post2, Hashtag = hashtagAdopt },
            new PostHashtag { Post = post2, Hashtag = hashtagDhaka },
            new PostHashtag { Post = post3, Hashtag = hashtagCare }
        );
        db.PostPetTags.AddRange(new PostPetTag { Post = post1, Pet = milo }, new PostPetTag { Post = post2, Pet = luna });
        var comment = new Comment { Post = post1, Author = arif, Content = "Glad Milo is doing better. Saving this because Buddy has itchy spots too." };
        var reply = new Comment { Post = post1, Author = maya, ParentComment = comment, Content = "Ask Dr. Nusrat about diet too; that helped us a lot." };
        db.Comments.AddRange(comment, reply);
        db.Reactions.AddRange(
            new Reaction { Post = post1, User = arif, Type = ReactionType.Paw },
            new Reaction { Post = post2, User = maya, Type = ReactionType.Love },
            new Reaction { Post = post3, User = liza, Type = ReactionType.Like }
        );
        db.CommentReactions.Add(new CommentReaction { Comment = comment, User = maya, Type = ReactionType.Like });
        db.PostShares.Add(new PostShare { Post = post2, User = maya, Note = "Sharing for Dhaka friends looking to adopt." });
        db.PostSaves.AddRange(new PostSave { Post = post1, User = arif }, new PostSave { Post = post2, User = arif });
        db.PostReports.Add(new PostReport { Post = post3, Reporter = suspendedUser, Reason = "Misleading promotion", Details = "Reported for review by marketplace team.", Resolved = false });
        db.Follows.AddRange(
            new Follow { Follower = arif, Followed = maya, CreatedAt = now.AddDays(-10) },
            new Follow { Follower = maya, Followed = liza, CreatedAt = now.AddDays(-8) },
            new Follow { Follower = liza, Followed = doctorUser, CreatedAt = now.AddDays(-6) }
        );

        var conversation = new Conversation { Title = "Luna adoption questions", IsGroup = false, ContextType = "adoption", ContextRefId = listing.Id, LastMessageAt = now.AddHours(-1) };
        var message1 = new Message { Conversation = conversation, Sender = arif, Type = MessageType.AdoptionInquiry, Content = "Hi Liza, is Luna comfortable around visitors after a slow introduction?" };
        var message2 = new Message { Conversation = conversation, Sender = liza, Type = MessageType.Text, Content = "Yes, she usually hides for the first few minutes and then comes out for treats.", ReplyToMessage = message1 };
        var message3 = new Message { Conversation = conversation, Sender = arif, Type = MessageType.File, Content = "I attached my landlord's pet permission note.", MediaUrl = "/uploads/messages/landlord-pet-permission.pdf" };
        db.Conversations.Add(conversation);
        db.ConversationParticipants.AddRange(
            new ConversationParticipant { Conversation = conversation, User = arif, JoinedAt = now.AddDays(-2), LastReadAt = now.AddHours(-2), UnreadCount = 1 },
            new ConversationParticipant { Conversation = conversation, User = liza, JoinedAt = now.AddDays(-2), LastReadAt = now.AddMinutes(-50), UnreadCount = 0 }
        );
        db.Messages.AddRange(message1, message2, message3);
        db.MessageAttachments.Add(new MessageAttachment { Message = message3, Url = "/uploads/messages/landlord-pet-permission.pdf", MimeType = "application/pdf", SizeBytes = 184320, FileName = "landlord-pet-permission.pdf" });
        db.MessageReadReceipts.Add(new MessageReadReceipt { Message = message1, User = liza, ReadAt = now.AddHours(-20) });
        db.UserBlocks.Add(new UserBlock { Blocker = maya, BlockedUser = suspendedUser, Reason = "Repeated unwanted messages", CreatedAt = now.AddDays(-6) });
        db.MessageReports.Add(new MessageReport { Message = message3, Reporter = liza, Reason = "Document requires review", Resolved = false });

        var contentReport = new ContentReport
        {
            TargetType = ReportTargetType.Post,
            TargetId = post3.Id,
            Reporter = suspendedUser,
            Reason = "Marketplace claim",
            Details = "Asked admin to verify promotional language before featuring.",
            Status = ReportStatus.UnderReview
        };
        db.ContentReports.Add(contentReport);
        var warning = new UserWarning
        {
            User = suspendedUser,
            Severity = WarningSeverity.Major,
            Reason = "Repeated low-quality reports",
            Message = "Please include specific evidence when submitting reports.",
            RelatedContentType = "Post",
            RelatedContentId = post3.Id,
            IssuedBy = moderator,
            AcknowledgedByUser = false
        };
        var suspension = new UserSuspension
        {
            User = suspendedUser,
            Reason = "Harassing messages",
            Details = "Temporary hold while support reviews direct message reports.",
            IsBan = false,
            ExpiresAt = now.AddDays(5),
            Status = SuspensionStatus.Active,
            IssuedBy = moderator
        };
        db.UserWarnings.Add(warning);
        db.UserSuspensions.Add(suspension);
        db.ModerationActions.AddRange(
            new ModerationAction { Action = ModerationActionType.Escalate, TargetType = ModerationTargetType.Post, TargetId = post3.Id, Report = contentReport, Moderator = moderator, Notes = "Forwarded to marketplace admin for product claim verification.", RelatedWarning = warning },
            new ModerationAction { Action = ModerationActionType.Suspend, TargetType = ModerationTargetType.User, TargetId = suspendedUser.Id, Moderator = moderator, Notes = "Five-day message restriction pending review.", RelatedSuspension = suspension }
        );

        db.Notifications.AddRange(
            Notification(maya, "Order delivered", "Your Green Paw Supplies order was delivered to Dhanmondi.", "/store/orders/PZ-202605-1001", true, now.AddDays(-4)),
            Notification(arif, "Appointment confirmed", "Buddy's appointment with Dr. Nusrat is confirmed.", "/vets/appointments", false, now.AddHours(-22)),
            Notification(liza, "New adoption inquiry", "Arif asked a question about Luna.", "/messages", false, now.AddHours(-1)),
            Notification(storeUser, "Return request opened", "Maya requested a review for one item in order PZ-202605-1001.", "/store/orders", false, now.AddHours(-7))
        );

        db.ApprovalRequests.AddRange(
            new ApprovalRequest { EntityType = ApprovalEntityType.Doctor, EntityId = doctor.Id, SubmittedBy = doctorUser, Decision = ApprovalDecision.Approved, DecidedBy = admin, DecidedAt = now.AddDays(-9), AdminNotes = "License verified.", SlaDueAt = now.AddDays(-7), PayloadJson = JsonSerializer.Serialize(new { doctor.LicenseNumber, doctor.ClinicName }) },
            new ApprovalRequest { EntityType = ApprovalEntityType.Store, EntityId = store.Id, SubmittedBy = storeUser, Decision = ApprovalDecision.Approved, DecidedBy = admin, DecidedAt = now.AddDays(-17), AdminNotes = "KYC approved.", SlaDueAt = now.AddDays(-16), PayloadJson = JsonSerializer.Serialize(new { store.Name, ownerProfile.TradeLicenseNumber }) },
            new ApprovalRequest { EntityType = ApprovalEntityType.AdoptionListing, EntityId = listing.Id, SubmittedBy = liza, Decision = ApprovalDecision.Approved, DecidedBy = admin, DecidedAt = now.AddDays(-7), AdminNotes = "Health details complete.", SlaDueAt = now.AddDays(-6), PayloadJson = JsonSerializer.Serialize(new { listing.Title, listing.Location }) },
            new ApprovalRequest { EntityType = ApprovalEntityType.ServiceProvider, EntityId = serviceProvider.Id, SubmittedBy = groomerUser, Decision = ApprovalDecision.Pending, SlaDueAt = now.AddDays(2), PayloadJson = JsonSerializer.Serialize(new { serviceProvider.BusinessName, serviceProvider.ProviderType }) }
        );
        db.AdminActionLogs.AddRange(
            AdminLog(admin, "store.approve", "Store", store.Id, "Approved after trade license and address proof review.", now.AddDays(-17)),
            AdminLog(admin, "doctor.approve", "Doctor", doctor.Id, "License matched submitted credential document.", now.AddDays(-9)),
            AdminLog(moderator, "moderation.suspend", "User", suspendedUser.Id, "Temporary hold for repeated unwanted contact.", now.AddDays(-1))
        );
        db.AuditEntries.AddRange(
            Audit(admin, "approve", "Store", store.Id, "store", now.AddDays(-17), new { status = "Pending" }, new { status = "Approved" }),
            Audit(admin, "approve", "Doctor", doctor.Id, "vet", now.AddDays(-9), new { status = "Pending" }, new { status = "Approved" }),
            Audit(storeUser, "create", "Product", catFood.Id, "marketplace", now.AddDays(-15), null, new { catFood.Name, catFood.Price }),
            Audit(maya, "create", "Order", order.Id, "marketplace", now.AddDays(-5), null, new { order.OrderNumber, order.Total })
        );
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Topic = "notifications",
            EventType = "Pawzaroo.Demo.OrderDelivered",
            Version = "1",
            Payload = JsonSerializer.Serialize(new { order.OrderNumber, userId = maya.Id, deliveredAt = now.AddDays(-4) }),
            PartitionKey = order.Id.ToString(),
            OccurredAt = now.AddDays(-4),
            DispatchedAt = now.AddDays(-4).AddMinutes(2),
            Status = OutboxStatus.Dispatched,
            Attempts = 1,
            CorrelationId = "seed-order-delivered-1001",
            UserId = maya.Id
        });

        await db.SaveChangesAsync(ct);
    }

    private static User User(string email, string displayName, string userName, string location, string bio, string phone, DateTime lastLogin, string passwordHash, bool isSuspended = false) =>
        new()
        {
            Email = email,
            DisplayName = displayName,
            UserName = userName,
            PhoneNumber = phone,
            AvatarUrl = $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(displayName)}",
            Bio = bio,
            Location = location,
            PasswordHash = passwordHash,
            EmailConfirmed = true,
            IsActive = !isSuspended,
            IsSuspended = isSuspended,
            LastLoginAt = lastLogin,
            ApprovalStatus = isSuspended ? ApprovalStatus.Suspended : ApprovalStatus.Approved,
            ApprovedAt = DateTime.UtcNow.AddDays(-30)
        };

    private static void AddRole(ApplicationDbContext db, IReadOnlyDictionary<string, Role> roles, User user, string roleName)
    {
        if (roles.TryGetValue(roleName, out var role))
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = DateTime.UtcNow.AddDays(-20) });
    }

    private static UserProfile Profile(User user, string firstName, string lastName, string address, string city, string state, string postal, string currency, object preferences) =>
        new()
        {
            User = user,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateOnly(1991, 5, 12),
            Gender = "Not specified",
            AddressLine1 = address,
            City = city,
            StateRegion = state,
            PostalCode = postal,
            Country = "Bangladesh",
            PreferredLanguage = "en",
            PreferredCurrency = currency,
            PreferencesJson = JsonSerializer.Serialize(preferences)
        };

    private static UserDevice Device(User user, string fingerprint, string label, string city, bool trusted, DateTime firstSeen, DateTime lastSeen) =>
        new()
        {
            User = user,
            Fingerprint = fingerprint,
            Label = label,
            UserAgent = "Mozilla/5.0 demo browser",
            IpAddress = "103.106.239.22",
            IpCity = city,
            IpCountry = "Bangladesh",
            FirstSeenAt = firstSeen,
            LastSeenAt = lastSeen,
            IsTrusted = trusted
        };

    private static Pet Pet(User owner, string name, AnimalType type, string breed, Gender gender, DateTime birthDate, decimal weight, string color, string tag, bool availableForAdoption) =>
        new()
        {
            Owner = owner,
            Name = name,
            AnimalType = type,
            Breed = breed,
            Gender = gender,
            BirthDate = birthDate,
            WeightKg = weight,
            Color = color,
            TagNumber = tag,
            PrimaryPhotoUrl = type == AnimalType.Dog
                ? $"{AssetRoot}/photo-1552053831-71594a27632d?auto=format&fit=crop&w=900&q=80"
                : $"{AssetRoot}/photo-1574144611937-0df059b5ef3e?auto=format&fit=crop&w=900&q=80",
            Allergies = type == AnimalType.Cat ? "Sensitive to some fish-based foods." : null,
            DietNotes = type == AnimalType.Dog ? "Two measured meals daily." : "Mostly wet food with measured dry food.",
            IsAvailableForAdoption = availableForAdoption
        };

    private static PetPhoto Photo(Pet pet, string url, string caption) => new() { Pet = pet, Url = url, Caption = caption };

    private static DoctorAvailability Availability(Doctor doctor, DayOfWeek day, int startHour, int endHour, ConsultationType type) =>
        new() { Doctor = doctor, DayOfWeek = day, StartTime = new TimeOnly(startHour, 0), EndTime = new TimeOnly(endHour, 0), SlotMinutes = 30, ConsultationType = type };

    private static DoctorTimeSlot Slot(Doctor doctor, DateTime start, ConsultationType type, SlotStatus status) =>
        new() { Doctor = doctor, StartUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc), EndUtc = DateTime.SpecifyKind(start.AddMinutes(30), DateTimeKind.Utc), ConsultationType = type, Status = status };

    private static Product Product(Store store, ProductCategory category, Brand brand, string name, string description, string sku, decimal price, decimal? discountPrice, int stock, bool featured, decimal rating, int ratingCount) =>
        new()
        {
            Store = store,
            Category = category,
            Brand = brand,
            Name = name,
            Description = description,
            Sku = sku,
            Price = price,
            DiscountPrice = discountPrice,
            StockQuantity = stock,
            IsActive = true,
            IsFeatured = featured,
            RatingAverage = rating,
            RatingCount = ratingCount
        };

    private static ProductImage Image(Product product, string url) => new() { Product = product, Url = url, OrderIndex = 0 };

    private static InAppNotification Notification(User user, string title, string body, string url, bool read, DateTime at) =>
        new() { User = user, Title = title, Body = body, Url = url, IsRead = read, ReadAt = read ? at.AddHours(1) : null, CreatedAt = at, Payload = JsonSerializer.Serialize(new { source = "seed" }) };

    private static AdminActionLog AdminLog(User admin, string action, string targetType, Guid targetId, string reason, DateTime at) =>
        new() { Admin = admin, Action = action, TargetType = targetType, TargetId = targetId.ToString(), Reason = reason, At = at, IpAddress = "103.106.239.22", UserAgent = "Pawzaroo Admin Console" };

    private static AuditEntry Audit(User user, string action, string entity, Guid id, string module, DateTime at, object? oldValues, object? newValues) =>
        new()
        {
            UserId = user.Id,
            Action = action,
            EntityName = entity,
            EntityId = id.ToString(),
            Module = module,
            At = at,
            IpAddress = "103.106.239.22",
            UserAgent = "Pawzaroo Web",
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues)
        };
}
