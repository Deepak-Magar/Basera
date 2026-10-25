# 13 — Who owns a contract: Identity's resident phone lookup

## What was built

A refactor, with no behaviour change:

- `IResidentPhoneLookup`, `PhoneHolder` and `PhoneHolderKind` moved from `Basera.Modules.Residents.Contracts` to `Basera.Modules.Identity.Contracts`.
- The stub `NoResidentPhoneLookup` (finds nobody) moved into Identity's Infrastructure. `AddBaseraIdentity` registers it with `TryAddScoped`.
- Identity's project reference to Residents is gone, and `ResidentsModule` is empty again.
- A new architecture test, `Identity_references_no_other_module`, fails if Identity's assembly references any `Basera.Modules.*` assembly.

## Why it is shaped this way

**Two readings of ARCHITECTURE §3.2.** "Cross-module reads go through the owning module's `Contracts` interface." Feature 4 read "owning" as *the module that owns the data*. Residents owns residents and guardians, so the question "who has this phone?" went into Residents.Contracts, and Identity referenced Residents.

That works for one call, but it fixes a direction for the whole graph. If Identity depends on Residents, Residents can never depend on Identity, because project references can't form a cycle. Yet Residents (like every module) will need Identity's contracts: the tenant's name, who the signed-in user is, and later "create a Parent membership when a guardian is added". Identity is the module everyone else stands on.

**Dependency inversion between modules.** The module that *asks* defines the interface in its own Contracts, shaped exactly to what it needs (`PhoneHolder` has the four fields OTP sign-in uses, nothing more). The module that *knows* implements it. In code: Identity says "I need something that can answer `FindAsync(phone)`", and Residents says "I can do that" and registers itself. Both depend only on `Identity.Contracts`, and the arrow points one way: modules → Identity.

The data still belongs to Residents. Only Residents will read its tables to answer. What moved is the *shape of the question*, which belongs to the caller. This is the exception, not the new default: §3.2's own example, Billing calling `Residents.Contracts.IResidentLookup`, stays as it is, because Residents never needs Billing. Inversion is for calls *from* Identity, which must stay at the root.

**The stub and registration order.** Until Residents exists, someone must answer. Identity registers its own stub with `TryAddScoped`. `ModuleLoader` calls `AddModule` in module-name order, so Identity (I) runs before Residents (R). When Residents arrives in Session 3, a `TryAdd` there would silently lose to the stub, and OTP sign-in would keep refusing everyone. Residents must use `services.Replace(...)` (or a plain `AddScoped`, since the last registration wins). This is written on the contract itself, in ARCHITECTURE §7 and in PROGRESS.md. Tests still swap in a fake through `ConfigureTestServices`, which runs after everything.

**Why a test for it.** The existing boundary test checks that modules use each other only through `Contracts`, and the old design passed it. The new test states the rule this refactor is really about: Identity references no other module, so every arrow points towards it. It inspects the compiled assembly's references, so it would have failed before the move.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Contracts/IResidentPhoneLookup.cs`
2. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/NoResidentPhoneLookup.cs`
3. `src/Modules/Identity/Basera.Modules.Identity/Infrastructure/BaseraIdentity.cs` (the `TryAddScoped` line)
4. `tests/Basera.UnitTests/Architecture/ModuleBoundaryTests.cs` (`Identity_references_no_other_module`)

## Questions you should be able to answer

1. In Session 3 someone writes `services.TryAddScoped<IResidentPhoneLookup, ResidentPhoneLookup>()` in `ResidentsModule`. Which implementation does OTP sign-in get, why, and which test would you expect to notice?
2. ARCHITECTURE §3.2's example puts `IResidentLookup` in Residents.Contracts for Billing to call. Why is that placement right there, when the same placement was wrong for `IResidentPhoneLookup`?
