# EclipsePOS 0.2 preview — Windows setup

This package contains **the original Workbench and System Manager**, rebuilt for .NET Framework 4.8, plus the separate ModernPOS demonstration app. It is an engineering preview, not a validated production POS release.

## What is included

- Original transaction engine, inventory, management and reporting projects; original POS for .NET device adapters remain in place.
- New Workbench shell, flat menu buttons, management tabs, light/dark themes and larger default touch targets. Some legacy forms and icon buttons still use their original layouts.
- Per-user settings with atomic saves, backup and cross-process coordination. Existing ProgramData settings are read on first use without modifying the original file.
- PBKDF2-SHA256 passwords for new/reset credentials. Old MD5 passwords remain readable until explicitly reset; they are not silently converted. Apply schema 2 before using this build to edit passwords. Password editors are blank when opening an existing account; blank means keep the existing password.
- Fractional quantities survive transaction reload. Checkout completion, totals and stock writes use one SQL connection/transaction. Database errors now propagate instead of reporting success. **Payment, printer and cash-drawer side effects cannot be rolled back with SQL.** Failure/recovery scenarios require integration testing.
- SQL Server 2025 provisioning, schema-version tracking, encrypted Windows-authenticated connections and backup verification. No PostgreSQL or SQLite conversion has been attempted.

## Windows prerequisites

Use Windows 11 x64 and .NET Framework 4.8 or 4.8.1. Applications are built as x86 to retain compatibility with common 32-bit OPOS service objects. Native ARM64 device compatibility is not established.

Windows 10 with .NET Framework 4.8 is a compatibility target, **not tested here**. Ordinary Windows 10 support ended in October 2025; use an appropriately supported ESU/LTSC installation if it must remain in service.

For the original applications:

1. Install [SQL Server 2025 Express](https://www.microsoft.com/en-us/download/details.aspx?id=108836), using Windows authentication. SQL installation requires an administrator. A shared store SQL server can also be used; each cashier's Windows account must have a database user with appropriate permissions. Do not run routine cashiers as SQL administrators.
2. For real peripherals, install [Microsoft POS for .NET 1.14.1](https://www.microsoft.com/en-us/download/details.aspx?id=55758) and the exact vendor OPOS/service-object driver for each device. Epson printer models need their matching Epson OPOS driver and logical device names. Configure scanner, printer, drawer and scale in the original management screens and vendor tools. Keyboard-wedge scanners can send keystrokes without OPOS; that does not verify an OPOS connection.
3. Run `scripts/Test-WindowsPrerequisites.ps1` for an inventory. A successful inventory is not a device certification.

Microsoft lists SQL Server 2025 Express for [Windows 10 and Windows 11](https://learn.microsoft.com/en-us/sql/sql-server/install/hardware-and-software-requirements-for-installing-sql-server-2025?view=sql-server-ver17). SQL Server Express 2025 raises the database limit to 50 GB. Keeping SQL Server preserves the existing stored procedures and typed datasets; a PostgreSQL migration would require replacing that data access layer.

## Install the preview

Extract `EclipsePOS-Windows-0.2-preview.zip`. Open Windows PowerShell in the extracted package directory and run:

```powershell
.\Install-EclipsePOS.ps1
```

The installer verifies the package hashes and copies application files to `%LOCALAPPDATA%\Programs\EclipsePOS-0.2-preview`. It adds three Start-menu shortcuts. It refuses to overwrite an existing installation directory; use a new `-Destination` for another preview. Run from a path you control. This is an **unsigned PowerShell installer**, not a signed MSI; organizational execution policy may require signing/allow-listing it. Do not disable organization policy.

`EclipsePOS Demo` runs without SQL Server and uses its own sample articles and local receipts. Its card checkout is simulated. It is not the original full transaction engine.

Workbench and Management require database configuration. `--preview` opens only their layout; `--smoke` renders it and exits. These modes do not load business modules, connect to SQL or exercise hardware. Without these arguments the original bootstrapper and business modules run.

## Create a new database containing original sample data

Close both original applications. From the installed directory, using Windows PowerShell 5.1 and an account allowed to create a database:

```powershell
.\scripts\Set-EclipseDatabase.ps1 -Mode CreateDemo `
  -Server '.\SQLEXPRESS' -Database EclipsePOSDemo `
  -TrustServerCertificate -ConfigureApps
```

`-TrustServerCertificate` is an explicit concession for a local development instance with a self-signed certificate. For a store server, configure a trusted SQL TLS certificate and omit this switch. Encryption remains enabled in either case.

The script refuses existing databases, imports the original sample configuration/articles, upgrades the password schema and prompts for a management password (12+ characters) and cashier PIN (6–9 digits, no initial zero). It replaces **all seeded credentials** with the chosen demo credentials. Management login: `admin`; sample retail cashier login: `100`. The original sample data uses organization `1`, store `2`, terminal `1`, configuration `1`. Tax, currency and receipt rules are original demo data, not a configured Dutch retail installation.

The import and migrations run in a transaction. A failure rolls back the schema/data import but may leave the newly created empty database for inspection. The script never automatically drops it. Failed setup must be corrected before launching the original applications. Credentials are prompted, never supplied as command-line arguments.

The legacy application's destructive database-creation wizard and SQL 2005 installer hooks are retired from the compiled setup path. Existing-database selection checks schema 2 and uses Windows authentication. Use the script for self-signed development certificates.

## Upgrade a restored copy of an existing database

First restore your legacy backup into a **separate SQL Server 2025 test instance/database**. Very old SQL backups can require an intermediate SQL Server upgrade; confirm Microsoft's supported upgrade path for your source version. Close all POS and management sessions before migrating.

```powershell
.\scripts\Set-EclipseDatabase.ps1 -Mode Upgrade `
  -Server 'STORE-SQL' -Database EclipsePOSTest `
  -BackupPath 'D:\SQLBackups\EclipsePOSTest-before-v2.bak'

.\scripts\Set-EclipseDatabase.ps1 -Mode Verify `
  -Server 'STORE-SQL' -Database EclipsePOSTest -ConfigureApps
```

The backup path is on the SQL Server machine and must be writable by its service account. Backups append rather than overwrite; verification targets the newly written backup set. `RESTORE VERIFYONLY` is useful but is not a complete restore drill. The upgrade widens employee/subscriber hashes and matching stored-procedure parameters, preserving installed procedure bodies, and records version 2. A transaction and application lock protect the schema update. No table-drop baseline or demo seed runs during Upgrade.

Reverting to an old application after password resets can fail because old clients do not support PBKDF2. Roll back using your verified database backup and the old application together; do not truncate password columns.

## Data and removal

Settings: `%LOCALAPPDATA%\EclipsePOS\Data\EclipsePOS.settings`; diagnostics: `%LOCALAPPDATA%\EclipsePOS\Logs`. Set `ECLIPSEPOS_DATA_DIR` to a dedicated writable directory before launch for an isolated test profile. Shared data directories need deliberate Windows ACLs. Do not put live settings or databases in Program Files.

To remove installed code, close the apps and run:

```powershell
.\scripts\Uninstall-EclipsePOS.ps1
```

Only unchanged, manifest-listed application files and matching shortcuts are removed. Databases, user settings, receipts and modified files are preserved. Empty directories can be removed manually. SQL Server, vendor drivers and POS for .NET are managed separately.

## Build from the repository

Use a current .NET SDK on Windows, the .NET Framework 4.8 runtime and the official POS for .NET SDK files. Builds were verified with SDK 8.0.424; prefer a supported current SDK for ongoing development (the applications still target .NET Framework 4.8). Full Visual Studio is no longer required. NuGet access is required for reference assemblies.

```powershell
.\scripts\Build-WindowsPackage.ps1 `
  -PosNetSdkDir 'C:\Program Files (x86)\Microsoft Point Of Service\SDK' `
  -OutputDirectory 'C:\Builds\EclipsePOS-preview'
```

The SDK directory must include Microsoft's original `Eula.rtf`, `Microsoft.PointOfService.dll` and `Microsoft.PointOfService.ControlBase.dll`. The build accepts an extracted SDK for compilation; this does **not** install POS runtime services or drivers. Microsoft objects retain their separate included license; original third-party Prism/Unity/WPF toolkit binaries retain their upstream terms.

Build regression checks and SQL syntax validation independently:

```powershell
dotnet build tests/Regression/Regression.csproj -c Release
.\tests\Regression\bin\Release\net48\Regression.exe
dotnet build tests/SqlSyntax/SqlSyntax.csproj -c Release
.\tests\SqlSyntax\bin\Release\net48\SqlSyntax.exe database/001-legacy-demo.sql database/002-modern-security.sql
```

## Validation boundary and remaining release work

Verified locally on Windows 11: original Workbench and Manager compilation, shell rendering from installed files, password/settings/fractional-quantity regression checks, SQL 2025 syntax parsing, installation using Windows PowerShell 5.1, and uninstall preserving unknown/modified files. The standalone demo's 10 checkout/catalog/receipt checks pass separately from original-engine tests.

**Not verified:** executing the baseline or migration on SQL Server 2025; original-engine end-to-end sales/returns/suspend/resume, concurrent tills, crash recovery, physical OPOS/Epson devices, Windows 10 installation, production printer/scale behavior, a signed MSI rollout. The development machine has no SQL instance, no device drivers/physical devices and no administrator token.

Before a pilot: restore real test data, run the migration twice, test rollback on a forced SQL failure, sell/return weighted items, reconcile totals and stock, test simultaneous tills, disconnect the database/printer mid-checkout, verify recovery without duplicate stock/payment, and perform a real restore. Inspect every operational screen at the till's actual DPI/resolution. The old Prism/Unity/WPF toolkit stack and several legacy forms remain; replacing them and moving hardware into a separate .NET Framework bridge is a later architectural migration. Payment-terminal certification, fiscal requirements, full decimal-money conversion and production security hardening are not established by this preview.
