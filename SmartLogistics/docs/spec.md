# Specifikacija domene – Smart Logistics Planner

Izvori: `docs/zadatak.md` (oznaka **zadatak**) i odjeljak "Domena" u `CLAUDE.md`
(oznaka **CLAUDE.md**). Pretpostavke iz CLAUDE.md imaju prednost i ovdje se ne mijenjaju.
Prijedlozi analitičara označeni su kao dvosmislenosti (D1..Dn) i traže potvrdu
tehničkog vođe prije nego postanu pravila.

**Status (20.11.2026.):** tehnički vođa potvrdio je sve predložene pretpostavke
(D2–D8, D12–D16). Upisane su kao pravila R29–R39 (izvor **odluka Dx**);
D1 i D9–D11 riješeni su već u CLAUDE.md. Zapis odluka: `docs/odluke.md`.

Pojmovi:
- **Nalog** – prijevoz jednog stroja s lokacije preuzimanja na lokaciju isporuke.
- **Radnja** – preuzimanje (utovar) ili isporuka (istovar) jednog naloga.
- **Ruta kamiona** – niz radnji kamiona; točke rute su L0 (polazak), lokacije radnji
  redom, L0 (povratak).
- **Dionica** – vožnja između dviju uzastopnih točaka rute.
- **Teret** – ukupna masa (kg) i broj strojeva na kamionu.

---

## 1. Pravila

Numeracija je trajna; nova pravila dodaju se na kraj.

