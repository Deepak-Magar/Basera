# 08 — Identity module: users, tenants, memberships, policies

## What was built

The first real module, `Basera.Modules.Identity`. It has no screens yet:

- **Domain:**
  - `Tenant`: name, legal name, PAN, address, phone, logo path, plan, trial end, status. `Tenant.Register` starts a 60-day Starter trial.
  - `TenantMembership`: user, tenant, `Role`.
  - The `Role` enum: Owner, Manager, Warden, Cook, Resident, Parent.
- **Infrastructure:**
  - `ApplicationUser : IdentityUser<Guid>` with `TenantId`, `DisplayName`, `PreferredLanguage`, `PhoneVerifiedAt`.
  - `IdentityDbContext` (schema `identity`) on `TenantDbContext`, with migration `InitialIdentity`.
  - `SignInLookup`.
  - `AddBaseraIdentity()`: Identity core, the cookie scheme `.Basera.Auth`, and the five role policies.
- **Shared:** `BaseraPolicies.Require*` and `BaseraRoles` as string constants.
- **Host:** `app.UseAuthentication()` between the rate limiter and tenant resolution.

## Why it is shaped this way

**The user is tenant-owned.** `ApplicationUser` implements `ITenantOwned`, so `UserManager` only ever sees the current tenant's users. An owner listing staff can't stumble onto another hostel's warden. Without a tenant, the store throws rather than searching everyone (test `Without_a_tenant_the_user_store_throws...`).

**The one justified `IgnoreQueryFilters`: the login path.** On `/account/login` nobody is signed in, so there is no tenant claim. A filtered query for "the user with phone 98…" would throw, because it can't know which tenant to look in. `SignInLookup.FindAsync` is the single place that asks across tenants. Three things keep it safe:

1. **The question has one answer.** Phone (`UserName`) and email have unique indexes across all tenants, so a login names exactly one user.
2. **Only ids come back** (`SignInCandidate(UserId, TenantId)`). No entity leaves unfiltered.
3. **The caller switches back to the normal path:** it sets `TenantContext` to that tenant and loads the user through `UserManager`. The password check, lockout counters and claims all then run tenant-filtered, and `TenantInterceptor` checks every write.

Anywhere else, `IgnoreQueryFilters` in Identity is a review finding.

**Uniqueness lives in the database.** Identity's own duplicate check calls `FindByNameAsync`, which is filtered, so it only sees one tenant. The unique index is what refuses a phone number already used in another hostel (test `A_phone_number_belongs_to_one_user_across_all_tenants`). Feature 3's registration checks first through `SignInLookup`, to show a friendly message.

**Why not `IdentityDbContext<TUser>`.** C# has single inheritance, and the context must be a `TenantDbContext` to get the filters and interceptors. So it maps the four Identity tables itself, copying `IdentityUserContext`'s configuration. `AddEntityFrameworkStores` sees a custom context and uses `UserOnlyStore`.

**Why roles are not Identity roles.** A role is per tenant. `TenantMembership` holds it, and Feature 3 turns it into the `role` claim. Policies check that claim, never `IsInRole` against an Identity role table.

**Why `ApplicationUser` is not in Domain.** It derives from an ASP.NET Core type, and the architecture test keeps Domain framework-free.

**The cookie and the tenant.** The security stamp check reloads the user from inside `UseAuthentication`, before `TenantResolutionMiddleware` has run. The cookie event therefore sets the tenant from the cookie's own `tenant_id` claim first.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/SignInLookup.cs`
2. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/IdentityDbContext.cs`
3. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/IdentityServiceCollectionExtensions.cs`
4. `src/Modules/Identity/Basera.Modules.Identity/Domain/Tenant.cs`
5. `tests/Basera.IntegrationTests/Identity/IdentityModelTests.cs`

## Questions you should be able to answer

1. If `SignInLookup` returned the `ApplicationUser` entity instead of ids, and the caller changed and saved it without setting the tenant, what would happen, and which component would stop it?
2. Why would `options.User.RequireUniqueEmail = true` give a false sense of safety in this design?
