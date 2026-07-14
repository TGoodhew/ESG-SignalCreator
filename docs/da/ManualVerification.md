# ESG-SignalCreator — Procedure for manuel verifikation

> 🌐 **Dansk** (denne side) · English: [ManualVerification](../ManualVerification.md)

En trin-for-trin **bænkprocedure** til manuelt at verificere, at en ESG-SignalCreator-installation — 
**E4438C**-generatoren plus en **VSA** (Agilent **E4406A** eller Keysight **N9010A/EXA**) — er kablet, 
konfigureret og fungerer fra ende til ende. Hvert trin angiver den **præcise UI-kontrol**, der skal bruges, den **præcise værdi** 
der skal indtastes, og **hvad du bør se på analysatoren**.

Dette er den manuelle ledsager til selvtesten **Verify install…** med ét klik ([UserGuide §9.7](UserGuide.md#97-install-verification-self-test)): 
den kører det *samme* CW → AM → FM → I/Q-batteri og forventer de *samme* aflæsninger, men du udfører hvert trin 
selv og aflæser analysatoren direkte. Brug den til at idriftsætte en ny bænk, til at lokalisere en fejl, som den 
automatiske selvtest rapporterer, eller til at lære, hvordan "godt" ser ud på analysatoren.

> **Sikkerhed først.** Analysatoren *modtager* kun nogensinde RF. Før du driver nogen effekt, skal du bevæbne porten mod 
> indgangsskade (Trin 2) og læse [UserGuide §15 — Sikkerhedsbemærkninger](UserGuide.md#15-safety-notes). Alle effektværdier 
> nedenfor forudsætter **0 dB banetab**; hvis du har en inline-pad/dæmpeled, skal du trække den fra de forventede 
> analysatoraflæsninger (eller endnu bedre, indfang den med **Path cal…**, Trin 3).

---

## Referenceværdier brugt hele vejen igennem

Dette er de standardværdier, som den automatiske **Verify install…** bruger, så den manuelle kørsel matcher den nøjagtigt.

| Parameter | Værdi | Hvor den kommer fra |
|---|---|---|
| Bærefrekvens | **1 GHz** (1 000 000 000 Hz) | ESG **Instrument settings → Frequency** |
| Kommanderet ESG-effekt | **−10 dBm** | ESG **Instrument settings → Amplitude** |
| ARB-samplingsklokke | **10 MHz** | sættes automatisk ved **Play** |
| Analysatorspan | **5 MHz** | VSA-opsætning (Trin 1) |
| Toneoffset (CW/AM-underbærer) | **+1 MHz** → tone ved **1.001 GHz** | indbygget i signalerne |
| Indsvingningstid før aflæsning | **~3 s** | lad ALC'en re-niveauere pr. bølgeform |
| Banetab | **0 dB** (justér for din kabling) | RF-bane-sikkerhed / Path cal |

**Bestå-tolerancer** (samme som den automatiske selvtest):

| Metrik | Tolerance |
|---|---|
| Channel power | **± 3 dB** |
| PAPR (crestfaktor) | **± 2.5 dB** |
| Tonefrekvens | **± 50 kHz** |

---

## Tjekliste for VSA-indstillinger

Indstil disse på analysatoren **før** du begynder (eller bekræft dem, hvis appen allerede har drevet 
analysatoren). Appen sætter målemodus og center/span automatisk, når den måler, men for 
**at se signalet live** på frontpanelet — og for en manuel aflæsning — konfigurér analysatoren 
sådan her:

| Indstilling | E4406A | N9010A / EXA | Bemærkninger |
|---|---|---|---|
| **Målemodus** | **Basic** | **SA** (Spectrum Analyzer) til Channel Power / CCDF; **IQ Analyzer** til Spectrum/Waveform | Appen vælger den rigtige modus pr. måling; sæt Basic/SA for en manuel aflæsning. |
| **Centerfrekvens** | **1 GHz** | **1 GHz** | Bæreren. |
| **Span** | **5 MHz** | **5 MHz** | Bredt nok til at se bæreren og +1 MHz-tonen sammen. |
| **Referenceniveau** | **≈ 0 dBm** (≥ 10 dB over −10 dBm) | **≈ 0 dBm** | Headroom over −10 dBm-signalet; undgår indgangsoverbelastning og klipning. |
| **Indgangsdæmpning** | **Auto** (eller ≥ 10 dB) | **Auto** | Manuel kun hvis du kender niveauet; aldrig under signalet. |
| **Referenceoscillator** | **Internal**, eller **External 10 MHz** | **Internal**, eller **External 10 MHz** | En **fælles 10 MHz** (husreference, eller ESG 10 MHz OUT → analysator) strammer frekvensoverensstemmelsen. |
| **Sweep / trigger** | **Continuous** for at se, **Single** for at aflæse | **Continuous** / **Single** | Appen bruger single (`:INIT:CONT OFF`) for en indsvingen aflæsning; continuous viser live. |
| **RBW / VBW** | **Auto** | **Auto** | Auto-kobling er fint til disse tjek. |

> **Skadesgrænser.** Sæt porten mod indgangsskade ud fra modellen: **E4406A** type-N-indgang ≈ +35 dBm 
> (porten er som standard +30 dBm); **N9010A** et konservativt **+25 dBm** (bekræft mod dens datablad). 
> Ved −10 dBm kommanderet er du langt under begge, men bevæbn altid porten alligevel (Trin 2).

---

## Fremgangsmåde

### Trin 1 — Kabl og opsæt analysatoren
1. Kabl **ESG RF OUTPUT** til **analysatorens RF INPUT** (gennem din pad/dæmpeled hvis nogen). 
   Analysatoren modtager kun.
2. Anvend på analysatoren **tjeklisten for VSA-indstillinger** ovenfor: modus, **center 1 GHz**, **span 5 MHz**, 
   **ref-niveau ≈ 0 dBm**, referenceoscillator, continuous sweep.

### Trin 2 — Forbind begge instrumenter og bevæbn sikkerhed (i appen)
1. Klik på **Connect…** (øverste værktøjslinje). Indtast ESG'ens VISA-ressource (f.eks. `TCPIP0::192.168.1.82::inst1::INSTR` 
   eller `GPIB0::19::INSTR`) og forbind. Statuslinjen viser **Online** og modellen.
2. Sæt **VSA model**-omskifteren (ved siden af **Connect VSA…**) til **E4406A** eller **N9010A**.
3. Klik på **Connect VSA…**. Indtast analysatorens VISA-ressource (E4406A f.eks. `GPIB0::17::INSTR`; N9010A 
   f.eks. `TCPIP0::<ip>::hislip0::INSTR`) og forbind. Appen afviser et instrument, der ikke matcher 
   den valgte model.
4. I **RF-path safety**-indstillingerne:
   - **Armed** → **on**.
   - **Analyzer max safe input (dBm)** → lad modellens standard stå (**+30** E4406A / **+25** N9010A).
   - **Path loss (dB)** → dit inline-tab, eller **0** hvis direkte kablet.

### Trin 3 — (Anbefalet) Indfang banetab
1. Klik på **Path cal…**. Lad guiden drive en ren bærer og måle den; den registrerer 
   *kommanderet − målt* som **banetab** og anvender det på sikkerhedsporten og på Verify.
2. Afslut guiden (RF vender tilbage til **off**). Hvis du springer dette over, hold **banetab = 0** og træk mentalt 
   dit kabeltab fra hver forventet aflæsning nedenfor.

### Trin 4 — CW-tone (frekvens-/niveau-referencen)
1. Vælg **Source** (venstre træ) → **CW / Single tone**.
2. Sæt **Frequency offset** = **1 000 000 Hz** (1 MHz), **Amplitude** = **0 dBFS**, **Phase** = **0°**.
3. Klik på **Calculate**. I **resultataflæsningen**, bekræft **PAPR ≈ 0 dB**.
4. Vælg **Instrument settings**; sæt **Frequency = 1 GHz**, **Amplitude = −10 dBm**. Bekræft, at RF og 
   modulation er aktiveret til ARB-afspilning.
5. Klik på **Download**, derefter **Play**. **Afspilningstilstandsindikatoren** når **Playing**.
6. Vent **~3 s**, og aflæs derefter analysatoren.

**Hvad du bør se på VSA'en:**
- **Spectrum:** en enkelt skarp linje ved **1.001 GHz** (bærer + 1 MHz), ingen andre væsentlige toner.
- **Marker / Channel Power:** **≈ −10 dBm** (± 3 dB, minus dit banetab).
- **Tonefrekvens:** **1.001 GHz** (± 50 kHz — strammere med en fælles 10 MHz-reference).
- **CCDF / PAPR:** **≈ 0 dB** (± 2.5 dB) — en ren tone har praktisk talt ingen crest.

### Trin 5 — AM (amplitudebanen)
1. Det automatiske batteri bruger **50% AM ved 100 kHz på en +1 MHz-underbærer**. For at reproducere manuelt, 
   brug enten **Verify install…** (som bygger den for dig) eller sæt **Source → AM** med **depth 50%**, 
   **rate 100 kHz** og tilføj et **+1 MHz**-offset. Hold **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, vent ~3 s, aflæs analysatoren.

**Hvad du bør se på VSA'en:**
- **Spectrum:** en bærer ved **1.001 GHz** med **AM-sidebånd ved ± 100 kHz** omkring den.
- **Channel Power:** **≈ −13 dBm** (± 3 dB) — omkring **3 dB** under CW, fordi den peak-normaliserede ARB 
  lægger AM-cresten under det kommanderede niveau.
- **CCDF / PAPR:** **≈ 3 dB** (± 2.5 dB) — AM-envelopens crest.

> Hvis AM kommer ud **~60 dB lavt**, afspilles basisbåndet som rå ren-reel AM (`I = 1 + m·sin`, 
> `Q = 0`) med et stort DC-led, som E4438C ARB'en ikke vil reproducere på niveau — brug +1 MHz-underbærerformen 
> (hvilket er, hvad **Verify install…** gør).

### Trin 6 — FM (frekvens-/fasebanen)
1. Brug **Verify install…**'s FM (**500 kHz deviation ved 100 kHz**), eller **Source → FM** med disse 
   værdier. Hold **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, vent ~3 s, aflæs analysatoren.

**Hvad du bør se på VSA'en:**
- **Spectrum:** et **bredbånds-FM**-spektrum groft **± 500 kHz** omkring **1 GHz** (Bessel-sidebånd), 
  konstant envelope.
- **Channel Power:** **≈ −10 dBm** (± 3 dB) — samme som CW; FM er konstant-envelope.
- **CCDF / PAPR:** **≈ 0 dB** (± 2.5 dB) — konstant envelope, ingen crest.

### Trin 7 — I/Q-multitone (den fulde komplekse bane)
1. Brug **Verify install…**'s multitone (**4-tone Newman, 1 MHz afstand**), eller **Source → Multitone** 
   med **4 toner**, **1 MHz afstand**, **Newman**-fasning. Hold **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, vent ~3 s, aflæs analysatoren.

**Hvad du bør se på VSA'en:**
- **Spectrum:** **fire ligeligt fordelte toner**, 1 MHz fra hinanden, centreret på 1 GHz.
- **Channel Power:** **≈ −13 til −14 dBm** (± 3 dB) — under CW med multitone-cresten.
- **CCDF / PAPR:** **≈ 3.5–4 dB** (± 2.5 dB) — Newman-fasning holder cresten lav for 4 toner.

### Trin 8 — Stop og registrér
1. Klik på **Stop** — ARB'en afvæbnes og **RF slukker**.
2. Registrér hver metrik mod dens forventede værdi og tolerance. Alle fire signaler inden for tolerance = 
   installationen og konfigurationen er verificeret fra ende til ende (umoduleret → amplitude → frekvens → kompleks I/Q).

---

## Oversigt over forventede aflæsninger

| Signal | Analysatorcenter | Channel power (±3 dB) | PAPR (±2.5 dB) | Bemærkelsesværdigt spektrum |
|---|---|---|---|---|
| **CW** | 1 GHz | ≈ −10 dBm | ≈ 0 dB | én linje ved 1.001 GHz |
| **AM** | 1 GHz | ≈ −13 dBm | ≈ 3 dB | bærer + ±100 kHz sidebånd |
| **FM** | 1 GHz | ≈ −10 dBm | ≈ 0 dB | ±500 kHz Bessel-spektrum |
| **I/Q-multitone** | 1 GHz | ≈ −13…−14 dBm | ≈ 3.5–4 dB | 4 toner, 1 MHz fra hinanden |

*(Alle channel-power-tal forudsætter 0 dB banetab og et −10 dBm kommanderet niveau; træk dit banetab fra.)*

---

## Optagelse af skærmbilleder til dokumentationen

Hvert trin ovenfor har en "hvad du bør se på VSA'en"-beskrivelse. Du kan vedhæfte et rigtigt skærmbillede af
hvert analysatorresultat, optaget over VISA.

**Automatiseret (anbefalet) — én kommando, ingen manuel opsætning.** Dette driver hele CW/AM/FM/I-Q-batteriet,
måler hvert signal og optager analysatorens display efter hvert af dem:

```powershell
ESG-SignalCreator.HilHarness.exe --install-verify --vsa GPIB0::17::INSTR --vsa-model e4406a ^
    --capture-dir docs/images/vsa
```

Den skriver `cw`-, `am`-, `fm`-, `iq-multitone`-billeder (PNG på N9010A, GIF på E4406A) i mappen,
plus en `index.md`, der indlejrer dem — klar til at indsætte i disse trin.

**Ad-hoc — optag kun den aktuelle skærm** (kun analysator, ingen ESG/RF; sæt selv signalet op først):

```powershell
ESG-SignalCreator.HilHarness.exe --capture-screen docs/images/vsa/cw-result.png ^
    --vsa GPIB0::17::INSTR --vsa-model e4406a
```

Referér til et optaget billede fra et trin med f.eks. `![CW-resultat på analysatoren](images/vsa/cw.png)`.

> Standard-optage-SCPI'en (`:MMEMory:STORe:SCReen` + `:MMEMory:DATA?` + `:MMEMory:DELete`) er
> manuelt afledt og **kræver bænkbekræftelse**. Hvis din firmware afviger, kan du tilsidesætte den uden en
> genbygning: `--capture-save-cmd`, `--capture-data-query`, `--capture-cleanup-cmd`, `--capture-temp-path`
> (hver `*-cmd`/`*-query` tager instrumentsidens sti som `{0}`).

---

## Fejlfinding

| Symptom | Sandsynlig årsag | Løsning |
|---|---|---|
| **Alle niveauer ~banetab-lave** | Uindfanget kabel-/pad-tab | Kør **Path cal…** (Trin 3) eller sæt banetab. |
| **Alt ~40–48 dB lavt, PAPR enormt** | På en N9010A returnerer CCDF et 5001-punkts-spor, ikke skalarer | Rettet i appen (PAPR fra spor); opdatér til den seneste udgivelse. |
| **AM-bærer ~60 dB lav** | Rå ren-reel AM-basisbånd med DC | Brug **+1 MHz-underbærer**-AM (hvad **Verify install…** bygger). |
| **Multitone channel power periodisk lav** | Aflæst før ALC'en re-niveauerede | Øg indsvingning til ~3 s og aflæs igen. |
| **Tonefrekvens forskudt med > 50 kHz** | Uafhængige tidsbaser | Lås en **fælles 10 MHz**-reference (**Reference**-knap). |
| **Advarsel om indgangsoverbelastning** | Ref-niveau / dæmpning for lav | Hæv analysatorens referenceniveau; hold dæmpning ≥ signal. |
| **Effektkommando afvist** | Ville overskride analysatorens sikre indgang | Sænk niveauet eller angiv mere banetab — porten beskytter frontenden. |

For den automatiske ækvivalent og en vejledningsdialog ved FAIL-tidspunkt, se 
[UserGuide §9.7](UserGuide.md#97-install-verification-self-test); for den praktiske tutorial, se 
[Tutorials — Del F](Tutorials.md#part-f--vsa-verification-e4406a--n9010a).
