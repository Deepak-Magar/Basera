# 07 — Data Protection keys in PostgreSQL

## What was built

ASP.NET Core's Data Protection now keeps its key ring in PostgreSQL rather than on the local disk:

- **`DataProtectionKeysDbContext`** (Basera.Shared) implements `IDataProtectionKeyContext`. It owns the schema `dataprotection` and the table `data_protection_key` (`id`, `friendly_name`, `xml`), and has its own migration `InitialDataProtection` and its own `__EFMigrationsHistory`.
- **`AddBaseraDataProtection()`** registers that context, calls `PersistKeysToDbContext<DataProtectionKeysDbContext>()` and sets the application name to `Basera`. The host calls it right after `AddBaseraWeb()`.
- **`scripts/migrate.ps1`** migrates it before the outbox. The integration test fixture picks it up automatically, because it migrates every registered DbContext.
- **Four integration tests** (`Security/DataProtectionTests.cs`):
  1. A value protected by one host is unprotected by a second host on the same database.
  2. Every key the key manager knows about is stored in the table.
  3. An anti-forgery cookie and token issued by host A are accepted by host B.
  4. The application name is `Basera`.

## Why it is shaped this way

**What Data Protection signs.** The auth cookie Identity will issue, the anti-forgery cookie and token on every form, and later the password-reset and invitation tokens are all encrypted and signed with keys from this ring. If the keys change, every one of those values stops working. Everyone is signed out, and every form that was open fails with a 400.

**Why the default isn't good enough.**
- On Windows the keys go to `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`.
- In a Linux container they go to a folder inside the container, which disappears on every redeploy, or the ring is ephemeral.
- With two instances behind Caddy, each one has its own ring: a cookie issued by one is rejected by the other.

One shared table fixes all three, with no extra infrastructure, because PostgreSQL is already the system of record (§6).

**Why the application name matters.** By default the "application discriminator" is the content root path. Two hosts deployed to different folders would then isolate their payloads from each other even while reading the same keys. A fixed `Basera` name makes the ring the only thing that matters. Changing it later signs everyone out, so it is a constant.

**Why a separate DbContext, not a module's.** The keys are global (no tenant), they are needed before any module is involved, and they follow the same pattern as `OutboxDbContext`: Shared owns the schema and its migration.

**Why the test checks the table and not just the round trip.** On a developer's Windows machine, two hosts would share the default folder under `%LOCALAPPDATA%` anyway, so a round trip alone would pass even without this feature. Test 2 shows the keys really come from the database.

**Not done: encrypting keys at rest.** With a custom repository there is no default XML encryptor, so keys are stored as plain XML and a startup warning says so. Anyone who can read this table can forge cookies. For production the plan is `ProtectKeysWithCertificate` (a certificate from an environment secret), and it is listed in PROGRESS.md.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/DataProtection/BaseraDataProtection.cs`
2. `src/BuildingBlocks/Basera.Shared/DataProtection/DataProtectionKeysDbContext.cs`
3. `src/BuildingBlocks/Basera.Shared/DataProtection/Migrations/*_InitialDataProtection.cs`
4. `tests/Basera.IntegrationTests/Security/DataProtectionTests.cs`

## Questions you should be able to answer

1. You deploy a new image and every warden is suddenly signed out and gets a 400 on submit. Name two configuration mistakes that would cause it, and how you would tell which one it is from the `dataprotection.data_protection_key` table.
2. Why would test 1 pass on your laptop even without `PersistKeysToDbContext`, and what makes test 2 different?
