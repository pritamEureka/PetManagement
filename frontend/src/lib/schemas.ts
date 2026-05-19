import { z } from "zod";

export const loginSchema = z.object({
  email: z.string().email("Enter a valid email."),
  password: z.string().min(1, "Password required.")
});
export type LoginInput = z.infer<typeof loginSchema>;

// Roles a user may pick at sign-up. Privileged roles (SuperAdmin / Admin /
// Moderator / SupportAgent) are deliberately excluded — only a SuperAdmin can
// grant those via the admin role-assignment surface. Kept in sync with
// SelfRegisterRoles.Allowed on the backend.
export const selfRegisterRoles = [
  { value: "User",            label: "Pet Owner",
    hint: "Adopt, manage pets, post in the feed, book vets and shop the marketplace." },
  { value: "StoreOwner",      label: "Store Owner",
    hint: "Run a marketplace store: list products, manage stock and orders." },
  { value: "Veterinarian",    label: "Veterinarian (Vet Doctor)",
    hint: "Offer consultations, manage availability, issue prescriptions." },
  { value: "Seller",          label: "Individual Seller",
    hint: "Sell pet items as an individual without a full store." },
  { value: "ServiceProvider", label: "Service Provider",
    hint: "Grooming, training, boarding, walking, etc." },
  { value: "Breeder",         label: "Breeder",
    hint: "Manage pets, list animals for adoption / sale." },
  { value: "AdoptionCenter",  label: "Adoption Center / Shelter",
    hint: "Rescues and shelters managing adoption listings." },
  { value: "DeliveryUser",    label: "Delivery Partner",
    hint: "Logistics / courier handling marketplace deliveries." },
] as const;

export type SelfRegisterRole = typeof selfRegisterRoles[number]["value"];

export const registerSchema = z.object({
  displayName: z.string().min(2, "At least 2 characters.").max(128),
  email: z.string().email("Enter a valid email."),
  password: z.string().min(8, "Min 8 characters.")
    .regex(/[A-Z]/, "Must contain an uppercase letter.")
    .regex(/[a-z]/, "Must contain a lowercase letter.")
    .regex(/[0-9]/, "Must contain a digit."),
  phoneNumber: z.string().max(32).optional().or(z.literal("")),
  requestedRole: z.enum(selfRegisterRoles.map((r) => r.value) as [string, ...string[]])
});
export type RegisterInput = z.infer<typeof registerSchema>;

export const animalTypes = [
  "Dog", "Cat", "Bird", "Rabbit", "Fish", "Reptile",
  "Cow", "Goat", "Sheep", "Horse", "Chicken", "Duck", "Other"
] as const;
export const genders = ["Unknown", "Male", "Female"] as const;
export const contactPreferences = ["Chat", "Phone", "Email"] as const;
export const consultationTypes = ["Online", "Offline", "Both"] as const;

export const petSchema = z.object({
  name: z.string().min(1).max(128),
  animalType: z.enum(animalTypes),
  breed: z.string().max(128).optional().or(z.literal("")),
  gender: z.enum(genders).default("Unknown"),
  birthDate: z.string().optional().or(z.literal("")),
  weightKg: z.coerce.number().nonnegative().optional(),
  color: z.string().max(64).optional().or(z.literal("")),
  tagNumber: z.string().max(64).optional().or(z.literal("")),
  primaryPhotoUrl: z.string().url().optional().or(z.literal("")),
  allergies: z.string().max(1000).optional().or(z.literal("")),
  dietNotes: z.string().max(2000).optional().or(z.literal("")),
  isAvailableForAdoption: z.boolean().default(false)
});
export type PetInput = z.infer<typeof petSchema>;

export const animalSizes = ["Tiny", "Small", "Medium", "Large", "ExtraLarge"] as const;
export const homeEnvironments = ["Apartment", "House", "HouseWithYard", "Farm", "Other"] as const;

