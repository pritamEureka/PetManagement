# Pawzaroo - Pet Management Platform (Frontend)

A comprehensive, fully responsive React frontend for the **Pawzaroo** pet management platform. Pawzaroo connects pet owners, veterinarians, store sellers, delivery personnel, and administrators in a unified ecosystem for pet care, adoption, social sharing, and commerce.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Available Scripts](#available-scripts)
- [Architecture](#architecture)
- [Theming & Dark Mode](#theming--dark-mode)
- [Responsive Design](#responsive-design)
- [Authentication & Authorization](#authentication--authorization)
- [Routing](#routing)
- [UI Components](#ui-components)

---

## Features

### User Features
- **Social Feed** - Create posts, like, comment, save/bookmark, and share pet content
- **Pet Management** - Register pets, track health records, and manage profiles
- **Adoption** - Browse adoption listings, create adoption posts, save favorites, submit requests
- **Messaging** - Real-time chat between users via SignalR WebSocket integration
- **Notifications** - Bell icon with notification center accessible from all pages
- **User Profile** - Manage account details, avatar, and preferences

### Veterinary Module
- **Vet Directory** - Browse and search verified veterinarians
- **Appointment Booking** - Book appointments with available vets
- **Vet Dashboard** - Manage clinic, set availability, upload prescriptions
- **Doctor Registration** - Multi-step registration form for new veterinarians

### E-Commerce / Store
- **Marketplace** - Browse pet products with filtering (category, price range, rating)
- **Product Detail** - Full product pages with reviews and ratings
- **Shopping Cart & Checkout** - Full e-commerce flow with address management
- **Store Dashboard** - Seller portal for product management, inventory, and orders
- **Store Registration** - Registration flow for new sellers

### Admin Panel
- **User Management** - View, search, suspend, and manage all users
- **Roles & Permissions** - RBAC configuration with granular permission gates
- **Approvals** - Approve/reject vets, stores, and adoption listings
- **Moderation** - Feed posts, products, reviews, and abuse reports
- **Commerce Admin** - Categories, orders, appointments, commission config
- **Analytics & Reports** - Platform-wide analytics and audit logs
- **System Settings** - Platform configuration and notifications

### Delivery Module
- **Delivery Dashboard** - Manage assigned deliveries and update statuses

---

## Tech Stack

| Category | Technology |
|----------|------------|
| Framework | React 18 + TypeScript |
| Build Tool | Vite 5 |
| Styling | Tailwind CSS 3 + tailwindcss-animate |
| Component Library | Radix UI (Avatar, Dialog, Dropdown, Select, Tabs, Tooltip, Sheet) |
| State Management | Zustand 5 |
| Server State | TanStack React Query 5 |
| Forms | React Hook Form + Zod validation |
| Routing | React Router DOM 6 |
| HTTP Client | Axios |
| Real-time | Microsoft SignalR |
| Charts | Recharts |
| Icons | Lucide React |
| Notifications | Sonner (toast) |
| Date Utils | date-fns |

---

## Project Structure

```
src/
├── api/                    # API client modules (auth, pets, vets, store, etc.)
│   ├── client.ts           # Axios instance with interceptors
│   ├── auth.ts             # Authentication endpoints
│   ├── pets.ts             # Pet CRUD operations
│   ├── adoption.ts         # Adoption listings & requests
│   ├── vets.ts             # Veterinary services
│   ├── marketplace.ts      # Product catalog
│   ├── store.ts            # Seller store management
│   ├── messages.ts         # Messaging API
│   ├── posts.ts            # Social feed
│   ├── admin.ts            # Admin operations
│   ├── adminV2.ts          # Extended admin endpoints
│   ├── rbac.ts             # Roles & permissions
│   └── security.ts         # Security & moderation
├── components/
│   ├── admin/              # Admin-specific components (AdminLayout)
│   ├── auth/               # Auth guards and wrappers
│   ├── common/             # ErrorBoundary, shared utilities
│   ├── feed/               # Feed/post components
│   ├── layout/             # AppLayout, PublicLayout
│   ├── messaging/          # Chat UI components
│   ├── security/           # Security-related components
│   ├── theme/              # ThemeToggle (dark/light mode)
│   ├── ui/                 # Reusable UI primitives (Radix-based)
│   └── vet/               # Vet-specific components
├── hooks/
│   ├── useChatHub.ts       # SignalR real-time chat hook
│   └── usePermissions.ts   # RBAC permission checking hook
├── lib/
│   └── utils.ts            # cn() utility (clsx + tailwind-merge)
├── pages/
│   ├── admin/              # 19 admin pages
│   ├── adoption/           # Adoption flows (list, detail, create, saved)
│   ├── dashboards/         # Role-specific dashboards (vet, store)
│   ├── feed/               # Feed sub-pages (saved, my posts)
│   ├── messaging/          # Real-time messaging page
│   ├── pets/               # Pet management pages
│   ├── store/              # E-commerce pages (marketplace, cart, checkout, etc.)
│   └── vets/               # Vet directory, booking, registration
├── routes/
│   └── ProtectedRoute.tsx  # Permission-gated route wrapper
├── store/
│   ├── authStore.ts        # Zustand auth state (user, tokens, logout)
│   └── cartStore.ts        # Shopping cart state
├── App.tsx                 # Route definitions
├── main.tsx                # Entry point
└── index.css               # Tailwind directives + CSS custom properties
```

---

## Getting Started

### Prerequisites

- Node.js 18+ (LTS recommended)
- npm or pnpm

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd PetManagement/frontend

# Install dependencies
npm install

# Start development server
npm run dev
```

The app will be available at `http://localhost:5173`.

### Environment

The app expects a backend API. Configure the API base URL in `src/api/client.ts` or via environment variables as needed by your deployment.

---

## Available Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start Vite dev server with HMR |
| `npm run build` | Type-check with `tsc` then bundle with Vite |
| `npm run preview` | Preview production build locally |
| `npm run lint` | Run ESLint |

---

## Architecture

### Layouts

The app uses two primary authenticated layouts:

1. **AppLayout** (`src/components/layout/AppLayout.tsx`)
   - Used for all end-user pages (feed, pets, adoption, messaging, store, etc.)
   - Collapsible sidebar with smooth 300ms CSS transitions
   - Mobile: hamburger menu → Sheet (slide-in drawer)
   - Sidebar search input for filtering navigation items
   - Notification bell in the top navbar
   - Independent scroll isolation (sidebar and content scroll independently)

2. **AdminLayout** (`src/components/admin/AdminLayout.tsx`)
   - Dedicated admin panel with grouped navigation (Insights, People, Approvals, Moderation, Commerce, System)
   - Same collapsible sidebar pattern with animations
   - Breadcrumb trail derived from URL path
   - Global search bar in the header
   - Permission-gated nav items (invisible if user lacks permission)

3. **PublicLayout** - Minimal layout for landing/marketing pages

### State Management

- **Zustand** for client-side state (auth tokens, user session, cart)
- **React Query** for server state (data fetching, caching, background refetch)
- **React Hook Form + Zod** for form state and validation

### Real-time Communication

- **SignalR** hub connection via `useChatHub` hook for real-time messaging
- Automatic reconnection and connection state management

---

## Theming & Dark Mode

The app implements a CSS custom property theming system with light/dark mode support:

- Toggle via the `ThemeToggle` component (adds/removes `.dark` class on `<html>`)
- All colors defined as HSL CSS variables in `src/index.css`
- Both `:root` (light) and `.dark` (dark) variants for every color token
- Tailwind's `darkMode: ["class"]` configuration

### Color tokens include:
`--background`, `--foreground`, `--card`, `--popover`, `--primary`, `--secondary`, `--muted`, `--accent`, `--destructive`, `--border`, `--input`, `--ring`

All UI components (selects, dropdowns, tooltips, dialogs) properly use `bg-popover` / `bg-card` for theme-aware backgrounds.

---

## Responsive Design

The entire application is fully responsive across all breakpoints:

### Breakpoints (Tailwind defaults)
- `sm`: 640px
- `md`: 768px (sidebar collapse point for AppLayout)
- `lg`: 1024px (sidebar collapse point for AdminLayout)
- `xl`: 1280px
- `2xl`: 1400px (max container width)

### Key responsive patterns:
- **Collapsible sidebars** - Smooth width/opacity transitions; collapse to icon-only on desktop, slide-in drawer on mobile
- **Adaptive grids** - `grid-cols-1 sm:grid-cols-2 md:grid-cols-3` patterns throughout
- **Responsive filters** - Select/filter controls go `w-full` on mobile, fixed width on larger screens
- **Mobile messaging** - Conversation list and chat pane toggle with a back button on small screens
- **Scroll isolation** - `h-screen overflow-hidden` root prevents body scroll; sidebar and content scroll independently

---

## Authentication & Authorization

### Auth Flow
1. User logs in via `/login` → tokens stored in Zustand (`authStore`)
2. Axios interceptor attaches Bearer token to all API requests
3. `ProtectedRoute` wrapper checks authentication and permissions before rendering

### RBAC (Role-Based Access Control)
- `usePermissions()` hook exposes `can(permission)` and `canAny(permissions[])` helpers
- Routes are gated with `<ProtectedRoute permission="..." />` or `<ProtectedRoute anyOf={[...]} />`
- Sidebar items are conditionally rendered based on user permissions
- Roles: Pet Owner, Vet, Store Seller, Delivery, Admin (with fine-grained permissions)

### Key permission domains:
`users`, `roles`, `pets`, `posts`, `adoption`, `messaging`, `vets`, `appointments`, `products`, `orders`, `stores`, `sellers`, `delivery`, `moderation`, `reviews`, `reports`, `audit`, `notifications`, `settings`

---

## Routing

All routes are defined in `src/App.tsx`. Key route groups:

| Path | Layout | Description |
|------|--------|-------------|
| `/` | PublicLayout | Landing page |
| `/login`, `/register` | None | Auth pages |
| `/onboarding` | None (protected) | New user setup |
| `/home` | AppLayout | User dashboard |
| `/feed/*` | AppLayout | Social feed |
| `/pets/*` | AppLayout | Pet management |
| `/adoption/*` | AppLayout | Adoption system |
| `/messages` | AppLayout | Real-time messaging |
| `/vets/*` | AppLayout | Vet directory & booking |
| `/store/*` | AppLayout | E-commerce marketplace |
| `/orders/*` | AppLayout | Order history |
| `/dashboard/vet/*` | AppLayout | Vet pro dashboard |
| `/dashboard/store/*` | AppLayout | Seller dashboard |
| `/admin/*` | AdminLayout | Admin panel (20+ pages) |

---

## UI Components

Reusable primitives in `src/components/ui/`:

| Component | Base | Description |
|-----------|------|-------------|
| `avatar` | Radix Avatar | User/pet profile images with fallback |
| `badge` | CVA | Status indicators and tags |
| `button` | CVA + Radix Slot | Multi-variant button (ghost, outline, destructive, etc.) |
| `card` | div | Content container with header/footer |
| `checkbox` | HTML | Form checkbox |
| `dialog` | Radix Dialog | Modal dialogs |
| `dropdown-menu` | Radix DropdownMenu | Context menus and action menus |
| `input` | HTML | Text input with consistent styling |
| `label` | Radix Label | Form labels |
| `password-input` | Custom | Password field with show/hide toggle |
| `scroll-area` | Radix ScrollArea | Custom scrollbars |
| `select` | Radix Select | Themed dropdown select |
| `separator` | Radix Separator | Visual dividers |
| `sheet` | Radix Dialog | Slide-in panels (mobile sidebar) |
| `skeleton` | div | Loading placeholder animations |
| `sonner` | Sonner | Toast notifications |
| `tabs` | Radix Tabs | Tab navigation |
| `textarea` | HTML | Multi-line text input |
| `tooltip` | Radix Tooltip | Hover information tooltips |

All components use the `cn()` utility (clsx + tailwind-merge) for conditional class composition.

---

## Recent Updates

### Responsive & UI Overhaul (Latest)

- **Collapsible sidebars** - Both AppLayout and AdminLayout feature smooth animated sidebars that collapse to icon-only mode on desktop and convert to slide-in Sheet drawers on mobile
- **Sidebar search** - Both sidebars include a search input that live-filters navigation items
- **Independent scroll isolation** - Root uses `h-screen overflow-hidden`; sidebar and main content scroll independently without affecting each other
- **Notification bells** - Added to both layout navbars for quick access to notifications
- **Dark mode fixes** - Added missing `--popover` and `--popover-foreground` CSS variables; all dropdowns, selects, and tooltips now render correctly in both themes
- **Radix UI migration** - Replaced native `<select>` elements with Radix Select components for consistent theming
- **Sheet animations** - Added proper slide-in/slide-out and fade animations to the Sheet component
- **Responsive pages** - All pages updated with mobile-first grid layouts, responsive filter controls, and adaptive spacing
- **Messaging UX** - Mobile conversation list/chat toggle with back button navigation

---

## License

Private project. All rights reserved.
