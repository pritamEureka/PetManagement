# Pawzaroo Improvements

This document summarizes new features and improvements for Pawzaroo from both a product/user perspective and a technical perspective.

## 1. Highest-priority new features

### 1.1 Emergency and symptom triage

- Add a symptom checker with urgency levels and clear medical disclaimers.
- Add emergency and 24/7 vet discovery with direct calling.
- Add poison/exposure reporting and immediate veterinary escalation.
- Do not present the feature as a substitute for professional diagnosis.

### 1.2 Preventive care plans

- Combine vaccinations, medication, appointments, grooming, parasite prevention, and livestock treatments.
- Let vets publish care plans and let owners create custom plans.
- Add rescheduling, missed-care summaries, caretaker visibility, and notifications.

### 1.3 Improved appointment booking

- Add deposits, waitlists, recurring appointments, and follow-up bookings.
- Add doctor switching, shared calendars, pre-visit questionnaires, and cancellation policies.
- Implement secure timed meeting rooms for online consultations.
- Add post-consultation SOAP-style notes and record consent.

### 1.4 Marketplace expansion

- Add product variants, SKU-level inventory, low-stock alerts, and bundles.
- Add subscribe-and-save, replenishment reminders, missed-payment retries, and one-click reorder.
- Add store comparison, estimated delivery promises, and checkout availability checks.
- Add loyalty points, referral credits, gift cards, and promotions.

### 1.5 Delivery and seller operations

- Add courier checkpoints, live tracking timelines, and proof-of-delivery documents.
- Accept signed courier webhooks instead of relying on manual status updates.
- Add store settlement, payout holds, reversals, statements, and refund-ledger entries.
- Add delivery zones, peak-hour fees, fulfillment SLAs, and returns pickup.

### 1.6 Community features

- Add breed/species communities, local clubs, private groups, and community moderators.
- Add meetups, vaccination camps, adoption drives, RSVP limits, and waitlists.
- Add polls, pet albums, followable pet profiles, and community reputation.
- Improve location-aware feed discovery and lost-and-found alerts.

### 1.7 Family, multilingual, and accessibility support

- Complete Bengali and English i18n across navigation, forms, and API errors.
- Use locale-aware dates, units, currency, and phone-number formatting.
- Provide emergency access to critical health details.
- Improve keyboard navigation, screen-reader labels, contrast, and RTL-safe layouts.

## 2. Improvements to existing features

### 2.1 Pet health records

- Replace unstructured allergy and diet text with typed, searchable values.
- Add record versioning, vet signatures, record sealing, and amendment history.
- Add vaccination certificate generation and portable PDF/A or FHIR-compatible exports.
- Add missed-dose, weight anomaly, and repeated-visit insights.
- Add lab-result and document OCR workflows.
- Add herd-level diagnosis, treatment, withdrawal-period, and bulk-vaccination management.

### 2.2 Adoption

- Add application stages, reference checks, home visits, decision SLAs, and follow-up visits.
- Track foster-to-adoption transitions and post-adoption outcomes.
- Improve matching using household, children, existing pets, experience, size, and travel distance.
- Detect duplicate or suspicious listings.
- Send automated applicant status updates.

### 2.3 Messaging

- Add message search, pinned messages, folders, disappearing messages, editing, and recall.
- Make presence and typing state resilient across API replicas.
- Add block-aware conversation creation and stronger spam controls.
- Group and batch notifications to reduce duplicate alerts.
- Add optional end-to-end encryption for sensitive health conversations.

### 2.4 Marketplace

- Reserve inventory during checkout and make payment/order operations transactional.
- Support split payments and partial refunds for multi-store orders.
- Add return pickup scheduling and reverse logistics.
- Improve seller dashboards with cancellation, late-shipment, return, dispute, and conversion metrics.
- Clearly separate sponsored products from organic ranking.

### 2.5 Veterinary care

- Add clinical drafts, finalization, amendments, vet signatures, and prescription sealing.
- Add diagnosis codes, lab results, referrals, and consent-based record sharing.
- Prevent completion of appointments with unfinished required clinical data.
- Add clinic revenue, no-show, capacity, and patient-return analytics.

### 2.6 Lost and found

- Replace city-only matching with coordinate or PostGIS-based proximity searches.
- Match by breed, color, markings, microchip, time window, and location.
- Add sighting verification, geofenced alerts, incident timelines, and evidence uploads.
- Expire exact location information and avoid exposing owner coordinates.

## 3. Technical improvements

### 3.1 Architecture

- Move direct `ApplicationDbContext` usage out of API controllers into application/infrastructure services.
- Standardize a consistent CQRS or service-module pattern.
- Split large controllers and services where unrelated operations are grouped.
- Enable stronger warnings and .NET analyzers in CI.
- Adopt consistent API problem details and stable application error codes.
- Make direct database-to-controller calls auditable and permission-testable.