| ID | Pravilo (provjerljiva tvrdnja) | Izvor |
|----|--------------------------------|-------|
| R1 | Postoje točno dva kamiona: A (nosivost 3500 kg, najviše 2 stroja) i B (nosivost 10000 kg, najviše 3 stroja). | zadatak, CLAUDE.md |
| R2 | Ruta svakog korištenog kamiona počinje u L0 i završava u L0 (prva točka rute je L0, posljednja točka rute je L0). | zadatak ("Oba kamiona kreću iz baze L0", "Obavezan je povrat u bazu"), CLAUDE.md |
| R3 | Kamion smije ostati neiskorišten: nema nijednu radnju, ima 0 km, 0 naloga i to nije prekršaj. | CLAUDE.md |
| R4 | Svaki dodijeljeni nalog ima u planu točno jednu radnju preuzimanja i točno jednu radnju isporuke. | zadatak (FZ 2, 3, 5) |
| R5 | Preuzimanje i isporuku istog naloga izvodi isti kamion. | zadatak (FZ 5), CLAUDE.md |
| R6 | U ruti kamiona radnja preuzimanja naloga dolazi strogo prije radnje isporuke tog naloga. | zadatak (FZ 5), CLAUDE.md |
| R7 | Svaki nalog dodijeljen je najviše jednom kamionu. | zadatak (FZ 2) |
| R8 | Radnja preuzimanja naloga izvodi se na lokaciji preuzimanja tog naloga, a radnja isporuke na lokaciji isporuke tog naloga (prema tablici "Nalozi"). | zadatak |
| R9 | Kamion kreće iz L0 bez tereta (0 kg, 0 strojeva); stroj naloga nalazi se na kamionu točno od radnje preuzimanja do radnje isporuke tog naloga. | zadatak (FZ 5, tablica "Nalozi") |
| R10 | Teret nakon radnje: preuzimanje povećava masu za masu stroja i broj strojeva za 1; isporuka smanjuje masu za masu stroja i broj strojeva za 1. | zadatak ("teret nakon svake radnje") |
| R11 | Nakon svake radnje masa tereta je <= nosivosti kamiona (jednakost je dopuštena). | zadatak (FZ 4, "nikad ne prekoračiti"), CLAUDE.md |
| R12 | Nakon svake radnje broj strojeva na kamionu je <= najvećem dopuštenom broju strojeva kamiona. | zadatak (FZ 4), CLAUDE.md |
| R13 | Nalog čiji stroj ima masu veću od nosivosti kamiona ne smije biti dodijeljen tom kamionu (izravna posljedica R11). | zadatak (FZ 4) |
| R14 | Duljina dionice = euklidska udaljenost između koordinata dviju lokacija zaokružena na 1 decimalu (`Math.Round(x, 1, MidpointRounding.AwayFromZero)`); zaokružuje se svaka dionica zasebno, tek zatim se zbraja. | zadatak, CLAUDE.md |
| R15 | Dionica između dviju uzastopnih točaka na istoj lokaciji ima 0 km. | zadatak (euklidska udaljenost) |
| R16 | Km kamiona = zbroj dionica L0 -> lokacija 1. radnje -> ... -> lokacija zadnje radnje -> L0. Ukupni km plana = km(A) + km(B). | zadatak (FZ 7) |
| R17 | Vrijeme vožnje dionice u minutama jednako je njezinoj duljini u km (60 km/h, 1 km = 1 min). | zadatak, CLAUDE.md |
| R18 | Svaka radnja (preuzimanje ili isporuka) traje 20 min, uključujući radnje na lokaciji L0. | zadatak, CLAUDE.md |
| R19 | Polazak iz L0 i završni povratak u L0 nisu radnje i ne troše 20 min. | zadatak (radnja = utovar/istovar), CLAUDE.md |
| R20 | U načinu `WorkdayMode.Total`: za kamion vrijedi (vrijeme vožnje + 20 min x broj radnji) <= 540 min. | CLAUDE.md |
| R21 | U načinu `WorkdayMode.DrivingOnly`: za kamion vrijedi vrijeme vožnje <= 540 min; trajanje radnji ne ulazi u ograničenje. | CLAUDE.md |
| R22 | Zadani način radnog dana je `WorkdayMode.Total`; `WorkdayMode.DrivingOnly` je odabirljiva alternativa. | CLAUDE.md |
| R23 | Svaki nalog iz ulaza u rezultatu je ili dodijeljen točno jednom kamionu ili naveden na popisu neizvedivih naloga s razlogom; nijedan nalog nije izostavljen bez objašnjenja. | zadatak (FZ 8) |
| R24 | Među planovima koji poštuju sva ostala pravila bira se plan s najmanjim ukupnim km, uz prednost većeg broja izvršenih naloga prema R32. | zadatak (FZ 6) |
| R25 | Rezultat za svaki kamion sadrži: redoslijed lokacija, radnje, teret nakon svake radnje (kg i broj strojeva), km i broj naloga; rezultat sadrži i ukupne km. | zadatak (FZ 7, "Po kamionu prikazati") |
| R26 | Broj naloga kamiona = broj naloga dodijeljenih tom kamionu = broj njegovih radnji preuzimanja = broj njegovih radnji isporuke. | zadatak |
| R27 | Aplikacija prikazuje ulazne podatke (kamione, strojeve, lokacije, naloge). | zadatak (FZ 1) |
| R28 | Nakon zadnje radnje kamiona teret je 0 kg i 0 strojeva (posljedica R4–R10). | zadatak (FZ 5) |
| R29 | Ograničenje radnog dana (R20, R21) vrijedi zasebno za svaki kamion (jedan vozač po kamionu) i obuhvaća cijelu rutu od polaska iz L0 do završnog dolaska u L0: vožnju do prve radnje, sve dionice, povratnu vožnju te u `Total` načinu sve radnje, uključujući radnje u L0. | odluka D2 |
| R30 | Plan pokriva jedan radni dan; nalozi se ne prenose na sljedeći dan. Nalog koji ne stane u dan navodi se kao neizvediv (R23). | odluka D3 |
| R31 | Ruta kamiona je jedan neprekinuti niz radnji između početnog i završnog L0. Svaka točka rute osim početnog i završnog L0 mjesto je barem jedne radnje; prazni međuposjeti (uključujući L0) nisu dopušteni. Radnja u L0 usred rute (N04, N05) ne počinje novi radni dan i ne mijenja teret osim same radnje. | odluka D4 |
| R32 | Cilj optimizacije je leksikografski: (1) najveći broj izvršenih naloga, (2) najmanji ukupni km. Nalog smije ostati neizvršen samo ako ga nije moguće uključiti u plan bez kršenja pravila; izvediv nalog ne smije se izostaviti radi manje km. | odluka D5, D6 |
| R33 | Svaki neizvediv nalog ima točno jednu kategoriju razloga i poruku s brojkama: K1 – masa stroja veća od nosivosti svih kamiona; K2 – samostalna ruta L0 -> preuzimanje -> isporuka -> L0 prekoračuje radni dan na svim kamionima koji mogu nositi stroj; K3 – nalog je sam za sebe izvediv, ali ne stane u plan uz ostale naloge (radni dan / kapacitet); K4 – neispravni ulazni podaci naloga (R37). | odluka D7 |
| R34 | Kamioni kreću prazni; strojevi su dostupni cijeli dan; nema vremenskih prozora ni čekanja. | odluka D8 |
| R35 | Km i minute računaju se kao zbroj zaokruženih dionica (R14) i trajanja radnji, bez daljnjeg zaokruživanja; usporedba s 540 min je uključiva (<=) i otporna na grešku pomičnog zareza. Zaokruživanje na cijele minute (hh:mm) koristi se samo za prikaz. | odluka D12 |
| R36 | Neiskorišten kamion ima 0 km, 0 naloga, nijednu radnju i nijednu dionicu (ni L0 -> L0) i prikazuje se s oznakom "neiskorišten". | odluka D13 |
| R37 | **Izmijenjeno 21.11.2026. (faza 5, odluka T31).** Neispravni ulazni podaci nikad ne zaustavljaju planiranje – planer uvijek vraća plan. Nalog s neispravnim podacima je neizvediv **K4** s razlogom, a ostali nalozi se planiraju. Neispravan nalog je: nepostojeći stroj; nepostojeća lokacija preuzimanja ili isporuke; preuzimanje == isporuka; masa stroja <= 0; duplikat ID-a naloga (drugi i svaki sljedeći nalog s istim ID-om); stroj već korišten u ranijem nalogu; stroj ili lokacija čiji je ID dupliciran; lokacija s nebrojčanim koordinatama. Ako baza ne postoji, svi nalozi su K4 i nijedan kamion nema radnju. Kamion s nosivošću ili brojem strojeva <= 0, ili s ID-om koji se već pojavio, ne koristi se (nema radnji); ako nijedan upotrebljiv kamion ne može nositi stroj, nalog je K1. Neispravne `PlanningOptions` (npr. brzina <= 0) nisu ulazni podaci nego greška programa i bacaju `ArgumentException`. | odluka D14, izmijenjeno odlukom T31 |
| R40 | Poruke neizvedivih naloga su na hrvatskom i sadrže brojke. K1: "Stroj Mxx (N kg) teži je od nosivosti svih kamiona" (uz nosivosti); nalog izostavljen jer svi nalozi zajedno ne stanu u radni dan (K3, R32 – izostavlja se najmanji mogući broj naloga, a među jednakima plan s najmanje km): "Ne stane u radni dan" (uz način radnog dana i limit). | odluka T31 |
| R38 | Za veličinu zadanih podataka planer vraća egzaktni optimum prema R32. Ako planer koristi heuristiku, rezultat i UI moraju navesti da optimum nije zajamčen. | odluka D15 |
| R39 | Planer je deterministički: isti ulaz uvijek daje isti plan, i među planovima jednake vrijednosti cilja (R32). | odluka D6, D16 |

---

## 2. Dvosmislenosti

