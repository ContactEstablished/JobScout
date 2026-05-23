# Phase 1: Authentication & User Accounts

**Priority: CRITICAL** | **Branch:** `upgrade/dotnet10`

> Reference: [Roadmap.md](Roadmap.md) — Phase 1 (sections 1.1 through 1.5)

---

## Status Overview

| # | Task | Status |
|---|------|--------|
| 1.1 | ASP.NET Core Identity setup | DONE |
| 1.2 | JWT authentication for the API | DONE |
| 1.3 | User-to-Profile relationship | DONE |
| 1.4 | Blazor authentication state (provider, handler, pages) | DONE |
| 1.5 | TopBar auth integration + sign out | DONE |
| 1.6 | End-to-end verification | TODO |
| 1.7 | Commit & PR | TODO |

---

## Completed Tasks

### 1.1 ASP.NET Core Identity Setup

- [x] `ApplicationUser` entity — `src/JobScout.Infrastructure/Identity/ApplicationUser.cs`
  - Extends `IdentityUser` with `DisplayName`, `CreatedAt`, `ICollection<SearchProfile> Profiles`
- [x] `JobScoutDbContext` inherits `IdentityDbContext<ApplicationUser>`
  - FK config: `SearchProfile.UserId` -> `ApplicationUser.Id` (cascade delete, indexed)
- [x] EF Core migration generated — `20260522192358_InitialCreate.cs`
  - All Identity tables (AspNetUsers, AspNetRoles, claims, logins, tokens) + app tables
- [x] Password policy: 8+ chars, uppercase, lowercase, digit required
- [x] Package: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.8

### 1.2 JWT Authentication for the API

- [x] `AuthController` — `src/JobScout.Api/Controllers/AuthController.cs`
  - `POST /api/auth/register` — creates user, returns JWT + user info
  - `POST /api/auth/login` — validates credentials, returns JWT + user info
  - Claims: `NameIdentifier`, `EmailAddress`, `display_name` | 7-day expiry
- [x] `[Authorize]` on all controllers: Profiles, Jobs, Ratings, Metrics
- [x] JWT Bearer configured in `src/JobScout.Api/Program.cs`
- [x] `ICurrentUserService` — `src/JobScout.Core/Interfaces/ICurrentUserService.cs`
- [x] `CurrentUserService` — `src/JobScout.Infrastructure/Identity/CurrentUserService.cs`
- [x] Auth DTOs — `src/JobScout.Core/DTOs/AuthDtos.cs`
  - `RegisterRequest`, `LoginRequest`, `AuthResponse`, `UserDto`
- [x] Dev JWT key in `appsettings.Development.json`
- [x] Package: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.8

### 1.3 User-to-Profile Relationship

- [x] `UserId` (string, required) on `SearchProfile` model
- [x] `IProfileRepository` — all methods accept `string userId`
- [x] `ProfileRepository` — scopes all queries by userId
- [x] `ProfilesController` injects `ICurrentUserService`, passes userId to all calls
- [x] `DbSeeder` creates dev user `dev@jobscout.local` / `DevPass123!` with seeded profile

### 1.4 Blazor Authentication State

- [x] `JwtAuthenticationStateProvider` — `src/JobScout.Web/Auth/JwtAuthenticationStateProvider.cs`
  - Reads JWT from localStorage, parses claims, checks expiration
  - `MarkUserAsAuthenticated(token)` and `MarkUserAsLoggedOut()`
- [x] `AuthTokenHandler` — `src/JobScout.Web/Auth/AuthTokenHandler.cs`
  - DelegatingHandler attaching `Authorization: Bearer` header to all requests
- [x] `Login.razor` + `Register.razor` with dark-mode styling
  - Uses `MinimalLayout` (no sidebar/topbar on auth pages)
- [x] `App.razor` — `CascadingAuthenticationState` + `AuthorizeRouteView`
  - Unauthorized users redirected via `RedirectToLogin` component
- [x] `Program.cs` — registers auth services, `AuthTokenHandler`, `AuthService` HTTP client
- [x] Packages: `Microsoft.AspNetCore.Components.Authorization` 10.0.8, `System.IdentityModel.Tokens.Jwt` 8.18.0

### Verified Working

- Registration: `POST /api/auth/register` returns JWT ✓
- Login: `POST /api/auth/login` returns JWT ✓
- Protected endpoints return 401 without token ✓
- Protected endpoints return 200 with valid JWT ✓
- User scoping: User A cannot see User B's profiles ✓
- Dev user login works ✓
- Solution builds: 0 errors, 0 warnings across all 5 projects ✓

---

### 1.5 TopBar Auth Integration + Sign Out

- [x] Injected `AuthenticationStateProvider` and `NavigationManager`
- [x] `LoadUserInfo()` reads `display_name` and `email` claims from JWT auth state
- [x] User pill shows authenticated user's `DisplayName` and derived initials
- [x] Click-to-toggle dropdown with user avatar, name, email, and "Sign out" button
- [x] Sign out calls `MarkUserAsLoggedOut()`, clears JWT, navigates to `/login`
- [x] Click-outside-to-close via transparent backdrop
- [x] Subscribes to `AuthenticationStateChanged` for live updates
- [x] Proper `Dispose()` cleanup for both event subscriptions
- [x] Dropdown CSS added to `app.css` — consistent with existing dark theme styling

---

## Remaining Tasks

### 1.6 End-to-End Verification

Run both the API and the Blazor frontend together and verify the full flow:

- [ ] Navigating to the app unauthenticated redirects to `/login`
- [ ] Register a new account — redirects to home feed
- [ ] Page refresh — remains authenticated (token persisted)
- [ ] Sign out — redirects to `/login`
- [ ] Sign back in with created credentials
- [ ] Profile CRUD works for the logged-in user
- [ ] Dev user can log in (`dev@jobscout.local` / `DevPass123!`)
- [ ] Second user (new browser/incognito) cannot see first user's profiles

---

### 1.7 Commit & PR

- [ ] Stage all Phase 1 changes
- [ ] Commit with descriptive message
- [ ] Create PR targeting `main`
