# Rezultati

Ulaz: `data/scenario.json` (podaci iz `docs/zadatak.md`). Planer: `ExactDpPlanner` (egzaktni).
Svaki plan je provjeren s `PlanValidator` – **0 prekršaja u oba načina**. Rezultati odgovaraju
referentnim vrijednostima iz `CLAUDE.md`.

Brojke ispod generirane su pokretanjem planera nad trenutnim kodom (Release build, 21.11.2026.),
a ne prepisane iz izvještaja. Ista se slika vidi u aplikaciji: **Planiranje → Plan ruta** i **Karta**.

## Sažetak

| Način radnog dana | Ukupno | Kamion A | Kamion B | Isplanirano | Neizvedivo | Prekršaji |
|-------------------|-------:|---------:|---------:|------------:|-----------:|----------:|
| `Total` – vožnja + radnje ≤ 9 h (zadano) | **200,1 km** | 37,2 km (1 nalog) | 162,9 km (9 naloga) | 10 / 10 | 0 | 0 |
| `DrivingOnly` – samo vožnja ≤ 9 h | **182,2 km** | 0 km (neiskorišten) | 182,2 km (10 naloga) | 10 / 10 | 0 | 0 |

| Način | Kamion | Vožnja | Radnje | Ukupno | Ograničenje se odnosi na | Iskorišteno od 9 h |
|-------|--------|-------:|-------:|-------:|--------------------------|-------------------:|
| Total | A | 0:37 | 0:40 | 1:17 | vožnja + radnje | 14 % |
| Total | B | 2:43 | 6:00 | 8:43 | vožnja + radnje | 97 % |
| DrivingOnly | A | – | – | – | – | – |
| DrivingOnly | B | 3:02 | 6:40 | 9:42 | samo vožnja | 34 % |

**Napomena za prezentaciju:** u `DrivingOnly` načinu kamion B radi ukupno **9 h 42 min**, jer se broji
samo vožnja (3 h 02 min). Razlika 200,1 → 182,2 km je cijena tumačenja "9 h" (dvosmislenost D1):
u `Total` načinu B ne može odraditi svih 10 naloga (400 min radnji + najmanje 182,2 min vožnje
= 582,2 min > 540), pa u optimumu kamion A preuzima N03.

## Total (zadano): 200,1 km

### Kamion A – 37,2 km, 1 nalog, 77,2 min

| # | Lokacija | Radnja | Nalog | Teret nakon (kg) | Strojeva | Dionica km | Ukupno km | Minute |
|---|----------|--------|-------|-----------------:|---------:|-----------:|----------:|-------:|
| 1 | L9 | preuzimanje | N03 (M03) | 1.800 | 1 | 9,4 | 9,4 | 29,4 |
| 2 | L4 | isporuka | N03 | 0 | 0 | 17,0 | 26,4 | 66,4 |
| – | L0 | povratak | – | 0 | 0 | 10,8 | 37,2 | 77,2 |

### Kamion B – 162,9 km, 9 naloga, 522,9 min

