import type { ReactNode } from "react";

function Legal({ title, updated, children }: { title: string; updated: string; children: ReactNode }) {
  return (
    <article className="max-w-3xl mx-auto px-4 py-10 space-y-5 text-sm leading-relaxed [&_h2]:text-lg [&_h2]:font-semibold [&_h2]:mt-6 [&_ul]:list-disc [&_ul]:pl-5 [&_ul]:space-y-1">
      <header>
        <h1 className="text-3xl font-bold">{title}</h1>
        <p className="text-muted-foreground mt-1">Last updated {updated}</p>
      </header>
      {children}
    </article>
  );
}

export function PrivacyPage() {
  return (
    <Legal title="Privacy policy" updated="2 November 2026">
      <p>This policy explains what Pawzaroo collects, why, and the controls you have. It is a working draft and should be reviewed by legal counsel before launch.</p>

      <h2>What we collect</h2>
      <ul>
        <li><strong>Account data</strong> — name, email, phone, profile photo and location you enter.</li>
        <li><strong>Pet records</strong> — profiles, photos, vaccinations, medical history, medications, weight, care logs, expenses and insurance details you or your care team add.</li>
        <li><strong>Activity</strong> — posts, messages, bookings, orders, adoption and lost &amp; found reports.</li>
        <li><strong>Technical data</strong> — device and log information used for security and abuse prevention.</li>
      </ul>

      <h2>Who can see your pet's records</h2>
      <ul>
        <li>You, and people you add to a pet's care team, with the role you choose (viewer, caretaker or co-owner).</li>
        <li>A veterinarian you have booked an appointment with for that pet.</li>
        <li>Anyone holding a health-record share link you created, until it expires or you revoke it.</li>
        <li>Authorised Pawzaroo support staff, only when needed to help you.</li>
      </ul>

      <h2>Public QR tags</h2>
      <p>If you enable a QR tag, anyone who scans it sees your pet's name, photo, species, breed, colour, allergies and your first name. Your contact details are never shown; finders send you a message through Pawzaroo. You can turn the tag off at any time.</p>

      <h2>Lost &amp; found reports</h2>
      <p>Reports are visible to signed-in members and members in the same city may be notified. If you choose phone contact, your number is shown on the report. Close or delete the report once resolved.</p>

      <h2>Adoption hand-over</h2>
      <p>When you mark an adoption complete and choose to transfer a pet profile, the adopter receives the profile and vaccination records, plus other health history only if you allow it. Your expenses and insurance details are never transferred.</p>

      <h2>Your choices</h2>
      <ul>
        <li>Edit or delete pet records and photos at any time.</li>
        <li>Revoke share links and remove care-team members.</li>
        <li>Request a copy or deletion of your account data by contacting support.</li>
      </ul>
    </Legal>
  );
}

export function TermsPage() {
  return (
    <Legal title="Terms of service" updated="2 November 2026">
      <p>These terms govern your use of Pawzaroo. They are a working draft and should be reviewed by legal counsel before launch.</p>

      <h2>Your account</h2>
      <p>Keep your credentials safe and give accurate information. You are responsible for activity on your account.</p>

      <h2>Health information is not veterinary advice</h2>
      <p>Vaccine schedules, healthy weight ranges, care tips and reminders are general guidelines. Always follow the advice of a qualified veterinarian.</p>

      <h2>Marketplace and services</h2>
      <p>Stores, veterinarians and service providers are independent businesses approved by our team. Bookings for grooming, training, boarding and walking are agreements between you and the provider, and payment is settled with them directly unless stated otherwise.</p>

      <h2>Community rules</h2>
      <ul>
        <li>No false lost or found reports, scams or misleading adoption listings.</li>
        <li>No harassment, hate speech or content that harms animals.</li>
        <li>Only share pet records you have the right to share.</li>
      </ul>
      <p>We may remove content or suspend accounts that break these rules.</p>

      <h2>Changes</h2>
      <p>We may update these terms. Continued use after an update means you accept the new terms.</p>
    </Legal>
  );
}
