# Windows 10/11: controle en modernisering van EclipsePOS

Gecontroleerd op 29 oktober 2026. Dit document gaat over de oorspronkelijke Workbench en SystemManager, naast de afzonderlijke ModernPOS-demo.

## Conclusie

De technologie is bruikbaar voor een Windows-kassa, maar de volledige app is op basis van deze repository **nog niet aantoonbaar probleemloos installeerbaar** op een schone Windows 10- of Windows 11-machine. Er is geen door ons gevalideerd volledig installatiepakket. De moderne demo is wel gebouwd en gestart; dat bewijst de installatiegeschiktheid van de oorspronkelijke app niet.

WPF kan behouden blijven. De eerste stap is het herstellen van de complete build en een gecontroleerde installatie met SQL en POS-drivers. Daarna kan de nieuwe interface aan de echte verkoopengine worden gekoppeld.

## Al aangepast in deze branch

- De negen oorspronkelijke hoofdprojecten en hun runtimeconfiguraties zijn van .NET Framework 4.7 naar **4.8** omgezet. Historische CAL-projecten zijn niet gewijzigd. Reference-assemblies komen vastgepind via NuGet, zodat de twee datalagen zonder een lokaal 4.7 Developer Pack kunnen bouwen.
- Vier kapotte SystemManager.Data-verwijzingen naar een niet-bestaande `Library`-map wijzen nu naar de aanwezige `LIB`-map.
- Ongeldige `System.Windows.Presentation`-references zijn verwijderd.
- Ontbrekende voorbeeld-MDF/LDF-bestanden worden alleen gekopieerd als ze werkelijk aanwezig zijn. Er is hiermee geen database gemaakt; configuratie en schema blijven nodig.
- Het POS-SDK-pad is configureerbaar via `PosNetSdkDir`. Een ontbrekend SDK geeft nu één duidelijke buildfout in plaats van een cascade van ontbrekende types.
- `scripts/Test-WindowsPrerequisites.ps1` geeft een alleen-lezen inventaris van Windows, .NET Framework, SDK, SQL-service en nog handmatig te controleren voorwaarden. De uitkomst is nadrukkelijk geen certificering van een werkende installatie.

Deze wijzigingen zijn buildvoorbereiding. De volledige executables zijn nog niet geaccepteerd of functioneel getest op 4.8.

## Bevindingen en voorgestelde aanpassingen

### 1. Volledige build herstellen — eerste prioriteit

De twee datalagen bouwen na de reparaties. De Workbench is lokaal geblokkeerd op het ontbrekende Microsoft POS for .NET SDK. De SystemManager loopt bij `dotnet build` verderop vast op ontbrekende gegenereerde XAML-code (`InitializeComponent` en controls) en meldt een ontbrekende CommonServiceLocator-reference naar `bin/Debug/net47`.

De oude projectvorm en WPF-buildtargets moeten worden hersteld met passende Visual Studio/MSBuild-desktoptools, of omgezet naar SDK-style WPF-projecten. Alleen .NET retargeten herstelt dit niet. Vervolgens moeten de lokaal meegeleverde Prism-/Unity-binaries en CommonServiceLocator als één samenhangende set worden gecontroleerd en vastgelegd.

Bronnen: `V.1.0.8/.../EclipsePOS.WPF.SystemManager.Infrastructure.csproj`, de overige csproj-bestanden en de lokale buildlogs. Binaire metadata in SystemManager/LIB: Composite 2.0.1.0, Composite.Wpf 1.0.0.0, Unity 1.2.0.0, WPFToolkit 3.5.40128.1, Extended 1.1.0.0. Dit zijn legacy-versies; hun aanwezigheid bewijst geen onderhoud of veiligheid.

### 2. Nieuwe Windows-installatie bouwen

De oude Workbench-bootstrapper noemt SQL Server **2005 Express SP2**, .NET 2.0/3.5 en Windows Installer 3.1. De Desktop Bridge-projecten verwijzen naar tijdelijke PFX-certificaten en Windows-build 14393. Er is geen actueel, ondertekend en door ons getest installatiepakket.

