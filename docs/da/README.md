# ESG-SignalCreator

En Windows-skrivebordsapplikation — **ESG Signal Studio** — til at styre Agilent/Keysight
**ESG-serie RF-signalgeneratorer** (f.eks. **E4438C**, E4400-serien). Den bygger
baseband-I/Q-bølgeformer på pc'en, forhåndsviser og validerer dem og downloader dem til
instrumentets dobbelte ARB-afspiller via en bevidst **Calculate → Download → Play**-pipeline.
Det er en moderne reimplementering af det ældre *Signal Studio for E4438C*
(se kravdokumenterne i [docs/](../)).

Bygget med C# / WinForms målrettet **.NET Framework 4.7.2**, opdelt i et UI-frit
kernebibliotek, WinForms-appen og et xUnit-testprojekt.

**Dokumentation:** [Brugervejledning](UserGuide.md) (en komplet reference for hver funktion) ·
[Tutorials](Tutorials.md) (21 praktiske gennemgange, simpel → kompleks) ·
[Manuel verifikation](ManualVerification.md) (trin-for-trin bænktest med en VSA-indstillingstjekliste) ·
[Packaging](../Packaging.md) (installer + release-build).
🌐 English: [README](../../README.md).

> 📝 **Dokumentationsparitet:** Engelsk er den autoritative version; det danske sæt under
> [docs/da/](.) er en maskinoversættelse, der holdes i paritet. Når du ændrer et engelsk dokument, skal
> du opdatere dets danske modstykke (og tilføje et, hvis det engelske dokument er nyt).

## Funktioner

- **Signal-flow-skal** — en øverste handlingslinje, et venstre projekttræ, et signal-flow-
  bloklærred (`Source → … → Output`), en personality-vælger med et live
  konfigurationspanel og en højre dok med op til tre verifikationsplot.
- **Signal-personalities** — tilkoblelige kilder, der producerer normaliseret I/Q:
  - **CW / enkelt tone** (frekvens-offset, sømløst løkkende)
  - **Multitone** (tonetabel, auto-spacing, random/equal/Newman-phasing, live PAPR)
  - **Brugerdefineret digital modulation** (BPSK/QPSK/8PSK/16–256-QAM/MSK, PN9/15/23-data,
    RRC/RC/Gaussisk pulsformning)
  - **AWGN** (båndbegrænset Gaussisk støj med crest-factor-clipping)
  - **Importér I/Q** (CSV/TSV, rå interleaved int16, WAV)
- **Verifikationsplot** — I/Q vs. tid, FFT-spektrum, konstellation og CCDF, hver
  med en view-dropdown og rubber-band-zoom.
- **Bevidst pipeline** — **Calculate** genererer I/Q uden for UI-tråden med en
  fremdriftslinje, plot og et live afhængighedstjek; **Download** slukker ARB'en,
  koder til interleaved 16-bit big-endian tos-komplement, indrammer en IEEE-488.2
  definite-length-blok og skriver den til volatil `WFM1`; **Play/Stop** aktiverer ARB'en
  og RF med en firetilstands-afspilningsindikator. En etklik **Calc → DL → Play** kører alle
  tre.
- **Validering** — en live-tjekker (minimum antal samples, even/granularitet, hukommelseskapacitet vs.
  den tilsluttede baseband-option, sample-clock- og bærebølgegrænser, DAC-over-range-
  heuristik) vist i en Notifications-dok.
- **Instrumentstyring** — forbind over **VISA** gennem enhver installeret provider (Keysight IO
  Libraries, NI-VISA, R&S, Rigol, …), til TCPIP/LAN-, GPIB-, USB- eller serielle ressourcer, med discovery
  og `*IDN?`/`*OPT?`; et instrumentindstillingspanel (frekvens, amplitude, RF/modulation, ARB-sample-
  clock, runtime-skalering) med tilbagelæsning; og en rå-SCPI-konsol med en tidsstemplet log.
