# 01 — The solution and the host

## What was built

An empty but running Basera. There is one web project (`Basera.Web`, the *host*), one shared library (`Basera.Shared`) and eleven module projects (`Basera.Modules.Identity` … `Basera.Modules.Audit`). There are two test projects. When the site starts, it finds every module, asks each one to register its services, and shows the list on the landing page. Four user areas (`/owner`, `/staff`, `/resident`, `/parent`) and `/api` have placeholder pages. `/health` answers `Healthy`.

## Why it is shaped this way

**One process, many projects.** Basera is a *modular monolith* (ADR-002). It deploys as one app, but each business area is its own project, and modules may only talk through each other's `Contracts`. A separate project per module makes those boundaries visible to the compiler, and in feature 2 they are enforced by tests. With one big project and folders, nothing would stop `Billing` from reaching into `Residents`' tables.

**The host knows modules only as `IModule`.** `IModule` has two methods. `AddModule` runs while the app is being built: "put my services in the container". `MapModule` runs after routing is set up: "map my endpoints and jobs". `Program.cs` never names a module. It calls `AddModules()` and `MapModules()`. Adding a twelfth module means adding a project reference, and nothing else in the host changes.

**Why scan files instead of loaded assemblies?** .NET loads a referenced DLL only when code first touches one of its types. The host never touches `BillingModule` directly, so `Basera.Modules.Billing.dll` would not be loaded yet. `ModuleLoader` therefore looks for `Basera.Modules.*.dll` next to the executable and loads each one. The host still references every module project. That reference is what copies the DLLs into the output folder.

**Central build settings.** `Directory.Build.props` is imported automatically by every project below it. That is where nullable, implicit usings, warnings-as-errors and `net8.0` are set once. `Directory.Packages.props` turns on *central package management*: a csproj says `<PackageReference Include="xunit" />` with no version, and the version comes from this one file. Upgrading a package becomes a one-line change.

**Areas and layouts.** Each area gets its own `_Layout`, because Owner is desktop-first and Staff, Resident and Parent are phone-first with a bottom action bar. The shared `<head>` and scripts live once in `Views/Shared/_Head.cshtml` and `_Scripts.cshtml`. When Razor can't find a view or partial in the area, it falls back to `Views/Shared`. Bootstrap and HTMX are copied into `wwwroot/lib`, so pages do not depend on a CDN, which matters on slow Nepali mobile networks.

**Tests talk to the real host.** The integration test uses `WebApplicationFactory<Program>`, which starts the whole app in memory and gives you an `HttpClient`. That is why `Program.cs` ends with `public partial class Program`: the test project needs a type to point at.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Modules/IModule.cs`: the contract every module implements.
2. `src/BuildingBlocks/Basera.Shared/Modules/ModuleLoader.cs`: discovery, `AddModules`, `MapModules`.
3. `src/Basera.Web/Program.cs`: the startup pipeline and where modules plug in.
4. `src/Modules/Billing/Basera.Modules.Billing/BillingModule.cs`: an (empty) module and its folder layout.
5. `tests/Basera.IntegrationTests/Host/HostTests.cs`: how the whole app is tested end to end.

## Check yourself

1. If you deleted the `ProjectReference` to `Basera.Modules.Mess` from `Basera.Web.csproj`, what would happen at build time and at run time, and why?
2. Why is `MapModule` a separate step from `AddModule` instead of doing everything in one method?
