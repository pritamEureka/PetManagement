import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ProtectedRoute } from "@/routes/ProtectedRoute";

// Layouts
import { AppLayout } from "@/components/layout/AppLayout";
import { PublicLayout } from "@/components/layout/PublicLayout";

// Public pages
import { LandingPage } from "@/pages/LandingPage";
import { LoginPage } from "@/pages/LoginPage";
import { RegisterPage } from "@/pages/RegisterPage";
import { OnboardingPage } from "@/pages/OnboardingPage";

// In-app
import { FeedPage } from "@/pages/FeedPage";
import { PostDetailPage } from "@/pages/PostDetailPage";
import { UserDashboardPage } from "@/pages/UserDashboardPage";
import { UserProfilePage } from "@/pages/UserProfilePage";
import { SavedPostsPage } from "@/pages/feed/SavedPostsPage";
import { MyPostsPage } from "@/pages/feed/MyPostsPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";

// Pets
import { PetsPage } from "@/pages/pets/PetsPage";
import { PetDetailPage } from "@/pages/pets/PetDetailPage";

// Adoption
import { AdoptionListPage } from "@/pages/adoption/AdoptionListPage";
import { AdoptionDetailPage } from "@/pages/adoption/AdoptionDetailPage";
import { MyAdoptionListingsPage } from "@/pages/adoption/MyAdoptionListingsPage";
import { SavedAdoptionsPage } from "@/pages/adoption/SavedAdoptionsPage";
import { AdoptionRequestFormPage } from "@/pages/adoption/AdoptionRequestFormPage";
import { AdoptionApprovalPage } from "@/pages/admin/AdoptionApprovalPage";

// Messaging
import { MessagingPage } from "@/pages/messaging/MessagingPage";

// Vets
import { VetsPage } from "@/pages/vets/VetsPage";
import { VetDetailPage } from "@/pages/vets/VetDetailPage";
import { AppointmentBookingPage } from "@/pages/vets/AppointmentBookingPage";
import { DoctorRegistrationPage } from "@/pages/vets/DoctorRegistrationPage";
import { AvailabilityManagementPage } from "@/pages/vets/AvailabilityManagementPage";
import { MyAppointmentsPage } from "@/pages/vets/MyAppointmentsPage";
import { PrescriptionUploadPage } from "@/pages/vets/PrescriptionUploadPage";
import { DoctorApprovalPage } from "@/pages/admin/DoctorApprovalPage";

// Store
import { MarketplacePage } from "@/pages/store/MarketplacePage";
import { ProductDetailPage } from "@/pages/store/ProductDetailPage";
import { CartPage } from "@/pages/store/CartPage";
import { CheckoutPage } from "@/pages/store/CheckoutPage";
import { OrdersPage } from "@/pages/store/OrdersPage";
import { OrderDetailPage } from "@/pages/store/OrderDetailPage";
import { AddressBookPage } from "@/pages/store/AddressBookPage";
import { StoreRegistrationPage } from "@/pages/store/StoreRegistrationPage";
import { ProductManagementPage } from "@/pages/store/ProductManagementPage";
import { ProductEditPage } from "@/pages/store/ProductEditPage";
import { InventoryManagementPage } from "@/pages/store/InventoryManagementPage";
import { StoreOrderManagementPage } from "@/pages/store/StoreOrderManagementPage";

// Admin marketplace
import { StoreApprovalPage } from "@/pages/admin/StoreApprovalPage";
import { ProductModerationPage } from "@/pages/admin/ProductModerationPage";
import { CommissionConfigPage } from "@/pages/admin/CommissionConfigPage";

// Security / moderation
import { ReportedContentPage } from "@/pages/admin/ReportedContentPage";
import { SuspendedPage } from "@/pages/SuspendedPage";
import { UnauthorizedPage } from "@/pages/UnauthorizedPage";