- **Indbygget closed-loop-verifikation (E4406A / N9010A)** — forbind en VSA, og **Verify**
  måler derefter det afspillede signal (kanaleffekt, PAPR og — for en tone — frekvens) og viser
  det mod de forventede værdier (fra den genererede I/Q) i en Expected-vs-Measured
  **Verification**-visning med bestået/ikke bestået. En guidet **Path cal…**-guide indfanger kabeltab +
  analyzer-offset som en path-loss-korrektion; en **Reference**-menu låser begge instrumenter til en
  fælles 10 MHz tidsbase; en **VSA Mode**-menu (gated ud fra `:INSTrument:CATalog?`) vælger enhver
  installeret standard-personality (GSM / W-CDMA / cdma2000 / …). En **VSA model**-toggle vælger
  hvilken analyzer appen retter sig mod — E4406A eller en **Keysight N9010A (EXA)**; connect verificerer,
  at instrumentet matcher valget. _(N9010A-understøttelse lander i etaper — analyzer-
  valg og X-Series-kontrolplanet er på plads; SA-mode-målingsmapping er undervejs.)_
- **Indbygget Claude-assistent** (opt-in) — et natursprogspanel, der driver appen gennem en
  versioneret, function-calling-værktøjsoverflade i stedet for syntetiske klik: **read**-værktøjer (app-tilstand,
  config, validering, readout, personalities), **configure**-værktøjer (vælg/konfigurér en kilde, vælg et
  plot, projekt gem/indlæs, calculate) og **hardware**-værktøjer (connect, download, play/stop, indstil
  instrumentindstillinger). Guardrails håndhæves i dispatcheren, ikke i prompten: read/configure kører
  frit, men alt, der rører instrumentet, kræver et inline Approve/Decline-kort (RF- og bus-
  overtagelse bekræftes altid), og en før-udførelses-valideringsgate afviser hardware-handlinger ved en hård
  valideringsfejl — selv hvis godkendt. Den kan også **måle + verificere** på den tilsluttede analyzer (kanaleffekt,
  ACP, CCDF/PAPR, spektrumtop, bølgeform og et closed-loop `verify_signal`) og eksponerer en opt-in,
  altid-bekræftet **rå-SCPI**-nødudgang (slået fra som standard). Read-tool_uses kører samtidigt, mens
  configure/hardware forbliver serialiseret; lange chats komprimeres. Værktøjsoutput behandles som data, aldrig
  kommandoer; API-nøglen gemmes krypteret (Windows DPAPI); funktionen er slået fra, indtil den aktiveres. Dækket
  af en end-to-end acceptance-suite (schema-validitet, gate/bekræftelse, injection-modstand, SCPI-
  paritet, hemmeligheds-hygiejne).
- **Projekter** — gem/åbn den aktive kilde + indstillinger som en `*.ssproj` JSON-fil.
- Angiv **`--classic`** på kommandolinjen for at starte den oprindelige enkeltvindues-UI.

## Krav

- Windows med .NET Framework 4.7.2
- Visual Studio 2017+ (eller MSBuild) til at bygge
- Til live instrumentstyring, **enhver IVI-kompatibel VISA-runtime** installeret (Keysight IO Libraries
  Suite, NI-VISA, R&S, Rigol, …). Appen bruger de leverandørneutrale **IVI VISA.NET Shared Components**
  (`Ivi.Visa` / `GlobalResourceManager`) og dispatcher til den provider, der er installeret — GPIB,
  TCPIP/LAN, USB og seriel går alle gennem VISA. Der refereres ingen leverandørspecifikke assemblies.

Kernebiblioteket refererer til `Ivi.Visa` via en `HintPath`-post i
[ESG-SignalCreator.Core.csproj](../../ESG-SignalCreator.Core/ESG-SignalCreator.Core.csproj) (IVI VISA.NET
Shared Components, installeret af enhver VISA-provider); justér stien, hvis din installation afviger.
Authoring, forhåndsvisning, validering og projekt gem/indlæs fungerer alle uden noget instrument tilsluttet.

## Bygning

```powershell
# From a Developer Command Prompt / PowerShell with MSBuild on PATH
msbuild ESG-SignalCreator.sln -t:Restore,Build /p:Configuration=Release
```

Eller åbn `ESG-SignalCreator.sln` i Visual Studio og byg. Kør testene med
`dotnet test` eller VS Test Explorer (`-t:Restore` betyder noget — testprojektet bruger
NuGet `PackageReference`s).

## Brug

1. Klik på **Connect…**, indtast eller opdag en **VISA resource** (f.eks.
   `TCPIP0::192.168.1.82::inst1::INSTR` eller `GPIB0::19::INSTR`) og forbind (`*IDN?`/`*OPT?` vises).
