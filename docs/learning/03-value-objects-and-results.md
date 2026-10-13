# 03 — Value objects and results

## What was built

The small, framework-free types every module will lean on, all in `Basera.Shared`:

- **`Money`**: rupees held as whole paisa in a `long`. It does checked add, subtract and multiply; `Percent`; `RoundToRupee`; and `Allocate(n)`, which splits an amount without losing a paisa. It formats as `रु 12,500.00` with Nepali lakh grouping (`रु 1,25,000.00`), optionally in Devanagari digits.
- **`NepaliCalendar` / `NepaliDate`**: Bikram Sambat ↔ AD conversion for 2000–2099 BS from an embedded table (`calendar-bs.csv`), plus month lengths, `AddDays`, `AddMonths`, first/last day of month, month names in English and Nepali, and parsing.
- **`BillingPeriod`**: one BS month, the unit Basera bills in.
- **`PhoneNumber`**: a Nepal mobile in E.164 form, accepting the ways people actually type it.
- **`Result`, `Result<T>`, `DomainError`, `ErrorCode`**: how a service says "that didn't work, and here's why" without throwing.
- **`IClock` / `SystemClock`**: "now" and "today in Nepal", injectable for tests.

## Why it is shaped this way

**Value objects instead of primitives.** A `decimal amount` parameter tells you nothing: rupees or paisa? Already rounded? A `Money` can only be built in valid ways, and its methods carry the rules (round half away from zero; never lose a paisa when splitting). The same goes for `PhoneNumber`: once you hold one, it *is* valid, so no code downstream re-checks it. These are `record`s, so two values with the same contents are equal.

**Why `long` paisa (ADR-009).** Floating point cannot represent 0.10 exactly; `decimal` can, but invites values like 10.005 that are not real money. Whole paisa removes the question. `checked` arithmetic makes an overflow throw instead of silently wrapping into a negative balance.

**Why our own calendar, and why a data file.** BS month lengths follow no formula. They are published each year by a government committee. So conversion is a lookup table, and keeping it as a CSV makes yearly maintenance a data edit. The table was cross-checked against four independent libraries. 2084 onward is *provisional* because those years are projections; see the header of `calendar-bs.csv` and `PROGRESS.md`. A test converts every day in the 100-year range there and back.

**`default` must be valid.** `NepaliDate` and `BillingPeriod` are structs, and every struct has a `default` value you cannot forbid. Storing offsets (year − 2000, month − 1) makes `default` equal to 2000-01-01 instead of an impossible 0000-00-00.

**Results for expected failures, exceptions for bugs.** "The phone number is wrong" is normal. The user typed something, and the UI should show a message next to the field (`DomainError.Target = "phone"`). Using exceptions for that hides failure paths and is slow. A `Result` makes the failure part of the method's signature. A failed `Result.Value` read *does* throw, because that is a programming mistake. Each `ErrorCode` will map to one HTTP status and one envelope code in feature 6.

**Nepal time, not server time.** The server runs in UTC. A payment at 00:15 in Kathmandu is still "yesterday" in UTC. `IClock.Today` gives the Nepal date (fixed UTC+05:45, no daylight saving), and tests swap in a fake `TimeProvider`.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Domain/Money.cs`
2. `src/BuildingBlocks/Basera.Shared/Domain/Calendar/NepaliCalendar.cs` and `calendar-bs.csv`
3. `src/BuildingBlocks/Basera.Shared/Domain/Calendar/NepaliDate.cs`
4. `src/BuildingBlocks/Basera.Shared/Results/ResultOfT.cs` together with `Domain/PhoneNumber.cs` (a real use)
5. `tests/Basera.UnitTests/Shared/MoneyTests.cs`: the rules, as examples

## Check yourself

1. A hostel splits a रु 1,000 electricity bill across three residents. What does `Allocate(3)` return, and why would `1000 / 3` per resident be wrong for the ledger?
2. A resident's phone number is typed with a letter in it. Should `PhoneNumber.Create` throw or return a failed result, and what does the controller do with each?
