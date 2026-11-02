# Pawzaroo — Feature Roadmap (Pet Management)

This document lists new features that can be added to Pawzaroo, based on an analysis of the current codebase (as of 2026-11-02). Each item notes **what exists today**, **what is missing**, and **where it would plug in**.

Priority legend: **P0** = gap / risk in existing functionality, **P1** = high value & low effort (model already exists), **P2** = new capability, **P3** = nice to have.

## Implementation status (2026-11-02)

| Item | Status | Where |
|---|---|---|
| 2.1 Ownership & privacy | ✅ Done | `IPetAccessService` (owner / co-owner / caretaker / viewer / treating vet / `pets.read_any`); strangers get 404 |
| 2.2 Module alignment | ✅ Done | `Modules/Pets` (DTOs, services, validators, events), `Controllers/V1/PetsController` replaces the legacy `api/pets` controller |
| 2.3 Health record CRUD | ✅ Done | Vaccinations, medical, grooming with document uploads — pet detail **Health** tab |
| 2.4 Photo gallery | ✅ Done | **Overview** tab |
| 3.1 Vaccination reminders | ✅ Done | `PetCareReminderJob` (7 d / 1 d / overdue), dashboard **Upcoming care** widget, overdue badges |
| 3.2 Vet visit sync | ✅ Done | `PetClinicalSync` — completed appointments → medical record |
| 3.3 Weight tracking | ✅ Done | **Weight** tab with healthy-range band |
| 3.4 Services marketplace | ✅ Done (pay-at-provider) | `/services`, provider dashboard, admin approvals; online SSLCommerz payment not wired |
| 3.5 Medication schedule | ✅ Done | Prescriptions → courses, dose logging, dose reminders, **Today's doses** widget |
| 4.1 Lost & found | ✅ Done (city-based alerts) | `/lost-found`, matching, feed post; geo-radius alerts need user coordinates |
| 4.2 QR ID tag | ✅ Done | Public `/p/:code`, sighting relay, printable tag |
| 4.3 Co-caretakers | ✅ Done | **Care team** tab (viewer / caretaker / co-owner, optional expiry) |
| 4.4 Daily care log | ✅ Done | **Daily care** tab — routines, checklist, streak |
| 4.5 Telemedicine | ✅ Done | Meeting links already existed; treating vets now see the pet's record |
| 4.6 Export & share | ✅ Done | Printable HTML/PDF report, expiring revocable share links |
| 4.7 Adoption hand-over | ✅ Done | Mark-adopted dialog transfers the pet profile (consent for health history) |
| 4.8 Recommendations | ✅ Done (rule-based) | Life stage, care tips, species vaccine schedule, matching products |
| 4.9 Livestock | ✅ Done | `/livestock` — groups, bulk vaccination, production, breeding |
| 4.10 Expenses & insurance | ✅ Done | **Expenses** tab + `/pets/expenses` |
| P3 Birthdays, lineage, privacy/terms | ✅ Done | Reminder job, family tree, `/privacy`, `/terms` |
| P3 Pet social profiles, events, i18n, PWA/push, AI assistant | ⏳ Not started | |

---

## 1. Current state of the Pet module (summary)

| Area | Status |
|---|---|
| `Pet` entity (name, type, breed, gender, birth date, weight, color, tag, allergies, diet, photo) | ✅ Implemented |
| Pet CRUD API — `PetsController` (`/api/pets`) | ✅ Basic create / read / update / delete |
| Pet UI — `PetsPage`, `PetDetailPage`, `PetFormDialog` | ✅ List, detail, form |
| `VaccinationRecord`, `MedicalRecord`, `GroomingRecord`, `PetPhoto` entities | ⚠️ Modelled & displayed **read-only** — no API to create/edit/delete them |
| `ServiceProviderProfile`, `ServiceBooking`, `ServiceReview` (Grooming / Training / Boarding / Walking) | ⚠️ Modelled & seeded only — **no controller, service, or UI** |
| `Prescription` (linked to appointment) | ⚠️ Not automatically reflected into the pet's `MedicalRecord` history |
| Appointment reminders (`AppointmentReminderJob`) | ✅ Exists — but there are no vaccination / medication reminders |

---

## 2. P0 — Fix gaps in the existing Pet module

