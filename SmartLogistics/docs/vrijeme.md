# Utrošeno vrijeme

**Ukupno: oko 6 h** zajedničkog rada (tehnički vođa + Claude Code) u dvije sesije:

- razvoj: 20.11.2026. od 22:07 do 21.11.2026. 02:05 (~3 h 58 min);
- priprema predaje: 21.11.2026. od 09:21 do ~11:20 (~2 h).

Vrijeme je izmjereno iz vremenskih oznaka (git commitovi, datoteke u repozitoriju i zapis sesije
Claude Codea), ne procijenjeno napamet. Ne uključuje vrijeme izvan sesije – čitanje koda radi
razumijevanja, pripremu prezentacije, deploy na Azure i demo. To dopiši u posljednji redak tablice.

## Po fazama

| Faza | Od – do | Trajanje | Što je napravljeno | Izvor oznake |
|------|---------|---------:|--------------------|--------------|
| Priprema | 22:07 – 22:16 | 9 min | Visual Studio rješenje (kasnije zamijenjeno) | datoteke VS predloška |
| 0 – Kostur | 22:16 – 22:24 | 8 min | .NET rješenje, CLAUDE.md, zadatak, 6 podagenata, prelazak na .NET 10 | `docs/zadatak.md`, `.claude/agents/` |
| 1 – Specifikacija | 22:24 – 22:50 | 26 min | domain-analyst: `spec.md`; potvrda pretpostavki; zapis odluka; stranica Dokumentacija | commit `e9e1313` |
| UI – Dizajn sustav | 22:50 – 23:09 | 19 min | bočni izbornik, kartice, ikone, tokeni, `ui-dizajn.md` | commit `3568821` |
| 2 – Domena i udaljenosti | 23:09 – 23:17 | 8 min | `Core/Domain`, `Distance`, `scenario.json`, loader, testovi udaljenosti, stranica Ulazni podaci | commit `11233d7` |
| 3 – Validator i testovi | 23:17 – 23:33 | 16 min | validator-engineer i test-engineer paralelno; stranica Pravila i provjere | commit `f04f0da` |
| 4 – Egzaktni planer | 23:33 – 23:51 | 18 min | solver-engineer: `ExactDpPlanner`; stranice Plan ruta i Karta | commit `c858034` |
| D – Dokumentacija predaje | 23:51 – 00:15 | ~24 min | README, rezultati, AI log, utrošeno vrijeme, pregled otvorenih odluka | ova datoteka |
| 5 – Neizvedivi nalozi | 00:15 – 00:42 | ~27 min | `Feasibility.cs`, planer uvijek vraća plan (R37, R40); validator prilagođen; 50 novih testova s neovisnim oracleom; primjeri scenarija u aplikaciji (kasnije, u fazi 6b, zamijenjeni uređivanjem naloga); objašnjenje planera (`planer.md`) | ova datoteka |
| 6a – Spremište i ViewModeli | 00:43 – 01:03 | 20 min | ui-builder: `IScenarioStore` (u memoriji), layout s označenim regijama, sve klase s prefiksom `sl-`, Transport ViewModeli i mapper | zapis sesije |
| 6b, 6c – Stranice | 01:03 – 01:26 | 23 min | ui-builder: Ulazni podaci (`/`), uređivanje naloga (`/Orders`), Plan prijevoza (`/Plan`) s tablicama i SVG kartom | zapis sesije |
| 7 – OR-Tools planer | 01:25 – 01:57 | 32 min | solver-engineer: `OrToolsPlanner` (T54–T58); ui-builder: izbor algoritma na `/Plan`; test-engineer: `OrToolsPlannerTests` (T60) | zapis sesije |
| Testovi Web mappera | 01:27 – 01:34 | 7 min (paralelno sa 7) | test-engineer: `FormatTests`, `PlanMapperTests`, `MapViewModelTests` | zapis sesije |
| 9 – README | 01:28 – 02:00 | paralelno sa 7 | novi README s brojkama iz pokrenute aplikacije; odjeljci "AI tools" i "Time spent" ostavljeni prazni za tehničkog vođu | zapis sesije |
| 4b – Referentni oracle | 01:38 – 01:43 | 5 min (paralelno sa 7) | test-engineer: `ReferenceOracle` (Dijkstra), 20 nasumičnih scenarija × 2 načina | zapis sesije |
| 8 – Recenzija | 01:53 – 02:03 | 10 min (paralelno sa 7) | reviewer: pregled cijelog repozitorija, samo izvještaj (otvoreni nalazi: T61–T63) | zapis sesije |
| 10 – Završna provjera | 02:03 – 02:05 | 2 min | čisti klon, build, test, popis stavki predaje; commit svih faza od 6a | commit `3d3b74a` |
| **Ukupno u sesiji razvoja** | 22:07 – 02:05 | **~3 h 58 min** | | |
| P – Priprema predaje | 21.11. 09:21 – ~11:20 | ~2 h | README premješten u korijen repozitorija (vidljiv na GitHubu); snimke zaslona za README; spajanje grana `main` i `master`; popunjeni odjeljci README-a o AI alatima i vremenu; dopuna ove datoteke i AI loga | commitovi `6d40f9f`, `d8b1153`, `dd90cd8` i sljedeći |
| **Ukupno sa Claude Codeom** | | **~6 h** | | |
| Izvan sesije (dopisati) | | … | čitanje i razumijevanje koda, priprema prezentacije, deploy na Azure, demo | – |

## Napomene

- Faze s podagentima (1, 3, 4) trajale su 5–8 minuta strojnog rada po agentu. Faza 3 je dva agenta
  pokrenula paralelno pa je trajala koliko i jedan.
- Od faze 6 nadalje više podagenata radilo je paralelno (npr. OR-Tools, testovi Web mappera,
  referentni oracle i recenzija), pa se redovi tablice vremenski preklapaju. Ukupno je stvarno
  proteklo vrijeme, ne zbroj redaka.
- Najviše vremena otišlo je na odluke i provjere (specifikacija, ugovor validatora, provjera
  rezultata), a ne na pisanje koda: kod su pisali Claude Code i podagenti, a odluke i provjere tehnički vođa.
