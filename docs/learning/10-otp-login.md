# 10 — OTP login for residents and parents

## What was built

- **Pages** `/account/otp` (phone) and `/account/otp/verify` (code), Nepali by default. A link from the password login page leads to them.
- **`OtpSignInService`** (Identity Application):
  - `SendCodeAsync` decides whether this phone may get a code at all.
  - `SignInAsync` checks the code, then signs in an existing user, or creates the user and a Resident/Parent membership on the first success.
- **`IResidentPhoneLookup`** in `Basera.Modules.Identity.Contracts` (moved there from Residents afterwards, see doc 13), returning a `PhoneHolder(TenantId, PersonId, Kind, DisplayName)`. A stub finds nobody, so for now every new number is refused with a clear message.
- **`OtpService` returns outcomes** (`OtpSendResult`, `OtpCheck`) instead of English errors. Each page maps them to its own localized text.
- **13 integration tests** using a hand-moved `TestClock` and a fake lookup. They cover each limit: 6 digits, keyed hash, 5 minutes, 5 attempts, 3 sends per 10 minutes, newest code only, codes of one purpose never working for the other, Devanagari digits, unknown and staff numbers.

## Why it is shaped this way

**Who may receive a code.** Every SMS costs money, and an OTP page is a favourite target for "SMS pumping", where attackers trigger floods of paid messages. So the decision comes before sending:

| Phone | Answer |
| --- | --- |
| has a user with Resident/Parent role | send |
| has a user with a staff role | "sign in with your password", no SMS |
| has a user with no membership | "no access", no SMS |
| no user, Residents knows it | send |
| no user, Residents doesn't know it | "ask your hostel office", no SMS |

The price is that the page reveals whether a number is known to some hostel. That is accepted: residents need a clear message more than they need secrecy about being registered. The `auth` rate limit (10 POSTs a minute per IP) and the 3-codes-per-phone rule cap the probing.

**Where the contract lives.** This feature first put it in Residents.Contracts, on the reading that the owner of residents owns the question. Doc 13 moved it into Identity.Contracts, so Identity depends on no other module.

**The user is created at verify, not at send.** A code request proves nothing; a correct code proves the person holds the phone. Only then are the user (no password, phone confirmed) and the membership written, in one `SaveChanges`.

**Persistent cookie.** Residents open the app a few times a month. A session cookie would cost an SMS per visit; this one lasts 14 days, sliding.

**Testing time without waiting.** `IClock` is injected everywhere a code's age matters. The tests swap in a `TestClock` and call `Advance(TimeSpan.FromMinutes(5))`, so "expires after 5 minutes" is checked in milliseconds, exactly at the boundary (4:59 works, 5:00 does not).

**Why a correct sixth try still fails.** After five wrong codes the challenge is spent. Otherwise guessing would never end.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Contracts/IResidentPhoneLookup.cs`
2. `src/Modules/Identity/Basera.Modules.Identity/Application/OtpSignInService.cs`
3. `src/Modules/Identity/Basera.Modules.Identity/Application/OtpService.cs`
4. `src/Basera.Web/Areas/Account/Controllers/AccountController.cs` (the `Otp` and `OtpVerify` actions)
5. `tests/Basera.IntegrationTests/Identity/OtpLoginTests.cs`

## Questions you should be able to answer

1. Why does the 3-per-10-minutes limit count password-reset codes and login codes together, while a reset code can never sign anyone in?
2. When the real Residents module replaces the stub, its lookup runs with no tenant. What must it do differently from a normal Residents query, and what keeps that safe?
