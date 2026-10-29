# Eclipse / Market — moderne WPF retaildemo

Touchvriendelijke kassademo binnen de EclipsePOS-fork. De oorspronkelijke broncode en MIT-licentie zijn behouden. Deze nieuwe WPF-shell heeft een kleine lokale verkoopkern en is **nog niet gekoppeld aan de oorspronkelijke SQL-/ERP-engine**.

## Starten

Pak `EclipsePOS-Modern-Demo.zip` uit en start `EclipsePOS.Modern.exe` op Windows met **.NET Framework 4.8**. Een .NET 8-runtime alleen is onvoldoende. Geen SQL Server, POS-SDK of drivers nodig in demomodus.

Klik artikelen aan, kies **Afrekenen**, daarna **Pin simuleren** of **Contant**. Contant heeft een touch-toetsenbord en berekent wisselgeld. **Thema** wisselt donker/licht; **Laatste bon** toont de laatste afgeronde verkoop van deze sessie. De kassa start leeg met twaalf voorbeeldartikelen in de catalogus.

## Bouwen en installeren

Benodigd: Windows, .NET 8 SDK of compatibele recentere SDK, internet voor eerste NuGet-restore en .NET Framework 4.8 om uit te voeren. Visual Studio 2022 met .NET-desktopontwikkeling kan het csproj rechtstreeks openen.

```powershell
git clone https://github.com/centauri/eclipsepos.git
cd eclipsepos
git switch feature/modern-retail-pos
dotnet build ModernPOS/ModernPOS.csproj -c Release
& .\ModernPOS\bin\Release\net48\EclipsePOS.Modern.exe
```

`ModernPOS/build-demo.ps1` bouwt, voert de self-test uit en maakt het startklare zipbestand. Pak het uit in een schrijfbare map. Er is geen installatieprogramma nodig. Alleen de Microsoft reference-assemblies worden via NuGet opgehaald; vendor-SDK's worden niet herverdeeld.

De standaardbuild is **x86**, passend bij veel bestaande OPOS-serviceobjecten. Voor uitsluitend 64-bit drivers bouw je met `-p:PlatformTarget=x64` en gebruik je passende SDK-/drivercomponenten. Meng geen 32- en 64-bit componenten.

## Functies en opslag

- Twaalf artikelen, categorieën, zoeken, barcode-invoer, aantallen, verwijderen en verkoop wissen.
- Gewichtsartikelen: demo-invoer in kg via touch-toetsenbord; hardwaremodus gebruikt de echte weegschaal.
- Prijzen inclusief voorbeeld-btw van 9% en 21%, afronding op regelniveau, contant met wisselgeld en gesimuleerde pin.
- Afgeronde verkopen staan als JSON in `%LOCALAPPDATA%\EclipsePOS.Modern\sales`, met regels, btw, totaal, betaalwijze en bontekst. Opslag gebeurt vóór printen/lade openen. Bij een printfout blijft de verkoop opgeslagen; herdruk is gemarkeerd als kopie.
- Geen echte betaalterminal, productieboekhouding, retouren, rollen/autorisaties, voorraadbeheer, fiscale certificering of ERP-/SQL-synchronisatie in deze demo. Alle bonnen zijn als demo gemarkeerd.

Barcodevoorbeeld: `8710000000011` voor flat white; `2001` en `2002` zijn gewichtsartikelen. Een USB-scanner in toetsenbordmodus werkt met focus in het zoekveld en Enter als suffix. POS for .NET-scanners gebruiken events.

## Echte POS for .NET / OPOS-apparaten

De oorspronkelijke `ScannerWrapper`, `PosPrinterWrapper`, `CashDrawerWrapper` en andere integraties onder `V.1.0.8/EclipsePOS.WPF.PosWorkBench/EclipsePos.Apps/Hardware` zijn ongewijzigd behouden. De moderne shell gebruikt `Hardware.cs` met echte `Microsoft.PointOfService`-aanroepen: `PosExplorer`, expliciete logische namen, `Open`, `Claim`, `DeviceEnabled`, scanner-`DataEvent`, `PrintNormal(Receipt, ...)`, `OpenDrawer`, `ReadWeight`, `Release` en `Close`.

Runtime-binding maakt de SDK optioneel voor demobuilds. Ontbrekende SDK's, namen of apparaten geven een fout; er wordt geen apparaat gesimuleerd in PosNet-modus. Niet-geconfigureerde apparaten zijn optioneel. Als één geconfigureerd apparaat faalt, wordt de volledige verbindingspoging opgeruimd.

1. Installeer Microsoft POS for .NET 1.14.1 en de passende serviceobjecten van de fabrikant. Voor Epson: een voor het model geschikte **EPSON OPOS ADK for .NET** of een door POS for .NET ondersteund legacy OPOS-serviceobject. Een gewone Windows-printerdriver alleen is onvoldoende.
2. Configureer en test apparaten eerst in de vendor-tool, met expliciete logische namen. Een Epson-lade is vaak een apart apparaat aan dezelfde printerpoort; volg de modelspecifieke instructies.
3. Pas `hardware.json` naast de exe aan. Controleer het werkelijke SDK-pad; onderstaand pad is slechts het standaardvoorbeeld.