export const adoptionListingSchema = z.object({
  title: z.string().min(4).max(256),
  petName: z.string().max(128).optional().or(z.literal("")),
  description: z.string().max(5000).optional().or(z.literal("")),
  animalType: z.enum(animalTypes),
  breed: z.string().max(128).optional().or(z.literal("")),
  ageMonths: z.coerce.number().int().min(0).max(50 * 12).optional(),
  gender: z.enum(genders),
  size: z.enum(animalSizes).optional(),
  color: z.string().max(64).optional().or(z.literal("")),
  vaccinated: z.boolean().default(false),
  vaccinationDetails: z.string().max(2000).optional().or(z.literal("")),
  neuteredSpayed: z.boolean().default(false),
  healthCondition: z.string().max(2000).optional().or(z.literal("")),
  behaviorNotes: z.string().max(2000).optional().or(z.literal("")),
  goodWithChildren: z.boolean().nullable().optional(),
  goodWithOtherPets: z.boolean().nullable().optional(),
  location: z.string().max(256).optional().or(z.literal("")),
  adoptionFee: z.coerce.number().nonnegative().default(0),
  reasonForListing: z.string().max(2000).optional().or(z.literal("")),
  contactPreference: z.enum(contactPreferences),
  petId: z.string().uuid().optional().or(z.literal("")),
  photoUrls: z.array(z.string().url()).max(12).optional(),
  submitForApproval: z.boolean().default(true)
});
export type AdoptionListingInput = z.infer<typeof adoptionListingSchema>;

export const adoptionWantedSchema = z.object({
  animalType: z.enum(animalTypes),
  breed: z.string().max(128).optional().or(z.literal("")),
  preferredAgeMonthsMin: z.coerce.number().int().min(0).max(50 * 12).optional(),
  preferredAgeMonthsMax: z.coerce.number().int().min(0).max(50 * 12).optional(),
  preferredSize: z.enum(animalSizes).optional(),
  preferredLocation: z.string().max(256).optional().or(z.literal("")),
  experienceWithPets: z.string().max(2000).optional().or(z.literal("")),
  homeEnvironment: z.enum(homeEnvironments).optional(),
  otherPetsAtHome: z.string().max(1000).optional().or(z.literal("")),
  reasonForAdoption: z.string().max(2000).optional().or(z.literal("")),
  contactPreference: z.enum(contactPreferences),
  description: z.string().max(4000).optional().or(z.literal(""))
});
export type AdoptionWantedInput = z.infer<typeof adoptionWantedSchema>;

export const createPostSchema = z.object({
  content: z.string().max(5000).optional().or(z.literal("")),
  animalType: z.enum(animalTypes).optional(),
  location: z.string().max(256).optional().or(z.literal("")),
  mediaUrls: z.array(z.string().url()).optional(),
  hashtags: z.array(z.string().min(1).max(64)).optional()
}).refine((d) => (d.content && d.content.length > 0) || (d.mediaUrls && d.mediaUrls.length > 0),
  "Add text or at least one image.");
export type CreatePostInput = z.infer<typeof createPostSchema>;

export const bookAppointmentSchema = z.object({
  doctorId: z.string().uuid(),
  timeSlotId: z.string().uuid(),
  petId: z.string().uuid().optional().or(z.literal("")),
  type: z.enum(consultationTypes),
  symptoms: z.string().max(2000).optional().or(z.literal(""))
});
export type BookAppointmentInput = z.infer<typeof bookAppointmentSchema>;

export const doctorRegistrationSchema = z.object({
  licenseNumber: z.string().min(2).max(64),
  specialty: z.string().max(128).optional().or(z.literal("")),
  experienceYears: z.coerce.number().int().min(0).max(70).optional(),
  about: z.string().max(4000).optional().or(z.literal("")),
  clinicName: z.string().max(256).optional().or(z.literal("")),
  clinicAddress: z.string().max(512).optional().or(z.literal("")),
  city: z.string().max(128).optional().or(z.literal("")),
  country: z.string().max(64).optional().or(z.literal("")),
  consultationFee: z.coerce.number().nonnegative().default(0),
  consultationType: z.enum(consultationTypes),
  onlineAvailable: z.boolean().default(false),
  offlineAvailable: z.boolean().default(true),
  autoConfirmAppointments: z.boolean().default(false),
  defaultSlotMinutes: z.coerce.number().int().min(5).max(240).default(30),
  cancellationCutoffHours: z.coerce.number().int().min(0).max(168).default(12),
  supportedAnimalTypes: z.array(z.enum(animalTypes)).min(1, "Pick at least one animal type."),
  specialtyIds: z.array(z.string().uuid()).default([])
});
export type DoctorRegistrationInput = z.infer<typeof doctorRegistrationSchema>;