// Admin v2 — dedicated layout, new pages
import { AdminLayout } from "@/components/admin/AdminLayout";
import { AdminOrderManagementPage } from "@/pages/admin/OrderManagementPage";
import { AdminAppointmentManagementPage } from "@/pages/admin/AppointmentManagementPage";
import { AdminCategoryManagementPage } from "@/pages/admin/CategoryManagementPage";
import { AdminFeedModerationPage } from "@/pages/admin/FeedModerationPage";
import { AdminReviewManagementPage } from "@/pages/admin/ReviewManagementPage";
import { AdminNotificationsPage } from "@/pages/admin/NotificationsAdminPage";
import { AdminAuditLogPage } from "@/pages/admin/AuditLogPage";
import { AdminAnalyticsPage } from "@/pages/admin/AnalyticsPage";
import { AdminSystemSettingsPage } from "@/pages/admin/SystemSettingsPage";

// Dashboards
import { VetDashboardPage } from "@/pages/dashboards/VetDashboardPage";
import { StoreDashboardPage } from "@/pages/dashboards/StoreDashboardPage";

// Admin
import { AdminDashboardPage } from "@/pages/AdminDashboardPage";
import { UsersPage } from "@/pages/admin/UsersPage";
import { RolesPage } from "@/pages/admin/RolesPage";
import { ApprovalsPage } from "@/pages/admin/ApprovalsPage";
import { ReportsPage } from "@/pages/admin/ReportsPage";

