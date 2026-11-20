# ExactDpPlanner – kako radi

Objašnjenje planera za prezentaciju: tok izvršavanja, stanje dinamičkog programiranja, zašto je
rezultat optimalan, složenost, izmjerene granice upotrebljivosti i tri mjesta u kodu na kojima bi
greška najlakše promijenila rezultat. Brojevi redaka odnose se na stanje koda 21.11.2026.

## Ukratko

Planer **isproba svaku moguću podjelu naloga između kamiona**. Za svaki kamion i njegov skup naloga
**dinamičkim programiranjem** nađe najkraću rutu, a zatim od podjela u kojima nijedan kamion ne
prekoračuje radni dan izabere onu s najmanje ukupnih km.

Datoteke: `Core/Planning/ExactDpPlanner.cs` (pretraga raspodjela i izgradnja plana),
`Core/Planning/RouteDp.cs` (DP za jedan kamion), `Core/Planning/PreparedScenario.cs` (priprema ulaza).

## 1. Tok izvršavanja

Ulazna točka: `ExactDpPlanner.SolveWithStatistics` (`ExactDpPlanner.cs:72`).

1. **Provjera ulaza** – `Feasibility.Check` (`Core/Feasibility.cs`, od faze 5) i priprema ulaza
   (`PreparedScenario`):
   - **neispravni podaci nikad ne zaustavljaju planiranje** (izmijenjeni R37, odluka T31): neupotrebljivi
     kamioni (ponovljeni ID, nosivost ili broj strojeva ≤ 0) se ne koriste, a nalozi se razvrstavaju:
     K4 (neispravni podaci – nepostojeći stroj ili lokacija, preuzimanje == isporuka, masa ≤ 0,
     duplikat naloga, …) → K1 (pretežak za sve upotrebljive kamione) → K2 (ni sam ne stane u radni
     dan) → ostali su "planirljivi";
   - `ArgumentException` baca se samo za neispravne `PlanningOptions` (npr. brzina ≤ 0) – to je
     greška programa, ne ulaznih podataka;
   - poruke neizvedivosti su na hrvatskom (R40), npr. "Stroj M11 (11000 kg) teži je od nosivosti
     svih kamiona (A 3500 kg, B 10000 kg).";
   - matrica dionica u **cijelim desetinkama km**: 8,5 km → 85.
2. **Pretraga raspodjela** – `AssignmentSearch.Run` (`ExactDpPlanner.cs:236`). Za svaki nalog popis
   kamiona koji ga mogu nositi (N01–N05: A ili B; N06–N10: samo B). Petlja radi kao **brojčanik**
   (retci 268–280): zadnji nalog se mijenja najbrže. Ukupno 2^5 = **32 raspodjele**.
3. **Ocjena jedne raspodjele** – `EvaluateAssignment` (redak 289):
   - raspodjela → bitmaska naloga po kamionu;
   - za svaki kamion `Evaluate(kamion, maska)` (redak 205) pokreće DP i **pamti rezultat po paru
     (kamion, skup naloga)**, jer se isti skup pojavljuje u više raspodjela – zato su za 32 raspodjele
     dovoljna 63 DP-a;
   - ako ijedan kamion prekoračuje radni dan, raspodjela otpada (redak 306);
   - bolja je raspodjela s više isplaniranih naloga, pa s manje km; pobjeđuje samo strogo bolja (redak 315).
4. **Rezervni put** – ako nijedna raspodjela svih naloga nije izvediva, pretraga se ponavlja s opcijom
   "neraspoređen" (odluka T23). Cilj je najviše isplaniranih naloga, a zatim najmanje km – to je isto
   što i **najmanji broj izostavljenih naloga** (prvo 1, pa 2 …), među jednakima najmanje km.
   Izostavljeni nalozi su K3 s porukom "Ne stane u radni dan …". Primjer: radni dan 300 min (Total)
   → izostavljeni N08 i N09, 8 naloga, 203,8 km; neovisni testni oracle potvrđuje isti broj naloga i km.
5. **Izgradnja plana** – `BuildPlan` (redak 102): iz koraka DP-a pune se sva polja `Stop` (teret, km,
   minute). **Samoprovjera** (redak 146): km rekonstruirane rute moraju biti jednaki vrijednosti iz DP-a.
6. **Validator** (redak 88): ako `PlanValidator` nađe ijedan prekršaj, to je greška u planeru i baca se
   `InvalidOperationException`.

## 2. Stanje dinamičkog programiranja

Ključ stanja je **(trenutna lokacija, status svih naloga)** (`RouteDp.cs:40`).