| ID | Što je nejasno | Moguća tumačenja | Prijedlog pretpostavke | Utjecaj na rezultat |
|----|----------------|------------------|------------------------|---------------------|
| D1 | "Vozač smije voziti 9 h dnevno" – računa li se samo vožnja ili i radnje. | (a) samo vožnja <= 540 min; (b) vožnja + radnje <= 540 min. | **Riješeno u CLAUDE.md**: zadano `Total` (b), alternativa `DrivingOnly` (a). Vidi R20, R21, R22 i posljedice u odjeljku 3.8. | Velik: u `Total` načinu radnje troše 40 min po nalogu iz istog proračuna od 540 min; skup izvedivih planova je uži i optimum ne može biti manji nego u `DrivingOnly`. |
| D2 | Na koga se odnosi ograničenje od 540 min i što obuhvaća. | (a) po kamionu (jedan vozač po kamionu); (b) zbirno za oba kamiona; (c) bez vožnje do prve lokacije / bez povratka. | **Potvrđeno 20.11.2026. → R29.** Po kamionu (jedan vozač po kamionu), od polaska iz L0 do završnog dolaska u L0, uključujući vožnju do prve radnje i povratnu vožnju, te (u `Total`) sve radnje uključujući radnje u L0. | Srednji: tumačenje (b) bi drastično suzilo skup izvedivih planova; (c) bi ga proširilo. |
| D3 | Jedan dan ili više dana. | (a) plan pokriva jedan radni dan; (b) nalozi se mogu prenijeti na sljedeći dan. | **Potvrđeno 20.11.2026. → R30.** Jedan radni dan; nema prijenosa naloga. Nalog koji ne stane u dan je neizvediv (R23). | Velik ako bi se dopustilo više dana (nestaje ograničenje radnog dana kao uzrok neizvedivosti). |
| D4 | Smije li kamion usput proći kroz bazu ili napraviti više tura u danu. | (a) jedna neprekinuta ruta, L0 se usred rute pojavljuje samo kao lokacija radnje (N04 isporuka, N05 preuzimanje); (b) dopušteni su "prazni" međuposjeti (točke bez radnje), uključujući L0; (c) više zasebnih tura s povratkom u L0, svaka s vlastitim pravilima. | **Potvrđeno 20.11.2026. → R31.** (a): ruta je jedan niz radnji između početnog i završnog L0; posjet L0 usred rute postoji samo ako se tamo izvodi radnja i nema posebno značenje (ne resetira radni dan, ne mijenja teret osim same radnje). Prazni međuposjeti nisu dopušteni. | Mali u euklidskom smislu (prazni međuposjet ne može skratiti put), ali zbog zaokruživanja dionica na 0.1 km zbroj dviju zaokruženih dionica preko međutočke teoretski može biti manji od izravne zaokružene dionice – zabrana sprječava takve artefakte i pojednostavljuje validator. |
| D5 | Smije li solver namjerno odbaciti izvediv nalog da bi smanjio km. | (a) ne – najprije se izvršava što više naloga; (b) da – čisto minimiziranje km (trivijalno: prazan plan, 0 km). | **Potvrđeno 20.11.2026. → R32.** (a): nalog smije ostati neizvršen samo ako ga nije moguće uključiti bez kršenja pravila. Tumačenje (b) je besmisleno jer daje prazan plan. | Velik: određuje cilj optimizacije (leksikografski: broj izvršenih naloga, zatim km). |
| D6 | Kriterij izbora kad nisu svi nalozi izvedivi zajedno. | (a) najviše izvršenih naloga, zatim najmanje km; (b) najveća ukupna prevezena masa; (c) prioritet po ID-u naloga; (d) km uz kaznu po neizvršenom nalogu. | **Potvrđeno 20.11.2026. → R32, R39.** (a), uz deterministički odabir među jednakima (vidi D16). | Velik kad radni dan ili kapacitet ne dopuštaju sve naloge; u zadanim podacima ne utječe ako su svi nalozi izvedivi zajedno (to ova specifikacija ne računa). |
| D7 | Kako klasificirati i prijaviti razlog neizvedivosti. | Slobodan tekst ili kategorije. | **Potvrđeno 20.11.2026. → R33.** Kategorije: (K1) masa stroja veća od nosivosti svih kamiona; (K2) samostalna ruta L0 -> preuzimanje -> isporuka -> L0 prekoračuje radni dan na svim kamionima koji mogu nositi stroj; (K3) nalog je sam za sebe izvediv, ali ne stane u plan uz ostale naloge (radni dan / kapacitet); (K4) neispravni ulazni podaci naloga (D14). Uz kategoriju navesti brojke (npr. masa i nosivost, potrebne minute i 540). | Utječe na UI i testove (FZ 8 "jasno upozoriti"). |
| D8 | Početno stanje i vremenska ograničenja naloga. | (a) kamioni kreću prazni, strojevi su dostupni cijeli dan, nema vremenskih prozora ni čekanja; (b) postoje vremenski prozori ili početni teret. | **Potvrđeno 20.11.2026. → R34.** (a). Zadatak ne navodi vremenske prozore ni početni teret. | Mali za zadane podatke; bitan ako se kasnije dodaju vremenski prozori. |
| D9 | Vrijedi li ograničenje kapaciteta samo nakon radnje ili i "tijekom" radnje, te je li granica uključiva. | (a) nakon svake radnje, <=; (b) strogo <. | **Riješeno u CLAUDE.md** ("vrijedi NAKON SVAKE radnje"), vidi R11, R12. Granica je uključiva jer zadatak kaže "nikad ne prekoračiti". | Granični slučajevi (npr. M06 + M08 = 10000 kg na B) su dopušteni. |
| D10 | Traje li 20 min po radnji (stroju) ili po zaustavljanju na lokaciji. | (a) po radnji; (b) po zaustavljanju. | **Riješeno u CLAUDE.md** ("svaka radnja 20 min"), vidi R18: dvije radnje na istoj lokaciji traju 40 min. | Srednji u `Total` načinu. |
| D11 | Troše li radnje u bazi vrijeme i je li završni povratak radnja. | (a) radnja u L0 traje 20 min, povratak nije radnja; (b) radnje u bazi su "besplatne". | **Riješeno u CLAUDE.md** ("svaka radnja 20 min, i u bazi"), vidi R18, R19. | Odnosi se na N04 i N05. |
| D12 | Aritmetika vremena i km. | (a) minute su decimalne (npr. 8.5 min), bez zaokruživanja na cijele minute; (b) zaokruživanje vremena na minute. | **Potvrđeno 20.11.2026. → R35.** (a): km i minute računaju se točno na desetinke (zbroj zaokruženih dionica), usporedba s 540 je uključiva (<=) i otporna na grešku pomičnog zareza; zaokruživanje na cijele minute samo za prikaz (hh:mm) u UI-ju. | Mali, ali bitan za granični slučaj točno 540 min i za testove (tolerancija prema CLAUDE.md). |
| D13 | Kako prikazati neiskorišten kamion. | (a) ruta "L0" bez dionica, 0 km; (b) ruta "L0 -> L0" s dionicom 0 km; (c) kamion se ne prikazuje. | **Potvrđeno 20.11.2026. → R36.** (a): kamion se prikazuje kao "neiskorišten", 0 km, 0 naloga, bez radnji i bez dionice. Dopuštenost je riješena u CLAUDE.md (R3). | Samo prikaz; km se ne mijenja. |
| D14 | Postupanje s neispravnim ulaznim podacima. | (a) cijeli ulaz se odbija; (b) odbijaju se samo neispravni nalozi, ostali se planiraju. | **Potvrđeno 20.11.2026. → R37; izmijenjeno 21.11.2026. (T31): neispravan nalog više ne odbija cijeli ulaz, nego postaje K4, a planer uvijek vraća plan.** Prvotni prijedlog: Globalne greške (duplikat ID-a kamiona/stroja/lokacije/naloga, nepostojeća baza L0, nosivost ili max. broj strojeva <= 0, masa stroja <= 0, nedostajuće ili nebrojčane koordinate) – ulaz se odbija s popisom grešaka, planiranje se ne pokreće. Greške pojedinog naloga (nepostojeći stroj ili lokacija, preuzimanje == isporuka, stroj već korišten u drugom nalogu) – nalog se označava neizvedivim (K4) s razlogom, ostali se planiraju. | Utječe na validator ulaza i testove; za zadane podatke nema grešaka (odjeljak 3.1). |
| D15 | Mora li rezultat biti dokazani optimum ili je dovoljna heuristika. | (a) egzaktno (dokazani minimum km); (b) heuristika uz prijavu da optimum nije zajamčen. | **Potvrđeno 20.11.2026. → R38.** (a) za veličinu zadanih podataka; ako se koristi heuristika, UI to mora navesti. Specifikacija ne računa optimum. | Utječe na izbor algoritma i na usporedbu s referentnim vrijednostima iz CLAUDE.md. |
| D16 | Odabir među planovima jednakih km. | (a) bilo koji; (b) deterministički (npr. leksikografski po ID-ovima naloga i kamiona). | **Potvrđeno 20.11.2026. → R39.** (b): isti ulaz uvijek daje isti plan. | Ponovljivost testova i demoa. |

