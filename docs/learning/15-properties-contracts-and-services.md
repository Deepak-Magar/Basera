# 15 — Properties contracts and application services

## What was built

- **Contracts** (what other modules may call): `IBedAvailability` (free beds by property and room type, reserve, release, occupy, vacate), `IPropertyDirectory` (the tenant's properties, one by id), `IRateResolver` (a bed's monthly rate on a day), with the DTOs `FreeBed` and `PropertySummary`. `RoomType` moved into Contracts.
- **Application services** (what the screens will call): `PropertyService`, `RoomService`, `BedService`, `RatePlanService`, all returning `Result`/`Result<T>`, on top of `PropertyStore`.
- **Contract implementations** in Infrastructure (`BedAvailability`, `RateResolver`, `PropertyDirectory`), internal, registered in `AddBaseraProperties`.
- 10 integration tests through the services and contracts, including a race and tenant isolation.

## Why it is shaped this way

**Contracts delegate to the services; they don't repeat them.** `BedAvailability.OccupyAsync` is one line that calls `BedService.OccupyAsync`. Check-in from Residents and "mark occupied" from the floor map go through the same aggregate method, so §6 rule 6 can't be enforced in one path and forgotten in the other. The contract classes are `internal`, so nothing outside the module can depend on how they are built.

**`RoomType` lives in Contracts.** A contract can't expose Domain types: the architecture test forbids other modules from touching `Properties.Domain`. Yet "free double rooms" and "prefers a dorm" need the word. The enum is the module's public vocabulary, so it moved to `Properties.Contracts`, and the Domain uses it from there. How many beds each type has (`RoomTypes.BedCount`) is a rule and stays in Domain.

**`PropertyStore`: load, change, save, map failures.** Every write is "load the aggregate, call one method, save". Two things can go wrong only at save time:
- the `xmin` check fails (`DbUpdateConcurrencyException`), meaning someone changed the row since we read it;
- a unique index refuses the insert (`23505`), for example two check-ins racing for one bed, which the partial index `ix_bed_occupancy_one_open_per_bed` catches.

Both are expected and caused by users, so per ARCHITECTURE §5 they are a `Conflict` result ("Someone else changed this at the same time…"), not an exception. The race test fires two `OccupyAsync` calls at once and asserts exactly one wins.

**Services take ids, not entities.** Identity's `StaffController` loads the membership, authorizes it, then passes the entity to the service. Here the resource for authorization will be *the property* (Feature 3), and almost every action names a property or a bed. So a service method takes `propertyId` (or `bedId`, whose property `PropertyIdOfBedAsync` finds). The controller authorizes that id first, and the service loads the aggregate itself.

**Tenant isolation needs nothing new.** Every query runs through `TenantDbContext`'s filter. Another tenant's bed id finds no property, and the call returns `NotFound`, the same as an id that never existed. The isolation test tries every contract and service with a second tenant and checks the first tenant's data is unchanged.

**History is a query, not part of the aggregate.** `BedService.GetAsync` reads `bed_occupancy` rows for one bed, newest first, for the floor-map panel (Feature 5). The aggregate only loads open occupancies, which is all its rules need.

**Floors cascade.** EF refused to remove a floor under `ClientNoAction`, so floors now cascade (a real delete of a number and a label).

## Read in this order

1. `src/Modules/Properties/Basera.Modules.Properties/Contracts/IBedAvailability.cs`
2. `src/Modules/Properties/Basera.Modules.Properties/Application/PropertyStore.cs`
3. `src/Modules/Properties/Basera.Modules.Properties/Application/BedService.cs`
4. `src/Modules/Properties/Basera.Modules.Properties/Infrastructure/PropertyContracts.cs`
5. `tests/Basera.IntegrationTests/Properties/PropertyServicesTests.cs` (`Two_check_ins_racing_for_one_bed_give_one_conflict`)

## Questions you should be able to answer

1. In the race test, which of the two safeguards (the `xmin` check on the bed row or the partial unique index) normally stops the loser, and when would the other one be the only thing that does?
2. Why does `IBedAvailability.ReserveAsync` with another tenant's bed id return `NotFound` rather than `Forbidden`, and where in the code is that decided?