2. I **Source**-visningen skal du vælge en personality fra vælgeren og redigere dens
   parametre (sample rate, længde som tid/samples/symboler og personalitys
   egne indstillinger).
3. Klik på **Calculate** for at generere og forhåndsvise bølgeformen; tjek
   **Notifications** for eventuelle valideringsadvarsler.
4. Klik på **Download** og derefter **Play** (eller den kombinerede **Calc → DL → Play**) for at indlæse
   og køre bølgeformen; **Stop** slukker ARB'en. Instrumentindstillings- og
   SCPI-konsol-visningerne er tilgængelige fra det venstre træ.

## Hardware-in-the-loop-test

Unit-testene kører **uden instrument** (blokindramning, encoder, DSP, validering,
VSA-SCPI-parsing, skærmoptagelses-blokafkodning, …). For at validere de rigtige instrumenter skal du køre den headless harness
([ESG-SignalCreator.HilHarness](../../ESG-SignalCreator.HilHarness/)):

```powershell
# ESG-only: RF stays OFF (power -30 dBm) unless --rf-on briefly enables it.
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"

# Comprehensive closed-loop battery (ESG -> VSA) across the frequency range:
# verifies EVERY signal type on the analyzer, with a machine-readable report.
ESG-SignalCreator.HilHarness.exe --vsa GPIB0::17::INSTR --all --dwell-seconds 3 --json report.json

# Target a Keysight N9010A instead of the E4406A (LAN address, --vsa-model):
ESG-SignalCreator.HilHarness.exe --vsa TCPIP0::192.168.1.90::hislip0::INSTR --vsa-model n9010a --all

# Install self-test: the CW/AM/FM/IQ battery on the one selected analyzer (JSON + exit code):
ESG-SignalCreator.HilHarness.exe --install-verify --vsa GPIB0::17::INSTR --vsa-model e4406a --json verify.json

# A single signal type, or the amplitude-flatness power sweep:
ESG-SignalCreator.HilHarness.exe --vsa --signal multitone
ESG-SignalCreator.HilHarness.exe --vsa --flatness
#   options: --vsa-model e4406a|n9010a --points N --start-hz --stop-hz --carrier-hz --offset-hz
#            --verify-power-dbm --max-input-dbm --path-loss-db --dwell-seconds --json

# AUTOMATISERET skærmbillede: driv CW/AM/FM/IQ, mål hver, og optag analyzer-skærmen pr. trin —
# én kommando, ingen manuel opsætning. Skriver cw/am/fm/iq-multitone-billeder + en index.md i mappen:
ESG-SignalCreator.HilHarness.exe --install-verify --vsa GPIB0::17::INSTR --vsa-model e4406a --capture-dir docs/images/vsa

# Eller optag blot analyzerens NUVÆRENDE display (kun analyzer, ingen ESG/RF), til et ad-hoc-billede:
ESG-SignalCreator.HilHarness.exe --capture-screen docs/images/vsa/cw-result.png --vsa GPIB0::17::INSTR --vsa-model e4406a
#   SCPI-tilsidesættelser for begge modes (bekræft/justér pr. firmware):
#     --capture-data-query ":MMEMory:DATA? \"{0}\"" --capture-save-cmd ":MMEMory:STORe:SCReen \"{0}\""
#     --capture-cleanup-cmd ":MMEMory:DELete \"{0}\"" --capture-temp-path "C:\Temp\ESGCAP.png"
```

ESG-only-tilstand tjekker `*IDN?`/`*OPT?`, downloader en CW til `WFM1`, aktiverer ARB'en og læser
frekvens/amplitude tilbage. Det **closed-loop-batteri** (`--all`) forbinder den analyzer, der er valgt med
`--vsa-model` (E4406A som standard, eller N9010A; afviser en model, der ikke matcher), håndhæver **input-skade-
sikkerhedsgaten** (per-model standard — E4406A +30 dBm under dens +35 dBm-rating; N9010A +30 dBm / 1 W iht.
dens datablad), og for hver signaltype — **CW, multitone, AWGN, custom-mod (QAM), multi-carrier,
I/Q-impairment, import-I/Q** — driver ESG'en på et sikkert niveau over et frekvenssweep og
verificerer på analyzeren:

- **kanaleffekt** vs. det kommanderede niveau, og **PAPR** (CCDF) vs. den værdi, der beregnes ud fra den
  genererede I/Q, for hvert signal;
- **tonefrekvens** (CW / import-I/Q), **ACPR** (custom-mod) og **gain-imbalance-billedet**
  (I/Q-impairment) hvor relevant.

Analyzeren kører i kontinuerlig tilstand under per-punkt-dwell'en, så frontpanelet følger med live;
kørslen slutter RF-off med analyzeren stadig sweepende. Per-trin PASS/FAIL, valgfri JSON-rapport,
ikke-nul exit ved fejl. Et separat konsolprojekt, holdt uden for unit-test-kørslen, så CI forbliver
hardware-fri.

**Skærmoptagelse** producerer de per-trin VSA-skærmbilleder til tutorials og
[Manuel verifikation](ManualVerification.md)-dokumentet (billeder skrevet som PNG på X-Series, GIF på
E4406A). To modes:

- **Automatiseret** — `--install-verify --capture-dir <dir>` driver ESG'en gennem CW/AM/FM/I-Q-batteriet,
  måler hvert på analyzeren og optager analyzerens display efter hvert signal, alt sammen i **én
  kommando uden manuel opsætning**. Den skriver `cw`-, `am`-, `fm`-, `iq-multitone`-billeder plus en
  `index.md`, der indlejrer dem (klar til at indsætte i dokumentationen).
- **Ad-hoc** — `--capture-screen <file>` er kun analyzer (ingen ESG/RF): den optager, hvad end VSA'en
  aktuelt viser, til et engangsbillede, efter du selv har sat et signal op.

Begge læser displayet tilbage over VISA som en IEEE-488.2-blok. Standard-optage-SCPI'en er manuelt afledt
og **kræver bænkbekræftelse** — `--capture-*`-tilsidesættelserne lader dig tune den pr. firmware uden en
genbygning.

> Bænkvalideret (2026-06, E4406A FW A.08.10) over 50 MHz–3 GHz: alle signaltyper PASS —
> f.eks. multitone PAPR ≈3,8 dB (forv. 2,9), AWGN crest ≈10,2 dB, 16-QAM ACPR ≈−48 dBc, en 3 dB
> I/Q-gain-imbalance → billede ved −15,4 dBc (matcher teorien), og amplitudenøjagtighed inden for et
> konsistent ~0,76 dB kabel-roll-off ved 3 GHz.
>
> N9010A-understøttelse er afledt af Keysight X-Series-manualerne og unit-testet for SCPI-dialekten
> (mode-routing, målingsrødder og result-scalar-rækkefølger), men er **endnu ikke bænkvalideret** —
> bekræft ACP-resultatlayoutet og max-safe-input-grænsen mod din enhed.

## Installer

To artefakter bygges med det gratis **WiX Toolset v5** (gendannet fra NuGet af `dotnet build`, ingen
toolset-installation nødvendig) — en **`setup.exe`**-bootstrapper og en rå **MSI**. Fra repo-roden:

```powershell
./build-installer.ps1 -Version 1.0.0.0   # builds the app, the MSI, then the setup.exe
```

- **`ESG-SignalCreator-Setup-<version>.exe`** (anbefalet) — en bootstrapper, der **kæder .NET
  Framework 4.7.2-installeren**: den installerer frameworket automatisk, hvis det mangler, og derefter appen.
- **`ESG-SignalCreator-<version>.msi`** — den rå pakke, til maskiner, der allerede har .NET 4.7.2.

Begge installerer appen per-maskine til `Program Files`, tilføjer Start-menu-/skrivebordsgenveje og en ordentlig
Tilføj/fjern-programmer-post (med app-ikonet) og registrerer en installeret **VISA**-runtime (leverandørneutral
— Keysight, NI, R&S, Rigol, …). Installer-/bootstrapper-projekterne holdes uden for solutionen, så en
maskine uden WiX stadig bygger appen. Se [docs/Packaging.md](../Packaging.md) for detaljer.