Aanpak: een installer met versienummer, ondertekening, duidelijke fout-/herstartcodes, vereistencontrole, repair/uninstall en een gecontroleerd upgradepad. SQL en vendor-drivers moeten expliciet worden geïnstalleerd of gecontroleerd. Voor deze legacy hardware is een conventionele Windows-installer een praktische eerste route; MSIX vereist aanvullend onderzoek naar serviceobjecten en machinebrede componenten. Dit is een ontwerpkeuze, geen bewijs dat MSIX onmogelijk is.

Bewijs: `EclipsePOS.WPF.PosWorkBench.csproj` (BootstrapperPackage), beide `.wapproj`-bestanden en `PosSetup/SQLSetup/EmbeddedInstall.cs` (oude ADDLOCAL/SECURITYMODE/SAPWD-commandoregel). Er is geen actieve aanroep naar `EmbeddedInstall` gevonden buiten de klasse zelf; behandel deze niet als bewezen werkende huidige installatieflow.

### 3. Database-installatie en upgrades scheiden

SQL Server 2025 Express is na aanvullend onderzoek de voorkeurskandidaat voor een nieuwe installatie; SQL Server 2022 Express blijft een alternatief als de doelomgeving dat vereist. De EclipsePOS-schema's moeten op de gekozen versie worden getest. De app gebruikt klassieke SQL-clientcode, typed datasets en stored procedures. Dat kan werken, maar is hier niet bewezen met een SQL-instance.

De meegeleverde aanmaakscripts bevatten `CREATE DATABASE` en `DROP TABLE`. Gebruik ze uitsluitend na beoordeling voor een nieuwe, lege testdatabase. Voor upgrades zijn versiegebonden migraties, voorafgaande backup en hersteltests nodig. Bestaande klantdata mag niet door een herinstallatie worden vervangen.

Bewijs: `SystemManager.PosSetup/Resources/CreatePosSite_v106.sql`, `SystemManager.Data/app.config` en `EclipsePos.Data/SqlRelator.cs`. De oude optionele MDF-/User Instance-route moet worden vervangen door een expliciet geconfigureerde databaseverbinding. Windows-authenticatie en minimale databasebevoegdheden hebben de voorkeur; SQL-authenticatie vraagt aparte bescherming van de opgeslagen inloggegevens.

### 4. POS-hardware: behouden, maar per model kwalificeren

Microsoft noemt Windows 10/11 als ondersteunde platforms voor POS for .NET 1.14. Dat maakt de bestaande API bruikbaar, maar bewijst niets over een specifieke oude Epson-/scannerdriver. De oorspronkelijke app gebruikt sterke koppelingen aan het SDK; de moderne demo heeft een optionele runtime-adapter.

Kies één architectuur voor de kassa en serviceobjecten. Begin bij x86 als de beschikbare vendor-drivers dat vereisen, en test ook op 64-bit Windows. Een x64-Windows-installatie betekent niet dat de POS-app zelf x64 moet zijn. Controleer claim/release, USB-loskoppelen, herstarten, papier op, lade open/dicht en meerdere scans. Houd PIN-terminal-integratie apart: die is niet gerealiseerd door alleen een betaalwijze 'PIN' te tonen.

Bewijs: `EclipsePos.Apps/Hardware`, `EclipsePos.Apps/Context/PosContext.cs` en `ModernPOS/Hardware.cs`. Geen fysieke hardwaretest uitgevoerd.

### 5. Gebruikers, wachtwoorden en instellingen moderniseren

De Workbench-login en ontgrendeling gebruiken ongesalte MD5-hashes; de SystemManager-login doet dat ook. Voor productie: een geschikte password-hash met unieke salt en work factor, een versieveld en migratie bij succesvolle login of een verplichte reset. Vervang oude hashes niet blind, anders werken bestaande accounts niet meer. Demo-accounts mogen niet als productiestandaard blijven bestaan.

Instellingen gebruiken `%ProgramData%\EclipseSoftware`. De providers gebruiken `FileInfo` op een bedoeld mappad en keren de bovenliggende map terug. Meerdere providers schrijven daardoor naar dezelfde locatie; dat kan bestaand gedeeld gedrag zijn en vraagt een migratieplan. Schrijffouten worden bovendien genegeerd. De installer moet de juiste beperkte schrijfrechten voor kassamedewerkers regelen; de app moet fouten zichtbaar melden. Niet oplossen door de gehele kassa steeds als administrator te starten.