const qc = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: false } }
});

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <BrowserRouter>
        <Routes>
          {/* Public marketing */}
          <Route element={<PublicLayout />}>
            <Route path="/" element={<LandingPage />} />
            <Route path="/privacy" element={<PlaceholderPage title="Privacy" />} />
            <Route path="/terms"   element={<PlaceholderPage title="Terms" />} />
          </Route>

          {/* Auth */}
          <Route path="/login"    element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forbidden" element={<UnauthorizedPage />} />
          <Route path="/account/suspended" element={<SuspendedPage />} />

          {/* Onboarding (no sidebar) */}
          <Route element={<ProtectedRoute />}>
            <Route path="/onboarding" element={<OnboardingPage />} />
          </Route>

          {/* In-app — sidebar layout */}
          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/home" element={<UserDashboardPage />} />
              <Route path="/feed" element={<FeedPage />} />
              <Route path="/feed/saved" element={<SavedPostsPage />} />
              <Route path="/feed/mine" element={<MyPostsPage />} />
              <Route path="/feed/:id" element={<PostDetailPage />} />
              <Route path="/u/:userId" element={<UserProfilePage />} />

              <Route element={<ProtectedRoute permission="pets.view" />}>
                <Route path="/pets" element={<PetsPage />} />
                <Route path="/pets/:id" element={<PetDetailPage />} />
              </Route>

              <Route element={<ProtectedRoute permission="adoption.view" />}>
                <Route path="/adoption" element={<AdoptionListPage />} />
                <Route path="/adoption/mine" element={<MyAdoptionListingsPage />} />
                <Route path="/adoption/saved" element={<SavedAdoptionsPage />} />
                <Route path="/adoption/wanted/new" element={<AdoptionRequestFormPage />} />
                <Route path="/adoption/:id" element={<AdoptionDetailPage />} />
              </Route>

              <Route element={<ProtectedRoute permission="messaging.view" />}>
                <Route path="/messages" element={<MessagingPage />} />
              </Route>

              <Route element={<ProtectedRoute permission="vets.view" />}>
                <Route path="/vets" element={<VetsPage />} />
                <Route path="/vets/:id" element={<VetDetailPage />} />
                <Route path="/vets/:doctorId/book" element={<AppointmentBookingPage />} />
              </Route>

              <Route path="/appointments" element={<MyAppointmentsPage />} />

              <Route element={<ProtectedRoute permission="vets.edit" />}>
                <Route path="/dashboard/vet/profile" element={<DoctorRegistrationPage />} />
                <Route path="/dashboard/vet/availability" element={<AvailabilityManagementPage />} />
                <Route path="/dashboard/vet/appointments/:appointmentId/prescription" element={<PrescriptionUploadPage />} />
              </Route>

              {/* Vet registration is available to any authenticated user */}
              <Route path="/vets/register" element={<DoctorRegistrationPage />} />

              <Route element={<ProtectedRoute permission="products.view" />}>
                <Route path="/store" element={<MarketplacePage />} />
                <Route path="/store/products/:id" element={<ProductDetailPage />} />
                <Route path="/store/register" element={<StoreRegistrationPage />} />
                <Route path="/cart" element={<CartPage />} />
                <Route path="/checkout" element={<CheckoutPage />} />
                <Route path="/orders" element={<OrdersPage />} />
                <Route path="/orders/:id" element={<OrderDetailPage />} />
                <Route path="/account/addresses" element={<AddressBookPage />} />
              </Route>

              {/* Seller surfaces */}
              <Route element={<ProtectedRoute permission="products.create" />}>
                <Route path="/dashboard/store/products" element={<ProductManagementPage />} />
                <Route path="/dashboard/store/products/new" element={<ProductEditPage />} />
                <Route path="/dashboard/store/products/:id" element={<ProductEditPage />} />
                <Route path="/dashboard/store/inventory" element={<InventoryManagementPage />} />
                <Route path="/dashboard/store/orders" element={<StoreOrderManagementPage />} />
              </Route>

              {/* Role dashboards */}
              <Route element={<ProtectedRoute permission="vets.edit" />}>
                <Route path="/dashboard/vet" element={<VetDashboardPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="products.edit" />}>
                <Route path="/dashboard/store" element={<StoreDashboardPage />} />
              </Route>
            </Route>
          </Route>

          {/* Admin — dedicated layout with its own sidebar / topbar. */}
          <Route element={<ProtectedRoute anyOf={["users.view", "roles.view", "moderation.view"]} />}>
            <Route element={<AdminLayout />}>
              <Route path="/admin" element={<AdminDashboardPage />} />

              <Route element={<ProtectedRoute permission="users.view" />}>
                <Route path="/admin/users" element={<UsersPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="roles.view" />}>
                <Route path="/admin/roles" element={<RolesPage />} />
              </Route>

              {/* Approvals */}
              <Route element={<ProtectedRoute anyOf={["adoption.approve", "moderation.moderate", "vets.approve", "stores.approve"]} />}>
                <Route path="/admin/approvals" element={<ApprovalsPage />} />
              </Route>
              <Route element={<ProtectedRoute anyOf={["adoption.approve", "adoption.reject"]} />}>
                <Route path="/admin/adoption-approvals" element={<AdoptionApprovalPage />} />
              </Route>
              <Route element={<ProtectedRoute anyOf={["vets.approve", "vets.reject", "vets.suspend"]} />}>
                <Route path="/admin/doctor-approvals" element={<DoctorApprovalPage />} />
              </Route>
              <Route element={<ProtectedRoute anyOf={["stores.approve", "stores.reject", "sellers.approve"]} />}>
                <Route path="/admin/store-approvals" element={<StoreApprovalPage />} />
              </Route>

              {/* Moderation */}
              <Route element={<ProtectedRoute anyOf={["moderation.view", "moderation.moderate"]} />}>
                <Route path="/admin/abuse-reports" element={<ReportedContentPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="posts.moderate" />}>
                <Route path="/admin/feed-moderation" element={<AdminFeedModerationPage />} />
              </Route>
              <Route element={<ProtectedRoute anyOf={["products.feature", "products.publish", "products.edit"]} />}>
                <Route path="/admin/product-moderation" element={<ProductModerationPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="reviews.moderate" />}>
                <Route path="/admin/review-moderation" element={<AdminReviewManagementPage />} />
              </Route>

              {/* Commerce */}
              <Route element={<ProtectedRoute permission="settings.edit" />}>
                <Route path="/admin/categories" element={<AdminCategoryManagementPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="orders.view" />}>
                <Route path="/admin/orders" element={<AdminOrderManagementPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="appointments.view" />}>
                <Route path="/admin/appointments" element={<AdminAppointmentManagementPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="settings.view" />}>
                <Route path="/admin/commissions" element={<CommissionConfigPage />} />
              </Route>

              {/* System */}
              <Route element={<ProtectedRoute permission="notifications.view" />}>
                <Route path="/admin/notifications" element={<AdminNotificationsPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="audit.view" />}>
                <Route path="/admin/audit" element={<AdminAuditLogPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="reports.view" />}>
                <Route path="/admin/reports" element={<AdminAnalyticsPage />} />
                <Route path="/admin/legacy-reports" element={<ReportsPage />} />
              </Route>
              <Route element={<ProtectedRoute permission="settings.view" />}>
                <Route path="/admin/settings" element={<AdminSystemSettingsPage />} />
              </Route>
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
