# 09 — Owner registration and password login

## What was built

- **Pages** in the host's `Account` area (phone-first card, English and Nepali):
  - `/account/register`: hostel name, owner name, phone, email, password.
  - `/account/login`: email or phone, plus password.
  - `/account/logout`, `/account/forgot` → `/account/reset`, and `/account/denied`.
- **`AccountService`** (Identity Application), with one method per use case:
  - `RegisterOwnerAsync`
  - `PasswordSignInAsync`, with lockout after 5 failures for 15 minutes
  - `SignOutAsync`
  - `SendPasswordResetCodeAsync`
  - `ResetPasswordAsync`
- **`OtpService` + `OtpChallenge`.** SMS codes: 6 digits, 5 minutes, 5 tries, 3 sends per phone in 10 minutes. Only an HMAC of each code is stored. Feature 4's OTP login reuses them.
- **`BaseraClaimsPrincipalFactory`**: puts `sub`, `name`, `tenant_id`, `role`, `lang` and the security stamp in the cookie.
- **Shared:**
  - `FluentValidationFilter`, which fills `ModelState` (ADR-008).
  - `ModelState.AddDomainError`, which puts a service error under its field.
  - The `auth` rate-limit partition, which now applies only to `/account/*` POSTs.
- **Tests:** 18 integration tests driving the real pages with cookies and anti-forgery, and 5 unit tests for the OTP rules.

## Why it is shaped this way

**One `SaveChanges`, one transaction.** Registration must not leave a hostel with no owner. Normally `UserManager.CreateAsync` saves at once. Turning off the store's `AutoSaveChanges` for that call keeps the user in the change tracker, so tenant, user and membership commit together. A weak password fails before anything is written (test `A_weak_password_is_refused_and_no_tenant_is_left_behind`). There is no explicit transaction, which keeps the outbox rules of Feature 5 intact.

**The login path in three steps:**
1. `SignInLookup` turns "98…" or an email into a user id and tenant id, with the one unfiltered query.
2. `TenantContext.Set(tenantId)`.
3. Everything else is normal Identity. `CheckPasswordSignInAsync` counts failures and locks out, then the claims factory reads the membership tenant-filtered.

The membership check comes *after* the password check, so the page can't be used to probe who works where.

**Same answer for unknown and wrong.** "The email or mobile number and password don't match" either way. The forgot page always moves on to the reset page and sends an SMS only to real accounts. That saves money and avoids revealing who has an account.

**Why HMAC, not a hash.** Six digits are a million values: anyone with a database copy could reverse a plain SHA-256 in milliseconds. HMAC under a key kept outside the database can't be reversed that way. Phone and purpose are part of the input, so a reset code can never sign anyone in.

**Validation order in reset.** The new password is checked before the code is spent, so a typo in the password doesn't burn the SMS.

**A reset proves the phone.** It sets `PhoneVerifiedAt`, clears the lockout, and changes the security stamp, so other sessions are signed out when the cookie is next validated.

**Security stamp and tenants.** The test with `ValidationInterval = 0` proves the cookie event from Feature 2 works: every request reloads the user, before tenancy has run.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Application/AccountService.cs`
2. `src/Modules/Identity/Basera.Modules.Identity/Application/OtpService.cs` and `Domain/OtpChallenge.cs`
3. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/BaseraClaimsPrincipalFactory.cs`
4. `src/Basera.Web/Areas/Account/Controllers/AccountController.cs`
5. `tests/Basera.IntegrationTests/Identity/AccountTests.cs`

## Questions you should be able to answer

1. Two people register with the same phone at the same moment. Both pass the `SignInLookup` check. What happens to the second request, and which line turns it into a friendly message?
2. Why does a correct code typed after five wrong ones still fail, and why does `Check` not count attempts on an expired code?