---

## 3. Izvedene činjenice

### 3.1 Provjera ulaznih podataka

- ID-jevi kamiona (A, B), strojeva (M01–M10), lokacija (L0–L9) i naloga (N01–N10) su jedinstveni.
- Svi strojevi i sve lokacije navedene u nalozima postoje u tablicama "Strojevi" i "Lokacije".
- Svaki stroj pojavljuje se u točno jednom nalogu (N0k koristi M0k, N10 koristi M10).
- Ni u jednom nalogu preuzimanje nije jednako isporuci.
- Sve mase (800–9400 kg) i nosivosti (3500, 10000) su pozitivne; max. broj strojeva (2, 3) je pozitivan.
- Svaka lokacija L0–L9 je lokacija preuzimanja točno jednog naloga.
- Lokacije isporuke: L3 dvaput (N01, N10), L6 nijednom; ostale po jednom.
- Nalog s preuzimanjem u bazi: N05 (L0 -> L8). Nalog s isporukom u bazu: N04 (L3 -> L0).
- Podudaranje lokacija (lokacija isporuke jednog naloga = lokacija preuzimanja drugog; samo činjenica iz podataka, ne prijedlog rute):
  N01->L3=N04, N02->L5=N06, N03->L4=N08, N04->L0=N05, N05->L8=N07, N06->L7=N10,
  N07->L2=N02, N08->L9=N03, N09->L1=N01, N10->L3=N04.

### 3.2 Kompatibilnost strojeva i kamiona (pojedinačno)

Pravilo R13: stroj smije na kamion samo ako je masa <= nosivost.

| Stroj | Masa | <= 3500 (A)? | <= 10000 (B)? | Nalog | Dopušteni kamioni |
|-------|-----:|:------------:|:-------------:|-------|-------------------|
| M01 | 800  | da | da | N01 | A, B |
| M02 | 1300 | da | da | N02 | A, B |
| M03 | 1800 | da | da | N03 | A, B |
| M04 | 2400 | da | da | N04 | A, B |
| M05 | 3100 | da | da | N05 | A, B |
| M06 | 3900 | ne (3900 > 3500) | da | N06 | B |
| M07 | 4800 | ne (4800 > 3500) | da | N07 | B |
| M08 | 6100 | ne (6100 > 3500) | da | N08 | B |
| M09 | 7500 | ne (7500 > 3500) | da | N09 | B |
| M10 | 9400 | ne (9400 > 3500) | da | N10 | B |

Zaključak: **M06, M07, M08, M09, M10 ne mogu na kamion A**; nalozi N06–N10 mogu
ići samo na B. Nalozi N01–N05 mogu na A ili B. Nijedan stroj nije teži od 10000 kg,
pa nijedan nalog nije neizvediv zbog mase (kategorija K1 se u zadanim podacima ne pojavljuje).

### 3.3 Kombinacije strojeva na kamionu A (3500 kg, najviše 2 stroja)

Kandidati su samo M01–M05 (3.2). Svih 10 parova:

| Par | Zbroj (kg) | <= 3500? |
|-----|-----------:|:--------:|
| M01 + M02 | 800 + 1300 = 2100 | da |
| M01 + M03 | 800 + 1800 = 2600 | da |
| M01 + M04 | 800 + 2400 = 3200 | da |
| M02 + M03 | 1300 + 1800 = 3100 | da |
| M01 + M05 | 800 + 3100 = 3900 | ne |
| M02 + M04 | 1300 + 2400 = 3700 | ne |
| M02 + M05 | 1300 + 3100 = 4400 | ne |
| M03 + M04 | 1800 + 2400 = 4200 | ne |
| M03 + M05 | 1800 + 3100 = 4900 | ne |
| M04 + M05 | 2400 + 3100 = 5500 | ne |

- Parovi koji stanu na A: **(M01, M02), (M01, M03), (M01, M04), (M02, M03)**.
- M05 ne stane ni s jednim drugim strojem na A: dok je M05 ukrcan na A, A ne smije nositi ništa drugo.
- Tri stroja na A krše R12 (najviše 2); usto i tri najlakša, M01 + M02 + M03 = 3900 kg > 3500, krše i R11.
- Nijedan par na A nema zbroj točno 3500 kg (granični slučaj nosivosti A treba testirati hipotetskim podacima).