### 2.1 Pet record ownership & privacy
- **Issue:** `GET /api/pets/{id}` returns any pet, including its full medical, vaccination and grooming history, to **any authenticated user**. There is no owner check.
- **Add:** an access policy that allows the owner, a vet with an active or past appointment for that pet, and admins with a `pets.read_any` permission.
- **Where:** `backend/src/Pawzaroo.Api/Controllers/PetsController.cs`, `Application/Common/Permissions/Permissions.cs`.

### 2.2 Bring the Pets module in line with the other modules
- `PetsController` sits outside `Controllers/V1`. It uses the unversioned route `api/pets`, talks to `ApplicationDbContext` directly, and has no `[Permission]` attributes, validators or DTOs. It also returns EF entities directly, which leaks navigation properties.
- **Add:** a `Modules/Pets` application module with DTOs, `IPetServices`, and FluentValidation validators (name length, weight > 0, birth date not in the future). Move the controller to V1 and add audit logging.

### 2.3 Health record CRUD (vaccinations, medical, grooming)
- The detail page already has the three tabs, but users cannot add records.
- **API:**
  - `POST/PUT/DELETE /api/v1/pets/{id}/vaccinations`
  - `POST/PUT/DELETE /api/v1/pets/{id}/medical-records`
  - `POST/PUT/DELETE /api/v1/pets/{id}/grooming-records`
- **UI:** "Add record" dialogs on each tab in `PetDetailPage.tsx`, with an attachment upload (lab report, certificate) through the existing `MediaController` / S3 storage.

### 2.4 Pet photo gallery
- `PetPhoto` exists but has no endpoints.
- **Add:** upload multiple photos with captions, set one as the primary photo, delete, and show a gallery tab on the detail page.

---

## 3. P1 — High value, low effort (domain largely exists)

### 3.1 Vaccination & deworming reminders
- `VaccinationRecord.NextDueOn` is already stored but never acted on.
- **Add:** a `VaccinationReminderJob` in `Pawzaroo.Worker`, modelled on `AppointmentReminderJob`. It would send in-app, SignalR and email notifications 7 days and 1 day before a dose is due, and flag overdue doses.
- **UI:** an "Upcoming care" widget on `UserDashboardPage` and an overdue badge on pet cards.

### 3.2 Auto-sync vet visits into pet history
- When an appointment is marked **Completed** or a `Prescription` is issued, create or update a `MedicalRecord` with `AppointmentId`, diagnosis and the prescription file.
- Publish a `PetMedicalRecordAdded` event over Kafka (outbox) so notifications and the feed can react.

### 3.3 Weight & growth tracking
- `WeightKg` only stores the latest value.
- **Add:** a `PetWeightLog` entity (date, weight, note) and a growth chart on the pet detail page. The frontend already includes `recharts`. Show a healthy-range hint by species and size (the `AnimalSize` enum already exists).

### 3.4 Pet services marketplace (grooming / training / boarding / walking)
- The full domain exists: `ServiceProviderProfile`, `ServiceBooking`, `ServiceReview`, and the `ServiceProvider` role.
- **Add:**
  - Provider registration and approval, reusing the unified approvals inbox (`ApprovalRequest`).
  - Search providers by type and city, booking linked to a pet, and payment via the existing `SslCommerzPaymentGateway`.
  - A provider dashboard and post-booking reviews.
  - Auto-create a `GroomingRecord` when a grooming booking is completed.

### 3.5 Medication schedule & dose tracking
- Turn `Prescription.ItemsJson` (drug, dose, frequency, duration) into a **medication schedule** per pet.
- Owners tick off doses, receive reminder notifications, and the vet can see adherence on the next visit.

---

## 4. P2 — New capabilities

### 4.1 Lost & Found pets
- A "Report lost" action on a pet, which creates a public alert with last-seen location, photo and contact preference (the `ContactPreference` enum already exists).
- Nearby users get notifications (city/area based first, geo-radius later). A "Found a pet" post flow can match on `AnimalType`, breed, color and tag number.
- Integrate with the feed as a special post type, and with messaging through a new `MessageType.LostPetInquiry`.

### 4.2 Microchip / QR pet ID tag
- Add `MicrochipNumber` to `Pet` and generate a **public QR profile page**, e.g. `/p/{shortCode}`. It shows only owner-approved fields (name, photo, allergies, owner contact via relay chat).
- Scanning the QR of a lost pet notifies the owner with the scanner's approximate location.