- **Status** je jedan broj u bazi 3; svaki nalog je jedna znamenka: 0 = nije preuzet,
  1 = u kamionu, 2 = isporučen. Primjer s tri naloga – N01 isporučen (2), N02 u kamionu (1),
  N05 nije preuzet (0): status = 2·1 + 1·3 + 0·9 = **5**.
- **Vrijednost stanja** (`Best`, redak 113) = najmanji broj km koji preostaje do kraja svih naloga
  **i povratka u bazu**. Završno stanje (sve znamenke 2) vrijedi samo dionicu do baze (retci 115–118).
- **Prijelazi** (`Candidate`, redak 142):
  - **preuzimanje** naloga sa znamenkom 0 – samo ako masa i broj strojeva nakon preuzimanja stanu;
  - **isporuka** naloga sa znamenkom 1.
- **Teret se ne pamti** – `Decode` (redak 168) ga izračuna kao zbroj masa naloga sa znamenkom 1.
- **Rekonstrukcija rute** (`Reconstruct`, redak 187) prati zapamćene vrijednosti od početnog stanja i
  bira prvi prijelaz koji postiže optimum – zato je ruta deterministička.

**Pitanja koja će vjerojatno doći:**
- *Zašto je lokacija dio stanja?* Do istog statusa može se doći s različitih mjesta, a preostali km
  ovise o tome gdje je kamion.
- *Zašto tri vrijednosti, a ne dvije?* Treba razlikovati "u kamionu" (ulazi u teret, smije se isporučiti)
  od "isporučen".
- *Zašto se nosivost provjerava samo pri preuzimanju?* Isporuka teret samo smanjuje. Ako je teret
  ispravan nakon svakog preuzimanja, ispravan je nakon svake radnje (R11, R12).

## 3. Zašto je rezultat optimalan

1. **DP je potpun i ispravan za jedan kamion.** Svaka dopuštena ruta je put kroz graf stanja
   (redoslijed preuzimanje → isporuka osigurava prijelaz znamenke 0 → 1 → 2, a nosivost provjera kod
   preuzimanja). Preostali km ovise samo o (lokacija, status), ne o tome kako se do stanja došlo –
   Bellmanovo načelo optimalnosti – pa je memoizacija ispravna.
2. **Najkraća ruta je ujedno i najbrža** (dokaz u XML komentaru klase, `ExactDpPlanner.cs`, retci
   32–41). Za fiksan skup od m naloga broj radnji je uvijek 2m, pa su minute radnji konstantne, a
   minute vožnje rastu s km. Zato je radni dan dovoljno provjeriti za najkraću rutu: ako ona ne stane,
   ne stane nijedna ruta za taj skup.
3. **Kamioni su neovisni kad je raspodjela fiksna** – ne dijele teret, vrijeme ni lokacije, pa je
   minimum zbroja jednak zbroju minimuma. Budući da se isprobaju **sve** raspodjele, minimum je globalan.
4. **Aritmetika je točna** – desetinke km su cijeli brojevi, nema šuma pomičnog zareza; izjednačenja
   su determinirana fiksnim redoslijedom obilaska (R39).

**Napomena:** optimum vrijedi u prostoru ruta u kojima je svaka točka mjesto radnje (R31, odluka D4).
Zbog zaokruživanja dionica prazan međuposjet bi teoretski mogao skratiti zbroj – zato je zabranjen.

**Neovisna potvrda:** planer pogađa referentne vrijednosti iz `CLAUDE.md` izračunate izvan koda
(200,1 / 182,2 km), plan prolazi validator napisan bez uvida u planer, a referentni testovi to
provjeravaju pri svakom pokretanju.

## 4. Složenost

| Dio | Broj |
|-----|------|
| Raspodjele | ∏ (broj kamiona koji mogu nositi nalog) – za zadatak 2^5 = 32 |
| Stanja jednog DP-a | najviše 3^m × (2m + 1), uz O(m) rada po stanju |
| Različiti DP-ovi (zbog pamćenja) | najviše T × 2^n |
| Najgori slučaj | O(T · 4^n · n²), jer je Σ C(n, m) · 3^m = 4^n |
| **Ovaj zadatak** | 32 raspodjele, 63 DP-a, 284.366 stanja, **40–90 ms** |

U praksi je broj stanja puno manji od 3^m, jer B nosi najviše 3 stroja, a A najviše 2 – statusi s
više znamenki 1 nisu dostižni.

## 5. Gdje prestaje biti upotrebljiv – izmjereno

Sintetički scenariji u **najgorem slučaju**: svaki nalog mogu nositi oba kamiona (sve mase 500–1500 kg),
stvarni limiti kamiona (A 2 stroja, B 3 stroja), radni dan neograničen, lokacije nasumične u
kvadratu ±30 km (seed 42). Release build, isto računalo kao za rezultate, 21.11.2026.