**Napomena o istovremenosti:** R11 i R12 provjeravaju teret nakon svake radnje. Par
strojeva je relevantan samo ako su oba stroja na kamionu u isto vrijeme, tj. ako je
preuzimanje jednog između preuzimanja i isporuke drugog. Ako se nalog isporuči prije
preuzimanja sljedećeg, isti kamion smije uzastopno prevesti i strojeve čiji zbroj
prelazi nosivost (npr. A smije prevesti M04 i M05 jedan za drugim, ali ne istovremeno).

### 3.4 Kombinacije strojeva na kamionu B (10000 kg, najviše 3 stroja)

**M10 (9400 kg):** najlakši drugi stroj je M01 (800 kg); 9400 + 800 = 10200 > 10000.
Dakle **M10 ne može dijeliti kamion B ni s jednim drugim strojem istovremeno.**
Posljedica za redoslijed: radnja preuzimanja N10 (L7) mora se izvesti kad je B prazan
(0 kg nakon prethodne radnje), a između preuzimanja N10 i isporuke N10 (L3) B ne smije
izvesti nijedno drugo preuzimanje. Svi nalozi preuzeti prije N10 moraju biti isporučeni
prije preuzimanja N10.

Parovi na B koji **ne** stanu (zbroj > 10000), od ukupno 45 parova:

| Par | Zbroj (kg) |
|-----|-----------:|
| M10 + bilo koji od M01–M09 | od 9400 + 800 = 10200 naviše |
| M09 + M05 | 7500 + 3100 = 10600 |
| M09 + M06 | 7500 + 3900 = 11400 |
| M09 + M07 | 7500 + 4800 = 12300 |
| M09 + M08 | 7500 + 6100 = 13600 |
| M08 + M07 | 6100 + 4800 = 10900 |

Ukupno 9 + 4 + 1 = 14 nedopuštenih parova; svi ostali (31) parovi stanu na B.

Parovi s teškim strojevima koji stanu:
- M09 (7500): s M01 (8300), M02 (8800), M03 (9300), M04 (9900).
- M08 (6100): s M01 (6900), M02 (7400), M03 (7900), M04 (8500), M05 (9200), **M06 (10000 – točno na granici, dopušteno)**.
- M07 (4800): s M01 (5600), M02 (6100), M03 (6600), M04 (7200), M05 (7900), M06 (8700).

Trojke na B (najviše 3 stroja):
- Bez M07–M10: sve trojke iz M01–M06 stanu, jer je najteža M04 + M05 + M06 = 2400 + 3100 + 3900 = 9400 <= 10000.
- S M10: nijedna (već par ne stane).
- S M09: preostala dva stroja smiju imati zajedno najviše 2500 kg; jedini takav par iz M01–M04 je M01 + M02 = 2100 (M01 + M03 = 2600 > 2500). Jedina trojka: **M09 + M01 + M02 = 9600**.
- S M08 (preostalo <= 3900, bez M07, M09, M10): M01+M02 (9200), M01+M03 (8700), M01+M04 (9300), **M01+M05 (10000, granica)**, M02+M03 (9200), M02+M04 (9800). Ne stanu: M02+M05 (4400), M03+M04 (4200), M03+M05, M04+M05, i svi parovi sa M06.
- S M07 (preostalo <= 5200, bez M08, M09, M10): M01+M02, M01+M03, M01+M04, M01+M05 (3900), M01+M06 (4700), M02+M03, M02+M04, M02+M05 (4400), **M02+M06 (5200, granica: ukupno 10000)**, M03+M04 (4200), M03+M05 (4900). Ne stanu: M03+M06 (5700), M04+M05 (5500), M04+M06 (6300), M05+M06 (7000).
- Granični slučajevi točno 10000 kg (moraju proći validator): M06 + M08; M01 + M05 + M08; M02 + M06 + M07.
- Kršenje samo broja strojeva na B: četiri najlakša M01 + M02 + M03 + M04 = 6300 kg <= 10000, ali 4 > 3 (krši R12, ne R11).

Napomena o istovremenosti iz 3.3 vrijedi i ovdje.

### 3.5 Matrica dionica

Formula: d = sqrt((x2 - x1)^2 + (y2 - y1)^2), zatim `Math.Round(d, 1, MidpointRounding.AwayFromZero)`.

Provjera zaokruživanja za čovjeka: za n = dx^2 + dy^2 i kandidata a.b vrijedi da je
zaokruženo a.b ako je (a.b - 0.05)^2 < n < (a.b + 0.05)^2. Primjer L0–L8: n = 754;
27.45^2 = 753.5025 < 754 < 27.55^2 = 759.0025, pa je dionica 27.5.

Nijedna vrijednost ne pada točno na polovicu (x.x5): korijen cijelog broja koji nije
potpun kvadrat je iracionalan, a jedini potpuni kvadrat je L3–L4 (289 = 17^2). Zato
`AwayFromZero` i `ToEven` daju iste vrijednosti za ove podatke, ali implementacija
mora koristiti `AwayFromZero` (CLAUDE.md). Najbliže granici su L3–L9 (28.6531 -> 28.7)
i L1–L7 (23.3452 -> 23.3) – dobri kandidati za test zaokruživanja.

Lokacije: L0 (0,0), L1 (3,8), L2 (10,11), L3 (17,9), L4 (9,-6), L5 (26,3),
L6 (-7,13), L7 (20,-8), L8 (23,15), L9 (-8,-5).

Izračun svih 45 parova:

| Par | dx | dy | dx^2 + dy^2 | sqrt | Dionica (km) |
|-----|---:|---:|------------:|-----:|-------------:|
| L0–L1 | 3 | 8 | 73 | 8.5440 | 8.5 |
| L0–L2 | 10 | 11 | 221 | 14.8661 | 14.9 |
| L0–L3 | 17 | 9 | 370 | 19.2354 | 19.2 |
| L0–L4 | 9 | -6 | 117 | 10.8167 | 10.8 |
| L0–L5 | 26 | 3 | 685 | 26.1725 | 26.2 |
| L0–L6 | -7 | 13 | 218 | 14.7648 | 14.8 |
| L0–L7 | 20 | -8 | 464 | 21.5407 | 21.5 |
| L0–L8 | 23 | 15 | 754 | 27.4591 | 27.5 |
| L0–L9 | -8 | -5 | 89 | 9.4340 | 9.4 |
| L1–L2 | 7 | 3 | 58 | 7.6158 | 7.6 |
| L1–L3 | 14 | 1 | 197 | 14.0357 | 14.0 |
| L1–L4 | 6 | -14 | 232 | 15.2315 | 15.2 |
| L1–L5 | 23 | -5 | 554 | 23.5372 | 23.5 |
| L1–L6 | -10 | 5 | 125 | 11.1803 | 11.2 |
| L1–L7 | 17 | -16 | 545 | 23.3452 | 23.3 |
| L1–L8 | 20 | 7 | 449 | 21.1896 | 21.2 |
| L1–L9 | -11 | -13 | 290 | 17.0294 | 17.0 |
| L2–L3 | 7 | -2 | 53 | 7.2801 | 7.3 |
| L2–L4 | -1 | -17 | 290 | 17.0294 | 17.0 |
| L2–L5 | 16 | -8 | 320 | 17.8885 | 17.9 |
| L2–L6 | -17 | 2 | 293 | 17.1172 | 17.1 |
| L2–L7 | 10 | -19 | 461 | 21.4709 | 21.5 |
| L2–L8 | 13 | 4 | 185 | 13.6015 | 13.6 |
| L2–L9 | -18 | -16 | 580 | 24.0832 | 24.1 |
| L3–L4 | -8 | -15 | 289 | 17.0000 | 17.0 |
| L3–L5 | 9 | -6 | 117 | 10.8167 | 10.8 |
| L3–L6 | -24 | 4 | 592 | 24.3311 | 24.3 |
| L3–L7 | 3 | -17 | 298 | 17.2627 | 17.3 |
| L3–L8 | 6 | 6 | 72 | 8.4853 | 8.5 |
| L3–L9 | -25 | -14 | 821 | 28.6531 | 28.7 |
| L4–L5 | 17 | 9 | 370 | 19.2354 | 19.2 |
| L4–L6 | -16 | 19 | 617 | 24.8395 | 24.8 |
| L4–L7 | 11 | -2 | 125 | 11.1803 | 11.2 |
| L4–L8 | 14 | 21 | 637 | 25.2389 | 25.2 |
| L4–L9 | -17 | 1 | 290 | 17.0294 | 17.0 |
| L5–L6 | -33 | 10 | 1189 | 34.4819 | 34.5 |
| L5–L7 | -6 | -11 | 157 | 12.5300 | 12.5 |
| L5–L8 | -3 | 12 | 153 | 12.3693 | 12.4 |
| L5–L9 | -34 | -8 | 1220 | 34.9285 | 34.9 |
| L6–L7 | 27 | -21 | 1170 | 34.2053 | 34.2 |
| L6–L8 | 30 | 2 | 904 | 30.0666 | 30.1 |
| L6–L9 | -1 | -18 | 325 | 18.0278 | 18.0 |
| L7–L8 | 3 | 23 | 538 | 23.1948 | 23.2 |
| L7–L9 | -28 | 3 | 793 | 28.1603 | 28.2 |
| L8–L9 | -31 | -20 | 1361 | 36.8917 | 36.9 |

Simetrična matrica (km, zaokruženo; dijagonala 0 prema R15):

|    | L0 | L1 | L2 | L3 | L4 | L5 | L6 | L7 | L8 | L9 |
|----|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| L0 | 0.0 | 8.5 | 14.9 | 19.2 | 10.8 | 26.2 | 14.8 | 21.5 | 27.5 | 9.4 |
| L1 | 8.5 | 0.0 | 7.6 | 14.0 | 15.2 | 23.5 | 11.2 | 23.3 | 21.2 | 17.0 |
| L2 | 14.9 | 7.6 | 0.0 | 7.3 | 17.0 | 17.9 | 17.1 | 21.5 | 13.6 | 24.1 |
| L3 | 19.2 | 14.0 | 7.3 | 0.0 | 17.0 | 10.8 | 24.3 | 17.3 | 8.5 | 28.7 |
| L4 | 10.8 | 15.2 | 17.0 | 17.0 | 0.0 | 19.2 | 24.8 | 11.2 | 25.2 | 17.0 |
| L5 | 26.2 | 23.5 | 17.9 | 10.8 | 19.2 | 0.0 | 34.5 | 12.5 | 12.4 | 34.9 |
| L6 | 14.8 | 11.2 | 17.1 | 24.3 | 24.8 | 34.5 | 0.0 | 34.2 | 30.1 | 18.0 |
| L7 | 21.5 | 23.3 | 21.5 | 17.3 | 11.2 | 12.5 | 34.2 | 0.0 | 23.2 | 28.2 |
| L8 | 27.5 | 21.2 | 13.6 | 8.5 | 25.2 | 12.4 | 30.1 | 23.2 | 0.0 | 36.9 |
| L9 | 9.4 | 17.0 | 24.1 | 28.7 | 17.0 | 34.9 | 18.0 | 28.2 | 36.9 | 0.0 |

### 3.6 Dionica preuzimanje -> isporuka po nalogu

Vrijednosti iz matrice 3.5:

| Nalog | Stroj | Dionica | km |
|-------|-------|---------|---:|
| N01 | M01 | L1 -> L3 | 14.0 |
| N02 | M02 | L2 -> L5 | 17.9 |
| N03 | M03 | L9 -> L4 | 17.0 |
| N04 | M04 | L3 -> L0 | 19.2 |
| N05 | M05 | L0 -> L8 | 27.5 |
| N06 | M06 | L5 -> L7 | 12.5 |
| N07 | M07 | L8 -> L2 | 13.6 |
| N08 | M08 | L4 -> L9 | 17.0 |
| N09 | M09 | L6 -> L1 | 11.2 |
| N10 | M10 | L7 -> L3 | 17.3 |

### 3.7 Samostalna izvedivost svakog naloga (provjera za kategoriju K2)

Samostalna ruta L0 -> preuzimanje -> isporuka -> L0 (prema D7, K2). To nije
prijedlog plana, nego dokaz da je svaki nalog sam za sebe izvediv.