`InstallHelper` opent direct `C:\temp\mylog.txt` zonder die map aan te maken en slikt fouten in de installatie af. Die helper moet worden vervangen of hersteld vóór hergebruik in een installer.

Bewijs: `EclipsePos.Apps/Util/MD5HashHelper.cs`, `PosEvents/LogOn.cs`, `PosEvents/UnLock.cs`, `SystemManager.Infrastructure/Views/Login/LoginView.xaml.cs`, de vier `EclipseSettingsProvider.cs`-bestanden en `PosSetup/Setup/InstallHelper.cs`.

### 6. Verkoopbetrouwbaarheid testen vóór UI-integratie

De oorspronkelijke geldvelden gebruiken op verschillende plaatsen `double`; conversies en afrondingsregels verdienen regressietests. `TransItem` leest quantity bijvoorbeeld via een cast naar int, hoewel de property double is. Dit kan breukhoeveelheden beïnvloeden en moet met echte gewichtsverkopen worden gecontroleerd. Geen bedragen-/schemamigratie uitvoeren zonder representatieve transactietests.

Test complete verkoop, annuleren, retour, korting, btw, afronding, cash, credit/debt, herdruk en herstel na database-/stroomuitval. Databasefouten mogen niet ongemerkt als geslaagde verkoop worden verwerkt. De bestaande exception handling in de datalaag vraagt daarvoor een gerichte controle. Deze inspectie is geen volledige transactie- of security-audit.

Bewijs: `EclipsePos.Data/TransItem.cs`, `EclipsePos.Data/SqlRelator.cs` en de EJournal-/PosEvents-code.

### 7. README-functionaliteit controleren op werkelijk aanwezige modules

De README noemt multistore-synchronisatie en Sage/Accpac. In `SystemManager/Bootstrapper.cs` zijn de module-registraties voor `EclipsePOS.AccpacDataSync` en `EclipsePOS.PosDataSync.Client` uitgecommentarieerd. In deze checkout zijn de bijbehorende moduleprojecten niet gevonden. Er bestaan wel gegevensstructuren voor Accpac-export. Daarom mogen deze functies niet als complete, werkende onderdelen van deze fork worden aangeboden.

Microsoft Sync Framework 2.1 is sinds januari 2021 uit ondersteuning. Maak voor multistore eventueel een nieuwe afzonderlijke synchronisatievoorziening, met versiebeheer en conflictafhandeling; installeer het oude framework niet standaard voor een enkele kassa.

### 8. De echte interface moderniseren

Koppel de nieuwe schermen aan bestaande autorisaties, artikel-/prijsregels, verkooptransacties en hardwarewrappers. Neem rollen en bestaande kassaworkflows mee. Voeg DPI-schaling, touch targets, focus/scannerbediening, thema's en bruikbare foutmeldingen toe. De huidige ModernPOS-demo is nog een aparte verkoopkern en kan de volledige app niet vervangen.

## Voorgestelde technische koers

### Databasekeuze: aanvullend onderzocht

Een moderne database kan worden gebruikt. De volgende afweging gaat specifiek over het behouden van de oorspronkelijke EclipsePOS-functionaliteit; er is nog geen database geïnstalleerd, schema gemigreerd of compatibiliteitstest op een draaiende server uitgevoerd.

**SQL Server 2025 Express — voorkeursroute voor deze bestaande app.** Gratis editie, officieel beschikbaar voor Windows 10/11, met maximaal 50 GB per relationele database. Express heeft ook capaciteitslimieten: maximaal het kleinste van één socket of vier cores, en 1.410 MB bufferpool-geheugen. Geschiktheid voor meerdere kassa's moet dus met de verwachte belasting worden getest. Een 32-bit POS-client kan via het databaseprotocol met een 64-bit databaseserver werken; de POS-driverarchitectuur hoeft daarom geen database-enginekeuze te bepalen. Een lokale server kan zonder internet functioneren. Meerdere kassa's kunnen een winkelserver gebruiken, maar zijn dan wel afhankelijk van die server en het lokale netwerk.

