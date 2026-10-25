# 11 — Staff invitations and membership management

## What was built

- **The first module screens.**
  - `/owner/staff`: staff list and pending invitations, with change role, remove and withdraw.
  - `/owner/staff/invite`: invite a phone as Manager, Warden or Cook.

  They live in `Basera.Modules.Identity/Presentation/`, which now builds with the Razor SDK. `ModuleLoader` adds module assemblies as MVC parts and the view location `/Presentation/Views/{area}/{controller}/{action}.cshtml`.
- **`StaffInvitation`** (tenant-owned): phone, role, token hash, 7-day expiry, accepted/revoked. **`TenantMembership.ChangeRole`**, limited to staff roles.
- **`InvitationService`** (invite, preview, accept, revoke) and **`StaffService`** (overview, change role, remove).
- **`/account/invite/{token}`** in the host's Account area: the invitee sets a name and password.
- **`TenantMemberRequirement`**, with handlers for memberships and invitations.
- **Tests:** 12 integration tests (the full flow, expiry, single use, replacement, tampering, another hostel's number, role change and removal, re-invite, owner protection, cross-tenant 404s, policy) and 10 unit tests (invitation states, role rules, handler).

## Why it is shaped this way

**A token that names its tenant.** The invitee isn't signed in, and invitations are tenant-filtered. The obvious solution, `IgnoreQueryFilters` and a search of all tenants by hash, would be a second unfiltered path. Instead the link is `<tenant id><secret>`:
1. Opening it sets `TenantContext` to that tenant.
2. The invitation is found by `SHA-256(secret)` with the normal filter.

Change the tenant part and you search a different hostel, where the hash doesn't exist. The secret's 256 random bits make a plain hash safe to store. That differs from OTPs, where 6 digits need a keyed HMAC.

**One `SaveChanges` again.** Accepting writes the user (or updates a former staff member's user), the membership and `AcceptedAt` together, with `HoldChanges()`. Calling `RemovePasswordAsync` and then `AddPasswordAsync` before that single save produced a concurrency failure on the user row. Setting the hash directly and making one store update fixed it.

**Removal must reach live sessions.** A cookie carries the role until it is rechecked. Removing a member or changing a role changes their security stamp, and the check interval is now 5 minutes instead of 30. A removed warden is out within 5 minutes; a changed role means signing in again.

**Belt and braces.** The query filter already makes another hostel's membership invisible, so the controller answers 404 before any authorization runs. The resource handler states the rule independently: "this membership's `TenantId` equals my `tenant_id` claim". If a future query forgot the filter, the handler would still say no. A unit test proves it denies another tenant and a missing claim.

**Owners are not staff.** Only Manager, Warden and Cook can be invited, changed or removed here. That prevents an owner locking themselves out, and stops anyone becoming an owner by invitation.

**Views in the module.** ARCHITECTURE §3.6 puts presentation in the module. The module's `_ViewStart` points at `_Layout`, which the view engine finds in the host's `Areas/Owner/Views/Shared`, so every Owner page shares one shell.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Application/InvitationService.cs`
2. `src/Modules/Identity/Basera.Modules.Identity/Domain/StaffInvitation.cs`
3. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/TenantMemberAuthorization.cs`
4. `src/Modules/Identity/Basera.Modules.Identity/Presentation/Controllers/StaffController.cs`
5. `tests/Basera.IntegrationTests/Identity/StaffInvitationTests.cs`

## Questions you should be able to answer

1. An attacker takes a valid invitation link and replaces the first 32 characters with their own hostel's id. Walk through what happens on the server, and why no row is found.
2. The owner removes a warden at 10:00. Until when can the warden's open phone browser still load staff pages, and what would you change to make it immediate?
