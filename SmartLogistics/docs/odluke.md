# Zapis odluka

Sve pretpostavke i odluke projekta na jednom mjestu. Detaljno obrazloženje domenskih
odluka (moguća tumačenja, utjecaj na rezultat) nalazi se u `docs/spec.md`, odjeljak 2.

Statusi: **Potvrđeno** (odlučio tehnički vođa) · **CLAUDE.md** (zadano pravilima projekta) ·
**Prijedlog** (čeka odluku).

## Domenske odluke

| ID | Pitanje | Odluka | Pravilo | Status | Datum |
|----|---------|--------|---------|--------|-------|
| D1 | "9 h vožnje": samo vožnja ili vožnja + radnje? | Zadano `WorkdayMode.Total` (vožnja + radnje <= 540 min), alternativa `WorkdayMode.DrivingOnly` (samo vožnja <= 540 min). | R20, R21, R22 | CLAUDE.md | 20.11.2026. |
| D2 | Na koga se odnosi 540 min i što obuhvaća? | Po kamionu, od polaska iz L0 do završnog dolaska u L0, uključujući sve radnje (i one u bazi). | R29 | Potvrđeno | 20.11.2026. |
| D3 | Jedan dan ili više? | Jedan dan; nalog koji ne stane u dan je neizvediv, ne prenosi se. | R30 | Potvrđeno | 20.11.2026. |
| D4 | Usputni prolaz kroz bazu, više tura? | Jedna neprekinuta ruta; L0 usred rute samo kao mjesto radnje (N04, N05), ne počinje novi dan; prazni međuposjeti zabranjeni (zbog zaokruživanja dionica mogli bi umjetno skratiti put). | R31 | Potvrđeno | 20.11.2026. |
| D5 | Smije li se izvediv nalog odbaciti radi manje km? | Ne smije; inače bi prazan plan s 0 km bio "optimalan". | R32 | Potvrđeno | 20.11.2026. |
| D6 | Kriterij kad nisu svi nalozi izvedivi? | Najviše izvršenih naloga, zatim najmanje km, deterministički među jednakima. | R32, R39 | Potvrđeno | 20.11.2026. |
| D7 | Kako prijaviti razlog neizvedivosti? | Kategorije s brojkama: K1 masa veća od svih kamiona, K2 samostalna ruta dulja od radnog dana, K3 ne stane uz ostale naloge, K4 neispravni podaci naloga. | R33 | Potvrđeno | 20.11.2026. |
| D8 | Početno stanje, vremenski prozori? | Kamioni kreću prazni, strojevi dostupni cijeli dan, nema vremenskih prozora. | R34 | Potvrđeno | 20.11.2026. |
| D9 | Kapacitet nakon radnje, uključiva granica? | Nakon svake radnje, <= (npr. M06 + M08 = 10000 kg na B je dopušteno). | R11, R12 | CLAUDE.md | 20.11.2026. |
| D10 | 20 min po radnji ili po zaustavljanju? | Po radnji; dvije radnje na istoj lokaciji traju 40 min. | R18 | CLAUDE.md | 20.11.2026. |
| D11 | Radnje u bazi, je li povratak radnja? | Radnja u L0 traje 20 min; polazak i povratak nisu radnje. | R18, R19 | CLAUDE.md | 20.11.2026. |
| D12 | Aritmetika vremena? | Decimalne minute bez zaokruživanja; <= 540 uključivo s tolerancijom; hh:mm samo za prikaz. | R35 | Potvrđeno | 20.11.2026. |
| D13 | Prikaz neiskorištenog kamiona? | Oznaka "neiskorišten", 0 km, 0 naloga, bez dionice L0 -> L0. | R36 | Potvrđeno | 20.11.2026. |
| D14 | Neispravni ulazni podaci? | Globalne greške odbijaju cijeli ulaz; greške pojedinog naloga čine samo taj nalog neizvedivim (K4). | R37 | Potvrđeno | 20.11.2026. |
| D15 | Egzaktni optimum ili heuristika? | Egzaktni optimum za ovu veličinu problema; heuristika samo uz jasnu napomenu u UI-ju. | R38 | Potvrđeno | 20.11.2026. |
| D16 | Izbor među planovima jednakih km? | Deterministički: isti ulaz uvijek daje isti plan. | R39 | Potvrđeno | 20.11.2026. |

## Tehničke odluke

| ID | Odluka | Razlog | Status | Datum |
|----|--------|--------|--------|-------|
| T1 | Ciljna platforma .NET 10 (LTS) umjesto .NET 8. | Na računalu su samo SDK 10 i 11; .NET 10 je LTS. | Potvrđeno | 20.11.2026. |
| T2 | Rješenje u formatu `.slnx`. | Zadani format SDK-a 10+; `.sln` je trebao samo radi .NET 8. | Potvrđeno | 20.11.2026. |
| T3 | Web prikazuje `docs/*.md` vlastitim minimalnim Markdown rendererom (`Web/Services/MarkdownRenderer.cs`), bez NuGet paketa. | Dodavanje paketa Markdig odbijeno je u alatu; renderer HTML-kodira sav tekst. Zamjena za Markdig je jedan poziv. | Prijedlog | 20.11.2026. |
| T4 | Zajednički model (ulazni podaci, tip plana, `WorkdayMode`, ugovor `IRoutePlanner`) u `Core/Domain/`; implementacija planera u `Core/Planning/`, validator u `Core/Validation/`. | Validator ne smije čitati `Core/Planning/`, pa ugovor i tipovi plana ne smiju biti tamo. | Prijedlog | 20.11.2026. |
| T5 | Zabrana čitanja `Core/Planning/` za validator-engineer provodi se uputom u agentu, ne alatom. | Polje `tools` u frontmatteru ograničava alate, ne putanje; tvrda zabrana bi tražila hook. | Prijedlog | 20.11.2026. |