### 4.3 Shared pet ownership / co-caretakers
- Invite a family member, pet sitter or walker to a pet with a role such as Viewer, Caretaker or Co-owner.
- Caretakers can log feeding, walks and medication doses. Co-owners can book vet appointments.
- New entity: `PetCaretaker (PetId, UserId, Role, ExpiresAt)`.

### 4.4 Daily care log & routines
- Feeding schedule (food, portion, times), walks and activity, litter, and baths.
- Show a daily checklist with streaks. This builds on `DietNotes`, which is free text today.

### 4.5 Telemedicine (online vet consultation)
- `ConsultationType.Online` exists, but there is no video or meeting link.
- **Add:** a meeting link on `Appointment` (Jitsi / Daily / Twilio), plus an in-app pre-consult chat that reuses `ChatHub`. The pet's health summary would be shared with the vet automatically.

### 4.6 Pet health record export & sharing
- Export a pet's full history (vaccinations, visits, prescriptions, weight) as a PDF. The existing `InvoiceRenderer` approach can be reused.
- Generate a **time-limited share link** for a new vet, boarding facility or travel/pet passport purposes.

### 4.7 Adoption ↔ Pet profile handover
- `AdoptionListing.PetId` already links to `Pet`.
- **Add:** when a listing is marked **Adopted**, transfer pet ownership to the selected adopter, including health history (with the previous owner's consent). Keep an ownership history table for traceability.

### 4.8 Breed-aware recommendations
- Use `AnimalType`, breed, age and weight to recommend products from the marketplace (food by life stage, size-appropriate accessories). Suggest care tips and vaccination schedules from a species template, e.g. a default puppy or kitten vaccine plan.

### 4.9 Livestock / farm animal support
- `AnimalType` already includes Cow, Goat, Sheep, Horse, Chicken and Duck.
- **Add:** herd/flock grouping, bulk vaccination entry, breeding and pregnancy tracking, production logs (milk, eggs), and a vet visit for a whole group.

### 4.10 Pet insurance & expense tracker
- Track spending per pet across marketplace orders, vet appointments, service bookings and manual entries. Show monthly charts on the dashboard.
- Optionally store insurance policies (provider, policy number, coverage, claim documents).

---

## 5. P3 — Nice to have

| Feature | Notes |
|---|---|
| Pet social profiles | Public pet page with posts tagged to a pet; follow a pet (the `Follow` entity exists for users) |
| Pet birthdays & milestones | Notification on `BirthDate`, auto feed post template |
| Breeding & lineage | Parent links (sire/dam), litter records — relevant to the `Breeder` role |
| Events & meetups | Adoption drives, vaccination camps, pet shows organised by `AdoptionCenter` / vets |
| Multi-language (i18n) | Bengali + English UI. The courier providers (Pathao, SteadFast) suggest a Bangladesh market |
| PWA / offline + push notifications | Web push for reminders. Installable app shell |
| AI assistant | Symptom triage ("should I see a vet?"), photo-based breed detection, care Q&A |
| Privacy & Terms pages | Currently `PlaceholderPage` in `frontend/src/App.tsx` |

---

## 6. Supporting / quality work recommended alongside

- **Tests:** `backend/tests/Pawzaroo.Tests/Integration` contains only `PostgresFixture.cs`. Add integration tests for the pet APIs, especially ownership/authorization (2.1) and health record CRUD.
- **Permissions catalog:** add `pets.*`, `pet_records.*`, `services.*` and `lost_found.*` codes, and extend `PermissionsCatalogTests`.
- **Kafka topics:** `PetCreated`, `PetMedicalRecordAdded`, `VaccinationDue`, `PetReportedLost`, `ServiceBookingCompleted`.
- **Admin:** a services approval page, lost & found moderation, and pet-health analytics (vaccination coverage) on `AnalyticsPage`.

---

## 7. Suggested delivery order

1. **Sprint 1:** 2.1 Ownership/privacy → 2.2 Module refactor → 2.3 Health record CRUD → 2.4 Photo gallery
2. **Sprint 2:** 3.1 Vaccination reminders → 3.2 Vet visit sync → 3.3 Weight tracking
3. **Sprint 3:** 3.4 Pet services marketplace → 3.5 Medication schedule
4. **Sprint 4+:** 4.1 Lost & Found → 4.2 QR ID tag → 4.3 Co-caretakers → remaining P2/P3 items
