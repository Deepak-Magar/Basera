# 16 — Managers scoped to their properties

## What was built

- **Properties:** `ManagerPropertyAssignment` (membership → property, table `manager_property_assignment`, migration `AddManagerPropertyAssignment`). `PropertyAccess` answers "which properties may this user see". `PropertyAccessRequirement` (`View`, `Manage`) with `PropertyAccessHandler`, checked by every Properties action through `DenyUnlessAsync`. `StaffPropertyAssignments` implements Identity's picker contract. The first screens: `/owner/properties`, `/owner/properties/{id}`, `/owner/beds/{id}`.
- **Identity:** two contracts, `ITenantMembers` (implemented by Identity) and `IStaffPropertyAssignments` (implemented by Properties, with a stub in Identity). Plus `/owner/staff/{id}/properties` with checkboxes, and cleanup when a manager is removed or changes role.
- 8 integration tests in `ManagerScopingTests`.

## Why it is shaped this way

**Properties owns the assignment.** The brief offered two homes: a `PropertyId` on Identity's membership, or a table in Properties. A property id inside `identity.tenant_membership` would be a column pointing at another module's table, which Identity can't check and Properties can't join (§3.2). It would also allow only one property per manager, while PRODUCT_SPEC's Pro plan has several branches. So Properties owns `manager_property_assignment` and keys it by the membership id it gets from Identity.

**Two contracts, two directions.**
- *Properties asks Identity* "who is this user in this tenant?" through `ITenantMembers`. That is the normal direction (modules → Identity), so Identity owns and implements it.
- *Identity asks Properties* "which properties can this manager run?" for its staff page. Identity must not reference Properties (learning doc 13). So, exactly like `IResidentPhoneLookup`, Identity defines `IStaffPropertyAssignments`, registers a stub with `TryAdd`, and Properties replaces it with `services.Replace`.

Identity's tables stay Identity's, and the arrow still points one way.

**One requirement, two strengths.** `View` lets Wardens and Cooks see every property (the staff floor map in Feature 5). `Manage` is for Owner and assigned Managers only. A controller first loads a `PropertyRef` (id + tenant) through the tenant-filtered context. Missing means 404: another tenant's property is indistinguishable from a made-up id. Then it authorizes; refused means 403, which the cookie handler turns into `/account/denied`. Bed pages authorize the bed's *property*, so no handler is needed per entity type.

**The handler is a singleton.** It needs the DbContext (through `PropertyAccess`), and a scoped handler would have been simplest. But a scoped `IAuthorizationHandler` makes `IAuthorizationService` impossible to resolve outside a scope, and an existing test resolving it from the root provider failed at once. So the handler takes `IHttpContextAccessor` and gets `PropertyAccess` from the request. With no request (a job) there is no user, and it refuses.

**Lists filter, actions authorize.** `PropertyService.ListAsync(onlyIds)` gets `VisiblePropertyIdsAsync(User)`: null for the owner (everything), the assigned ids for a manager. A manager with no assignment sees an empty list, never everything.

**Assignments follow the role.** Changing a manager to Warden, or removing them, calls `SetAsync(membership, [])`. A new invitation creates a new membership id, so an old assignment can never come back by accident.

## Read in this order

1. `src/Modules/Identity/Basera.Modules.Identity/Contracts/IStaffPropertyAssignments.cs`
2. `src/Modules/Properties/Basera.Modules.Properties/Infrastructure/PropertyAccessAuthorization.cs`
3. `src/Modules/Properties/Basera.Modules.Properties/Presentation/Controllers/PropertiesController.cs` (and `PropertyAuthorization.cs`)
4. `src/Modules/Properties/Basera.Modules.Properties/Infrastructure/BaseraProperties.cs` (the `Replace` line)
5. `tests/Basera.IntegrationTests/Properties/ManagerScopingTests.cs`

## Questions you should be able to answer

1. A manager assigned to North Block opens `/owner/beds/{id}` for a South Block bed, and then for a bed of another hostel. Which status does each get, and which line produces it?
2. Why does Properties register `StaffPropertyAssignments` with `Replace`, while `ITenantMembers` is registered by Identity with a plain `AddScoped`?