De lagere migratie-impact is een conclusie uit broninspectie: `EclipsePos.Data/ORM.xml` bevat 111 commandmappings naar 110 verschillende stored-procedurenamen. `SqlRelator.cs` gebruikt `SqlConnection`, `SqlCommand`, `SqlDbType`, outputparameters en `CommandType.StoredProcedure`. Het ene bekeken `CreatePosSite_v106.sql` bevat volgens een telling van CREATE-statements 63 tabellen en 111 procedures. Dit zijn aantallen uit specifieke bestanden, geen volledige inventaris van alle versies of bewijs dat mappings en schema exact overeenkomen. Daarnaast zijn SQL Server typed datasets en één DBML-bestand aanwezig. Een recente SQL Server houdt de bestaande API en SQL-dialectfamilie zoveel mogelijk intact, maar oude SQL en procedures moeten alsnog worden gevalideerd.

**PostgreSQL 18 — goede kandidaat voor een bredere herbouw.** Een ondersteunde opensource serverdatabase met Windows-installers. De PostgreSQL-site noemt voor de EDB-installer geteste Windows Server-platforms; desktop-Windows wordt als vergelijkbaar verwacht, dus Windows 10/11-acceptatie moet zelf worden uitgevoerd. Voor EclipsePOS vereist de overstap vertaling van T-SQL/schema/procedures, andere parameter-/resultaatcontracten waar nodig, vervanging van SqlClient en LINQ-to-SQL/typed-dataset-toegang, en tests van iedere verkoop-/beheerflow. Voor een nieuwe servicegerichte architectuur is dit serieus te overwegen. Alleen de connection string veranderen is onvoldoende.

**SQLite — interessant voor een enkele lokale kassa of latere offline-opslag.** Geen aparte databaseservice en geschikt voor lokale opslag. Het heeft geen SQL Server stored-procedurelaag, dus ook hier moet applicatielogica worden verplaatst of herschreven. Bewaar geld als integer in kleinste valuta-eenheid of met een expliciet geteste exacte representatie; neem SQL Server-decimalgedrag niet vanzelf aan. Deel een SQLite-bestand niet rechtstreeks via een netwerkschijf tussen kassa's. SQLite staat één gelijktijdige schrijver per database toe. Een centrale server met lokale SQLite-caches is mogelijk als ontwerp, maar vraagt nieuwe synchronisatie-, conflict- en dubbele-transactiebeveiliging; SQLite levert die niet automatisch.

**Aanpak voor de eerstvolgende databaseproef:** maak op SQL Server 2025 Express een afzonderlijke lege testdatabase aan, controleer de benodigde tabellen/procedures tegen de mappings, stel een beveiligde verbinding in en test artikelen, login, prijzen, btw, volledige verkoop, annulering/retour, concurrente transactienummers en herstart/herstel. Voer eerst een backup-/restoreproef uit. Moderniseer vervolgens de toegang via een afgebakende datalaag en versiegebonden migraties. Beoordeel een overstap naar PostgreSQL of SQLite opnieuw als eenvoudige distributie, opensource hosting of zelfstandige offline-kassa's zwaarder wegen dan behoud van de bestaande datalaag.

De OPOS/POS for .NET-apparaten blijven een aparte integratielaag. Een andere database maakt drivers niet automatisch incompatibel, maar het moment van definitief opslaan, printen en lade openen moet bij iedere migratie correct blijven.

