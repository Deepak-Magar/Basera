# 14 — The Properties aggregate: rules in one place, persisted with xmin and soft delete

## What was built

- `Basera.Modules.Properties.Domain`: `Property` (aggregate root), `Floor`, `Room`, `Bed`, `BedOccupancy`, `RatePlan`, plus `RoomType`/`RoomAttributes`/`RoomTypes` and `BedStatus`.
- `PropertiesDbContext` (schema `properties`) on `TenantDbContext`, migration `InitialProperties`, the `WithAggregate()` loading query, and a line in `scripts/migrate.ps1`.
- 54 unit tests for the rules and 8 integration tests for persistence.

## Why it is shaped this way

**One aggregate root.** Every rule in the brief involves more than one object. "A double room has two beds" spans Room and Bed. "Bed codes are unique" spans all rooms. "A bed's rate is its override, else the plan in effect" spans Bed, Room and RatePlan. So `Property` is the only public way to change anything: `AddRoom`, `UpdateRoom`, `Reserve`, `Occupy`, `Vacate`, `MonthlyRate`. Children have `internal` mutators only the root calls. A hostel has at most a few hundred beds, so loading the whole aggregate is cheap; `WithAggregate()` loads only each bed's *open* occupancy.

**Bed count follows the type.** Creating a room creates its beds (slots 1..n). Changing the type adds beds at the end, or removes the last ones if they are free. A dorm takes an explicit count (5–20). The room can never disagree with its type.

**Codes come from numbers.** `B{floor}-{room}-{slot}`, with room numbers unique per property (upper-case letters and digits, so no stray `-`). Renumbering a floor, renaming a room or moving it recodes its beds. `EnsureUniqueBedCodes` states the rule, and the database backs it with a unique index filtered on `deleted_at IS NULL`, so a removed room's number can be reused.

**Rooms point to floors by number, not by id.** Rooms are soft-deleted and floors are not. A soft-deleted room keeping a foreign key to a floor would stop that floor from ever being removed. By number, the history keeps "it was on floor 2" without a constraint.

**Reservation expiry is computed, not scheduled.** A reservation stores `ReservedUntil` (exclusive). `StatusOn(today)` reports an expired one as Free, and every rule uses it. So nothing depends on a job having run. `ReleaseExpiredReservations` only tidies the stored status.

**One active stay per bed, twice.** `Bed.Occupy` refuses when an occupancy is open. The partial unique index `ix_bed_occupancy_one_open_per_bed` refuses it in the database too, for two requests that race past the check.

**`xmin` and soft delete.** Property, Room and Bed map a `Version` property to PostgreSQL's `xmin` system column, so a stale edit fails with `DbUpdateConcurrencyException` instead of overwriting. Removing a room from the aggregate makes EF cascade the delete to its beds, and `AuditInterceptor` turns each delete into `deleted_at`. Floors, rate plans and occupancy history use `ClientNoAction`, so a soft-deleted parent never cascades a real delete into them.

**`ValueGeneratedNever` on ids.** `Entity` assigns GuidV7 ids in its constructor. A room added to a *loaded* property is found by `DetectChanges` with its key already set. With the default "generated on add" key, EF assumes such an entity already exists, marks it Modified and never inserts it. The integration test `A_removed_room_number_can_be_used_again` caught this.

## Read in this order

1. `src/Modules/Properties/Basera.Modules.Properties/Domain/Property.cs`
2. `src/Modules/Properties/Basera.Modules.Properties/Domain/Bed.cs` (`StatusOn`, `Occupy`)
3. `src/Modules/Properties/Basera.Modules.Properties/Domain/Room.cs` (`ResizeTo`, `Recode`)
4. `src/Modules/Properties/Basera.Modules.Properties/Infrastructure/PropertiesDbContext.cs`
5. `tests/Basera.IntegrationTests/Properties/PropertiesPersistenceTests.cs`

## Questions you should be able to answer

1. A bed was reserved on 1 Kartik with the default seven days, and no job has run since. On 8 Kartik a warden tries to put a different resident in it. What happens, and which line decides?
2. Why would removing a room with `DeleteBehavior.Cascade` on the bed → occupancy relationship lose data here, even though rooms and beds are only soft-deleted?
