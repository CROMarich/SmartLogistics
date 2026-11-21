# Utrošeno vrijeme

**Ukupno: oko 2 h 15 min** efektivnog zajedničkog rada (tehnički vođa + Claude Code) u dvije sesije:

- razvoj (20.–21.11.2026.): ~1 h 20 min;
- priprema predaje i faza 11 (21.11.2026.): ~55 min.

Trajanja su procjena efektivnog rada: oko trećine vremena proteklog između vremenskih oznaka (git
commitovi, datoteke u repozitoriju, zapis sesije Claude Codea). Ostatak su čekanje na podagente i
pauze. Ne uključuje vrijeme izvan sesije – čitanje koda radi razumijevanja, pripremu prezentacije,
deploy na Azure i demo. To dopiši u posljednji redak tablice.

## Po fazama

| Faza | Efektivno | Što je napravljeno | Izvor oznake |
|------|----------:|--------------------|--------------|
| Priprema | 3 min | Visual Studio rješenje (kasnije zamijenjeno) | datoteke VS predloška |
| 0 – Kostur | 3 min | .NET rješenje, CLAUDE.md, zadatak, 6 podagenata, prelazak na .NET 10 | `docs/zadatak.md`, `.claude/agents/` |
| 1 – Specifikacija | 9 min | domain-analyst: `spec.md`; potvrda pretpostavki; zapis odluka; stranica Dokumentacija | commit `e9e1313` |
| UI – Dizajn sustav | 6 min | bočni izbornik, kartice, ikone, tokeni, `ui-dizajn.md` | commit `3568821` |
| 2 – Domena i udaljenosti | 3 min | `Core/Domain`, `Distance`, `scenario.json`, loader, testovi udaljenosti, stranica Ulazni podaci | commit `11233d7` |
| 3 – Validator i testovi | 5 min | validator-engineer i test-engineer paralelno; stranica Pravila i provjere | commit `f04f0da` |
| 4 – Egzaktni planer | 6 min | solver-engineer: `ExactDpPlanner`; stranice Plan ruta i Karta | commit `c858034` |
| D – Dokumentacija predaje | 8 min | README, rezultati, AI log, utrošeno vrijeme, pregled otvorenih odluka | commit `fbab07b` |
| 5 – Neizvedivi nalozi | 9 min | `Feasibility.cs`, planer uvijek vraća plan (R37, R40); validator prilagođen; 50 novih testova s neovisnim oracleom; primjeri scenarija u aplikaciji (kasnije, u fazi 6b, zamijenjeni uređivanjem naloga); objašnjenje planera (`planer.md`) | commit `37d11e2` |
| 6a – Spremište i ViewModeli | 7 min | ui-builder: `IScenarioStore` (u memoriji), layout s označenim regijama, sve klase s prefiksom `sl-`, Transport ViewModeli i mapper | zapis sesije |
| 6b, 6c – Stranice | 8 min | ui-builder: Ulazni podaci (`/`), uređivanje naloga (`/Orders`), Plan prijevoza (`/Plan`) s tablicama i SVG kartom | zapis sesije |
| 7 – OR-Tools planer | 11 min | solver-engineer: `OrToolsPlanner` (T54–T58); ui-builder: izbor algoritma na `/Plan`; test-engineer: `OrToolsPlannerTests` (T60) | zapis sesije |
| Testovi Web mappera | paralelno sa 7 | test-engineer: `FormatTests`, `PlanMapperTests`, `MapViewModelTests` | zapis sesije |
| 9 – README | paralelno sa 7 | novi README s brojkama iz pokrenute aplikacije; odjeljci "AI tools" i "Time spent" ostavljeni prazni za tehničkog vođu | zapis sesije |
| 4b – Referentni oracle | paralelno sa 7 | test-engineer: `ReferenceOracle` (Dijkstra), 20 nasumičnih scenarija × 2 načina | zapis sesije |
| 8 – Recenzija | paralelno sa 7 | reviewer: pregled cijelog repozitorija, samo izvještaj (otvoreni nalazi: T61–T63) | zapis sesije |
| 10 – Završna provjera | 1 min | čisti klon, build, test, popis stavki predaje; commit svih faza od 6a | commit `3d3b74a` |
| **Ukupno u sesiji razvoja** | **~1 h 20 min** | | |
| P – Priprema predaje | ~40 min | README premješten u korijen repozitorija (vidljiv na GitHubu); snimke zaslona za README; spajanje grana `main` i `master`; popunjeni odjeljci README-a o AI alatima i vremenu; dopuna ove datoteke i AI loga | commitovi `6d40f9f`, `d8b1153`, `dd90cd8`, `72e4804` |
| 11 – Usklađivanje, testni scenariji, UpRent tema | ~15 min | čišćenje repozitorija (T64), izračunati optimum na Pregledu (T61), testovi K2/R37/R33 (T62), usklađivanje (T63), stranica Testni scenariji, UpRent tema i poveznica na web, GIF za README, PDF za prijavu prema EU, recenzija i popravci nalaza; pet podagenata paralelno | zapis sesije, commitovi faze 11 |
| **Ukupno sa Claude Codeom** | **~2 h 15 min** | | |
| Izvan sesije (dopisati) | … | čitanje i razumijevanje koda, priprema prezentacije, deploy na Azure, demo | – |

## Napomene

- Faze s podagentima (1, 3, 4) trajale su 5–8 minuta strojnog rada po agentu. Faza 3 je dva agenta
  pokrenula paralelno pa je trajala koliko i jedan.
- Od faze 6 nadalje više podagenata radilo je paralelno (testovi Web mappera, referentni oracle,
  README i recenzija uz fazu 7), pa nemaju zasebno vrijeme.
- Najviše vremena otišlo je na odluke i provjere (specifikacija, ugovor validatora, provjera
  rezultata), a ne na pisanje koda: kod su pisali Claude Code i podagenti, a odluke i provjere tehnički vođa.