Bronnen: [SQL Server 2025 Express download en Windows-vereisten](https://www.microsoft.com/en-us/download/details.aspx?id=108836), [SQL Server 2025 editielimieten](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025?view=sql-server-ver17), [PostgreSQL-versieondersteuning](https://www.postgresql.org/support/versioning/), [PostgreSQL Windows-installers](https://www.postgresql.org/download/windows/), [SQLite toepassingsgebieden en gelijktijdigheid](https://www.sqlite.org/whentouse.html).

### Applicatie en besturingssysteem

**Eerst stabiliseren:** WPF + .NET Framework 4.8, passende x86/x64 POS for .NET-drivers, gecontroleerde SQL Server-installatie en een ondertekende installer. Framework 4.8 is niet de nieuwste .NET-generatie, maar wel een ondersteund Windows-onderdeel afhankelijk van de Windows-lifecycle.

**Daarna moderniseren:** overweeg WPF op .NET 10 LTS voor de applicatie en houd niet-migreerbare hardwaredrivers in een apart .NET Framework-proces met expliciete communicatie. Dat is een ontwerpvoorstel, nog geen geïmplementeerde bridge. .NET 10 wordt tot november 2028 ondersteund; .NET 8/9 bereiken op 10 november 2026 einde ondersteuning. Het gebruik van de .NET 8 SDK tijdens deze controle verandert de doelruntime van de net48-app niet.

**OS-keuze:** nieuwe kassapc's bij voorkeur Windows 11. Windows 10 alleen met een passende nog ondersteunde LTSC/IoT-editie of ESU-dekking; gewone Windows 10-support eindigde op 14 oktober 2025. Technisch kunnen starten en ondersteund blijven zijn verschillende controles.

## Werkelijk uitgevoerd

- Broninspectie van projects/configuratie, installerhelpers, database-aanmaak, login, settings, hardware en module-registratie.
- Windows 11 Enterprise build 26200; .NET Framework release key 533509 aanwezig.
- Schone Rebuild van Workbench Data op net48: geslaagd, 24 bestaande compilerwaarschuwingen, 0 fouten.
- Schone Rebuild van SystemManager Data op net48: geslaagd, 6 bestaande compilerwaarschuwingen, 0 fouten.
- Volledige Workbench-build: blokkeert met expliciete ontbrekend-POS-SDK-melding.
- Volledige SystemManager-build via lokale dotnet SDK: blokkeert op ontbrekende gegenereerde XAML-code; ontbrekende CommonServiceLocator-reference gemeld.
- Vereistencontrole uitgevoerd in PowerShell 7 en Windows PowerShell 5.1; ook het alternatief met externe SQL-server en ontbrekend SDK-pad getest.
- Geen SQL Server-engine of POS-SDK gevonden. Geen installatie op een schone Windows 10/11-VM, database-integratietest, upgrade/uninstall-test of fysieke hardwaretest uitgevoerd.

```powershell
# Alleen-lezen controle op de doel-pc:
.\scripts\Test-WindowsPrerequisites.ps1
.\scripts\Test-WindowsPrerequisites.ps1 -SqlServer 'SERVER\SQLEXPRESS' -AsJson

# De twee geverifieerde oorspronkelijke dataprojecten:
dotnet build V.1.0.8/EclipsePOS.WPF.PosWorkBench/EclipsePos.Data/EclipsePos.Data.csproj -c Release
dotnet build V.1.0.8/EclipsePOS.WPF.SystemManager/EclipsePOS.WPF.SystemManager.Data/EclipsePOS.WPF.SystemManager.Data.csproj -c Release

# Workbench vereist geïnstalleerd SDK en passende WPF-buildtools:
dotnet build V.1.0.8/EclipsePOS.WPF.PosWorkBench/EclipsePOS.WPF.PosWorkBench/EclipsePOS.WPF.PosWorkBench.csproj -c Release '-p:PosNetSdkDir=C:\pad\naar\SDK'
```

## Acceptatie vóór een volledige release

Volledige reproduceerbare build; nieuwe installatie op beide gekozen OS-edities; starten als gewone kassamedewerker; database aanmaken en herstarten; upgrade met behoud van data; rollback/backup/herstel; fysiek hardwarepad; verkoopregressies; repair/uninstall zonder verlies van verkoopdata. Pas na deze controles kan het pakket als betrouwbaar installeerbare volledige app worden aangeboden.

## Actuele externe bronnen

- [WPF-overzicht](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview)
- [.NET Framework-lifecycle](https://learn.microsoft.com/en-us/lifecycle/faq/dotnet-framework)
- [Windows 10 release- en supportinformatie](https://learn.microsoft.com/en-us/windows/release-health/release-information)
- [SQL Server 2022 Express, ondersteunde OS-versies](https://www.microsoft.com/en-us/download/details.aspx?id=104781)
- [Microsoft POS for .NET FAQ](https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/pos-for-net/pos-for-net-faq)
- [Sync Framework 2.1 lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-sync-framework-21)
- [.NET supportbeleid](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