### 3.2 Transactions and consistency

- Run EF migrations through a dedicated Kubernetes job rather than API startup.
- Add atomic outbox claiming or advisory-lock ownership for dispatcher replicas.
- Partition and index outbox events by status and next-attempt time.
- Reuse Kafka producers and avoid creating a producer for each DLQ write.
- Classify transient and permanent consumer failures.
- Add dead-letter replay/redrive management to the admin UI.
- Add workflows/sagas for adoption, checkout, and cancellation operations.

### 3.3 Idempotency and payments

- Add a durable HTTP idempotency-key store for orders, bookings, payments, and refunds.
- Return the original response for repeated successful requests.
- Add checkout stock reservations with expiry.
- Add gateway reconciliation and an immutable payment audit ledger.
- Add retry-safe status callbacks and duplicate-callback protection.

### 3.4 Performance

- Audit EF tracking and identify and remove N+1 queries.
- Require pagination and bounded ordering on every list endpoint.
- Add dashboard read models or projections instead of repeated cross-domain joins.
- Add cache stampede protection and key/version invalidation.
- Add ETags or conditional requests for frequently edited records.
- Profile PostgreSQL and optimize JSONB and full-text-search indexes.
- Add Redis and database capacity monitoring.

### 3.5 Security

- Move to short-lived credentials plus secure refresh-token rotation.
- Add passkeys/WebAuthn and backup recovery codes.
- Improve session management with revocation and logout everywhere.
- Require step-up authentication for ownership transfer, health export, refund, and role changes.
- Strengthen malware, MIME, metadata, and image validation for uploads.
- Sign and expire object-storage URLs consistently.
- Add impossible-travel, repeated-OTP, and account-enumeration detection.
- Replace fail-open Redis security behavior with an explicit degraded-mode policy.

### 3.6 Observability

- Add OpenTelemetry traces and metrics across API, workers, Kafka, PostgreSQL, and Redis.
- Track booking, checkout, notification, outbox, webhook, and DLQ latency.
- Alert on database pool exhaustion, Kafka lag, failed outboxes, Redis saturation, payment failures, and 5xx spikes.
- Include build version and Git SHA in logs, traces, and health responses.
- Define and track service-level objectives for critical workflows.

### 3.7 Testing

- Add permission and object-level authorization tests for every pet-sensitive operation.
- Use PostgreSQL Testcontainers for EF transactions, migrations, JSONB, and search.
- Add Kafka and webhook contract tests.
- Add frontend component and integration tests for critical workflows.
- Add Playwright flows for login, pet management, booking, checkout, adoption, and admin approval.
- Add fuzz/property tests for validation and file metadata.
- Add CI smoke tests for Docker Compose and Kubernetes manifests.

### 3.8 Deployment and operations

- Pin and sign container images.
- Add separate migration and seed jobs.
- Configure pod disruption budgets and anti-affinity.
- Add secret rotation and external-secret-manager integration.
- Add backup/restore runbooks and regular restore drills.
- Add one-click staging and production promotion workflows.
- Separate development, test, staging, and production Kafka and storage configuration.

## 4. User experience improvements

- Replace UUID inputs with searchable pet, user, product, and store pickers.
- Add guided workflows for pet onboarding, adoption, booking, and checkout.
- Add autosave and draft recovery for long forms.
- Make loading, unavailable, partial-result, and retry states consistent.
- Improve mobile navigation and responsive form density.
- Add recent items, search history, favorites, and saved filters.
- Add quiet hours, notification grouping, mute controls, and category preferences.
- Use optimistic updates only where rollback and duplicate protection are reliable.
- Add undo for reversible destructive actions.
- Improve photo preview, crop, compression, and upload progress.
- Add explicit empty states with a clear next action.
- Keep accessibility semantics consistent in dialogs, validation, tables, and toasts.

## 5. Recommended implementation order

1. **Reliability:** idempotency, migration separation, DLQ replay, OpenTelemetry, and PostgreSQL test coverage.
2. **Trust and security:** passkeys, sessions, consent, upload security, step-up authorization, and audit history.
3. **Core workflows:** waitlists, deposits, recurring care, care plans, live delivery, and checkout reservations.
4. **Commerce:** variants, subscriptions, payout ledger, settlements, and return logistics.
5. **Community:** groups, local events, richer adoption matching, and moderation tools.
6. **Discovery:** Bengali/English i18n, accessibility, PWA, push notifications, and advanced search.
7. **Advanced intelligence:** AI assistance and forecasting only after consent, records, and telemetry are reliable.