| Nalog | L0 -> P | P -> I | I -> L0 | Vožnja (km = min) | Total: vožnja + 2 x 20 min | <= 540? |
|-------|--------:|-------:|--------:|------------------:|---------------------------:|:-------:|
| N01 | 8.5 | 14.0 | 19.2 | 41.7 | 81.7 | da |
| N02 | 14.9 | 17.9 | 26.2 | 59.0 | 99.0 | da |
| N03 | 9.4 | 17.0 | 10.8 | 37.2 | 77.2 | da |
| N04 | 19.2 | 19.2 | 0.0 | 38.4 | 78.4 | da |
| N05 | 0.0 | 27.5 | 27.5 | 55.0 | 95.0 | da |
| N06 | 26.2 | 12.5 | 21.5 | 60.2 | 100.2 | da |
| N07 | 27.5 | 13.6 | 14.9 | 56.0 | 96.0 | da |
| N08 | 10.8 | 17.0 | 9.4 | 37.2 | 77.2 | da |
| N09 | 14.8 | 11.2 | 8.5 | 34.5 | 74.5 | da |
| N10 | 21.5 | 17.3 | 19.2 | 58.0 | 98.0 | da |

Zaključak: u oba načina radnog dana svaki nalog je sam za sebe izvediv na svakom
kamionu koji smije nositi njegov stroj; kategorije K1 i K2 se u zadanim podacima ne
pojavljuju. Za N04 i N05 jedna od dionica je 0 km jer je jedan kraj u L0 (R15).

### 3.8 Proračun radnog dana (posljedice D1)

- Svaki dodijeljeni nalog daje 2 radnje (R4) = 2 x 20 = 40 min radnji.
- 10 naloga = 20 radnji = **400 min radnji ukupno**, raspodijeljeno po kamionima prema
  broju naloga: kamion s k naloga ima 40 x k min radnji.
- A smije nositi samo N01–N05 (3.2), pa A ima najviše 5 naloga, a B (ako se izvršavaju
  svi nalozi) najmanje 5 naloga (N06–N10) = najmanje 200 min radnji.

Proračun vožnje po kamionu ovisno o broju naloga k:

| k (naloga na kamionu) | Radnje (min) | `Total`: preostalo za vožnju (540 - 40k) | `DrivingOnly`: dopuštena vožnja | `DrivingOnly`: ukupno trajanje dana najviše (540 + 40k) |
|---:|---:|---:|---:|---:|
| 1 | 40 | 500 | 540 | 580 |
| 2 | 80 | 460 | 540 | 620 |
| 3 | 120 | 420 | 540 | 660 |
| 4 | 160 | 380 | 540 | 700 |
| 5 | 200 | 340 | 540 | 740 |
| 6 | 240 | 300 | 540 | 780 |
| 7 | 280 | 260 | 540 | 820 |
| 8 | 320 | 220 | 540 | 860 |
| 9 | 360 | 180 | 540 | 900 |
| 10 | 400 | 140 | 540 | 940 |

Posljedice:
- **`Total` (zadano, R20):** radnje i vožnja dijele istih 540 min. Svaki dodatni nalog
  na kamionu smanjuje proračun vožnje za 40 min. Sama trajanja radnji ograničavaju kamion
  na najviše floor(540 / 40) = 13 naloga (uz 0 km, što je nemoguće), pa u ovim podacima
  (10 naloga) ograničenje proizlazi iz kombinacije vožnje i radnji, ne iz radnji samih.
  Ako A preuzme a naloga (a <= 5), B ima 10 - a naloga i proračun vožnje B je
  540 - 40 x (10 - a), dakle od 140 min (a = 0) do 340 min (a = 5); A ima proračun
  vožnje 540 - 40a, najmanje 340 min.
- **`DrivingOnly` (alternativa, R21):** vožnja svakog kamiona do 540 min bez obzira na
  broj naloga; radnje ne ograničavaju plan, pa ukupno trajanje dana kamiona može doseći
  540 + 40k min (npr. 940 min = 15 h 40 min za kamion sa svih 10 naloga). Skup izvedivih
  planova je nadskup skupa izvedivih planova u `Total` načinu, pa minimum km u
  `DrivingOnly` nije veći nego u `Total`.
- Ova specifikacija ne računa je li konkretna raspodjela izvediva ni koja je optimalna.

---

## 4. Rubni slučajevi

Svaki slučaj navodi očekivano ponašanje i pravilo koje ga pokriva.

