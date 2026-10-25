# 12 — Role-aware shell, profile, seed and smoke

## What was built

- **Areas are closed.**
  - `/owner` requires Owner or Manager; `/staff` any staff role; `/resident` and `/parent` their role.
  - Anonymous visitors go to sign-in, and the wrong role goes to `/account/denied`.
- **Landing by role.** After a password, OTP or invitation sign-in, the user goes to the `returnUrl` if it is local, else to `BaseraRoles.HomePath(role)`. A signed-in visit to `/` redirects too.
- **`_UserMenu`** in every layout: the name from the `name` claim, the hostel name from `ITenantDirectory` (Identity's first Contract, cached), the profile link, and sign-out.
- **Language on the user.** The header switch calls `ProfileService.SetLanguageAsync` when signed in and re-issues the cookie. The culture providers now read the `lang` claim first.
- **`/account/profile`** (`ProfileController` in the host's Account area): name and language, and change password for users who have one.
- **Dev seed.** `IDevSeeder` (Shared) with the runner `RunDevSeedersAsync`, and `IdentityDevSeeder`, which creates "Sunrise Boys Hostel, Kirtipur" with an owner, a warden, and resident `+9779800000001`. Run it with `dotnet run --project src/Basera.Web -- seed`; `scripts/dev.ps1` does this.
- **`scripts/smoke.ps1`.** It walks register → sign out/in → resident OTP → invite → accept against a running app.
- **Found and fixed:**
  - The header's language form never had an anti-forgery token, so without HTMX/JavaScript it returned 400.
  - The validation filter ignored `[Bind(Prefix)]`.

## Why it is shaped this way

**Why a landing rule in Shared.** Many places need "where does this role live?": login, OTP, invitations, `/`, profile's home link. `BaseraRoles.HomePath` is one switch, unit-tested, next to the role names.

**Why the claim beats the cookie.** The cookie belongs to a browser; the preference belongs to the person, on any device. Before sign-in the cookie is all there is, so it stays second.

**`RefreshSignInAsync` after every profile change.** The name and language live in the cookie as claims. Re-issuing it makes the header and the culture change on the very next page, rather than whenever the security stamp check next runs.

**Why the tenant name isn't a claim.** It would go stale when the owner renames the hostel, and every cookie would carry it. A five-minute cache gives one query per tenant per five minutes, at the cost of a short delay after a rename.

**A seed you can run twice.** The seeder checks for the owner first and does nothing if found. It runs through `UserManager` and the tenant context like real registration, so the demo data obeys the same rules as real data. It refuses to run outside Development, because its passwords are published in this repository.

**The token bug.** The form tag helper adds an anti-forgery token only when it builds the action itself. A literal `action="/language"` skips it. HTMX hid this, because `hx-headers` sends the token on HTMX requests, but a plain submit failed. A test now checks the form itself.

**Smoke as a script, not a test.** It hits a real Kestrel process, like a deploy check. `HttpClient` is used rather than `Invoke-WebRequest`, whose redirect handling differs between PowerShell 5.1 and 7.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Authorization/BaseraRoles.cs` (`HomePath`)
2. `src/Basera.Web/Views/Shared/_UserMenu.cshtml`
3. `src/Modules/Identity/Basera.Modules.Identity/Application/ProfileService.cs`
4. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/IdentityDevSeeder.cs`
5. `tests/Basera.IntegrationTests/Identity/ShellTests.cs`

## Questions you should be able to answer

1. A warden switches to English on the staff phone, then signs in on the office PC that has a Nepali culture cookie. Which language does the PC show, and which line of `BaseraCultures` decides it?
2. Why did the language switch work in every test until now, and what kind of user would have hit the 400?