Færdigbyggede installere udgives på [Releases](https://github.com/TGoodhew/ESG-SignalCreator/releases)-
siden. Et GitHub Actions-workflow bygger MSI'en + bootstrapperen og udgiver en release ved hvert push til
`main` (prerelease) og ved hvert `vX.Y.Z`-tag (stabil) — se [docs/Packaging.md](../Packaging.md#continuous-release-github-actions)
(bygger på en Windows-runner med IVI VISA.NET Shared Components — enhver VISA-provider — installeret).

## Projektlayout

Solutionen er opdelt i et UI-frit kernebibliotek, WinForms-appen og et testprojekt:

| Sti | Formål |
|------|---------|
| [ESG-SignalCreator.Core/](../../ESG-SignalCreator.Core/) | Klassebibliotek — ingen UI-afhængighed. Transport, ARB-encoding, DSP, personalities, validering. |
| [Core/EsgController.cs](../../ESG-SignalCreator.Core/EsgController.cs) | SCPI-hjælpere på højt niveau (frekvens, effekt, ARB-download/afspilning) |
| [Core/Instruments/](../../ESG-SignalCreator.Core/Instruments/) | `IInstrument` transport-abstraktion; VISA- og GPIB-implementeringer (488.2) |
| [Core/Visa/](../../ESG-SignalCreator.Core/Visa/) | `EsgInstrument`-facade + `*IDN?`/`*OPT?`-parsing |
| [Core/Arb/](../../ESG-SignalCreator.Core/Arb/) | IEEE-488.2-blokindramning og int16/interleave/big-endian ARB-encoderen |
| [Core/Model/](../../ESG-SignalCreator.Core/Model/) | `WaveformModel` — det neutrale I/Q-output fra hver signal-personality |
| [Core/Personalities/](../../ESG-SignalCreator.Core/Personalities/) | `IWaveformPersonality`-kontrakt + CW, Multitone, CustomMod, AWGN, Import-IQ |
| [Core/Dsp/](../../ESG-SignalCreator.Core/Dsp/) | FFT, FIR (RRC/RC/Gaussian), vinduer, CCDF/PAPR, resampling |
| [Core/Validation/](../../ESG-SignalCreator.Core/Validation/) | `WaveformValidator` afhængighedstjekker |
| [Core/Capability/](../../ESG-SignalCreator.Core/Capability/) | Per-target capability-profiler (indlejret JSON) |
| [Core/Timing/](../../ESG-SignalCreator.Core/Timing/) | `SampleCountSolver` (tid/samples/symboler → sample-antal) |
| [Core/Project/](../../ESG-SignalCreator.Core/Project/) | `SsProject` + `ProjectStore` (`.ssproj` gem/indlæs) |
| [App/Ui/](../../ESG-SignalCreator.App/Ui/) | `StudioForm`-skal, signal-flow-lærred, kildepaneler, plot-paneler, instrument-UI |
| [ESG-SignalCreator.App/](../../ESG-SignalCreator.App/) | WinForms-applikation — refererer Core (indgangspunkt `Program.cs`) |
| [Core/Measure/](../../ESG-SignalCreator.Core/Measure/) | VSA-målinger (E4406A Basic-mode / N9010A SA + IQ Analyzer, via en per-model SCPI-dialekt): Channel Power, ACP, CCDF, Spectrum, Waveform, Power-vs-Time + mask |
| [Core/Verify/](../../ESG-SignalCreator.Core/Verify/) | Closed-loop-verifikation harness/profil/resultat, RF-path-sikkerhedsgate, path-kalibrering |
| [ESG-SignalCreator.Assistant/](../../ESG-SignalCreator.Assistant/) | Indbygget Claude-assistent: Messages API-klient, agent-loop, værktøjsoverflade (read/configure/hardware), guardrails, DPAPI-hemmeligheder |
| [ESG-SignalCreator.Tests/](../../ESG-SignalCreator.Tests/) | xUnit-tests (356: framing, encoder, DSP, personalities, validering, sekvensering, målinger, verifikation, assistent-værktøjer + guardrails + acceptance, …) |
| [ESG-SignalCreator.HilHarness/](../../ESG-SignalCreator.HilHarness/) | Headless hardware-in-the-loop-testkører for en rigtig E4438C |

Kør testene med `dotnet test` eller VS Test Explorer.

## Ansvarsfraskrivelse

Ikke tilknyttet eller godkendt af Keysight Technologies, Agilent eller National
Instruments. "ESG", "E4438C", VISA og GPIB refereres udelukkende af hensyn til interoperabilitet.
Brug på eget ansvar ved styring af rigtig hardware.

## Licens

Udgivet under [MIT License](../../LICENSE).