| ID | Slučaj | Očekivano ponašanje | Pravila / D |
|----|--------|---------------------|-------------|
| RS1 | **Preuzimanje u bazi (N05, L0 -> L8).** | Ruta i dalje počinje u L0 (polazak nije radnja). Ako je N05 prva radnja, dionica L0 -> L0 ima 0 km, ali radnja preuzimanja traje 20 min i ulazi u `Total`. Teret nakon radnje: +3100 kg, +1 stroj. Na A: dok je M05 ukrcan, A ne smije nositi ništa drugo (3.3). Ako N05 nije prva radnja, kamion se vraća u L0 usred rute radi radnje (dopušteno prema D4). | R2, R15, R18, R19, R11, D4 |
| RS2 | **Isporuka u bazu (N04, L3 -> L0).** | Isporuka u L0 je radnja od 20 min i smanjuje teret (-2400 kg, -1 stroj). Završni povratak u L0 nije radnja (0 min radnje). Ako je isporuka N04 zadnja radnja, završna dionica L0 -> L0 ima 0 km, ali isporuka i dalje troši 20 min. Ako nije zadnja, kamion nastavlja dalje iz L0 i kasnije se ponovno vraća u L0. Validator ne smije "spojiti" isporuku u L0 sa završnim povratkom (radnja se broji, povratak ne). | R2, R15, R16, R18, R19 |
| RS3 | Isporuka N04 i preuzimanje N05 uzastopno u L0 na istom kamionu. | Dionica između njih 0 km; 2 radnje = 40 min. Teret se provjerava nakon svake radnje zasebno; redoslijed (isporuka pa preuzimanje) utječe na teret između njih. | R10–R12, R15, R18 |
| RS4 | Neiskorišten kamion. | 0 km, 0 naloga, bez radnji; nije prekršaj; prikaz prema D13. Ukupni km = km drugog kamiona. | R3, R16, D13 |
| RS5 | Kamion s rutom koja ne završava u L0 ili ne počinje u L0 (neispravan plan zadan validatoru). | Validator prijavljuje prekršaj. | R2 |
| RS6 | **Nalog teži od svih kamiona (hipotetski, npr. stroj 12000 kg).** | Nalog se ne dodjeljuje; navodi se na popisu neizvedivih s razlogom K1 i brojkama (12000 kg > 10000 kg B, > 3500 kg A). Ostali nalozi planiraju se normalno. | R13, R23, D7 |
| RS7 | Nalog izvediv samo na B dodijeljen kamionu A (npr. N06 na A). | Validator prijavljuje prekršaj nosivosti (3900 > 3500). | R11, R13 |
| RS8 | M10 istovremeno s bilo kojim drugim strojem na B. | Prekršaj (najmanje 10200 > 10000). Validator mora uhvatiti i slučaj kad je drugi stroj preuzet prije M10 i još nije isporučen. | R11, 3.4 |
| RS9 | Granica nosivosti točno 10000 kg (M06 + M08; M01 + M05 + M08; M02 + M06 + M07). | Dopušteno, nije prekršaj. | R11, D9 |
| RS10 | Prekoračenje samo broja strojeva (4 stroja na B, npr. M01–M04 = 6300 kg; 3 stroja na A). | Prekršaj R12 i kad masa nije prekoračena. | R12 |
| RS11 | Isporuka prije preuzimanja u ruti. | Prekršaj redoslijeda (i teret bi postao negativan). | R6, R9 |
| RS12 | Preuzimanje i isporuka istog naloga na različitim kamionima. | Prekršaj. | R5 |
| RS13 | Nalog dvaput u planu, nalog samo s jednom radnjom, ili nalog ni u planu ni na popisu neizvedivih. | Prekršaj. | R4, R7, R23 |
| RS14 | Teret na kraju rute različit od 0. | Prekršaj (posljedica nepotpunog naloga). | R28 |
| RS15 | **Prekoračenje radnog dana.** | U `Total`: prekršaj ako vožnja + 20 x radnje > 540. U `DrivingOnly`: prekršaj ako vožnja > 540. Test mora imati plan koji prolazi `DrivingOnly`, a pada `Total` (vožnja <= 540, vožnja + radnje > 540). Točno 540 min je dopušteno. | R20–R22, D12 |
| RS16 | **Radni dan prekratak za sve naloge (hipotetski, npr. dodatni udaljeni nalozi ili više naloga nego što stane u 2 x 540 min).** | Svaki nalog koji je sam za sebe izvediv ostaje kandidat; solver bira skup naloga prema D5/D6 (najviše izvršenih, zatim najmanje km, deterministički među jednakima). Neizvršeni nalozi navode se kao neizvedivi s razlogom K3 ("ne stane u radni dan uz ostale naloge") i podatkom o načinu radnog dana. | R23, R24, D5, D6, D7, D16 |
| RS17 | Nalog sam za sebe dulji od radnog dana (hipotetska lokacija udaljena npr. 300 km). | Neizvediv s razlogom K2 i brojkama (potrebne minute prema odabranom načinu vs 540). Rezultat ovisi o načinu: može biti izvediv u `DrivingOnly`, a neizvediv u `Total`. | R20, R21, R23, D7 |
| RS18 | Više radnji na istoj lokaciji uzastopno (npr. L3: isporuka N01, preuzimanje N04, isporuka N10). | Dionice 0 km, svaka radnja 20 min, kapacitet se provjerava nakon svake radnje. | R10–R12, R15, R18 |
| RS19 | Prazan popis naloga. | Oba kamiona neiskorištena, ukupno 0 km, popis neizvedivih prazan. | R3, R16, R23 |
| RS20 | Ruta s "praznim" međuposjetom (točka bez radnje, uključujući L0 usred rute). | Prema prijedlogu D4: prekršaj / nije dopušteno. | D4 |
| RS21 | Aritmetika pomičnog zareza (zbroj mnogo dionica s 1 decimalom, usporedba s 540). | Rezultat km prikazan na 1 decimalu; usporedbe s tolerancijom; granični slučaj točno 540.0 prolazi. | R14, R16, D12 |
| RS22 | Više planova jednakih km. | Uvijek isti plan za isti ulaz. | D16 |
| RS23 | **Neispravni ulazni podaci** (prema D14) – vidi izmijenjeni R37 (faza 5): ulaz se više ne odbija, neispravni nalozi su K4: nepostojeći stroj u nalogu; nepostojeća lokacija preuzimanja ili isporuke; duplikat ID-a naloga, stroja, lokacije ili kamiona; negativna ili nulta masa stroja; negativna ili nulta nosivost ili max. broj strojeva; preuzimanje == isporuka; isti stroj u više naloga; nedostaje baza L0; prazan popis kamiona. | (izmijenjeno prema T31) Ulaz se nikad ne odbija – planer uvijek vraća plan (R37, R40). Nalog s neispravnim podacima (uključujući dupliciran ID stroja ili lokacije, masu <= 0, isti stroj u više naloga) je neizvediv (K4) s razlogom, ostali se planiraju. Kamion s nosivošću ili max. brojem strojeva <= 0 ili s već korištenim ID-om ne koristi se; ako nijedan upotrebljiv kamion (ni kad je popis kamiona prazan) ne može nositi stroj, nalog je K1. Ako baza L0 nedostaje, svi nalozi su K4 i nijedan kamion nema radnju. Samo neispravne `PlanningOptions` bacaju `ArgumentException` (greška programa). Nijedna greška ulaznih podataka ne smije izazvati neobrađenu iznimku ni tihi pad. | R23, R37, R40, D7, D14 |
| RS24 | Zaokruživanje dionica blizu polovice (L3–L9 = 28.6531 -> 28.7; L1–L7 = 23.3452 -> 23.3) i hipotetska točna polovica (npr. 0.25 -> 0.3 uz `AwayFromZero`). | Dionica zaokružena prije zbrajanja; `AwayFromZero` na točnoj polovici. | R14 |
| RS25 | Km kamiona računat zbrajanjem nezaokruženih udaljenosti pa zaokruživanjem zbroja. | Pogrešno; test mora uspoređivati sa zbrojem zaokruženih dionica. | R14, R16 |