export const availabilityRuleSchema = z.object({
  dayOfWeek: z.coerce.number().int().min(0).max(6),
  startTime: z.string().regex(/^\d{2}:\d{2}$/, "HH:mm"),
  endTime: z.string().regex(/^\d{2}:\d{2}$/, "HH:mm"),
  slotMinutes: z.coerce.number().int().min(5).max(240).default(30),
  consultationType: z.enum(consultationTypes)
});
export type AvailabilityRuleInput = z.infer<typeof availabilityRuleSchema>;

export const checkoutSchema = z.object({
  shippingAddressId: z.string().uuid().optional().or(z.literal("")),
  shippingAddress: z.string().min(5).max(512).optional().or(z.literal("")),
  shippingCity: z.string().max(128).optional().or(z.literal("")),
  shippingCountry: z.string().max(64).optional().or(z.literal("")),
  paymentMethod: z.enum(["sslcommerz", "cod"]).default("sslcommerz")
}).refine((v) => !!v.shippingAddressId || !!(v.shippingAddress && v.shippingAddress.length >= 5),
  { message: "Provide a saved address or a shipping address line.", path: ["shippingAddress"] });
export type CheckoutInput = z.infer<typeof checkoutSchema>;

// ---------- Marketplace ----------

export const storeRegisterSchema = z.object({
  name: z.string().min(2).max(128),
  description: z.string().max(2000).optional().or(z.literal("")),
  logoUrl: z.string().url().optional().or(z.literal("")),
  bannerUrl: z.string().url().optional().or(z.literal("")),
  address: z.string().max(256).optional().or(z.literal("")),
  city: z.string().max(128).optional().or(z.literal("")),
  country: z.string().max(64).optional().or(z.literal("")),
  phoneNumber: z.string().max(32).optional().or(z.literal("")),
  email: z.string().email().optional().or(z.literal(""))
});
export type StoreRegisterInput = z.infer<typeof storeRegisterSchema>;

export const kycSubmitSchema = z.object({
  legalName: z.string().min(2).max(256),
  businessName: z.string().max(256).optional().or(z.literal("")),
  tradeLicenseNumber: z.string().max(128).optional().or(z.literal("")),
  nationalIdNumber: z.string().max(64).optional().or(z.literal("")),
  taxId: z.string().max(64).optional().or(z.literal("")),
  tradeLicenseDocUrl: z.string().url().optional().or(z.literal("")),
  nationalIdDocUrl: z.string().url().optional().or(z.literal("")),
  addressProofDocUrl: z.string().url().optional().or(z.literal(""))
}).refine((v) => !!v.tradeLicenseNumber || !!v.nationalIdNumber,
  { message: "Provide trade license or national ID.", path: ["nationalIdNumber"] });
export type KycSubmitInput = z.infer<typeof kycSubmitSchema>;

export const productSchema = z.object({
  name: z.string().min(2).max(256),
  sku: z.string().min(1).max(64).regex(/^[A-Za-z0-9_-]+$/, "Letters, digits, '_' or '-' only."),
  description: z.string().max(8000).optional().or(z.literal("")),
  price: z.coerce.number().nonnegative(),
  discountPrice: z.coerce.number().nonnegative().optional(),
  stockQuantity: z.coerce.number().int().nonnegative(),
  categoryId: z.string().uuid().optional().or(z.literal("")),
  brandId: z.string().uuid().optional().or(z.literal("")),
  imageUrls: z.array(z.string().url()).max(12).optional()
}).refine((v) => !v.discountPrice || v.discountPrice < v.price, {
  message: "Discount price must be less than price.", path: ["discountPrice"]
});
export type ProductFormInput = z.infer<typeof productSchema>;

export const shippingAddressSchema = z.object({
  label: z.string().min(1).max(32),
  recipientName: z.string().min(2).max(128),
  phoneNumber: z.string().min(3).max(32),
  addressLine1: z.string().min(3).max(256),
  addressLine2: z.string().max(256).optional().or(z.literal("")),
  city: z.string().max(128).optional().or(z.literal("")),
  state: z.string().max(128).optional().or(z.literal("")),
  country: z.string().max(64).optional().or(z.literal("")),
  postalCode: z.string().max(32).optional().or(z.literal("")),
  isDefault: z.boolean().default(false)
});
export type ShippingAddressInput = z.infer<typeof shippingAddressSchema>;