| # | Lokacija | Radnja | Nalog | Teret nakon (kg) | Strojeva | Dionica km | Ukupno km | Minute |
|---|----------|--------|-------|-----------------:|---------:|-----------:|----------:|-------:|
| 1 | L0 | preuzimanje | N05 (M05) | 3.100 | 1 | 0,0 | 0,0 | 20,0 |
| 2 | L1 | preuzimanje | N01 (M01) | 3.900 | 2 | 8,5 | 8,5 | 48,5 |
| 3 | L2 | preuzimanje | N02 (M02) | 5.200 | 3 | 7,6 | 16,1 | 76,1 |
| 4 | L8 | isporuka | N05 | 2.100 | 2 | 13,6 | 29,7 | 109,7 |
| 5 | L8 | preuzimanje | N07 (M07) | 6.900 | 3 | 0,0 | 29,7 | 129,7 |
| 6 | L2 | isporuka | N07 | 2.100 | 2 | 13,6 | 43,3 | 163,3 |
| 7 | L3 | isporuka | N01 | 1.300 | 1 | 7,3 | 50,6 | 190,6 |
| 8 | L5 | isporuka | N02 | 0 | 0 | 10,8 | 61,4 | 221,4 |
| 9 | L5 | preuzimanje | N06 (M06) | 3.900 | 1 | 0,0 | 61,4 | 241,4 |
| 10 | L7 | isporuka | N06 | 0 | 0 | 12,5 | 73,9 | 273,9 |
| 11 | L7 | preuzimanje | N10 (M10) | 9.400 | 1 | 0,0 | 73,9 | 293,9 |
| 12 | L3 | isporuka | N10 | 0 | 0 | 17,3 | 91,2 | 331,2 |
| 13 | L3 | preuzimanje | N04 (M04) | 2.400 | 1 | 0,0 | 91,2 | 351,2 |
| 14 | L4 | preuzimanje | N08 (M08) | 8.500 | 2 | 17,0 | 108,2 | 388,2 |
| 15 | L9 | isporuka | N08 | 2.400 | 1 | 17,0 | 125,2 | 425,2 |
| 16 | L6 | preuzimanje | N09 (M09) | 9.900 | 2 | 18,0 | 143,2 | 463,2 |
| 17 | L1 | isporuka | N09 | 2.400 | 1 | 11,2 | 154,4 | 494,4 |
| 18 | L0 | isporuka | N04 | 0 | 0 | 8,5 | 162,9 | 522,9 |
| – | L0 | povratak | – | 0 | 0 | 0,0 | 162,9 | 522,9 |

Najveći teret na B: 9.900 kg (korak 16), 3 stroja (koraci 3 i 5). M10 (9.400 kg) vozi sam (koraci 11–12).

## DrivingOnly: 182,2 km

### Kamion A – neiskorišten (0 km, 0 naloga)

### Kamion B – 182,2 km, 10 naloga, vožnja 182,2 min, ukupno 582,2 min

| # | Lokacija | Radnja | Nalog | Teret nakon (kg) | Strojeva | Dionica km | Ukupno km | Minute |
|---|----------|--------|-------|-----------------:|---------:|-----------:|----------:|-------:|
| 1 | L4 | preuzimanje | N08 (M08) | 6.100 | 1 | 10,8 | 10,8 | 30,8 |
| 2 | L9 | preuzimanje | N03 (M03) | 7.900 | 2 | 17,0 | 27,8 | 67,8 |
| 3 | L9 | isporuka | N08 | 1.800 | 1 | 0,0 | 27,8 | 87,8 |
| 4 | L6 | preuzimanje | N09 (M09) | 9.300 | 2 | 18,0 | 45,8 | 125,8 |
| 5 | L1 | isporuka | N09 | 1.800 | 1 | 11,2 | 57,0 | 157,0 |
| 6 | L1 | preuzimanje | N01 (M01) | 2.600 | 2 | 0,0 | 57,0 | 177,0 |
| 7 | L0 | preuzimanje | N05 (M05) | 5.700 | 3 | 8,5 | 65,5 | 205,5 |
| 8 | L4 | isporuka | N03 | 3.900 | 2 | 10,8 | 76,3 | 236,3 |
| 9 | L8 | isporuka | N05 | 800 | 1 | 25,2 | 101,5 | 281,5 |
| 10 | L8 | preuzimanje | N07 (M07) | 5.600 | 2 | 0,0 | 101,5 | 301,5 |
| 11 | L2 | preuzimanje | N02 (M02) | 6.900 | 3 | 13,6 | 115,1 | 335,1 |
| 12 | L2 | isporuka | N07 | 2.100 | 2 | 0,0 | 115,1 | 355,1 |
| 13 | L3 | isporuka | N01 | 1.300 | 1 | 7,3 | 122,4 | 382,4 |
| 14 | L5 | isporuka | N02 | 0 | 0 | 10,8 | 133,2 | 413,2 |
| 15 | L5 | preuzimanje | N06 (M06) | 3.900 | 1 | 0,0 | 133,2 | 433,2 |
| 16 | L7 | isporuka | N06 | 0 | 0 | 12,5 | 145,7 | 465,7 |
| 17 | L7 | preuzimanje | N10 (M10) | 9.400 | 1 | 0,0 | 145,7 | 485,7 |
| 18 | L3 | isporuka | N10 | 0 | 0 | 17,3 | 163,0 | 523,0 |
| 19 | L3 | preuzimanje | N04 (M04) | 2.400 | 1 | 0,0 | 163,0 | 543,0 |
| 20 | L0 | isporuka | N04 | 0 | 0 | 19,2 | 182,2 | 582,2 |
| – | L0 | povratak | – | 0 | 0 | 0,0 | 182,2 | 582,2 |

