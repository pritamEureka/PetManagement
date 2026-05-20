namespace Pawzaroo.Domain.Common;

public enum AnimalType
{
    Dog = 1, Cat = 2, Bird = 3, Rabbit = 4, Fish = 5,
    Reptile = 6, Cow = 7, Goat = 8, Sheep = 9, Horse = 10,
    Chicken = 11, Duck = 12, Other = 99
}

public enum Gender { Unknown = 0, Male = 1, Female = 2 }

public enum ApprovalStatus { Pending = 0, Approved = 1, Rejected = 2, Suspended = 3 }

public enum ContactPreference { Chat = 0, Phone = 1, Email = 2 }

public enum ReactionType { Like = 0, Love = 1, Haha = 2, Wow = 3, Sad = 4, Angry = 5, Paw = 6 }

public enum MessageType { Text = 0, Image = 1, Video = 2, File = 3, AdoptionInquiry = 10, VetInquiry = 11, StoreInquiry = 12, AppointmentInfo = 13, System = 99 }

public enum ConsultationType { Online = 0, Offline = 1, Both = 2 }

public enum AppointmentStatus
{
    Draft = 0,
    PendingPayment = 1,
    PendingConfirmation = 2,
    Confirmed = 3,
    Rescheduled = 4,
    CancelledByUser = 5,
    CancelledByDoctor = 6,
    Completed = 7,
    NoShow = 8,
    Refunded = 9
}

public enum AppointmentDisputeStatus { Open = 0, UnderReview = 1, Resolved = 2, Closed = 3 }

public enum PaymentStatus { Unpaid = 0, Pending = 1, Paid = 2, Refunded = 3, Failed = 4 }

public enum OrderStatus { Created = 0, Confirmed = 1, Packed = 2, Shipped = 3, Delivered = 4, Cancelled = 5, Returned = 6, Denied = 7 }

public enum ShipmentStatus { NotShipped = 0, Processing = 1, InTransit = 2, OutForDelivery = 3, Delivered = 4, Failed = 5 }

public enum DeliveryAssignmentStatus { Assigned = 0, PickedUp = 1, InTransit = 2, OutForDelivery = 3, Delivered = 4, Failed = 5 }

// Third-party courier services a store owner can hand an order off to. Distinct
// from the internal DeliveryUser flow on Order.DeliveryAssignment — when this
// is set, the parcel is being handled externally and our app only tracks
// status by syncing the courier's tracking number, not by driver app updates.
public enum CourierProvider { SteadFast = 1, Pathao = 2, Paperfly = 3, Sundarban = 4 }

public enum ServiceProviderType { Grooming = 0, Training = 1, Boarding = 2, Walking = 3 }

public enum AnimalSize { Tiny = 1, Small = 2, Medium = 3, Large = 4, ExtraLarge = 5 }

public enum AdoptionListingStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Adopted = 4,
    Closed = 5
}

public enum AdoptionRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Withdrawn = 3,
    Selected = 4
}

public enum HomeEnvironment
{
    Apartment = 0,
    House = 1,
    HouseWithYard = 2,
    Farm = 3,
    Other = 99
}