| Naloga | Vrijeme | Raspodjela | DP izračuna | DP stanja | Memorija |
|-------:|--------:|-----------:|------------:|----------:|---------:|
| 6 | 0,03 s | 64 | 126 | 18.534 | 2 MB |
| 8 | 0,09 s | 256 | 510 | 313.390 | 2 MB |
| 10 | 0,7 s | 1.024 | 2.046 | 4,7 mil. | 8 MB |
| 11 | 2,5 s | 2.048 | 4.094 | 17,7 mil. | 34 MB |
| 12 | 10,7 s | 4.096 | 8.190 | 65,6 mil. | 128 MB |
| 13 | 44 s | 8.192 | 16.382 | 239 mil. | 174 MB |
| 14 | **175 s** | 16.384 | 32.766 | 863 mil. | 608 MB |

**Svaki dodatni nalog otprilike učetverostručuje vrijeme** – točno kako predviđa 4^n.

- **Do ~12–13 naloga koje mogu svi kamioni:** sekunde – interaktivno upotrebljivo.
- **14 i više:** minute; procjena za 16 naloga je oko 45 minuta – neupotrebljivo.
- **Više kamiona** je još gore: raspodjela ima T^k (3 kamiona i 15 zajedničkih naloga = 14 mil. raspodjela).
- **Proširena pretraga (T23)**, kad radni dan ne dopušta sve naloge, ima (c + 1)^n raspodjela –
  20 naloga = 3^20 ≈ 3,5 milijardi.

Zadatak je brz zato što samo 5 od 10 naloga mogu oba kamiona – teški strojevi idu samo na B i
drastično smanjuju pretragu. Tvrde granice u kodu (najviše 39 naloga po kamionu, 63 ukupno, inače
`NotSupportedException`, odluka T30) daleko su iznad granice prihvatljivog vremena.

**Zamjena za veće probleme:** heuristika (umetanje + lokalna pretraga) ili solver (OR-Tools / MIP),
uz oznaku "optimum nije zajamčen" prema pravilu R38. Sučelje `IRoutePlanner` to omogućuje bez
promjene validatora, testova i weba.

## 6. Tri mjesta gdje bi greška najlakše promijenila rezultat

### 1. Provjera nosivosti – `RouteDp.cs:147`

```csharp
if (loadKg + _weight[i] > _capacityKg || machines + 1 > _maxMachines)
```

- `>` zamijenjen s `>=` zabranio bi teret točno na granici (npr. M06 + M08 = 10.000 kg na B) – rješenje
  bi moglo postati dulje.
- Provjera tereta **prije** dodavanja stroja (`loadKg > _capacityKg`) dopustila bi preopterećenje –
  rezultat **kraći od referentnog**, ali nevaljan.
- Ovo je jedino mjesto u DP-u gdje se provode R11 i R12.

### 2. Povratak u bazu – `RouteDp.cs:115–118` (uz redak 53)

```csharp
if (status == _allDelivered) return _legs[location, 0];
```

- `return 0` bi značio da DP ne plaća povratak – rute bi završavale daleko od baze i rezultat bi bio
  ispod 200,1 km.
- Indeks `0` je baza samo zato što lokalno mapiranje u retku 53 stavlja bazu prvu. Promjena tog
  mapiranja tiho bi pretvorila neku drugu lokaciju u "bazu".

### 3. Provjera radnog dana – `ExactDpPlanner.cs:229` i `PreparedScenario.cs:80–82`

```csharp
var fits = input.FitsWorkday(input.WorkdayMinutes(dp.Km10, 2 * orders.Count));
```

- **Ovo je jedina razlika između 200,1 i 182,2 km.**
- `orders.Count` umjesto `2 * orders.Count` ili zamijenjene grane `Total` / `DrivingOnly` u
  `WorkdayMinutes` dopustile bi kamionu B svih 10 naloga u `Total` načinu (582 min) – rezultat bi pao
  na 182,2 km.

### Što bi te greške uhvatilo

Sve tri bi uhvatila zaštita koju projekt već ima:

| Greška | Validator (planer baca iznimku) | Testovi |
|--------|--------------------------------|---------|
| Nosivost | R11 / R12 | referentni km, plan prolazi validator |
| Povratak | R2 / R16 | referentni km |
| Radni dan | R20 | referentni km (200,1 u `Total`) |

**Poruka za prezentaciju:** planer nije siguran zato što je pažljivo napisan, nego zato što ga
provjeravaju dva neovisna mehanizma – validator napisan samo iz specifikacije i referentne
vrijednosti izračunate izvan koda.