## Izračun i pretraga

| | Total | DrivingOnly |
|---|---:|---:|
| Vrijeme izračuna (Release, medijan 5 poziva nakon zagrijavanja) | 88 ms | 84 ms |
| Isprobane raspodjele naloga (2^5 za N01–N05, N06–N10 samo B) | 32 | 32 |
| DP izračuna (različiti parovi kamion + skup naloga) | 63 | 63 |
| DP stanja ukupno | 284.366 | 284.366 |
| Proširena pretraga (T23) | ne | ne |

Vrijeme ovisi o računalu i JIT zagrijavanju (pojedinačni pozivi 41–92 ms). U aplikaciji se
plan računa jednom po načinu i verziji `scenario.json`, zatim se čuva u memoriji.

## Testovi

`dotnet test`: **144 / 144 prolazi** (stanje nakon faze 5).

| Skupina | Testova | Što dokazuje |
|---------|--------:|--------------|
| `DistanceTests` | 16 | dionice (8,5 / 8,5 / 34,9), simetrija, dijagonala 0, zaokruživanje AwayFromZero |
| `ScenarioDataTests` | 7 | `scenario.json` odgovara zadatku, zadane opcije 540 / 20 / 60 |
| `PlanBuilderTests` | 4 | testni pomoćnik računa km/minute kao ručni izračun |
| `PlanValidatorValidPlanTests` | 15 | ispravni planovi i granični slučajevi prolaze (točno 10.000 kg, točno 540 min, radnje u bazi…) |
| `PlanValidatorViolationTests` | 38 | validator odbija: preopterećenje (R11), previše strojeva (R12), isporuka prije preuzimanja (R6), preuzima A / isporučuje B (R5), bez povratka (R2), stroj ostao u kamionu (R28), krivi km (R16), radni dan (R20/R21) i dr. |
| `ReferenceSolutionTests` | 9 | 200,1 / 182,2 km, plan prolazi validator, jedna ruta po kamionu, determinizam, DrivingOnly ≤ Total |
| `InvalidInputPlanningTests` | 36 | neispravan ulaz nikad ne ruši planer: stroj 11.000 kg (K1), nepostojeća lokacija, preuzimanje == isporuka, masa ≤ 0, duplikat naloga, nepostojeći ili ponovljeni stroj, nema baze, neupotrebljivi kamioni (K4/K1) |
| `WorkdayOverflowPlanningTests` | 19 | prekratak radni dan (300 / 200 / 80 min): plan prolazi validator, izostavljeni su K3 "Ne stane u radni dan", najmanji broj izostavljenih i km potvrđeni neovisnim iscrpnim oracleom |

## Rubni slučajevi (faza 5)

Slučajevi su izračunati u fazi 5 (tada kao primjeri scenarija; od faze 6b isti se slučajevi u aplikaciji rade uređivanjem naloga na stranici Nalozi i poljem "max minuta" na stranici Plan). Validator: 0 prekršaja u svim slučajevima.

| Primjer | Ukupno | Isplanirano | Neizvedivi nalozi |
|---------|-------:|------------:|-------------------|
| Pretežak stroj (N11, M11 11.000 kg) | 200,1 km | 10 / 11 | N11 – K1: "Stroj M11 (11000 kg) teži je od nosivosti svih kamiona (A 3500 kg, B 10000 kg)." |
| Radni dan 300 min (Total) | 203,8 km (A 97,1, B 106,7) | 8 / 10 | N08, N09 – K3: "Ne stane u radni dan …" (najmanji mogući broj izostavljenih: 2) |
| Nepostojeća lokacija (N11 s L99) | 200,1 km | 10 / 11 | N11 – K4: "Neispravni podaci naloga N11: lokacija preuzimanja L99 ne postoji." |
| Neispravni podaci (masa 0, P == I, duplikat N05, stroj M99) | 200,1 km | 10 / 14 | N11, N12, drugi N05, N13 – svi K4 s razlogom |
