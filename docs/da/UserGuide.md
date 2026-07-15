# ESG-SignalCreator — Brugervejledning

> 🌐 English: [User Guide](../UserGuide.md) · **Dansk** (denne side)

En komplet reference for **ESG-SignalCreator**, en Windows-applikation der bygger vilkårlige I/Q-bølgeformer, afspiller dem på en Agilent/Keysight **E4438C** ESG-vektorsignalgenerator og (valgfrit) verificerer resultatet på en **VSA** (en Agilent **E4406A** eller en Keysight **N9010A**) — med en indbygget **Claude-assistent** der kan drive hele forløbet i naturligt sprog.

Dette dokument forklarer *hvad appen gør, og hvordan hver enkelt funktion virker*. For trinvise gennemgange, se [Tutorials.md](Tutorials.md).

---

## 1. Hvad appen er

ESG-SignalCreator omsætter *hensigt* ("et 4-tones signal ved 1 GHz, −10 dBm, lavest mulige PAPR") til et reelt RF-signal, der kommer ud af generatoren, og kan bevise at RF-signalet matcher hensigten ved at måle det på analysatoren. Den er organiseret omkring fire idéer:

- **Signalpersonligheder** — pluggbare *kilder* der hver producerer en normaliseret baseband-**I/Q-bølgeform** (CW, Multitone, Multi-Carrier, Custom Digital Modulation, AWGN, Import I/Q).
- **En bevidst pipeline** — du **Calculate** (beregner) bølgeformen på pc'en, **Download** (henter) den til generatorens ARB-hukommelse og **Play** (afspiller) den derefter (armerer ARB'en + tænder RF). Hvert trin er eksplicit, så du altid ved, hvad der er på instrumentet.
- **Closed-loop (lukket sløjfe) verifikation** — med en VSA (E4406A eller N9010A) forbundet til generatorens RF-udgang måler appen det afspillede signal (kanaleffekt, PAPR, tonefrekvens, ACP, …) og sammenligner **forventet vs. målt**.
- **En assistent** — en tilvalgsrude hvor Claude bruger de *samme* operationer som du gør, gennem en beskyttet værktøjsflade (intet når DAC'en eller RF uden din eksplicitte godkendelse).

Alt undtagen live instrument-I/O virker **offline**: du kan bygge, forhåndsvise, validere og gemme bølgeformer uden et instrument tilsluttet.

Appen er C#/.NET Framework 4.7.2 / WinForms. Start den moderne skal (`StudioForm`) på normal vis, eller angiv `--classic` på kommandolinjen for det oprindelige single-window-UI (`MainForm`).

---

## 2. Nøglebegreber

- **I/Q-bølgeform** — et komplekst baseband-signal, en strøm af (I, Q)-sampleparr ved en **sample clock** (samplingsrate). ESG'ens ARB afspiller disse samples for at rekonstruere den modulerede RF-bærebølge.
- **ARB (arbitrary waveform generator)** — ESG-optionen (001/601 eller 002/602) der lagrer og genafspiller I/Q-samples fra "WFM1" flygtig hukommelse. Kræver en baseband-generator-option.
- **PAPR / crest-faktor** — peak-to-average power ratio (dB). Høj PAPR (f.eks. støj, OFDM-lignende multitone) belaster forstærkere; lav-PAPR-fasning (Newman) pakker de samme toner med mindre peaks.
- **Runtime-skalering** — ARB'en afspiller samples ved en brøkdel af fuld skala (standard 70 %) for at efterlade DAC-headroom og undgå over-range-clipping.
- **Sample-granularitet / minimumssamples** — ESG'en kræver, at bølgeformens længde opfylder et minimum (≈60 samples) og en granularitet; validatoren kontrollerer dette.
- **Sømløs loop** — en afspillet bølgeform looper; hvis enden ikke flugter med begyndelsen, opstår der en diskontinuitet ("søm"). Længder med heltallige cyklusser looper rent.
- **VISA-ressource** — adressestrengen for et instrument, f.eks. `TCPIP0::192.168.1.82::inst1::INSTR` (LAN) eller `GPIB0::17::INSTR` (GPIB). Appen bruger en hvilken som helst installeret VISA-provider til at åbne den.

---

## 3. Krav og installation

- **Windows** med **.NET Framework 4.7.2** eller nyere.
- For **live instrumentstyring**, en hvilken som helst **IVI-kompatibel VISA-runtime** installeret — Keysight IO Libraries Suite, NI-VISA, Rohde & Schwarz, Rigol osv. Appen kommunikerer med instrumenter gennem den leverandørneutrale **IVI `GlobalResourceManager`**, så uanset hvilken provider der er installeret, bruges den automatisk (for TCPIP/LAN-, GPIB-, USB- og serielle ressourcer).
- **Installér** fra MSI'en på projektets Releases-side (per-machine, x64; Start-menu + valgfrie skrivebordsgenveje; ren afinstallation). Installationsprogrammet håndhæver .NET 4.7.2 og detekterer en VISA-runtime. Se [Packaging.md](../Packaging.md) for build/CI-detaljer.
- **Afspilning på instrumentet** kræver en E4438C med en baseband/ARB-option (001/601 eller 002/602). **Verifikation** kræver en VSA (E4406A eller N9010A) på generatorens RF-udgang.

---

## 4. Hovedvinduet

Skallen er opdelt i fire områder:

### 4.1 Øverste værktøjslinje (handlinger)

| Knap | Hvad den gør |
|--------|--------------|
| **Connect…** | Åbn forbindelsesmanageren og forbind til ESG'en (VISA-ressource eller GPIB-board/-adresse). |
| **Connect VSA…** | Forbind analysatoren (E4406A eller N9010A, iht. **VSA model**-knappen), inklusive RF-vejens sikkerhedsindstillinger (§9). |
| **Calculate** | Generér I/Q-bølgeformen ud fra den aktuelle kilde + impairments (uden for UI-tråden, med en fremdriftslinje). Opdaterer plots, validering og aflæsning. Ingen hardware. |
| **Download** | Indkod bølgeformen og skub den til generatorens ARB-hukommelse (WFM1). Kræver en forbindelse. |
| **Play** | Armér ARB'en og tænd RF **on**. |
| **Stop** | Disarmér ARB'en og sluk RF **off**. |
| **Verify** | Closed-loop-mål det afspillede signal på analysatoren og vis forventet-vs-målt (§9). |
| **Path cal…** | Kør guiden til vejkalibrering for at opfange kabeltab + analysatoroffset (§9). |
| **Verify install…** | Kør installations-selvtesten — et CW → AM → FM → I/Q-batteri målt på analysatoren (§9.7). |
| **Reference** | Lås ESG og analysatoren til uafhængige tidsbaser eller en fælles ekstern 10 MHz. |
| **VSA model** | Skift hvilken analysator appen målretter — E4406A eller N9010A (§9). |
| **VSA Mode** | Vælg analysatorens måletilstand blandt de tilstande, der faktisk er installeret på enheden. |
| **Calc → DL → Play** | Kør alle tre pipeline-trin i rækkefølge. |
| **Save… / Open…** | Gem eller indlæs et projekt (`.ssproj`). |

Statuslinjen i bunden viser en statusmeddelelse, **Online/Offline**-indikatoren og den forbundne instrumentmodel.

### 4.2 Venstre navigationstræ (visninger)

Når du vælger en node, vises dens kort i midten:

- **Source** — vælg en signalpersonlighed og redigér dens parametre.
- **Impairments** — anvend valgfrit I/Q-, AWGN-, CFR- og filter-impairments.
- **Sequence** — byg en sekvens af bølgeformssegmenter.
- **Instrument settings** — frekvens, amplitude, RF/modulation, ARB sample clock, runtime-skalering, med tilbagelæsning.
- **SCPI console** — send rå SCPI og følg en tidsstemplet log.
- **Notifications** — outputtet fra validerings-/afhængighedstjekkeren.
- **Verification** — tabellen Forventet-vs-Målt fra sidste Verify-kørsel.
- **Assistant** — den indbyggede Claude-assistentrude.

### 4.3 Højre dok (plots og tilstand)

- **Tre plot-ruder**, hver med en visningsvælger: **I/Q vs time**, **Spectrum** (FFT), **Constellation**, **CCDF** og **Eye**. Standardvisningerne er IQ / Spectrum / Constellation; rubber-band-zoom understøttes.
- En **resultataflæsnings**-stribe: sample-antal, sample clock, varighed, downloadstørrelse (bytes), peak, RMS, PAPR og 99 % optaget båndbredde.
- En **fremdriftslinje** for Calculate.
- En **afspilningstilstandsindikator** (Idle / Busy / Waiting-for-trigger / Playing).

---

## 5. Signalkilder (personligheder)

Åbn **Source**-visningen, vælg en personlighed fra vælgeren, og redigér dens parametre i panelet. Hver personlighed producerer en `WaveformModel` (I/Q + samplingsrate), når du Calculate.

### 5.1 CW / Enkelt tone
En enkelt continuous-wave-tone. Parametre: **frekvensoffset** fra bærebølgen (Hz), **amplitude** (dBFS, 0 = fuld skala, negativ = neddrosling) og **startfase** (grader). Nyttig som reference og til frekvens-/niveautjek.

### 5.2 Multitone
N ligeligt fordelte toner. Parametre: **antal toner**, **toneafstand** (Hz, eller auto) og **fasestrategi** — **Newman** (minimerer PAPR), **Random** eller **Zero** (alle faser justeret → høj PAPR). En klassisk test af forstærkerlinearitet og PAPR-håndtering.

### 5.3 Multi-Carrier
Flere uafhængigt placerede bærebølger (hver kan have sin egen modulation), summeret til én bølgeform — til multi-kanals-/multi-standard-scenarier.

### 5.4 Custom Digital Modulation
En digitalt moduleret bærebølge. Parametre: **modulationsformat** (BPSK, QPSK, 8PSK, 16/64/256-QAM, MSK…), **symbolrate** (Hz), et **pulsformende filter** (RRC, RC, Gaussian eller intet) med **roll-off / BT** (alpha) og et **payload**-mønster (PN9/PN15/PN23, all-ones/zeros, random). Bruges til ACP/ACPR og modulationskvalitetsarbejde.

### 5.5 Pulse Building
Et gentaget radarlignende pulstog (en v1 af Signal Studio for Pulse Building, N7620A). Parametre: **pulsbredde** (s), **pulsgentagelsesinterval / PRI** (s, ≥ pulsbredde), en valgfri raised-cosine **stige-/faldtid** (s, 0 = rektangulære kanter), en **startforsinkelse** (s) og **intra-puls-modulationen** — **None** (gated CW-burst), **Linear FM-chirp** (med en fejet **båndbredde** i Hz) eller en **Barker-fasekode** (længde 2/3/4/5/7/11/13). En enkelt puls bygges og gentages ved PRI'en for at udfylde bølgeformen; en et-samples **markør** udsendes ved hver pulsstart (nyttig som ARB-trigger/scope-sync). Bruges til radar/EW-modtagertest og pulskompressionsarbejde. Avancerede N7620A-funktioner (per-puls-offsettabeller, forskudt/jittret PRI, antennescanningsmønstre, CSV-import) er endnu ikke implementeret.

### 5.6 AWGN
Båndbegrænset additiv hvid gaussisk støj. Parametre: **støjbåndbredde** (Hz), **carrier-to-noise-ratio** (dB) og valgfri **peak clipping**. AWGN har en høj crest-faktor (~10 dB) — en god headroom- og CCDF-test.

### 5.7 Import I/Q
Indlæs I/Q fra en fil — appens svar på Signal Studio Toolkit (N7622A) "medbring din egen I/Q"-arbejdsgang. Parametre: **filsti** (du angiver den), **format** (**Auto**-detektér efter filendelse, afgrænset tekst CSV/TSV, rå interleaved little-endian **Int16** `.bin`/`.iq`, Agilent/Keysight big-endian **Int16** `.agt` — ESG'ens native ARB-byteorden — eller 16-bit PCM **WAV**), kilde-**samplingsrate** (Hz), valgfri **I/Q-swap** og en **skala**-multiplikator. Angiv formatet eksplicit for at gennemtvinge Agilent big-endian-byteorden, når en filendelse er tvetydig. Lader dig genafspille eksternt opfangede eller eksternt genererede (MATLAB/C++) signaler. *(MATLAB `.mat`-import understøttes endnu ikke — eksportér til CSV, WAV eller et 16-bit binært format.)*

### 5.8 Multitone Distortion
Et tæt multitone-/noise-power-ratio-(NPR)-stimulus til linearitetstest af forstærkere og konvertere (en v1 af Signal Studio for Multitone Distortion, N7621B). Parametre: **antal toner** (2 op til 4097), **toneafstand** (Hz), **centeroffset** (Hz), en **fase-preset** — **Parabolic** (Newman, lav PAPR), **Random** eller **Constant** (justeret, høj PAPR) — og en valgfri **NPR-notch** (aktivér, **bredde** Hz, **offset** Hz fra båndcentrum), der rydder et bånd af toner, så du kan måle intermodulation/støj, der falder ned i notchen. Sammensat båndbredde ≈ antal toner × afstand; live-PAPR rapporteres. Spektrumanalysator-assisteret prædistortionskorrektion er endnu ikke implementeret.

### 5.9 Jitter Injection
Et jittret clock-/tonesignal til jitter-tolerancetest af modtagere (en v1 af Signal Studio for Jitter Injection, E4438C-SP1). Jitter påføres som timing-(fase)-modulation af en sinusformet clock, så envelopen forbliver konstant. Parametre: **clock-rate** (Hz, signalet der jittres); **periodisk jitter**-form (**Sinusoidal / Square / Triangle / SawTooth / Exponential** eller None), **rate** (Hz) og **amplitude** (UI peak-to-peak); og valgfri **random** gaussisk jitter (**RMS** i UI + **seed**). Periodisk + random kombineres til et **composite**-signal, og en given seed reproducerer en identisk sekvens (digital reproducerbarhed). Amplituder angives i unit intervals (1 UI = én clock-periode). Foruddefinerede standardmasker (fx ITU-T G.8251) og automatiske SJ-frekvenssweep er endnu ikke implementeret.

### 5.10 GSM/EDGE (GMSK)
En GMSK-moduleret GSM-familiebærebølge (en v1 af Signal Studio for GSM/EDGE, N7602B). GMSK er den konstant-envelope-modulation, GSM/GPRS bruger: databit driver en NRZ-frekvenspuls, gaussisk-filtreret (**BT**, standard 0,3) og integreret til kontinuert fase ved modulationsindeks 0,5. Parametre: **symbolrate** (Hz, GSM = 270,833 ksym/s), **samples pr. symbol**, **antal symboler**, **BT**, gaussisk span og **datakilde** (PN9/15/23, all-ones/zeros). Samplingsrate = symbolrate × samples/symbol. EDGE 8PSK (3π/8-roteret) og fuld burst-/træningssekvens-framing er endnu ikke implementeret.

### 5.11 Bluetooth (GFSK)
En GFSK-moduleret Bluetooth-bærebølge (en v1 af Signal Studio for Bluetooth, N7606B). GFSK er Basic Rate-/LE-modulationen — gaussisk-filtreret FSK (BT 0,5) integreret til kontinuert fase ved et konfigurerbart **modulationsindeks** (BR ≈ 0,32, LE ≈ 0,5). Parametre: **symbolrate** (Hz; BR/LE-1M = 1 M, LE-2M = 2 M), **samples pr. symbol**, **antal symboler**, **modulationsindeks**, **BT** og **datakilde**. Deler CPM-motoren med GMSK (GMSK er GFSK ved indeks 0,5). EDR (π/4-DQPSK / 8DPSK), LE coded PHY, pakke-framing og hopping er endnu ikke implementeret.

### 5.12 3GPP W-CDMA FDD
Et enkeltkode W-CDMA-downlink-lignende signal (en v1 af Signal Studio for 3GPP W-CDMA FDD, N7600B). Datasymboler spredes med en **OVSF**-(Walsh)-kode, kompleks-**scrambles** af en PN-sekvens og root-raised-cosine-formes (**β** = 0,22) ved **3,84 Mcps** chip-raten. Parametre: **chip-rate**, **samples pr. chip**, **antal symboler**, **spredningsfaktor** (potens af to) og **OVSF-indeks**, **modulation** (QPSK…), **RRC β** og **scramble** (aktivér + seed). Samplingsrate = chip-rate × samples/chip. Dette er et repræsentativt enkeltkanalsignal — multikode-downlink (CPICH/P-CCPCH/SCH), slot-/frame-struktur, TFCI, transmit diversity og HSPA-kanaler er endnu ikke implementeret.

### 5.13 3GPP W-CDMA HSPA
Et HS-PDSCH-lignende HSPA-signal (en v1 af Signal Studio for 3GPP W-CDMA HSPA, E4438C-419). Samme DSSS-struktur som §5.12, men med **16QAM** på en **SF-16**-kode som standard — HSPA's definerende træk er højere-ordens modulation på den højhastigheds-delte kanal. Parametre svarer til W-CDMA FDD plus et **modulations**-valg (QPSK / 16QAM / 64QAM). Repræsentativt enkeltkanalsignal — HS-SCCH/HS-DPCCH, E-DCH-kanaler, H-ARQ og TTI-struktur er endnu ikke implementeret.

### 5.14 3GPP2 CDMA (cdma2000)
Et enkeltkode cdma2000-forward-link-lignende signal (en v1 af Signal Studio for 3GPP2 CDMA, N7601B). Samme DSSS-struktur som §5.12 ved **1,2288 Mcps** chip-raten: QPSK-data spredt med en **Walsh**-kode, PN-scramblet, pulsformet (RRC-tilnærmelse af cdma2000-filteret). Parametre svarer til W-CDMA FDD. Repræsentativt enkeltkanalsignal — pilot-/sync-/paging-kanaler, radiokonfigurationer og frame-/PCG-struktur er endnu ikke implementeret, og det eksakte cdma2000-basebåndsfilter tilnærmes af RRC.

### 5.15 TD-SCDMA
Et enkeltkode TD-SCDMA-signal (en v1 af Signal Studio for TD-SCDMA, N7612B) ved **1,28 Mcps** low-chip-rate: QPSK/16QAM/64QAM-data spredt med en OVSF-kode, scramblet, RRC-formet (β = 0,22). Parametre svarer til W-CDMA FDD. Repræsentativt enkeltkanalsignal — 5 ms-subframe/7-timeslot-TDD-burststruktur (DwPTS/UpPTS/GP), midamble-koder, switching points og HSDPA-kanaler er endnu ikke implementeret.

### 5.16 S-DMB (CDM, tilnærmet)
Et **tilnærmet** Satellite-DMB-stimulus (en v1 af Signal Studio for S-DMB, E4438C-407). ⚠️ S-DMB-luftgrænsefladen (System E) kunne ikke bekræftes fra primær litteratur, så dette genererer et repræsentativt **CDM**-signal (QPSK spredt med en OVSF-kode, scramblet, RRC-formet) — **ikke en verificeret S-DMB-bølgeform.** Chip-rate, spredning, FEC og framing er pladsholdere; betragt outputtet som "et QPSK spread-spectrum-signal." Parametre svarer til de øvrige CDMA-familie-personligheder.

### 5.17 3GPP LTE FDD
Et downlink-OFDM-signal med LTE-numerologi (en v1 af Signal Studio for 3GPP LTE, N7624B). Bruger **15 kHz** subcarrier-afstand og den standardiserede **FFT-størrelse / antal brugte subcarriers** for den valgte **kanalbåndbredde** (1,4 / 3 / 5 / 10 / 15 / 20 MHz → 128…2048-punkts FFT, RB×12 subcarriers) med et normalt cyklisk præfiks. Parametre: **båndbredde**, **antal symboler** og subcarrier-**modulation** (QPSK / 16QAM / 64QAM / 256QAM). Bygget på den delte OFDM-motor. Repræsentativt signal — ingen PSS/SSS, referencesignaler, PBCH/PDCCH/PDSCH-mapping, resource-block-scheduling eller 10 ms-rammestruktur.

### 5.18 3GPP LTE TDD
Samme downlink-OFDM-signal som §5.17 (identisk LTE-OFDM-numerologi og parametre) — LTE's fysiske lag er fælles for FDD og TDD (en v1 af Signal Studio for 3GPP LTE TDD, N7625B). De TDD-specifikke dele — DL/UL-subframe-konfigurationer, det specielle subframe (DwPTS/GP/UpPTS) og 10 ms-rammestrukturen — er endnu ikke implementeret.

### 5.19 802.11 WLAN (OFDM)
Et generisk 802.11-OFDM-signal (en v1 af Signal Studio for 802.11 WLAN, N7617B) med 11a/g/n-numerologi: **312,5 kHz** subcarrier-afstand, en **64-punkts** (20 MHz) eller **128-punkts** (40 MHz) FFT med det standardiserede antal brugte subcarriers og cyklisk præfiks. Parametre: **båndbredde**, **antal symboler** og subcarrier-**modulation** (BPSK…256QAM). Bygget på den delte OFDM-motor. Repræsentativt signal — ingen L-STF/L-LTF/L-SIG-præambel, piloter, kodning/interleaving, MIMO eller 80/160 MHz (11ac/ax, som overstiger ESG'en).

### 5.20 802.16-2004 WiMAX (OFDM)
Et fixed-WiMAX-(IEEE 802.16-2004)-**256-FFT-OFDM**-signal (en v1 af Signal Studio for 802.16-2004, N7613A) med 200 brugte subcarriers. Parametre: **kanalbåndbredde** (Hz; samplingsrate ≈ båndbredde × 8/7), **cyklisk-præfiks-forhold** (1/4, 1/8, 1/16, 1/32), **antal symboler** og **modulation** (BPSK…64QAM). Bygget på den delte OFDM-motor. Repræsentativt signal — ingen long/short-præambel, FCH, DL/UL-MAP, DCD/UCD, pilotmønstre eller RS-CC-kodning.

### 5.21 802.16e Mobile WiMAX (OFDMA)
Et mobile-WiMAX-(IEEE 802.16e)-**scalable-OFDMA**-signal (en v1 af Signal Studio for 802.16 WiMAX, N7615B) ved den faste **10,9375 kHz** subcarrier-afstand med en valgbar **FFT-størrelse** (128 / 512 / 1024 / 2048 for ≈ 1,25 / 5 / 10 / 20 MHz), så samplingsraten skalerer med FFT. Parametre: **FFT-størrelse**, **cyklisk-præfiks-forhold**, **antal symboler** og **modulation** (QPSK…64QAM). Modelleret som almindelig OFDM (ingen OFDMA-subkanalpermutation). Repræsentativt signal — ingen PUSC/FUSC/AMC-zoner, præambel, FCH/MAPs, piloter, MIMO eller CTC/CC-kodning.

### 5.22 T-DMB (DAB COFDM)
DAB-COFDM-signalet, der ligger under Terrestrial-DMB (en v1 af Signal Studio for T-DMB, N7616B). Bevarer en **2,048 MHz** signalbåndbredde på tværs af alle fire **transmissionsmodes** (I/II/III/IV), som sætter FFT-størrelsen (2048 / 512 / 256 / 1024), aktive bærebølger og guard-interval. Parametre: **mode**, **antal symboler**, **datakilde**. Modulationen er DQPSK, her tilnærmet med almindelig QPSK. Bygget på den delte OFDM-motor. Repræsentativt signal — ingen null-/fasereferencesymboler, synkroniseringskanal, FIC/MSC-multipleks, differentiel kodning eller foldningskodning.

---

## 6. Impairments

**Impairments**-visningen anvender valgfrie, uafhængigt slåede effekter på den beregnede bølgeform (i rækkefølge), så du kan modellere virkelighedens ufuldkommenheder eller teste korrektion:

- **I/Q-impairments (signalforringelser)** — gain-ubalance, kvadratur-(fase)fejl og DC-offset. (En gain-ubalance producerer en målbar billedtone — se verifikationstutorialerne.)
- **AWGN** — tilføj støj til signalet ved et fastsat niveau.
- **CFR (crest-factor reduction)** — reducér PAPR (peak-windowing/clipping) for at teste eller emulere CFR.
- **Filter** — anvend et yderligere FIR-filter.

Hver har et afkrydsningsfelt til at aktivere den og et property grid til at redigere dens indstillinger. Impairments anvendes under **Calculate**, efter kilden har produceret sin baseband-I/Q.

---

## 7. Pipelinen

Det bevidste tretrins-forløb holder dig i kontrol over, hvad der når hardwaren:

1. **Calculate** — bygger I/Q'en uden for UI-tråden (fremdriftslinje), anvender alle aktiverede impairments og opdaterer derefter de tre plots, kører **validering** (§8) og opdaterer **aflæsningen**. Ingen hardware berøres, så dette er altid sikkert.
2. **Download** — slår ARB'en fra, indkoder I/Q'en til **interleaved 16-bit, two's-complement, big-endian**-samples, indrammer dem som en IEEE-488.2 definite-length-blok og skriver dem til generatorens flygtige **WFM1**-hukommelse. Kræver en forbindelse.
3. **Play / Stop** — **Play** vælger + armerer ARB-segmentet (ved sample clock'en, med runtime-skalering) og tænder RF, og viser afspilningstilstandsindikatoren; **Stop** disarmerer og slukker RF.

**Calc → DL → Play** kører alle tre med ét klik. Downloadstørrelsen og målet vises i aflæsningen / notifications.

---

## 8. Validering (Notifications)

Efter hver Calculate gennemgår **afhængighedstjekkeren** bølgeformen mod den forbundne (eller standard) målprofil for kapabilitet og viser fund i **Notifications** med alvorlighedsgrader (Info / Warning / Error). Den kontrollerer:

- **Minimumssamples** og **granularitet** (ARB'ens længderegler).
- **Hukommelseslofter** — passer bølgeformen ind i den installerede baseband-options sample-hukommelse?
- **Sample-clock- og bærebølgegrænser** — inden for instrumentets område?
- **DAC over-range** — ville samplene clippe ved den valgte skalering?
- **Loop-søm** — vil bølgeformen loope sømløst (heltallig cykluslængde)? En diskontinuitet er en advarsel.

Fejl bør løses før download; verifikationsvejen og assistenten kører begge denne tjekker igen som en sikkerhedsgate før enhver hardwarehandling.

---

## 9. VSA-verifikation (E4406A eller N9010A)

Med en **VSA** på generatorens RF-udgang bliver appen til et closed-loop *generér → mål → sammenlign*-system. Analysatoren **modtager** kun nogensinde RF. To analysatorer understøttes: Agilent **E4406A** og Keysight **N9010A (EXA)**.

**Valg af analysator.** **VSA model**-knappen på handlingslinjen (ved siden af **Connect VSA…**) vælger, hvilken analysator appen målretter — **E4406A** eller **N9010A** — og huskes mellem sessioner. Valget styrer forbindelsesdialogens titel, dens standardgrænseflade og adresse-hint (E4406A bruger som standard GPIB, f.eks. `GPIB0::17::INSTR`; N9010A bruger som standard LAN/USB, f.eks. `TCPIP0::<ip>::hislip0::INSTR`), den modelspecifikke input-skade-standard og identitetskontrollen — det afvises at forbinde et instrument, der ikke matcher den valgte model. N9010A er valideret mod Keysight X-Series-manualerne; E4406A-stien er derudover hardware-valideret. Bekræft N9010A'ens maksimale sikre input mod dens datablad, før du driver effekt (se nedenfor).

> N9010A'en kører periodiske **auto-alignments**, der kan pause en måling i sekunder. Appen venter dem ud
> via en Service-Request-notifikation (SRQ) i stedet for en fast timeout, så en måling, der falder sammen
> med en alignment, fuldføres normalt i stedet for at fejle uberettiget.

### 9.1 Tilslutning af analysatoren (sikkerhed først)
**Connect VSA…** åbner VSA-forbindelsesformularen, som inkluderer **RF-vejens sikkerhedsindstillinger**:

- **Armed** — slå denne til, når analysatoren fysisk er på ESG-udgangen og skal beskyttes.
- **Analyzer max safe input (dBm)** — skadetærsklen, sået fra den valgte model (E4406A type-N-indgang ≈ +35 dBm, standardgate +30 dBm; N9010A +30 dBm / 1 W maks. sikre input iht. dens datablad, 5989-6529EN). Tilsidesæt den for din enhed.
- **Path loss (dB)** — enhver inline-pad/-dæmper mellem ESG'en og analysatoren.

Når armeret, blokerer **power-sikkerhedsgaten** enhver kommanderet ESG-effekt, der ville lægge mere end det sikre niveau på analysatorindgangen (med hensyntagen til path loss). Denne gate beskytter både det manuelle UI og assistenten.

### 9.2 Verify (forventet vs. målt)
**Verify** måler det afspillede signal og sammenligner det med forventningerne, og udfylder **Verification**-tabellen (metrik, forventet, målt, Δ, tolerance, pass/fail, med et sammendrag):

- **Channel power** vs. det kommanderede niveau minus path loss.
- **PAPR** vs. den værdi, der er beregnet ud fra den genererede I/Q.
- **Tone frequency** (for en enkelt tone) vs. bærebølge + offset.

### 9.3 Guide til vejkalibrering
**Path cal…** driver en ren umoduleret bærebølge ved et kendt niveau, måler den på analysatoren og registrerer *kommanderet − målt* som det inline **path loss** — anvendt på både sikkerhedsgaten og Verify, så efterfølgende kørsler er selvkonsistente. RF returneres slukket, når det er færdigt.

### 9.4 Referencelåsning
**Reference**-menuen sætter ESG og analysatoren til **uafhængige** interne tidsbaser eller til en **fælles 10 MHz ekstern** reference (en husreference eller ESG'ens 10 MHz OUT kablet til analysatoren) for rene frekvenssammenligninger. Den rapporterer den resulterende kilde for hvert instrument.

### 9.5 VSA-måletilstand
**VSA Mode**-menuen viser de måletilstande, der faktisk er installeret på enheden (læst live fra analysatorens `:INSTrument:CATalog?`): altid **Basic**, plus alle option-gatede kommunikationsstandard-personligheder (GSM, EDGE, cdmaOne, cdma2000, 1xEV-DO, W-CDMA, NADC, PDC, iDEN). Når du vælger en, skifter analysatorens tilstand; en ikke-installeret tilstand afvises med en besked, der viser hvad der *er* installeret (se §9.8).

### 9.6 Målinger
Under motorhjelmen leverer appen typede VSA-målinger (også eksponeret for assistenten, §10): **Channel Power**, **ACP/ACPR**, **CCDF / PAPR** (Power Statistics), **Spectrum**-markør (tonefrekvens/-effekt, optaget BW), **Waveform** (tidsdomæne-peak/-mean/-peak-to-mean) og **Power-vs-Time** med en konfigurerbar **power mask** (pass/fail over tidsvinduer) for burst-signaler.

### 9.7 Selvtest af installationen
**Verify install…** kører et kort, guidet **generér → afspil → mål → sammenlign**-batteri, der beviser, at
hele installationen og konfigurationen virker fra ende til ende, på tværs af signaltyper frem for én. Det
syntetiserer fire signaler som ARB-I/Q og afspiller hvert gennem ESG'en, mens hvert måles på den forbundne
analysator:

1. **CW** — en umoduleret tone; tjekker kanaleffekt, PAPR (≈ 0 dB) og tonefrekvens (bærebølge + offset).
2. **AM** — 50% ved 100 kHz; den forhøjede PAPR fingeraftrykker amplitude-stien.
3. **FM** — 500 kHz deviation ved 100 kHz; den konstante-indhylningskurve-PAPR (≈ 0 dB) fingeraftrykker frekvens-stien.
4. **I/Q-multitone** — et 4-tone Newman-signal; kanaleffekt + PAPR for den fulde komplekse sti.

Resultaterne vises i **Verification**-visningen (forventet vs. målt pr. trin) med et samlet **PASS/FAIL**.
Det kræver en baseband-kapabel ESG og en forbundet analysator; **input-skade-sikkerheds-gaten** håndhæves
før enhver RF, og RF returneres slukket, når det er færdigt. AM/FM verificeres via effekt/PAPR (ikke analog demodulation).
Ved **FAIL** viser en **fejlfindingsdialog** hvert fejlet tjeks sandsynlige årsag og trin i rækkefølge
(f.eks. for kraftig AM → overdrevet ESG eller forkert VSA-aflæsning → sænk niveauet / tjek ARB-skalering / kør Path cal… igen).

For at køre det *samme* batteri **manuelt** — hvor du selv aflæser analysatoren, med hver UI-kontrol,
værdi og forventet aflæsning specificeret og en selvstændig **tjekliste for VSA-indstillinger** — følg
[**Manuel verifikations-procedure**](ManualVerification.md).

### 9.8 Kapabilitetsbinding — Core vs. option-gatet
Appen binder til det, den **forbundne enhed faktisk rapporterer**, ikke til en fast modelkonfiguration, så
den aldrig tilbyder en personlighed, hardwaren ikke kan køre, eller accepterer en indstilling, instrumentet
lydløst ville afvise. Ved tilslutning læser den `*IDN?`, `*OPT?` og de live `? MAX/MIN`-grænser og forliger
dem med den statiske profil (den *effektive profil*). To niveauer:

- **Core (altid til stede).** Funktioner tilgængelige på enhver understøttet enhed uanset options:
  - *ESG (E4438C):* ARB-bølgeform-download & -afspilning, frekvens/amplitude/RF-output-styring,
    referencelåsning. (Selve baseband-ARB kræver en installeret baseband-generator-option — se nedenfor.)
  - *Analysator (E4406A):* **Basic**-måletilstand. *(N9010A):* **SA**- og **IQ Analyzer**-tilstande.
    Channel Power, CCDF/PAPR, Spectrum-markør og Waveform-målinger kører i disse core-tilstande.
- **Option-gatet (kun til stede hvis enheden rapporterer optionen).**
  - *ESG:* baseband-generator / ARB-hukommelsesdybde — de forligede lofter for **sample-antal** og
    **sample-clock** afspejler kun de baseband-options, `*OPT?` faktisk rapporterer, og download-stien
    læser `*OPC?` + `:SYSTem:ERRor?` tilbage, så en afvist bølgeform bliver synlig, ikke antaget indlæst.
  - *Analysator:* kommunikationsstandard-personligheder (GSM, EDGE, cdmaOne, cdma2000, 1xEV-DO, W-CDMA,
    NADC, PDC, iDEN). Disse vises kun i **VSA Mode**-menuen, når de er installeret.

Hvis du vælger en tilstand, der ikke er installeret, afviser appen den med en klar besked (der navngiver de
installerede tilstande) frem for at forlade sig på en lydløs afvisning fra instrumentets side. Tilslutning
af en model, appen ikke understøtter (andet end den valgte E4406A/N9010A-analysator eller E4438C-ESG'en),
afvises ved tilslutning.

---

## 10. Claude-assistenten

**Assistant**-visningen er en tilvalgsrude, hvor Claude driver appen i naturligt sprog gennem en *værktøjsflade* — aldrig syntetiske klik eller skjult SCPI. Den er **slukket, indtil du aktiverer den** og angiver en API-nøgle.

### 10.1 Ruden
- En **transkription** af dine beskeder og Claudes svar (streamet live).
- En **inputboks** med **Send** og **Stop** (Stop annullerer den igangværende tur).
- En **indstillingsstribe**: **Enable assistant** (hovedafbryder), **Auto-approve hardware**, **Allow raw SCPI** og **Set API key…**.
- **Inline-bekræftelseskort** vises i transkriptionen, hver gang Claude vil gøre noget, der berører instrumentet — med handlingen, dens parametre og **Approve / Decline**.

### 10.2 Hvad den kan gøre (værktøjer)
- **Read** (kører frit): `get_app_state`, `list_personalities`, `get_current_config`, `get_validation_results`, `get_results_readout`.
- **Configure** (kun projekt-/pc-tilstand): `set_source_personality`, `configure_cw`, `configure_multitone`, `configure_custom_modulation`, `configure_awgn`, `configure_import_iq`, `select_plot_view`, `set_project`, `calculate_waveform`.
- **Hardware** (hver bag bekræftelse): `connect_instrument`, `disconnect_instrument`, `download_waveform`, `play_rf`, `stop_rf`, `set_instrument_settings`.
- **Measure / verify** (læs analysatoren): `get_vsa_state`, `measure_channel_power`, `measure_acp`, `measure_ccdf`, `measure_spectrum_peak`, `measure_waveform`, `verify_signal`.
- **Gated** (tilvalg): `send_raw_scpi` — en avanceret nødudgang, deaktiveret indtil du afkrydser **Allow raw SCPI**, og altid bekræftet pr. kald.

### 10.3 Beskyttelsesmekanismer (sikkerhed)
Håndhævet i dispatcheren, ikke i prompten:

- **read / configure** kører uden prompt; **hardware / destructive** kræver et **Approve/Decline**-kort. **Auto-approve hardware** kan springe prompten over for almindelige hardwareværktøjer, men **`play_rf`, `connect_instrument` og `send_raw_scpi` bekræfter altid** (RF-emission, busovertagelse, rå kommandoer).
- En **pre-execution valideringsgate** kører afhængighedstjekkeren igen før `download_waveform` / `play_rf` og **nægter ved en hård valideringsfejl — selv hvis du har godkendt**.
- Kommanderet effekt går gennem **input-damage-sikkerhedsgaten** (§9.1).
- **Instruktionskilde-grænse**: alt der returneres *fra* et værktøj (en fils indhold, et instrumentsvar) behandles som **data, aldrig som kommandoer** — Claude handler ikke på instruktioner skjult i værktøjsoutput.
- **API-nøglen** lagres krypteret med **Windows DPAPI** (per-bruger) og skrives aldrig til projekter, logs eller request-body'en. Massedata (f.eks. rå I/Q-arrays) minimeres, før de sendes.

Læsninger udstedt i én tur kører **samtidigt**; configure-/hardware-trin forbliver **serialiserede** i rækkefølge; lange samtaler **komprimeres** automatisk.

---

## 11. Sekvensering, projekter, eksporter

- **Sequence**-visning — sammensæt flere bølgeforms-**segmenter** til en sekvens (tabel- + script-visninger), med undersekvenser og batch-compile, til afspilning af flere segmenter.
- **Projekter** — **Save… / Open…** persisterer den aktive kilde + indstillinger som en `*.ssproj` JSON-fil.
- **Eksporter** — eksportér bølgeformen som rå ARB-bytes, CSV eller et SCPI-script; og brug indbyggede **test-model-presets** som udgangspunkter.
- **Markører** — ARB-markører understøttes til triggering/segmentering.

---

## 12. SCPI console

**SCPI console** sender rå SCPI til det forbundne instrument og viser en tidsstemplet request/response-log — nyttig til ad hoc-kommandoer og fejlfinding. (Assistentens tilsvarende, `send_raw_scpi`, er gatet og bekræftet; se §10.)

---

## 13. Headless hardware-in-the-loop-harness

`ESG-SignalCreator.HilHarness.exe` er en konsol-runner til automatiserede hardwaretests (CI / bench-regression), adskilt fra GUI'en:

```
# ESG-only: connect, *IDN?/*OPT?, download a CW, arm the ARB, read back (RF off, safe)
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"

# Closed-loop battery across signal types + a frequency sweep, with a JSON report
ESG-SignalCreator.HilHarness.exe --vsa GPIB0::17::INSTR --all --dwell-seconds 3 --json report.json

# A single signal type, or a flatness power sweep
ESG-SignalCreator.HilHarness.exe --vsa --signal multitone
ESG-SignalCreator.HilHarness.exe --vsa --flatness
```

Den håndhæver input-damage-sikkerhedsgaten, holder analysatoren kørende under dwell, så frontpanelet følger live, afslutter med ikke-nul ved fejl og kan udsende en maskinlæsbar JSON-rapport.

---

## 14. Fejlfinding

- **"Connect the ESG first" / Offline** — forbind via **Connect…** før Download/Play/Verify.
- **Open mislykkes / ressource ikke fundet** — bekræft at en VISA-runtime er installeret, og at ressourcestrengen er korrekt (prøv SCPI console'ens discovery eller forbindelsesmanagerens Find).
- **Download/Play deaktiveret** — du har brug for en beregnet bølgeform *og* en forbindelse; tjek Notifications for valideringsfejl.
- **Valideringsfejl (memory/min-samples/over-range)** — justér længde, sample clock eller skalering; meddelelsen navngiver grænsen. Hardwarehandlinger nægtes, mens en hård fejl består.
- **Verify mislykkes på power** — kør **Path cal…**, så path loss opfanges, og bekræft tolerancer i verifikationsprofilen.
- **Assistenten siger den er deaktiveret / ingen nøgle** — afkryds **Enable assistant** og **Set API key…**.
- **Assistenten vil ikke røre hardware** — det er efter hensigten; godkend inline-kortet. Hvis en hardwarehandling *nægtes* på trods af godkendelse, blokerer en valideringsfejl den — ret den (se Notifications).
- **CI-release i kø for evigt** — release-workflowet kræver en selvhostet Windows-runner med en VISA-provider installeret (se [Packaging.md](../Packaging.md)).

---

## 15. Sikkerhedsnoter

- Analysatoren modtager kun nogensinde RF. **Armér** RF-vejens sikkerhed og indstil **max safe input** + **path loss**, før du driver effekt ind i den; gaten blokerer derefter usikre niveauer.
- **Play tænder RF.** Brug **Stop** til at slukke det. Assistenten bekræfter altid `play_rf`.
- Behandl **rå SCPI** (console eller `send_raw_scpi`) som den avancerede, fuldt loggede nødudgang — den kan gøre alt, hvad instrumentet tillader.

---

*Se [Tutorials.md](Tutorials.md) for praktiske, opbyggende gennemgange af hver funktion.*