```json
{
  "Mode": "PosNet",
  "AssemblyPath": "C:\\Program Files (x86)\\Microsoft Point Of Service\\SDK\\Microsoft.PointOfService.dll",
  "Scanner": "RetailScanner",
  "Printer": "EpsonReceipt",
  "Drawer": "EpsonDrawer",
  "Scale": ""
}
```

4. Druk op **Apparaten** om de configuratie te herladen en apparaten te claimen. Sluit andere apps die dezelfde apparaten claimen. De statusregel en foutmelding geven het resultaat. Controleer na een apparaatfout de hardware en verbind opnieuw.
5. Scan een voorbeeldbarcode en rond een demoverkoop af: de geconfigureerde printer print echt; alleen contant opent de geconfigureerde kassalade. **In PosNet-modus hebben print- en ladeacties dus fysieke gevolgen.** Pin blijft gesimuleerd.
6. Configureer een weegschaal om **kilogram** te retourneren en controleer dit met een bekend gewicht vóór gebruik. De demo neemt de decimale waarde als kg over, zonder automatische eenheidsdetectie. Time-out, nul en negatieve waarden worden geweigerd.

De eenvoudige tekstbon moet op de echte Epson worden getest op papierbreedte en speciale tekens. Automatisch snijden en uitgebreid apparaatfoutherstel zijn niet geïmplementeerd. SDK-/driverondersteuning hangt af van apparaat, Windows-versie en architectuur.

Bronnen: [Microsoft PosExplorer](https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/pos-for-net/posexplorer-class), [scanner-events](https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/pos-for-net/scanner-events), [ReadWeight](https://learn.microsoft.com/en-us/dotnet/api/microsoft.pointofservice.scale.readweight?view=point-of-service-1.14), [vendor-/OPOS-serviceobjecten](https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/pos-for-net/pos-for-net-faq), [Epson .NET 4+ ADK](https://download3.ebz.epson.net/dsc/f/03/00/15/42/15/ca378a14c71dbd21da64d130301fcbc386031d56/ov_OPOSN1.14.29.pdf).

## Beoordeling van de oorspronkelijke opbouw

Bron: publieke MIT-repository `musoftware/eclipsepos`, upstream-commit `05e936be12005a9f4405adb8720c5c3abd02f36a`, zelf een fork van `naushard/EclipsePOS`.

De README noemt .NET 3.5, maar de hoofdprojecten in `V.1.0.8` richten zich op **.NET Framework 4.7**. Oude Composite Application Library-onderdelen gebruiken 3.5/4.0. Workbench, Apps, Data en SystemManager zijn afzonderlijke projecten, met WPF, oude Prism/Composite en Unity. Hardware en verkoopverwerking zijn gekoppeld via applicatiecontext en events.

De vele relatieve `HintPath`-verwijzingen naar lokale `Program Files`, POS for .NET 1.12, LIB-bestanden, Sync Framework en Windows SDK maken de oorspronkelijke build afhankelijk van historische installaties. Er is ook een DesktopBridge `.wapproj`. De legacy-build is hier geprobeerd en geblokkeerd op ontbrekende .NET 4.7-reference-assemblies en `Microsoft.DesktopBridge.props`; verdere afhankelijkheden zijn daardoor niet volledig gevalideerd.

Daarom staat de nieuwe compileerbare shell apart op .NET Framework 4.8. De bestaande engine en hardwarecode blijven behouden als broncode; een productie-upgrade vraagt nog migratie van de verkoopengine en hardwaretests.

## Verificatie

```powershell
dotnet build ModernPOS/ModernPOS.csproj -c Release
cd ModernPOS/bin/Release/net48
$p = Start-Process .\EclipsePOS.Modern.exe --self-test -Wait -PassThru
$p.ExitCode
$p = Start-Process .\EclipsePOS.Modern.exe --smoke -Wait -PassThru
$p.ExitCode
```

Exitcode 0 betekent geslaagd. `--self-test` controleert barcode-uniciteit, gemengde totalen/btw, samenvoegen, negatieve aantallen, maximumaantal, gewichtsafronding, boninhoud, opslag/overschrijfbeveiliging en gedrag zonder hardware. Resultaten worden in de huidige map als `self-test-passed.txt` of `self-test-failed.txt` opgeslagen.

`--smoke` rendert de echte WPF-hoofdvensters in beide thema's en de betaal-/contantdialoog naar PNG's. Hiervoor is een interactieve Windows-desktopsessie nodig. Dit is geen volledige touch- of hardwaretest.

Lokaal geverifieerd: Release-build zonder waarschuwingen/fouten, self-test geslaagd, vensters gerenderd en visueel bekeken. **POS-SDK en fysieke hardware ontbreken hier: runtime-binding aan het SDK, scanner-events, Epson-printing, lade en schaal zijn niet fysiek getest.**

## Licentie

MIT, zie de oorspronkelijke `LICENSE`. Vendor-drivers en Microsoft POS for .NET hebben eigen voorwaarden en worden niet herverdeeld.
