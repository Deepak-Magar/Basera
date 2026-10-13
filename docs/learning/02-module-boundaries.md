# 02 — Module boundaries as tests

## What was built

Architecture tests in `Basera.UnitTests` that read the compiled module assemblies and fail the build's test run when the structure breaks two rules from ARCHITECTURE §3 and §11:

1. **A module may use another module only through its `Contracts` namespace.** `Billing` may call `Basera.Modules.Residents.Contracts.IResidentLookup`. It may not touch `Basera.Modules.Residents.Domain.Resident` or `ResidentsDbContext`.
2. **Domain code is framework-free.** Nothing in a `...Domain` namespace (in any module or in `Basera.Shared`) may depend on `Microsoft.EntityFrameworkCore` or `Microsoft.AspNetCore`.

There are also *self-tests* that run each rule against small deliberately-broken fixture classes to prove the rule catches violations.

## Why it is shaped this way

**Compilers enforce project references, not namespaces.** Once Billing has a project reference to Residents (which it will need, to see the Contracts), C# lets Billing use every *public* type in Residents. Code review alone will not hold that line for long. A test that inspects the IL and lists offending types turns "please don't" into a red test. This is what ADR-002 means by "requires an architecture test to keep boundaries honest".

**How the boundary rule works.** For module M, the test collects the full names of every type in every *other* module, except those under `<OtherModule>.Contracts`. It then asks NetArchTest: "does any type in M depend on any of these?" NetArchTest reads the compiled IL with Mono.Cecil, so it sees field types, method parameters, local variables and base classes. It catches more than a text search would.

**Why the Domain rule matters.** The domain (entities, value objects, rules like pro-rating) is the part of Basera most worth unit testing and least likely to change. If an entity used `DbContext` or `HttpContext`, you could not test it without a database or a web request, and switching persistence or UI would ripple into business rules. EF mapping therefore lives in `Infrastructure/`, HTTP in `Presentation/`, and the domain is plain C#.

**Why the self-tests.** Today all eleven modules are empty, so the real tests would pass even if the rules were written wrongly. The fixtures (`Fixtures/RuleFixtures.cs`) include a good consumer that uses a `Contracts` type, a bad consumer that uses a `Domain` type, and a "leaky" entity holding an `HttpContext`. The self-tests show the rules accept the first and reject the other two.

**Why load modules via `ModuleLoader`.** The tests find module assemblies exactly the way the host does (`Basera.Modules.*.dll` next to the test binary). A new module is checked automatically. A separate test asserts there are eleven, so a module missing from the test project's references cannot slip past unchecked.

## Read in this order

1. `tests/Basera.UnitTests/Architecture/ArchitectureRules.cs`: the two rules, written once.
2. `tests/Basera.UnitTests/Architecture/ModuleBoundaryTests.cs`: the rules applied to every real module.
3. `tests/Basera.UnitTests/Architecture/Fixtures/RuleFixtures.cs`: good and bad example code.
4. `tests/Basera.UnitTests/Architecture/RuleSelfTests.cs`: proof each rule can fail.
5. `docs/ARCHITECTURE.md` §3: the module rules these tests protect.

## Check yourself

1. Billing needs the name of a resident to print on an invoice. Which namespace should the type it calls live in, which module registers its implementation in DI, and what would the boundary test say if Billing read the `Resident` entity directly?
2. Why does a rule that has only ever passed give you less confidence than one you have seen fail, and how do the self-tests address that?
